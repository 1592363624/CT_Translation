namespace CT_Translation.Services;

public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string targetLanguage = "zh-CN");
    Task<Dictionary<string, string>> TranslateBatchAsync(List<string> texts, string targetLanguage = "zh-CN");
}

public class MockTranslationService : ITranslationService
{
    public Task<string> TranslateAsync(string text, string targetLanguage = "zh-CN")
    {
        // 简单的模拟翻译逻辑
        // 如果是引用的字符串，保留引号
        if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length > 2)
        {
            var content = text.Substring(1, text.Length - 2);
            return Task.FromResult($"\"[汉化] {content}\"");
        }
        
        return Task.FromResult($"[汉化] {text}");
    }

    public Task<Dictionary<string, string>> TranslateBatchAsync(List<string> texts, string targetLanguage = "zh-CN")
    {
        var result = new Dictionary<string, string>();
        foreach (var text in texts)
        {
            result[text] = $"[批量汉化] {text}";
        }
        return Task.FromResult(result);
    }
}
