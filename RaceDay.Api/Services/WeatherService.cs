using System.Globalization;
using System.Text.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Services;

public interface IWeatherService
{
    Task<WeatherResponse> GetAsync(double latitude, double longitude, DateOnly date, CancellationToken ct = default);
}

public class WeatherService(HttpClient http) : IWeatherService
{
    public async Task<WeatherResponse> GetAsync(double latitude, double longitude, DateOnly date, CancellationToken ct = default)
    {
        var daysAway = date.DayNumber - Clock.Today.DayNumber;
        var isForecast = daysAway is >= 0 and <= 15;
        var range = isForecast ? $"&start_date={date:yyyy-MM-dd}&end_date={date:yyyy-MM-dd}" : "&forecast_days=1";
        const string fields = "temperature_2m_max,apparent_temperature_max,precipitation_probability_max,wind_speed_10m_max,weather_code";
        var url = string.Create(CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={latitude}&longitude={longitude}&timezone=auto{range}&daily={fields}");

        using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
        var daily = doc.RootElement.GetProperty("daily");

        double? Value(string name)
        {
            var element = daily.GetProperty(name)[0];
            return element.ValueKind == JsonValueKind.Number ? element.GetDouble() : null;
        }

        var code = Value("weather_code");
        return new WeatherResponse(date, Value("temperature_2m_max"), Value("apparent_temperature_max"),
            code is null ? "Unknown" : Describe((int)code.Value), Value("wind_speed_10m_max"),
            (int?)Value("precipitation_probability_max"), isForecast);
    }

    private static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 or 2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        >= 51 and <= 57 => "Drizzle",
        >= 61 and <= 67 => "Rain",
        >= 71 and <= 77 => "Snow",
        >= 80 and <= 82 => "Rain showers",
        >= 95 => "Thunderstorm",
        _ => "Unknown"
    };
}
