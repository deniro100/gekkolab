using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        var buildDate = System.IO.File.GetLastWriteTimeUtc(assembly.Location);
        
        return Ok(new
        {
            version,
            buildDate,
            buildDateLocal = buildDate.ToLocalTime(),
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "unknown",
            machineName = Environment.MachineName,
            osVersion = Environment.OSVersion.ToString(),
            dotnetVersion = Environment.Version.ToString()
        });
    }
}
