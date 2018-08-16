using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class OnceFunction : RepeatedFunction
    {
        public OnceFunction()
            : base("once")
        {
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, 1, out result);
        }
    }
}
