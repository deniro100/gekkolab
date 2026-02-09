using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigController> _logger;
    private readonly IWebHostEnvironment _environment;

    public ConfigController(IConfiguration configuration, ILogger<ConfigController> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Get application configuration settings (sanitized - no secrets)
    /// </summary>
    [HttpGet]
    public IActionResult GetConfiguration()
    {
        var config = new
        {
            Logging = new
            {
                LogLevel = new
                {
                    Default = _configuration["Logging:LogLevel:Default"],
                    MicrosoftAspNetCore = _configuration["Logging:LogLevel:Microsoft.AspNetCore"],
                    EntityFramework = _configuration["Logging:LogLevel:Microsoft.EntityFrameworkCore"]
                }
            },
            Kestrel = new
            {
                HttpUrl = _configuration["Kestrel:Endpoints:Http:Url"]
            },
            ConnectionStrings = new
            {
                SQLite = _configuration.GetConnectionString("SQLite")
            },
            SensorConfiguration = new
            {
                PollingInterval = _configuration["SensorConfiguration:PollingInterval"],
                I2CAddress = _configuration["SensorConfiguration:I2CAddress"],
                EnableRetry = _configuration.GetValue<bool>("SensorConfiguration:EnableRetry"),
                MaxRetryAttempts = _configuration.GetValue<int>("SensorConfiguration:MaxRetryAttempts"),
                RetryDelayMs = _configuration.GetValue<int>("SensorConfiguration:RetryDelayMs"),
                UseSimulator = _configuration.GetValue<bool>("SensorConfiguration:UseSimulator")
            },
            CameraConfiguration = new
            {
                UseSimulator = _configuration.GetValue<bool>("CameraConfiguration:UseSimulator"),
                Width = _configuration.GetValue<int>("CameraConfiguration:Width"),
                Height = _configuration.GetValue<int>("CameraConfiguration:Height"),
                Quality = _configuration.GetValue<int>("CameraConfiguration:Quality"),
                MotionDetection = new
                {
                    Enabled = _configuration.GetValue<bool>("CameraConfiguration:MotionDetection:Enabled"),
                    PollingInterval = _configuration["CameraConfiguration:MotionDetection:PollingInterval"],
                    MinCaptureInterval = _configuration["CameraConfiguration:MotionDetection:MinCaptureInterval"],
                    Sensitivity = _configuration.GetValue<double>("CameraConfiguration:MotionDetection:Sensitivity"),
                    CaptureDirectory = _configuration["CameraConfiguration:MotionDetection:CaptureDirectory"],
                    MaxCaptureAgeDays = _configuration.GetValue<int>("CameraConfiguration:MotionDetection:MaxCaptureAgeDays"),
                    MaxCaptureFiles = _configuration.GetValue<int>("CameraConfiguration:MotionDetection:MaxCaptureFiles")
                }
            },
            PerformanceMonitoring = new
            {
                Enabled = _configuration.GetValue<bool>("PerformanceMonitoring:Enabled"),
                SnapshotInterval = _configuration["PerformanceMonitoring:SnapshotInterval"],
                AggregationInterval = _configuration["PerformanceMonitoring:AggregationInterval"],
                MaxAgeDays = _configuration.GetValue<int>("PerformanceMonitoring:MaxAgeDays")
            },
            WeatherConfiguration = new
            {
                Enabled = _configuration.GetValue<bool>("WeatherConfiguration:Enabled"),
                PollingInterval = _configuration["WeatherConfiguration:PollingInterval"],
                Location = _configuration["WeatherConfiguration:Location"],
                Latitude = _configuration.GetValue<double>("WeatherConfiguration:Latitude"),
                Longitude = _configuration.GetValue<double>("WeatherConfiguration:Longitude")
            },
            GekkoDetector = new
            {
                Enabled = _configuration.GetValue<bool>("GekkoDetector:Enabled"),
                UseSimulator = _configuration.GetValue<bool>("GekkoDetector:UseSimulator"),
                ModelPath = _configuration["GekkoDetector:ModelPath"],
                PollingInterval = _configuration["GekkoDetector:PollingInterval"],
                InputWidth = _configuration.GetValue<int>("GekkoDetector:InputWidth"),
                InputHeight = _configuration.GetValue<int>("GekkoDetector:InputHeight"),
                ConfidenceThreshold = _configuration.GetValue<double>("GekkoDetector:ConfidenceThreshold")
            },
            AzureConfiguration = new
            {
                Enabled = _configuration.GetValue<bool>("AzureConfiguration:Enabled"),
                PublishIntervalMinutes = _configuration.GetValue<int>("AzureConfiguration:PublishIntervalMinutes")
                // Note: ConnectionString intentionally omitted for security
            },
            TelegramBot = new
            {
                Token = MaskToken(_configuration["TelegramBot:Token"]),
                Configured = !string.IsNullOrWhiteSpace(_configuration["TelegramBot:Token"])
            }
        };

        return Ok(config);
    }

    /// <summary>
    /// Update application configuration settings. Requires restart for changes to take effect.
    /// </summary>
    [HttpPut]
    public IActionResult UpdateConfiguration([FromBody] JsonElement settings)
    {
        try
        {
            var appSettingsPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");

            if (!System.IO.File.Exists(appSettingsPath))
            {
                _logger.LogError("appsettings.json not found at {Path}", appSettingsPath);
                return StatusCode(500, new { message = "Configuration file not found" });
            }

            var json = System.IO.File.ReadAllText(appSettingsPath);
            var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!.AsObject();

            MergeJsonObjects(root, JsonNode.Parse(settings.GetRawText())!.AsObject());

            var writeOptions = new JsonSerializerOptions { WriteIndented = true };
            System.IO.File.WriteAllText(appSettingsPath, root.ToJsonString(writeOptions));

            _logger.LogInformation("Configuration updated successfully");
            return Ok(new { message = "Configuration saved. Restart the service for changes to take full effect." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration");
            return StatusCode(500, new { message = "Error saving configuration" });
        }
    }

    private static void MergeJsonObjects(JsonObject target, JsonObject source)
    {
        foreach (var property in source)
        {
            if (property.Value is JsonObject sourceChild && target[property.Key] is JsonObject targetChild)
            {
                MergeJsonObjects(targetChild, sourceChild);
            }
            else
            {
                target[property.Key] = property.Value?.DeepClone();
            }
        }
    }

    private static string MaskToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "";
        if (token.Length <= 10) return "****";
        return token[..5] + "****" + token[^4..];
    }
}
