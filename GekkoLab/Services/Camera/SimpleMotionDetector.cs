namespace GekkoLab.Services.Camera;

/// <summary>
/// Simple motion detector that compares byte differences between frames
/// For production use, consider using a more sophisticated algorithm
/// </summary>
public class SimpleMotionDetector : IMotionDetector
{
    private readonly ILogger<SimpleMotionDetector> _logger;

    public SimpleMotionDetector(ILogger<SimpleMotionDetector> logger)
    {
        _logger = logger;
        Sensitivity = 0.05; // Default: 5% difference threshold
    }

    /// <summary>
    /// Sensitivity threshold (0.0 to 1.0)
    /// Lower values = more sensitive (detects smaller changes)
    /// Default is 0.05 (5% difference triggers motion)
    /// </summary>
    public double Sensitivity { get; set; }

    public bool DetectMotion(byte[] previousFrame, byte[] currentFrame)
    {
        if (previousFrame == null || currentFrame == null)
        {
            _logger.LogDebug("Cannot detect motion: one or both frames are null");
            return false;
        }

        if (previousFrame.Length == 0 || currentFrame.Length == 0)
        {
            _logger.LogDebug("Cannot detect motion: one or both frames are empty");
            return false;
        }

        // Compare frames by sampling bytes (for performance)
        // We don't need to compare every byte - sampling is sufficient for motion detection
        var sampleSize = Math.Min(previousFrame.Length, currentFrame.Length);
        var sampleStep = Math.Max(1, sampleSize / 10000); // Sample up to ~10000 points
        
        long totalDifference = 0;
        int sampledPoints = 0;

        for (int i = 0; i < sampleSize; i += sampleStep)
        {
            var diff = Math.Abs(previousFrame[i] - currentFrame[i]);
            totalDifference += diff;
            sampledPoints++;
        }

        if (sampledPoints == 0)
        {
            return false;
        }

        // Calculate average difference as a percentage of max possible difference (255)
        var averageDifference = (double)totalDifference / sampledPoints / 255.0;

        var motionDetected = averageDifference > Sensitivity;

        if (motionDetected)
        {
            _logger.LogDebug("Motion detected! Average difference: {Diff:P2} (threshold: {Threshold:P2})", 
                averageDifference, Sensitivity);
        }
        else
        {
            _logger.LogTrace("No motion. Average difference: {Diff:P2} (threshold: {Threshold:P2})", 
                averageDifference, Sensitivity);
        }

        return motionDetected;
    }
}
