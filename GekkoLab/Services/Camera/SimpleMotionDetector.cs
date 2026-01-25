using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GekkoLab.Services.Camera;

/// <summary>
/// Motion detector that compares actual pixel data between decoded frames.
/// Decodes JPEG images to avoid false positives from compression artifacts.
/// </summary>
public class SimpleMotionDetector : IMotionDetector
{
    private readonly ILogger<SimpleMotionDetector> _logger;
    
    // Downscale images for faster comparison
    private const int ComparisonWidth = 160;
    private const int ComparisonHeight = 120;

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

        try
        {
            // Decode JPEG images to pixel data
            using var prevImage = Image.Load<Rgb24>(previousFrame);
            using var currImage = Image.Load<Rgb24>(currentFrame);

            // Resize to smaller size for faster comparison
            prevImage.Mutate(x => x.Resize(ComparisonWidth, ComparisonHeight));
            currImage.Mutate(x => x.Resize(ComparisonWidth, ComparisonHeight));

            // Compare pixel data
            long totalDifference = 0;
            int totalPixels = ComparisonWidth * ComparisonHeight;

            for (int y = 0; y < ComparisonHeight; y++)
            {
                for (int x = 0; x < ComparisonWidth; x++)
                {
                    var prevPixel = prevImage[x, y];
                    var currPixel = currImage[x, y];

                    // Calculate difference for each color channel
                    var diffR = Math.Abs(prevPixel.R - currPixel.R);
                    var diffG = Math.Abs(prevPixel.G - currPixel.G);
                    var diffB = Math.Abs(prevPixel.B - currPixel.B);

                    // Average difference across channels
                    totalDifference += (diffR + diffG + diffB) / 3;
                }
            }

            // Calculate average difference as a percentage of max possible difference (255)
            var averageDifference = (double)totalDifference / totalPixels / 255.0;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing frames for motion detection");
            return false;
        }
    }
}
