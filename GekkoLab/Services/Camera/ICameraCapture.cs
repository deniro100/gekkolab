namespace GekkoLab.Services.Camera;

/// <summary>
/// Interface for camera capture operations
/// </summary>
public interface ICameraCapture : IDisposable
{
    /// <summary>
    /// Captures a single frame from the camera
    /// </summary>
    /// <returns>Image data as byte array, or null if capture failed</returns>
    Task<byte[]?> CaptureFrameAsync();

    /// <summary>
    /// Checks if the camera is available and working
    /// </summary>
    bool IsAvailable { get; }
}
