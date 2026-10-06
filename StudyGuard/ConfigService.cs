using System.IO;
using System.Text.Json;

namespace StudyGuard
{
    public class ConfigService
    {
        private readonly string configPath;

        public StudyGuardConfig Config { get; private set; }

        public ConfigService()
        {
            configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "config.json"
            );

            Config = Load();
        }

        private StudyGuardConfig Load()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    var defaultConfig = new StudyGuardConfig();
                    Save(defaultConfig);

                    return defaultConfig;
                }

                string json = File.ReadAllText(configPath);

                return JsonSerializer.Deserialize<StudyGuardConfig>(json)
                       ?? new StudyGuardConfig();
            }
            catch
            {
                return new StudyGuardConfig();
            }
        }

        public void Save(StudyGuardConfig config)
        {
            Config = config;

            string json = JsonSerializer.Serialize(
                config,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(configPath, json);
        }
    }
}