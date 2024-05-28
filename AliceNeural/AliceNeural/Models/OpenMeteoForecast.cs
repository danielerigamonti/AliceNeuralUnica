

using System.Text.Json.Serialization;

namespace AliceNeural.Models
{
    // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
    public class Current
    {
        [JsonPropertyName("time")]
        public int time { get; set; }

        [JsonPropertyName("interval")]
        public int interval { get; set; }

        [JsonPropertyName("temperature_2m")]
        public double temperature_2m { get; set; }

        [JsonPropertyName("weather_code")]
        public int weather_code { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double wind_speed_10m { get; set; }

        [JsonPropertyName("wind_direction_10m")]
        public int wind_direction_10m { get; set; }
    }

    public class CurrentUnits
    {
        [JsonPropertyName("time")]
        public string time { get; set; }

        [JsonPropertyName("interval")]
        public string interval { get; set; }

        [JsonPropertyName("temperature_2m")]
        public string temperature_2m { get; set; }

        [JsonPropertyName("weather_code")]
        public string weather_code { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public string wind_speed_10m { get; set; }

        [JsonPropertyName("wind_direction_10m")]
        public string wind_direction_10m { get; set; }
    }

    public class Daily
    {
        [JsonPropertyName("time")]
        public List<int> time { get; set; }

        [JsonPropertyName("weather_code")]
        public List<int> weather_code { get; set; }

        [JsonPropertyName("temperature_2m_max")]
        public List<double> temperature_2m_max { get; set; }

        [JsonPropertyName("temperature_2m_min")]
        public List<double> temperature_2m_min { get; set; }

        [JsonPropertyName("apparent_temperature_max")]
        public List<double> apparent_temperature_max { get; set; }

        [JsonPropertyName("apparent_temperature_min")]
        public List<double> apparent_temperature_min { get; set; }
    }

    public class DailyUnits
    {
        [JsonPropertyName("time")]
        public string time { get; set; }

        [JsonPropertyName("weather_code")]
        public string weather_code { get; set; }

        [JsonPropertyName("temperature_2m_max")]
        public string temperature_2m_max { get; set; }

        [JsonPropertyName("temperature_2m_min")]
        public string temperature_2m_min { get; set; }

        [JsonPropertyName("apparent_temperature_max")]
        public string apparent_temperature_max { get; set; }

        [JsonPropertyName("apparent_temperature_min")]
        public string apparent_temperature_min { get; set; }
    }

    public class Hourly
    {
        [JsonPropertyName("time")]
        public List<int> time { get; set; }

        [JsonPropertyName("temperature_2m")]
        public List<double> temperature_2m { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public List<double> relative_humidity_2m { get; set; }

        [JsonPropertyName("dew_point_2m")]
        public List<double> dew_point_2m { get; set; }

        [JsonPropertyName("apparent_temperature")]
        public List<double> apparent_temperature { get; set; }

        [JsonPropertyName("precipitation_probability")]
        public List<double> precipitation_probability { get; set; }

        [JsonPropertyName("precipitation")]
        public List <double> precipitation { get; set; }

        [JsonPropertyName("rain")]
        public List<double> rain { get; set; }

        [JsonPropertyName("showers")]
        public List<double> showers { get; set; }

        [JsonPropertyName("weather_code")]
        public List<double> weather_code { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public List<double> wind_speed_10m { get; set; }

        [JsonPropertyName("wind_direction_10m")]
        public List<double> wind_direction_10m { get; set; }
    }

    public class HourlyUnits
    {
        [JsonPropertyName("time")]
        public string time { get; set; }

        [JsonPropertyName("temperature_2m")]
        public string temperature_2m { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public string relative_humidity_2m { get; set; }

        [JsonPropertyName("dew_point_2m")]
        public string dew_point_2m { get; set; }

        [JsonPropertyName("apparent_temperature")]
        public string apparent_temperature { get; set; }

        [JsonPropertyName("precipitation_probability")]
        public string precipitation_probability { get; set; }

        [JsonPropertyName("precipitation")]
        public string precipitation { get; set; }

        [JsonPropertyName("rain")]
        public string rain { get; set; }

        [JsonPropertyName("showers")]
        public string showers { get; set; }

        [JsonPropertyName("weather_code")]
        public string weather_code { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public string wind_speed_10m { get; set; }

        [JsonPropertyName("wind_direction_10m")]
        public string wind_direction_10m { get; set; }
    }

    public class Forecast
    {
        [JsonPropertyName("latitude")]
        public double latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double longitude { get; set; }

        [JsonPropertyName("generationtime_ms")]
        public double generationtime_ms { get; set; }

        [JsonPropertyName("utc_offset_seconds")]
        public int utc_offset_seconds { get; set; }

        [JsonPropertyName("timezone")]
        public string timezone { get; set; }

        [JsonPropertyName("timezone_abbreviation")]
        public string timezone_abbreviation { get; set; }

        [JsonPropertyName("elevation")]
        public double elevation { get; set; }

        [JsonPropertyName("current_units")]
        public CurrentUnits current_units { get; set; }

        [JsonPropertyName("current")]
        public Current current { get; set; }

        [JsonPropertyName("hourly_units")]
        public HourlyUnits hourly_units { get; set; }

        [JsonPropertyName("hourly")]
        public Hourly hourly { get; set; }

        [JsonPropertyName("daily_units")]
        public DailyUnits daily_units { get; set; }

        [JsonPropertyName("daily")]
        public Daily daily { get; set; }
    }
}
