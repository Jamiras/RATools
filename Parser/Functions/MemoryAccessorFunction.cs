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

            IntegerConstantExpression offsetConstant = null;
            IntegerConstantExpression scalarConstant = null;

            var funcCall = address as FunctionCallExpression;
            if (funcCall == null)
            {
                var mathematic = address as MathematicExpression;
                if (mathematic != null &&
                    (mathematic.Operation == MathematicOperation.Add || mathematic.Operation == MathematicOperation.Subtract))
                {
                    if (CountMathematicMemoryAccessors(mathematic, 2) >= 2)
                        return new ParseErrorExpression("Cannot construct single address lookup from multiple memory references", address);

                    offsetConstant = mathematic.Right as IntegerConstantExpression;
                    if (offsetConstant != null)
                    {
                        address = mathematic.Left;
                    }
                    else
                    {
                        offsetConstant = mathematic.Left as IntegerConstantExpression;
                        if (offsetConstant != null)
                            address = mathematic.Right;
                    }

                    if (offsetConstant != null)
                    {
                        if (mathematic.Operation == MathematicOperation.Subtract)
                            offsetConstant = new IntegerConstantExpression(-offsetConstant.Value);

                        if (offsetConstant.Value < 0)
                        {
                            // Negative relative offsets can actually be handled by the runtime through overflow
                            // addition, but the editor generates an error if the offset is larger than the
                            // available memory space for the current system.
                            return new ParseErrorExpression("Negative relative offset not supported", address);
                        }
                    }

                    mathematic = address as MathematicExpression;
                }

                if (mathematic != null && mathematic.Operation == MathematicOperation.Multiply)
                {
                    scalarConstant = mathematic.Right as IntegerConstantExpression;
                    if (scalarConstant != null)
                    {
                        address = mathematic.Left;
                    }
                    else
                    {
                        scalarConstant = mathematic.Left as IntegerConstantExpression;
                        if (scalarConstant != null)
                            address = mathematic.Right;
                    }
                }

                funcCall = address as FunctionCallExpression;
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

                        var lastRequirement = context.LastRequirement;
                        lastRequirement.Type = RequirementType.AddAddress;

                        if (scalarConstant != null && scalarConstant.Value != 1)
                        {
                            lastRequirement.Operator = RequirementOperator.Multiply;
                            lastRequirement.Right = new Field { Size = FieldSize.DWord, Type = FieldType.Value, Value = (uint)scalarConstant.Value };
                        }

                        // a memory reference without an offset has to be generated with a 0 offset.
                        uint offset = (offsetConstant != null) ? (uint)offsetConstant.Value : 0;

                        requirement.Left = new Field { Size = this.Size, Type = FieldType.MemoryAddress, Value = offset };
                        context.Trigger.Add(requirement);
                        return null;
                    }
                }
            }

            address = functionCall.Parameters.First();

            var builder = new StringBuilder();
            builder.Append("Cannot convert to an address: ");
            address.AppendString(builder);

            return new ParseErrorExpression(builder.ToString(), address);
        }
    }
}
