namespace GekkoLab.Services.Bme280Reader;

public interface IBme280SensorReaderProvider
{
    IBme280Reader GetReader();
}
