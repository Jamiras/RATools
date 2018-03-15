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
            ParsedContent = parser.Parse(Tokenizer.CreateTokenizer(newValue));
            base.OnContentChanged(newValue);
        }

        internal ExpressionGroup ParsedContent { get; private set; }

        protected override void OnFormatLine(LineFormatEventArgs e)
        {
            int line = e.Line.Line;
            var expressions = new List<ExpressionBase>();
            if (ParsedContent.GetExpressionsForLine(expressions, line))
            {
                foreach (var expression in expressions)
                {
                    var expressionStart = (expression.Line == line) ? expression.Column : 1;
                    var expressionEnd = (expression.EndLine == line) ? expression.EndColumn : e.Line.Text.Length + 1;

                    if (expression is ParseErrorExpression)
                        e.SetError(expressionStart, expressionEnd - expressionStart + 1, ((ParseErrorExpression)expression).Message);
                    else
                        e.SetColor(expressionStart, expressionEnd - expressionStart + 1, (int)expression.Type);
                }
            }

            base.OnFormatLine(e);
        }
    }
}
