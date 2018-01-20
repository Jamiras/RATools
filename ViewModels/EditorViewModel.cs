using Jamiras.Components;
using Jamiras.ViewModels.CodeEditor;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Windows.Media;

namespace RATools.ViewModels
{
    public class EditorViewModel : CodeEditorViewModel
    {
        public EditorViewModel()
        {
            Style.SetCustomColor((int)ExpressionType.Comment, Colors.DarkCyan);
            Style.SetCustomColor((int)ExpressionType.IntegerConstant, Colors.DarkGray);
            Style.SetCustomColor((int)ExpressionType.FunctionDefinition, Colors.DarkViolet);
            Style.SetCustomColor((int)ExpressionType.FunctionCall, Colors.DarkViolet);
            Style.SetCustomColor((int)ExpressionType.Variable, Colors.Violet);
            Style.SetCustomColor((int)ExpressionType.StringConstant, Colors.DarkSeaGreen);
            Style.SetCustomColor((int)ExpressionType.Keyword, Colors.DarkGoldenrod);
            Style.SetCustomColor((int)ExpressionType.ParseError, Colors.Red);
        }

        protected override void OnContentChanged(string newValue)
        {
            var parser = new AchievementScriptParser();
            _expressionGroup = parser.Parse(Tokenizer.CreateTokenizer(newValue));
            base.OnContentChanged(newValue);
        }

        private ExpressionGroup _expressionGroup;

        protected override void OnLineChanged(LineChangedEventArgs e)
        {
            base.OnLineChanged(e);

            var expressions = new List<ExpressionBase>();
            if (_expressionGroup.GetExpressionsForLine(expressions, e.Line.Line))
            {
                foreach (var expression in expressions)
                    e.SetColor(expression.Column, expression.EndColumn - expression.Column + 1, (int)expression.Type);
            }

            base.OnLineChanged(e);
        }
    }
}
