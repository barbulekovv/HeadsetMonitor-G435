
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace HeadsetMonitor
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public AppSettings Settings { get; private set; } = new AppSettings();

        public event EventHandler? SaveRequested;
        public event EventHandler? CancelRequested;

        public SettingsView()
        {
            InitializeComponent();
            LoadSettings();
            ApplyTheme();
        }

        public void SetSettings(AppSettings currentSettings)
        {
            Settings = new AppSettings
            {
                Theme = currentSettings.Theme,
                StartWithWindows = currentSettings.StartWithWindows,
                StartMinimizedToTray = currentSettings.StartMinimizedToTray,
                MinimizeToTrayOnClose = currentSettings.MinimizeToTrayOnClose,
                AutoUpdate = currentSettings.AutoUpdate,
                UpdateIntervalMinutes = currentSettings.UpdateIntervalMinutes,
                LowBatteryNotification = currentSettings.LowBatteryNotification,
                LowBatteryThreshold = currentSettings.LowBatteryThreshold,
                ShowTechnicalInfo = currentSettings.ShowTechnicalInfo,
                ShowTrayPercentage = currentSettings.ShowTrayPercentage
            };

            LoadSettings();
            ApplyTheme();
        }

        private void LoadSettings()
        {
            ThemeComboBox.SelectedIndex = Settings.Theme switch
            {
                "Dark" => 0,
                "Light" => 1,
                _ => 2
            };

            StartWithWindowsCheckBox.IsChecked = Settings.StartWithWindows;
            StartMinimizedCheckBox.IsChecked = Settings.StartMinimizedToTray;
            MinimizeToTrayCheckBox.IsChecked = Settings.MinimizeToTrayOnClose;
            LowBatteryCheckBox.IsChecked = Settings.LowBatteryNotification;
            SelectLowBatteryThreshold(Settings.LowBatteryThreshold);
            TrayPercentageCheckBox.IsChecked = Settings.ShowTrayPercentage;
            TechnicalInfoCheckBox.IsChecked = Settings.ShowTechnicalInfo;
        }

        private void SelectLowBatteryThreshold(int threshold)
        {
            for (int i = 0; i < LowBatteryThresholdComboBox.Items.Count; i++)
            {
                if (LowBatteryThresholdComboBox.Items[i] is ComboBoxItem item &&
                    int.TryParse(item.Tag?.ToString(), out int value) &&
                    value == threshold)
                {
                    LowBatteryThresholdComboBox.SelectedIndex = i;
                    return;
                }
            }

            LowBatteryThresholdComboBox.SelectedIndex = 1;
        }

        public void ApplyTheme()
        {
            bool isLightTheme;

            if (Settings.Theme == "Light")
            {
                isLightTheme = true;
            }
            else if (Settings.Theme == "Dark")
            {
                isLightTheme = false;
            }
            else
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? value = key?.GetValue("AppsUseLightTheme");
                isLightTheme = value is int themeValue && themeValue == 1;
            }

            if (isLightTheme)
            {
                Resources["WindowBackground"] = new SolidColorBrush(MediaColor.FromRgb(245, 246, 250));
                Resources["WindowForeground"] = new SolidColorBrush(MediaColor.FromRgb(30, 32, 38));
                Resources["PanelBrush"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
                Resources["MutedBrush"] = new SolidColorBrush(MediaColor.FromRgb(105, 110, 122));
                Resources["AccentBrush"] = new SolidColorBrush(MediaColor.FromRgb(75, 125, 230));
                Resources["ComboBackground"] = new SolidColorBrush(MediaColor.FromRgb(247, 248, 252));
                Resources["ComboBorder"] = new SolidColorBrush(MediaColor.FromRgb(185, 195, 215));
                Resources["ComboHighlight"] = new SolidColorBrush(MediaColor.FromRgb(218, 229, 250));
                Resources["ComboSelected"] = new SolidColorBrush(MediaColor.FromRgb(194, 213, 246));
                Resources["ComboHighlightForeground"] = new SolidColorBrush(MediaColor.FromRgb(30, 32, 38));
                Resources["ComboSelectedForeground"] = new SolidColorBrush(MediaColor.FromRgb(30, 32, 38));
            }
            else
            {
                Resources["WindowBackground"] = new SolidColorBrush(MediaColor.FromRgb(16, 17, 22));
                Resources["WindowForeground"] = MediaBrushes.White;
                Resources["PanelBrush"] = new SolidColorBrush(MediaColor.FromRgb(29, 31, 39));
                Resources["MutedBrush"] = new SolidColorBrush(MediaColor.FromRgb(155, 160, 175));
                Resources["AccentBrush"] = new SolidColorBrush(MediaColor.FromRgb(90, 140, 255));
                Resources["ComboBackground"] = new SolidColorBrush(MediaColor.FromRgb(35, 37, 46));
                Resources["ComboBorder"] = new SolidColorBrush(MediaColor.FromRgb(65, 68, 80));
                Resources["ComboHighlight"] = new SolidColorBrush(MediaColor.FromRgb(65, 85, 125));
                Resources["ComboSelected"] = new SolidColorBrush(MediaColor.FromRgb(75, 100, 150));
                Resources["ComboHighlightForeground"] = MediaBrushes.White;
                Resources["ComboSelectedForeground"] = MediaBrushes.White;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Theme = ThemeComboBox.SelectedIndex switch
            {
                0 => "Dark",
                1 => "Light",
                _ => "System"
            };

            Settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            Settings.StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true;
            Settings.MinimizeToTrayOnClose = MinimizeToTrayCheckBox.IsChecked == true;
            Settings.LowBatteryNotification = LowBatteryCheckBox.IsChecked == true;

            if (LowBatteryThresholdComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int threshold))
            {
                Settings.LowBatteryThreshold = threshold;
            }

            Settings.ShowTrayPercentage = TrayPercentageCheckBox.IsChecked == true;
            Settings.ShowTechnicalInfo = TechnicalInfoCheckBox.IsChecked == true;

            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
