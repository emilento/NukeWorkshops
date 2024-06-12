using Microsoft.Extensions.Logging;
using NSubstitute;
using NukeWorkshops.Server.Controllers;

namespace NukeWorkshops.Server.Tests
{
    public class WeatherForecastControllerTests
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _mockLogger;
        private readonly WeatherForecastController _controller;

        public WeatherForecastControllerTests()
        {
            _mockLogger = Substitute.For<ILogger<WeatherForecastController>>();
            _controller = new WeatherForecastController(_mockLogger);
        }

        [Fact]
        public void Get_ReturnsFiveWeatherForecasts()
        {
            // Act
            var result = _controller.Get();

            // Assert
            Assert.Equal(5, result.Count());
        }

        [Fact]
        public void Get_ReturnsWeatherForecastsWithValidDate()
        {
            // Act
            var result = _controller.Get();

            // Assert
            foreach (var forecast in result)
            {
                Assert.InRange(forecast.Date.ToDateTime(TimeOnly.MinValue), DateTime.Now.Date.AddDays(1), DateTime.Now.Date.AddDays(5));
            }
        }

        [Fact]
        public void Get_ReturnsWeatherForecastsWithValidTemperature()
        {
            // Act
            var result = _controller.Get();

            // Assert
            foreach (var forecast in result)
            {
                Assert.InRange(forecast.TemperatureC, -20, 55);
            }
        }

        [Fact]
        public void Get_ReturnsWeatherForecastsWithValidSummary()
        {
            // Act
            var result = _controller.Get();

            // Assert
            foreach (var forecast in result)
            {
                Assert.Contains(forecast.Summary, Summaries);
            }
        }
    }
}
