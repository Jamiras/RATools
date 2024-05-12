using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class FunctionDefinitionExpression : ExpressionBase, INestedExpressions, IExecutableExpression
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

            MakeReadOnly();
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

        public override bool IsConstant { get { return true; } }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
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
        /// <returns><c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result"/> will likely be a <see cref="ErrorExpression"/>.</returns>
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
                    result = new ErrorExpression("No value provided for " + parameterName.Name + " parameter", parameterName);
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
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
        /// </returns>
        public virtual bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var interpreter = new AchievementScriptInterpreter();
            var interpreterScope = new InterpreterScope(scope) { Context = interpreter };

            result = AchievementScriptInterpreter.Execute(Expressions, interpreterScope);
            if (result != null)
                return false;

            result = interpreterScope.ReturnValue;
            return true;
        }

        /// <summary>
        /// Invokes the function.
        /// </summary>
        /// <param name="scope">The scope object containing variable values and function parameters.</param>
        /// <param name="result">[out] <c>null</c> if successful, or an <see cref="ErrorExpression"/>.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
        /// </returns>
        public virtual bool Invoke(InterpreterScope scope, out ExpressionBase result)
        {
            if (!Evaluate(scope, out result))
                return false;

            result = null;
            return true;
        }

        protected ErrorExpression InvalidParameter(ExpressionBase parameter,
            InterpreterScope scope, string name, string expectedType)
        {
            var originalParameter = LocateParameter(scope, name) ?? parameter;
            return new ConversionErrorExpression(parameter, expectedType, originalParameter.Location, name);
        }

        protected ErrorExpression InvalidParameter(ExpressionBase parameter,
            InterpreterScope scope, string name, ExpressionType expectedType)
        {
            var originalParameter = LocateParameter(scope, name) ?? parameter;
            return new ConversionErrorExpression(parameter, expectedType, originalParameter.Location, name);
        }

        /// <summary>
        /// Gets a parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        protected static ExpressionBase GetParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = scope.GetVariable(name);
            if (parameter == null)
            {
                parseError = new ErrorExpression("No value provided for " + name + " parameter");
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
                foreach (var assignment in functionCall.Parameters.OfType<AssignmentExpression>())
                {
                    if (assignment.Variable.Name == name)
                        return assignment.Value;
                }

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
                parseError = InvalidParameter(parameter, scope, name, ExpressionType.IntegerConstant);
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
                parseError = InvalidParameter(parameter, scope, name, ExpressionType.StringConstant);
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        /// <summary>
        /// Gets the boolean parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        protected BooleanConstantExpression GetBooleanParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = GetParameter(scope, name, out parseError);
            if (parameter == null)
                return null;

            var typedParameter = parameter as BooleanConstantExpression;
            if (typedParameter == null)
            {
                parseError = InvalidParameter(parameter, scope, name, ExpressionType.BooleanConstant);
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
                parseError = InvalidParameter(parameter, scope, name, ExpressionType.Dictionary);
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        /// <summary>
        /// Gets the array parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        protected ArrayExpression GetArrayParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = GetParameter(scope, name, out parseError);
            if (parameter == null)
                return null;

            var typedParameter = parameter as ArrayExpression;
            if (typedParameter == null)
            {
                parseError = InvalidParameter(parameter, scope, name, ExpressionType.Array);
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        /// <summary>
        /// Gets the memory address parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        internal MemoryAccessorExpression GetMemoryAddressParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = GetParameter(scope, name, out parseError);
            if (parameter == null)
                return null;

            parseError = null;

            var integerParameter = parameter as IntegerConstantExpression;
            if (integerParameter != null)
            {
                var memoryAccessor = new MemoryAccessorExpression(FieldType.MemoryAddress, FieldSize.DWord, (uint)integerParameter.Value);
                parameter.CopyLocation(memoryAccessor);
                return memoryAccessor;
            }

            var accessorParameter = parameter as MemoryAccessorExpression;
            if (accessorParameter != null)
            {
                var memoryAccessor = new MemoryAccessorExpression(FieldType.MemoryAddress, FieldSize.DWord, 0U);
                foreach (var pointer in memoryAccessor.PointerChain)
                    memoryAccessor.AddPointer(pointer);
                memoryAccessor.AddPointer(new Requirement { Type = RequirementType.AddAddress, Left = accessorParameter.Field });
                parameter.CopyLocation(memoryAccessor);
                return memoryAccessor;
            }

            var memoryValueParameter = parameter as MemoryValueExpression;
            if (memoryValueParameter != null && memoryValueParameter.MemoryAccessors.Count() == 1)
            {
                var pointerBase = memoryValueParameter.MemoryAccessors.First();
                if (pointerBase.ModifyingOperator == RequirementOperator.None)
                {
                    var memoryAccessor = new MemoryAccessorExpression(FieldType.MemoryAddress, FieldSize.DWord, (uint)memoryValueParameter.IntegerConstant);
                    foreach (var pointer in pointerBase.MemoryAccessor.PointerChain)
                        memoryAccessor.AddPointer(pointer);
                    memoryAccessor.AddPointer(new Requirement { Type = RequirementType.AddAddress, Left = pointerBase.MemoryAccessor.Field });
                    parameter.CopyLocation(memoryAccessor);
                    return memoryAccessor;
                }
            }

            parseError = InvalidParameter(parameter, scope, name, "memory address");
            return null;
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
                parseError = new ErrorExpression("No value provided for " + name + " parameter");
                return null;
            }

            var typedParameter = parameter as VariableReferenceExpression;
            if (typedParameter == null)
            {
                parseError = InvalidParameter(parameter, scope, name, ExpressionType.VariableReference);
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        protected FunctionDefinitionExpression GetFunctionParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = scope.GetVariable(name);
            if (parameter == null)
            {
                parseError = new ErrorExpression("No value provided for " + name + " parameter");
                return null;
            }

            var functionDefinition = parameter as FunctionDefinitionExpression;
            if (functionDefinition == null)
            {
                var functionReference = parameter as FunctionReferenceExpression;
                if (functionReference == null)
                {
                    parseError = InvalidParameter(parameter, scope, name, "function reference");
                    return null;
                }

                functionDefinition = scope.GetFunction(functionReference.Name);
                if (functionDefinition == null)
                {
                    parseError = new ErrorExpression("Undefined function: " + functionReference.Name);
                    return null;
                }
            }

            parseError = null;
            return functionDefinition;
        }

        /// <summary>
        /// Gets the requirement parameter from the <paramref name="scope"/> or <see cref="DefaultParameters"/> collections.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="parseError">[out] The error that occurred.</param>
        /// <returns>The parameter value, or <c>null</c> if an error occurred.</b></returns>
        internal RequirementExpressionBase GetRequirementParameter(InterpreterScope scope, string name, out ExpressionBase parseError)
        {
            var parameter = GetParameter(scope, name, out parseError);
            if (parameter == null)
                return null;

            var typedParameter = parameter as RequirementExpressionBase;
            if (typedParameter == null)
            {
                var invalidClause = parameter;

                var originalParameter = LocateParameter(scope, name);
                if (originalParameter != null && !ReferenceEquals(originalParameter, parameter))
                {
                    var conditional = parameter as ConditionalExpression;
                    if (conditional != null)
                    {
                        invalidClause = conditional.Conditions.FirstOrDefault(c => c is not RequirementExpressionBase) ?? parameter;
                    }
                    else
                    {
                        var mathematic = parameter as MathematicExpression;
                        if (mathematic != null)
                            invalidClause = mathematic.FindFirstSubclause<MathematicExpression>();
                    }

                    // FunctionCallExpression.ReplaceVariables will call Evaluate and then
                    // replace the location of the result to the function call's location.
                    // Call Evaluate directly to get the original location.
                    if (invalidClause.Location.Start == originalParameter.Location.Start &&
                        invalidClause.Location.End == originalParameter.Location.End)
                    {
                        var functionCall = originalParameter as FunctionCallExpression;
                        if (functionCall != null)
                        {
                            ExpressionBase result;
                            if (functionCall.Evaluate(scope, out result) && result == parameter)
                                invalidClause = result;
                        }
                    }
                }

                var error = InvalidParameter(parameter, scope, name, ExpressionType.Requirement);

                // FunctionCall evaluation may return the same expression as parameter, but not the same instance.
                // Look for a location match instead of calling the more expensive equality check.
                if (invalidClause.Type != parameter.Type ||
                    invalidClause.Location.Start != parameter.Location.Start ||
                    invalidClause.Location.End != parameter.Location.End)
                {
                    error.InnerError = new ConversionErrorExpression(invalidClause, ExpressionType.Requirement);
                }

                parseError = error;
                return null;
            }

            parseError = null;
            return typedParameter;
        }

        protected ArrayExpression GetVarArgsParameter(InterpreterScope scope, out ExpressionBase parseError, ExpressionBase lastExpression, bool expandArray = false)
        {
            var varargs = GetParameter(scope, "varargs", out parseError) as ArrayExpression;
            if (varargs == null)
            {
                if (!(parseError is ErrorExpression))
                {
                    if (lastExpression != null)
                        parseError = new ErrorExpression("unexpected varargs", lastExpression);
                    else
                        parseError = new ErrorExpression("unexpected varargs");
                }
                return null;
            }

            // special case - if there's a single array parameter, treat it as a list of parameters
            if (varargs.Entries.Count == 1 && expandArray)
            {
                var arrayExpression = varargs.Entries[0] as ArrayExpression;
                if (arrayExpression == null)
                {
                    var referenceExpression = varargs.Entries[0] as VariableReferenceExpression;
                    if (referenceExpression != null)
                        arrayExpression = referenceExpression.Expression as ArrayExpression;
                }
                if (arrayExpression != null)
                    varargs = arrayExpression;
            }

            return varargs;
        }

        /// <summary>
        /// Creates an <see cref="InterpreterScope"/> with all variables required for the function call.
        /// </summary>
        public InterpreterScope CreateCaptureScope(InterpreterScope scope)
        {
            var captureScope = new InterpreterScope(scope);
            captureScope.Context = this;

            // only have to capture variables for anonymous functions
            var userFunctionDefinition = this as AnonymousUserFunctionDefinitionExpression;
            if (userFunctionDefinition != null)
            {
                foreach (var captured in userFunctionDefinition.CapturedVariables)
                    captureScope.DefineVariable(captured.Variable, captured.Expression);
            }

            // set the context to the function definition and return the new context
            return captureScope;
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

        public ErrorExpression Execute(InterpreterScope scope)
        {
            scope.AddFunction(this);
            return null;
        }
    }

    internal class UserFunctionDefinitionExpression : FunctionDefinitionExpression, IValueExpression
    {
        protected UserFunctionDefinitionExpression(VariableDefinitionExpression name)
            : base(name)
        {
        }

        internal static UserFunctionDefinitionExpression ParseForTest(string definition)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(definition));
            tokenizer.Match("function");
            return Parse(tokenizer, 0, 0) as UserFunctionDefinitionExpression;
        }

        /// <summary>
        /// Parses a function definition.
        /// </summary>
        /// <remarks>
        /// Assumes the 'function' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            var locationStart = new TextLocation(line, column); // location of 'function' keyword

            SkipWhitespace(tokenizer);

            line = tokenizer.Line;
            column = tokenizer.Column;

            var functionName = tokenizer.ReadIdentifier();
            var functionNameVariable = new VariableDefinitionExpression(functionName.ToString(), line, column);
            var function = new UserFunctionDefinitionExpression(functionNameVariable);
            function.Location = new TextRange(locationStart.Line, locationStart.Column, 0, 0);

            if (functionName.IsEmpty)
                return ParseError(tokenizer, "Invalid function name");

            return function.Parse(tokenizer);
        }

        protected new ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != '(')
                return ParseError(tokenizer, "Expected '(' after function name", Name);
            tokenizer.Advance();

            ErrorExpression nonConstantDefaultParameterError = null;

            SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != ')')
            {
                do
                {
                    var line = tokenizer.Line;
                    var column = tokenizer.Column;

                    var parameter = tokenizer.ReadIdentifier();
                    if (parameter.IsEmpty)
                        return ParseError(tokenizer, "Invalid parameter name", line, column);

                    var variableDefinition = new VariableDefinitionExpression(parameter.ToString(), line, column);
                    Parameters.Add(variableDefinition);

                    SkipWhitespace(tokenizer);

                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        SkipWhitespace(tokenizer);

                        var value = ExpressionBase.Parse(tokenizer);
                        if (value.Type == ExpressionType.Error)
                            return ParseError(tokenizer, "Invalid default value for " + parameter.ToString(), value);

                        var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
                        scope.Context = new TriggerBuilderContext(); // prevent errors passing memory references as default parameters

                        ExpressionBase evaluated;
                        if (!value.ReplaceVariables(scope, out evaluated))
                        {
                            // a variable reference could be a constant hiding a magic number or a dictionary.
                            // as long as it's not referencing another parameter, let it through (for now).
                            var variable = value as VariableExpression;
                            if (variable != null && !Parameters.Any(p => p.Name == variable.Name))
                                evaluated = value;
                            else if (nonConstantDefaultParameterError == null)
                                nonConstantDefaultParameterError = ParseError(tokenizer, "Default value for " + parameter.ToString() + " is not constant", value);
                        }

                        DefaultParameters[parameter.ToString()] = evaluated;
                    }
                    else if (DefaultParameters.Count > 0)
                    {
                        return ParseError(tokenizer,
                            string.Format("Non-default parameter {0} appears after default parameters", parameter.ToString()), variableDefinition);
                    }

                    if (tokenizer.NextChar == ')')
                        break;

                    if (tokenizer.NextChar != ',')
                        return ParseError(tokenizer, "Expected ',' or ')' after parameter name, found: " + tokenizer.NextChar);

                    tokenizer.Advance();
                    SkipWhitespace(tokenizer);
                } while (true);
            }

            tokenizer.Advance(); // closing parenthesis
            SkipWhitespace(tokenizer);

            if (nonConstantDefaultParameterError != null)
                return nonConstantDefaultParameterError;

            ExpressionBase expression;

            if (tokenizer.Match("=>"))
                return ParseShorthandBody(tokenizer);

            if (tokenizer.NextChar != '{')
                return ParseError(tokenizer, "Expected '{' after function declaration", Name);

            tokenizer.Advance();
            SkipWhitespace(tokenizer);

            bool seenReturn = false;
            while (tokenizer.NextChar != '}')
            {
                expression = ExpressionBase.Parse(tokenizer);
                if (expression.Type == ExpressionType.Error)
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
                    ParseError(tokenizer, "Expression after return statement", expression);

                Expressions.Add(expression);

                SkipWhitespace(tokenizer);
            }

            Location = new TextRange(Location.Start, tokenizer.Location);
            tokenizer.Advance();
            return MakeReadOnly();
        }

        protected ExpressionBase ParseShorthandBody(PositionalTokenizer tokenizer)
        {
            SkipWhitespace(tokenizer);

            var expression = ExpressionBase.Parse(tokenizer);
            if (expression.Type == ExpressionType.Error)
                return expression;

            switch (expression.Type)
            {
                case ExpressionType.Return:
                    return ParseError(tokenizer, "Return statement is implied by =>", ((ReturnExpression)expression).Keyword);

                case ExpressionType.For:
                    return ParseError(tokenizer, "Shorthand function definition does not support loops.", expression);

                case ExpressionType.If:
                    return ParseError(tokenizer, "Shorthand function definition does not support branches.", expression);
            }

            var returnExpression = new ReturnExpression(expression);
            Expressions.Add(returnExpression);
            Location = new TextRange(Location.Start, expression.Location.End);
            return MakeReadOnly();
        }

        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            return this;
        }

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns><c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result"/> will likely be a <see cref="ErrorExpression"/>.</returns>
        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            // user-defined functions should be evaluated (expanded) immediately.
            if (!base.Evaluate(scope, out result))
                return false;

            if (result == null)
            {
                // don't return "Anonymous@5,12 did not return a value", just return null
                // and let the caller handle it.
                if (this is AnonymousUserFunctionDefinitionExpression)
                    return true;

                var functionCall = scope.GetContext<FunctionCallExpression>();
                if (functionCall != null)
                    result = new ErrorExpression(Name.Name + " did not return a value", functionCall.FunctionName);
                else
                    result = new ErrorExpression(Name.Name + " did not return a value");

                return false;
            }

            return true;
        }

        public override bool Invoke(InterpreterScope scope, out ExpressionBase result)
        {
            if (!base.Evaluate(scope, out result))
                return false;

            result = null;
            return true;
        }
    }

    internal class AnonymousUserFunctionDefinitionExpression : UserFunctionDefinitionExpression
    {
        protected AnonymousUserFunctionDefinitionExpression(VariableDefinitionExpression name)
            : base(name)
        {
            CapturedVariables = Enumerable.Empty<VariableReferenceExpression>();
        }

        /// <summary>
        /// Determines if the tokenizer is pointing at a parameter list for an anonymous function.
        /// </summary>
        internal static bool IsAnonymousParameterList(PositionalTokenizer tokenizer)
        {
            var result = false;

            tokenizer.PushState();

            if (tokenizer.Match("("))
            {
                tokenizer.SkipWhitespace();

                do
                {
                    tokenizer.ReadIdentifier();
                    tokenizer.SkipWhitespace();

                    if (tokenizer.NextChar != ',')
                        break;

                    tokenizer.Advance();
                    tokenizer.SkipWhitespace();
                } while (true);

                if (tokenizer.Match(")"))
                {
                    tokenizer.SkipWhitespace();
                    result = tokenizer.NextChar == '{' || tokenizer.Match("=>");
                }
            }

            tokenizer.PopState();

            return result;
        }

        private static VariableDefinitionExpression CreateAnonymousFunctionName(int line, int column)
        {
            return new VariableDefinitionExpression(string.Format("AnonymousFunction@{0},{1}", line, column));
        }

        public static bool IsAnonymousFunctionName(string functionName)
        {
            return functionName.StartsWith("AnonymousFunction@");
        }

        /// <summary>
        /// Parses an anonymous function definition in the format "(a) => body" or "(a) { body }"
        /// </summary>
        public static ExpressionBase ParseAnonymous(PositionalTokenizer tokenizer)
        {
            var name = CreateAnonymousFunctionName(tokenizer.Line, tokenizer.Column);
            var function = new AnonymousUserFunctionDefinitionExpression(name);
            function.Location = new TextRange(tokenizer.Line, tokenizer.Column, 0, 0);
            return function.Parse(tokenizer);
        }

        /// <summary>
        /// Parses an anonymous function definition in the format "a => body" where <paramref name="parameter"/> 
        /// is "a" and <paramref name="tokenizer"/> is pointing at "body".
        /// </summary>
        public static ExpressionBase ParseAnonymous(PositionalTokenizer tokenizer, ExpressionBase parameter)
        {
            var variable = parameter as VariableExpression;
            if (variable == null)
                return new ErrorExpression("Cannot create anonymous function from " + parameter.Type);

            var name = CreateAnonymousFunctionName(parameter.Location.Start.Line, parameter.Location.Start.Column);
            var function = new AnonymousUserFunctionDefinitionExpression(name);
            function.Location = parameter.Location;
            function.Parameters.Add(new VariableDefinitionExpression(variable.Name, variable.Location.Start.Line, variable.Location.Start.Column));
            return function.ParseShorthandBody(tokenizer);
        }

        public IEnumerable<VariableReferenceExpression> CapturedVariables { get; private set; }

        public void IdentifyCaptureVariables(InterpreterScope scope)
        {
            // Initialize a new scope object with a FunctionCall context so we can determine which
            // variables have to be captured. The FunctionCall context will only see the globals.
            var captureScope = new InterpreterScope(scope);
            captureScope.Context = new FunctionCallExpression("NonAnonymousFunction", new ExpressionBase[0]);

            var capturedVariables = new List<VariableReferenceExpression>();

            var possibleDependencies = new HashSet<string>();
            ((INestedExpressions)this).GetDependencies(possibleDependencies);
            foreach (var dependency in possibleDependencies)
            {
                if (captureScope.GetVariable(dependency) == null)
                {
                    // the variable is not visible to the function scope. check to see if it's visible
                    // in the calling scope. if it is, create a copy for the function call.
                    var variable = scope.GetVariableReference(dependency);
                    if (variable != null)
                        capturedVariables.Add(variable);
                }
            }

            if (capturedVariables.Count > 0)
                CapturedVariables = capturedVariables.ToArray();
        }
    }

    internal class FunctionReferenceExpression : VariableExpressionBase, IValueExpression
    {
        public FunctionReferenceExpression(string name)
            : base(name)
        {
        }

        public override string ToString()
        {
            return "FunctionReference: " + Name;
        }

        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            return this;
        }
    }
}
