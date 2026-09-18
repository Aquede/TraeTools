using System.IO;

namespace TraeTools.Services;

/// <summary>
/// 统一数据根目录（%APPDATA%\TraeTools），收敛历史遗留的 TraeCheckin / TraeSwitch 三个目录。
/// WebView 缓存与抓包输出保持在 %LOCALAPPDATA%\TraeTools（缓存不进 APPDATA）。
/// 启动时调用 Migrate() 把旧目录数据自动搬入新位置（保留旧目录，幂等、失败不阻塞）。
/// </summary>
public static class DataPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TraeTools");

    /// <summary>签到配置 config.json。</summary>
    public static string ConfigPath => Path.Combine(Root, "config.json");

    /// <summary>签到历史 + 签到调试日志（history_*.txt / checkin_log_*.txt）。</summary>
    public static string HistoryDir => Path.Combine(Root, "history");

    /// <summary>用量统计明细（usage_*.jsonl）。</summary>
    public static string UsageDir => Path.Combine(Root, "usage");

    /// <summary>账号切换配置（settings.json）。</summary>
    public static string SwitchDir => Path.Combine(Root, "switch");

    /// <summary>登录态备份 vault（原 %LOCALAPPDATA%\TraeSwitch\vault）。</summary>
    public static string VaultDir => Path.Combine(Root, "vault");

    /// <summary>把旧版目录（TraeCheckin / TraeSwitch）数据迁移到统一根，保留旧目录；幂等。</summary>
    public static void Migrate()
    {
        try
        {
            Directory.CreateDirectory(HistoryDir);
            Directory.CreateDirectory(UsageDir);
            Directory.CreateDirectory(SwitchDir);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // 1) 签到配置 + 历史 + 用量 + 总积分
            var oldCheckin = Path.Combine(appData, "TraeCheckin");
            if (Directory.Exists(oldCheckin))
            {
                MoveFile(Path.Combine(oldCheckin, "config.json"), ConfigPath);
                foreach (var f in Directory.GetFiles(oldCheckin, "history_*.txt"))
                    MoveFile(f, Path.Combine(HistoryDir, Path.GetFileName(f)));
                foreach (var f in Directory.GetFiles(oldCheckin, "checkin_log_*.txt"))
                    MoveFile(f, Path.Combine(HistoryDir, Path.GetFileName(f)));
                foreach (var f in Directory.GetFiles(oldCheckin, "usage_*.jsonl"))
                    MoveFile(f, Path.Combine(UsageDir, Path.GetFileName(f)));
                foreach (var f in Directory.GetFiles(oldCheckin, "credits_total_*.txt"))
                    MoveFile(f, Path.Combine(Root, Path.GetFileName(f)));
            }

            // 2) 账号切换配置
            var oldSwitch = Path.Combine(appData, "TraeSwitch");
            if (Directory.Exists(oldSwitch))
                MoveFile(Path.Combine(oldSwitch, "settings.json"), Path.Combine(SwitchDir, "settings.json"));

            // 3) 登录态备份 vault（LocalAppData\TraeSwitch\vault → AppData\TraeTools\vault）
            var oldVault = Path.Combine(localAppData, "TraeSwitch", "vault");
            if (Directory.Exists(oldVault) && !Directory.Exists(VaultDir))
                Directory.Move(oldVault, VaultDir);
        }
        catch { /* 迁移失败不阻塞启动 */ }
    }

    private static void MoveFile(string src, string dst)
    {
        try
        {
            if (!File.Exists(src) || File.Exists(dst)) return;
            var dir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Move(src, dst);
        }
        catch { /* 单文件失败跳过 */ }
    }
}