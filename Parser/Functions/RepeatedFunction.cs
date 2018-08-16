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

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            return Evaluate(scope, (ushort)count.Value, out result);
        }

        protected bool Evaluate(InterpreterScope scope, ushort count, out ExpressionBase result)
        { 
            var logicalComparison = GetParameter(scope, "comparison", out result) as ConditionalExpression;
            if (logicalComparison != null && logicalComparison.Operation == ConditionalOperation.Or)
            {
                if (!EvaluateAddHits(scope, logicalComparison, out result))
                    return false;
            }
            else
            {
                if (!base.Evaluate(scope, out result))
                    return false;
            }

            var context = scope.GetContext<TriggerBuilderContext>();
            context.LastRequirement.HitCount = count;
            return true;
        }

        protected override void ModifyRequirements(ScriptInterpreterAchievementBuilder builder)
        {
            // we want to set the HitCount on the last requirement, but don't know what to set it to. will modify back in Evaluate
        }

        private bool EvaluateAddHits(InterpreterScope scope, ConditionalExpression condition, out ExpressionBase result)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            if (context == null)
            {
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a trigger clause");
                return false;
            }

            var builder = new ScriptInterpreterAchievementBuilder();
            builder.CoreRequirements.Add(new Requirement()); // empty core requirement required for optimize call, we'll ignore it
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, condition, scope, out result))
                return false;

            var requirements = new List<Requirement>();
            foreach (var altGroup in builder.AlternateRequirements)
            {
                if (!ValidateSingleCondition(condition, altGroup, out result))
                    return false;

                var requirement = altGroup.First();
                if (requirement.Type != RequirementType.None)
                {
                    result = new ParseErrorExpression("modifier not allowed in multi-condition repeated clause");
                    return false;
                }

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

            return true;
        }
    }
}
