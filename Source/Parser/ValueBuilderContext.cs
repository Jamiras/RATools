using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser
{

    internal class ValueBuilderContext : TriggerBuilderContext
    {
        /// <summary>
        /// Gets a serialized string for calculating a value from memory.
        /// </summary>
        /// <param name="expression">The expression to evaluate. May contain mathematic operations and memory accessors.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="result">[out] The error if not successful.</param>
        /// <returns>The string if successful, <c>null</c> if not.</returns>
        public static string GetValueString(ExpressionBase expression, InterpreterScope scope, out ExpressionBase result)
        {
            var requirements = new List<Requirement>();
            var context = new ValueBuilderContext { Trigger = requirements };
            var triggerBuilderScope = new InterpreterScope(scope) { Context = context };
            if (!expression.ReplaceVariables(triggerBuilderScope, out expression))
            {
                result = expression;
                return null;
            }

            var integerConstant = expression as IntegerConstantExpression;
            if (integerConstant != null)
            {
                result = null;
                return String.Format("v{0}", (int)integerConstant.Value);
            }

            var trigger = expression as ITriggerExpression;
            if (trigger == null)
            {
                result = new ErrorExpression("Cannot create value from " + expression.Type);
                return null;
            }

            result = trigger.BuildTrigger(context);
            if (result != null)
                return null;

            bool mustBeMeasured = requirements.Any(requirement =>
            {
                switch (requirement.Type)
                {
                    case RequirementType.None: // implied AddSource
                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                        switch (requirement.Operator)
                        {
                            case RequirementOperator.Multiply:
                                switch (requirement.Right.Type)
                                {
                                    case FieldType.Value:
                                    case FieldType.Float:
                                        break;

                                    default:
                                        // if the right side is not a number, then Measured is required
                                        return true;
                                }
                                break;

                            case RequirementOperator.Divide:
                                if (requirement.Right.Type != FieldType.Float)
                                    return true;
                                break;

                            case RequirementOperator.None: // implied *1
                                break;

                            default:
                                // any modifier that's not multiplication or division requires Measured syntax
                                return true;
                        }

                        return false;

                    default:
                        // anything that's not an AddSource or SubSource requires Measured syntax
                        return true;
                }
            });

            if (mustBeMeasured)
                return GetMeasuredValueString(requirements);

            return GetLegacyValueString(requirements);
        }

        private static string GetLegacyValueString(List<Requirement> requirements)
        {
            // convert all division to multiplication
            foreach (var requirement in requirements.Where(r => r.Operator == RequirementOperator.Divide))
            {
                if (requirement.Right.Type == FieldType.Float)
                {
                    requirement.Right = FieldFactory.CreateField(1.0f / requirement.Right.Float);
                    requirement.Operator = RequirementOperator.Multiply;
                }
                else if (requirement.Right.Type == FieldType.Value)
                {
                    requirement.Right = FieldFactory.CreateField(1.0f / requirement.Right.Value);
                    requirement.Operator = RequirementOperator.Multiply;
                }
            }

            var minVer = 0.0;
            foreach (var requirement in requirements)
                minVer = Math.Max(minVer, requirement.MinimumVersion());

            var value = new StringBuilder();
            var adjustment = 0;
            foreach (var requirement in requirements)
            {
                if (requirement.Left.Type == FieldType.Value && requirement.Operator == RequirementOperator.None)
                {
                    var intValue = (int)requirement.Left.Value;
                    if (requirement.Type == RequirementType.SubSource)
                        adjustment -= intValue;
                    else
                        adjustment += intValue;

                    continue;
                }

                var factor = (requirement.Type == RequirementType.SubSource) ? -1.0 : 1.0;
                if (requirement.Operator == RequirementOperator.Multiply)
                    factor *= requirement.Right.Type == FieldType.Float ? requirement.Right.Float : (int)requirement.Right.Value;

                requirement.Left.Serialize(value);
                if (factor != 1.0)
                {
                    value.Append('*');

                    if ((factor % 1.0f) == 0)
                    {
                        // factor is a whole number, just output it
                        value.Append((int)(uint)factor);
                    }
                    else
                    {
                        // use invariant culture to force decimal in output
                        // cast to float to truncate to 7 significant digits
                        value.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0#####}", (float)factor);
                    }
                }

                value.Append('_');
            }

            if (adjustment != 0)
            {
                value.Append('v');
                value.Append(adjustment);
            }
            else if (value.Length > 0)
            {
                value.Length--;
            }
            else
            {
                value.Append('0');
            }

            return value.ToString();
        }

        private static string GetMeasuredValueString(List<Requirement> requirements)
        {
            MeasuredRequirementExpression.EnsureHasMeasuredRequirement(requirements);

            var achievement = new AchievementBuilder();
            foreach (var requirement in requirements)
                achievement.CoreRequirements.Add(requirement);

            return achievement.SerializeRequirements();
        }
    }
}
