namespace GekkoLab.Models;

public class SensorReading
{
    public int Id { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Pressure { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}