using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BangShow.Core.Api;

/// <summary>称重服务的客户端能力，便于替换为测试替身。</summary>
public interface IWeightApiClient : IDisposable
{
    /// <summary>调用接口 1：保存数值（数值变化时服务端才修改）。</summary>
    Task<WeightUpdateResult?> UpsertAsync(string bangName, long weight, CancellationToken cancellationToken = default);

    /// <summary>调用接口 2：按 bangname 查询数值；不存在时返回 null。</summary>
    Task<WeightQueryResult?> GetAsync(string bangName, CancellationToken cancellationToken = default);
}

/// <summary>接口 1 的请求体。</summary>
public sealed record WeightUpdateRequest(
    [property: JsonPropertyName("bangname")] string Bangname,
    [property: JsonPropertyName("weight")] long Weight);

/// <summary>接口 1 的响应体。</summary>
public sealed record WeightUpdateResult(
    [property: JsonPropertyName("bangname")] string Bangname,
    [property: JsonPropertyName("weight")] long Weight,
    [property: JsonPropertyName("updated")] bool Updated,
    [property: JsonPropertyName("updated_at")] string? UpdatedAt);

/// <summary>接口 2 的响应体。</summary>
public sealed record WeightQueryResult(
    [property: JsonPropertyName("bangname")] string Bangname,
    [property: JsonPropertyName("weight")] long Weight,
    [property: JsonPropertyName("updated_at")] string? UpdatedAt);

/// <summary>Python 服务（service/main.py）的 HTTP 客户端。</summary>
public sealed class WeightApiClient : IWeightApiClient
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    /// <param name="baseUrl">服务根地址，例如 http://127.0.0.1:8000。</param>
    /// <param name="timeout">单次请求超时。</param>
    /// <param name="handler">可选的 HttpMessageHandler（测试用）。</param>
    public WeightApiClient(string baseUrl, TimeSpan timeout, HttpMessageHandler? handler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out Uri? baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"服务地址不合法：{baseUrl}", nameof(baseUrl));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "超时必须大于 0");
        }

        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _httpClient.BaseAddress = baseUri;
        _httpClient.Timeout = timeout;
    }

    public async Task<WeightUpdateResult?> UpsertAsync(
        string bangName,
        long weight,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bangName);

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync("api/weight", new WeightUpdateRequest(bangName, weight), SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<WeightUpdateResult>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WeightQueryResult?> GetAsync(
        string bangName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bangName);

        using HttpResponseMessage response = await _httpClient
            .GetAsync($"api/weight/{Uri.EscapeDataString(bangName)}", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<WeightQueryResult>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose() => _httpClient.Dispose();
}
