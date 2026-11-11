using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;

namespace UrbanShade.Infraestructure.Geospatial;

public sealed class EarthEngineService : IEarthEngineService
{
    private readonly HttpClient _client;
    private readonly GoogleEarthEngineOptions _options;
    private GoogleCredential? _credential;
    public EarthEngineService(HttpClient client, IOptions<GoogleEarthEngineOptions> options)
    { _client = client; _options = options.Value; }
    private async Task<string> Token(CancellationToken cancellationToken)
    {
        try
        {
            if (_credential is null)
            {
                GoogleCredential credential;
                if (_options.Credentials.Count > 0)
                {
                    if (!_options.Credentials.TryGetValue("type", out var type) || type != "service_account")
                        throw new EarthEngineException("Invalid satellite service account configuration.");
                    /// :: Credentials originate only from trusted server configuration, never from an API request.
                    credential = CredentialFactory.FromJson<ServiceAccountCredential>(JsonSerializer.Serialize(_options.Credentials)).ToGoogleCredential();
                }
                else credential = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
                _credential = credential.CreateScoped("https://www.googleapis.com/auth/earthengine");
            }
            return await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new EarthEngineException("Satellite authentication failed. Check the server credentials and permissions."); }
    }
    private async Task<JsonElement> Compute(object expression, bool features, int pageSize, CancellationToken cancellationToken)
    {
        _options.Validate();
        var token = await Token(cancellationToken);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/projects/{_options.ProjectId}/{(features ? "table:computeFeatures" : "value:compute")}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = features ? JsonContent.Create(new { expression, pageSize }) : JsonContent.Create(new { expression });
                using var response = await _client.SendAsync(request, cancellationToken);
                if ((response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500) && attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), cancellationToken);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw new EarthEngineException(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        ? "Satellite access denied. Check project registration, API activation and service account permissions."
                        : $"Satellite provider rejected the request (HTTP {(int)response.StatusCode}).", response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500);
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return (features ? json.RootElement : json.RootElement.GetProperty("result")).Clone();
            }
            catch (HttpRequestException) when (attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), cancellationToken); }
            catch (HttpRequestException) { throw new EarthEngineException("Satellite provider is temporarily unreachable.", true); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), cancellationToken); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new EarthEngineException("Satellite provider timed out.", true); }
            catch (JsonException) { throw new EarthEngineException("Satellite provider returned an invalid response."); }
        }
    }
    public async Task<SatelliteObservation?> Analyze(Geometry boundary, DateTime start, DateTime end, GeoAnalysisType type, GeoProcessingParameters parameters, CancellationToken cancellationToken)
    {
        var candidates = await Compute(new EarthEngineExpressionBuilder().Candidates(boundary, start, end, type, parameters, _options), false, 0, cancellationToken);
        JsonElement result = default;
        foreach (var candidate in candidates.GetProperty("imageIds").EnumerateArray())
        {
            var observation = await Compute(new EarthEngineExpressionBuilder().Observation(boundary, candidate.GetString()!, type, parameters, _options), false, 0, cancellationToken);
            if (observation.GetProperty("validPercent").GetDecimal() >= (decimal)parameters.MinimumValidPixelPercent &&
                observation.GetProperty("statistics").TryGetProperty("value_mean", out var candidateMean) && candidateMean.ValueKind != JsonValueKind.Null)
            { result = observation; break; }
        }
        if (result.ValueKind == JsonValueKind.Undefined) return null;
        var stats = result.GetProperty("statistics");
        if (!stats.TryGetProperty("value_mean", out var mean) || mean.ValueKind == JsonValueKind.Null) return null;
        var validPercent = result.GetProperty("validPercent").GetDecimal();
        if (validPercent < (decimal)parameters.MinimumValidPixelPercent) return null;
        var imageId = result.GetProperty("imageId").GetString()!;
        var observedAt = DateTimeOffset.FromUnixTimeMilliseconds(result.GetProperty("observedAt").GetInt64()).UtcDateTime;
        var vegetation = type == GeoAnalysisType.Vegetation;
        var source = vegetation ? SatelliteDataSource.Sentinel2 : imageId.StartsWith(_options.Landsat8Dataset, StringComparison.Ordinal) ? SatelliteDataSource.Landsat8 : SatelliteDataSource.Landsat9;
        var dataset = source switch { SatelliteDataSource.Sentinel2 => _options.SentinelDataset, SatelliteDataSource.Landsat8 => _options.Landsat8Dataset, _ => _options.Landsat9Dataset };
        var validArea = result.GetProperty("validArea").GetDecimal();
        var vegetationArea = vegetation ? result.GetProperty("vegetationArea").GetDecimal() : (decimal?)null;
        var polygons = new List<VegetationPolygon>();
        var vectorStatus = vegetation ? "NO_VEGETATION" : "NOT_APPLICABLE";
        var skippedGeometries = 0;
        if (vegetation && vegetationArea > 0)
        {
            var features = await Compute(new EarthEngineExpressionBuilder().Vectors(boundary, imageId, parameters, _options), true, parameters.MaximumGreenAreas, cancellationToken);
            foreach (var feature in features.GetProperty("features").EnumerateArray())
            {
                var geometry = GeoJsonGeometry.Read(feature.GetProperty("geometry"));
                if (geometry.NumPoints > 2000) geometry = TopologyPreservingSimplifier.Simplify(geometry, 0.00005);
                if (!geometry.IsEmpty && geometry.IsValid && geometry.NumPoints <= 2000)
                    polygons.Add(new(geometry, feature.GetProperty("properties").GetProperty("area").GetDecimal()));
                else skippedGeometries++;
            }
            vectorStatus = features.GetProperty("features").GetArrayLength() >= parameters.MaximumGreenAreas ? "LIMIT_REACHED" : skippedGeometries > 0 ? "PARTIAL" : "AVAILABLE";
        }
        return new(source, dataset, imageId, observedAt, mean.GetDecimal(), stats.GetProperty("value_min").GetDecimal(), stats.GetProperty("value_max").GetDecimal(),
            Math.Min(100, validPercent), vegetation && validArea > 0 ? vegetationArea / validArea * 100 : null, vegetationArea,
            JsonSerializer.Serialize(new
            {
                imageId,
                parameters,
                validArea,
                vectorStatus,
                vectorCount = polygons.Count,
                skippedGeometries,
                scaleMeters = vegetation ? 10 : 30,
                areaProjection = "EPSG:4326",
                coverageDenominator = "VALID_OBSERVED_AREA",
                candidateLimit = 20
            }), polygons);
    }
}
