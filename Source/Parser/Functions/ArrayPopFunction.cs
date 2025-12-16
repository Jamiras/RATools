using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class ArrayPopFunction : FunctionDefinitionExpression
    {
        public ArrayPopFunction()
            : base("array_pop")
        {
            Parameters.Add(new VariableDefinitionExpression("array") { IsMutableReference = true });
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var array = GetArrayParameter(scope, "array", out result);
            if (array == null)
                return false;

            if (array.Entries.Count == 0)
            {
                result = new IntegerConstantExpression(0);
            }
            else
            {
                result = array.Entries[array.Entries.Count - 1];
                array.Entries.RemoveAt(array.Entries.Count - 1);
            }

            return true;
        }
    }
}
