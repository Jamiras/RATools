using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class RepeatedFunction : ComparisonModificationFunction
    {
        public RepeatedFunction()
            : base("repeated")
        {
            Parameters.Clear();
            Parameters.Add(new VariableDefinitionExpression("count"));
            Parameters.Add(new VariableDefinitionExpression("comparison"));
        }

        protected RepeatedFunction(string name)
            : base(name)
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!IsInTriggerClause(scope, out result))
                return false;

            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { count, comparison });
            CopyLocation(result);
            return true;
        }

        protected override ParseErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            // not actually called because we override BuildTrigger, but required because its abstract in the base class
            return null;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var count = (IntegerConstantExpression)functionCall.Parameters.First();
            var comparison = functionCall.Parameters.ElementAt(1);

            return BuildTriggerConditions(context, scope, comparison, count.Value);
        }

        protected ParseErrorExpression BuildTriggerConditions(TriggerBuilderContext context, InterpreterScope scope, ExpressionBase comparison, int count)
        {
            ParseErrorExpression error;

            var logicalComparison = comparison as ConditionalExpression;
            if (logicalComparison != null && logicalComparison.Operation == ConditionalOperation.Or)
                error = EvaluateAddHits(context, scope, logicalComparison);
            else
                error = BuildTriggerCondition(context, scope, comparison);

            if (error != null)
                return error;

            context.LastRequirement.HitCount = (ushort)count;
            return null;
        }

        private ParseErrorExpression EvaluateAddHits(TriggerBuilderContext context, InterpreterScope scope, ConditionalExpression condition)
        {
            ExpressionBase result;

            var builder = new ScriptInterpreterAchievementBuilder();
            builder.CoreRequirements.Add(new Requirement()); // empty core requirement required for optimize call, we'll ignore it
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, condition, scope, out result))
                return (ParseErrorExpression)result;

            var requirements = new List<Requirement>();
            foreach (var altGroup in builder.AlternateRequirements)
            {
                var error = ValidateSingleCondition(altGroup);
                if (error != null)
                    return error;

                var requirement = altGroup.First();
                if (requirement.Type != RequirementType.None)
                    return new ParseErrorExpression("modifier not allowed in multi-condition repeated clause");

                requirements.Add(requirement);
            }

            // the last item cannot have its own HitCount as it will hold the HitCount for the group. 
            // if necessary, find one without a HitCount and make it the last. 
            int index = requirements.Count - 1;
            if (requirements[index].HitCount > 0)
            {
                do
                {
                    index--;
                } while (index >= 0 && requirements[index].HitCount > 0);
                    
                if (index == -1)
                {
                    // all requirements had HitCount limits, add a dummy item that's never true for the total HitCount
                    requirements.Add(new Requirement
                    {
                        Left = new Field { Type = FieldType.Value, Value = 1 },
                        Operator = RequirementOperator.Equal,
                        Right = new Field { Type = FieldType.Value, Value = 0 }
                    });
                }
                else
                {
                    // found a requirement without a HitCount limit, move it to the last spot for the total HitCount
                    var requirement = requirements[index];
                    requirements.RemoveAt(index);
                    requirements.Add(requirement);
                }
            }

            // everything but the last becomes an AddHits
            for (int i = 0; i < requirements.Count - 1; i++)
                requirements[i].Type = RequirementType.AddHits;

            // load the requirements into the trigger
            foreach (var requirement in requirements)
                context.Trigger.Add(requirement);

            return null;
        }
    }
}
