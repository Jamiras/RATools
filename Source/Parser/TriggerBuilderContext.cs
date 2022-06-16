using RATools.Data;
using RATools.Parser.Functions;
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

            var triggerBuilderScope = new InterpreterScope(scope) { Context = new TriggerBuilderContext() };
            if (!expression.ReplaceVariables(triggerBuilderScope, out expression))
            {
                result = expression;
                return null;
            }

            if (!ProcessValueExpression(expression, scope, terms, out result))
                return null;

            terms.RemoveAll(t => t.multiplier == 0.0);

            if (terms.Any(t => t.measured != null))
                return BuildMeasuredValueString(terms);

            return BuildNonMeasuredValueString(terms);
        }

        private static string BuildNonMeasuredValueString(List<Term> terms)
        {
            var builder = new StringBuilder();

            foreach (var term in terms)
            {
                if (builder.Length > 0)
                    builder.Append('_');

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
                            builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0#####}", term.multiplier);
                        }
                        break;

                    default:
                        term.field.Serialize(builder);
                        if (term.multiplier != 1.0)
                        {
                            builder.Append('*');

                            if ((term.multiplier % 1) == 0)
                            {
                                // value is a whole number, just output it
                                builder.Append((int)term.multiplier);
                            }
                            else
                            {
                                builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0#####}", term.multiplier);
                            }
                        }
                        break;
                }
            }

            if (builder.Length == 0)
                return "v0";

            return builder.ToString();
        }

        private static string BuildMeasuredValueString(List<Term> terms)
        {
            var builder = new StringBuilder();

            // move subsources before addsources
            int index = 0;
            while (terms.Last().multiplier < 0 && index < terms.Count)
            {
                var term = terms.Last();
                terms.RemoveAt(terms.Count - 1);
                terms.Insert(index++, term);
            }

            for (int i = 0; i < terms.Count; i++)
            {
                var term = terms[i];

                if (builder.Length > 0)
                    builder.Append('_');

                if (term.measured != null)
                {
                    if (i < terms.Count - 1)
                    {
                        foreach (var requirement in term.measured)
                        {
                            if (requirement.Type == RequirementType.Measured)
                            {
                                if (term.multiplier < 0)
                                {
                                    requirement.Type = RequirementType.SubSource;
                                    term.multiplier = -term.multiplier;
                                }
                                else
                                {
                                    requirement.Type = RequirementType.AddSource;
                                }
                                break;
                            }
                        }
                    }

                    builder.Append(AchievementBuilder.SerializeRequirements(term.measured, new Requirement[0][]));
                }
                else
                {
                    if (i == terms.Count - 1)
                    {
                        builder.Append("M:");
                    }
                    else if (term.multiplier < 0)
                    {
                        builder.Append("B:");
                        term.multiplier = -term.multiplier;
                    }
                    else
                    {
                        builder.Append("A:");
                    }

                    term.field.Serialize(builder);
                }

                if (term.multiplier != 1.0)
                {
                    builder.Append('*');
                    if (term.multiplier != Math.Floor(term.multiplier))
                        builder.Append('f');

                    builder.Append(term.multiplier);
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

        private static ExpressionBase WrapInMeasured(ExpressionBase expression)
        {
            return new FunctionCallExpression("measured", new ExpressionBase[]
            {
                expression,
                AlwaysTrueFunction.CreateAlwaysTrueFunctionCall(),
                new StringConstantExpression("raw")
            });
        }

        private static bool MergeFields(Field field, Term term, MathematicOperation operation)
        {
            ExpressionBase right;
            if (field.Type == FieldType.Value)
                right = new IntegerConstantExpression((int)field.Value);
            else if (field.Type == FieldType.Float)
                right = new FloatConstantExpression(field.Float);
            else
                return false;

            ExpressionBase left = null;
            if (term.multiplier == 1.0)
            {
                if (term.field.Type == FieldType.Value)
                    left = new IntegerConstantExpression((int)term.field.Value);
                else if (term.field.Type == FieldType.Float)
                    left = new FloatConstantExpression(term.field.Float);
            }

            if (left == null)
            {
                FloatConstantExpression floatRight;
                switch (operation)
                {
                    case MathematicOperation.Multiply:
                        floatRight = FloatConstantExpression.ConvertFrom(right) as FloatConstantExpression;
                        if (floatRight == null)
                            return false;

                        term.multiplier *= floatRight.Value;
                        return true;

                    case MathematicOperation.Divide:
                        floatRight = FloatConstantExpression.ConvertFrom(right) as FloatConstantExpression;
                        if (floatRight == null)
                            return false;

                        term.multiplier /= floatRight.Value;
                        return true;

                    default:
                        return false;
                }
            }

            var mathematicExpression = new MathematicExpression(left, operation, right);
            term.field = AchievementBuilder.CreateFieldFromExpression(mathematicExpression.MergeOperands());
            return true;
        }

        private static bool ProcessValueExpression(ExpressionBase expression, InterpreterScope scope, List<Term> terms, out ExpressionBase result)
        {
            var functionCall = expression as FunctionCallExpression;
            if (functionCall != null)
            {
                var requirements = new List<Requirement>();
                var context = new ValueBuilderContext() { Trigger = requirements };
                var valueScope = new InterpreterScope(scope) { Context = context };
                var error = context.CallFunction(functionCall, valueScope);
                if (error != null)
                {
                    result = error;
                    return false;
                }

                SetImpliedMeasuredTarget(requirements);
                return ProcessMeasuredValue(requirements, expression, terms, out result);
            }

            var field = AchievementBuilder.CreateFieldFromExpression(expression);
            if (field.Type != FieldType.None)
            {
                terms.Last().field = field;
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

                field = AchievementBuilder.CreateFieldFromExpression(mathematic.Right);
                if (MergeFields(field, terms.Last(), mathematic.Operation))
                    return true;

                switch (mathematic.Operation)
                {
                    case MathematicOperation.Add:
                        terms.Add(new Term { multiplier = 1.0 });
                        return ProcessValueExpression(mathematic.Right, scope, terms, out result);

                    case MathematicOperation.Subtract:
                        terms.Add(new Term { multiplier = -1.0 });
                        return ProcessValueExpression(mathematic.Right, scope, terms, out result);

                    case MathematicOperation.Multiply:
                    case MathematicOperation.Divide:
                        return ProcessValueExpression(WrapInMeasured(expression), scope, terms, out result);
                }
            }

            var conditionalExpression = expression as ConditionalExpression;
            if (conditionalExpression != null)
            {
                var valueScope = new InterpreterScope(scope) { Context = new ValueBuilderContext() };

                ErrorExpression parseError;
                var achievement = new ScriptInterpreterAchievementBuilder();
                if (!achievement.PopulateFromExpression(expression, valueScope, out parseError))
                {
                    result = parseError;
                    return false;
                }

                SetImpliedMeasuredTarget(achievement.CoreRequirements);
                foreach (var alt in achievement.AlternateRequirements)
                    SetImpliedMeasuredTarget(alt);

                var message = achievement.Optimize();
                if (message != null)
                {
                    result = new ErrorExpression(message, expression);
                    return false;
                }

                if (achievement.AlternateRequirements.Any())
                {
                    result = new ErrorExpression("Alt groups not supported in value expression", expression);
                    return false;
                }

                return ProcessMeasuredValue(achievement.CoreRequirements, expression, terms, out result);
            }

            result = new ErrorExpression("Value must be a constant or a memory accessor", expression);
            return false;
        }

        private static void SetImpliedMeasuredTarget(IEnumerable<Requirement> requirements)
        {
            bool seenMeasured = false;
            foreach (var requirement in requirements)
            {
                if (requirement.IsMeasured)
                {
                    seenMeasured = true;

                    // when Measured is used in a value expression, it has an infinite hit target.
                    // if a specific hit target is not provided, set it to MaxValue to prevent the
                    // ResetIf/PauseIf flags from being optimized away.
                    if (requirement.Operator != RequirementOperator.None && requirement.HitCount == 0)
                        requirement.HitCount = uint.MaxValue;
                }
            }

            // complex expression must be converted into a Measured statement
            // if a Measured requirement does not exist, assign one
            if (!seenMeasured)
            {
                foreach (var requirement in requirements)
                {
                    if (requirement.Type == RequirementType.None)
                    {
                        requirement.Type = RequirementType.Measured;
                        SetImpliedMeasuredTarget(requirements);
                        break;
                    }
                }
            }
        }

        private static bool ProcessMeasuredValue(ICollection<Requirement> requirements, ExpressionBase expression, List<Term> terms, out ExpressionBase result)
        {
            var count = requirements.Count;
            if (count == 0)
            {
                result = new ErrorExpression("value did not evaluate to a memory accessor", expression);
                return false;
            }

            // if expression is a single term, just return it
            if (count == 1 && requirements.First().Operator == RequirementOperator.None)
            {
                terms.Last().field = requirements.First().Left;
                result = null;
                return true;
            }

            var measured = requirements.FirstOrDefault(r => r.Type == RequirementType.Measured);
            if (measured == null)
            {
                result = new ErrorExpression("value could not be converted into a measured statement", expression);
                return false;
            }

            foreach (var requirement in requirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.Measured:
                        if (requirement != measured)
                        {
                            result = new ErrorExpression("value contains multiple measured elements", expression);
                            return false;
                        }
                        break;

                    case RequirementType.ResetIf:
                        // ResetIf are only allowed if something has a target HitCount
                        if (requirements.All(r => r.HitCount == 0))
                        {
                            result = new ErrorExpression("value contains a never without a repeated", expression);
                            return false;
                        }
                        break;

                    case RequirementType.PauseIf:
                        // PauseIf only allowed if the measured value has a target HitCount
                        if (measured.HitCount == 0)
                        {
                            result = new ErrorExpression("value contains an unless without a repeated", expression);
                            return false;
                        }
                        break;

                    default:
                        break;
                }
            }

            if (measured.HitCount == uint.MaxValue)
                measured.HitCount = 0;

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

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                var context = scope.GetInterpreterContext<TriggerBuilderContext>();
                if (context == null)
                {
                    result = new ErrorExpression(Name.Name + " has no meaning outside of a trigger clause");
                    return false;
                }

                return ReplaceVariables(scope, out result);
            }

            public abstract ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall);
        }

        public ErrorExpression CallFunction(FunctionCallExpression functionCall, InterpreterScope scope)
        {
            var functionDefinition = scope.GetFunction(functionCall.FunctionName.Name);
            if (functionDefinition == null)
                return new UnknownVariableParseErrorExpression("Unknown function: " + functionCall.FunctionName.Name, functionCall.FunctionName);

            var triggerBuilderFunction = functionDefinition as FunctionDefinition;
            if (triggerBuilderFunction == null)
                return new ErrorExpression(functionCall.FunctionName.Name + " cannot be called from within a trigger clause", functionCall);

            var error = triggerBuilderFunction.BuildTrigger(this, scope, functionCall);
            if (error != null)
                return ErrorExpression.WrapError(error, functionCall.FunctionName.Name + " call failed", functionCall.FunctionName);

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
            ErrorExpression parseError;
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
                    result = new ErrorExpression(message, expression);
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

    internal class ValueBuilderContext : TriggerBuilderContext
    {
    }
}