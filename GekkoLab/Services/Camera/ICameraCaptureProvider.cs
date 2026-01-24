namespace GekkoLab.Services.Camera;

/// <summary>
/// Provider interface for camera capture, follows the same pattern as BME280 sensor reader
/// </summary>
public interface ICameraCaptureProvider
{
    /// <summary>
    /// Gets the camera capture instance (hardware or simulator)
    /// </summary>
    ICameraCapture GetCapture();
}
