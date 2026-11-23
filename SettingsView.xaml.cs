using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

using WpfRadioButton = System.Windows.Controls.RadioButton;
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
                Language = currentSettings.Language,
                Theme = currentSettings.Theme,
                AccentColor = currentSettings.AccentColor,

                StartWithWindows = currentSettings.StartWithWindows,
                StartMinimizedToTray = currentSettings.StartMinimizedToTray,
                MinimizeToTrayOnClose = currentSettings.MinimizeToTrayOnClose,
                RememberWindowPosition = currentSettings.RememberWindowPosition,

                WindowTop = currentSettings.WindowTop,
                WindowLeft = currentSettings.WindowLeft,
                WindowWidth = currentSettings.WindowWidth,
                WindowHeight = currentSettings.WindowHeight,

                AutoUpdate = currentSettings.AutoUpdate,
                UpdateIntervalMinutes = currentSettings.UpdateIntervalMinutes,

                LowBatteryNotification = currentSettings.LowBatteryNotification,
                LowBatteryThreshold = currentSettings.LowBatteryThreshold,
                FullBatteryNotification = currentSettings.FullBatteryNotification,

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

            FullBatteryCheckBox.IsChecked = Settings.FullBatteryNotification;
            TrayPercentageCheckBox.IsChecked = Settings.ShowTrayPercentage;
            TechnicalInfoCheckBox.IsChecked = Settings.ShowTechnicalInfo;

            SelectLanguage(Settings.Language);

            SelectAccentColor(Settings.AccentColor);
        }

        private void SelectLanguage(string? language)
        {
            string selectedLanguage = language == "en" ? "en" : "ru";

            for (int i = 0; i < LanguageComboBox.Items.Count; i++)
            {
                if (LanguageComboBox.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == selectedLanguage)
                {
                    LanguageComboBox.SelectedIndex = i;
                    return;
                }
            }

            LanguageComboBox.SelectedIndex = 0;
        }

        private void SelectAccentColor(string? accentColor)
        {
            string selectedColor = AccentPalette.Get(accentColor).Key;

            foreach (WpfRadioButton radioButton in AccentColorPanel.Children.OfType<WpfRadioButton>())
            {
                radioButton.IsChecked =
                    string.Equals(
                        radioButton.Tag?.ToString(),
                        selectedColor,
                        StringComparison.OrdinalIgnoreCase);
            }

            Settings.AccentColor = selectedColor;
        }

        private void AccentColor_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not WpfRadioButton radioButton)
                return;

            if (radioButton.Tag is not string colorKey)
                return;

            Settings.AccentColor = AccentPalette.Get(colorKey).Key;

            ApplyTheme();
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

            AccentPreset accent = AccentPalette.Get(Settings.AccentColor);

            Resources["AccentBrush"] = new SolidColorBrush(accent.Accent);
            Resources["AccentHover"] = new SolidColorBrush(accent.Hover);
            Resources["AccentPressed"] = new SolidColorBrush(accent.Pressed);

            if (isLightTheme)
            {
                Resources["WindowBackground"] =
                    new SolidColorBrush(MediaColor.FromRgb(245, 246, 250));

                Resources["WindowForeground"] =
                    new SolidColorBrush(MediaColor.FromRgb(30, 32, 38));

                Resources["PanelBrush"] =
                    new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));

                Resources["MutedBrush"] =
                    new SolidColorBrush(MediaColor.FromRgb(105, 110, 122));

                Resources["AccentSoft"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["CheckBoxUncheckedBackground"] =
                    new SolidColorBrush(MediaColor.FromRgb(250, 251, 253));

                Resources["CheckBoxUncheckedBorder"] =
                    new SolidColorBrush(MediaColor.FromRgb(195, 201, 212));

                Resources["SecondaryButtonBackground"] =
                    new SolidColorBrush(MediaColor.FromRgb(239, 241, 246));

                Resources["SecondaryButtonBorder"] =
                    new SolidColorBrush(MediaColor.FromRgb(213, 217, 226));

                Resources["SecondaryButtonForeground"] =
                    new SolidColorBrush(MediaColor.FromRgb(45, 47, 56));

                Resources["SecondaryButtonHover"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["SecondaryButtonPressed"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["ComboBackground"] =
                    new SolidColorBrush(MediaColor.FromRgb(247, 248, 252));

                Resources["ComboBorder"] =
                    new SolidColorBrush(MediaColor.FromRgb(195, 201, 214));

                Resources["ComboHighlight"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["ComboSelected"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["ComboHighlightForeground"] =
                    new SolidColorBrush(MediaColor.FromRgb(30, 32, 38));

                Resources["ComboSelectedForeground"] =
                    new SolidColorBrush(MediaColor.FromRgb(30, 32, 38));
            }
            else
            {
                Resources["WindowBackground"] =
                    new SolidColorBrush(MediaColor.FromRgb(16, 17, 22));

                Resources["WindowForeground"] =
                    MediaBrushes.White;

                Resources["PanelBrush"] =
                    new SolidColorBrush(MediaColor.FromRgb(29, 31, 39));

                Resources["MutedBrush"] =
                    new SolidColorBrush(MediaColor.FromRgb(155, 160, 175));

                Resources["AccentSoft"] =
                    new SolidColorBrush(accent.SoftDark);

                Resources["CheckBoxUncheckedBackground"] =
                    new SolidColorBrush(MediaColor.FromRgb(37, 39, 51));

                Resources["CheckBoxUncheckedBorder"] =
                    new SolidColorBrush(MediaColor.FromRgb(81, 75, 120));

                Resources["SecondaryButtonBackground"] =
                    new SolidColorBrush(MediaColor.FromRgb(48, 49, 61));

                Resources["SecondaryButtonBorder"] =
                    new SolidColorBrush(MediaColor.FromRgb(81, 75, 120));

                Resources["SecondaryButtonForeground"] =
                    new SolidColorBrush(MediaColor.FromRgb(242, 241, 248));

                Resources["SecondaryButtonHover"] =
                    new SolidColorBrush(accent.SoftDark);

                Resources["SecondaryButtonPressed"] =
                    new SolidColorBrush(accent.SoftDark);

                Resources["ComboBackground"] =
                    new SolidColorBrush(MediaColor.FromRgb(35, 37, 46));

                Resources["ComboBorder"] =
                    new SolidColorBrush(MediaColor.FromRgb(65, 68, 80));

                Resources["ComboHighlight"] =
                    new SolidColorBrush(accent.SoftDark);

                Resources["ComboSelected"] =
                    new SolidColorBrush(accent.Pressed);

                Resources["ComboHighlightForeground"] =
                    MediaBrushes.White;

                Resources["ComboSelectedForeground"] =
                    MediaBrushes.White;
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

            if (LanguageComboBox.SelectedItem is ComboBoxItem languageItem &&
                languageItem.Tag is string language)
            {
                Settings.Language = language;
            }

            WpfRadioButton? selectedAccent =
                AccentColorPanel.Children
                    .OfType<WpfRadioButton>()
                    .FirstOrDefault(button => button.IsChecked == true);

            if (selectedAccent?.Tag is string accentKey)
            {
                Settings.AccentColor = AccentPalette.Get(accentKey).Key;
            }

            Settings.StartWithWindows =
                StartWithWindowsCheckBox.IsChecked == true;

            Settings.StartMinimizedToTray =
                StartMinimizedCheckBox.IsChecked == true;

            Settings.MinimizeToTrayOnClose =
                MinimizeToTrayCheckBox.IsChecked == true;

            Settings.LowBatteryNotification =
                LowBatteryCheckBox.IsChecked == true;

            if (LowBatteryThresholdComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int threshold))
            {
                Settings.LowBatteryThreshold = threshold;
            }

            Settings.FullBatteryNotification =
                FullBatteryCheckBox.IsChecked == true;

            Settings.ShowTrayPercentage =
                TrayPercentageCheckBox.IsChecked == true;

            Settings.ShowTechnicalInfo =
                TechnicalInfoCheckBox.IsChecked == true;

            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}