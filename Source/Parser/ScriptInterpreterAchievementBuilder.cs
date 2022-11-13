using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser
{
    internal class ScriptInterpreterAchievementBuilder : AchievementBuilder
    {
        public ScriptInterpreterAchievementBuilder() : base()
        {
        }

        /// <summary>
        /// Populates the <see cref="AchievementBuilder"/> from an expression.
        /// </summary>
        /// <param name="expression">The expression to populate from.</param>
        /// <returns><c>null</c> if successful, otherwise an error message indicating why it failed.</returns>
        public string PopulateFromExpression(ExpressionBase expression)
        {
            var context = new AchievementBuilderContext(this);
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope()) { Context = context };
            ErrorExpression error;

            ExpressionBase result;
            if (!expression.ReplaceVariables(scope, out result))
            {
                error = (ErrorExpression)result;
                if (error.InnerError != null)
                    return error.InnermostError.Message;

                return error.Message;
            }

            var requirement = result as RequirementExpressionBase;
            if (requirement == null)
                return "expression is not a requirement expression";

            // go through ITriggerExpression to ensure optimization occurs
            error = ((ITriggerExpression)requirement).BuildTrigger(context);
            if (error != null)
                return error.Message;

            return null;
        }
    }
}