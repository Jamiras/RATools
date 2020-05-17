﻿using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    public partial class AchievementScriptInterpreter
    {
        public AchievementScriptInterpreter()
        {
            _achievements = new List<Achievement>();
            _leaderboards = new List<Leaderboard>();
            _richPresence = new RichPresenceBuilder();
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
        public string RichPresence { get; private set; }

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
                _globalScope.AddFunction(new BitFunction());
                _globalScope.AddFunction(new BitCountFunction());

                _globalScope.AddFunction(new PrevPriorFunction("prev", FieldType.PreviousValue));
                _globalScope.AddFunction(new PrevPriorFunction("prior", FieldType.PriorValue));

                _globalScope.AddFunction(new OnceFunction());
                _globalScope.AddFunction(new RepeatedFunction());
                _globalScope.AddFunction(new FlagConditionFunction("never", RequirementType.ResetIf));
                _globalScope.AddFunction(new FlagConditionFunction("unless", RequirementType.PauseIf));
                _globalScope.AddFunction(new FlagConditionFunction("measured", RequirementType.Measured));

                _globalScope.AddFunction(new AchievementFunction());
                _globalScope.AddFunction(new LeaderboardFunction());

                _globalScope.AddFunction(new RichPresenceDisplayFunction());
                _globalScope.AddFunction(new RichPresenceConditionalDisplayFunction());
                _globalScope.AddFunction(new RichPresenceValueFunction());
                _globalScope.AddFunction(new RichPresenceLookupFunction());

                _globalScope.AddFunction(new AlwaysTrueFunction());
                _globalScope.AddFunction(new AlwaysFalseFunction());

                _globalScope.AddFunction(new RangeFunction());
                _globalScope.AddFunction(new FormatFunction());
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
                builder.AppendFormat("{0}:{1} {2}", Error.Line, Error.Column, Error.Message);
                var error = Error.InnerError;
                while (error != null)
                {
                    builder.AppendLine();
                    builder.AppendFormat("- {0}:{1} {2}", error.Line, error.Column, error.Message);
                    error = error.InnerError;
                }
                return builder.ToString();
            }
        }

        internal ParseErrorExpression Error { get; private set; }

        public string GetFormattedErrorMessage(Tokenizer tokenizer)
        {
            var neededLines = new List<int>();
            var error = Error;
            while (error != null)
            {
                for (int i = error.Line; i <= error.EndLine; i++)
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
                builder.AppendFormat("{0}:{1} {2}", error.Line, error.Column, error.Message);
                builder.AppendLine();
                //for (int i = error.Line; i <= error.EndLine; i++)
                int i = error.Line; // TODO: show all lines associated to error?
                {
                    var line = lineDictionary[error.Line];

                    builder.Append(":: ");
                    var startColumn = 0;
                    while (Char.IsWhiteSpace(line[startColumn]))
                        startColumn++;

                    if (i == error.Line)
                    {
                        builder.Append("{{color|#C0C0C0|");
                        builder.Append(line.Substring(startColumn, error.Column - startColumn - 1));
                        builder.Append("}}");
                        startColumn = error.Column - 1;
                    }

                    if (i == error.EndLine)
                    {
                        builder.Append(line.Substring(startColumn, error.EndColumn - startColumn));
                        builder.Append("{{color|#C0C0C0|");
                        builder.Append(line.Substring(error.EndColumn));
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
            var expressionGroup = new AchievementScriptParser().Parse(input);
            if (expressionGroup.Comments.Count > 0)
            {
                GameTitle = expressionGroup.Comments[0].Value.Substring(2).Trim();

                foreach (var comment in expressionGroup.Comments)
                {
                    if (comment.Value.Contains("#ID"))
                    {
                        ExtractGameId(new Token(comment.Value, 0, comment.Value.Length));
                        break;
                    }
                }
            }

            InterpreterScope scope;
            return Run(expressionGroup, out scope);
        }

        internal bool Run(ExpressionGroup expressionGroup, out InterpreterScope scope)
        { 
            var parseError = expressionGroup.Expressions.OfType<ParseErrorExpression>().FirstOrDefault();
            if (parseError != null)
            {
                Error = parseError;
                scope = null;
                return false;
            }

            scope = new InterpreterScope(GetGlobalScope());
            scope.Context = new AchievementScriptContext
            {
                Achievements = _achievements,
                Leaderboards = _leaderboards,
                RichPresence = _richPresence
            };

            if (!Evaluate(expressionGroup.Expressions, scope))
            {
                var error = Error;
                if (error != null)
                    expressionGroup.Errors.Add(error);

                return false;
            }

            if (!String.IsNullOrEmpty(_richPresence.DisplayString))
            {
                RichPresence = _richPresence.ToString();
                RichPresenceLine = _richPresence.Line;
            }

            return true;
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

        internal bool Evaluate(IEnumerable<ExpressionBase> expressions, InterpreterScope scope)
        {
            foreach (var expression in expressions)
            {
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
                    var assignment = (AssignmentExpression)expression;
                    var assignmentScope = new InterpreterScope(scope) { Context = assignment };

                    ExpressionBase result;
                    if (!assignment.Value.ReplaceVariables(assignmentScope, out result))
                    {
                        Error = result as ParseErrorExpression;
                        return false;
                    }

                    scope.AssignVariable(assignment.Variable, result);
                    return true;

                case ExpressionType.FunctionCall:
                    return CallFunction((FunctionCallExpression)expression, scope);

                case ExpressionType.For:
                    return EvaluateLoop((ForExpression)expression, scope);

                case ExpressionType.If:
                    return EvaluateIf((IfExpression)expression, scope);

                case ExpressionType.Return:
                    return EvaluateReturn((ReturnExpression)expression, scope);

                case ExpressionType.ParseError:
                    Error = expression as ParseErrorExpression;
                    return false;

                case ExpressionType.FunctionDefinition:
                    return EvaluateFunctionDefinition((FunctionDefinitionExpression)expression, scope);

                default:
                    Error = new ParseErrorExpression("Only assignment statements, function calls and function definitions allowed at outer scope", expression);
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
                Error = result as ParseErrorExpression;
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
                Error = range as ParseErrorExpression;
                return false;
            }

            var dict = range as DictionaryExpression;
            if (dict != null)
            {
                var iterator = forExpression.IteratorName;
                var iteratorScope = new InterpreterScope(scope);
                var iteratorVariable = new VariableExpression(iterator.Name);

                foreach (var entry in dict.Entries)
                {
                    iteratorScope.Context = new AssignmentExpression(iteratorVariable, entry.Key);

                    ExpressionBase key;
                    if (!entry.Key.ReplaceVariables(iteratorScope, out key))
                    {
                        Error = key as ParseErrorExpression;
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

            var array = range as ArrayExpression;
            if (array != null)
            {
                var iterator = forExpression.IteratorName;
                var iteratorScope = new InterpreterScope(scope);
                var iteratorVariable = new VariableExpression(iterator.Name);

                foreach (var entry in array.Entries)
                {
                    iteratorScope.Context = new AssignmentExpression(iteratorVariable, entry);

                    ExpressionBase key;
                    if (!entry.ReplaceVariables(iteratorScope, out key))
                    {
                        Error = key as ParseErrorExpression;
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

            Error = new ParseErrorExpression("Cannot iterate over " + forExpression.Range.ToString(), forExpression.Range);
            return false;
        }

        private bool EvaluateIf(IfExpression ifExpression, InterpreterScope scope)
        {
            ParseErrorExpression error;
            bool result = ifExpression.Condition.IsTrue(scope, out error);
            if (error != null)
            {
                Error = error;
                return false;
            }

            return Evaluate(result ? ifExpression.Expressions : ifExpression.ElseExpressions, scope);
        }

        private bool CallFunction(FunctionCallExpression expression, InterpreterScope scope)
        {
            ExpressionBase result;
            bool success = (scope.GetInterpreterContext<AssignmentExpression>() != null) ? expression.ReplaceVariables(scope, out result) : expression.Invoke(scope, out result);
            if (!success)
            {
                if (scope.GetInterpreterContext<FunctionCallExpression>() != null)
                {
                    var error = result as ParseErrorExpression;
                    result = new ParseErrorExpression(expression.FunctionName.Name + " call failed: " + error.Message, expression.FunctionName) { InnerError = error };
                }

                Error = result as ParseErrorExpression;
                return false;
            }

            scope.ReturnValue = result;
            return true;
        }
    }
}
