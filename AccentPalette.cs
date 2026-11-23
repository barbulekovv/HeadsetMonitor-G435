
using System;
using System.Collections.Generic;
using System.Linq;
using MediaColor = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace HeadsetMonitor
{
    public sealed class AccentPreset
    {
        public string Key { get; }
        public string Name { get; }
        public MediaColor Accent { get; }
        public MediaColor Hover { get; }
        public MediaColor Pressed { get; }
        public MediaColor SoftLight { get; }
        public MediaColor SoftDark { get; }

        public AccentPreset(
            string key,
            string name,
            string accent,
            string hover,
            string pressed,
            string softLight,
            string softDark)
        {
            Key = key;
            Name = name;
            Accent = Parse(accent);
            Hover = Parse(hover);
            Pressed = Parse(pressed);
            SoftLight = Parse(softLight);
            SoftDark = Parse(softDark);
        }

        private static MediaColor Parse(string value)
        {
            return (MediaColor)ColorConverter.ConvertFromString(value)!;
        }
    }

    public static class AccentPalette
    {
        public static IReadOnlyList<AccentPreset> Presets { get; } =
            new List<AccentPreset>
            {
                new AccentPreset(
                    "Purple", "Фиолетовый",
                    "#7565D9", "#8677E8", "#5D4DBB",
                    "#EAE6FC", "#342D52"),

                new AccentPreset(
                    "Blue", "Синий",
                    "#3976CE", "#4C8AE0", "#285BA5",
                    "#E3EDFC", "#263C5B"),

                new AccentPreset(
                    "Cyan", "Голубой",
                    "#087F99", "#1598B3", "#06677D",
                    "#DDF3F7", "#1E414A"),

                new AccentPreset(
                    "Green", "Зелёный",
                    "#27834F", "#359B63", "#1D683E",
                    "#E1F3E8", "#244A35"),

                new AccentPreset(
                    "Red", "Красный",
                    "#C34D59", "#D6636E", "#A83B46",
                    "#FBE7E9", "#512D33"),

                new AccentPreset(
                    "Orange", "Оранжевый",
                    "#B96A22", "#D08032", "#965217",
                    "#FAEBDD", "#503923"),

                new AccentPreset(
                    "Pink", "Розовый",
                    "#C04F87", "#D2639B", "#A63D70",
                    "#F9E5EF", "#512D42")
            };

        public static AccentPreset Get(string? key)
        {
            return Presets.FirstOrDefault(
                preset => string.Equals(
                    preset.Key,
                    key,
                    StringComparison.OrdinalIgnoreCase))
                ?? Presets[0];
        }
    }
}
