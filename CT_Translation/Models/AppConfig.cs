using System.Text.Json.Serialization;

namespace CT_Translation.Models;

public class AppConfig
{
    public string SelectedProvider { get; set; } = "GoogleFree"; // GoogleFree, OpenAI, Tencent

    public GoogleConfig Google { get; set; } = new();
    public OpenAiConfig OpenAi { get; set; } = new();
    public TencentConfig Tencent { get; set; } = new();
}

public class GoogleConfig
{
    public string BaseUrl { get; set; } = "https://translate.googleapis.com/translate_a/single";
}

public class TencentConfig
{
    public string SecretId { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Region { get; set; } = "ap-shanghai"; // 默认地域
}

public class OpenAiConfig
{
    public string ApiUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-3.5-turbo";
    public string CustomSystemPrompt { get; set; } = "You are a professional translator.";
}
