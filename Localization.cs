
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HeadsetMonitor
{
    public static class Localization
    {
        public static string CurrentLanguage { get; private set; } = "ru";

        private static readonly Dictionary<string, string> Translations = new()
        {
            // Главное окно
            ["Мои устройства"] = "My Devices",
            ["Мониторинг аккумулятора и подключения"] = "Battery and connection monitoring",
            ["ПОДКЛЮЧЕННОЕ УСТРОЙСТВО"] = "CONNECTED DEVICE",
            ["УРОВЕНЬ ЗАРЯДА"] = "BATTERY LEVEL",
            ["Нажмите кнопку, чтобы считать заряд"] = "Press the button to check the battery",
            ["Проверить заряд"] = "Check battery",
            ["Во время измерения звук и микрофон могут прерваться примерно на 0,6 секунды."] = "Audio and microphone may be interrupted for approximately 0.6 seconds during measurement.",
            ["СОСТОЯНИЕ"] = "STATUS",
            ["Проверка USB-передатчика..."] = "Checking USB receiver...",
            ["Подключение наушников: ещё не проверено"] = "Headset connection: not checked yet",
            ["Состояние наушников проверяется при считывании заряда"] = "Headset status is checked when reading the battery level",
            ["USB ИНФОРМАЦИЯ"] = "USB INFORMATION",
            ["АВТОМАТИЧЕСКОЕ СЧИТЫВАНИЕ ЗАРЯДА"] = "AUTOMATIC BATTERY CHECK",
            ["Периодическая проверка"] = "Periodic check",
            ["Включить автоматическую проверку"] = "Enable automatic checking",
            ["Интервал считывания"] = "Checking interval",
            ["5 минут"] = "5 minutes",
            ["15 минут"] = "15 minutes",
            ["30 минут"] = "30 minutes",
            ["60 минут"] = "60 minutes",
            ["Последняя проверка: ещё не выполнялась"] = "Last check: not performed yet",
            ["Последняя проверка: "] = "Last check: ",
            ["Последняя проверка завершилась ошибкой: "] = "Last check failed: ",
            ["Автоматическое считывание может кратковременно прерывать звук."] = "Automatic checking may briefly interrupt audio.",
            ["Обратная связь"] = "Feedback",
            ["USB проверяется"] = "Checking USB",
            ["HEADSET MONITOR"] = "HEADSET MONITOR",
            ["Made with ♥"] = "Made with ♥",

            // Настройки
            ["НАСТРОЙКИ"] = "SETTINGS",
            ["Параметры приложения"] = "Application settings",
            ["Настрой работу Headset Monitor под себя"] = "Customize Headset Monitor",
            ["ВНЕШНИЙ ВИД"] = "APPEARANCE",
            ["Тема приложения"] = "Application theme",
            ["Тёмная"] = "Dark",
            ["Светлая"] = "Light",
            ["Как в Windows"] = "As in Windows",
            ["Акцентный цвет"] = "Accent color",
            ["Выбранный цвет применяется к кнопкам и другим акцентным элементам."] = "The selected color is applied to buttons and other accent elements.",

            // Цвета
            ["Фиолетовый"] = "Purple",
            ["Синий"] = "Blue",
            ["Голубой"] = "Cyan",
            ["Зелёный"] = "Green",
            ["Красный"] = "Red",
            ["Оранжевый"] = "Orange",
            ["Розовый"] = "Pink",

            // Запуск и поведение
            ["ЗАПУСК И ПОВЕДЕНИЕ"] = "STARTUP AND BEHAVIOR",
            ["Запускать вместе с Windows"] = "Start with Windows",
            ["Запускать сразу свёрнутым в трей"] = "Start minimized to tray",
            ["Запускать свёрнутым в трей"] = "Start minimized to tray",
            ["Сворачивать в трей при нажатии на крестик"] = "Minimize to tray when clicking close",
            ["Обычная кнопка сворачивания оставляет приложение на панели задач."] = "The regular minimize button keeps the app on the taskbar.",

            // Уведомления
            ["УВЕДОМЛЕНИЯ"] = "NOTIFICATIONS",
            ["Уведомлять о низком заряде"] = "Notify when battery is low",
            ["Порог низкого заряда"] = "Low battery threshold",
            ["Уведомлять о полном заряде"] = "Notify when battery is full",

            // Дополнительно
            ["ДОПОЛНИТЕЛЬНО"] = "ADDITIONAL",
            ["Показывать техническую USB-информацию"] = "Show technical USB information",
            ["Показывать процент заряда в трее"] = "Show battery percentage in tray",
            ["Язык приложения"] = "Application language",

            // Кнопки и меню
            ["Сохранить"] = "Save",
            ["Отмена"] = "Cancel",
            ["Выход"] = "Exit",
            ["Открыть"] = "Open",
            ["Скрыть"] = "Hide",
            ["Настройки"] = "Settings",
            ["⚙ Настройки"] = "⚙ Settings",

            // Состояния USB и наушников
            ["USB найден"] = "USB receiver found",
            ["USB не найден"] = "USB receiver not found",
            ["Передатчик обнаружен"] = "Receiver detected",
            ["Передатчик отключён"] = "Receiver disconnected",
            ["Наушники подключены"] = "Headset connected",
            ["Наушники выключены или не подключены"] = "Headset is off or disconnected",
            ["Наушники: нет USB-передатчика"] = "Headset: no USB receiver",
            ["Считываем..."] = "Reading...",
            ["Обращение к USB-передатчику..."] = "Communicating with USB receiver...",
            ["Наушники не подключены — заряд получить невозможно"] = "Headset is not connected — battery level is unavailable",
            ["приблизительный заряд по напряжению"] = "approximate battery level based on voltage",

            // Уведомления системного трея
            ["Заряд Logitech G435"] = "Logitech G435 battery",
            ["Текущий заряд: "] = "Current battery: ",
            ["Не удалось проверить заряд"] = "Could not check battery",
            ["Проверь подключение наушников и USB-передатчика."] = "Check the headset and USB receiver connection.",
            ["Низкий заряд наушников"] = "Headset battery low",
            ["Осталось примерно "] = "Approximately ",
            [" заряда Logitech G435."] = " battery remaining on Logitech G435.",
            ["Наушники полностью заряжены"] = "Headset fully charged",
            ["Расчётный заряд Logitech G435 достиг 100%."] = "Estimated Logitech G435 battery level has reached 100%.",

            // Ошибки
            ["Ошибка: "] = "Error: ",
            ["заряд ещё не проверен"] = "battery has not been checked yet",
            ["Не удалось изменить автозапуск Windows."] = "Could not change Windows startup settings.",
            ["Не удалось настроить автозапуск Windows."] = "Could not configure Windows startup.",
            ["Не удалось открыть страницу обратной связи."] = "Could not open the feedback page."
        };

        public static void SetLanguage(string language)
        {
            CurrentLanguage = language == "en" ? "en" : "ru";
        }

        public static string T(string text)
        {
            string russianText = GetRussianText(text);

            if (CurrentLanguage == "ru")
                return russianText;

            return Translations.TryGetValue(russianText, out string? translation)
                ? translation
                : text;
        }

        private static string GetRussianText(string text)
        {
            foreach (var item in Translations)
            {
                if (item.Value == text)
                    return item.Key;
            }

            return text;
        }

        public static void TranslateTree(DependencyObject root)
        {
            if (root is Window window &&
                !string.IsNullOrWhiteSpace(window.Title))
            {
                window.Title = T(window.Title);
            }

            if (root is TextBlock textBlock &&
                !string.IsNullOrWhiteSpace(textBlock.Text))
            {
                textBlock.Text = TranslateTextBlock(textBlock);
            }

            if (root is not ComboBoxItem &&
                root is ContentControl contentControl &&
                contentControl.Content is string content &&
                !string.IsNullOrWhiteSpace(content))
            {
                contentControl.Content = T(content);
            }

            if (root is HeaderedContentControl headeredControl &&
                headeredControl.Header is string header &&
                !string.IsNullOrWhiteSpace(header))
            {
                headeredControl.Header = T(header);
            }

            if (root is FrameworkElement element &&
                element.ToolTip is string toolTip &&
                !string.IsNullOrWhiteSpace(toolTip))
            {
                element.ToolTip = T(toolTip);
            }

            if (root is System.Windows.Controls.ComboBox comboBox)
            {
                TranslateComboBox(comboBox);
            }

            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                TranslateTree(VisualTreeHelper.GetChild(root, i));
            }
        }

        private static string TranslateTextBlock(TextBlock textBlock)
        {
            string text = textBlock.Text;

            // Переводим начало динамической строки с датой и временем.
            if (textBlock.Name == "LastUpdateText")
            {
                string[] prefixes =
                {
                    "Последняя проверка: ",
                    "Last check: ",
                    "Последняя проверка завершилась ошибкой: ",
                    "Last check failed: "
                };

                foreach (string prefix in prefixes)
                {
                    if (text.StartsWith(prefix))
                    {
                        string russianPrefix = GetRussianText(prefix);
                        return T(russianPrefix) + text.Substring(prefix.Length);
                    }
                }
            }

            // Отдельно переводим описание заряда, сохраняя значение напряжения.
            const string batterySeparator = " mV • ";

            int separatorIndex = text.IndexOf(
                batterySeparator,
                StringComparison.Ordinal);

            if (separatorIndex >= 0)
            {
                string voltage = text.Substring(
                    0,
                    separatorIndex + batterySeparator.Length);

                string description = text.Substring(
                    separatorIndex + batterySeparator.Length);

                return voltage + T(description);
            }

            return T(text);
        }

        private static void TranslateComboBox(System.Windows.Controls.ComboBox comboBox)
        {
            int selectedIndex = comboBox.SelectedIndex;

            // Названия языков не переводим, но обновляем отображение выбранного пункта.
            if (comboBox.Name == "LanguageComboBox")
            {
                RefreshSelectedItem(comboBox, selectedIndex);
                return;
            }

            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    comboBoxItem.Content is string itemText &&
                    !string.IsNullOrWhiteSpace(itemText))
                {
                    comboBoxItem.Content = T(itemText);
                }
            }

            RefreshSelectedItem(comboBox, selectedIndex);
        }

        private static void RefreshSelectedItem(
            System.Windows.Controls.ComboBox comboBox,
            int selectedIndex)
        {
            if (selectedIndex >= 0 &&
                selectedIndex < comboBox.Items.Count)
            {
                comboBox.SelectedIndex = -1;
                comboBox.SelectedIndex = selectedIndex;
            }

            comboBox.InvalidateVisual();
        }
    }
}
