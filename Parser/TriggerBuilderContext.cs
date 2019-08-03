using RATools.Data;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    internal class TriggerBuilderContext
    {
        /// <summary>
        /// Gets the collection of requirements that forms the trigger.
        /// </summary>
        public ICollection<Requirement> Trigger;

        /// <summary>
        /// Gets the last requirement added to the achievement.
        /// </summary>
        public Requirement LastRequirement
        {
            get { return Trigger.LastOrDefault(); }
        }

        /// <summary>
        /// Gets a serialized string for calculating a value from memory.
        /// </summary>
        /// <param name="expression">The expression to evaluate. May contain mathematic operations and memory accessors.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="result">[out] The error if not successful.</param>
        /// <returns>The string if successful, <c>null</c> if not.</returns>
        public static string GetValueString(ExpressionBase expression, InterpreterScope scope, out ExpressionBase result)
        {
            var builder = new StringBuilder();
            if (!ProcessValueExpression(expression, scope, builder, out result))
                return null;

            return builder.ToString();
        }

        private static bool ProcessValueExpression(ExpressionBase expression, InterpreterScope scope, StringBuilder builder, out ExpressionBase result)
        {
            IntegerConstantExpression integer;

            var mathematic = expression as MathematicExpression;
            if (mathematic != null)
            {
                if (!ProcessValueExpression(mathematic.Left, scope, builder, out result))
                    return false;

                integer = mathematic.Right as IntegerConstantExpression;
                switch (mathematic.Operation)
                {
                    case MathematicOperation.Add:
                        builder.Append('_');

                        if (integer != null)
                        {
                            builder.Append('v');
                            builder.Append(integer.Value);
                            return true;
                        }

                        return ProcessValueExpression(mathematic.Right, scope, builder, out result);

                    case MathematicOperation.Subtract:
                        if (integer != null)
                        {
                            builder.Append("_v-");
                            builder.Append(integer.Value);
                            return true;
                        }

                        result = new ParseErrorExpression("Value being subtracted must be an integer constant", mathematic.Right);
                        return false;

                    case MathematicOperation.Multiply:
                        if (integer != null)
                        {
                            builder.Append('*');
                            builder.Append(integer.Value);
                            return true;
                        }

                        result = new ParseErrorExpression("Multiplier must be an integer constant", mathematic.Right);
                        return false;

                    case MathematicOperation.Divide:
                        if (integer != null)
                        {
                            builder.Append('*');
                            var inverted = 1 / (double)integer.Value;
                            builder.Append(inverted);
                            return true;
                        }

                        result = new ParseErrorExpression("Divisor must be an integer constant", mathematic.Right);
                        return false;
                }
            }

            integer = expression as IntegerConstantExpression;
            if (integer != null)
            {
                result = new ParseErrorExpression("value cannot start with integer constant", integer);
                return false;
            }

            var functionCall = expression as FunctionCallExpression;
            if (functionCall == null)
            {
                result = new ParseErrorExpression("value can only contain memory accessors or arithmetic expressions", expression);
                return false;
            }

            var requirements = new List<Requirement>();
            var context = new TriggerBuilderContext { Trigger = requirements };
            var innerScope = new InterpreterScope(scope) { Context = context };
            result = context.CallFunction(functionCall, innerScope);
            if (result != null)
                return false;

            if (requirements.Count != 1 || requirements[0].Operator != RequirementOperator.None)
            {
                result = new ParseErrorExpression(functionCall.FunctionName.Name + " did not evaluate to a memory accessor", functionCall.FunctionName);
                return false;
            }

            requirements[0].Left.Serialize(builder);
            return true;
        }

        internal abstract class FunctionDefinition : FunctionDefinitionExpression
        {
            public FunctionDefinition(string name)
                : base(name)
            {
            }

            protected bool IsInTriggerClause(InterpreterScope scope, out ExpressionBase result)
            {
                var triggerContext = scope.GetContext<TriggerBuilderContext>();
                if (triggerContext == null) // explicitly in trigger clause
                {
                    var assignment = scope.GetInterpreterContext<AssignmentExpression>();
                    if (assignment == null) // in generic assignment clause - may be used in a trigger - will determine later
                    {
                        result = new ParseErrorExpression(Name.Name + " has no meaning outside of a trigger clause");
                        return false;
                    }
                }

                result = null;
                return true;
            }

            public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
            {
                return IsInTriggerClause(scope, out result);
            }

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a trigger clause");
                return false;
            }

            public abstract ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall);
        }

        public ParseErrorExpression CallFunction(FunctionCallExpression functionCall, InterpreterScope scope)
        {
            var functionDefinition = scope.GetFunction(functionCall.FunctionName.Name);
            if (functionDefinition == null)
                return new ParseErrorExpression("Unknown function: " + functionCall.FunctionName.Name, functionCall.FunctionName);

            var triggerBuilderFunction = functionDefinition as FunctionDefinition;
            if (triggerBuilderFunction == null)
                return new ParseErrorExpression(functionCall.FunctionName.Name + " cannot be called from within a trigger clause", functionCall);

            var error = triggerBuilderFunction.BuildTrigger(this, scope, functionCall);
            if (error != null)
                return new ParseErrorExpression(error, functionCall);

            return null;
        }

        /// <summary>
        /// Populates an achievement from an expression.
        /// </summary>
        /// <param name="achievement">The achievement to populate.</param>
        /// <param name="expression">The expression to populate from.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="result">[out] The error if not successful.</param>
        /// <returns><c>true</c> if successful, <c>false</c> if not.</returns>
        public static bool ProcessAchievementConditions(ScriptInterpreterAchievementBuilder achievement, ExpressionBase expression, InterpreterScope scope, out ExpressionBase result)
        {
            ParseErrorExpression parseError;
            if (!achievement.PopulateFromExpression(expression, scope, out parseError))
            {
                result = parseError;
                return false;
            }

            // only optimize at the outermost level
            if (ReferenceEquals(scope.GetOutermostContext<TriggerBuilderContext>(), scope.GetContext<TriggerBuilderContext>()))
            {
                var message = achievement.Optimize();
                if (message != null)
                {
                    result = new ParseErrorExpression(message, expression);
                    return false;
                }
            }

            result = null;
            return true;
        }

        /// <summary>
        /// Gets a serialized string for determining if memory matches the provided expression.
        /// </summary>
        /// <param name="expression">The expression to process.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="result">[out] The error if not successful.</param>
        /// <returns><c>true</c> if successful, <c>false</c> if not.</returns>
        public static string GetConditionString(ExpressionBase expression, InterpreterScope scope, out ExpressionBase result)
        {
            var achievement = new ScriptInterpreterAchievementBuilder();
            if (!ProcessAchievementConditions(achievement, expression, scope, out result))
                return null;

            return achievement.SerializeRequirements();
        }
    }
}
