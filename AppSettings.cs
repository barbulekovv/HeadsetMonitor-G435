using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HeadsetMonitor
{
    public class AppSettings
    {
        public string Language { get; set; } = "ru";

        public string Theme { get; set; } = "Dark";

        public string AccentColor { get; set; } = "Purple";

        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimizedToTray { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public bool RememberWindowPosition { get; set; } = true;

        public double WindowTop { get; set; } = -1;
        public double WindowLeft { get; set; } = -1;
        public double WindowWidth { get; set; } = 820;
        public double WindowHeight { get; set; } = 560;

        public bool AutoUpdate { get; set; } = true;
        public int UpdateIntervalMinutes { get; set; } = 15;

        public bool LowBatteryNotification { get; set; } = true;
        public int LowBatteryThreshold { get; set; } = 20;
        public bool FullBatteryNotification { get; set; } = true;

        public bool ShowTechnicalInfo { get; set; } = false;
        public bool ShowTrayPercentage { get; set; } = true;

        private static readonly string SettingsFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "HeadsetMonitor");

        private static readonly string SettingsPath =
            Path.Combine(SettingsFolder, "settings.json");

        private static readonly string TemporarySettingsPath =
            Path.Combine(SettingsFolder, "settings.tmp");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new AppSettings();

                string json = File.ReadAllText(SettingsPath);

                AppSettings settings =
                    JsonSerializer.Deserialize<AppSettings>(json)
                    ?? new AppSettings();

                settings.Validate();

                return settings;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);

                Validate();

                string json = JsonSerializer.Serialize(
                    this,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(TemporarySettingsPath, json);

                File.Move(
                    TemporarySettingsPath,
                    SettingsPath,
                    true);
            }
            catch
            {
                try
                {
                    if (File.Exists(TemporarySettingsPath))
                        File.Delete(TemporarySettingsPath);
                }
                catch
                {
                }
            }
        }

        private void Validate()
        {
            if (Language != "ru" && Language != "en")
                Language = "ru";

            if (Theme != "Dark" &&
                Theme != "Light" &&
                Theme != "System")
            {
                Theme = "Dark";
            }

            if (!AccentPalette.Presets.Any(
                preset => preset.Key == AccentColor))
            {
                AccentColor = "Purple";
            }

            if (UpdateIntervalMinutes != 5 &&
                UpdateIntervalMinutes != 15 &&
                UpdateIntervalMinutes != 30 &&
                UpdateIntervalMinutes != 60)
            {
                UpdateIntervalMinutes = 15;
            }

            if (LowBatteryThreshold != 10 &&
                LowBatteryThreshold != 15 &&
                LowBatteryThreshold != 20 &&
                LowBatteryThreshold != 25)
            {
                LowBatteryThreshold = 20;
            }

            if (WindowWidth < 450 || WindowWidth > 10000)
                WindowWidth = 820;

            if (WindowHeight < 400 || WindowHeight > 10000)
                WindowHeight = 560;

            if (double.IsNaN(WindowLeft) ||
                double.IsInfinity(WindowLeft))
            {
                WindowLeft = -1;
            }

            if (double.IsNaN(WindowTop) ||
                double.IsInfinity(WindowTop))
            {
                WindowTop = -1;
            }
        }
    }
}