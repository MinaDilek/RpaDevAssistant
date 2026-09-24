using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Api.Configuration;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/features")]
public sealed class FeaturesController(RpaDevAssistantFeatureFlags featureFlags) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(featureFlags);
}
