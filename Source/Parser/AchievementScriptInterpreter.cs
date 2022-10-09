﻿using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    public interface IScriptInterpreterCallback
    {
        void UpdateProgress(int percentage, int line);
        bool IsAborted { get; }
    }

    public class AchievementScriptInterpreter
    {
        public AchievementScriptInterpreter()
        {
            _achievements = new List<Achievement>();
            _leaderboards = new List<Leaderboard>();
            _richPresence = new RichPresenceBuilder();
        }

        internal RichPresenceBuilder RichPresenceBuilder
        {
            get { return _richPresence; }
        }
        private readonly RichPresenceBuilder _richPresence;

        /// <summary>
        /// Gets the achievements generated by the script.
        /// </summary>
        public IEnumerable<Achievement> Achievements
        {
            get { return _achievements; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Achievement> _achievements;

        /// <summary>
        /// Gets the game identifier from the script.
        /// </summary>
        public int GameId { get; private set; }

        /// <summary>
        /// Gets the game title from the script.
        /// </summary>
        public string GameTitle { get; private set; }

        /// <summary>
        /// Gets the rich presence script generated by the script.
        /// </summary>
        public string RichPresence { get; internal set; }

        internal int RichPresenceLine { get; private set; }

        /// <summary>
        /// Gets the leaderboards generated by the script.
        /// </summary>
        public IEnumerable<Leaderboard> Leaderboards
        {
            get { return _leaderboards; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Leaderboard> _leaderboards;

        internal static InterpreterScope GetGlobalScope()
        {
            if (_globalScope == null)
            {
                _globalScope = new InterpreterScope();
                _globalScope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));
                _globalScope.AddFunction(new MemoryAccessorFunction("bit0", FieldSize.Bit0));
                _globalScope.AddFunction(new MemoryAccessorFunction("bit1", FieldSize.Bit1));
                _globalScope.AddFunction(new MemoryAccessorFunction("bit2", FieldSize.Bit2));
                _globalScope.AddFunction(new MemoryAccessorFunction("bit3", FieldSize.Bit3));
                _globalScope.AddFunction(new MemoryAccessorFunction("bit4", FieldSize.Bit4));
                _globalScope.AddFunction(new MemoryAccessorFunction("bit5", FieldSize.Bit5));
                _globalScope.AddFunction(new MemoryAccessorFunction("bit6", FieldSize.Bit6));
                _globalScope.AddFunction(new MemoryAccessorFunction("bit7", FieldSize.Bit7));
                _globalScope.AddFunction(new MemoryAccessorFunction("low4", FieldSize.LowNibble));
                _globalScope.AddFunction(new MemoryAccessorFunction("high4", FieldSize.HighNibble));
                _globalScope.AddFunction(new MemoryAccessorFunction("word", FieldSize.Word));
                _globalScope.AddFunction(new MemoryAccessorFunction("tbyte", FieldSize.TByte));
                _globalScope.AddFunction(new MemoryAccessorFunction("dword", FieldSize.DWord));
                _globalScope.AddFunction(new MemoryAccessorFunction("word_be", FieldSize.BigEndianWord));
                _globalScope.AddFunction(new MemoryAccessorFunction("tbyte_be", FieldSize.BigEndianTByte));
                _globalScope.AddFunction(new MemoryAccessorFunction("dword_be", FieldSize.BigEndianDWord));
                _globalScope.AddFunction(new MemoryAccessorFunction("float", FieldSize.Float));
                _globalScope.AddFunction(new MemoryAccessorFunction("mbf32", FieldSize.MBF32));
                _globalScope.AddFunction(new BitFunction());
                _globalScope.AddFunction(new MemoryAccessorFunction("bitcount", FieldSize.BitCount));

                _globalScope.AddFunction(new PrevPriorFunction("prev", FieldType.PreviousValue));
                _globalScope.AddFunction(new PrevPriorFunction("prior", FieldType.PriorValue));
                _globalScope.AddFunction(new PrevPriorFunction("bcd", FieldType.BinaryCodedDecimal));

                _globalScope.AddFunction(new OnceFunction());
                _globalScope.AddFunction(new RepeatedFunction());
                _globalScope.AddFunction(new TallyFunction());
                _globalScope.AddFunction(new DeductFunction());
                _globalScope.AddFunction(new NeverFunction());
                _globalScope.AddFunction(new UnlessFunction());
                _globalScope.AddFunction(new TriggerWhenFunction());
                _globalScope.AddFunction(new MeasuredFunction());
                _globalScope.AddFunction(new DisableWhenFunction());

                _globalScope.AddFunction(new AchievementFunction());
                _globalScope.AddFunction(new LeaderboardFunction());
                _globalScope.AddFunction(new MaxOfFunction());

                _globalScope.AddFunction(new RichPresenceDisplayFunction());
                _globalScope.AddFunction(new RichPresenceConditionalDisplayFunction());
                _globalScope.AddFunction(new RichPresenceValueFunction());
                _globalScope.AddFunction(new RichPresenceMacroFunction());
                _globalScope.AddFunction(new RichPresenceLookupFunction());

                _globalScope.AddFunction(new AlwaysTrueFunction());
                _globalScope.AddFunction(new AlwaysFalseFunction());

                _globalScope.AddFunction(new AllOfFunction());
                _globalScope.AddFunction(new AnyOfFunction());
                _globalScope.AddFunction(new NoneOfFunction());
                _globalScope.AddFunction(new SumOfFunction());
                _globalScope.AddFunction(new TallyOfFunction());

                _globalScope.AddFunction(new RangeFunction());
                _globalScope.AddFunction(new FormatFunction());
                _globalScope.AddFunction(new LengthFunction());
                _globalScope.AddFunction(new ArrayPushFunction());
                _globalScope.AddFunction(new ArrayPopFunction());
                _globalScope.AddFunction(new ArrayMapFunction());

                _globalScope.AddFunction(new RepeatedFunction.OrNextWrapperFunction());
            }

            return _globalScope;
        }
        private static InterpreterScope _globalScope;

        /// <summary>
        /// Gets the error message generated by the script if processing failed.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                if (Error == null)
                    return null;

                var builder = new StringBuilder();
                builder.AppendFormat("{0}:{1} {2}", Error.Location.Start.Line, Error.Location.Start.Column, Error.Message);
                var error = Error.InnerError;
                while (error != null)
                {
                    builder.AppendLine();
                    builder.AppendFormat("- {0}:{1} {2}", error.Location.Start.Line, error.Location.Start.Column, error.Message);
                    error = error.InnerError;
                }
                return builder.ToString();
            }
        }

        internal ErrorExpression Error { get; private set; }

        public string GetFormattedErrorMessage(Tokenizer tokenizer)
        {
            var neededLines = new List<int>();
            var error = Error;
            while (error != null)
            {
                for (int i = error.Location.Start.Line; i <= error.Location.End.Line; i++)
                {
                    if (!neededLines.Contains(i))
                        neededLines.Add(i);
                }

                error = error.InnerError;
            }

            neededLines.Sort();

            var lineDictionary = new TinyDictionary<int, string>();
            var positionalTokenizer = new PositionalTokenizer(tokenizer);
            int lineIndex = 0;
            while (lineIndex < neededLines.Count)
            {
                while (positionalTokenizer.NextChar != '\0' && positionalTokenizer.Line != neededLines[lineIndex])
                {
                    positionalTokenizer.ReadTo('\n');
                    positionalTokenizer.Advance();
                }

                lineDictionary[neededLines[lineIndex]] = positionalTokenizer.ReadTo('\n').TrimRight().ToString();
                lineIndex++;
            }

            var builder = new StringBuilder();
            error = Error;
            while (error != null)
            {
                builder.AppendFormat("{0}:{1} {2}", error.Location.Start.Line, error.Location.Start.Column, error.Message);
                builder.AppendLine();
                //for (int i = error.Line; i <= error.EndLine; i++)
                int i = error.Location.Start.Line; // TODO: show all lines associated to error?
                {
                    var line = lineDictionary[error.Location.Start.Line];

                    builder.Append(":: ");
                    var startColumn = 0;
                    while (Char.IsWhiteSpace(line[startColumn]))
                        startColumn++;

                    if (i == error.Location.Start.Line)
                    {
                        builder.Append("{{color|#C0C0C0|");
                        builder.Append(line.Substring(startColumn, error.Location.Start.Column - startColumn - 1));
                        builder.Append("}}");
                        startColumn = error.Location.Start.Column - 1;
                    }

                    if (i == error.Location.End.Line)
                    {
                        builder.Append(line.Substring(startColumn, error.Location.End.Column - startColumn));
                        builder.Append("{{color|#C0C0C0|");
                        builder.Append(line.Substring(error.Location.End.Column));
                        builder.Append("}}");
                    }
                    else
                    {
                        builder.Append(line.Substring(startColumn));
                    }
                    builder.AppendLine();
                }
                builder.AppendLine();
                error = error.InnerError;
            }

            while (builder.Length > 0 && Char.IsWhiteSpace(builder[builder.Length - 1]))
                builder.Length--;

            return builder.ToString();
        }

        /// <summary>
        /// Processes the provided script.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the script was successfully processed, 
        /// <c>false</c> if not - in which case <see cref="ErrorMessage"/> will indicate why.
        /// </returns>
        public bool Run(Tokenizer input)
        {
            var expressionGroups = new ExpressionGroupCollection();
            expressionGroups.Parse(input);

            if (Error == null)
            {
                foreach (var group in expressionGroups.Groups)
                {
                    Error = group.ParseErrors.FirstOrDefault();
                    if (Error != null)
                        return false;
                }
            }

            GameTitle = null;
            foreach (var comment in expressionGroups.Groups.First().Expressions.OfType<CommentExpression>())
            {
                if (comment.Value.Contains("#ID"))
                {
                    ExtractGameId(new Token(comment.Value, 0, comment.Value.Length));
                    break;
                }
                else if (GameTitle == null)
                {
                    GameTitle = comment.Value.Substring(2).Trim();
                }
            }

            return Run(expressionGroups, null);
        }

        internal bool Run(ExpressionGroupCollection expressionGroups, IScriptInterpreterCallback callback)
        {
            AchievementScriptContext scriptContext = null;
            InterpreterScope scope = expressionGroups.Scope;

            if (scope != null)
                scriptContext = scope.GetContext<AchievementScriptContext>();

            if (scriptContext == null)
            {
                scriptContext = new AchievementScriptContext();
                scope = new InterpreterScope(expressionGroups.Scope ?? GetGlobalScope()) { Context = scriptContext };
            }

            expressionGroups.ResetErrors();

            bool result = true;
            foreach (var expressionGroup in expressionGroups.Groups)
            {
                if (expressionGroup.NeedsEvaluated)
                {
                    if (scriptContext.Achievements == null)
                        scriptContext.Achievements = new List<Achievement>();
                    if (scriptContext.Leaderboards == null)
                        scriptContext.Leaderboards = new List<Leaderboard>();
                    if (scriptContext.RichPresence == null)
                        scriptContext.RichPresence = new RichPresenceBuilder();

                    if (!Evaluate(expressionGroup.Expressions, scope, callback))
                    {
                        var error = Error;
                        if (error != null)
                            expressionGroups.AddEvaluationError(error);

                        result = false;
                    }

                    if (scriptContext.Achievements.Count > 0)
                    {
                        expressionGroup.GeneratedAchievements = scriptContext.Achievements;
                        scriptContext.Achievements = null;
                    }
                    else if (expressionGroup.GeneratedAchievements != null)
                    {
                        expressionGroup.GeneratedAchievements = null;
                    }

                    if (scriptContext.Leaderboards.Count > 0)
                    {
                        expressionGroup.GeneratedLeaderboards = scriptContext.Leaderboards;
                        scriptContext.Leaderboards = null;
                    }
                    else if (expressionGroup.GeneratedLeaderboards != null)
                    {
                        expressionGroup.GeneratedLeaderboards = null;
                    }

                    if (!scriptContext.RichPresence.IsEmpty)
                    {
                        expressionGroup.GeneratedRichPresence = scriptContext.RichPresence;
                        scriptContext.RichPresence = null;
                    }

                    expressionGroup.MarkEvaluated();
                }
            }

            if (!ReferenceEquals(scope, expressionGroups.Scope))
            {
                if (scope.FunctionCount > 0 || scope.VariableCount > 0)
                {
                    if (expressionGroups.Scope != null)
                        expressionGroups.Scope.Merge(scope);
                    else
                        expressionGroups.Scope = scope;
                }
            }

            _achievements.Clear();
            _leaderboards.Clear();
            _richPresence.Clear();

            foreach (var expressionGroup in expressionGroups.Groups)
            {
                if (expressionGroup.GeneratedAchievements != null)
                    _achievements.AddRange(expressionGroup.GeneratedAchievements);

                if (expressionGroup.GeneratedLeaderboards != null)
                    _leaderboards.AddRange(expressionGroup.GeneratedLeaderboards);

                if (expressionGroup.GeneratedRichPresence != null)
                {
                    var error = _richPresence.Merge(expressionGroup.GeneratedRichPresence);
                    if (error != null)
                    {
                        expressionGroups.AddEvaluationError(error);
                        result = false;
                    }
                }
            }

            double minimumVersion = 0.30;
            foreach (var achievement in _achievements)
            {
                var achievementMinimumVersion = AchievementBuilder.GetMinimumVersion(achievement);
                if (achievementMinimumVersion > minimumVersion)
                    minimumVersion = achievementMinimumVersion;
            }

            foreach (var leaderboard in _leaderboards)
            {
                var leaderboardMinimumVersion = AchievementBuilder.GetMinimumVersion(leaderboard);
                if (leaderboardMinimumVersion > minimumVersion)
                    minimumVersion = leaderboardMinimumVersion;
            }

            _richPresence.DisableLookupCollapsing = (minimumVersion < 0.79);
            _richPresence.DisableBuiltInMacros = (minimumVersion < 0.80);

            if (!String.IsNullOrEmpty(_richPresence.DisplayString))
            {
                RichPresence = _richPresence.ToString();
                RichPresenceLine = _richPresence.Line;
            }

            if (Error == null)
                Error = expressionGroups.Errors.FirstOrDefault();

            return result;
        }

        private void ExtractGameId(Token line)
        {
            var tokens = line.Split('=');
            if (tokens.Length > 1)
            {
                int gameId;
                if (Int32.TryParse(tokens[1].ToString(), out gameId))
                    GameId = gameId;
            }
        }

        internal bool Evaluate(IEnumerable<ExpressionBase> expressions, InterpreterScope scope, IScriptInterpreterCallback callback = null)
        {
            int i = 0;
            int count = expressions.Count();

            foreach (var expression in expressions)
            {
                if (callback != null)
                {
                    if (callback.IsAborted)
                        return false;

                    int progress = (i * 100 / count);
                    if (progress > 0)
                        callback.UpdateProgress(progress, expression.Location.Start.Line);

                    i++;
                }

                if (!Evaluate(expression, scope))
                    return false;

                if (scope.IsComplete)
                    break;
            }

            return true;
        }

        private bool Evaluate(ExpressionBase expression, InterpreterScope scope)
        {
            switch (expression.Type)
            {
                case ExpressionType.Assignment:
                    Error = ((AssignmentExpression)expression).Evaluate(scope);
                    return (Error == null);

                case ExpressionType.FunctionCall:
                    return CallFunction((FunctionCallExpression)expression, scope);

                case ExpressionType.For:
                    return EvaluateLoop((ForExpression)expression, scope);

                case ExpressionType.If:
                    return EvaluateIf((IfExpression)expression, scope);

                case ExpressionType.Return:
                    return EvaluateReturn((ReturnExpression)expression, scope);

                case ExpressionType.Error:
                    Error = expression as ErrorExpression;
                    return false;

                case ExpressionType.FunctionDefinition:
                    return EvaluateFunctionDefinition((FunctionDefinitionExpression)expression, scope);

                case ExpressionType.Comment:
                    return true;

                default:
                    var executable = expression as IExecutableExpression;
                    if (executable != null)
                    {
                        var error = executable.Execute(scope);
                        if (error != null)
                        {
                            Error = error;
                            return false;
                        }

                        return true;
                    }

                    Error = new ErrorExpression("Only assignment statements, function calls and function definitions allowed at outer scope", expression);
                    return false;
            }
        }

        private bool EvaluateFunctionDefinition(FunctionDefinitionExpression expression, InterpreterScope scope)
        {
            scope.AddFunction(expression);
            return true;
        }

        private bool EvaluateReturn(ReturnExpression expression, InterpreterScope scope)
        {
            ExpressionBase result;
            var returnScope = new InterpreterScope(scope) { Context = new AssignmentExpression(new VariableExpression("@return"), expression.Value) };
            if (!expression.Value.ReplaceVariables(returnScope, out result))
            {
                Error = result as ErrorExpression;
                return false;
            }

            var functionCall = result as FunctionCallExpression;
            if (functionCall != null)
            {
                if (!CallFunction(functionCall, returnScope))
                    return false;

                scope.ReturnValue = returnScope.ReturnValue;
            }
            else
            {
                scope.ReturnValue = result;
            }

            scope.IsComplete = true;
            return true;
        }

        private bool EvaluateLoop(ForExpression forExpression, InterpreterScope scope)
        {
            ExpressionBase range;
            if (!forExpression.Range.ReplaceVariables(scope, out range))
            {
                Error = range as ErrorExpression;
                return false;
            }

            var iterableExpression = range as IIterableExpression;
            if (iterableExpression != null)
            {
                var iterator = forExpression.IteratorName;
                var iteratorScope = new InterpreterScope(scope);
                var iteratorVariable = new VariableExpression(iterator.Name);

                foreach (var entry in iterableExpression.IterableExpressions())
                {
                    iteratorScope.Context = new AssignmentExpression(iteratorVariable, entry);

                    ExpressionBase key;
                    if (!entry.ReplaceVariables(iteratorScope, out key))
                    {
                        Error = key as ErrorExpression;
                        return false;
                    }

                    var loopScope = new InterpreterScope(scope);
                    loopScope.DefineVariable(iterator, key);

                    if (!Evaluate(forExpression.Expressions, loopScope))
                        return false;

                    if (loopScope.IsComplete)
                    {
                        if (loopScope.ReturnValue != null)
                        {
                            scope.ReturnValue = loopScope.ReturnValue;
                            scope.IsComplete = true;
                        }
                        break;
                    }
                }

                return true;
            }

            Error = new ErrorExpression("Cannot iterate over " + forExpression.Range.ToString(), forExpression.Range);
            return false;
        }

        private bool EvaluateIf(IfExpression ifExpression, InterpreterScope scope)
        {
            ErrorExpression error;
            ExpressionBase value;
            bool? result = ifExpression.Condition.IsTrue(scope, out error);
            if (result == null)
            {
                if (!ifExpression.Condition.ReplaceVariables(scope, out value))
                {
                    Error = value as ErrorExpression;
                    return false;
                }

                result = ifExpression.Condition.IsTrue(scope, out error);
                if (result == null)
                {
                    if (ContainsRuntimeLogic(value))
                        Error = new ErrorExpression("Comparison contains runtime logic.", ifExpression.Condition);
                    else
                        Error = new ErrorExpression("Condition did not evaluate to a boolean.", ifExpression.Condition) { InnerError = error };

                    return false;
                }
            }

            return Evaluate(result.GetValueOrDefault() ? ifExpression.Expressions : ifExpression.ElseExpressions, scope);
        }

        private static bool ContainsRuntimeLogic(ExpressionBase expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.MemoryAccessor:
                case ExpressionType.ModifiedMemoryAccessor:
                case ExpressionType.MemoryValue:
                case ExpressionType.RequirementClause:
                    return true;

                default:
                    var nested = expression as INestedExpressions;
                    if (nested != null)
                    {
                        foreach (var nestedExpression in nested.NestedExpressions)
                        {
                            if (ContainsRuntimeLogic(nestedExpression))
                                return true;
                        }
                    }
                    return false;
            }
        }

        private bool CallFunction(FunctionCallExpression expression, InterpreterScope scope)
        {
            ExpressionBase result;
            bool success = expression.Invoke(scope, out result);
            if (!success)
            {
                if (scope.GetInterpreterContext<FunctionCallExpression>() != null)
                {
                    var error = result as ErrorExpression;
                    result = new ErrorExpression(expression.FunctionName.Name + " call failed: " + error.Message, expression.FunctionName) { InnerError = error };
                }

                Error = result as ErrorExpression;
                return false;
            }

            scope.ReturnValue = result;
            return true;
        }
    }
}
