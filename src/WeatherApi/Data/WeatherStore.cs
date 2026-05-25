using WeatherApi.Models;

namespace WeatherApi.Data;

public static class WeatherStore
{
    private static int _nextId = 4;
    private static readonly List<WeatherRecord> _records =
    [
        new(1, "London",    15.2, "Cloudy"),
        new(2, "Sydney",    28.0, "Sunny"),
        new(3, "New York",  -2.5, "Snowy"),
    ];

    public static List<WeatherRecord> GetAll() => [.._records];

    public static WeatherRecord? GetById(int id) =>
        _records.FirstOrDefault(r => r.Id == id);

    public static WeatherRecord Add(string city, double temperatureC, string summary)
    {
        var record = new WeatherRecord(_nextId++, city, temperatureC, summary);
        _records.Add(record);
        return record;
    }

    public static WeatherRecord? Update(int id, string city, double temperatureC, string summary)
    {
        var index = _records.FindIndex(r => r.Id == id);
        if (index < 0) return null;
        var updated = new WeatherRecord(id, city, temperatureC, summary);
        _records[index] = updated;
        return updated;
    }

    public static bool Delete(int id)
    {
        var index = _records.FindIndex(r => r.Id == id);
        if (index < 0) return false;
        _records.RemoveAt(index);
        return true;
    }
}
