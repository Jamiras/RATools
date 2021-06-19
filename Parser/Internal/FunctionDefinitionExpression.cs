using Jamiras.Components;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class FunctionDefinitionExpression : ExpressionBase, INestedExpressions
    {
        public FunctionDefinitionExpression(string name)
            : this(new VariableDefinitionExpression(name))
        {
        }

        protected FunctionDefinitionExpression(VariableDefinitionExpression name)
            : base(ExpressionType.FunctionDefinition)
        {
            Name = name;
            Parameters = new List<VariableDefinitionExpression>();
            Expressions = new List<ExpressionBase>();
            DefaultParameters = new TinyDictionary<string, ExpressionBase>();
        }

        /// <summary>
        /// Gets the name of the function.
        /// </summary>
        public VariableDefinitionExpression Name { get; private set; }

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
        /// Replaces the variables in the expression with values from <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns><c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result"/> will likely be a <see cref="ParseErrorExpression"/>.</returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            // FunctionDefinition.ReplaceVariables is called when evaluating a function for an assignment.
            // For user functions (see UserFunctionDefinition.ReplaceVariables) - it will just evaluate the
            // function call and return the result. Several internal functions have very special Evaluate
            // handling that should not be executed when defining variables. Those functions rely on this
            // behavior to just evaluate the parameters without calling Evaluate. There are some built-in
            // functions that should call Evaluate when ReplaceVariables is called. They will override
            // ReplaceVariables to do that.
            var parameters = new ExpressionBase[Parameters.Count];
            int i = 0;

            foreach (var parameterName in Parameters)
            {
                // do a direct lookup here. calling GetParameter will discard the VariableReference
                // and we want to preserve those for now.
                var parameter = scope.GetVariable(parameterName.Name);
                if (parameter == null)
                {
                    result = new ParseErrorExpression("No value provided for " + parameterName.Name + " parameter", parameterName);
                    return false;
                }

                parameters[i++] = parameter;
            }

            result = new FunctionCallExpression(Name.Name, parameters);
            CopyLocation(result);
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
            var interpreterScope = new InterpreterScope(scope) { Context = interpreter };

            if (!interpreter.Evaluate(Expressions, interpreterScope))
            {
                result = interpreter.Error;
                return false;
            }

            result = interpreterScope.ReturnValue;
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

            // if it's a variable reference, return the referenced object.
            if (parameter.Type == ExpressionType.VariableReference)
                return ((VariableReferenceExpression)parameter).Expression;

            // WARNING: variable references may still exist within a varargs object
            return parameter;
        }

        private ExpressionBase LocateParameter(InterpreterScope scope, string name)
        {
            var functionCall = scope.GetContext<FunctionCallExpression>();
            if (functionCall != null)
            {
                var nameEnumerator = Parameters.GetEnumerator();
                var valueEnumerator = functionCall.Parameters.GetEnumerator();
                while (nameEnumerator.MoveNext() && valueEnumerator.MoveNext())
                {
                    if (nameEnumerator.Current.Name == name)
                        return valueEnumerator.Current;
                }
            }

            return null;
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
                var originalParameter = LocateParameter(scope, name);
                if (originalParameter != null)
                    parameter = originalParameter;

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
                var originalParameter = LocateParameter(scope, name);
                if (originalParameter != null)
                    parameter = originalParameter;

                parseError = new ParseErrorExpression(name + " is not a string", parameter);
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        /// <summary>
        /// Gets the dictionary parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        protected DictionaryExpression GetDictionaryParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = GetParameter(scope, name, out parseError);
            if (parameter == null)
                return null;

            var typedParameter = parameter as DictionaryExpression;
            if (typedParameter == null)
            {
                var originalParameter = LocateParameter(scope, name);
                if (originalParameter != null)
                    parameter = originalParameter;

                parseError = new ParseErrorExpression(name + " is not a dictionary", parameter);
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        /// <summary>
        /// Gets the variable reference from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The variable reference, or <c>null</c> if an error occurred.</b></returns>
        protected VariableReferenceExpression GetReferenceParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = scope.GetVariable(name);
            if (parameter == null)
            {
                parseError = new ParseErrorExpression("No value provided for " + name + " parameter");
                return null;
            }

            var typedParameter = parameter as VariableReferenceExpression;
            if (typedParameter == null)
            {
                var originalParameter = LocateParameter(scope, name);
                if (originalParameter != null)
                    parameter = originalParameter;

                parseError = new ParseErrorExpression(name + " is not a reference", parameter);
                return null;
            }

            parseError = null;
            return typedParameter;
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
            var that = obj as FunctionDefinitionExpression;
            return that != null && Name == that.Name && ExpressionsEqual(Parameters, that.Parameters) && 
                ExpressionsEqual(Expressions, that.Expressions);
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (!Location.IsEmpty)
                    yield return new KeywordExpression("function", Location.Start.Line, Location.Start.Column);

                if (Name != null)
                    yield return Name;

                foreach (var parameter in Parameters)
                    yield return parameter;

                foreach (var expression in Expressions)
                    yield return expression;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            foreach (var expression in Expressions)
            {
                var nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }

            foreach (var parameter in Parameters)
                dependencies.Remove(parameter.Name);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
            modifies.Add(Name.Name);

            foreach (var expression in Expressions)
            {
                var nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetModifications(modifies);
            }

            foreach (var parameter in Parameters)
                modifies.Remove(parameter.Name);
        }
    }

    internal class UserFunctionDefinitionExpression : FunctionDefinitionExpression
    {
        private UserFunctionDefinitionExpression(VariableDefinitionExpression name)
            : base(name)
        {
        }

        internal static UserFunctionDefinitionExpression ParseForTest(string definition)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(definition));
            tokenizer.Match("function");
            return Parse(tokenizer) as UserFunctionDefinitionExpression;
        }

        /// <summary>
        /// Parses a function definition.
        /// </summary>
        /// <remarks>
        /// Assumes the 'function' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            var locationStart = new TextLocation(line, column);

            ExpressionBase.SkipWhitespace(tokenizer);

            line = tokenizer.Line;
            column = tokenizer.Column;

            var functionName = tokenizer.ReadIdentifier();
            var functionNameVariable = new VariableDefinitionExpression(functionName.ToString(), line, column);
            var function = new UserFunctionDefinitionExpression(functionNameVariable);

            if (functionName.IsEmpty)
                return ExpressionBase.ParseError(tokenizer, "Invalid function name");

            ExpressionBase.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != '(')
                return ExpressionBase.ParseError(tokenizer, "Expected '(' after function name", function.Name);
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
                        return ExpressionBase.ParseError(tokenizer, "Invalid parameter name", line, column);

                    var variableDefinition = new VariableDefinitionExpression(parameter.ToString(), line, column);
                    function.Parameters.Add(variableDefinition);

                    ExpressionBase.SkipWhitespace(tokenizer);

                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        ExpressionBase.SkipWhitespace(tokenizer);

                        var value = ExpressionBase.Parse(tokenizer);
                        if (value.Type == ExpressionType.ParseError)
                            return ExpressionBase.ParseError(tokenizer, "Invalid default value for " + parameter.ToString(), value);

                        var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
                        scope.Context = new TriggerBuilderContext(); // prevent errors passing memory references as default parameters

                        ExpressionBase evaluated;
                        if (!value.ReplaceVariables(scope, out evaluated))
                            return ExpressionBase.ParseError(tokenizer, "Default value for " + parameter.ToString() + " is not constant", evaluated);

                        function.DefaultParameters[parameter.ToString()] = evaluated;
                    }
                    else if (function.DefaultParameters.Count > 0)
                    {
                        return ExpressionBase.ParseError(tokenizer,
                            string.Format("Non-default parameter {0} appears after default parameters", parameter.ToString()), variableDefinition);
                    }

                    if (tokenizer.NextChar == ')')
                        break;

                    if (tokenizer.NextChar != ',')
                        return ExpressionBase.ParseError(tokenizer, "Expected ',' or ')' after parameter name, found: " + tokenizer.NextChar);

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
                function.Location = new TextRange(function.Location.Start, expression.Location.End);
                return function;
            }

            if (tokenizer.NextChar != '{')
                return ExpressionBase.ParseError(tokenizer, "Expected '{' after function declaration", function.Name);

            tokenizer.Advance();
            ExpressionBase.SkipWhitespace(tokenizer);

            bool seenReturn = false;
            while (tokenizer.NextChar != '}')
            {
                expression = ExpressionBase.Parse(tokenizer);
                if (expression.Type == ExpressionType.ParseError)
                {
                    // the ExpressionTokenizer will capture the error, we should still return the incomplete FunctionDefinition
                    if (tokenizer is ExpressionTokenizer)
                        break;

                    // not an ExpressionTokenizer, just return the error
                    return expression;
                }

                if (expression.Type == ExpressionType.Return)
                    seenReturn = true;
                else if (seenReturn)
                    ExpressionBase.ParseError(tokenizer, "Expression after return statement", expression);

                function.Expressions.Add(expression);

                ExpressionBase.SkipWhitespace(tokenizer);
            }

            function.Location = new TextRange(locationStart, tokenizer.Location);
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
            // user-defined functions should be evaluated (expanded) immediately.
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
    }

    internal class FunctionReferenceExpression : VariableExpressionBase
    {
        public FunctionReferenceExpression(string name)
            : base(name)
        {
        }

        public override string ToString()
        {
            return "FunctionReference: " + Name;
        }
    }
}
