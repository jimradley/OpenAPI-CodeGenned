using WeatherApi.Data;

namespace WeatherApi.Endpoints;

public partial class UpdateWeatherEndpoint
{
    public override async Task HandleAsync(UpdateWeatherRequest req, CancellationToken ct)
    {
        var record = WeatherStore.Update(req.Id, req.City ?? "", req.TemperatureC ?? 0, req.Summary ?? "");
        if (record is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        await SendOkAsync(record, ct);
    }
}
