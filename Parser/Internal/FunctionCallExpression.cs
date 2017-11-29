using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class FunctionCallExpression : ExpressionBase
    {
        public FunctionCallExpression(string functionName, ICollection<ExpressionBase> parameters)
            : base(ExpressionType.FunctionCall)
        {
            FunctionName = functionName;
            Parameters = parameters;
        }

        /// <summary>
        /// Gets the name of the function to call.
        /// </summary>
        public string FunctionName { get; private set; }

        /// <summary>
        /// Gets the parameters to pass to the function.
        /// </summary>
        public ICollection<ExpressionBase> Parameters { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(FunctionName);
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
            var parameters = new List<ExpressionBase>();
            foreach (var parameter in Parameters)
            {
                ExpressionBase value;
                if (!parameter.ReplaceVariables(scope, out value))
                {
                    result = value;
                    return false;
                }

                parameters.Add(value);
            }

            var functionCall = new FunctionCallExpression(FunctionName, parameters);
            functionCall.Line = Line;
            functionCall.Column = Column;
            result = functionCall;
            return true;
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
    }
}
