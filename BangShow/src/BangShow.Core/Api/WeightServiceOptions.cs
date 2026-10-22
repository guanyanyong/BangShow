namespace BangShow.Core.Api;

/// <summary>
/// 称重服务连接配置（对应 appsettings.json 的 Service 节点）。
/// </summary>
public sealed class WeightServiceOptions
{
    /// <summary>服务根地址，例如 http://127.0.0.1:8765。</summary>
    public string BaseUrl { get; init; } = "http://127.0.0.1:8765";

    /// <summary>榜名称：上报与查询都使用该值。</summary>
    public string BangName { get; init; } = "";

    /// <summary>每秒最多上报次数（数值发生变化时）。</summary>
    public int MaxReportsPerSecond { get; init; } = 2;

    /// <summary>单次 HTTP 请求超时（秒）。</summary>
    public int TimeoutSeconds { get; init; } = 5;

    /// <summary>校验配置，非法时抛出异常，调用方据此禁用上报。</summary>
    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Service.BaseUrl 不是合法的 http/https 地址：{BaseUrl}");
        }

        if (string.IsNullOrWhiteSpace(BangName))
        {
            throw new ArgumentException("Service.BangName 未配置（不能为空）");
        }

        if (BangName.Trim().Length > 64)
        {
            throw new ArgumentException("Service.BangName 长度不能超过 64 个字符");
        }

        if (MaxReportsPerSecond is < 1 or > 100)
        {
            throw new ArgumentException($"Service.MaxReportsPerSecond 必须在 1~100 之间：{MaxReportsPerSecond}");
        }

        if (TimeoutSeconds is < 1 or > 120)
        {
            throw new ArgumentException($"Service.TimeoutSeconds 必须在 1~120 之间：{TimeoutSeconds}");
        }
    }
}
