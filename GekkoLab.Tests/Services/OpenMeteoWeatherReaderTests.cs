using System.Net;
using FluentAssertions;
using GekkoLab.Services.WeatherReader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace GekkoLab.Tests.Services;

[TestClass]
public class OpenMeteoWeatherReaderTests
{
    private Mock<ILogger<OpenMeteoWeatherReader>> _loggerMock = null!;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private IConfiguration _configuration = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<OpenMeteoWeatherReader>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        var configData = new Dictionary<string, string?>
        {
            { "WeatherConfiguration:Latitude", "47.67" },
            { "WeatherConfiguration:Longitude", "-122.12" },
            { "WeatherConfiguration:Location", "Redmond" }
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private HttpClient CreateMockHttpClient(HttpResponseMessage response)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return new HttpClient(_httpMessageHandlerMock.Object);
    }

    [TestMethod]
    public async Task GetCurrentWeatherAsync_WithValidResponse_ReturnsWeatherData()
    {
        // Arrange
        var jsonResponse = @"{
            ""latitude"": 47.67,
            ""longitude"": -122.12,
            ""current"": {
                ""time"": ""2026-01-24T12:00"",
                ""temperature_2m"": 5.5,
                ""relative_humidity_2m"": 75.0
            }
        }";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        var httpClient = CreateMockHttpClient(response);
        var reader = new OpenMeteoWeatherReader(_loggerMock.Object, _configuration, httpClient);

        // Act
        var result = await reader.GetCurrentWeatherAsync();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Temperature.Should().Be(5.5);
        result.Humidity.Should().Be(75.0);
        result.Latitude.Should().Be(47.67);
        result.Longitude.Should().Be(-122.12);
        result.ErrorMessage.Should().BeNull();
    }

    [TestMethod]
    public async Task GetCurrentWeatherAsync_WithNullCurrentData_ReturnsInvalidResult()
    {
        // Arrange
        var jsonResponse = @"{
            ""latitude"": 47.67,
            ""longitude"": -122.12,
            ""current"": null
        }";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        var httpClient = CreateMockHttpClient(response);
        var reader = new OpenMeteoWeatherReader(_loggerMock.Object, _configuration, httpClient);

        // Act
        var result = await reader.GetCurrentWeatherAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid response");
    }

    [TestMethod]
    public async Task GetCurrentWeatherAsync_WithHttpError_ReturnsInvalidResult()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var httpClient = CreateMockHttpClient(response);
        var reader = new OpenMeteoWeatherReader(_loggerMock.Object, _configuration, httpClient);

        // Act
        var result = await reader.GetCurrentWeatherAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP error");
    }

    [TestMethod]
    public async Task GetCurrentWeatherAsync_WithInvalidJson_ReturnsInvalidResult()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("invalid json")
        };

        var httpClient = CreateMockHttpClient(response);
        var reader = new OpenMeteoWeatherReader(_loggerMock.Object, _configuration, httpClient);

        // Act
        var result = await reader.GetCurrentWeatherAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GetCurrentWeatherAsync_WithEmptyResponse_ReturnsInvalidResult()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        };

        var httpClient = CreateMockHttpClient(response);
        var reader = new OpenMeteoWeatherReader(_loggerMock.Object, _configuration, httpClient);

        // Act
        var result = await reader.GetCurrentWeatherAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid response");
    }

    [TestMethod]
    public void Constructor_UsesDefaultCoordinates_WhenNotConfigured()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var httpClient = new HttpClient();
        
        // Act - Constructor should not throw
        var reader = new OpenMeteoWeatherReader(_loggerMock.Object, emptyConfig, httpClient);

        // Assert - reader created successfully with defaults
        reader.Should().NotBeNull();
    }

    [TestMethod]
    public async Task GetCurrentWeatherAsync_SetsTimestamp()
    {
        // Arrange
        var jsonResponse = @"{
            ""latitude"": 47.67,
            ""longitude"": -122.12,
            ""current"": {
                ""time"": ""2026-01-24T12:00"",
                ""temperature_2m"": 5.5,
                ""relative_humidity_2m"": 75.0
            }
        }";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        var httpClient = CreateMockHttpClient(response);
        var reader = new OpenMeteoWeatherReader(_loggerMock.Object, _configuration, httpClient);

        var beforeCall = DateTime.UtcNow;

        // Act
        var result = await reader.GetCurrentWeatherAsync();

        var afterCall = DateTime.UtcNow;

        // Assert
        result.Timestamp.Should().BeOnOrAfter(beforeCall);
        result.Timestamp.Should().BeOnOrBefore(afterCall);
    }

    [TestMethod]
    public async Task GetCurrentWeatherAsync_HandlesNegativeTemperature()
    {
        // Arrange
        var jsonResponse = @"{
            ""latitude"": 47.67,
            ""longitude"": -122.12,
            ""current"": {
                ""time"": ""2026-01-24T12:00"",
                ""temperature_2m"": -15.5,
                ""relative_humidity_2m"": 85.0
            }
        }";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        var httpClient = CreateMockHttpClient(response);
        var reader = new OpenMeteoWeatherReader(_loggerMock.Object, _configuration, httpClient);

        // Act
        var result = await reader.GetCurrentWeatherAsync();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Temperature.Should().Be(-15.5);
    }
}
