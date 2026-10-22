using System.Text.Json;
using BangShow.Core.Api;

namespace BangShow;

/// <summary>
/// appsettings.json 的强类型视图。
/// <para>文件随 exe 一起输出，修改后重启程序生效。</para>
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>称重服务配置。</summary>
    public WeightServiceOptions Service { get; init; } = new();

    /// <summary>默认配置文件路径：与 exe 同目录的 appsettings.json。</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>读取配置文件；文件不存在或格式错误时抛出异常，由调用方决定如何降级。</summary>
    public static AppSettings Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
    }
}
