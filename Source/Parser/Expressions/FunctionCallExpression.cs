using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class FunctionCallExpression : ExpressionBase, INestedExpressions,
        IExecutableExpression, IValueExpression
    {
        public FunctionCallExpression(string functionName, ICollection<ExpressionBase> parameters)
            : this(new FunctionNameExpression(functionName), parameters)
        {
        }

        public FunctionCallExpression(FunctionNameExpression functionName, ICollection<ExpressionBase> parameters)
            : base(ExpressionType.FunctionCall)
        {
            FunctionName = functionName;
            Parameters = parameters;

            Location = new Jamiras.Components.TextRange(functionName.Location.Start.Line, functionName.Location.Start.Column, 0, 0);
        }

        internal FunctionCallExpression(IValueExpression source, ICollection<ExpressionBase> parameters)
            : base(ExpressionType.FunctionCall)
        {
            _source = source;
            Parameters = parameters;

            var expression = (ExpressionBase)source;
            Location = new Jamiras.Components.TextRange(expression.Location.Start.Line, expression.Location.Start.Column, 0, 0);
        }

        private readonly IValueExpression _source;

        /// <summary>
        /// Gets the name of the function to call.
        /// </summary>
        public FunctionNameExpression FunctionName { get; private set; }

        /// <summary>
        /// Gets the parameters to pass to the function.
        /// </summary>
        public ICollection<ExpressionBase> Parameters { get; private set; }

        /// <summary>
        /// Gets whether this is non-changing.
        /// </summary>
        public override bool IsConstant
        {
            get { return _fullyExpanded; }
        }
        private bool _fullyExpanded = false;

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            if (FunctionName != null)
                FunctionName.AppendString(builder);
            else
                ((ExpressionBase)_source).AppendString(builder);

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
        /// Replaces the variables in the expression with values from <paramref name="scope" />.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!Evaluate(scope, false, out result))
                return false;

            if (result == null)
            {
                result = new ErrorExpression(FunctionName.Name + " did not return a value", FunctionName);
                return false;
            }

            CopyLocation(result);
            return true;
        }

        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            ExpressionBase result;
            if (!Evaluate(scope, false, out result))
                return result;

            if (result == null)
                return new ErrorExpression(FunctionName.Name + " did not return a value", FunctionName);

            CopyLocation(result);
            return result;
        }

        /// <summary>
        /// Gets the return value from calling a function.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the function result.</param>
        /// <returns>
        ///   <c>true</c> if invocation was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
        /// </returns>
        public bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, false, out result);
        }

        private bool Evaluate(InterpreterScope scope, bool isInvoking, out ExpressionBase result)
        {
            FunctionDefinitionExpression functionDefinition;
            if (FunctionName != null)
            {
                functionDefinition = scope.GetFunction(FunctionName.Name);

                if (functionDefinition == null)
                {
                    if (scope.GetVariable(FunctionName.Name) != null)
                        result = new UnknownVariableParseErrorExpression(FunctionName.Name + " is not a function", FunctionName);
                    else
                        result = new UnknownVariableParseErrorExpression("Unknown function: " + FunctionName.Name, FunctionName);

                    return false;
                }
            }
            else
            {
                result = _source.Evaluate(scope);
                if (result is ErrorExpression)
                    return false;

                var functionReference = result as FunctionReferenceExpression;
                if (functionReference != null)
                    functionDefinition = scope.GetFunction(functionReference.Name);
                else
                    functionDefinition = result as FunctionDefinitionExpression;

                if (functionDefinition == null)
                {
                    var sourceExpression = (ExpressionBase)_source;
                    var funcCall = sourceExpression as FunctionCallExpression;
                    if (funcCall != null)
                        result = new ErrorExpression(funcCall.FunctionName.Name + " did not return a function reference", sourceExpression);
                    else
                        result = new ErrorExpression(sourceExpression.ToString() + " is not a function reference", sourceExpression);
                    return false;
                }
            }

            var functionParametersScope = GetParameters(functionDefinition, scope, out result);
            if (functionParametersScope == null || result is ErrorExpression)
                return false;

            if (functionParametersScope.Depth >= 100)
            {
                result = new ErrorExpression("Maximum recursion depth exceeded", this);
                return false;
            }

            functionParametersScope.Context = this;
            if (isInvoking)
                functionDefinition.Invoke(functionParametersScope, out result);
            else
                functionDefinition.Evaluate(functionParametersScope, out result);

            var error = result as ErrorExpression;
            if (error != null)
            {
                if (error.Location.End.Line == 0)
                    CopyLocation(error);

                result = ErrorExpression.WrapError(error, FunctionName.Name + " call failed", FunctionName);
                return false;
            }

            if (result != null)
            {
                if (!result.IsReadOnly || result.Location.End.Line == 0)
                {
                    CopyLocation(result);
                }
                else if (!result.Location.RangeEquals(Location))
                {
                    var cloneable = result as ICloneableExpression;
                    if (cloneable != null)
                    {
                        result = cloneable.Clone();
                        CopyLocation(result);
                    }
                }
            }

            var functionCall = result as FunctionCallExpression;
            if (functionCall != null)
            {
                // if the result is a function call, check for any variable references. it can't be marked
                // as fully expanded if any variable references are present.
                if (!functionCall.Parameters.Any(p => p is VariableReferenceExpression))
                    functionCall._fullyExpanded = true;

                // if there was no change, also mark the source as fully expanded.
                if (result == this)
                    _fullyExpanded = true;

                // when expanding the parameters, a new functionCall object will be created without a name
                // location. if that has happened, replace the temporary name object with the real one.
                if (functionCall.FunctionName.Location.Start.Line == 0 && functionCall.FunctionName.Name == FunctionName.Name)
                    functionCall.FunctionName = FunctionName;
            }

            return true;
        }

        /// <summary>
        /// Gets the return value from calling a function.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] <c>null</c> if successful, or an <see cref="ErrorExpression"/>.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
        /// </returns>
        public bool Invoke(InterpreterScope scope, out ExpressionBase result)
        {
            if (Evaluate(scope, true, out result))
            {
                result = null;
                return true;
            }

            if (result.Location.Start.Line == 0)
                result = new ErrorExpression(result, FunctionName);

            return false;
        }

        private static ExpressionBase GetParameter(InterpreterScope parameterScope, InterpreterScope scope, AssignmentExpression assignment)
        {
            ExpressionBase value = assignment.Value;

            if (value.IsConstant)
            {
                // already a basic type, do nothing
            }
            else
            {
                var valueExpression = value as IValueExpression;
                if (valueExpression == null)
                    return new ErrorExpression("Cannot assign " + value.Type.ToLowerString() + " to parameter", value);

                bool isLogicalUnit = value.IsLogicalUnit;
                value = valueExpression.Evaluate(scope);

                var error = value as ErrorExpression;
                if (error != null)
                    return new ErrorExpression("Invalid value for parameter: " + assignment.Variable.Name, assignment.Value) { InnerError = error };

                if (!value.IsReadOnly && !VariableReferenceExpression.CanReference(value.Type))
                {
                    value.IsLogicalUnit = isLogicalUnit;
                    assignment.Value.CopyLocation(value);
                    value.MakeReadOnly();
                }
            }

            var anonymousFunction = value as AnonymousUserFunctionDefinitionExpression;
            if (anonymousFunction != null)
            {
                // a function normally can only see its variables and global variables. an
                // anonymous function may also see variables in the current function scope.
                // identify any local variables the anonymous function needs so we can copy
                // them to the function call scope when the function is invoked.
                anonymousFunction.IdentifyCaptureVariables(parameterScope);
                parameterScope.AddFunction(anonymousFunction);
            }

            return value;
        }

        private bool GetSingleParameter(FunctionDefinitionExpression function, 
            InterpreterScope parameterScope, InterpreterScope initializationScope, out ExpressionBase error)
        {
            var funcParameter = function.Parameters.First();

            ExpressionBase value = Parameters.First();
            if (value.IsConstant && value is not AnonymousUserFunctionDefinitionExpression)
            {
                // already a basic type, just proceed to storing it
                error = null;
            }
            else
            {
                var assignedParameter = value as AssignmentExpression;
                if (assignedParameter == null)
                {
                    assignedParameter = new AssignmentExpression(new VariableExpression(funcParameter.Name), value);
                }
                else if (funcParameter.Name != assignedParameter.Variable.Name)
                {
                    error = new ErrorExpression(string.Format("'{0}' does not have a '{1}' parameter", function.Name.Name, assignedParameter.Variable.Name), value);
                    return true;
                }

                value = GetParameter(parameterScope, initializationScope, assignedParameter);
                error = value as ErrorExpression;
                if (error != null)
                    return true;
            }

            parameterScope.DefineVariable(new VariableDefinitionExpression(funcParameter.Name), value);
            return true;
        }

        /// <summary>
        /// Creates a new scope for calling a function and populates values for parameters passed to the function.
        /// </summary>
        /// <param name="function">The function defining the parameters to populate.</param>
        /// <param name="scope">The outer scope containing the function call.</param>
        /// <param name="error">[out] A <see cref="ErrorExpression"/> indicating why constructing the new scope failed.</param>
        /// <returns>The new scope, <c>null</c> if an error occurred - see <paramref name="error"/> for error details.</returns>
        public InterpreterScope GetParameters(FunctionDefinitionExpression function, InterpreterScope scope, out ExpressionBase error)
        {
            var parameterScope = function.CreateCaptureScope(scope);
            var initializationScope = new InterpreterScope(scope) { Context = new ParameterInitializationContext(function, Parameters) };

            // optimization for no parameter function
            if (function.Parameters.Count == 0 && Parameters.Count == 0)
            {
                error = null;
                return parameterScope;
            }

            // optimization for single parameter function
            if (function.Parameters.Count == 1 && Parameters.Count == 1)
            {
                if (GetSingleParameter(function, parameterScope, initializationScope, out error))
                {
                    if (function.Parameters.First().Name == "...")
                    {
                        var value = parameterScope.GetVariable("...");
                        parameterScope.UndefineVariable("...");
                        parameterScope.DefineVariable(new VariableDefinitionExpression("varargs"), value);
                    }
                    return parameterScope;
                }

                if (error != null)
                    return null;
            }

            var providedParameters = new List<string>(function.Parameters.Count);
            foreach (var parameter in function.Parameters)
                providedParameters.Add(parameter.Name);

            ArrayExpression varargs = null;
            if (providedParameters.Remove("..."))
            {
                varargs = new ArrayExpression();
                parameterScope.DefineVariable(new VariableDefinitionExpression("varargs"), varargs);
            }

            var parameterCount = providedParameters.Count;

            int index = 0;
            bool namedParameters = false;
            foreach (var parameter in Parameters)
            {
                var assignedParameter = parameter as AssignmentExpression;
                if (assignedParameter != null)
                {
                    if (!providedParameters.Remove(assignedParameter.Variable.Name))
                    {
                        if (!function.Parameters.Any(p => p.Name == assignedParameter.Variable.Name))
                        {
                            error = new ErrorExpression(string.Format("'{0}' does not have a '{1}' parameter", function.Name.Name, assignedParameter.Variable.Name), parameter);
                            return null;
                        }

                        error = new ErrorExpression(string.Format("'{0}' already has a value", assignedParameter.Variable.Name), assignedParameter.Variable);
                        return null;
                    }

                    var value = GetParameter(parameterScope, initializationScope, assignedParameter);
                    error = value as ErrorExpression;
                    if (error != null)
                        return null;

                    var variableDefinition = new VariableDefinitionExpression(assignedParameter.Variable);
                    parameter.CopyLocation(variableDefinition);
                    parameterScope.DefineVariable(variableDefinition, value);
                    namedParameters = true;
                }
                else
                {
                    if (namedParameters)
                    {
                        error = new ErrorExpression("Non-named parameter following named parameter", parameter);
                        return null;
                    }

                    if (index >= parameterCount && varargs == null)
                    {
                        error = new ErrorExpression("Too many parameters passed to function", parameter);
                        return null;
                    }

                    var variableName = index < parameterCount ? function.Parameters.ElementAt(index).Name : "...";

                    assignedParameter = new AssignmentExpression(new VariableExpression(variableName), parameter);
                    var value = GetParameter(parameterScope, initializationScope, assignedParameter);
                    error = value as ErrorExpression;
                    if (error != null)
                        return null;

                    if (index < parameterCount)
                    {
                        providedParameters.Remove(variableName);
                        var variableDefinition = new VariableDefinitionExpression(variableName);
                        parameter.CopyLocation(variableDefinition);
                        parameterScope.DefineVariable(variableDefinition, value);
                    }
                    else
                    {
                        varargs.Entries.Add(value);
                    }
                }

                ++index;
            }

            foreach (var parameter in providedParameters)
            {
                ExpressionBase value;
                if (!function.DefaultParameters.TryGetValue(parameter, out value))
                {
                    error = new ErrorExpression(string.Format("Required parameter '{0}' not provided", parameter), FunctionName);
                    return null;
                }

                var assignmentScope = new InterpreterScope(initializationScope) { Context = new AssignmentExpression(new VariableExpression(parameter), value) };
                if (!value.ReplaceVariables(assignmentScope, out value))
                {
                    error = new ErrorExpression(value, this);
                    return null;
                }

                parameterScope.DefineVariable(new VariableDefinitionExpression(parameter), value);
            }

            error = null;
            return parameterScope;
        }

        /// <summary>
        /// Determines whether the specified <see cref="FunctionCallExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="FunctionCallExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="FunctionCallExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as FunctionCallExpression;
            return that != null && FunctionName == that.FunctionName && ExpressionsEqual(Parameters, that.Parameters);
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                yield return FunctionName;

                foreach (var parameter in Parameters)
                    yield return parameter;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            dependencies.Add(FunctionName.Name);

            foreach (var parameter in Parameters)
            {
                var nested = parameter as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }

        public override bool? IsTrue(InterpreterScope scope, out ErrorExpression error)
        {
            ExpressionBase result;
            if (!Evaluate(scope, false, out result))
            {
                error = result as ErrorExpression;
                return null;
            }

            var functionCall = result as FunctionCallExpression;
            if (functionCall != null) // prevent recursion
            {
                error = null;

                var funcDef = scope.GetFunction(functionCall.FunctionName.Name);
                if (funcDef is Functions.AlwaysTrueFunction)
                    return true;

                if (funcDef is Functions.AlwaysFalseFunction)
                    return false;

                return null;
            }

            return result.IsTrue(scope, out error);
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            ExpressionBase result;
            bool success = Invoke(scope, out result);
            if (!success)
            {
                if (scope.GetInterpreterContext<FunctionCallExpression>() != null)
                {
                    var error = result as ErrorExpression;
                    result = new ErrorExpression(FunctionName.Name + " call failed: " + error.Message, FunctionName) { InnerError = error };
                }
                return (ErrorExpression)result;
            }

            scope.ReturnValue = result;
            return null;
        }
    }

    public class FunctionNameExpression : VariableExpressionBase, INestedExpressions
    {
        public FunctionNameExpression(string name)
            : base(name)
        {
        }

        internal FunctionNameExpression(string name, int line, int column)
            : base(name, line, column)
        {
        }

        internal FunctionNameExpression(VariableExpression variable)
            : base(variable.Name)
        {
            Location = variable.Location;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("FunctionName: ");
            AppendString(builder);
            return builder.ToString();
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                return Enumerable.Empty<ExpressionBase>();
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            dependencies.Add(Name);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }
    }
}
