using System.Text.Json.Serialization;

namespace CT_Translation.Models;

public class AppConfig
{
    public string SelectedProvider { get; set; } = "GoogleFree"; // GoogleFree, OpenAI

    public OpenAiConfig OpenAi { get; set; } = new();
}

public class OpenAiConfig
{
    public string ApiUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-3.5-turbo";
    public string CustomSystemPrompt { get; set; } = "You are a professional translator.";
}
