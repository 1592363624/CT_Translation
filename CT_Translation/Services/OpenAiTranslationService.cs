using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CT_Translation.Models;

namespace CT_Translation.Services;

public class OpenAiTranslationService : ITranslationService
{
    public event Action<string> OnLog;
    private readonly OpenAiConfig _config;
    private readonly HttpClient _httpClient;

    public OpenAiTranslationService(OpenAiConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage = "zh-CN")
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var systemPrompt = _config.CustomSystemPrompt;
        if (string.IsNullOrWhiteSpace(systemPrompt)) systemPrompt = "You are a professional translator. Translate the following text to Simplified Chinese directly. Do not add any explanations or extra quotes unless they are part of the original text.";

        var messages = new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = text }
        };

        var requestBody = new
        {
            model = _config.Model,
            messages = messages,
            temperature = 0.3
        };

        try
        {
            OnLog?.Invoke($"[OpenAI] Translating single item...");
            var response = await SendRequestAsync(requestBody);
            return response?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? text;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[OpenAI] Error translating single item: {ex.Message}");
            return text;
        }
    }

    public async Task<Dictionary<string, string>> TranslateBatchAsync(List<string> texts, string targetLanguage = "zh-CN")
    {
        var result = new Dictionary<string, string>();
        if (texts == null || texts.Count == 0) return result;

        // 简单的批量处理：每批 20 条，避免 Token 超限
        int batchSize = 20;
        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var currentBatch = texts.Skip(i).Take(batchSize).ToList();
            var batchResult = await TranslateBatchInternalAsync(currentBatch);
            
            foreach (var kvp in batchResult)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, string>> TranslateBatchInternalAsync(List<string> texts)
    {
        var result = new Dictionary<string, string>();
        
        // 构造 Prompt，让 AI 返回 JSON 格式或者特定分隔符
        // 这里使用 JSON 数组格式，比较稳健
        var jsonContent = JsonSerializer.Serialize(texts);
        var prompt = $"Translate the following JSON array of strings to Simplified Chinese. Return ONLY a valid JSON array of strings. Do not include markdown formatting like ```json.\n\n{jsonContent}";

        var systemPrompt = _config.CustomSystemPrompt;
        if (string.IsNullOrWhiteSpace(systemPrompt)) systemPrompt = "You are a professional translator.";

        var messages = new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = prompt }
        };

        var requestBody = new
        {
            model = _config.Model,
            messages = messages,
            temperature = 0.1 // 低温度以保证格式稳定
        };

        try
        {
            OnLog?.Invoke($"[OpenAI] Sending batch request ({texts.Count} items)...");
            var response = await SendRequestAsync(requestBody);
            var content = response?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (!string.IsNullOrEmpty(content))
            {
                // 清理可能的 Markdown 标记
                content = content.Replace("```json", "").Replace("```", "").Trim();
                
                OnLog?.Invoke($"[OpenAI] Received response, length: {content.Length}");

                var translatedTexts = JsonSerializer.Deserialize<List<string>>(content);
                if (translatedTexts != null && translatedTexts.Count == texts.Count)
                {
                    for (int j = 0; j < texts.Count; j++)
                    {
                        result[texts[j]] = translatedTexts[j];
                    }
                    OnLog?.Invoke($"[OpenAI] Batch parsed successfully.");
                    return result;
                }
                else
                {
                     OnLog?.Invoke($"[OpenAI] Batch parsing mismatch: Sent {texts.Count}, Received {translatedTexts?.Count ?? 0}");
                }
            }
            else
            {
                 OnLog?.Invoke($"[OpenAI] Received empty content.");
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[OpenAI] Batch processing failed: {ex.Message}");
            // 批量失败，尝试逐个翻译
            // 或者直接返回原文
        }

        // 如果批量失败，回退到原文
        OnLog?.Invoke($"[OpenAI] Fallback to original text for this batch.");
        foreach (var text in texts)
        {
            if (!result.ContainsKey(text))
            {
                result[text] = text; 
            }
        }

        return result;
    }

    private async Task<OpenAiResponse?> SendRequestAsync(object requestBody)
    {
        // 自动处理 API URL 路径
        var apiUrl = _config.ApiUrl?.TrimEnd('/');
        if (!string.IsNullOrEmpty(apiUrl) && !apiUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            // 如果用户只填了域名或 /v1，尝试智能补全
            if (apiUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                apiUrl += "/chat/completions";
            }
            else
            {
                // 如果没有 v1，也没有 chat/completions，保守策略：
                // 很多中转商给的地址如 https://api.xxx.com/v1，但也有些是 https://api.xxx.com
                // 如果用户填的是 https://api.xiaomimimo.com/v1，我们需要补上 /chat/completions
                // 如果用户填的是 https://api.xiaomimimo.com，可能需要补上 /v1/chat/completions
                // 这里为了最大兼容性，我们检查是否已经包含 v1，如果包含则追加 /chat/completions
                // 如果不包含，暂时假设用户知道自己在做什么，或者可以尝试追加 /v1/chat/completions
                
                // 针对用户遇到的 https://api.xiaomimimo.com/v1 情况
                apiUrl += "/chat/completions";
            }
            OnLog?.Invoke($"[OpenAI] Auto-corrected API URL to: {apiUrl}");
        }
        
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");
        request.Content = JsonContent.Create(requestBody);

        var response = await _httpClient.SendAsync(request);
        
        // 增加更详细的错误日志
        if (!response.IsSuccessStatusCode)
        {
             var errorContent = await response.Content.ReadAsStringAsync();
             throw new HttpRequestException($"API request failed with status {response.StatusCode}. Response: {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync();
        
        // 尝试捕获非 JSON 响应（比如 HTML 错误页）
        if (json.TrimStart().StartsWith("<"))
        {
             throw new JsonException($"Received unexpected HTML response instead of JSON. Check your API URL. Preview: {json.Substring(0, Math.Min(100, json.Length))}...");
        }

        return JsonSerializer.Deserialize<OpenAiResponse>(json);
    }

    // 内部类用于反序列化响应
    private class OpenAiResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
