using CommunityToolkit.Mvvm.ComponentModel;
using System.Xml.Linq;

namespace CT_Translation.Models;

/// <summary>
/// 表示一个 Cheat Engine 条目模型
/// </summary>
public partial class CheatEntryModel : ObservableObject
{
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
    /// 对应的 XML 元素，用于保存回写
    /// </summary>
    public XElement? XmlElement { get; set; }

    /// <summary>
    /// 标记原始值是否被双引号包裹
    /// </summary>
    public bool HasQuotes { get; set; }
}
