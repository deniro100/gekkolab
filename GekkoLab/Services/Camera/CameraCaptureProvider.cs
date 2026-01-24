namespace GekkoLab.Services.Camera;

/// <summary>
/// Provider for camera capture, switching between hardware and simulator
/// based on configuration (similar to Bme280SensorReaderProvider)
/// </summary>
public class CameraCaptureProvider : ICameraCaptureProvider
{
    private readonly Lazy<ICameraCapture> _capture;

    public CameraCaptureProvider(
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _capture = new Lazy<ICameraCapture>(() =>
        {
            var logger = loggerFactory.CreateLogger<CameraCaptureProvider>();
            var useSimulator = configuration.GetValue<bool>("CameraConfiguration:UseSimulator", false);

            logger.LogInformation("CameraCaptureProvider initializing. UseSimulator={UseSimulator}", useSimulator);

            if (useSimulator)
            {
                logger.LogInformation("Using Camera Simulator");
                return new SimulatorCameraCapture(loggerFactory.CreateLogger<SimulatorCameraCapture>());
            }
            else
            {
                var width = configuration.GetValue<int>("CameraConfiguration:Width", 1280);
                var height = configuration.GetValue<int>("CameraConfiguration:Height", 720);
                var quality = configuration.GetValue<int>("CameraConfiguration:Quality", 85);

                logger.LogInformation("Using Raspberry Pi Camera (rpicam-still) - {Width}x{Height}, Quality: {Quality}", 
                    width, height, quality);
                
                var capture = new RaspberryPiCameraCapture(
                    loggerFactory.CreateLogger<RaspberryPiCameraCapture>(),
                    width, height, quality);
                
                logger.LogInformation("RaspberryPiCameraCapture created. IsAvailable={IsAvailable}", capture.IsAvailable);
                
                return capture;
            }
        });
    }

    public ICameraCapture GetCapture() => _capture.Value;
}
