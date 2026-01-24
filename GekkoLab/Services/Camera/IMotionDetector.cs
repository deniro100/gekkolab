namespace GekkoLab.Services.Camera;

/// <summary>
/// Interface for motion detection functionality
/// </summary>
public interface IMotionDetector
{
    /// <summary>
    /// Detects if there is motion between two frames
    /// </summary>
    /// <param name="previousFrame">Previous frame data</param>
    /// <param name="currentFrame">Current frame data</param>
    /// <returns>True if motion is detected, false otherwise</returns>
    bool DetectMotion(byte[] previousFrame, byte[] currentFrame);

    /// <summary>
    /// Gets or sets the sensitivity threshold (0.0 to 1.0)
    /// Lower values = more sensitive to motion
    /// </summary>
    double Sensitivity { get; set; }
}
