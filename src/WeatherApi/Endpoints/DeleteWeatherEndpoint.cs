using WeatherApi.Data;

namespace WeatherApi.Endpoints;

public partial class DeleteWeatherEndpoint
{
    public override async Task HandleAsync(DeleteWeatherRequest req, CancellationToken ct)
    {
        if (!WeatherStore.Delete(req.Id))
        {
            await SendNotFoundAsync(ct);
            return;
        }
        await SendNoContentAsync(ct);
    }
}
