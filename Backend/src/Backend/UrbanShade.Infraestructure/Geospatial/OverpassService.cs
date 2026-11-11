using System.Globalization;
using System.Net;
using System.Text.Json;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Geospatial;

namespace UrbanShade.Infraestructure.Geospatial;

public sealed class OverpassService : IOpenStreetMapService
{
    public const string Endpoint = "https://overpass-api.de/api/interpreter";
    private readonly HttpClient _client;
    public OverpassService(HttpClient client) { _client = client; }
    public static string BuildQuery(Geometry boundary)
    {
        var box = boundary.EnvelopeInternal;
        var bounds = string.Join(",", new[] { box.MinY, box.MinX, box.MaxY, box.MaxX }.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
        var samples = new[] { (box.MinY, box.MinX), (box.MinY, box.MaxX), (box.MaxY, box.MinX), (box.MaxY, box.MaxX), ((box.MinY + box.MaxY) / 2, (box.MinX + box.MaxX) / 2) };
        var containing = string.Join("\n", samples.Select(point => $"is_in({point.Item1.ToString("R", CultureInfo.InvariantCulture)},{point.Item2.ToString("R", CultureInfo.InvariantCulture)});"));
        return $"""
            [out:json][timeout:60][maxsize:33554432];
            ({containing})->.containing;
            (
              way(pivot.containing)[leisure~"^(park|garden)$"];
              relation(pivot.containing)[type=multipolygon][leisure~"^(park|garden)$"];
              way(pivot.containing)[landuse~"^(forest|grass|village_green|recreation_ground)$"];
              relation(pivot.containing)[type=multipolygon][landuse~"^(forest|grass|village_green|recreation_ground)$"];
              way(pivot.containing)[natural~"^(wood|grassland|scrub)$"];
              relation(pivot.containing)[type=multipolygon][natural~"^(wood|grassland|scrub)$"];
              way[leisure~"^(park|garden)$"]({bounds});
              relation[type=multipolygon][leisure~"^(park|garden)$"]({bounds});
              way[landuse~"^(forest|grass|village_green|recreation_ground)$"]({bounds});
              relation[type=multipolygon][landuse~"^(forest|grass|village_green|recreation_ground)$"]({bounds});
              way[natural~"^(wood|grassland|scrub)$"]({bounds});
              relation[type=multipolygon][natural~"^(wood|grassland|scrub)$"]({bounds});
            );
            out body geom;
            """;
    }
    public async Task<OpenStreetMapSnapshot> GetGreenAreas(Geometry boundary, OpenStreetMapParameters parameters, CancellationToken cancellationToken)
    {
        if (boundary.IsEmpty || !boundary.IsValid || boundary.SRID != 4326 || boundary is not (Polygon or MultiPolygon) || boundary.NumPoints > 2000 ||
            boundary.EnvelopeInternal.Width > 1 || boundary.EnvelopeInternal.Height > 1)
            throw new OpenStreetMapException("OpenStreetMap requires a valid region within one degree per axis.");
        for (var attempt = 0; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                request.Headers.UserAgent.ParseAdd("UrbanShade/1.0");
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = BuildQuery(boundary) });
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500) && attempt < 2)
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * (attempt + 1));
                    await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 1, 60)), cancellationToken);
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw new OpenStreetMapException($"OpenStreetMap import failed (HTTP {(int)response.StatusCode}). Previous data was preserved.");
                if (response.Content.Headers.ContentLength > parameters.MaximumResponseBytes) throw new OpenStreetMapException("OpenStreetMap response exceeded the import size limit.");
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream();
                var bytes = new byte[8192];
                int count;
                while ((count = await stream.ReadAsync(bytes, timeout.Token)) > 0)
                {
                    if (buffer.Length + count > parameters.MaximumResponseBytes) throw new OpenStreetMapException("OpenStreetMap response exceeded the import size limit.");
                    buffer.Write(bytes, 0, count);
                }
                buffer.Position = 0;
                using var json = await JsonDocument.ParseAsync(buffer, cancellationToken: timeout.Token);
                return OverpassResponseParser.Parse(json.RootElement, parameters);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(5 * (attempt + 1)), cancellationToken); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new OpenStreetMapException("OpenStreetMap import timed out. Previous data was preserved."); }
            catch (HttpRequestException) when (attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(5 * (attempt + 1)), cancellationToken); }
            catch (HttpRequestException) { throw new OpenStreetMapException("OpenStreetMap is temporarily unreachable. Previous data was preserved."); }
            catch (JsonException) { throw new OpenStreetMapException("OpenStreetMap returned an invalid response. Previous data was preserved."); }
        }
    }
}
