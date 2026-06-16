using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace CT_Translation.Models;

public partial class DropDownLineModel : ObservableObject
{
    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    public string Value { get; set; } = string.Empty;
    public bool HasValue { get; set; }
    
    /// <summary>
    /// 原始的完整行文本（用于调试和回退）
    /// </summary>
    public string RawLine { get; set; } = string.Empty;
}

/// <summary>
/// 表示一个 Cheat Engine 条目模型
/// </summary>
public partial class CheatEntryModel : ObservableObject
{
    /// <summary>
    /// 序号 (从1开始)
    /// </summary>
    [ObservableProperty]
    private int _index;
    
    /// <summary>
    /// 是否包含下拉列表
    /// </summary>
    [ObservableProperty]
    private bool _hasDropDown;

    /// <summary>
    /// 条目 ID
    /// </summary>
    [ObservableProperty]
    private string _id = string.Empty;

    /// <summary>
    /// 原始描述
    /// </summary>
    [ObservableProperty]
    private string _originalDescription = string.Empty;

    /// <summary>
    /// 翻译后的描述
    /// </summary>
    [ObservableProperty]
    private string _translatedDescription = string.Empty;

    /// <summary>
    /// 下拉列表行
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DropDownLineModel> _dropDownLines = new();

    /// <summary>
    /// 对应的 XML 元素，用于保存回写
    /// </summary>
    public XElement? XmlElement { get; set; }

    /// <summary>
    /// 对应的 DropDownList XML 元素
    /// </summary>
    public XElement? DropDownListElement { get; set; }

    /// <summary>
    /// 对应的 DropDownListLink XML 元素
    /// </summary>
    public XElement? DropDownListLinkElement { get; set; }

    /// <summary>
    /// 原始的 DropDownListLink 文本
    /// </summary>
    [ObservableProperty]
    private string _originalDropDownLinkText = string.Empty;

    /// <summary>
    /// 翻译后的 DropDownListLink 文本
    /// </summary>
    [ObservableProperty]
    private string _translatedDropDownLinkText = string.Empty;

    /// <summary>
    /// 标记原始值是否被双引号包裹
    /// </summary>
    public bool HasQuotes { get; set; }
}
