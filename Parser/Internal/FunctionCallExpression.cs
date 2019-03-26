using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class FunctionCallExpression : ExpressionBase, INestedExpressions
    {
        public FunctionCallExpression(string functionName, ICollection<ExpressionBase> parameters)
            : this(new VariableExpression(functionName), parameters)
        {
        }

        public FunctionCallExpression(VariableExpression functionName, ICollection<ExpressionBase> parameters)
            : base(ExpressionType.FunctionCall)
        {
            FunctionName = functionName;
            Parameters = parameters;

            Line = functionName.Line;
            Column = functionName.Column;            
        }

        /// <summary>
        /// Gets the name of the function to call.
        /// </summary>
        public VariableExpression FunctionName { get; private set; }

        /// <summary>
        /// Gets the parameters to pass to the function.
        /// </summary>
        public ICollection<ExpressionBase> Parameters { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            FunctionName.AppendString(builder);
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
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var functionDefinition = scope.GetFunction(FunctionName.Name);
            if (functionDefinition == null)
            {
                result = new ParseErrorExpression("Unknown function: " + FunctionName.Name, FunctionName);
                return false;
            }

            var functionScope = GetParameters(functionDefinition, scope, out result);
            if (functionScope == null)
                return false;

            if (functionScope.Depth >= 100)
            {
                result = new ParseErrorExpression("Maximum recursion depth exceeded", this);
                return false;
            }

            functionScope.Context = this;
            if (!functionDefinition.ReplaceVariables(functionScope, out result))
                return false;

            CopyLocation(result);
            return true;
        }

        /// <summary>
        /// Gets the return value from calling a function.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the function result.</param>
        /// <returns>
        ///   <c>true</c> if invocation was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var functionDefinition = scope.GetFunction(FunctionName.Name);
            if (functionDefinition == null)
            {
                result = new ParseErrorExpression("Unknown function: " + FunctionName.Name, FunctionName);
                return false;
            }

            var functionScope = GetParameters(functionDefinition, scope, out result);
            if (functionScope == null)
                return false;

            if (functionScope.Depth >= 100)
            {
                result = new ParseErrorExpression("Maximum recursion depth exceeded", this);
                return false;
            }

            functionScope.Context = this;
            if (!functionDefinition.Evaluate(functionScope, out result))
                return false;

            scope.ReturnValue = result;
            return true;
        }

        /// <summary>
        /// Gets the return value from calling a function.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the function result.</param>
        /// <returns>
        ///   <c>true</c> if invocation was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public bool Invoke(InterpreterScope scope, out ExpressionBase result)
        {
            if (Evaluate(scope, out result))
                return true;

            if (result.Line == 0)
                result = new ParseErrorExpression(result, FunctionName);

            return false;
        }

        /// <summary>
        /// Creates a new scope for calling a function and populates values for parameters passed to the function.
        /// </summary>
        /// <param name="function">The function defining the parameters to populate.</param>
        /// <param name="scope">The outer scope containing the function call.</param>
        /// <param name="error">[out] A <see cref="ParseErrorExpression"/> indicating why constructing the new scope failed.</param>
        /// <returns>The new scope, <c>null</c> if an error occurred - see <paramref name="error"/> for error details.</returns>
        public InterpreterScope GetParameters(FunctionDefinitionExpression function, InterpreterScope scope, out ExpressionBase error)
        {
            var parameterScope = new InterpreterScope(scope);

            var providedParameters = new List<string>(function.Parameters.Count);
            foreach (var parameter in function.Parameters)
                providedParameters.Add(parameter.Name);

            ArrayExpression varargs = null;
            if (providedParameters.Remove("..."))
            {
                varargs = new ArrayExpression();
                parameterScope.AssignVariable(new VariableExpression("varargs"), varargs);
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
                            error = new ParseErrorExpression(String.Format("'{0}' does not have a '{1}' parameter", function.Name.Name, assignedParameter.Variable.Name), parameter);
                            return null;
                        }

                        error = new ParseErrorExpression(String.Format("'{0}' already has a value", assignedParameter.Variable.Name), assignedParameter.Variable);
                        return null;
                    }

                    var assignmentScope = new InterpreterScope(scope) { Context = assignedParameter };

                    ExpressionBase value;
                    if (!assignedParameter.Value.ReplaceVariables(assignmentScope, out value))
                    {
                        error = new ParseErrorExpression(value, assignedParameter.Value);
                        return null;
                    }

                    parameterScope.DefineVariable(new VariableDefinitionExpression(assignedParameter.Variable), value);
                    namedParameters = true;
                }
                else
                {
                    if (namedParameters)
                    {
                        error = new ParseErrorExpression("Non-named parameter following named parameter", parameter);
                        return null;
                    }

                    if (index >= parameterCount && varargs == null)
                    {
                        error = new ParseErrorExpression("Too many parameters passed to function", parameter);
                        return null;
                    }

                    var variableName = (index < parameterCount) ? function.Parameters.ElementAt(index).Name : "...";
                    var assignmentScope = new InterpreterScope(scope) { Context = new AssignmentExpression(new VariableExpression(variableName), parameter) };

                    ExpressionBase value;
                    if (!parameter.ReplaceVariables(assignmentScope, out value))
                    {
                        error = new ParseErrorExpression(value, parameter);
                        return null;
                    }

                    if (index < parameterCount)
                    {
                        providedParameters.Remove(variableName);
                        parameterScope.DefineVariable(new VariableDefinitionExpression(variableName), value);
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
                    error = new ParseErrorExpression(String.Format("Required parameter '{0}' not provided", parameter), FunctionName);
                    return null;
                }

                var assignmentScope = new InterpreterScope(scope) { Context = new AssignmentExpression(new VariableExpression(parameter), value) };
                if (!value.ReplaceVariables(assignmentScope, out value))
                {
                    error = new ParseErrorExpression(value, this);
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
            var that = (FunctionCallExpression)obj;
            return FunctionName == that.FunctionName && Parameters == that.Parameters;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            if (FunctionName.Line == line)
                expressions.Add(new FunctionCallExpression(FunctionName, Parameters) { EndLine = FunctionName.Line, EndColumn = FunctionName.EndColumn });

            return ExpressionGroup.GetExpressionsForLine(expressions, Parameters, line);
        }
    }
}
