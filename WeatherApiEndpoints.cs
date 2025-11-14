using MyReactApp.Server.Endpoints;

namespace MyReactApp.Server.Endpoints
{
    /// <summary>
    /// Производный класс для эндпоинтов погоды
    /// </summary>
    public class WeatherApiEndpoints : ApiEndpointBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        /// <summary>
        /// Регистрирует эндпоинты погоды
        /// </summary>
        public override void RegisterEndpoints(WebApplication app)
        {
            app.MapGet("/weatherforecast", () =>
            {
                var forecast = Enumerable.Range(1, 5).Select(index =>
                    new WeatherForecast
                    (
                        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        Random.Shared.Next(-20, 55),
                        Summaries[Random.Shared.Next(Summaries.Length)]
                    ))
                    .ToArray();
                return forecast;
            })
            .WithName("GetWeatherForecast")
            .WithOpenApi();
        }

        public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
        {
            public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
        }
    }
}

