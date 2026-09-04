using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/rules")]
public sealed class UiPathRulesController : ControllerBase
{
    private readonly IUiPathRuleCatalogProvider ruleCatalogProvider;

    public UiPathRulesController(IUiPathRuleCatalogProvider ruleCatalogProvider)
    {
        this.ruleCatalogProvider = ruleCatalogProvider;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<UiPathRuleCatalogItem>> GetRules([FromQuery] string? locale = null)
    {
        return Ok(ruleCatalogProvider.GetRules(locale));
    }

    [HttpGet("{id}")]
    public ActionResult<UiPathRuleCatalogItem> GetRule(string id, [FromQuery] string? locale = null)
    {
        var rule = ruleCatalogProvider.GetRules(locale)
            .FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        return rule is null ? NotFound(new { error = $"Rule '{id}' was not found." }) : Ok(rule);
    }
}
