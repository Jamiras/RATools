using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Functions;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class FunctionDefinitionExpression : ExpressionBase, INestedExpressions
    {
        public FunctionDefinitionExpression(string name)
            : this()
        {
            Name = new VariableDefinitionExpression(name);
        }

        private FunctionDefinitionExpression()
            : base(ExpressionType.FunctionDefinition)
        {
            Parameters = new List<VariableDefinitionExpression>();
            Expressions = new List<ExpressionBase>();
            DefaultParameters = new TinyDictionary<string, ExpressionBase>();
        }

        /// <summary>
        /// Gets the name of the function.
        /// </summary>
        public VariableDefinitionExpression Name { get; private set; }

        private KeywordExpression _keyword;

        /// <summary>
        /// Gets the names of the parameters.
        /// </summary>
        public ICollection<VariableDefinitionExpression> Parameters { get; private set; }

        /// <summary>
        /// Gets default values for the parameters.
        /// </summary>
        public IDictionary<string, ExpressionBase> DefaultParameters { get; private set; }

        /// <summary>
        /// Gets the expressions for the contents of the function.
        /// </summary>
        public ICollection<ExpressionBase> Expressions { get; private set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder);
            return builder.ToString();
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("function ");
            Name.AppendString(builder);
            builder.Append('(');

            if (Parameters.Count > 0)
            {
                foreach (var parameter in Parameters)
                {
                    parameter.AppendString(builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            builder.Append(')');
        }

        /// <summary>
        /// Parses a function definition.
        /// </summary>
        /// <remarks>
        /// Assumes the 'function' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            var function = new FunctionDefinitionExpression();
            function._keyword = new KeywordExpression("function", line, column);

            ExpressionBase.SkipWhitespace(tokenizer);

            line = tokenizer.Line;
            column = tokenizer.Column;

            var functionName = tokenizer.ReadIdentifier();
            function.Name = new VariableDefinitionExpression(functionName.ToString(), line, column);
            if (functionName.IsEmpty)
            {
                ExpressionBase.ParseError(tokenizer, "Invalid function name");
                return function;
            }

            ExpressionBase.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != '(')
            {
                ExpressionBase.ParseError(tokenizer, "Expected '(' after function name", function.Name);
                return function;
            }
            tokenizer.Advance();

            ExpressionBase.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != ')')
            {
                do
                {
                    line = tokenizer.Line;
                    column = tokenizer.Column;

                    var parameter = tokenizer.ReadIdentifier();
                    if (parameter.IsEmpty)
                    {
                        ExpressionBase.ParseError(tokenizer, "Invalid parameter name", line, column);
                        return function;
                    }

                    function.Parameters.Add(new VariableDefinitionExpression(parameter.ToString(), line, column));

                    ExpressionBase.SkipWhitespace(tokenizer);
                    if (tokenizer.NextChar == ')')
                        break;

                    if (tokenizer.NextChar != ',')
                    {
                        ExpressionBase.ParseError(tokenizer, "Expected ',' or ')' after parameter name, found: " + tokenizer.NextChar);
                        return function;
                    }

                    tokenizer.Advance();
                    ExpressionBase.SkipWhitespace(tokenizer);
                } while (true);
            }

            tokenizer.Advance(); // closing parenthesis
            ExpressionBase.SkipWhitespace(tokenizer);

            ExpressionBase expression;

            if (tokenizer.Match("=>"))
            {
                ExpressionBase.SkipWhitespace(tokenizer);

                expression = ExpressionBase.Parse(tokenizer);
                if (expression.Type == ExpressionType.ParseError)
                    return expression;

                if (expression.Type == ExpressionType.Return)
                    return new ParseErrorExpression("Return statement is implied by =>", ((ReturnExpression)expression).Keyword);

                var returnExpression = new ReturnExpression(expression);
                function.Expressions.Add(returnExpression);
                return function;
            }

            if (tokenizer.NextChar != '{')
            {
                ExpressionBase.ParseError(tokenizer, "Expected '{' after function declaration", function.Name);
                return function;
            }

            line = tokenizer.Line;
            column = tokenizer.Column;
            tokenizer.Advance();
            ExpressionBase.SkipWhitespace(tokenizer);

            bool seenReturn = false;
            while (tokenizer.NextChar != '}')
            {
                expression = ExpressionBase.Parse(tokenizer);
                if (expression.Type == ExpressionType.ParseError)
                    return expression;

                if (expression.Type == ExpressionType.Return)
                    seenReturn = true;
                else if (seenReturn)
                    ExpressionBase.ParseError(tokenizer, "Expression after return statement", expression);

                function.Expressions.Add(expression);

                ExpressionBase.SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();
            return function;
        }

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns><c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result"/> will likely be a <see cref="ParseErrorExpression"/>.</returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            scope = new InterpreterScope(scope) { IsReplacingVariables = true };
            if (!Evaluate(scope, out result))
                return false;

            if (result == null)
            {
                var functionCall = scope.GetContext<FunctionCallExpression>();
                if (functionCall != null)
                    result = new ParseErrorExpression(Name.Name + " did not return a value", functionCall.FunctionName);
                else
                    result = new ParseErrorExpression(Name.Name + " did not return a value");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the return value from calling a function.
        /// </summary>
        /// <param name="scope">The scope object containing variable values and function parameters.</param>
        /// <param name="result">[out] The new expression containing the function result.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public virtual bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var interpreter = new AchievementScriptInterpreter();
            if (!interpreter.Evaluate(Expressions, scope))
            {
                result = interpreter.Error;
                return false;
            }

            result = scope.ReturnValue;
            return true;
        }

        /// <summary>
        /// Gets the  parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        protected ExpressionBase GetParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = scope.GetVariable(name);
            if (parameter == null)
            {
                parseError = new ParseErrorExpression("No value provided for " + name + " parameter");
                return null;
            }

            parseError = null;
            return parameter;
        }

        /// <summary>
        /// Gets the integer parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        protected IntegerConstantExpression GetIntegerParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = GetParameter(scope, name, out parseError);
            if (parameter == null)
                return null;

            var typedParameter = parameter as IntegerConstantExpression;
            if (typedParameter == null)
            {
                parseError = new ParseErrorExpression(name + " is not an integer", parameter);
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        /// <summary>
        /// Gets the string parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        protected StringConstantExpression GetStringParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = GetParameter(scope, name, out parseError);
            if (parameter == null)
                return null;

            var typedParameter = parameter as StringConstantExpression;
            if (typedParameter == null)
            {
                parseError = new ParseErrorExpression(name + " is not a string", parameter);
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        /// <summary>
        /// Gets the memory accessor parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        protected FunctionCallExpression GetMemoryAccessorParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = GetParameter(scope, name, out parseError);
            if (parameter == null)
                return null;

            var functionCall = parameter as FunctionCallExpression;
            if (functionCall == null)
            {
                parseError = new ParseErrorExpression(name + " did not evaluate to a memory accessor", parameter);
                return null;
            }

            var functionDefinition = scope.GetFunction(functionCall.FunctionName.Name);
            var memoryAccessor = functionDefinition as MemoryAccessorFunction;
            if (memoryAccessor == null)
            {
                parseError = new ParseErrorExpression(name + " did not evaluate to a memory accessor", parameter);
                return null;
            }

            ExpressionBase result;
            if (!functionCall.ReplaceVariables(scope, out result))
            {
                parseError = (ParseErrorExpression)result;
                return null;
            }

            parseError = null;
            return (FunctionCallExpression)result;
        }

        /// <summary>
        /// Determines whether the specified <see cref="FunctionDefinitionExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="FunctionDefinitionExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="FunctionDefinitionExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (FunctionDefinitionExpression)obj;
            return Name == that.Name && Parameters == that.Parameters && Expressions == that.Expressions;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            if (_keyword != null && _keyword.Line == line)
                expressions.Add(_keyword);
            if (Name.Line == line)
                expressions.Add(Name);

            foreach (var parameter in Parameters)
            {
                if (parameter.Line == line)
                    expressions.Add(parameter);
            }

            ExpressionGroup.GetExpressionsForLine(expressions, Expressions, line);

            return true;
        }
    }
}
