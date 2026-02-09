using Microsoft.AspNetCore.Mvc;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IConfiguration configuration, ILogger<ConfigController> logger)
    {
        _configuration = configuration;
        _logger = logger;
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
            }
        };

        return Ok(config);
    }
}
