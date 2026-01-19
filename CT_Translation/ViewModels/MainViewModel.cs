using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CT_Translation.Models;
using CT_Translation.Services;
using Microsoft.Win32;

namespace CT_Translation.ViewModels;

/// <summary>
/// 主窗口 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITranslationService _translationService;
    private XDocument? _currentDoc;

    [ObservableProperty]
    private string _currentFilePath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CheatEntryModel> _entries = new();

    [ObservableProperty]
    private string _statusMessage = "就绪";

    public MainViewModel()
    {
        // 使用 Google 翻译服务
        _translationService = new GoogleTranslationService();
    }

    /// <summary>
    /// 打开 CT 文件命令
    /// </summary>
    [RelayCommand]
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Cheat Table (*.CT)|*.CT|All Files (*.*)|*.*",
            Title = "打开 Cheat Table 文件"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadFile(dialog.FileName);
        }
    }

    /// <summary>
    /// 加载文件逻辑
    /// </summary>
    /// <param name="path"></param>
    public void LoadFile(string path)
    {
        try
        {
            CurrentFilePath = path;
            _currentDoc = XDocument.Load(path);
            Entries.Clear();

            if (_currentDoc.Root == null) return;

            // 递归查找所有 CheatEntry
            var cheatEntries = _currentDoc.Descendants("CheatEntry");
            foreach (var entry in cheatEntries)
            {
                var descNode = entry.Element("Description");
                var idNode = entry.Element("ID");

                if (descNode != null)
                {
                    // CT 文件中的 Description 通常包含引号，例如 "Description"，我们需要处理一下
                    // 但通常保存回去也要带引号，所以最好原样保留，或者在 UI 上去引号，保存时加回去
                    // 观察用户提供的例子：<Description>"========= X4 v8.00 ========="</Description>
                    // 内容是带双引号的。

                    var rawValue = descNode.Value;
                    
                    Entries.Add(new CheatEntryModel
                    {
                        Id = idNode?.Value ?? "N/A",
                        OriginalDescription = rawValue,
                        TranslatedDescription = rawValue, // 默认翻译为原文
                        XmlElement = descNode
                    });
                }
            }
            StatusMessage = $"已加载 {Entries.Count} 个条目";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
            MessageBox.Show($"无法加载文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 保存文件命令
    /// </summary>
    [RelayCommand]
    private void SaveFile()
    {
        if (_currentDoc == null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Cheat Table (*.CT)|*.CT|All Files (*.*)|*.*",
            FileName = Path.GetFileNameWithoutExtension(CurrentFilePath) + "_CN.CT",
            Title = "保存汉化后的 Cheat Table"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // 将 ViewModel 中的修改应用到 XML
                foreach (var entry in Entries)
                {
                    if (entry.XmlElement != null)
                    {
                        entry.XmlElement.Value = entry.TranslatedDescription;
                    }
                }

                _currentDoc.Save(dialog.FileName);
                StatusMessage = "保存成功";
                MessageBox.Show("文件保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存失败: {ex.Message}";
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 模拟自动汉化命令（这里可以接入翻译 API）
    /// </summary>
    [RelayCommand]
    private async Task AutoTranslate()
    {
        if (Entries.Count == 0) return;

        StatusMessage = "正在收集需要翻译的条目...";
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // 收集所有需要翻译的文本
        var toTranslate = Entries.Select(e => e.OriginalDescription).Distinct().ToList();
        
        if (toTranslate.Count == 0)
        {
            StatusMessage = "没有需要翻译的条目";
            return;
        }

        StatusMessage = $"正在批量翻译 {toTranslate.Count} 个唯一文本...";

        // 调用批量翻译
        var translations = await _translationService.TranslateBatchAsync(toTranslate);

        // 回写结果
        int successCount = 0;
        foreach (var entry in Entries)
        {
            if (translations.TryGetValue(entry.OriginalDescription, out var translatedText))
            {
                entry.TranslatedDescription = translatedText;
                successCount++;
            }
        }
        
        stopwatch.Stop();
        StatusMessage = $"自动汉化完成，共处理 {Entries.Count} 个条目，成功匹配 {successCount} 个，耗时 {stopwatch.Elapsed.TotalSeconds:F2} 秒";
    }
}
