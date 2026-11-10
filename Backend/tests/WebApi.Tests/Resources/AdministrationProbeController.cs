using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Tests.Resources;

// Exists only in the test host to exercise the application's administration policy.
[ApiController]
[Route("test/administration")]
[Authorize(Policy = "PlatformAdministration")]
public sealed class AdministrationProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
