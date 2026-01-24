namespace GekkoLab.Models;

/// <summary>
/// Represents a detection result from the ONNX-based gecko detector
/// </summary>
public class GekkoDetectionResult
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public bool GekkoDetected { get; set; }
    public float Confidence { get; set; }
    public string? Label { get; set; }
    public int? BoundingBoxX { get; set; }
    public int? BoundingBoxY { get; set; }
    public int? BoundingBoxWidth { get; set; }
    public int? BoundingBoxHeight { get; set; }
}
