namespace CT_Translation.Services;

public interface ITranslationService
{
    event Action<string>? OnLog;
    Task<string> TranslateAsync(string text, string targetLanguage = "zh-CN");
    Task<Dictionary<string, string>> TranslateBatchAsync(List<string> texts, IProgress<int>? progress = null, CancellationToken cancellationToken = default, string targetLanguage = "zh-CN");
}

public class MockTranslationService : ITranslationService
{
    public event Action<string>? OnLog;

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

    public async Task<Dictionary<string, string>> TranslateBatchAsync(List<string> texts, IProgress<int>? progress = null, CancellationToken cancellationToken = default, string targetLanguage = "zh-CN")
    {
        var result = new Dictionary<string, string>();
        int total = texts.Count;
        int processed = 0;

        foreach (var text in texts)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            result[text] = $"[批量汉化] {text}";
            processed++;
            
            if (processed % 10 == 0 || processed == total)
            {
                progress?.Report(processed);
                await Task.Delay(10, cancellationToken); // 模拟一点延迟
            }
        }
        return result;
    }
}
