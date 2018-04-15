using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.CodeEditor;
using Jamiras.ViewModels.CodeEditor.ToolWindows;
using RATools.Parser;
using RATools.Parser.Internal;
using System;
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

            Braces['('] = ')';
            Braces['['] = ']';
            Braces['{'] = '}';
            Braces['"'] = '"';

            ErrorsToolWindow = new CodeReferencesToolWindowViewModel("Error List", this);

            GotoDefinitionCommand = new DelegateCommand(GotoDefinitionAtCursor);
        }

        private readonly GameViewModel _owner;

        protected override void OnUpdateSyntax(ContentChangedEventArgs e)
        {
            var parser = new AchievementScriptParser();
            _parsedContent = parser.Parse(Tokenizer.CreateTokenizer(e.Content));

            if (e.IsAborted)
                return;

            ServiceRepository.Instance.FindService<IBackgroundWorkerService>().InvokeOnUiThread(() =>
            {
                ErrorsToolWindow.References.Clear();
                foreach (var error in _parsedContent.Errors)
                {
                    var innerError = error;
                    while (innerError.InnerError != null)
                        innerError = innerError.InnerError;

                    ErrorsToolWindow.References.Add(new CodeReferenceViewModel
                    {
                        StartLine = innerError.Line,
                        StartColumn = innerError.Column,
                        EndLine = innerError.EndLine,
                        EndColumn = innerError.EndColumn,
                        Message = innerError.Message
                    });
                }
            });

            if (e.IsAborted)
                return;

            if (e.IsWhitespaceOnlyChange)
                return;

            var interpreter = new AchievementScriptInterpreter();
            interpreter.Run(_parsedContent, out _scope);

            if (e.IsAborted)
                return;

            ServiceRepository.Instance.FindService<ITimerService>().Schedule(() =>
            {
                if (e.IsAborted)
                    return;

                _owner.PopulateEditorList(interpreter);

            }, TimeSpan.FromMilliseconds(700));

            base.OnUpdateSyntax(e);
        }

        private ExpressionGroup _parsedContent;
        private InterpreterScope _scope;

        public CodeReferencesToolWindowViewModel ErrorsToolWindow { get; private set; }

        public CommandBase GotoDefinitionCommand { get; private set; }

        protected override void OnFormatLine(LineFormatEventArgs e)
        {
            int line = e.Line.Line;
            var expressions = new List<ExpressionBase>();
            _parsedContent.GetExpressionsForLine(expressions, line);

            foreach (var expression in expressions)
            {
                string tooltip = null;

                var variable = expression as VariableExpression;
                if (variable != null)
                {
                    var value = _scope.GetVariable(variable.Name);
                    if (value != null)
                    {
                        tooltip = value.ToString();
                        var index = tooltip.IndexOf(':');
                        if (index >= 0)
                        {
                            if (tooltip[index + 1] == ' ')
                                index++;
                            tooltip = tooltip.Substring(index + 1);
                        }
                    }
                }

                var expressionStart = (expression.Line == line) ? expression.Column : 1;
                var expressionEnd = (expression.EndLine == line) ? expression.EndColumn : e.Line.Text.Length + 1;

                var parseError = expression as ParseErrorExpression;
                if (parseError != null)
                    e.SetError(expressionStart, expressionEnd - expressionStart + 1, parseError.Message);
                else
                    e.SetColor(expressionStart, expressionEnd - expressionStart + 1, (int)expression.Type, tooltip);
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
