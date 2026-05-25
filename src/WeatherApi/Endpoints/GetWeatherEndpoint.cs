using WeatherApi.Data;

namespace WeatherApi.Endpoints;

public partial class GetWeatherEndpoint
{
    public override async Task HandleAsync(GetWeatherRequest req, CancellationToken ct)
    {
        var record = WeatherStore.GetById(req.Id);
        if (record is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        await SendOkAsync(record, ct);
    }
}
