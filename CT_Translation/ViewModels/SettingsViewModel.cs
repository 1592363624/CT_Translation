using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CT_Translation.Models;
using CT_Translation.Services;

namespace CT_Translation.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;

    [ObservableProperty]
    private string _selectedProvider;

    [ObservableProperty]
    private string _openAiApiUrl;

    [ObservableProperty]
    private string _openAiApiKey;

    [ObservableProperty]
    private string _openAiModel;

    [ObservableProperty]
    private string _customSystemPrompt;

    [ObservableProperty]
    private string _googleBaseUrl;

    [ObservableProperty]
    private string _tencentSecretId;

    [ObservableProperty]
    private string _tencentSecretKey;

    public SettingsViewModel(IConfigService configService)
    {
        _configService = configService;
        
        // 加载当前配置到 ViewModel 属性
        _selectedProvider = _configService.Config.SelectedProvider;
        _openAiApiUrl = _configService.Config.OpenAi.ApiUrl;
        _openAiApiKey = _configService.Config.OpenAi.ApiKey;
        _openAiModel = _configService.Config.OpenAi.Model;
        _customSystemPrompt = _configService.Config.OpenAi.CustomSystemPrompt;
        _googleBaseUrl = _configService.Config.Google.BaseUrl;
        _tencentSecretId = _configService.Config.Tencent.SecretId;
        _tencentSecretKey = _configService.Config.Tencent.SecretKey;
    }

    [RelayCommand]
    private void Save(Window window)
    {
        // 更新 Config 对象
        _configService.Config.SelectedProvider = SelectedProvider;
        _configService.Config.OpenAi.ApiUrl = OpenAiApiUrl;
        _configService.Config.OpenAi.ApiKey = OpenAiApiKey;
        _configService.Config.OpenAi.Model = OpenAiModel;
        _configService.Config.OpenAi.CustomSystemPrompt = CustomSystemPrompt;
        _configService.Config.Google.BaseUrl = GoogleBaseUrl;
        _configService.Config.Tencent.SecretId = TencentSecretId;
        _configService.Config.Tencent.SecretKey = TencentSecretKey;

        // 持久化保存
        _configService.Save();

        // 关闭窗口
        window?.Close();
    }

    /// <summary>
    /// 取消并关闭窗口
    /// </summary>
    /// <param name="window">窗口实例</param>
    [RelayCommand]
    private void Cancel(Window window)
    {
        window?.Close();
    }

    [RelayCommand]
    private void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // 简单处理：如果无法打开，可以记录日志或忽略
            Debug.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }
}
