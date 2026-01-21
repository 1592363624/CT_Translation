using System.IO;
using System.Text.Json;
using CT_Translation.Models;

namespace CT_Translation.Services;

public interface IConfigService
{
    AppConfig Config { get; }
    void Save();
    void Load();
}

public class ConfigService : IConfigService
{
    private const string ConfigFileName = "config.json";
    private readonly string _configPath;
    
    public AppConfig Config { get; private set; }

    public ConfigService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        Config = new AppConfig();
        Load();
    }

    public void Load()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    Config = config;
                }
            }
            catch
            {
                // 加载失败使用默认配置
            }
        }
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Config, options);
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // 保存失败处理
        }
    }
}
