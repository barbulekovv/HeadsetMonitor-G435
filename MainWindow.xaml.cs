
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

        // Последнее успешное измерение для подсказки в трее.
        private int? lastMeasurementPercent;
        private DateTime? lastMeasurementTime;

        private readonly DispatcherTimer updateTimer;
        private readonly DispatcherTimer deviceMonitorTimer;
        private readonly Forms.NotifyIcon trayIcon;

        private AppSettings settings = AppSettings.Load();

        private bool isReading;
        private bool isCheckingReceiver;
        private bool isReceiverPresent;
        private bool receiverPresenceInitialized;
        private int receiverMissCount;
        private bool isInitializing = true;
        private bool exitRequested;

        private const int VendorId = 0x046D;
        private const int ProductId = 0x0ACB;
        private const byte ReportId = 0x06;
        private const int OutputLength = 515;
        private const int FeatureLength = 64;

        public MainWindow()
        {
            InitializeComponent();

            RestoreWindowBounds();

            var trayMenu = new Forms.ContextMenuStrip();

            var openItem = new Forms.ToolStripMenuItem("Открыть");
            openItem.Click += (_, _) => RestoreWindow();

            var exitItem = new Forms.ToolStripMenuItem("Выход");
            exitItem.Click += (_, _) => ExitApplication();

            trayMenu.Items.Add(openItem);
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
            trayMenu.Items.Add(exitItem);

            trayIcon = new Forms.NotifyIcon
            {
                Icon = settings.ShowTrayPercentage
                    ? CreateBatteryIcon(null)
                    : CreateTrayIcon(),

                Text = "Headset Monitor: заряд ещё не проверен",
                ContextMenuStrip = trayMenu,
                Visible = true
            };

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
                        "Не удалось настроить автозапуск Windows.\n\n" +
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

                // При каждом запуске сначала выполняем одну проверку.
                await ReadBatteryAndUpdate(false);

                // Автообновление запускается только после первой проверки.
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
            var window = new SettingsWindow(settings)
            {
                Owner = this
            };

            if (window.ShowDialog() != true)
                return;

            settings = window.Settings;

            settings.Save();

            ApplySettings();
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
                    "Не удалось изменить автозапуск Windows.\n\n" +
                    ex.Message,
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
        // ТЕМА
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

                Resources["Accent"] =
                    new SolidColorBrush(Color.FromRgb(105, 88, 220));

                Resources["ComboBackground"] =
                    new SolidColorBrush(Color.FromRgb(247, 248, 252));

                Resources["ComboBorder"] =
                    new SolidColorBrush(Color.FromRgb(185, 190, 205));

                Resources["ComboHighlight"] =
                    new SolidColorBrush(Color.FromRgb(218, 224, 247));

                Resources["ComboSelected"] =
                    new SolidColorBrush(Color.FromRgb(194, 205, 240));

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

                Resources["WindowForeground"] = Brushes.White;

                Resources["Panel"] =
                    new SolidColorBrush(Color.FromRgb(27, 29, 37));

                Resources["Muted"] =
                    new SolidColorBrush(Color.FromRgb(146, 149, 165));

                Resources["Accent"] =
                    new SolidColorBrush(Color.FromRgb(139, 124, 255));

                Resources["ComboBackground"] =
                    new SolidColorBrush(Color.FromRgb(37, 39, 51));

                Resources["ComboBorder"] =
                    new SolidColorBrush(Color.FromRgb(81, 75, 120));

                Resources["ComboHighlight"] =
                    new SolidColorBrush(Color.FromRgb(81, 75, 120));

                Resources["ComboSelected"] =
                    new SolidColorBrush(Color.FromRgb(117, 101, 232));

                Resources["ComboHighlightForeground"] = Brushes.White;
                Resources["ComboSelectedForeground"] = Brushes.White;

                BatteryBarTrack.Background =
                    new SolidColorBrush(Color.FromRgb(48, 50, 61));
            }

            if (isReceiverPresent)
            {
                UsbStatusText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "WindowForeground");
            }

            UpdateBatteryBar();
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

        // Загружаем нашу фиолетовую иконку.
        private static Drawing.Icon CreateTrayIcon()
        {
            string iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "HeadsetMonitor.ico");

            if (File.Exists(iconPath))
                return new Drawing.Icon(iconPath);

            // Запасной вариант, если ICO не найден.
            return CreateBatteryIcon(null);
        }

        // Значок с процентом заряда.
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

            // Показываем последнее успешное измерение,
            // даже если последующая проверка завершилась ошибкой.
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
                    "Headset Monitor — заряд ещё не проверен";
            }
        }

        private void HideToTray()
        {
            ShowInTaskbar = false;
            Hide();
            WindowState = WindowState.Normal;
        }

        private void RestoreWindow()
        {
            ShowInTaskbar = true;
            Show();

            WindowState = WindowState.Normal;

            Activate();

            Topmost = true;
            Topmost = false;

            Focus();
        }

        private void ExitApplication()
        {
            exitRequested = true;
            trayIcon.Visible = false;
            Close();
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
                UsbBadgeText.Text = "USB найден";

                UsbBadgeText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0x4A, 0xDE, 0x80));

                UsbStatusText.Text = "Передатчик обнаружен";

                UsbStatusText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "WindowForeground");

                SetHeadsetConnection(null);
            }
            else
            {
                UsbBadgeText.Text = "USB не найден";

                UsbBadgeText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0xF8, 0x71, 0x71));

                UsbStatusText.Text = "Передатчик отключён";

                UsbStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0xF8, 0x71, 0x71));

                SetHeadsetConnection(null);
            }
        }

        private void SetHeadsetConnection(bool? connected)
        {
            if (!isReceiverPresent)
            {
                HeadsetConnectionText.Text =
                    "Наушники: нет USB-передатчика";

                HeadsetConnectionText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0xF8, 0x71, 0x71));
            }
            else if (connected == true)
            {
                HeadsetConnectionText.Text =
                    "Наушники подключены";

                HeadsetConnectionText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0x4A, 0xDE, 0x80));
            }
            else if (connected == false)
            {
                HeadsetConnectionText.Text =
                    "Наушники выключены или не подключены";

                HeadsetConnectionText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0xF8, 0x71, 0x71));
            }
            else
            {
                HeadsetConnectionText.Text =
                    "Подключение наушников: ещё не проверено";

                HeadsetConnectionText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0xE5, 0xC0, 0x70));
            }
        }

        // =========================================================
        // АВТООБНОВЛЕНИЕ
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

            var answer = System.Windows.MessageBox.Show(
                "Во время измерения звук и микрофон могут " +
                "прерваться примерно на 0,6 секунды.\n\n" +
                "Продолжить?",
                "Headset Monitor",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            await ReadBatteryAndUpdate(true);
        }

        private async Task ReadBatteryAndUpdate(bool manual)
        {
            if (isReading)
                return;

            isReading = true;

            RefreshButton.IsEnabled = false;
            RefreshButton.Content = "Считываем...";

            BatteryStatusText.Text =
                "Обращение к USB-передатчику...";

            try
            {
                var reading = await Task.Run(ReadBattery);

                ApplyReceiverPresence(true);

                SetHeadsetConnection(reading.HeadsetConnected);

                LastUpdateText.Text =
                    "Последняя проверка: " +
                    DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

                if (!reading.Percent.HasValue ||
                    !reading.Millivolts.HasValue)
                {
                    lastPercent = null;

                    BatteryPercentText.Text = "—";

                    BatteryStatusText.Text =
                        "Наушники не подключены — заряд получить невозможно";

                    UpdateBatteryBar();
                    UpdateTrayBatteryIcon(null);

                    return;
                }

                int newPercent = reading.Percent.Value;

                // Запоминаем только успешное измерение.
                lastMeasurementPercent = newPercent;
                lastMeasurementTime = DateTime.Now;

                if (settings.LowBatteryNotification &&
                    newPercent <= settings.LowBatteryThreshold &&
                    (!lastNotifiedPercent.HasValue ||
                     lastNotifiedPercent.Value > settings.LowBatteryThreshold))
                {
                    trayIcon.BalloonTipTitle = "Низкий заряд наушников";

                    trayIcon.BalloonTipText =
                        $"Осталось примерно {newPercent}% заряда Logitech G435.";

                    trayIcon.ShowBalloonTip(5000);
                }

                lastNotifiedPercent = newPercent;

                lastPercent = newPercent;

                BatteryPercentText.Text = newPercent.ToString();

                BatteryStatusText.Text =
                    $"{reading.Millivolts.Value} мВ • " +
                    "приблизительный заряд по напряжению";

                UpdateBatteryBar();
                UpdateTrayBatteryIcon(newPercent);
            }
            catch (Exception ex)
            {
                SetHeadsetConnection(null);

                BatteryStatusText.Text =
                    "Ошибка: " + ex.Message;

                BatteryPercentText.Text = "—";

                lastPercent = null;

                LastUpdateText.Text =
                    "Последняя проверка завершилась ошибкой: " +
                    DateTime.Now.ToString("HH:mm:ss");

                UpdateBatteryBar();
                UpdateTrayBatteryIcon(null);
            }
            finally
            {
                isReading = false;

                RefreshButton.IsEnabled = true;
                RefreshButton.Content = "Проверить заряд";
            }
        }

        private void UpdateBatteryBar()
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

            trayIcon.Visible = false;

            var icon = trayIcon.Icon;

            trayIcon.Dispose();

            icon?.Dispose();

            base.OnClosed(e);
        }
    }
}
