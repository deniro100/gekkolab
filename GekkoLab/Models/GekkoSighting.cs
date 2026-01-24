namespace GekkoLab.Models;

/// <summary>
/// Represents a confirmed gecko sighting - stored only when gecko is detected
/// </summary>
public class GekkoSighting
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public float Confidence { get; set; }
    
    // Position in image (if available from detection model)
    public int? PositionX { get; set; }
    public int? PositionY { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    
    // Image dimensions for reference
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    
    // Additional metadata
    public string? Label { get; set; }
    public string? Notes { get; set; }
}
