using System.Security.Cryptography;
using System.Text;
using RpaDevAssistant.Api.Controllers;
using RpaDevAssistant.Api.Configuration;
using RpaDevAssistant.Core.Central;

namespace RpaDevAssistant.Api.Services;

public sealed class CentralAccessMiddleware(RequestDelegate next)
{
    public const string PrincipalItemKey = "RpaDevAssistant.CentralPrincipal";

    public async Task InvokeAsync(
        HttpContext context,
        RpaDevAssistantCentralOptions options)
    {
        if (!context.Request.Path.StartsWithSegments("/api/central"))
        {
            await next(context);
            return;
        }

        if (!options.Enabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "CENTRAL_MODE_DISABLED", message = "Central mode is not enabled." });
            return;
        }

        if (context.Request.Path.Equals("/api/central/status"))
        {
            await next(context);
            return;
        }

        var license = context.RequestServices.GetRequiredService<ICentralLicenseService>().GetStatus();
        if (!license.AllowsAccess)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await context.Response.WriteAsJsonAsync(new { error = "CENTRAL_LICENSE_INVALID", state = license.State.ToString(), message = license.Error });
            return;
        }

        var repository = context.RequestServices.GetRequiredService<ICentralCatalogRepository>();
        var apiKey = ReadApiKey(context.Request);
        CentralPrincipal? principal;
        if (IsBootstrapKey(apiKey, options.BootstrapApiKey))
        {
            principal = new CentralPrincipal("bootstrap", "system", CentralRole.SystemAdmin, "Bootstrap Administrator", true);
        }
        else if (options.AuthenticationMode is CentralAuthenticationMode.Oidc or CentralAuthenticationMode.Hybrid
            && context.User.Identity?.IsAuthenticated == true)
        {
            var externalTenant = context.User.FindFirst(options.TenantClaim)?.Value;
            var externalSubject = context.User.FindFirst(options.SubjectClaim)?.Value;
            principal = repository.AuthenticateFederated(externalTenant ?? string.Empty, externalSubject ?? string.Empty);
            if (principal is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "FEDERATED_USER_NOT_PROVISIONED", message = "The authenticated identity is not provisioned for this tenant." });
                return;
            }
        }
        else
        {
            principal = options.AuthenticationMode == CentralAuthenticationMode.Oidc
                ? null
                : repository.Authenticate(apiKey ?? string.Empty);
        }

        if (principal is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "CENTRAL_AUTH_REQUIRED", message = "A valid central API key is required." });
            return;
        }

        context.Items[PrincipalItemKey] = principal;
        try
        {
            await next(context);
        }
        catch (CentralAccessDeniedException)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "CENTRAL_ACCESS_DENIED", message = "The current role cannot perform this operation." });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "CENTRAL_VALIDATION_FAILED", message = ex.Message });
        }
    }

    internal static string? ReadApiKey(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authorization["Bearer ".Length..].Trim();
        var explicitKey = request.Headers["X-RPA-API-Key"].ToString();
        return string.IsNullOrWhiteSpace(explicitKey) ? null : explicitKey.Trim();
    }

    private static bool IsBootstrapKey(string? candidate, string? expected)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(expected)) return false;
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
