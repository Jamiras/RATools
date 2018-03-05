using RATools.Data;
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
        /// Gets or sets a value indicating whether the trigger processing is currently within a NOT logical container.
        /// </summary>
        public bool IsInNot { get; set; }

        /// <summary>
        /// Gets a memory accessor.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="accessor">[out] The generated accessor.</param>
        /// <param name="error">[out] The error that occurred.</param>
        /// <returns><c>true</c> if <paramref name="accessor"/> is set, <c>false</c> if <paramref name="error"/> is set.</returns>
        public static bool GetMemoryAccessor(FunctionCallExpression functionCall, InterpreterScope scope, out Field accessor, out ParseErrorExpression error)
        {
            var requirements = new List<Requirement>();

            ExpressionBase result;
            var innerScope = new InterpreterScope(scope) { Context = new TriggerBuilderContext { Trigger = requirements } };
            if (!functionCall.Evaluate(innerScope, out result, false))
            {
                accessor = new Field();
                error = result as ParseErrorExpression;
                return false;
            }

            if (requirements.Count != 1 || requirements[0].Operator != RequirementOperator.None)
            {
                accessor = new Field();
                error = new ParseErrorExpression(functionCall.FunctionName.Name + " did not evaluate to a memory accessor");
                return false;
            }

            accessor = requirements[0].Left;
            error = null;
            return true;
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
                result = new ParseErrorExpression("value can only contain memory accessors or arithmetic expressions");
                return false;
            }

            var requirements = new List<Requirement>();
            var innerScope = new InterpreterScope(scope) { Context = new TriggerBuilderContext { Trigger = requirements } };
            if (!functionCall.Evaluate(innerScope, out result, false))
                return false;

            if (result != null)
                return ProcessValueExpression(result, scope, builder, out result);

            if (requirements.Count != 1 || requirements[0].Operator != RequirementOperator.None)
            {
                result = new ParseErrorExpression(functionCall.FunctionName.Name + " did not evaluate to a memory accessor");
                return false;
            }

            requirements[0].Left.Serialize(builder);
            return true;
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

            var message = achievement.Optimize();
            if (message != null)
            {
                result = new ParseErrorExpression(message, expression);
                return false;
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
