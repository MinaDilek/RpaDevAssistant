using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Api.Requests;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Scanning;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/custom-rules")]
public sealed class UiPathCustomRulesController : ControllerBase
{
    private readonly IUiPathCustomRuleRepository repository;
    private readonly IUiPathCustomRuleValidator validator;
    private readonly IUiPathCustomRuleEvaluator evaluator;
    private readonly IUiPathProjectScanner scanner;

    public UiPathCustomRulesController(
        IUiPathCustomRuleRepository repository,
        IUiPathCustomRuleValidator validator,
        IUiPathCustomRuleEvaluator evaluator,
        IUiPathProjectScanner scanner)
    {
        this.repository = repository;
        this.validator = validator;
        this.evaluator = evaluator;
        this.scanner = scanner;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<UiPathCustomRuleDefinition>> GetRules()
    {
        return Ok(repository.GetRules());
    }

    [HttpGet("{id}")]
    public ActionResult<UiPathCustomRuleDefinition> GetRule(string id)
    {
        var rule = repository.GetRule(id);
        return rule is null ? NotFound(new { error = $"Custom rule '{id}' was not found." }) : Ok(rule);
    }

    [HttpPost]
    public IActionResult SaveRule([FromBody] UiPathCustomRuleDefinition rule)
    {
        try
        {
            var saved = repository.SaveRule(rule);
            return Ok(saved);
        }
        catch (UiPathCustomRuleValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPost("validate")]
    public IActionResult ValidateRule([FromBody] UiPathCustomRuleDefinition rule)
    {
        var existingIds = repository.GetRules()
            .Where(existing => !existing.Id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase))
            .Select(existing => existing.Id);
        return Ok(validator.Validate(rule, existingIds));
    }

    [HttpPost("test")]
    public IActionResult TestRule([FromBody] TestCustomRuleRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        var validation = validator.Validate(request.Rule);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        var scan = scanner.Scan(request.ProjectPath);
        var result = evaluator.Test(new UiPathAnalysisContext { Project = scan }, request.Rule);
        return Ok(result);
    }

    [HttpPost("import")]
    public IActionResult ImportRules([FromBody] ImportCustomRulesRequest request)
    {
        var result = repository.ImportRules(request.Rules, request.Overwrite);
        return result.Errors.Count == 0 ? Ok(result) : BadRequest(result);
    }

    [HttpGet("export")]
    public ActionResult<UiPathCustomRuleStore> ExportRules()
    {
        return Ok(repository.ExportRules());
    }
}
