namespace TraeSwitch.Services;

public static class CarrierDefaults
{
    public static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TraeSwitch");

    public static string DefaultUserDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TRAE SOLO CN");

    /// <summary>
    /// 默认 Trae 客户端可执行文件路径。
    /// 优先自动探测常见安装位置（已缓存结果），全部未命中时回退到历史默认 D 盘路径，
    /// 用户可在 settings.json 的 ClientExe 中显式覆盖。
    /// </summary>
    public static string DefaultClientExe
    {
        get
        {
            if (string.IsNullOrEmpty(_detectedExe))
            {
                lock (DetectLock)
                {
                    if (string.IsNullOrEmpty(_detectedExe))
                        _detectedExe = AutoDetectClientExe() ?? @"D:\TRAE SOLO CN\TRAE SOLO CN.exe";   // 全部未命中：回退历史默认
                }
            }
            return _detectedExe;
        }
    }

    public static string DefaultProcessName => "TRAE SOLO CN";

    private static readonly object DetectLock = new();
    private static string _detectedExe = "";

    /// <summary>按候选列表探测客户端 exe；命中即返回完整路径，未命中返回 null。</summary>
    private static string? AutoDetectClientExe()
    {
        const string exeName = "TRAE SOLO CN.exe";
        var candidates = new List<string>
        {
            @"D:\TRAE SOLO CN\" + exeName,                                                   // 历史默认（D 盘安装）
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TRAE SOLO CN", exeName), // 绿色版/便携版
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TRAE SOLO CN", exeName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TRAE SOLO CN", exeName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TRAE SOLO CN", exeName),
        };
        foreach (var c in candidates)
        {
            try
            {
                if (File.Exists(c)) return c;
            }
            catch { /* 单个候选判断失败继续下一个 */ }
        }
        return null;
    }
}