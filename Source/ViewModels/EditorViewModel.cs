using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.CodeEditor;
using Jamiras.ViewModels.CodeEditor.ToolWindows;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.ViewModels
{
    public class EditorViewModel : CodeEditorViewModel, IDisposable
    {
        public EditorViewModel(GameViewModel owner)
        {
            _owner = owner;

            if (_themeColors == null)
            {
                _themeColors = new Dictionary<Theme.Color, ExpressionType>();
                _themeColors[Theme.Color.EditorKeyword] = ExpressionType.Keyword;
                _themeColors[Theme.Color.EditorComment] = ExpressionType.Comment;
                _themeColors[Theme.Color.EditorIntegerConstant] = ExpressionType.IntegerConstant;
                _themeColors[Theme.Color.EditorStringConstant] = ExpressionType.StringConstant;
                _themeColors[Theme.Color.EditorVariable] = ExpressionType.Variable;
                _themeColors[Theme.Color.EditorFunctionDefinition] = ExpressionType.FunctionDefinition;
                _themeColors[Theme.Color.EditorFunctionCall] = ExpressionType.FunctionCall;
            }

            Theme.ColorChanged += Theme_ColorChanged;

            foreach (var kvp in _themeColors)
                Style.SetCustomColor((int)kvp.Value, Theme.GetColor(kvp.Key));
            Style.SetCustomColor((int)ExpressionType.BooleanConstant, Theme.GetColor(Theme.Color.EditorIntegerConstant));
            Style.SetCustomColor((int)ExpressionType.FloatConstant, Theme.GetColor(Theme.Color.EditorIntegerConstant));
            Style.SetCustomColor((int)ExpressionType.Array, Theme.GetColor(Theme.Color.Foreground));
            Style.SetCustomColor((int)ExpressionType.Dictionary, Theme.GetColor(Theme.Color.Foreground));
            Style.Background = Theme.GetColor(Theme.Color.Background);
            Style.Foreground = Theme.GetColor(Theme.Color.Foreground);
            Style.Selection = Theme.GetColor(Theme.Color.EditorSelection);
            Style.LineNumber = Theme.GetColor(Theme.Color.EditorLineNumbers);

            Braces['('] = ')';
            Braces['['] = ']';
            Braces['{'] = '}';
            Braces['"'] = '"';

            ErrorsToolWindow = new ErrorsToolWindowViewModel(this);

            GotoDefinitionCommand = new DelegateCommand(GotoDefinitionAtCursor);
        }

        public virtual void Dispose()
        {
            Theme.ColorChanged -= Theme_ColorChanged;
        }

        private void Theme_ColorChanged(object sender, Theme.ColorChangedEventArgs e)
        {
            switch (e.Color)
            {
                case Theme.Color.Background:
                    Style.Background = e.NewValue;
                    break;

                case Theme.Color.Foreground:
                    Style.Foreground = e.NewValue;
                    break;

                case Theme.Color.EditorSelection:
                    Style.Selection = e.NewValue;
                    break;

                case Theme.Color.EditorLineNumbers:
                    Style.LineNumber = e.NewValue;
                    break;

                default:
                    ExpressionType type;
                    if (!_themeColors.TryGetValue(e.Color, out type))
                        return;

                    Style.SetCustomColor((int)type, e.NewValue);

                    if (type == ExpressionType.IntegerConstant)
                        Style.SetCustomColor((int)ExpressionType.BooleanConstant, e.NewValue);

                    break;
            }

            // force repaint of all lines
            foreach (var line in Lines)
                line.Refresh();
        }

        private static Dictionary<Theme.Color, ExpressionType> _themeColors;

        private readonly GameViewModel _owner;

        private class ScriptInterpreterCallback : IScriptInterpreterCallback
        {
            public ScriptInterpreterCallback(EditorViewModel owner, ContentChangedEventArgs e)
            {
                _owner = owner;
                _args = e;
            }

            private EditorViewModel _owner;
            private ContentChangedEventArgs _args;

            public bool IsAborted
            {
                get { return _args.IsAborted; }
            }

            public void UpdateProgress(int percentage, int line)
            {
                _owner.UpdateProgress(percentage, line);
            }
        }

        protected override void OnUpdateSyntax(ContentChangedEventArgs e)
        {
            bool needsUpdate = false;

            // parse immediately so we can update the syntax highlighting
            var tokenizer = Tokenizer.CreateTokenizer(e.Content);
            if (_parsedContent == null || e.Type == ContentChangeType.Refresh)
            {
                _parsedContent = AchievementScriptInterpreter.CreateExpressionGroupCollection();

                lock (_parsedContent)
                {
                    _parsedContent.Parse(tokenizer);
                    needsUpdate = true;
                }
            }
            else if (e.Type == ContentChangeType.Update)
            {
                lock (_parsedContent)
                {
                    needsUpdate = _parsedContent.Update(tokenizer, e.AffectedLines);
                }

                if (!needsUpdate && !e.IsAborted)
                    UpdateErrorList();
            }

            if (needsUpdate && !e.IsAborted)
            {
                // running the script can take a lot of time, push that work onto a background thread and show progress bar
                UpdateProgress(1, 0);

                // make sure to at least show the script file in the editor list
                if (!_owner.Editors.Any())
                    _owner.PopulateEditorList(null);

                ServiceRepository.Instance.FindService<IBackgroundWorkerService>().RunAsync(() =>
                {
                    if (!e.IsAborted)
                    {
                        // run the script
                        var callback = new ScriptInterpreterCallback(this, e);
                        var interpreter = new AchievementScriptInterpreter();
                        interpreter.Initialize(_owner.PublishedSets);

                        bool hadErrors, hasErrors;
                        lock (_parsedContent)
                        {
                            hadErrors = _parsedContent.HasEvaluationErrors;
                            hasErrors = !interpreter.Run(_parsedContent, callback);
                        }

                        if (!e.IsAborted)
                        {
                            UpdateProgress(100, 0);

                            // if any errors were added or removed, update the highlighting
                            if (hasErrors != hadErrors)
                                UpdateSyntaxHighlighting(e);

                            // report any errors
                            UpdateErrorList();

                            if (!e.IsAborted)
                            {
                                // update the editor list
                                _owner.PopulateEditorList(interpreter);
                            }
                        }
                    }

                    // make sure the progress bar is hidden
                    UpdateProgress(0, 0);
                });
            }

            base.OnUpdateSyntax(e);
        }

        internal void UpdateProgress(int progress, int line)
        {
            _owner.UpdateCompileProgress(progress, line);
        }

        private void UpdateErrorList()
        {
            ServiceRepository.Instance.FindService<IBackgroundWorkerService>().InvokeOnUiThread(() =>
            {
                ErrorsToolWindow.References.Clear();
                foreach (var error in _parsedContent.Errors)
                {
                    var errors = new Stack<CodeReferenceViewModel>();

                    ErrorExpression innerError = error;
                    while (innerError != null)
                    {
                        errors.Push(new CodeReferenceViewModel
                        {
                            StartLine = innerError.Location.Start.Line,
                            StartColumn = innerError.Location.Start.Column,
                            EndLine = innerError.Location.End.Line,
                            EndColumn = innerError.Location.End.Column,
                            Message = innerError.Message
                        });

                        innerError = innerError.InnerError;
                    }

                    int depth = 0;
                    while (errors.Count() > 0)
                    {
                        var errorViewModel = errors.Pop();
                        if (depth > 0)
                        {
                            errorViewModel.Message = "> " + errorViewModel.Message;
                            if (depth > 1)
                                errorViewModel.Message = new string(' ', (depth - 1) * 2) + errorViewModel.Message;
                        }

                        ErrorsToolWindow.References.Add(errorViewModel);
                        depth++;
                    }
                }
            });
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

        private ExpressionGroupCollection _parsedContent;

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
            if (expression.Location.Start.Line > 0 && expression.Location.End.Line > 0)
            {
                var text = GetText(expression.Location.Start.Line, expression.Location.Start.Column,
                    expression.Location.End.Line, expression.Location.End.Column + 1);
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
            int line = e.LineNumber;
            var expressions = new List<ExpressionBase>();
            if (_parsedContent != null)
                _parsedContent.GetExpressionsForLine(expressions, line);

            foreach (var expression in expressions)
            {
                string tooltip = null;

                if (_parsedContent.Scope != null)
                {
                    var variable = expression as VariableExpression;
                    if (variable != null)
                    {
                        var value = _parsedContent.Scope.GetVariable(variable.Name);
                        if (value != null)
                            tooltip = BuildTooltip(value);
                    }

                    var functionCall = expression as FunctionNameExpression;
                    if (functionCall != null)
                    {
                        var function = _parsedContent.Scope.GetFunction(functionCall.Name);
                        if (function != null && function.Expressions.Count == 1)
                            tooltip = BuildTooltip(function.Expressions.First());
                    }
                }

                var expressionStart = (expression.Location.Start.Line == line) ? expression.Location.Start.Column : 1;
                var expressionEnd = (expression.Location.End.Line == line) ? expression.Location.End.Column : e.Text.Length + 1;

                var parseError = expression as ErrorExpression;
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

        public override void MoveCursorTo(int line, int column, MoveCursorFlags flags)
        {
            if (flags == MoveCursorFlags.None && Math.Abs(line - CursorLine) > 1)
            {
                // if the cursor is being moved more than one line away, capture the current location so the user can return to it.
                _owner.CaptureNavigationLocation();
            }

            base.MoveCursorTo(line, column, flags);
        }

        private void GotoDefinitionAtCursor()
        {
            if (_parsedContent.Scope == null)
                return;

            var expressions = new List<ExpressionBase>();
            if (!_parsedContent.GetExpressionsForLine(expressions, CursorLine))
                return;

            var column = CursorColumn;
            foreach (var expression in expressions)
            {
                if (column < expression.Location.Start.Column || column > expression.Location.End.Column + 1)
                    continue;

                var functionCall = expression as FunctionNameExpression;
                if (functionCall != null)
                {
                    var function = _parsedContent.Scope.GetFunction(functionCall.Name);
                    if (function != null && function.Location.Start.Line != 0)
                    {
                        GotoLine(function.Name.Location.Start.Line);
                        MoveCursorTo(function.Name.Location.Start.Line, function.Name.Location.Start.Column, MoveCursorFlags.None);
                        MoveCursorTo(function.Name.Location.End.Line, function.Name.Location.End.Column + 1, MoveCursorFlags.Highlighting);
                    }

                    break;
                }

                var variableReference = expression as VariableExpression;
                if (variableReference != null)
                {
                    var variable = _parsedContent.Scope.GetVariableDefinition(variableReference.Name);
                    if (variable != null && variable.Location.Start.Line != 0)
                    {
                        MoveCursorTo(variable.Location.Start.Line, variable.Location.Start.Column, MoveCursorFlags.None);
                        MoveCursorTo(variable.Location.End.Line, variable.Location.End.Column + 1, MoveCursorFlags.Highlighting);
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
