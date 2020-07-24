using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;
using System.Text;

namespace RATools.Parser.Functions
{
    internal class MemoryAccessorFunction : TriggerBuilderContext.FunctionDefinition
    {
        public MemoryAccessorFunction(string name, FieldSize size)
            : base(name)
        {
            Size = size;

            Parameters.Add(new VariableDefinitionExpression("address"));
        }

        public FieldSize Size { get; private set; }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!IsInTriggerClause(scope, out result))
                return false;

            var address = GetParameter(scope, "address", out result);
            if (address == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { address });
            CopyLocation(result);
            return true;
        }

        public static bool ContainsMemoryAccessor(ExpressionBase expression)
        {
            var funcCall = expression as FunctionCallExpression;
            if (funcCall != null)
            {
                var func = AchievementScriptInterpreter.GetGlobalScope().GetFunction(funcCall.FunctionName.Name);
                if (func is MemoryAccessorFunction)
                    return true;

                foreach (var parameter in funcCall.Parameters)
                {
                    if (ContainsMemoryAccessor(parameter))
                        return true;
                }

                return false;
            }

            var leftRightExpression = expression as LeftRightExpressionBase;
            if (leftRightExpression != null)
                return ContainsMemoryAccessor(leftRightExpression.Left) || ContainsMemoryAccessor(leftRightExpression.Right);

            return false;
        }

        private static int CountMathematicMemoryAccessors(ExpressionBase expression, int limit)
        {
            var mathematic = expression as MathematicExpression;
            if (mathematic != null)
            {
                var count = CountMathematicMemoryAccessors(mathematic.Left, limit);
                if (count < limit)
                    count += CountMathematicMemoryAccessors(mathematic.Right, limit);

                return count;
            }

            if (ContainsMemoryAccessor(expression))
                return 1;

            return 0;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var requirement = new Requirement();
            var address = functionCall.Parameters.First();

            var integerConstant = address as IntegerConstantExpression;
            if (integerConstant != null)
            {
                requirement.Left = new Field { Size = this.Size, Type = FieldType.MemoryAddress, Value = (uint)integerConstant.Value };
                context.Trigger.Add(requirement);
                return null;
            }

            var funcCall = address as FunctionCallExpression;
            if (funcCall != null)
            {
                // a memory reference without an offset has to be generated with a 0 offset.
                integerConstant = new IntegerConstantExpression(0);
            }
            else
            {
                var mathematic = address as MathematicExpression;
                if (mathematic != null &&
                    (mathematic.Operation == MathematicOperation.Add || mathematic.Operation == MathematicOperation.Subtract))
                {
                    if (CountMathematicMemoryAccessors(mathematic, 2) >= 2)
                        return new ParseErrorExpression("Cannot construct single address lookup from multiple memory references", address);

                    integerConstant = mathematic.Right as IntegerConstantExpression;
                    if (integerConstant != null)
                    {
                        funcCall = mathematic.Left as FunctionCallExpression;
                    }
                    else
                    {
                        integerConstant = mathematic.Left as IntegerConstantExpression;
                        if (integerConstant != null)
                            funcCall = mathematic.Right as FunctionCallExpression;
                    }

                    if (integerConstant != null)
                    {
                        if (mathematic.Operation == MathematicOperation.Subtract)
                            integerConstant = new IntegerConstantExpression(-integerConstant.Value);

                        if (integerConstant.Value < 0)
                        {
                            // Negative relative offsets can actually be handled by the runtime through overflow
                            // addition, but the editor generates an error if the offset is larger than the
                            // available memory space for the current system.
                            return new ParseErrorExpression("Negative relative offset not supported", address);
                        }
                    }
                }
            }

            if (funcCall != null)
            {
                var funcDef = scope.GetFunction(funcCall.FunctionName.Name) as TriggerBuilderContext.FunctionDefinition;
                if (funcDef != null)
                {
                    if (funcDef is MemoryAccessorFunction || funcDef is PrevPriorFunction)
                    {
                        var error = funcDef.BuildTrigger(context, scope, funcCall);
                        if (error != null)
                            return error;

                        context.LastRequirement.Type = RequirementType.AddAddress;

                        requirement.Left = new Field { Size = this.Size, Type = FieldType.MemoryAddress, Value = (uint)integerConstant.Value };
                        context.Trigger.Add(requirement);
                        return null;
                    }
                }
            }

            var builder = new StringBuilder();
            builder.Append("Cannot convert to an address: ");
            address.AppendString(builder);

            return new ParseErrorExpression(builder.ToString(), address);
        }
    }
}
