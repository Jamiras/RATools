using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Media;

namespace RATools.Services
{
    public class Theme
    {
        public enum Color
        {
            None,

            EditorBackground,
            EditorForeground,
            EditorSelection,
            EditorLineNumbers,

            EditorKeyword,
            EditorComment,
            EditorIntegerConstant,
            EditorStringConstant,
            EditorVariable,
            EditorFunctionDefinition,
            EditorFunctionCall,
        }

        static Theme()
        {
            InitDefault();
        }

        public static void InitDefault()
        {
            SetColor(Color.EditorBackground, Colors.White);
            SetColor(Color.EditorForeground, Colors.Black);
            SetColor(Color.EditorSelection, Colors.LightGray);
            SetColor(Color.EditorLineNumbers, Colors.LightGray);

            SetColor(Color.EditorKeyword, Colors.DarkGoldenrod);
            SetColor(Color.EditorComment, Colors.DarkCyan);
            SetColor(Color.EditorIntegerConstant, Colors.DarkGray);
            SetColor(Color.EditorStringConstant, Colors.DarkSeaGreen);
            SetColor(Color.EditorVariable, Colors.Violet);
            SetColor(Color.EditorFunctionDefinition, Colors.DarkViolet);
            SetColor(Color.EditorFunctionCall, Colors.DarkViolet);

            _themeName = "Default";
        }

        public static void InitDark()
        {
            SetColor(Color.EditorBackground, System.Windows.Media.Color.FromRgb(0x12, 0x12, 0x12));
            SetColor(Color.EditorForeground, System.Windows.Media.Color.FromRgb(0x90, 0x90, 0x90));
            SetColor(Color.EditorSelection, System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x30));
            SetColor(Color.EditorLineNumbers, System.Windows.Media.Color.FromRgb(0x50, 0x50, 0x50));

            SetColor(Color.EditorKeyword, System.Windows.Media.Color.FromRgb(0xC0, 0x80, 0xC0));
            SetColor(Color.EditorComment, System.Windows.Media.Color.FromRgb(0x60, 0x70, 0xA0));
            SetColor(Color.EditorIntegerConstant, System.Windows.Media.Color.FromRgb(0x70, 0x70, 0x80));
            SetColor(Color.EditorStringConstant, System.Windows.Media.Color.FromRgb(0xC0, 0xC0, 0x80));
            SetColor(Color.EditorVariable, System.Windows.Media.Color.FromRgb(0x90, 0xA0, 0xC0));
            SetColor(Color.EditorFunctionDefinition, System.Windows.Media.Color.FromRgb(0x50, 0xF0, 0x80));
            SetColor(Color.EditorFunctionCall, System.Windows.Media.Color.FromRgb(0xC0, 0xB8, 0xB8));

            _themeName = "Dark";
        }

        public static System.Windows.Media.Color GetColor(Color color)
        {
            return _colors[(int)color];
        }

        public static void SetColor(Color color, System.Windows.Media.Color value)
        {
            var oldValue = _colors[(int)color];
            if (value != oldValue)
            {
                _colors[(int)color] = value;
                OnColorChanged(new ColorChangedEventArgs(color, value, oldValue));

                _themeName = "Custom";
            }
        }

        private static readonly System.Windows.Media.Color[] _colors = new System.Windows.Media.Color[16];
        private static string _themeName;

        public class ColorChangedEventArgs : EventArgs
        {
            public ColorChangedEventArgs(Color color, System.Windows.Media.Color value, System.Windows.Media.Color oldValue)
            {
                Color = color;
                NewValue = value;
                OldValue = oldValue;
            }

            public Color Color { get; private set; }
            public System.Windows.Media.Color NewValue { get; private set; }
            public System.Windows.Media.Color OldValue { get; private set; }
        }

        private static void OnColorChanged(ColorChangedEventArgs e)
        {
            if (ColorChanged != null)
                ColorChanged(typeof(Theme), e);
        }

        public static event EventHandler<ColorChangedEventArgs> ColorChanged;

        public static string Serialize()
        {
            if (_themeName != "Custom")
                return _themeName;

            var builder = new StringBuilder();
            foreach (Color color in Enum.GetValues(typeof(Color)))
            {
                if (color == Color.None)
                    continue;

                if (builder.Length > 0)
                    builder.Append(',');

                var value = GetColor(color);
                builder.Append(color.ToString());
                builder.Append(':');
                builder.AppendFormat("{0:X2}{1:X2}{2:X2}", value.R, value.G, value.B);
            }

            return builder.ToString();
        }

        public static void Deserialize(string serialized)
        {
            if (String.IsNullOrEmpty(serialized) || serialized == "Default")
            {
                InitDefault();
                return;
            }

            if (serialized == "Dark")
            {
                InitDark();
                return;
            }

            InitDefault();

            var tokenizer = Tokenizer.CreateTokenizer(serialized);
            while (tokenizer.NextChar != '\0')
            {
                var setting = tokenizer.ReadTo(':');
                tokenizer.Advance();
                var value = tokenizer.ReadTo(',');
                tokenizer.Advance();

                Color color;
                if (Enum.TryParse(setting.ToString(), out color))
                {
                    var rtb = value.ToString();
                    byte r, g, b;
                    if (Byte.TryParse(rtb.Substring(0, 2), NumberStyles.HexNumber, null, out r) &&
                        Byte.TryParse(rtb.Substring(2, 2), NumberStyles.HexNumber, null, out g) &&
                        Byte.TryParse(rtb.Substring(4, 2), NumberStyles.HexNumber, null, out b))
                    {
                        SetColor(color, System.Windows.Media.Color.FromRgb(r, g, b));
                    }
                }
            }
        }
    }
}
