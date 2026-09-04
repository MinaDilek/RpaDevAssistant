using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Core.Analysis.Profiles;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/rule-profiles")]
public sealed class UiPathRuleProfilesController : ControllerBase
{
    private readonly IUiPathRuleProfileProvider profileProvider;
    private readonly IUiPathRuleProfileRepository profileRepository;

    public UiPathRuleProfilesController(
        IUiPathRuleProfileProvider profileProvider,
        IUiPathRuleProfileRepository profileRepository)
    {
        this.profileProvider = profileProvider;
        this.profileRepository = profileRepository;
    }

    [HttpGet]
    public IActionResult GetProfiles()
    {
        return Ok(profileProvider.GetProfiles());
    }

    [HttpGet("{id}")]
    public IActionResult GetProfile(string id)
    {
        try
        {
            return Ok(profileProvider.GetProfile(id));
        }
        catch (UnknownRuleProfileException ex)
        {
            return NotFound(new { error = ex.Message, profileId = ex.ProfileId });
        }
    }

    [HttpPost]
    public IActionResult SaveProfile([FromBody] UiPathRuleProfile profile)
    {
        try
        {
            return Ok(profileRepository.SaveProfile(profile));
        }
        catch (UiPathRuleProfileValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpGet("export")]
    public ActionResult<UiPathRuleProfileStore> ExportProfiles()
    {
        return Ok(profileRepository.ExportProfiles());
    }
}
