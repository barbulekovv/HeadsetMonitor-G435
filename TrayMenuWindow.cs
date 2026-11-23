
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace HeadsetMonitor
{
    public sealed class TrayMenuWindow : Window
    {
        private readonly Action checkBattery;
        private readonly Action openSettings;
        private readonly Action toggleApplication;
        private readonly Func<bool> isApplicationVisible;
        private readonly Action exitApplication;

        private bool isLightTheme;
        private AccentPreset currentAccent = AccentPalette.Get("Purple");

        public TrayMenuWindow(
            Action checkBattery,
            Action openSettings,
            Action toggleApplication,
            Func<bool> isApplicationVisible,
            Action exitApplication)
        {
            this.checkBattery = checkBattery;
            this.openSettings = openSettings;
            this.toggleApplication = toggleApplication;
            this.isApplicationVisible = isApplicationVisible;
            this.exitApplication = exitApplication;

            Width = 250;
            SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Content = BuildMenu();

            Deactivated += (_, _) => Hide();
        }

        public void ApplyAppearance(bool light, AccentPreset accent)
        {
            isLightTheme = light;
            currentAccent = accent;

            Content = BuildMenu();
            SizeToContent = SizeToContent.Height;
        }

        private UIElement BuildMenu()
        {
            var panelBackground = new SolidColorBrush(
                isLightTheme
                    ? System.Windows.Media.Color.FromRgb(255, 255, 255)
                    : System.Windows.Media.Color.FromRgb(27, 29, 37));

            var panelBorder = new SolidColorBrush(
                isLightTheme
                    ? System.Windows.Media.Color.FromRgb(217, 221, 230)
                    : System.Windows.Media.Color.FromRgb(72, 65, 95));

            System.Windows.Media.Brush mainForeground = isLightTheme
                ? new SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(37, 39, 51))
                : System.Windows.Media.Brushes.White;

            var mutedForeground = new SolidColorBrush(
                isLightTheme
                    ? System.Windows.Media.Color.FromRgb(115, 120, 135)
                    : System.Windows.Media.Color.FromRgb(133, 133, 148));

            var accentBrush = new SolidColorBrush(currentAccent.Accent);

            var separatorBrush = new SolidColorBrush(
                isLightTheme
                    ? System.Windows.Media.Color.FromRgb(230, 232, 238)
                    : System.Windows.Media.Color.FromRgb(56, 53, 67));

            var container = new Border
            {
                Background = panelBackground,
                BorderBrush = panelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 3,
                    Opacity = 0.22
                }
            };

            var stack = new StackPanel();

            var header = new TextBlock
            {
                Text = "HEADSET MONITOR",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Margin = new Thickness(10, 7, 10, 9)
            };

            stack.Children.Add(header);

            stack.Children.Add(CreateMenuButton(
                "↻",
                Localization.T("Проверить заряд"),
                checkBattery,
                mainForeground));

            stack.Children.Add(CreateMenuButton(
                "⚙",
                Localization.T("Настройки"),
                openSettings,
                mainForeground));

            string applicationButtonText = isApplicationVisible()
                ? Localization.T("Скрыть")
                : Localization.T("Открыть");

            stack.Children.Add(CreateMenuButton(
                "▣",
                applicationButtonText,
                toggleApplication,
                mainForeground));

            var separator = new Border
            {
                Height = 1,
                Background = separatorBrush,
                Margin = new Thickness(8, 7, 8, 7)
            };

            stack.Children.Add(separator);

            var exitForeground = new SolidColorBrush(
                isLightTheme
                    ? System.Windows.Media.Color.FromRgb(180, 35, 50)
                    : System.Windows.Media.Color.FromRgb(240, 128, 128));

            stack.Children.Add(CreateMenuButton(
                "✕",
                Localization.T("Выход"),
                exitApplication,
                exitForeground));

            var footer = new TextBlock
            {
                Text = "VERSION 1.0.2",
                FontSize = 9,
                Foreground = mutedForeground,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 2)
            };

            stack.Children.Add(footer);

            container.Child = stack;

            return container;
        }

        private System.Windows.Controls.Button CreateMenuButton(
            string icon,
            string text,
            Action action,
            System.Windows.Media.Brush foreground)
        {
            var button = new System.Windows.Controls.Button
            {
                Height = 40,
                Margin = new Thickness(2, 2, 2, 2),
                Padding = new Thickness(9, 0, 9, 0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = false,
                Template = CreateButtonTemplate()
            };

            var content = new Grid();

            content.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(30) });

            content.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = foreground,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };

            Grid.SetColumn(iconText, 0);
            Grid.SetColumn(label, 1);

            content.Children.Add(iconText);
            content.Children.Add(label);

            button.Content = content;

            button.Click += (_, _) =>
            {
                Hide();
                action();
            };

            return button;
        }

        private ControlTemplate CreateButtonTemplate()
        {
            var hoverColor = isLightTheme
                ? currentAccent.SoftLight
                : currentAccent.SoftDark;

            var pressedColor = currentAccent.Pressed;

            var template = new ControlTemplate(
                typeof(System.Windows.Controls.Button));

            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "ButtonBorder";

            border.SetValue(
                Border.CornerRadiusProperty,
                new CornerRadius(7));

            border.SetValue(
                Border.BackgroundProperty,
                System.Windows.Media.Brushes.Transparent);

            border.SetValue(
                Border.BorderBrushProperty,
                System.Windows.Media.Brushes.Transparent);

            border.SetValue(
                Border.BorderThicknessProperty,
                new Thickness(1));

            var presenter =
                new FrameworkElementFactory(typeof(ContentPresenter));

            presenter.SetValue(
                ContentPresenter.HorizontalAlignmentProperty,
                System.Windows.HorizontalAlignment.Stretch);

            presenter.SetValue(
                ContentPresenter.VerticalAlignmentProperty,
                System.Windows.VerticalAlignment.Center);

            presenter.SetValue(
                ContentPresenter.MarginProperty,
                new Thickness(0));

            border.AppendChild(presenter);
            template.VisualTree = border;

            var hoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };

            hoverTrigger.Setters.Add(
                new Setter(
                    Border.BackgroundProperty,
                    new SolidColorBrush(hoverColor),
                    "ButtonBorder"));

            hoverTrigger.Setters.Add(
                new Setter(
                    Border.BorderBrushProperty,
                    new SolidColorBrush(currentAccent.Accent),
                    "ButtonBorder"));

            template.Triggers.Add(hoverTrigger);

            var pressedTrigger = new Trigger
            {
                Property = System.Windows.Controls.Button.IsPressedProperty,
                Value = true
            };

            pressedTrigger.Setters.Add(
                new Setter(
                    Border.BackgroundProperty,
                    new SolidColorBrush(pressedColor),
                    "ButtonBorder"));

            pressedTrigger.Setters.Add(
                new Setter(
                    Border.BorderBrushProperty,
                    new SolidColorBrush(currentAccent.Accent),
                    "ButtonBorder"));

            template.Triggers.Add(pressedTrigger);

            return template;
        }

        public void ShowNearCursor()
        {
            System.Drawing.Point cursor = Forms.Cursor.Position;

            System.Drawing.Rectangle workingArea =
                Forms.Screen.FromPoint(cursor).WorkingArea;

            Opacity = 0;

            if (!IsVisible)
                Show();

            UpdateLayout();

            var source = PresentationSource.FromVisual(this);

            double scaleX =
                source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;

            double scaleY =
                source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            double cursorX = cursor.X * scaleX;
            double cursorY = cursor.Y * scaleY;

            double screenLeft = workingArea.Left * scaleX;
            double screenTop = workingArea.Top * scaleY;
            double screenRight = workingArea.Right * scaleX;
            double screenBottom = workingArea.Bottom * scaleY;

            double menuWidth = ActualWidth;
            double menuHeight = ActualHeight;

            double newLeft = cursorX - menuWidth + 12;
            double newTop = cursorY - menuHeight - 8;

            if (newTop < screenTop)
                newTop = cursorY + 12;

            if (newLeft < screenLeft + 5)
                newLeft = screenLeft + 5;

            if (newLeft + menuWidth > screenRight - 5)
                newLeft = screenRight - menuWidth - 5;

            if (newTop + menuHeight > screenBottom - 5)
                newTop = screenBottom - menuHeight - 5;

            Left = newLeft;
            Top = newTop;

            UpdateLayout();

            Opacity = 1;

            Activate();
        }
    }
}

