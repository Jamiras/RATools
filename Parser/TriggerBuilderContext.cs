using RATools.Data;
using RATools.Parser.Internal;
using System;
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
            var terms = new List<Term>();
            terms.Add(new Term { multiplier = 1.0 });

            if (!ProcessValueExpression(expression, scope, terms, out result))
                return null;

            var builder = new StringBuilder();
            foreach (var term in terms)
            {
                if (term.multiplier == 0.0)
                    continue;

                if (builder.Length > 0)
                    builder.Append('_');

                if (term.measured != null)
                {
                    builder.Append(AchievementBuilder.SerializeRequirements(term.measured, new Requirement[0][]));
                    continue;
                }

                switch (term.field.Type)
                {
                    case FieldType.Value:
                        if (term.field.Value == 0)
                        {
                            if (builder.Length > 0)
                                builder.Length--;
                            break;
                        }

                        builder.Append('v');
                        var value = term.field.Value * term.multiplier;
                        if ((value % 1) == 0)
                        {
                            // value is a whole number, just output it
                            builder.Append((int)value);
                        }
                        else
                        {
                            // value is a complex number, output the parts
                            builder.Append(term.field.Value);
                            builder.Append('*');
                            builder.Append(term.multiplier);
                        }
                        break;

                    default:
                        term.field.Serialize(builder);
                        if (term.multiplier != 1.0)
                        {
                            builder.Append('*');
                            builder.Append(term.multiplier);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        private class Term
        {
            public Field field;
            public IEnumerable<Requirement> measured;
            public double multiplier;
        }

        private static bool ProcessValueExpression(ExpressionBase expression, InterpreterScope scope, List<Term> terms, out ExpressionBase result)
        {
            var functionCall = expression as FunctionCallExpression;
            if (functionCall != null)
            {
                var requirements = new List<Requirement>();
                var context = new TriggerBuilderContext() { Trigger = requirements };
                var error = context.CallFunction(functionCall, scope);
                if (error != null)
                {
                    result = error;
                    return false;
                }

                return ProcessMeasuredValue(requirements, expression, terms, out result);
            }

            IntegerConstantExpression integer = expression as IntegerConstantExpression;
            if (integer != null)
            {
                terms.Last().field = new Field { Type = FieldType.Value, Value = (uint)integer.Value };
                result = null;
                return true;
            }

            var mathematic = expression as MathematicExpression;
            if (mathematic != null)
            {
                if (mathematic.Operation == MathematicOperation.Multiply || mathematic.Operation == MathematicOperation.Divide)
                {
                    var mathematicLeft = mathematic.Left as MathematicExpression;
                    if (mathematicLeft != null && MathematicExpression.GetPriority(mathematicLeft.Operation) == MathematicPriority.Add)
                    {
                        var newLeft = new MathematicExpression(mathematicLeft.Left, mathematic.Operation, mathematic.Right);
                        var newRight = new MathematicExpression(mathematicLeft.Right, mathematic.Operation, mathematic.Right);
                        mathematic = new MathematicExpression(newLeft, mathematicLeft.Operation, newRight);
                    }
                }

                if (!ProcessValueExpression(mathematic.Left, scope, terms, out result))
                    return false;

                integer = mathematic.Right as IntegerConstantExpression;
                switch (mathematic.Operation)
                {
                    case MathematicOperation.Add:
                        if (integer != null)
                        {
                            if (terms.Last().field.Type == FieldType.Value && terms.Last().multiplier == 1.0)
                            {
                                terms.Last().field.Value += (uint)integer.Value;
                                return true;
                            }
                        }

                        terms.Add(new Term { multiplier = 1.0 });
                        return ProcessValueExpression(mathematic.Right, scope, terms, out result);

                    case MathematicOperation.Subtract:
                        if (integer != null)
                        {
                            if (terms.Last().field.Type == FieldType.Value && terms.Last().multiplier == 1.0)
                            {
                                terms.Last().field.Value -= (uint)integer.Value;
                                return true;
                            }
                        }

                        terms.Add(new Term { multiplier = -1.0 });
                        return ProcessValueExpression(mathematic.Right, scope, terms, out result);

                    case MathematicOperation.Multiply:
                        if (integer != null)
                        {
                            if (terms.Last().field.Type == FieldType.Value && terms.Last().multiplier == 1.0)
                                terms.Last().field.Value *= (uint)integer.Value;
                            else
                                terms.Last().multiplier *= integer.Value;

                            return true;
                        }

                        result = new ParseErrorExpression("Multiplier must be an integer constant", mathematic.Right);
                        return false;

                    case MathematicOperation.Divide:
                        if (integer != null)
                        {
                            if (terms.Last().field.Type == FieldType.Value && terms.Last().multiplier == 1.0 && (terms.Last().field.Value % integer.Value) == 0)
                                terms.Last().field.Value /= (uint)integer.Value;
                            else
                                terms.Last().multiplier /= integer.Value;
                            return true;
                        }

                        result = new ParseErrorExpression("Divisor must be an integer constant", mathematic.Right);
                        return false;
                }
            }

            var conditionalExpression = expression as ConditionalExpression;
            if (conditionalExpression != null)
            {
                var achievement = new ScriptInterpreterAchievementBuilder();
                if (!TriggerBuilderContext.ProcessAchievementConditions(achievement, expression, scope, out result))
                    return false;

                return ProcessMeasuredValue(achievement.CoreRequirements, expression, terms, out result);
            }

            result = new ParseErrorExpression("Value must be a constant or a memory accessor", expression);
            return false;
        }

        private static bool ProcessMeasuredValue(ICollection<Requirement> requirements, ExpressionBase expression, List<Term> terms, out ExpressionBase result)
        {
            var count = requirements.Count;
            if (count == 0)
            {
                result = new ParseErrorExpression("value did not evaluate to a memory accessor", expression);
                return false;
            }

            // if expression is a single term, just return it
            if (count == 1 && requirements.First().Operator == RequirementOperator.None)
            {
                terms.Last().field = requirements.First().Left;
                result = null;
                return true;
            }

            // complex expression must be converted into a Measured statement
            // if a Measured requirement does not exist, assign one
            var measured = requirements.FirstOrDefault(r => r.Type == RequirementType.Measured);
            if (measured == null)
            {
                measured = requirements.FirstOrDefault(r => r.Type == RequirementType.None);
                if (measured == null)
                {
                    result = new ParseErrorExpression("value could not be converted into a measured statement", expression);
                    return false;
                }

                measured.Type = RequirementType.Measured;
            }

            foreach (var requirement in requirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.Measured:
                        if (requirement != measured)
                        {
                            result = new ParseErrorExpression("value contains multiple measured elements", expression);
                            return false;
                        }
                        break;

                    case RequirementType.ResetIf:
                        // ResetIf are only allowed if the measured value has a target HitCount
                        if (measured.HitCount == 0)
                        {
                            result = new ParseErrorExpression("value contains a never without a repeated", expression);
                            return false;
                        }
                        break;

                    case RequirementType.PauseIf:
                        // PauseIf only allowed if the measured value has a target HitCount
                        if (measured.HitCount == 0)
                        {
                            result = new ParseErrorExpression("value contains an unless without a repeated", expression);
                            return false;
                        }
                        break;

                    default:
                        break;
                }
            }

            terms.Last().measured = requirements;

            result = null;
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
                return new UnknownVariableParseErrorExpression("Unknown function: " + functionCall.FunctionName.Name, functionCall.FunctionName);

            var triggerBuilderFunction = functionDefinition as FunctionDefinition;
            if (triggerBuilderFunction == null)
                return new ParseErrorExpression(functionCall.FunctionName.Name + " cannot be called from within a trigger clause", functionCall);

            var error = triggerBuilderFunction.BuildTrigger(this, scope, functionCall);
            if (error != null)
                return ParseErrorExpression.WrapError(error, "Function call failed.", functionCall);

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
