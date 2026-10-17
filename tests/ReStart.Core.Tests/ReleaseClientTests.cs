using System.Net;
using System.Text.Json;
using ReStart.Core;

namespace ReStart.Core.Tests;

public sealed class ReleaseClientTests
{
    private static object Entry(string tag, bool draft = false, bool preview = false, string runtime = "win-x64") => new
    {
        tag_name = tag,
        draft,
        prerelease = preview,
        body = "Release notes",
        html_url = "https://untrusted.example/ignored",
        assets = new[] { new { name = $"ReStart-{runtime}.zip", state = "uploaded" }, new { name = $"ReStart-{runtime}.zip.sha256", state = "uploaded" } }
    };
    [Theory]
    [InlineData("v0.10.0", "0.9.9", 1)]
    [InlineData("1.0.0-rc.10", "1.0.0-rc.2", 1)]
    [InlineData("1.0.0", "1.0.0-rc.9", 1)]
    [InlineData("1.0.0+build-a", "1.0.0+build-b", 0)]
    [InlineData("1.0.0-1", "1.0.0-a", -1)]
    public void SemanticPrecedenceIsNumericAndIgnoresBuildMetadata(string left, string right, int expected)
        => Assert.Equal(expected, Math.Sign(ReleaseVersion.Parse(left)!.CompareTo(ReleaseVersion.Parse(right))));
    [Theory]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2")]
    [InlineData("../1.2.3")]
    public void InvalidVersionsAreRejected(string text) => Assert.Null(ReleaseVersion.Parse(text));
    [Fact]
    public async Task SelectsPublishedCompleteVersionsAndNeverUsesFeedUrls()
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal("api.github.com", request.RequestUri!.Host);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("fixture-token", request.Headers.Authorization.Parameter);
            return Json(new[] { Entry("v9.0.0", draft: true), Entry("v0.10.0"), Entry("v1.0.0", preview: true), Entry("v0.9.0") });
        }));
        var result = await new ReleaseClient(client).CheckAsync("0.9.0", "win-x64", false, "fixture-token");
        Assert.Equal(UpdateCheckStatus.Available, result.Status);
        Assert.Equal("v0.10.0", result.Release!.Tag);
        Assert.Equal("https://github.com/LastResolution/ReStart/releases/tag/v0.10.0", result.Release.Page.AbsoluteUri);
    }
    [Fact]
    public async Task PrereleasesAndArchitectureAreSelectedExplicitly()
    {
        using var client = new HttpClient(new Handler(_ => Json(new[] { Entry("v0.6.0", preview: true, runtime: "win-arm64") })));
        var checker = new ReleaseClient(client);
        Assert.Equal(UpdateCheckStatus.NoReleases, (await checker.CheckAsync("0.5.0", "win-x64", true)).Status);
        Assert.Equal(UpdateCheckStatus.NoReleases, (await checker.CheckAsync("0.5.0", "win-arm64", false)).Status);
        Assert.Equal(UpdateCheckStatus.Available, (await checker.CheckAsync("0.5.0", "win-arm64", true)).Status);
        Assert.Equal(UpdateCheckStatus.UpToDate, (await checker.CheckAsync("0.6.0", "win-arm64", true)).Status);
    }
    [Theory]
    [InlineData(401, UpdateCheckStatus.AccessRequired)]
    [InlineData(404, UpdateCheckStatus.AccessRequired)]
    [InlineData(403, UpdateCheckStatus.AccessRequired)]
    [InlineData(429, UpdateCheckStatus.RateLimited)]
    [InlineData(503, UpdateCheckStatus.NetworkError)]
    public async Task ErrorsNeverMasqueradeAsUpToDate(int status, UpdateCheckStatus expected)
    {
        using var client = new HttpClient(new Handler(_ => new((HttpStatusCode)status)));
        Assert.Equal(expected, (await new ReleaseClient(client).CheckAsync("0.5.0", "win-x64", true)).Status);
    }
    [Fact]
    public async Task FollowsPaginationWithinBoundAndRejectsMalformedData()
    {
        using var client = new HttpClient(new Handler(request =>
        {
            var first = request.RequestUri!.Query.Split('&').Contains("page=1", StringComparer.Ordinal);
            var response = Json(new[] { Entry(first ? "v0.4.0" : "v0.6.0") });
            if (first) response.Headers.Add("Link", "<https://untrusted.example/>; rel=\"next\"");
            return response;
        }));
        Assert.Equal("v0.6.0", (await new ReleaseClient(client).CheckAsync("0.5.0", "win-x64", true)).Release!.Tag);
        using var malformed = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("{") }));
        Assert.Equal(UpdateCheckStatus.InvalidResponse, (await new ReleaseClient(malformed).CheckAsync("0.5.0", "win-x64", true)).Status);
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
