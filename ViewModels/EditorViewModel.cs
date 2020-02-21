﻿using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.CodeEditor;
using Jamiras.ViewModels.CodeEditor.ToolWindows;
using RATools.Parser;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            ErrorsToolWindow = new ErrorsToolWindowViewModel(this);

            GotoDefinitionCommand = new DelegateCommand(GotoDefinitionAtCursor);
        }

        private readonly GameViewModel _owner;

        protected override void OnUpdateSyntax(ContentChangedEventArgs e)
        {
            var parser = new AchievementScriptParser();
            _parsedContent = parser.Parse(Tokenizer.CreateTokenizer(e.Content));

            if (e.IsAborted)
                return;

            var interpreter = new AchievementScriptInterpreter();
            interpreter.Run(_parsedContent, out _scope);

            if (e.IsAborted)
                return;

            ServiceRepository.Instance.FindService<IBackgroundWorkerService>().InvokeOnUiThread(() =>
            {
                ErrorsToolWindow.References.Clear();
                foreach (var error in _parsedContent.Errors)
                {
                    string message = error.Message;

                    var innerError = error;
                    if (innerError.InnerError != null)
                    {
                        do
                        {
                            innerError = innerError.InnerError;
                        } while (innerError.InnerError != null);

                        if (error.Line != innerError.Line || error.Column != innerError.Column)
                            message += String.Format(" (called from {0}:{1})", error.Line, error.Column);
                    }

                    ErrorsToolWindow.References.Add(new CodeReferenceViewModel
                    {
                        StartLine = innerError.Line,
                        StartColumn = innerError.Column,
                        EndLine = innerError.EndLine,
                        EndColumn = innerError.EndColumn,
                        Message = message
                    });
                }
            });

            if (e.IsAborted)
                return;

            if (e.IsWhitespaceOnlyChange)
                return;

            // wait a short while before updating the editor list
            ServiceRepository.Instance.FindService<ITimerService>().Schedule(() =>
            {
                if (e.IsAborted)
                    return;

                _owner.PopulateEditorList(interpreter);

            }, TimeSpan.FromMilliseconds(700));

            base.OnUpdateSyntax(e);
        }

        protected override void OnContentChanged(ContentChangedEventArgs e)
        {
            // create a backup file in a few seconds
            if (!String.IsNullOrEmpty(_owner.Script.Filename) && _owner.Script.Filename.Contains('\\'))
            {
                ServiceRepository.Instance.FindService<ITimerService>().Schedule(() =>
                {
                    if (e.IsAborted)
                        return;

                    if (_owner.Script.CompareState == GeneratedCompareState.LocalDiffers)
                        _owner.Script.SaveBackup(e.Content);

                }, TimeSpan.FromSeconds(5));
            }

            base.OnContentChanged(e);
        }

        private ExpressionGroup _parsedContent;
        private InterpreterScope _scope;

        private class ErrorsToolWindowViewModel : CodeReferencesToolWindowViewModel
        {
            public ErrorsToolWindowViewModel(CodeEditorViewModel owner)
                : base("Error List", owner)
            {
            }

            public static readonly ModelProperty SelectedReferenceIndexProperty = ModelProperty.Register(typeof(ErrorsToolWindowViewModel), "SelectedReferenceIndex", typeof(int), -1);
            public int SelectedReferenceIndex
            {
                get { return (int)GetValue(SelectedReferenceIndexProperty); }
                set { SetValue(SelectedReferenceIndexProperty, value); }
            }
        }

        public CodeReferencesToolWindowViewModel ErrorsToolWindow { get; private set; }

        public CommandBase GotoDefinitionCommand { get; private set; }

        private string BuildTooltip(ExpressionBase expression)
        {
            if (expression.Line > 0 && expression.EndLine > 0)
            {
                var text = GetText(expression.Line, expression.Column, expression.EndLine, expression.EndColumn + 1);
                var builder = new StringBuilder();
                bool lastCharWasWhitespace = true;
                bool inComment = false;
                foreach (var c in text)
                {
                    if (inComment)
                    {
                        inComment = (c != '\n');
                    }
                    else if (Char.IsWhiteSpace(c))
                    {
                        if (lastCharWasWhitespace)
                            continue;

                        builder.Append(' ');
                        lastCharWasWhitespace = true;
                    }
                    else if (c == '/' && (builder.Length > 0 && builder[builder.Length - 1] == '/'))
                    {
                        inComment = true;
                        builder.Length--;

                        lastCharWasWhitespace = (builder.Length == 0 || builder[builder.Length - 1] == ' ');
                    }
                    else
                    {
                        builder.Append(c);
                        lastCharWasWhitespace = false;
                    }
                }

                if (lastCharWasWhitespace && builder.Length > 0)
                    builder.Length--;

                return builder.ToString();
            }

            var tooltip = expression.ToString();
            var index = tooltip.IndexOf(':');
            if (index >= 0)
            {
                if (tooltip[index + 1] == ' ')
                    index++;
                tooltip = tooltip.Substring(index + 1);
            }

            return tooltip;
        }

        protected override void OnFormatLine(LineFormatEventArgs e)
        {
            int line = e.Line.Line;
            var expressions = new List<ExpressionBase>();
            _parsedContent.GetExpressionsForLine(expressions, line);

            foreach (var expression in expressions)
            {
                string tooltip = null;

                if (_scope != null)
                {
                    var variable = expression as VariableExpression;
                    if (variable != null)
                    {
                        var value = _scope.GetVariable(variable.Name);
                        if (value != null)
                            tooltip = BuildTooltip(value);
                    }

                    var functionCall = expression as FunctionCallExpression;
                    if (functionCall != null)
                    {
                        var function = _scope.GetFunction(functionCall.FunctionName.Name);
                        if (function != null && function.Expressions.Count == 1)
                            tooltip = BuildTooltip(function.Expressions.First());
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

            if (e.Key == System.Windows.Input.Key.E && e.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                ErrorsToolWindow.IsVisible = !ErrorsToolWindow.IsVisible;
                e.Handled = true;
                return;
            }

            if (e.Key == System.Windows.Input.Key.F8)
            {
                GotoNextError(e.Modifiers);
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
                        GotoLine(function.Name.Line);
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

        private void GotoNextError(System.Windows.Input.ModifierKeys modifierKeys)
        {
            var errorsToolWindowViewModel = ((ErrorsToolWindowViewModel)ErrorsToolWindow);
            if (errorsToolWindowViewModel.References.Count > 0)
            {
                if (modifierKeys == System.Windows.Input.ModifierKeys.Shift)
                {
                    if (errorsToolWindowViewModel.SelectedReferenceIndex > 0)
                        errorsToolWindowViewModel.SelectedReferenceIndex--;
                    else
                        errorsToolWindowViewModel.SelectedReferenceIndex = errorsToolWindowViewModel.References.Count - 1;

                    errorsToolWindowViewModel.GotoReferenceCommand.Execute(errorsToolWindowViewModel.References[errorsToolWindowViewModel.SelectedReferenceIndex]);
                }
                else if (modifierKeys == System.Windows.Input.ModifierKeys.None)
                {
                    if (errorsToolWindowViewModel.SelectedReferenceIndex < 0)
                        errorsToolWindowViewModel.SelectedReferenceIndex = 0;
                    else
                        errorsToolWindowViewModel.SelectedReferenceIndex = (errorsToolWindowViewModel.SelectedReferenceIndex + 1) % errorsToolWindowViewModel.References.Count;

                    errorsToolWindowViewModel.GotoReferenceCommand.Execute(errorsToolWindowViewModel.References[errorsToolWindowViewModel.SelectedReferenceIndex]);
                }
            }
        }
    }
}
