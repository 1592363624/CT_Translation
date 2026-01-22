using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CT_Translation.Models;

namespace CT_Translation.Services;

public class TencentTranslationService : ITranslationService
{
    public event Action<string>? OnLog;
    private readonly TencentConfig _config;
    private readonly HttpClient _httpClient;
    private const string Endpoint = "tmt.tencentcloudapi.com";
    private const string Service = "tmt";
    private const string Action = "TextTranslate";
    private const string Version = "2018-03-21";

    public TencentTranslationService(TencentConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage = "zh")
    {
        var result = await TranslateBatchAsync(new List<string> { text }, null, default, targetLanguage);
        return result.ContainsKey(text) ? result[text] : text;
    }

    public async Task<Dictionary<string, string>> TranslateBatchAsync(List<string> texts, IProgress<int>? progress = null, CancellationToken cancellationToken = default, string targetLanguage = "zh")
    {
        var result = new Dictionary<string, string>();
        if (texts == null || texts.Count == 0) return result;

        // 腾讯云建议批量请求不要过大，这里限制每次 20 条
        // 腾讯云 TextTranslateBatch 接口并未广泛开放，这里循环调用 TextTranslate 或者使用 TextTranslate 的批量能力？
        // 实际上 TextTranslate 接口只支持单条 SourceText。
        // 为了提高效率，这里使用并发调用。腾讯云默认 QPS 限制为 5。
        // 注意：TMT 其实有 TextTranslateBatch 接口，但需要白名单。普通用户只能用 TextTranslate。
        
        int completed = 0;
        var semaphore = new SemaphoreSlim(5); // 控制并发数，以免触发 QPS 限制
        var tasks = texts.Select(async text =>
        {
            if (cancellationToken.IsCancellationRequested) return;

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var translated = await TranslateSingleWithRetryAsync(text, targetLanguage);
                lock (result)
                {
                    result[text] = translated;
                    completed++;
                    progress?.Report(completed);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Tencent] Error translating '{text.Substring(0, Math.Min(10, text.Length))}...': {ex.Message}");
                lock (result)
                {
                    result[text] = text; // 失败回退到原文
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return result;
    }

    private async Task<string> TranslateSingleWithRetryAsync(string text, string target)
    {
        int retries = 3;
        for (int i = 0; i < retries; i++)
        {
            try
            {
                return await TranslateSingleInternalAsync(text, target);
            }
            catch (Exception ex)
            {
                if (i == retries - 1) throw;
                await Task.Delay(500 * (i + 1));
            }
        }
        return text;
    }

    private async Task<string> TranslateSingleInternalAsync(string text, string target)
    {
        // 构造请求体
        var requestPayload = new
        {
            SourceText = text,
            Source = "auto",
            Target = target,
            ProjectId = 0
        };
        string jsonPayload = JsonSerializer.Serialize(requestPayload);

        // 构造 V3 签名
        // 参考：https://cloud.tencent.com/document/api/213/30654
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        
        // 1. 拼接规范请求串
        string algorithm = "TC3-HMAC-SHA256";
        string httpRequestMethod = "POST";
        string canonicalUri = "/";
        string canonicalQueryString = "";
        string canonicalHeaders = $"content-type:application/json; charset=utf-8\nhost:{Endpoint}\n";
        string signedHeaders = "content-type;host";
        string hashedRequestPayload = Sha256Hex(jsonPayload);
        string canonicalRequest = $"{httpRequestMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedRequestPayload}";

        // 2. 拼接待签名字符串
        string credentialScope = $"{date}/{Service}/tc3_request";
        string hashedCanonicalRequest = Sha256Hex(canonicalRequest);
        string stringToSign = $"{algorithm}\n{timestamp}\n{credentialScope}\n{hashedCanonicalRequest}";

        // 3. 计算签名
        byte[] secretDate = HmacSha256(Encoding.UTF8.GetBytes("TC3" + _config.SecretKey), Encoding.UTF8.GetBytes(date));
        byte[] secretService = HmacSha256(secretDate, Encoding.UTF8.GetBytes(Service));
        byte[] secretSigning = HmacSha256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        byte[] signatureBytes = HmacSha256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));
        string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

        // 4. 构造 Authorization 头
        string authorization = $"{algorithm} Credential={_config.SecretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        // 发送请求
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Endpoint}");
        request.Headers.Add("Authorization", authorization);
        request.Headers.Add("Host", Endpoint);
        request.Headers.Add("X-TC-Action", Action);
        request.Headers.Add("X-TC-Version", Version);
        request.Headers.Add("X-TC-Timestamp", timestamp.ToString());
        request.Headers.Add("X-TC-Region", _config.Region);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Tencent API Error: {response.StatusCode} - {responseContent}");
        }

        // 解析响应
        using var doc = JsonDocument.Parse(responseContent);
        if (doc.RootElement.TryGetProperty("Response", out var responseObj))
        {
            if (responseObj.TryGetProperty("Error", out var errorObj))
            {
                throw new Exception($"API Error: {errorObj.GetProperty("Code").GetString()} - {errorObj.GetProperty("Message").GetString()}");
            }

            if (responseObj.TryGetProperty("TargetText", out var targetText))
            {
                return targetText.GetString() ?? text;
            }
        }

        return text;
    }

    private static string Sha256Hex(string s)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(s));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    private static byte[] HmacSha256(byte[] key, byte[] msg)
    {
        using var mac = new HMACSHA256(key);
        return mac.ComputeHash(msg);
    }
}
