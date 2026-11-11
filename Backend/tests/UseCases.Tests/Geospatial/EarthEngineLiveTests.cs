using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Infraestructure.Geospatial;
using Xunit.Abstractions;

namespace UseCases.Tests.Geospatial;

public sealed class EarthEngineLiveFactAttribute : FactAttribute
{
    public EarthEngineLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("URBANSHADE_EARTH_ENGINE_TEST") != "true") Skip = "Set URBANSHADE_EARTH_ENGINE_TEST=true to run against Google Earth Engine with local credentials.";
    }
}
public sealed class EarthEngineLiveTests(ITestOutputHelper output)
{
    [EarthEngineLiveFact]
    public async Task RealProvider_AcceptsSerializedGraphs_AndReturnsEnvironmentalIndicators()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrbanShade.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory!.FullName, "src/Backend/UrbanShade.API/appsettings.Development.json")));
        var section = config.RootElement.GetProperty("GoogleEarthEngine");
        var options = new GoogleEarthEngineOptions { ProjectId = section.GetProperty("ProjectId").GetString()!, Credentials = section.GetProperty("Credentials").Deserialize<Dictionary<string, string>>()! };
        using var handler = new ProviderHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(JsonSerializer.Serialize(options.Credentials)).ToGoogleCredential().CreateScoped("https://www.googleapis.com/auth/earthengine");
        var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://earthengine.googleapis.com/v1/projects/{options.ProjectId}/algorithms");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, handler.LastError);
        using var algorithms = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var catalog = algorithms.RootElement.GetProperty("algorithms").EnumerateArray().ToDictionary(item => item.GetProperty("name").GetString()!.Split('/').Last());
        var start = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var boundary = GeoJsonGeometry.Read(GeoExpressionTests.Boundary);
        foreach (var type in Enum.GetValues<GeoAnalysisType>())
        {
            var graph = JsonSerializer.SerializeToElement(new EarthEngineExpressionBuilder().Candidates(boundary, start, end, type, new GeoAnalysisSettings().Parameters(), options));
            Validate(graph);
            Validate(JsonSerializer.SerializeToElement(new EarthEngineExpressionBuilder().Observation(boundary, "test", type, new GeoAnalysisSettings().Parameters(), options)));
            try
            {
                var result = await new EarthEngineService(client, Options.Create(options)).Analyze(boundary, start, end, type, new GeoAnalysisSettings().Parameters(), default);
                Assert.NotNull(result);
                Assert.InRange(result!.ValidPixelPercent, 70m, 100m);
                Assert.True(result.Minimum <= result.Mean && result.Mean <= result.Maximum);
                if (type == GeoAnalysisType.Vegetation) Assert.InRange(result.Mean, -1m, 1m);
                else Assert.InRange(result.Mean, -40m, 80m);
                output.WriteLine($"{type}: {result.Source}, observed {result.ObservedAt:O}, mean {result.Mean}, valid {result.ValidPixelPercent}%, vectors {result.GreenAreas.Count}.");
            }
            catch (EarthEngineException exception) { Assert.Fail(exception.Message + " " + handler.LastError); }
        }
        var empty = await new EarthEngineService(client, Options.Create(options)).Analyze(boundary,
            new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(1990, 2, 1, 0, 0, 0, DateTimeKind.Utc), GeoAnalysisType.Vegetation, new GeoAnalysisSettings().Parameters(), default);
        Assert.Null(empty);
        Validate(JsonSerializer.SerializeToElement(new EarthEngineExpressionBuilder().Vectors(boundary, "test", new GeoAnalysisSettings().Parameters(), options)));
        void Validate(JsonElement graph)
        {
            foreach (var node in graph.GetProperty("values").EnumerateObject())
            {
                if (!node.Value.TryGetProperty("functionInvocationValue", out var invocation)) continue;
                var name = invocation.GetProperty("functionName").GetString()!;
                Assert.True(catalog.TryGetValue(name, out var algorithm), $"Unknown algorithm: {name}");
                var arguments = algorithm.TryGetProperty("arguments", out var argumentList) ? argumentList.EnumerateArray().Select(item => item.GetProperty("argumentName").GetString()).ToHashSet() : [];
                foreach (var argument in invocation.GetProperty("arguments").EnumerateObject()) Assert.True(arguments.Contains(argument.Name), $"Unknown argument {name}.{argument.Name}");
            }
        }
    }
    private sealed class ProviderHandler : DelegatingHandler
    {
        public string? LastError { get; private set; }
        public ProviderHandler() : base(new HttpClientHandler()) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                LastError = request.RequestUri!.AbsolutePath + ": " + json.RootElement.GetProperty("error").GetProperty("message").GetString();
            }
            return response;
        }
    }
}

