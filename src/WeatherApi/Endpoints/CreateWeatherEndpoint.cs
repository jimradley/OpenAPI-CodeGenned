using WeatherApi.Data;
using WeatherApi.Models;

namespace WeatherApi.Endpoints;

public partial class CreateWeatherEndpoint
{
    public override async Task HandleAsync(CreateWeatherRequest req, CancellationToken ct)
    {
        var record = WeatherStore.Add(req.City ?? "", req.TemperatureC, req.Summary ?? "");
        await SendCreatedAtAsync<GetWeatherEndpoint>(
            new { id = record.Id }, record, cancellation: ct);
    }
}
