using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RATools.Parser.Internal
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

        public SoftwareVersion MinimumVersion { get; set; }

        [DebuggerDisplay("{field} * {Multiplier}")]
        private class Term
        {
            public Term()
            {
                multiplier = new Field { Type = FieldType.Value, Size = FieldSize.DWord, Value = 1 };
            }

            public Field field;
            public IEnumerable<Requirement> measured;
            public Field multiplier;

            public float Multiplier
            {
                get
                {
                    switch (multiplier.Type)
                    {
                        default:
                            return 1.0f;

                        case FieldType.Value:
                            return (int)multiplier.Value;

                        case FieldType.Float:
                            return multiplier.Float;
                    }
                }
            }
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
            if (term.Multiplier == 1.0f)
            {
                term.multiplier = field;
                return true;
            }

            if (term.multiplier.Type == FieldType.Value && field.Type == FieldType.Value)
            {
                // integer math
                switch (operation)
                {
                    case MathematicOperation.Multiply:
                        term.multiplier.Value *= field.Value;
                        return true;

                    case MathematicOperation.Divide:
                        term.multiplier.Value /= field.Value;
                        return true;

                    default:
                        return false;
                }
            }

            if (term.multiplier.Type == FieldType.Value)
                term.multiplier = FieldFactory.ConvertToFloat(term.multiplier);
            if (field.Type == FieldType.Value)
                field = FieldFactory.ConvertToFloat(field);

            switch (operation)
            {
                case MathematicOperation.Multiply:
                    term.multiplier.Float *= field.Float;
                    return true;

                case MathematicOperation.Divide:
                    term.multiplier.Float /= field.Float;
                    return true;

                default:
                    return false;
            }
        }

        private static Term ConvertToTerm(MemoryAccessorExpression memoryAccessor, out ExpressionBase error)
        {
            var requirements = new List<Requirement>();
            var context = new TriggerBuilderContext { Trigger = requirements };
            error = memoryAccessor.BuildTrigger(context);
            if (error != null)
                return null;

            if (requirements.Count == 1 && requirements[0].Operator == RequirementOperator.None)
                return new Term { field = requirements[0].Left };

            if (requirements.Last().Type == RequirementType.None)
                requirements.Last().Type = RequirementType.AddSource;

            return new Term { measured = requirements };
        }

        private static Term ConvertToTerm(ModifiedMemoryAccessorExpression modifiedMemoryAccessor, out ExpressionBase error)
        {
            var term = ConvertToTerm(modifiedMemoryAccessor.MemoryAccessor, out error);
            if (term == null)
                return term;

            Requirement requirement;
            if (term.measured == null)
            {
                if (modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.None)
                {
                    if (modifiedMemoryAccessor.CombiningOperator == RequirementType.SubSource)
                        term.multiplier = FieldFactory.CreateField(new FloatConstantExpression(-1.0f));

                    return term;
                }

                if (modifiedMemoryAccessor.Modifier.IsMemoryReference)
                {
                    // not a constant, have to use Measured syntax
                    requirement = new Requirement { Left = modifiedMemoryAccessor.MemoryAccessor.Field };
                    term.measured = new[] { requirement };
                }
                else
                {
                    // merge the constant into the multiplier
                    term.multiplier = modifiedMemoryAccessor.Modifier;

                    if (modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.Divide)
                    {
                        term.multiplier = FieldFactory.ConvertToFloat(term.multiplier);
                        term.multiplier.Float = 1.0f / term.multiplier.Float;
                    }

                    if (modifiedMemoryAccessor.CombiningOperator == RequirementType.SubSource)
                    {
                        term.multiplier = FieldFactory.NegateValue(term.multiplier);
                    }

                    return term;
                }
            }
            else
            {
                requirement = term.measured.Last();
            }

            requirement.Operator = modifiedMemoryAccessor.ModifyingOperator;
            requirement.Right = modifiedMemoryAccessor.Modifier;

            if (modifiedMemoryAccessor.CombiningOperator != RequirementType.None)
                requirement.Type = modifiedMemoryAccessor.CombiningOperator;
            else
                requirement.Type = RequirementType.AddSource;

            return term;
        }

        private static bool ProcessValueExpression(ExpressionBase expression, InterpreterScope scope, List<Term> terms, out ExpressionBase result)
        {
            Term term;

            switch (expression.Type)
            {
                case ExpressionType.IntegerConstant:
                case ExpressionType.FloatConstant:
                    terms.Add(new Term { field = FieldFactory.CreateField(expression) });
                    result = null;
                    return true;

                case ExpressionType.MemoryAccessor:
                    var memoryAccessor = expression as MemoryAccessorExpression;
                    if (memoryAccessor != null)
                    {
                        term = ConvertToTerm(memoryAccessor, out result);
                        if (term == null)
                            return false;

                        terms.Add(term);
                        return true;
                    }

                    var modifiedMemoryAccessor = expression as ModifiedMemoryAccessorExpression;
                    if (modifiedMemoryAccessor != null)
                    {
                        term = ConvertToTerm(modifiedMemoryAccessor, out result);
                        if (term == null)
                            return false;

                        terms.Add(term);
                        return true;
                    }

                    var memoryValue = expression as MemoryValueExpression;
                    if (memoryValue != null)
                    {
                        foreach (var accessor in memoryValue.MemoryAccessors)
                        {
                            term = ConvertToTerm(accessor, out result);
                            if (term == null)
                                return false;

                            terms.Add(term);
                        }

                        if (memoryValue.HasConstant)
                        {
                            term = new Term();
                            term.field = FieldFactory.CreateField(memoryValue.ExtractConstant());
                            if (term.field.Type == FieldType.Value && (int)term.field.Value < 0)
                            {
                                term.field.Value = (uint)-(int)term.field.Value;
                                term.multiplier = FieldFactory.NegateValue(term.multiplier);
                            }
                            terms.Add(term);
                        }

                        result = null;
                        return true;
                    }
                    break;

                case ExpressionType.Mathematic:
                    var mathematic = (MathematicExpression)expression;
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

                    if (MergeFields(FieldFactory.CreateField(mathematic.Right), terms.Last(), mathematic.Operation))
                        return true;

                    switch (mathematic.Operation)
                    {
                        case MathematicOperation.Add:
                            return ProcessValueExpression(mathematic.Right, scope, terms, out result);

                        case MathematicOperation.Subtract:
                            var numTerms = terms.Count;
                            if (!ProcessValueExpression(mathematic.Right, scope, terms, out result))
                                return false;
                            for (int i = numTerms; i < terms.Count; i++)
                                terms[i].multiplier = FieldFactory.NegateValue(terms[i].multiplier);
                            return true;

                        case MathematicOperation.Multiply:
                        case MathematicOperation.Divide:
                            return ProcessValueExpression(WrapInMeasured(expression), scope, terms, out result);
                    }
                    break;

                case ExpressionType.FunctionCall:
                    {
                        var functionCall = (FunctionCallExpression)expression;
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

                case ExpressionType.Requirement:
                    var triggerExpression = expression as ITriggerExpression;
                    if (triggerExpression != null)
                    {
                        var requirements = new List<Requirement>();
                        var context = new ValueBuilderContext() { Trigger = requirements };
                        var error = triggerExpression.BuildTrigger(context);
                        if (error != null)
                        {
                            result = error;
                            return false;
                        }

                        SetImpliedMeasuredTarget(requirements);
                        return ProcessMeasuredValue(requirements, expression, terms, out result);
                    }
                    break;
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
                terms.Add(new Term { field = requirements.First().Left });
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

            terms.Add(new Term { measured = requirements });

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

            public virtual ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
            {
                return new ErrorExpression("Not implemented", functionCall);
            }
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
        public static bool ProcessAchievementConditions(ScriptInterpreterAchievementBuilder achievement,
            RequirementExpressionBase expression, InterpreterScope scope, out ExpressionBase result)
        {
            var context = new AchievementBuilderContext(achievement);

            var scriptContext = scope.GetContext<AchievementScriptContext>();
            if (scriptContext != null)
                context.MinimumVersion = scriptContext.SerializationContext.MinimumVersion;

            var error = ((ITriggerExpression)expression).BuildTrigger(context);
            if (error != null)
            {
                result = error;
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
        public static string GetConditionString(ExpressionBase expression, InterpreterScope scope, SerializationContext serializationContext, out ExpressionBase result)
        {
            var requirement = expression as RequirementExpressionBase;
            if (requirement == null)
            {
                result = new ErrorExpression("expression is not a requirement", expression);
                return null;
            }

            var achievement = new ScriptInterpreterAchievementBuilder();
            if (!ProcessAchievementConditions(achievement, requirement, scope, out result))
                return null;

            return achievement.SerializeRequirements(serializationContext);
        }
    }

    internal class AchievementBuilderContext : TriggerBuilderContext
    {
        public AchievementBuilderContext(AchievementBuilder builder)
        {
            Achievement = builder;
            Trigger = Achievement.CoreRequirements;
        }

        public AchievementBuilderContext()
            : this(new AchievementBuilder())
        {
        }

        public AchievementBuilder Achievement { get; private set; }

        public bool? HasPauseIf { get; set; }

        public void BeginAlt()
        {
            Trigger = new List<Requirement>();
            Achievement.AlternateRequirements.Add(Trigger);
        }
    }

    internal class AltBuilderContext : AchievementBuilderContext
    {
        public AltBuilderContext(AchievementBuilderContext parent)
            : base(parent.Achievement)
        {
            Parent = parent;
        }

        public AchievementBuilderContext Parent { get; private set; }
    }

    /// <summary>
    /// <see cref="TriggerBuilderContext"/> for building a value.
    /// </summary>
    internal class ValueBuilderContext : TriggerBuilderContext
    {
    }

    /// <summary>
    /// <see cref="TriggerBuilderContext"/> for building a subclause to be used in a once/repeated/tally
    /// </summary>
    internal class TallyBuilderContext : TriggerBuilderContext
    {
    }
}