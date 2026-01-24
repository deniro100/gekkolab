namespace GekkoLab.Services.GekkoDetector;

/// <summary>
/// Provider for gecko detector, switching between ONNX and simulator
/// based on configuration
/// </summary>
public interface IGekkoDetectorProvider
{
    IGekkoDetector GetDetector();
}

public class GekkoDetectorProvider : IGekkoDetectorProvider
{
    private readonly Lazy<IGekkoDetector> _detector;

    public GekkoDetectorProvider(
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _detector = new Lazy<IGekkoDetector>(() =>
        {
            var logger = loggerFactory.CreateLogger<GekkoDetectorProvider>();
            var useSimulator = configuration.GetValue<bool>("GekkoDetector:UseSimulator", true);
            var modelPath = configuration.GetValue<string>("GekkoDetector:ModelPath", "models/gekko_detector.onnx");

            // Use simulator if configured or if model doesn't exist
            if (useSimulator || !File.Exists(modelPath))
            {
                if (!useSimulator && !File.Exists(modelPath))
                {
                    logger.LogWarning("ONNX model not found at {ModelPath}, falling back to simulator", modelPath);
                }
                else
                {
                    logger.LogInformation("Using Gecko Detector Simulator");
                }
                return new SimulatorGekkoDetector(loggerFactory.CreateLogger<SimulatorGekkoDetector>());
            }

            logger.LogInformation("Using ONNX Gecko Detector with model: {ModelPath}", modelPath);
            return new OnnxGekkoDetector(
                loggerFactory.CreateLogger<OnnxGekkoDetector>(),
                configuration);
        });
    }

    public IGekkoDetector GetDetector() => _detector.Value;
}
