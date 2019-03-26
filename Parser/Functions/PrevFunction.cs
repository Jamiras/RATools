using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class PrevFunction : TriggerBuilderContext.FunctionDefinition
    {
        public PrevFunction()
            : base("prev")
        {
            Parameters.Add(new VariableDefinitionExpression("accessor"));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var accessor = GetMemoryAccessorParameter(scope, "accessor", out result);
            if (accessor == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { accessor });
            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var accessor = (FunctionCallExpression)functionCall.Parameters.First();
            var error = context.CallFunction(accessor, scope);
            if (error != null)
                return error;

            var left = context.LastRequirement.Left;
            context.LastRequirement.Left = new Field { Size = left.Size, Type = FieldType.PreviousValue, Value = left.Value };
            return null;
        }
    }
}
