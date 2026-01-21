using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CT_Translation.Models;
using CT_Translation.Services;
using System.Windows;

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

    public SettingsViewModel(IConfigService configService)
    {
        _configService = configService;
        
        // 加载当前配置到 ViewModel 属性
        _selectedProvider = _configService.Config.SelectedProvider;
        _openAiApiUrl = _configService.Config.OpenAi.ApiUrl;
        _openAiApiKey = _configService.Config.OpenAi.ApiKey;
        _openAiModel = _configService.Config.OpenAi.Model;
        _customSystemPrompt = _configService.Config.OpenAi.CustomSystemPrompt;
    }

    /// <summary>
    /// 保存设置并关闭窗口
    /// </summary>
    /// <param name="window">窗口实例</param>
    [RelayCommand]
    private void Save(Window window)
    {
        // 更新配置对象
        _configService.Config.SelectedProvider = SelectedProvider;
        _configService.Config.OpenAi.ApiUrl = OpenAiApiUrl;
        _configService.Config.OpenAi.ApiKey = OpenAiApiKey;
        _configService.Config.OpenAi.Model = OpenAiModel;
        _configService.Config.OpenAi.CustomSystemPrompt = CustomSystemPrompt;

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
}
