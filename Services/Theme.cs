using System;
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
            EditorError,
        }

        static Theme()
        {
            _colors[(int)Color.EditorBackground] = Colors.White;
            _colors[(int)Color.EditorForeground] = Colors.Black;
            _colors[(int)Color.EditorSelection] = Colors.LightGray;
            _colors[(int)Color.EditorLineNumbers] = Colors.LightGray;

            _colors[(int)Color.EditorKeyword] = Colors.DarkGoldenrod;
            _colors[(int)Color.EditorComment] = Colors.DarkCyan;
            _colors[(int)Color.EditorIntegerConstant] = Colors.DarkGray;
            _colors[(int)Color.EditorStringConstant] = Colors.DarkSeaGreen;
            _colors[(int)Color.EditorVariable] = Colors.Violet;
            _colors[(int)Color.EditorFunctionDefinition] = Colors.DarkViolet;
            _colors[(int)Color.EditorFunctionCall] = Colors.DarkViolet;
            _colors[(int)Color.EditorError] = Colors.Red;
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
            }
        }

        private static readonly System.Windows.Media.Color[] _colors = new System.Windows.Media.Color[16];

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
    }
}
