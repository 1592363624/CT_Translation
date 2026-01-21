using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
            _currentDoc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
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
                    
                    // 读取时逻辑：如果最外层是引号，则移除它，只保留核心内容
                    // 先 Trim 防止 XML 格式化产生的空白字符干扰判断
                    string displayValue = rawValue.Trim();
                    bool hasQuotes = false;
                    
                    if (displayValue.Length >= 2 && displayValue.StartsWith("\"") && displayValue.EndsWith("\""))
                    {
                        displayValue = displayValue.Substring(1, displayValue.Length - 2);
                        hasQuotes = true;
                    }
                    
                    Entries.Add(new CheatEntryModel
                    {
                        Id = idNode?.Value ?? "N/A",
                        OriginalDescription = displayValue, // 此时存储的是无引号的内容
                        TranslatedDescription = displayValue, // 默认翻译为原文
                        XmlElement = descNode,
                        HasQuotes = hasQuotes
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
                        // 保存时逻辑：根据 HasQuotes 决定是否添加引号
                        if (entry.HasQuotes)
                        {
                            entry.XmlElement.Value = $"\"{entry.TranslatedDescription}\"";
                        }
                        else
                        {
                            entry.XmlElement.Value = entry.TranslatedDescription;
                        }
                    }
                }

                SaveFilePreservingOriginalFormat(dialog.FileName);
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
    /// 清空界面内容命令
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        Entries.Clear();
        CurrentFilePath = string.Empty;
        _currentDoc = null;
        StatusMessage = "就绪";
    }

    private void SaveFilePreservingOriginalFormat(string outputPath)
    {
        if (_currentDoc == null || string.IsNullOrWhiteSpace(CurrentFilePath) || !File.Exists(CurrentFilePath))
        {
            return;
        }

        var sourceBytes = File.ReadAllBytes(CurrentFilePath);
        var bomInfo = DetectBom(sourceBytes);
        var encoding = bomInfo.Encoding;
        var sourceText = encoding.GetString(sourceBytes, bomInfo.PreambleLength, sourceBytes.Length - bomInfo.PreambleLength);

        var declaredEncoding = GetDeclaredEncodingName(sourceText);
        if (!string.IsNullOrWhiteSpace(declaredEncoding))
        {
            var declared = CreateEncodingFromName(declaredEncoding, bomInfo.HasBom);
            if (!string.Equals(declared.WebName, encoding.WebName, StringComparison.OrdinalIgnoreCase))
            {
                encoding = declared;
                sourceText = encoding.GetString(sourceBytes, bomInfo.PreambleLength, sourceBytes.Length - bomInfo.PreambleLength);
            }
        }

        var newline = sourceText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var updatedValues = _currentDoc.Descendants("Description")
            .Select(d => NormalizeLineEndings(d.Value, newline))
            .ToList();

        var matches = Regex.Matches(sourceText, "(<Description[^>]*>)([\\s\\S]*?)(</Description>)", RegexOptions.Singleline);
        if (matches.Count == 0)
        {
            WriteTextWithEncoding(outputPath, sourceText, encoding, bomInfo.HasBom);
            return;
        }

        var builder = new StringBuilder(sourceText.Length + 256);
        var lastIndex = 0;
        var valueIndex = 0;

        foreach (Match match in matches)
        {
            builder.Append(sourceText, lastIndex, match.Index - lastIndex);

            var originalInner = match.Groups[2].Value;
            var newValue = valueIndex < updatedValues.Count ? updatedValues[valueIndex] : originalInner;
            valueIndex++;

            var leading = GetLeadingWhitespace(originalInner);
            var trailing = GetTrailingWhitespace(originalInner);
            string replacedInner;

            if (IsCdataWrapped(originalInner))
            {
                replacedInner = leading + WrapCdata(newValue) + trailing;
            }
            else
            {
                var escaped = EncodeXmlText(newValue);
                replacedInner = leading + escaped + trailing;
            }

            builder.Append(match.Groups[1].Value);
            builder.Append(replacedInner);
            builder.Append(match.Groups[3].Value);
            lastIndex = match.Index + match.Length;
        }

        builder.Append(sourceText, lastIndex, sourceText.Length - lastIndex);
        WriteTextWithEncoding(outputPath, builder.ToString(), encoding, bomInfo.HasBom);
    }

    private static (Encoding Encoding, bool HasBom, int PreambleLength) DetectBom(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(true), true, 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (new UnicodeEncoding(false, true), true, 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (new UnicodeEncoding(true, true), true, 2);
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return (new UTF32Encoding(false, true), true, 4);
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return (new UTF32Encoding(true, true), true, 4);
        }

        return (new UTF8Encoding(false), false, 0);
    }

    private static string? GetDeclaredEncodingName(string text)
    {
        var match = Regex.Match(text, @"<\?xml[^>]*encoding\s*=\s*[""'](?<enc>[^""']+)[""']",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["enc"].Value : null;
    }

    private static Encoding CreateEncodingFromName(string name, bool withBom)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return normalized switch
        {
            "utf-8" or "utf8" => new UTF8Encoding(withBom),
            "utf-16" or "utf-16le" or "utf16" => new UnicodeEncoding(false, withBom),
            "utf-16be" or "utf16be" => new UnicodeEncoding(true, withBom),
            "utf-32" or "utf-32le" or "utf32" => new UTF32Encoding(false, withBom),
            "utf-32be" or "utf32be" => new UTF32Encoding(true, withBom),
            _ => Encoding.GetEncoding(name)
        };
    }

    private static string NormalizeLineEndings(string text, string newline)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return newline == "\n" ? normalized : normalized.Replace("\n", newline);
    }

    private static string GetLeadingWhitespace(string text)
    {
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index == 0 ? string.Empty : text.Substring(0, index);
    }

    private static string GetTrailingWhitespace(string text)
    {
        var index = text.Length - 1;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        return index == text.Length - 1 ? string.Empty : text.Substring(index + 1);
    }

    private static bool IsCdataWrapped(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith("<![CDATA[", StringComparison.Ordinal) && trimmed.EndsWith("]]>", StringComparison.Ordinal);
    }

    private static string WrapCdata(string value)
    {
        var safeValue = value.Replace("]]>", "]]]]><![CDATA[>");
        return $"<![CDATA[{safeValue}]]>";
    }

    private static string EncodeXmlText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var element = new XElement("Description", value).ToString(SaveOptions.DisableFormatting);
        if (string.Equals(element, "<Description />", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var start = element.IndexOf('>');
        var end = element.LastIndexOf("</Description>", StringComparison.Ordinal);
        return start >= 0 && end > start ? element.Substring(start + 1, end - start - 1) : value;
    }

    private static void WriteTextWithEncoding(string path, string text, Encoding encoding, bool withBom)
    {
        var outputEncoding = CreateEncodingFromName(encoding.WebName, withBom);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, outputEncoding);
        writer.Write(text);
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
                // 修复引号问题：将中文全角引号替换回英文半角引号
                // 虽然我们现在只处理内容，但翻译结果偶尔还是可能带上奇怪的标点，保险起见还是处理一下
                if (!string.IsNullOrEmpty(translatedText))
                {
                    // 移除可能被翻译引擎误加的外层引号（不管是中文还是英文）
                    // 因为我们现在是“纯内容”翻译，不需要翻译带回引号
                    translatedText = translatedText.Trim();
                    if (translatedText.StartsWith("\"") && translatedText.EndsWith("\""))
                        translatedText = translatedText.Substring(1, translatedText.Length - 2);
                    else if (translatedText.StartsWith("“") && translatedText.EndsWith("”"))
                        translatedText = translatedText.Substring(1, translatedText.Length - 2);
                        
                    // 替换中间可能出现的中文引号
                    translatedText = translatedText.Replace("“", "\"").Replace("”", "\"");
                }
                
                entry.TranslatedDescription = translatedText;
                successCount++;
            }
        }
        
        stopwatch.Stop();
        StatusMessage = $"自动汉化完成，共处理 {Entries.Count} 个条目，成功匹配 {successCount} 个，耗时 {stopwatch.Elapsed.TotalSeconds:F2} 秒";
    }
}
