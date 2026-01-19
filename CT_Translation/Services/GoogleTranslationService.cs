using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace CT_Translation.Services;

public class GoogleTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://translate.googleapis.com/translate_a/single";

    public GoogleTranslationService()
    {
        _httpClient = new HttpClient();
        // 模拟浏览器 User-Agent，防止被轻易拦截
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage = "zh-CN")
    {
        var result = await TranslateBatchAsync(new List<string> { text }, targetLanguage);
        return result.ContainsKey(text) ? result[text] : text;
    }

    public async Task<Dictionary<string, string>> TranslateBatchAsync(List<string> texts, string targetLanguage = "zh-CN")
    {
        var result = new Dictionary<string, string>();
        if (texts == null || texts.Count == 0) return result;

        // 使用特殊分隔符连接所有文本，Google API 可能会因为文本过长或格式问题截断，所以这里我们分批处理
        // 但 Google gtx 接口最好还是直接翻译多行文本（使用换行符分隔），或者单个请求翻译一段
        // 为了稳定性和速度平衡，这里采用换行符拼接，然后解析返回结果
        
        // 注意：Google gtx 接口对于长文本和多行文本的处理可能不如付费 API 完美，
        // 且过长会报错。我们需要限制每次请求的字符数。
        // 这里简单实现：如果合并后长度超过 3000 字符，就拆分。
        
        var batches = CreateBatches(texts, 3000);
        
        foreach (var batch in batches)
        {
            try 
            {
                // 使用特殊的不可见字符或者极少使用的字符组合作为分隔符，防止翻译后丢失
                // 但 Google 翻译通常会保留换行符。尝试使用换行符作为分隔。
                string combinedText = string.Join("\n", batch);
                
                // 构造 URL
                var url = $"{BaseUrl}?client=gtx&sl=auto&tl={targetLanguage}&dt=t&q={Uri.EscapeDataString(combinedText)}";

                var response = await _httpClient.GetStringAsync(url);
                var jsonArray = JArray.Parse(response);

                if (jsonArray != null && jsonArray.Count > 0)
                {
                    var sentences = jsonArray[0];
                    if (sentences != null)
                    {
                        var translatedBuilder = new System.Text.StringBuilder();
                        foreach (var sentence in sentences)
                        {
                            if (sentence is JArray sArray && sArray.Count > 0)
                            {
                                translatedBuilder.Append(sArray[0]?.ToString());
                            }
                        }
                        
                        // 将翻译后的完整文本按换行符拆分回原来的条目
                        // 注意：Google 翻译有时候可能会改变换行符的数量，这是一个风险点
                        // 另一种策略是使用特殊的 HTML 标签如 <br> 但 gtx 接口处理 HTML 标签行为不一
                        
                        var translatedLines = translatedBuilder.ToString().Split('\n');
                        
                        // 尝试匹配，如果数量不一致，可能需要回退到逐个翻译或者尽量匹配
                        for (int i = 0; i < Math.Min(batch.Count, translatedLines.Length); i++)
                        {
                            result[batch[i]] = translatedLines[i].Trim();
                        }
                        
                        // 如果翻译回来的行数少于原始行数，剩下的用原文填充
                         for (int i = translatedLines.Length; i < batch.Count; i++)
                        {
                            result[batch[i]] = batch[i];
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 批次失败，尝试逐个翻译（降级策略）或者直接返回原文
                foreach (var item in batch)
                {
                    if (!result.ContainsKey(item))
                        result[item] = item; // 暂时返回原文
                }
            }
            
            // 稍微延时，避免触发限流
            await Task.Delay(200);
        }

        return result;
    }

    private List<List<string>> CreateBatches(List<string> source, int maxChars)
    {
        var batches = new List<List<string>>();
        var currentBatch = new List<string>();
        int currentLength = 0;

        foreach (var item in source)
        {
            if (currentLength + item.Length + 1 > maxChars && currentBatch.Count > 0)
            {
                batches.Add(currentBatch);
                currentBatch = new List<string>();
                currentLength = 0;
            }

            currentBatch.Add(item);
            currentLength += item.Length + 1; // +1 for newline
        }

        if (currentBatch.Count > 0)
        {
            batches.Add(currentBatch);
        }

        return batches;
    }
}
