using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TraeCheckin;
using TraeSwitch.Services;
using TraeTools.Services.Usage;

namespace TraeTools.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DashboardViewModel _dashboard;
    private readonly CheckinViewModel _checkin;
    private readonly SwitchViewModel _switch;
    private readonly CloudViewModel _cloud;
    private readonly SettingsViewModel _settings;
    private readonly UsageViewModel _usage;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private string _currentPageKey = "dashboard";

    // 全局服务容器：页面 VM 通过静态属性访问
    public static AppConfig? AppConfig;
    public static TraeApiClient? CheckinApi;
    public static TraeUsageClient? UsageApi;
    public static SettingsStore? SwitchSettings;
    public static VaultService? Vault;
    public static GitHubApiClient? GitHubApi;

    public MainViewModel()
    {
        // 初始化真实服务（try-catch，失败则为 null，页面 VM 用 mock 兜底）
        try
        {
            AppConfig = AppConfig.Load();
        }
        catch
        {
            AppConfig = new AppConfig();
        }

        try
        {
            CheckinApi = new TraeApiClient();
        }
        catch
        {
            CheckinApi = null;
        }

        try
        {
            UsageApi = new TraeUsageClient();
        }
        catch
        {
            UsageApi = null;
        }

        try
        {
            SwitchSettings = new SettingsStore(CarrierDefaults.SettingsDir);
        }
        catch
        {
            SwitchSettings = null;
        }

        try
        {
            if (SwitchSettings != null)
            {
                var vaultRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TraeSwitch", "vault");
                Vault = new VaultService(SwitchSettings.Data.RootDir, vaultRoot);
            }
        }
        catch
        {
            Vault = null;
        }

        try
        {
            GitHubApi = new GitHubApiClient();
        }
        catch
        {
            GitHubApi = null;
        }

        Instance = this;
        _dashboard = new DashboardViewModel();
        _checkin = new CheckinViewModel();
        _switch = new SwitchViewModel();
        _cloud = new CloudViewModel();
        _settings = new SettingsViewModel();
        _usage = new UsageViewModel();

        _dashboard.NavigateRequested += p => Navigate(p);
        _checkin.NavigateRequested += p => Navigate(p);
        _switch.NavigateRequested += p => Navigate(p);
        _cloud.NavigateRequested += p => Navigate(p);
        _settings.NavigateRequested += p => Navigate(p);
        _usage.NavigateRequested += p => Navigate(p);

        CurrentPage = _dashboard;
        CurrentPageKey = "dashboard";
    }

    public bool IsDashboardActive => CurrentPageKey == "dashboard";
    public bool IsCheckinActive => CurrentPageKey == "checkin";
    public bool IsSwitchActive => CurrentPageKey == "switch";
    public bool IsCloudActive => CurrentPageKey == "cloud";
    public bool IsSettingsActive => CurrentPageKey == "settings";
    public bool IsUsageActive => CurrentPageKey == "usage";

    partial void OnCurrentPageKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsCheckinActive));
        OnPropertyChanged(nameof(IsSwitchActive));
        OnPropertyChanged(nameof(IsCloudActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsUsageActive));
    }

    /// <summary>账号管理切换激活账号后：仪表盘完整刷新 + 签到页 + 账号状态同步 + 用量页联动。</summary>
    public static async void NotifyActiveAccountChanged()
    {
        if (Instance is not { } vm) return;
        try
        {
            await vm._dashboard.RefreshAllAsync();
            vm._checkin.Reload();
            vm._usage.Reload();
            await vm._settings.RefreshAccountStatesAsync();
        }
        catch { /* 异步刷新失败不应导致崩溃 */ }
    }

    /// <summary>签到完成后同步仪表盘与账号状态。</summary>
    public static async Task NotifyAsyncRefresh()
    {
        if (Instance is not { } vm) return;
        await vm._dashboard.RefreshAllAsync();
        await vm._settings.RefreshAccountStatesAsync();
    }

    /// <summary>当前 VM 实例（供静态通知使用）。</summary>
    public static MainViewModel? Instance { get; private set; }

    /// <summary>由 MainWindow 定时器定时调用：到点自动签到（TraeCheckin「每日自动签到」能力）。</summary>
    public Task<bool> AutoCheckinIfDue() => _checkin.TryAutoCheckinAsync();

    /// <summary>由 MainWindow 定时器定时调用：客户端已退出时静默刷新当前账号的 vault 备份（本次运行只刷一次）。</summary>
    public Task RefreshSnapshotIfNeeded() => _switch.TryAutoRefreshSnapshotAsync();

    /// <summary>托盘「立即签到」：对当前账号执行一次签到（不弹窗，由调用方提示结果）。</summary>
    public async Task<bool> QuickCheckinAsync()
    {
        try
        {
            var cfg = AppConfig;
            var api = CheckinApi;
            if (cfg == null || api == null) return false;
            var acc = cfg.Accounts.FirstOrDefault(a => a.Id == cfg.ActiveAccountId)
                      ?? cfg.Accounts.FirstOrDefault();
            if (acc == null || string.IsNullOrEmpty(acc.Token)) return false;
            var result = await api.ClaimAsync(acc.Token, acc.DeviceId);
            if (result == null || result.code != 0) return false;
            acc.LastCheckinDate = DateTime.Now;
            cfg.LastCheckinDate = DateTime.Now;
            try
            {
                // 解析本次所得并写入签到历史（与签到页/自动签到同口径）
                var after = await api.GetStatusAsync(acc.Token, acc.DeviceId);
                double gained = TraeCheckin.CheckinEvaluator.ResolveGainedCredits(after ?? result, acc.IsMember);
                AccountHelpers.AppendHistory(acc, gained);
            }
            catch { /* 历史写入失败不影响 */ }
            try
            {
                var credits = await api.GetRemainingCreditsAsync(acc.Token, acc.DeviceId);
                if (credits >= 0) cfg.LastRemaining = credits;
            }
            catch { /* 积分刷新失败不影响签到结果 */ }
            try { cfg.Save(); } catch { /* 忽略 */ }
            return true;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        switch (page)
        {
            case "dashboard":
                _ = _dashboard.RefreshAllAsync();   // 每次进入仪表盘都按当前激活账号真实验证刷新
                CurrentPage = _dashboard;
                CurrentPageKey = "dashboard";
                break;
            case "checkin":
                _checkin.Reload();     // 同上，签到页跟随激活账号
                CurrentPage = _checkin;
                CurrentPageKey = "checkin";
                break;
            case "switch":
                CurrentPage = _switch;
                CurrentPageKey = "switch";
                break;
            case "cloud":
                CurrentPage = _cloud;
                CurrentPageKey = "cloud";
                break;
            case "settings":
                _ = _settings.RefreshAccountStatesAsync();
                CurrentPage = _settings;
                CurrentPageKey = "settings";
                break;
            case "usage":
                    _usage.Reload();        // 先展示本地记录，再后台增量同步近 7 天
                    _ = _usage.RefreshCommand.ExecuteAsync(null);
                    CurrentPage = _usage;
                    CurrentPageKey = "usage";
                    break;
        }
    }
}