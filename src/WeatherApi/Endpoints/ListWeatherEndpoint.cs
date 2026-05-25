using FastEndpoints;
using WeatherApi.Data;

namespace WeatherApi.Endpoints;

public partial class ListWeatherEndpoint
{
    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
        => await SendOkAsync(WeatherStore.GetAll(), ct);
}
