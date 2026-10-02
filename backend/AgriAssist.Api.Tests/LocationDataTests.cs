using System.Net;
using System.Text;
using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgriAssist.Api.Tests;

public sealed class LocationDataTests
{
    [Theory]
    [InlineData("0771234567")]
    [InlineData("077 123 4567")]
    [InlineData("077-123-4567")]
    [InlineData("+94771234567")]
    [InlineData("+94 77 123 4567")]
    public void Phone_normalizer_accepts_supported_formats(string input) =>
        Assert.Equal("+94771234567", SriLankanPhoneNumber.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("771234567")]
    [InlineData("+940771234567")]
    [InlineData("07712345")]
    [InlineData("077(123)4567")]
    public void Phone_normalizer_rejects_malformed_values(string? input) =>
        Assert.Null(SriLankanPhoneNumber.Normalize(input));

    [Fact]
    public void District_catalog_has_exactly_the_official_25_canonical_values()
    {
        Assert.Equal(25, SriLankanDistricts.All.Count);
        Assert.Equal("Kurunegala", SriLankanDistricts.Canonicalize(" kurunegala "));
        Assert.Null(SriLankanDistricts.Canonicalize("North Western"));
    }

    [Theory]
    [InlineData("Wariyapola", "Kurunegala", "Wariyapola, Kurunegala, Sri Lanka")]
    [InlineData("Kurunegala", "Kurunegala", "Kurunegala, Sri Lanka")]
    [InlineData("Wariyapola, kurunegala", "Kurunegala", "Wariyapola, kurunegala, Sri Lanka")]
    [InlineData("No. 25, Kurunegala Road", "Kurunegala", "No. 25, Kurunegala Road, Sri Lanka")]
    [InlineData("Wariyapola, Kurunegala, Sri Lanka", "Kurunegala", "Wariyapola, Kurunegala, Sri Lanka")]
    [InlineData("", "Kurunegala", "Kurunegala, Sri Lanka")]
    public void Weather_location_uses_valid_District_without_duplicate_parts(string location, string district, string expected) =>
        Assert.Equal(expected, WeatherLocationResolver.Resolve(location, district));

    [Theory]
    [InlineData("Legacy Farm Location", null)]
    [InlineData("Legacy Farm Location", "Invalid District")]
    [InlineData("  Legacy Farm Location  ", null)]
    public void Weather_location_preserves_legacy_location_when_District_is_unavailable(string location, string? district) =>
        Assert.Equal(location, WeatherLocationResolver.Resolve(location, district));

    [Fact]
    public async Task Weather_provider_falls_back_from_full_context_to_the_recognized_District()
    {
        var handler = new DistrictFallbackHandler();
        var service = new WeatherService(
            new HttpClient(handler),
            Options.Create(new WeatherOptions
            {
                BaseUrl = "https://weather.example.test/data/2.5",
                ApiKey = "test-key"
            }),
            NullLogger<WeatherService>.Instance);

        var result = await service.GetForecastAsync(
            "Wariyapola, Kurunegala, Sri Lanka",
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal("Wariyapola, Kurunegala, Sri Lanka", result.Location);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("q=Wariyapola%2C%20Kurunegala%2C%20Sri%20Lanka", handler.Requests[0].Query);
        Assert.Contains("q=Wariyapola", handler.Requests[1].Query);
        Assert.Contains("q=Kurunegala", handler.Requests[2].Query);
    }

    private sealed class DistrictFallbackHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (!request.RequestUri!.Query.Contains("q=Kurunegala", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            const string json = """
                {"list":[{"dt":1789977600,"main":{"temp_min":24.1,"temp_max":29.3},"wind":{"speed":4.2},"weather":[{"description":"light rain"}]}]}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
