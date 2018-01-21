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

            Content = "// Super Mario Bros\n" +
                      "// #ID = 1446\n" +
                      "\n" +
                      "// Event music buffer\n" +
                      "function current_music() => byte(0x0000F0)\n";
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
            var expressions = new List<ExpressionBase>();
            if (_expressionGroup.GetExpressionsForLine(expressions, e.Line.Line))
            {
                foreach (var expression in expressions)
                {
                    var expressionStart = (expression.Line == e.Line.Line) ? expression.Column : 1;
                    var expressionEnd = (expression.EndLine == e.Line.Line) ? expression.EndColumn : e.Line.LineLength + 1;

                    if (expression is ParseErrorExpression)
                        e.SetError(expressionStart, expressionEnd - expressionStart + 1, ((ParseErrorExpression)expression).Message);
                    else
                        e.SetColor(expressionStart, expressionEnd - expressionStart + 1, (int)expression.Type);
                }
            }

            base.OnLineChanged(e);
        }
    }
}
