using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using BangShow.Core.Api;

namespace BangShow.Core.Tests;

public class WeightApiClientTests
{
    private const string OkBody =
        """{"bangname":"bang1","weight":123,"updated":true,"updated_at":"2026-10-21T06:00:00+00:00"}""";

    [Fact]
    public async Task UpsertAsync_SendsBangnameAndWeight()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, OkBody);
        using var client = new WeightApiClient("http://127.0.0.1:8000", TimeSpan.FromSeconds(5), handler);

        WeightUpdateResult? result = await client.UpsertAsync("bang1", 123);

        Assert.NotNull(result);
        Assert.True(result!.Updated);
        Assert.Equal(123, result.Weight);
        Assert.Equal("bang1", result.Bangname);
        Assert.Equal("2026-10-21T06:00:00+00:00", result.UpdatedAt);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/weight", handler.RequestUri!.AbsolutePath);

        JsonNode body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal("bang1", body["bangname"]!.GetValue<string>());
        Assert.Equal(123L, body["weight"]!.GetValue<long>());
    }

    [Fact]
    public async Task UpsertAsync_WhenServerDidNotChangeValue_ReportsUpdatedFalse()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"bangname":"bang1","weight":123,"updated":false,"updated_at":"2026-10-21T05:00:00+00:00"}""");
        using var client = new WeightApiClient("http://127.0.0.1:8000/", TimeSpan.FromSeconds(5), handler);

        WeightUpdateResult? result = await client.UpsertAsync("bang1", 123);

        Assert.NotNull(result);
        Assert.False(result!.Updated);
    }

    [Fact]
    public async Task GetAsync_ReturnsStoredWeight()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"bangname":"bang1","weight":-42,"updated_at":"2026-10-21T06:00:00+00:00"}""");
        using var client = new WeightApiClient("http://127.0.0.1:8000", TimeSpan.FromSeconds(5), handler);

        WeightQueryResult? result = await client.GetAsync("bang1");

        Assert.NotNull(result);
        Assert.Equal(-42, result!.Weight);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/weight/bang1", handler.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetAsync_EscapesBangname()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"bangname":"一号榜","weight":7,"updated_at":null}""");
        using var client = new WeightApiClient("http://127.0.0.1:8000", TimeSpan.FromSeconds(5), handler);

        await client.GetAsync("一号榜");

        Assert.Contains(Uri.EscapeDataString("一号榜"), handler.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var handler = new RecordingHandler(HttpStatusCode.NotFound, """{"detail":"不存在"}""");
        using var client = new WeightApiClient("http://127.0.0.1:8000", TimeSpan.FromSeconds(5), handler);

        WeightQueryResult? result = await client.GetAsync("nobody");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertAsync_ServerError_Throws()
    {
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, "boom");
        using var client = new WeightApiClient("http://127.0.0.1:8000", TimeSpan.FromSeconds(5), handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpsertAsync("bang1", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://127.0.0.1:8000")]
    [InlineData("not-a-url")]
    public void InvalidBaseUrl_Throws(string baseUrl)
    {
        Assert.Throws<ArgumentException>(() => new WeightApiClient(baseUrl, TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyBangname_Throws(string bangName)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, OkBody);
        using var client = new WeightApiClient("http://127.0.0.1:8000", TimeSpan.FromSeconds(5), handler);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => client.UpsertAsync(bangName, 1));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public RecordingHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
