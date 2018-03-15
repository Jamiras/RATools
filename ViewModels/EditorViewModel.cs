using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.CodeEditor;
using RATools.Parser;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Windows.Media;

namespace RATools.ViewModels
{
    public class EditorViewModel : CodeEditorViewModel
    {
        public EditorViewModel(GameViewModel owner)
        {
            _owner = owner;

            Style.SetCustomColor((int)ExpressionType.Comment, Colors.DarkCyan);
            Style.SetCustomColor((int)ExpressionType.IntegerConstant, Colors.DarkGray);
            Style.SetCustomColor((int)ExpressionType.FunctionDefinition, Colors.DarkViolet);
            Style.SetCustomColor((int)ExpressionType.FunctionCall, Colors.DarkViolet);
            Style.SetCustomColor((int)ExpressionType.Variable, Colors.Violet);
            Style.SetCustomColor((int)ExpressionType.StringConstant, Colors.DarkSeaGreen);
            Style.SetCustomColor((int)ExpressionType.Keyword, Colors.DarkGoldenrod);
            Style.SetCustomColor((int)ExpressionType.ParseError, Colors.Red);

            GotoDefinitionCommand = new DelegateCommand(GotoDefinitionAtCursor);
        }

        private readonly GameViewModel _owner;

        protected override void OnContentChanged(string newValue)
        {
            var backgroundWorkerService = ServiceRepository.Instance.FindService<IBackgroundWorkerService>();
            backgroundWorkerService.RunAsync(() =>
            {
                var parser = new AchievementScriptParser();
                _parsedContent = parser.Parse(Tokenizer.CreateTokenizer(newValue));

                var interpreter = new AchievementScriptInterpreter();
                interpreter.Run(_parsedContent, out _scope);

                _owner.PopulateEditorList(interpreter);
            });

            base.OnContentChanged(newValue);
        }

        private ExpressionGroup _parsedContent;
        private InterpreterScope _scope;

        public CommandBase GotoDefinitionCommand { get; private set; }

        protected override void OnFormatLine(LineFormatEventArgs e)
        {
            int line = e.Line.Line;
            var expressions = new List<ExpressionBase>();
            if (_parsedContent.GetExpressionsForLine(expressions, line))
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

        protected override void OnKeyPressed(KeyPressedEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F12 && e.Modifiers == System.Windows.Input.ModifierKeys.None)
            {
                GotoDefinitionAtCursor();
                e.Handled = true;
                return;
            }

            base.OnKeyPressed(e);
        }

        private void GotoDefinitionAtCursor()
        {
            if (_scope == null)
                return;

            var expressions = new List<ExpressionBase>();
            if (!_parsedContent.GetExpressionsForLine(expressions, CursorLine))
                return;

            var column = CursorColumn;
            foreach (var expression in expressions)
            {
                if (column < expression.Column || column > expression.EndColumn + 1)
                    continue;

                var functionCall = expression as FunctionCallExpression;
                if (functionCall != null)
                {
                    var function = _scope.GetFunction(functionCall.FunctionName.Name);
                    if (function != null && function.Line != 0)
                    {
                        MoveCursorTo(function.Name.Line, function.Name.Column, MoveCursorFlags.None);
                        MoveCursorTo(function.Name.EndLine, function.Name.EndColumn + 1, MoveCursorFlags.Highlighting);
                    }

                    break;
                }

                var variableReference = expression as VariableExpression;
                if (variableReference != null)
                {
                    var variable = _scope.GetVariableDefinition(variableReference.Name);
                    if (variable != null && variable.Line != 0)
                    {
                        MoveCursorTo(variable.Line, variable.Column, MoveCursorFlags.None);
                        MoveCursorTo(variable.EndLine, variable.EndColumn + 1, MoveCursorFlags.Highlighting);
                    }

                    break;
                }
            }
        }
    }
}
