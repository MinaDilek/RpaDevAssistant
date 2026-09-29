using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Api.Configuration;
using RpaDevAssistant.Api.Services;
using RpaDevAssistant.Core.Central;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/scim/v2")]
public sealed partial class ScimProvisioningController(
    ICentralCatalogRepository repository,
    ICentralLicenseService licenseService,
    RpaDevAssistantCentralOptions options) : ControllerBase
{
    private const string UserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string ListSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    private const string PatchSchema = "urn:ietf:params:scim:api:messages:2.0:PatchOp";
    private const string ErrorSchema = "urn:ietf:params:scim:api:messages:2.0:Error";

    [HttpGet("ServiceProviderConfig")]
    public IActionResult ServiceProviderConfig() => !options.Enabled ? NotFound() : Ok(new
    {
        schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig" },
        patch = new { supported = true },
        bulk = new { supported = false, maxOperations = 0, maxPayloadSize = 0 },
        filter = new { supported = true, maxResults = 100 },
        changePassword = new { supported = false },
        sort = new { supported = false },
        etag = new { supported = false },
        authenticationSchemes = new[]
        {
            new { type = "oauthbearertoken", name = "Tenant administrator API key", description = "Tenant-scoped SCIM bearer credential", specUri = "https://www.rfc-editor.org/rfc/rfc6750", primary = true }
        }
    });

    [HttpGet("ResourceTypes")]
    public IActionResult ResourceTypes() => !options.Enabled ? NotFound() : Ok(new
    {
        schemas = new[] { ListSchema }, totalResults = 1, startIndex = 1, itemsPerPage = 1,
        Resources = new[] { new { id = "User", name = "User", endpoint = "/Users", schema = UserSchema } }
    });

    [HttpGet("Schemas")]
    public IActionResult Schemas() => !options.Enabled ? NotFound() : Ok(new
    {
        schemas = new[] { ListSchema }, totalResults = 1, startIndex = 1, itemsPerPage = 1,
        Resources = new[]
        {
            new
            {
                id = UserSchema, name = "User", description = "RPA Dev Assistant central user",
                attributes = new object[]
                {
                    new { name = "userName", type = "string", multiValued = false, required = true, uniqueness = "server" },
                    new { name = "displayName", type = "string", multiValued = false, required = false, uniqueness = "none" },
                    new { name = "active", type = "boolean", multiValued = false, required = false, uniqueness = "none" },
                    new { name = "externalId", type = "string", multiValued = false, required = false, uniqueness = "none" }
                }
            }
        }
    });

    [HttpGet("Users")]
    public IActionResult Users([FromQuery] string? filter = null, [FromQuery] int startIndex = 1, [FromQuery] int count = 100)
    {
        var principalResult = TenantAdministrator();
        if (principalResult.Error is not null) return principalResult.Error;
        if (startIndex < 1 || count is < 1 or > 100) return ScimError(400, "startIndex must be positive and count must be between 1 and 100.", "invalidValue");

        IEnumerable<CentralUser> users = repository.GetSnapshot().Users.Where(item => item.TenantId == principalResult.Principal!.TenantId);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var match = EqualityFilter().Match(filter);
            if (!match.Success) return ScimError(400, "Only userName eq and externalId eq filters are supported.", "invalidFilter");
            var attribute = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            users = attribute.Equals("userName", StringComparison.OrdinalIgnoreCase)
                ? users.Where(item => item.Email.Equals(value, StringComparison.OrdinalIgnoreCase))
                : users.Where(item => string.Equals(item.ExternalSubject, value, StringComparison.Ordinal));
        }

        var all = users.OrderBy(item => item.Email, StringComparer.OrdinalIgnoreCase).ToArray();
        var resources = all.Skip(startIndex - 1).Take(count).Select(ToScimUser).ToArray();
        return Ok(new { schemas = new[] { ListSchema }, totalResults = all.Length, startIndex, itemsPerPage = resources.Length, Resources = resources });
    }

    [HttpGet("Users/{id}")]
    public IActionResult GetUser(string id)
    {
        var principalResult = TenantAdministrator();
        if (principalResult.Error is not null) return principalResult.Error;
        var user = FindUser(principalResult.Principal!.TenantId, id);
        return user is null ? ScimError(404, "The SCIM user was not found.") : Ok(ToScimUser(user));
    }

    [HttpPost("Users")]
    public IActionResult CreateUser([FromBody] ScimUserRequest request)
    {
        var principalResult = TenantAdministrator();
        if (principalResult.Error is not null) return principalResult.Error;
        var validation = ValidateRequest(request);
        if (validation is not null) return validation;
        var tenantId = principalResult.Principal!.TenantId;
        if (repository.GetSnapshot().Users.Any(item => item.Email.Equals(request.UserName, StringComparison.OrdinalIgnoreCase)))
            return ScimError(409, "A user with this userName already exists.", "uniqueness");

        var created = repository.ProvisionFederatedUser(new CentralUser
        {
            Id = $"scim-{Guid.NewGuid():N}", TenantId = tenantId, Email = request.UserName.Trim(),
            DisplayName = DisplayName(request), Role = CentralRole.Viewer, Active = request.Active ?? true,
            ExternalSubject = NullIfWhiteSpace(request.ExternalId), CreatedAtUtc = DateTimeOffset.UtcNow
        });
        Audit(principalResult.Principal, "SCIM.User.Create", created.Id);
        Response.Headers.Location = $"/api/scim/v2/Users/{created.Id}";
        return StatusCode(StatusCodes.Status201Created, ToScimUser(created));
    }

    [HttpPut("Users/{id}")]
    public IActionResult ReplaceUser(string id, [FromBody] ScimUserRequest request)
    {
        var principalResult = TenantAdministrator();
        if (principalResult.Error is not null) return principalResult.Error;
        var validation = ValidateRequest(request);
        if (validation is not null) return validation;
        var existing = FindUser(principalResult.Principal!.TenantId, id);
        if (existing is null) return ScimError(404, "The SCIM user was not found.");

        var updated = repository.ProvisionFederatedUser(existing with
        {
            Email = request.UserName.Trim(), DisplayName = DisplayName(request), Active = request.Active ?? existing.Active,
            ExternalSubject = NullIfWhiteSpace(request.ExternalId)
        });
        Audit(principalResult.Principal, "SCIM.User.Replace", updated.Id);
        return Ok(ToScimUser(updated));
    }

    [HttpPatch("Users/{id}")]
    public IActionResult PatchUser(string id, [FromBody] ScimPatchRequest request)
    {
        var principalResult = TenantAdministrator();
        if (principalResult.Error is not null) return principalResult.Error;
        if (request.Schemas is null || !request.Schemas.Contains(PatchSchema, StringComparer.Ordinal))
            return ScimError(400, "The SCIM PatchOp schema is required.", "invalidSyntax");
        var existing = FindUser(principalResult.Principal!.TenantId, id);
        if (existing is null) return ScimError(404, "The SCIM user was not found.");

        var updated = existing;
        foreach (var operation in request.Operations ?? [])
        {
            if (!operation.Op.Equals("replace", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(operation.Path, "active", StringComparison.OrdinalIgnoreCase)
                || !TryBoolean(operation.Value, out var active))
                return ScimError(400, "Only replace active patch operations are supported.", "invalidValue");
            updated = updated with { Active = active };
        }
        updated = repository.ProvisionFederatedUser(updated);
        Audit(principalResult.Principal, "SCIM.User.Patch", updated.Id);
        return Ok(ToScimUser(updated));
    }

    [HttpDelete("Users/{id}")]
    public IActionResult DeleteUser(string id)
    {
        var principalResult = TenantAdministrator();
        if (principalResult.Error is not null) return principalResult.Error;
        var existing = FindUser(principalResult.Principal!.TenantId, id);
        if (existing is null) return ScimError(404, "The SCIM user was not found.");
        repository.ProvisionFederatedUser(existing with { Active = false });
        Audit(principalResult.Principal, "SCIM.User.Deactivate", existing.Id);
        return NoContent();
    }

    private (CentralPrincipal? Principal, IActionResult? Error) TenantAdministrator()
    {
        if (!options.Enabled) return (null, NotFound());
        var license = licenseService.GetStatus();
        if (!license.AllowsAccess) return (null, StatusCode(StatusCodes.Status402PaymentRequired, new { schemas = new[] { ErrorSchema }, status = "402", detail = license.Error }));
        var apiKey = CentralAccessMiddleware.ReadApiKey(Request);
        var principal = repository.Authenticate(apiKey ?? string.Empty);
        if (principal is null) return (null, ScimError(401, "A valid tenant administrator bearer credential is required."));
        if (principal.Role < CentralRole.TenantAdmin) return (null, ScimError(403, "Tenant administrator permission is required."));
        return (principal, null);
    }

    private CentralUser? FindUser(string tenantId, string id) => repository.GetSnapshot().Users.FirstOrDefault(item => item.TenantId == tenantId && item.Id == id);

    private void Audit(CentralPrincipal principal, string action, string resourceId) => repository.AppendAudit(new CentralAuditEvent
    {
        OperationId = HttpContext.TraceIdentifier, TimestampUtc = DateTimeOffset.UtcNow, TenantId = principal.TenantId,
        ActorUserId = principal.UserId, Action = action, ResourceType = "User", ResourceId = resourceId
    });

    private IActionResult? ValidateRequest(ScimUserRequest request)
    {
        if (request.Schemas is null || !request.Schemas.Contains(UserSchema, StringComparer.Ordinal)) return ScimError(400, "The SCIM User schema is required.", "invalidSyntax");
        if (string.IsNullOrWhiteSpace(request.UserName) || !request.UserName.Contains('@', StringComparison.Ordinal)) return ScimError(400, "userName must be a valid email address.", "invalidValue");
        return null;
    }

    private ObjectResult ScimError(int status, string detail, string? scimType = null) => StatusCode(status, new { schemas = new[] { ErrorSchema }, status = status.ToString(), scimType, detail });
    private static object ToScimUser(CentralUser user) => new
    {
        schemas = new[] { UserSchema }, id = user.Id, externalId = user.ExternalSubject, userName = user.Email,
        displayName = user.DisplayName, active = user.Active,
        emails = new[] { new { value = user.Email, type = "work", primary = true } },
        meta = new { resourceType = "User", created = user.CreatedAtUtc, location = $"/api/scim/v2/Users/{user.Id}" }
    };
    private static string DisplayName(ScimUserRequest request) => NullIfWhiteSpace(request.DisplayName) ?? request.UserName.Trim();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool TryBoolean(JsonElement value, out bool result)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) { result = value.GetBoolean(); return true; }
        return bool.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : null, out result);
    }

    [GeneratedRegex("^\\s*(userName|externalId)\\s+eq\\s+\"([^\"]+)\"\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EqualityFilter();

    public sealed record ScimUserRequest(IReadOnlyList<string>? Schemas, string UserName, string? DisplayName, bool? Active, string? ExternalId);
    public sealed record ScimPatchRequest(IReadOnlyList<string>? Schemas, IReadOnlyList<ScimPatchOperation>? Operations);
    public sealed record ScimPatchOperation(string Op, string? Path, JsonElement Value);
}
