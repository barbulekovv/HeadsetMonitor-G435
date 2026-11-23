
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using HidSharp;

using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace HeadsetMonitor
{
    public partial class MainWindow : Window
    {
        private int? lastPercent;
        private int? lastNotifiedPercent;

        private bool fullBatteryNotified;

        private int? lastMeasurementPercent;
        private DateTime? lastMeasurementTime;

        private readonly DispatcherTimer updateTimer;
        private readonly DispatcherTimer deviceMonitorTimer;
        private readonly Forms.NotifyIcon trayIcon;
        private TrayMenuWindow? trayMenuWindow;

        private AppSettings settings = AppSettings.Load();

        private bool isReading;
        private bool isCheckingReceiver;
        private bool isReceiverPresent;
        private bool receiverPresenceInitialized;
        private int receiverMissCount;
        private bool isInitializing = true;
        private bool exitRequested;
        private bool isLightTheme;
        private WindowState windowStateBeforeHide = WindowState.Normal;

        private const int VendorId = 0x046D;
        private const int ProductId = 0x0ACB;
        private const byte ReportId = 0x06;
        private const int OutputLength = 515;
        private const int FeatureLength = 64;

        public MainWindow()
        {
            InitializeComponent();
Localization.SetLanguage(settings.Language);
Localization.TranslateTree(this);

            SettingsView.SaveRequested += SettingsView_SaveRequested;
            SettingsView.CancelRequested += SettingsView_CancelRequested;

            SourceInitialized += (_, _) => ApplyDarkTitleBar();

            RestoreWindowBounds();

            trayIcon = new Forms.NotifyIcon
            {
                Icon = settings.ShowTrayPercentage
                    ? CreateBatteryIcon(null)
                    : CreateTrayIcon(),

                Text = "Headset Monitor: " +
       Localization.T("заряд ещё не проверен"),
                Visible = true
            };

            trayIcon.MouseUp += TrayIcon_MouseUp;
            trayIcon.DoubleClick += (_, _) => RestoreWindow();

            BatteryBarTrack.SizeChanged += (_, _) =>
                UpdateBatteryBar();

            SizeChanged += Window_SizeChanged;

            updateTimer = new DispatcherTimer();
            updateTimer.Tick += UpdateTimer_Tick;

            deviceMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            deviceMonitorTimer.Tick += DeviceMonitorTimer_Tick;

            Loaded += async (_, _) =>
            {
                Window_SizeChanged(this, null);

                isInitializing = true;

                ApplySettingsToControls();
                ApplyTheme();

                try
                {
                    SetWindowsStartup(settings.StartWithWindows);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        Localization.T("Не удалось настроить автозапуск Windows.") +
"\n\n" +
                        ex.Message,
                        "Headset Monitor",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                isInitializing = false;

                UpdateReceiverPresence();
                deviceMonitorTimer.Start();

                if (settings.StartMinimizedToTray)
                    HideToTray();

                await ReadBatteryAndUpdate(false);

                if (settings.AutoUpdate)
                    StartAutoUpdate();
            };
        }

        // =========================================================
        // ВОССТАНОВЛЕНИЕ РАЗМЕРА И ПОЛОЖЕНИЯ ОКНА
        // =========================================================

        private void RestoreWindowBounds()
        {
            if (!settings.RememberWindowPosition)
                return;

            Width = settings.WindowWidth >= MinWidth
                ? settings.WindowWidth
                : 820;

            Height = settings.WindowHeight >= MinHeight
                ? settings.WindowHeight
                : 560;

            if (settings.WindowLeft == -1 ||
                settings.WindowTop == -1)
            {
                return;
            }

            double screenLeft = SystemParameters.VirtualScreenLeft;
            double screenTop = SystemParameters.VirtualScreenTop;
            double screenRight =
                screenLeft + SystemParameters.VirtualScreenWidth;
            double screenBottom =
                screenTop + SystemParameters.VirtualScreenHeight;

            double savedLeft = settings.WindowLeft;
            double savedTop = settings.WindowTop;

            bool outsideScreen =
                savedLeft + Width < screenLeft + 50 ||
                savedLeft > screenRight - 50 ||
                savedTop + Height < screenTop + 50 ||
                savedTop > screenBottom - 50;

            if (outsideScreen)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            Left = savedLeft;
            Top = savedTop;

            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        private void ApplySettingsToControls()
        {
            AutoUpdateCheckBox.IsChecked = settings.AutoUpdate;

            SelectInterval(settings.UpdateIntervalMinutes);

            UpdateIntervalComboBox.IsEnabled = settings.AutoUpdate;

            TechnicalInfoCard.Visibility =
                settings.ShowTechnicalInfo
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // =========================================================
        // НАСТРОЙКИ
        // =========================================================

        private void SettingsButton_Click(
            object sender, RoutedEventArgs e)
        {
            SettingsView.SetSettings(settings);

            DashboardView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
        }

        private void SettingsView_SaveRequested(object? sender, EventArgs e)
{
    settings = SettingsView.Settings;
    settings.Save();

    Localization.SetLanguage(settings.Language);
    Localization.TranslateTree(this);

    ApplySettings();

    SettingsView.Visibility = Visibility.Collapsed;
    DashboardView.Visibility = Visibility.Visible;
}

        private void SettingsView_CancelRequested(object? sender, EventArgs e)
        {
            SettingsView.Visibility = Visibility.Collapsed;
            DashboardView.Visibility = Visibility.Visible;
        }

       
        private void ApplySettings()
        {
            isInitializing = true;

            ApplySettingsToControls();

            isInitializing = false;

            if (settings.AutoUpdate)
                StartAutoUpdate();
            else
                updateTimer.Stop();

            ApplyTheme();

            try
            {
                SetWindowsStartup(settings.StartWithWindows);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    Localization.T("Не удалось изменить автозапуск Windows.") +
                    "\n\n" + ex.Message,
                    "Headset Monitor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            UpdateTrayBatteryIcon(lastPercent);
        }


        private void SelectInterval(int minutes)
        {
            for (int i = 0; i < UpdateIntervalComboBox.Items.Count; i++)
            {
                if (UpdateIntervalComboBox.Items[i] is ComboBoxItem item &&
                    int.TryParse(item.Tag?.ToString(), out int value) &&
                    value == minutes)
                {
                    UpdateIntervalComboBox.SelectedIndex = i;
                    return;
                }
            }

            UpdateIntervalComboBox.SelectedIndex = 1;
        }

        // =========================================================
        // ТЕМА И АКЦЕНТНЫЙ ЦВЕТ
        // =========================================================

        private void ApplyTheme()
        {
            bool light = settings.Theme == "Light";

            if (settings.Theme == "System")
            {
                try
                {
                    using RegistryKey? key =
                        Registry.CurrentUser.OpenSubKey(
                            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                    object? value = key?.GetValue("AppsUseLightTheme");

                    light = value != null &&
                            Convert.ToInt32(value) == 1;
                }
                catch
                {
                    light = false;
                }
            }

            isLightTheme = light;

            AccentPreset accent =
                AccentPalette.Get(settings.AccentColor);

            Resources["Accent"] =
                new SolidColorBrush(accent.Accent);

            Resources["AccentHover"] =
                new SolidColorBrush(accent.Hover);

            Resources["AccentPressed"] =
                new SolidColorBrush(accent.Pressed);

            if (light)
            {
                Resources["WindowBackground"] =
                    new SolidColorBrush(Color.FromRgb(245, 246, 250));

                Resources["WindowForeground"] =
                    new SolidColorBrush(Color.FromRgb(30, 31, 40));

                Resources["Panel"] =
                    new SolidColorBrush(Color.FromRgb(255, 255, 255));

                Resources["Muted"] =
                    new SolidColorBrush(Color.FromRgb(105, 108, 122));

                Resources["AccentSoft"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["CheckBoxUncheckedBackground"] =
                    new SolidColorBrush(Color.FromRgb(250, 251, 253));

                Resources["CheckBoxUncheckedBorder"] =
                    new SolidColorBrush(Color.FromRgb(195, 201, 212));

                // Кнопка настроек
                Resources["SettingsButtonBackground"] =
                    new SolidColorBrush(Color.FromRgb(238, 240, 245));

                Resources["SettingsButtonBorder"] =
                    new SolidColorBrush(Color.FromRgb(217, 221, 230));

                Resources["SettingsButtonForeground"] =
                    new SolidColorBrush(Color.FromRgb(48, 51, 64));

                Resources["SettingsButtonHover"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["SettingsButtonPressed"] =
                    new SolidColorBrush(accent.SoftLight);

                // USB-индикатор: светлая тема
                Resources["UsbBadgeBackground"] =
                    new SolidColorBrush(Color.FromRgb(226, 245, 233));

                Resources["UsbBadgeForeground"] =
                    new SolidColorBrush(Color.FromRgb(22, 107, 57));

                Resources["UsbBadgeErrorBackground"] =
                    new SolidColorBrush(Color.FromRgb(255, 230, 230));

                Resources["UsbBadgeErrorForeground"] =
                    new SolidColorBrush(Color.FromRgb(180, 25, 45));

                // Статусы подключения
                Resources["ConnectionConnectedForeground"] =
                    new SolidColorBrush(Color.FromRgb(35, 117, 69));

                Resources["ConnectionDisconnectedForeground"] =
                    new SolidColorBrush(Color.FromRgb(180, 35, 50));

                // Более контрастный статус ожидания
                Resources["ConnectionUnknownForeground"] =
                    new SolidColorBrush(Color.FromRgb(138, 63, 0));

                // Совместимые ресурсы статусов
                Resources["StatusSuccessForeground"] =
                    new SolidColorBrush(Color.FromRgb(35, 117, 69));

                Resources["StatusErrorForeground"] =
                    new SolidColorBrush(Color.FromRgb(180, 25, 45));

                Resources["StatusPendingForeground"] =
                    new SolidColorBrush(Color.FromRgb(138, 63, 0));

                Resources["ComboBackground"] =
                    new SolidColorBrush(Color.FromRgb(247, 248, 252));

                Resources["ComboBorder"] =
                    new SolidColorBrush(Color.FromRgb(195, 201, 214));

                Resources["ComboHighlight"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["ComboSelected"] =
                    new SolidColorBrush(accent.SoftLight);

                Resources["ComboHighlightForeground"] =
                    new SolidColorBrush(Color.FromRgb(30, 31, 40));

                Resources["ComboSelectedForeground"] =
                    new SolidColorBrush(Color.FromRgb(30, 31, 40));

                BatteryBarTrack.Background =
                    new SolidColorBrush(Color.FromRgb(220, 222, 230));
            }
            else
            {
                Resources["WindowBackground"] =
                    new SolidColorBrush(Color.FromRgb(16, 17, 22));

                Resources["WindowForeground"] =
                    Brushes.White;

                Resources["Panel"] =
                    new SolidColorBrush(Color.FromRgb(27, 29, 37));

                Resources["Muted"] =
                    new SolidColorBrush(Color.FromRgb(146, 149, 165));

                Resources["AccentSoft"] =
                    new SolidColorBrush(accent.SoftDark);

                Resources["CheckBoxUncheckedBackground"] =
                    new SolidColorBrush(Color.FromRgb(37, 39, 51));

                Resources["CheckBoxUncheckedBorder"] =
                    new SolidColorBrush(Color.FromRgb(81, 75, 120));

                // Кнопка настроек
                Resources["SettingsButtonBackground"] =
                    new SolidColorBrush(Color.FromRgb(37, 39, 51));

                Resources["SettingsButtonBorder"] =
                    new SolidColorBrush(Color.FromRgb(65, 68, 80));

                Resources["SettingsButtonForeground"] =
                    Brushes.White;

                Resources["SettingsButtonHover"] =
                    new SolidColorBrush(accent.SoftDark);

                Resources["SettingsButtonPressed"] =
                    new SolidColorBrush(accent.Pressed);

                // USB-индикатор: тёмная тема
                Resources["UsbBadgeBackground"] =
                    new SolidColorBrush(Color.FromRgb(25, 59, 43));

                Resources["UsbBadgeForeground"] =
                    new SolidColorBrush(Color.FromRgb(74, 222, 128));

                Resources["UsbBadgeErrorBackground"] =
                    new SolidColorBrush(Color.FromRgb(59, 32, 36));

                Resources["UsbBadgeErrorForeground"] =
                    new SolidColorBrush(Color.FromRgb(248, 113, 113));

                // Статусы подключения
                Resources["ConnectionConnectedForeground"] =
                    new SolidColorBrush(Color.FromRgb(74, 222, 128));

                Resources["ConnectionDisconnectedForeground"] =
                    new SolidColorBrush(Color.FromRgb(248, 113, 113));

                Resources["ConnectionUnknownForeground"] =
                    new SolidColorBrush(Color.FromRgb(229, 192, 112));

                // Совместимые ресурсы статусов
                Resources["StatusSuccessForeground"] =
                    new SolidColorBrush(Color.FromRgb(74, 222, 128));

                Resources["StatusErrorForeground"] =
                    new SolidColorBrush(Color.FromRgb(248, 113, 113));

                Resources["StatusPendingForeground"] =
                    new SolidColorBrush(Color.FromRgb(229, 192, 112));

                Resources["ComboBackground"] =
                    new SolidColorBrush(Color.FromRgb(37, 39, 51));

                Resources["ComboBorder"] =
                    new SolidColorBrush(Color.FromRgb(65, 68, 80));

                Resources["ComboHighlight"] =
                    new SolidColorBrush(accent.SoftDark);

                Resources["ComboSelected"] =
                    new SolidColorBrush(accent.Pressed);

                Resources["ComboHighlightForeground"] =
                    Brushes.White;

                Resources["ComboSelectedForeground"] =
                    Brushes.White;

                BatteryBarTrack.Background =
                    new SolidColorBrush(Color.FromRgb(48, 50, 61));
            }

            // Применяем актуальные цвета USB-плашки сразу,
            // в том числе при переключении темы.
            if (receiverPresenceInitialized)
            {
                ApplyReceiverBadgeAppearance(isReceiverPresent);
            }

            UpdateBatteryBar();

            SettingsView.ApplyTheme();

            trayMenuWindow?.ApplyAppearance(
                isLightTheme,
                accent);

            ApplyDarkTitleBar();
        }

        // Обновляет фон плашки, текст и цвет индикатора USB.
        private void ApplyReceiverBadgeAppearance(bool present)
        {
            if (present)
            {
                UsbBadgeBorder.SetResourceReference(
                    Border.BackgroundProperty,
                    "UsbBadgeBackground");

                UsbBadgeText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "UsbBadgeForeground");

                UsbBadgeIndicator.SetResourceReference(
                    System.Windows.Shapes.Shape.FillProperty,
                    "StatusSuccessForeground");
            }
            else
            {
                UsbBadgeBorder.SetResourceReference(
                    Border.BackgroundProperty,
                    "UsbBadgeErrorBackground");

                UsbBadgeText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "UsbBadgeErrorForeground");

                UsbBadgeIndicator.SetResourceReference(
                    System.Windows.Shapes.Shape.FillProperty,
                    "StatusErrorForeground");
            }
        }

        // =========================================================
        // АВТОЗАПУСК WINDOWS
        // =========================================================

        private void SetWindowsStartup(bool enabled)
        {
            const string registryPath =
                @"Software\Microsoft\Windows\CurrentVersion\Run";

            const string appName = "HeadsetMonitor";

            using RegistryKey key =
                Registry.CurrentUser.CreateSubKey(registryPath)
                ?? throw new Exception(
                    "Не удалось открыть раздел автозапуска реестра.");

            if (!enabled)
            {
                key.DeleteValue(appName, false);
                return;
            }

            string processPath =
                Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new Exception(
                    "Не удалось определить путь приложения.");

            string command;

            if (Path.GetFileNameWithoutExtension(processPath)
                .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                string assemblyPath =
                    Assembly.GetEntryAssembly()?.Location
                    ?? throw new Exception(
                        "Не удалось определить DLL приложения.");

                command = $"\"{processPath}\" \"{assemblyPath}\"";
            }
            else
            {
                command = $"\"{processPath}\"";
            }

            key.SetValue(appName, command);
        }

        // =========================================================
        // СИСТЕМНЫЙ ТРЕЙ
        // =========================================================

        private void TrayIcon_MouseUp(
            object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button != Forms.MouseButtons.Right)
                return;

            if (trayMenuWindow == null)
            {
                trayMenuWindow = new TrayMenuWindow(
    CheckBatteryFromTray,
    OpenSettingsFromTray,
    ToggleWindowFromTray,
    IsApplicationVisible,
    ExitApplication);

                trayMenuWindow.Closed += (_, _) =>
                    trayMenuWindow = null;
            }

            trayMenuWindow.ApplyAppearance(
                isLightTheme,
                AccentPalette.Get(settings.AccentColor));

            trayMenuWindow.ShowNearCursor();
        }

        private void CheckBatteryFromTray()
        {
            _ = CheckBatteryFromTrayAsync();
        }

        
private async Task CheckBatteryFromTrayAsync()
{
    if (isReading)
        return;

    DateTime? previousMeasurement = lastMeasurementTime;

    await ReadBatteryAndUpdate(false);

    if (lastMeasurementTime != previousMeasurement &&
        lastMeasurementPercent.HasValue)
    {
        trayIcon.BalloonTipTitle =
            Localization.T("Заряд Logitech G435");

        trayIcon.BalloonTipText =
            Localization.T("Текущий заряд: ") +
            $"{lastMeasurementPercent.Value}%";

        trayIcon.ShowBalloonTip(4000);
    }
    else
    {
        trayIcon.BalloonTipTitle =
            Localization.T("Не удалось проверить заряд");

        trayIcon.BalloonTipText =
            Localization.T("Проверь подключение наушников и USB-передатчика.");

        trayIcon.ShowBalloonTip(4000);
    }
}

                  

        private void OpenSettingsFromTray()
{
    // Восстанавливаем окно только если оно скрыто или свёрнуто.
    // Если оно уже открыто, сохраняем его текущее состояние.
    if (!IsVisible || WindowState == WindowState.Minimized)
    {
        RestoreWindow();
    }

    SettingsButton_Click(this, new RoutedEventArgs());
}

        private static Drawing.Icon CreateTrayIcon()
        {
            string iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "HeadsetMonitor.ico");

            if (File.Exists(iconPath))
                return new Drawing.Icon(iconPath);

            return CreateBatteryIcon(null);
        }

        private static Drawing.Icon CreateBatteryIcon(int? percent)
        {
            using var bitmap = new Drawing.Bitmap(32, 32);

            using (var graphics = Drawing.Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode =
                    Drawing.Drawing2D.SmoothingMode.AntiAlias;

                graphics.TextRenderingHint =
                    Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                graphics.Clear(Drawing.Color.Transparent);

                string text = percent.HasValue
                    ? percent.Value.ToString() + "%"
                    : "—";

                float fontSize = text.Length >= 4 ? 12.5f : 14f;

                using var font = new Drawing.Font(
                    "Segoe UI",
                    fontSize,
                    Drawing.FontStyle.Bold,
                    Drawing.GraphicsUnit.Pixel);

                using var textBrush =
                    new Drawing.SolidBrush(Drawing.Color.White);

                using var outlineBrush =
                    new Drawing.SolidBrush(
                        Drawing.Color.FromArgb(180, 0, 0, 0));

                var format = new Drawing.StringFormat
                {
                    Alignment = Drawing.StringAlignment.Center,
                    LineAlignment = Drawing.StringAlignment.Center
                };

                var rect = new Drawing.RectangleF(0, 0, 32, 32);

                graphics.DrawString(
                    text, font, outlineBrush,
                    new Drawing.RectangleF(0.8f, 0, 32, 32), format);

                graphics.DrawString(
                    text, font, outlineBrush,
                    new Drawing.RectangleF(-0.8f, 0, 32, 32), format);

                graphics.DrawString(
                    text, font, outlineBrush,
                    new Drawing.RectangleF(0, 0.8f, 32, 32), format);

                graphics.DrawString(
                    text, font, outlineBrush,
                    new Drawing.RectangleF(0, -0.8f, 32, 32), format);

                graphics.DrawString(
                    text, font, textBrush, rect, format);
            }

            IntPtr handle = bitmap.GetHicon();

            try
            {
                using Drawing.Icon temporaryIcon =
                    Drawing.Icon.FromHandle(handle);

                return (Drawing.Icon)temporaryIcon.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        private void ApplyDarkTitleBar()
        {
            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

            int enabled = isLightTheme ? 0 : 1;

            DwmSetWindowAttribute(
                new System.Windows.Interop.WindowInteropHelper(this).Handle,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref enabled,
                sizeof(int));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            ref int value,
            int valueSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        
private void UpdateTrayBatteryIcon(int? percent)
{
    Drawing.Icon newIcon;

    if (settings.ShowTrayPercentage)
    {
        newIcon = CreateBatteryIcon(percent);
    }
    else
    {
        newIcon = CreateTrayIcon();
    }

    var oldIcon = trayIcon.Icon;

    trayIcon.Icon = newIcon;

    oldIcon?.Dispose();

    if (lastMeasurementPercent.HasValue &&
        lastMeasurementTime.HasValue)
    {
        trayIcon.Text =
            $"Headset Monitor | {lastMeasurementPercent.Value}% | " +
            lastMeasurementTime.Value.ToString("dd.MM HH:mm");
    }
    else
    {
        trayIcon.Text =
            "Headset Monitor — " +
            Localization.T("заряд ещё не проверен");
    }
}

                 

        private void HideToTray()
{
    if (WindowState != WindowState.Minimized)
        windowStateBeforeHide = WindowState;

    ShowInTaskbar = false;
    Hide();
}

private void RestoreWindow()
{
    ShowInTaskbar = true;

    if (!IsVisible)
        Show();

    WindowState = windowStateBeforeHide;

    Activate();

    Topmost = true;
    Topmost = false;

    Focus();
}

private bool IsApplicationVisible()
{
    return ShowInTaskbar;
}
private void ToggleWindowFromTray()
{
    if (IsApplicationVisible())
        HideToTray();
    else
        RestoreWindow();
}

        private void ExitApplication()
        {
            if (exitRequested)
                return;

            exitRequested = true;

            updateTimer.Stop();
            deviceMonitorTimer.Stop();

            trayIcon.Visible = false;

            Close();

            System.Windows.Application.Current.Shutdown();
        }

        private void SaveWindowSettings()
        {
            if (!settings.RememberWindowPosition)
                return;

            if (WindowState != WindowState.Normal)
                return;

            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;

            settings.Save();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveWindowSettings();

            if (!exitRequested && settings.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            if (!exitRequested)
            {
                exitRequested = true;
                trayIcon.Visible = false;
            }

            base.OnClosing(e);
        }

        // =========================================================
        // АДАПТИВНОСТЬ ОКНА
        // =========================================================

        private void Window_SizeChanged(
            object sender, SizeChangedEventArgs? e)
        {
            double width = ActualWidth;

            RootGrid.Margin = width < 420
                ? new Thickness(10)
                : width < 650
                    ? new Thickness(16)
                    : new Thickness(22);

            if (width < 650)
            {
                MainColumn.Width =
                    new GridLength(1, GridUnitType.Star);

                SideColumn.Width =
                    new GridLength(0);

                Grid.SetColumn(HeadsetCard, 0);
                Grid.SetRow(HeadsetCard, 0);

                Grid.SetColumn(SidePanel, 0);
                Grid.SetRow(SidePanel, 1);

                HeadsetCard.Margin = new Thickness(0);
                SidePanel.Margin = new Thickness(0, 12, 0, 0);
            }
            else
            {
                MainColumn.Width =
                    new GridLength(2, GridUnitType.Star);

                SideColumn.Width =
                    new GridLength(1, GridUnitType.Star);

                Grid.SetColumn(HeadsetCard, 0);
                Grid.SetRow(HeadsetCard, 0);

                Grid.SetColumn(SidePanel, 1);
                Grid.SetRow(SidePanel, 0);

                HeadsetCard.Margin = new Thickness(0, 0, 10, 0);
                SidePanel.Margin = new Thickness(0);
            }
        }

        // =========================================================
        // МОНИТОРИНГ USB
        // =========================================================

        private async void DeviceMonitorTimer_Tick(
            object? sender, EventArgs e)
        {
            if (isCheckingReceiver)
                return;

            isCheckingReceiver = true;

            try
            {
                bool present = await Task.Run(IsReceiverPresent);

                if (present)
                {
                    receiverMissCount = 0;
                    ApplyReceiverPresence(true);
                }
                else
                {
                    receiverMissCount++;

                    if (receiverMissCount >= 2)
                        ApplyReceiverPresence(false);
                }
            }
            finally
            {
                isCheckingReceiver = false;
            }
        }

        private void UpdateReceiverPresence()
        {
            bool present = IsReceiverPresent();

            receiverMissCount = 0;

            ApplyReceiverPresence(present);
        }

        private static bool IsReceiverPresent()
        {
            try
            {
                return DeviceList.Local
                    .GetHidDevices(VendorId, ProductId)
                    .Any(d =>
                        d.GetMaxOutputReportLength() == OutputLength &&
                        d.GetMaxFeatureReportLength() == FeatureLength);
            }
            catch
            {
                return false;
            }
        }

        private void ApplyReceiverPresence(bool present)
        {
            if (receiverPresenceInitialized &&
                isReceiverPresent == present)
                return;

            receiverPresenceInitialized = true;
            isReceiverPresent = present;

            if (present)
            {
                UsbBadgeText.Text = Localization.T("USB найден");

                UsbStatusText.Text = Localization.T("Передатчик обнаружен");

                UsbStatusText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "WindowForeground");

                SetHeadsetConnection(null);
            }
            else
            {
                UsbBadgeText.Text = Localization.T("USB не найден");

                UsbStatusText.Text = Localization.T("Передатчик отключён");

                UsbStatusText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "StatusErrorForeground");

                SetHeadsetConnection(null);
            }

            ApplyReceiverBadgeAppearance(present);
        }

        private void SetHeadsetConnection(bool? connected)
        {
            if (!isReceiverPresent)
            {
                HeadsetConnectionText.Text =
                    Localization.T("Наушники: нет USB-передатчика");

                HeadsetConnectionText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "ConnectionDisconnectedForeground");
            }
            else if (connected == true)
            {
                HeadsetConnectionText.Text =
                    Localization.T("Наушники подключены");

                HeadsetConnectionText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "ConnectionConnectedForeground");
            }
            else if (connected == false)
            {
                HeadsetConnectionText.Text =
                    Localization.T("Наушники выключены или не подключены");

                HeadsetConnectionText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "ConnectionDisconnectedForeground");
            }
            else
            {
                HeadsetConnectionText.Text =
                    Localization.T("Подключение наушников: ещё не проверено");

                HeadsetConnectionText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "ConnectionUnknownForeground");
            }
        }

        // =========================================================
        // АВТОМАТИЧЕСКОЕ СЧИТЫВАНИЕ ЗАРЯДА
        // =========================================================

        private void AutoUpdateCheckBox_Changed(
            object sender, RoutedEventArgs e)
        {
            if (isInitializing)
                return;

            bool enabled =
                AutoUpdateCheckBox.IsChecked == true;

            UpdateIntervalComboBox.IsEnabled = enabled;

            settings.AutoUpdate = enabled;
            settings.Save();

            if (enabled)
                StartAutoUpdate();
            else
                updateTimer.Stop();
        }

        private void UpdateIntervalComboBox_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing)
                return;

            if (UpdateIntervalComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int minutes))
            {
                settings.UpdateIntervalMinutes = minutes;
                settings.Save();
            }

            if (AutoUpdateCheckBox.IsChecked == true)
                StartAutoUpdate();
        }

        private void StartAutoUpdate()
        {
            updateTimer.Stop();

            if (UpdateIntervalComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int minutes))
            {
                updateTimer.Interval =
                    TimeSpan.FromMinutes(minutes);

                updateTimer.Start();
            }
        }

        private async void UpdateTimer_Tick(
            object? sender, EventArgs e)
        {
            updateTimer.Stop();

            if (!isReading)
                await ReadBatteryAndUpdate(false);

            if (AutoUpdateCheckBox.IsChecked == true)
                StartAutoUpdate();
        }

        // =========================================================
        // ЧТЕНИЕ АККУМУЛЯТОРА
        // =========================================================

        private async void RefreshButton_Click(
    object sender, RoutedEventArgs e)
{
    if (isReading)
        return;

    await ReadBatteryAndUpdate(true);
}

        
private async Task ReadBatteryAndUpdate(bool manual)
{
    if (isReading)
        return;

    isReading = true;

    RefreshButton.IsEnabled = false;
    RefreshButton.Content = Localization.T("Считываем...");

    BatteryStatusText.Text =
        Localization.T("Обращение к USB-передатчику...");

    try
    {
        var reading = await Task.Run(ReadBattery);

        ApplyReceiverPresence(true);
        SetHeadsetConnection(reading.HeadsetConnected);

        LastUpdateText.Text =
            Localization.T("Последняя проверка: ") +
            DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

        if (!reading.Percent.HasValue ||
            !reading.Millivolts.HasValue)
        {
            lastPercent = null;
            BatteryPercentText.Text = "—";

            BatteryStatusText.Text =
                Localization.T("Наушники не подключены — заряд получить невозможно");

            UpdateBatteryBar();
            UpdateTrayBatteryIcon(null);

            return;
        }

        int newPercent = reading.Percent.Value;

        lastMeasurementPercent = newPercent;
        lastMeasurementTime = DateTime.Now;

        if (settings.LowBatteryNotification &&
            newPercent <= settings.LowBatteryThreshold &&
            (!lastNotifiedPercent.HasValue ||
             lastNotifiedPercent.Value > settings.LowBatteryThreshold))
        {
            trayIcon.BalloonTipTitle =
                Localization.T("Низкий заряд наушников");

            trayIcon.BalloonTipText =
                Localization.T("Осталось примерно ") +
                $"{newPercent}%" +
                Localization.T(" заряда Logitech G435.");

            trayIcon.ShowBalloonTip(5000);
        }

        if (newPercent < 100)
        {
            fullBatteryNotified = false;
        }
        else if (newPercent >= 100)
        {
            if (lastPercent.HasValue &&
                lastPercent.Value < 100 &&
                !fullBatteryNotified &&
                settings.FullBatteryNotification)
            {
                trayIcon.BalloonTipTitle =
                    Localization.T("Наушники полностью заряжены");

                trayIcon.BalloonTipText =
                    Localization.T("Расчётный заряд Logitech G435 достиг 100%.");

                trayIcon.ShowBalloonTip(5000);
            }

            fullBatteryNotified = true;
        }

        lastNotifiedPercent = newPercent;
        lastPercent = newPercent;

        BatteryPercentText.Text = newPercent.ToString();

        BatteryStatusText.Text =
            $"{reading.Millivolts.Value} mV • " +
            Localization.T("приблизительный заряд по напряжению");

System.Diagnostics.Debug.WriteLine(
    $"LANGUAGE: {settings.Language} | LOCALIZATION: {Localization.CurrentLanguage}");

        UpdateBatteryBar();
        UpdateTrayBatteryIcon(newPercent);
    }
    catch (Exception ex)
    {
        SetHeadsetConnection(null);

        BatteryStatusText.Text =
            Localization.T("Ошибка: ") + ex.Message;

        BatteryPercentText.Text = "—";
        lastPercent = null;

        LastUpdateText.Text =
            Localization.T("Последняя проверка завершилась ошибкой: ") +
            DateTime.Now.ToString("HH:mm:ss");

        UpdateBatteryBar();
        UpdateTrayBatteryIcon(null);
    }
    finally
    {
        isReading = false;

        RefreshButton.IsEnabled = true;
        RefreshButton.Content =
            Localization.T("Проверить заряд");
    }
}                private void UpdateBatteryBar()
        {
            if (!lastPercent.HasValue)
            {
                BatteryBar.Width = 0;
                BatteryPercentText.Foreground = Brushes.White;
                return;
            }

            int percent = lastPercent.Value;

            BatteryBar.Width =
                BatteryBarTrack.ActualWidth * percent / 100.0;

            Brush color = percent <= 15
                ? Brushes.IndianRed
                : percent <= 35
                    ? Brushes.Orange
                    : Brushes.MediumSeaGreen;

            BatteryBar.Background = color;
            BatteryPercentText.Foreground = color;
        }

        // =========================================================
        // USB HID ПРОТОКОЛ LOGITECH G435
        // =========================================================

        private static (
            int? Percent,
            int? Millivolts,
            bool? HeadsetConnected) ReadBattery()
        {
            var device = DeviceList.Local
                .GetHidDevices(VendorId, ProductId)
                .FirstOrDefault(d =>
                    d.GetMaxOutputReportLength() == OutputLength &&
                    d.GetMaxFeatureReportLength() == FeatureLength);

            if (device == null)
                throw new Exception(
                    "Нужный HID-интерфейс Logitech не найден.");

            using HidStream stream = device.Open();

            stream.ReadTimeout = 100;
            stream.WriteTimeout = 1500;

            bool bootloaderAttempted = false;

            try
            {
                bootloaderAttempted = true;

                byte enterAck = SendBridge(
                    stream, 0x01, 0x03, Array.Empty<byte>());

                if (enterAck != 0x5A && enterAck != 0xFE)
                    throw new Exception(
                        $"Неожиданный ответ при входе: {enterAck:X2}");

                DrainFeatureReports(stream);

                byte sequence = 1;

                int? connected = Query(
                    stream,
                    ref sequence,
                    0x0E,
                    1,
                    new byte[] { 0xE3 },
                    data => ParseRemoteValue(data, 0xE3));

                if (connected == 0)
                    return (null, null, false);

                bool? headsetConnected = connected.HasValue
                    ? connected.Value != 0
                    : null;

                int? millivolts = Query(
                    stream,
                    ref sequence,
                    0x06,
                    1,
                    new byte[] { 0x00, 0x31 },
                    ParseBattery);

                if (IsValidBattery(millivolts))
                {
                    var reading = MakeReading(millivolts!.Value);

                    return (
                        reading.Percent,
                        reading.Millivolts,
                        headsetConnected);
                }

                Query(
                    stream,
                    ref sequence,
                    0x0E,
                    2,
                    new byte[] { 0xE1, 0x01 },
                    data => 1);

                millivolts = Query(
                    stream,
                    ref sequence,
                    0x06,
                    1,
                    new byte[] { 0x00, 0x31 },
                    ParseBattery);

                if (!IsValidBattery(millivolts))
                    throw new Exception(
                        "Не удалось получить корректное напряжение.");

                var fallbackReading =
                    MakeReading(millivolts!.Value);

                return (
                    fallbackReading.Percent,
                    fallbackReading.Millivolts,
                    headsetConnected);
            }
            finally
            {
                if (bootloaderAttempted)
                {
                    try
                    {
                        SendBridge(
                            stream,
                            0x08,
                            0x03,
                            Array.Empty<byte>());
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static (int Percent, int Millivolts) MakeReading(int mv)
        {
            return (VoltageToPercent(mv), mv);
        }

        private static bool IsValidBattery(int? mv)
        {
            return mv.HasValue &&
                   mv.Value >= 2500 &&
                   mv.Value <= 4600;
        }

        private static byte SendBridge(
            HidStream stream,
            byte operation1,
            byte operation2,
            byte[] body)
        {
            byte[] report = new byte[OutputLength];

            report[0] = ReportId;
            report[1] = operation1;
            report[2] = operation2;

            Array.Copy(body, 0, report, 3, body.Length);

            stream.Write(report);

            DateTime deadline =
                DateTime.UtcNow.AddMilliseconds(800);

            byte[] input =
                new byte[stream.Device.GetMaxInputReportLength()];

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    int count = stream.Read(input);

                    if (count >= 2 && input[0] == ReportId)
                        return input[1];
                }
                catch (TimeoutException)
                {
                }
            }

            throw new TimeoutException(
                "Передатчик не подтвердил USB-команду.");
        }

        private static void DrainFeatureReports(HidStream stream)
        {
            for (int i = 0; i < 16; i++)
            {
                byte[] report = new byte[FeatureLength];

                report[0] = ReportId;

                stream.GetFeature(report);

                if (report[1] == 0xFF)
                    break;
            }
        }

        private static int? Query(
            HidStream stream,
            ref byte sequence,
            byte category,
            byte length,
            byte[] payload,
            Func<byte[], int?> parser)
        {
            byte currentSequence = sequence;

            sequence = (byte)(sequence % 127 + 1);

            byte[] command = new byte[5 + payload.Length];

            command[0] = 0x50;
            command[1] = 0x41;
            command[2] = category;
            command[3] = currentSequence;
            command[4] = length;

            Array.Copy(
                payload,
                0,
                command,
                5,
                payload.Length);

            byte[] body = new byte[2 + command.Length];

            body[0] = (byte)(command.Length & 0xFF);
            body[1] = (byte)(command.Length >> 8);

            Array.Copy(
                command,
                0,
                body,
                2,
                command.Length);

            byte ack = SendBridge(
                stream, 0x04, 0x03, body);

            if (ack != 0x5A)
                throw new Exception(
                    $"Ошибка подтверждения команды: {ack:X2}");

            DateTime deadline =
                DateTime.UtcNow.AddMilliseconds(1500);

            while (DateTime.UtcNow < deadline)
            {
                byte[] response = new byte[FeatureLength];

                response[0] = ReportId;

                stream.GetFeature(response);

                if (response[1] != 0xFF)
                {
                    int? value = parser(response);

                    if (value.HasValue)
                        return value;
                }

                Thread.Sleep(30);
            }

            return null;
        }

        private static int? ParseRemoteValue(
            byte[] data, byte command)
        {
            for (int i = 0; i <= data.Length - 4; i++)
            {
                if (data[i] == 0x02 &&
                    data[i + 1] == 0x00 &&
                    data[i + 2] == command)
                {
                    return data[i + 3];
                }
            }

            return null;
        }

        private static int? ParseBattery(byte[] data)
        {
            for (int i = 0; i <= data.Length - 7; i++)
            {
                if (data[i] == 0x07 &&
                    data[i + 1] == 0x00 &&
                    data[i + 2] == 0x06 &&
                    (data[i + 3] & 0x80) != 0 &&
                    data[i + 4] == 0x00)
                {
                    return data[i + 5] |
                           (data[i + 6] << 8);
                }
            }

            return null;
        }

        private static int VoltageToPercent(int mv)
        {
            (int mv, int percent)[] curve =
            {
                (4200, 100),
                (4150, 95),
                (4100, 90),
                (4050, 85),
                (4000, 80),
                (3950, 75),
                (3900, 70),
                (3850, 62),
                (3800, 55),
                (3750, 45),
                (3700, 35),
                (3650, 25),
                (3600, 15),
                (3550, 10),
                (3500, 5),
                (3400, 0)
            };

            if (mv >= curve[0].mv)
                return 100;

            for (int i = 0; i < curve.Length - 1; i++)
            {
                var high = curve[i];
                var low = curve[i + 1];

                if (mv >= low.mv)
                {
                    double ratio =
                        (double)(mv - low.mv) /
                        (high.mv - low.mv);

                    return (int)Math.Round(
                        low.percent +
                        ratio * (high.percent - low.percent));
                }
            }

            return 0;
        }

        // =========================================================
        // ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ
        // =========================================================

        private void FeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/barbulekovv/HeadsetMonitor-G435/issues/new",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Не удалось открыть страницу обратной связи.\n\n" + ex.Message,
                    "Headset Monitor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveWindowSettings();

            updateTimer.Stop();
            deviceMonitorTimer.Stop();

            trayMenuWindow?.Close();
            trayMenuWindow = null;

            trayIcon.Visible = false;

            var icon = trayIcon.Icon;

            trayIcon.Dispose();

            icon?.Dispose();

            base.OnClosed(e);
        }
    }
}
