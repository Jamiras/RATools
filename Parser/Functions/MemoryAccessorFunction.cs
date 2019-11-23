using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

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
                if (mathematic != null && mathematic.Operation == MathematicOperation.Add)
                {
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

            return new ParseErrorExpression("Cannot convert to an address", address);
        }
    }
}
