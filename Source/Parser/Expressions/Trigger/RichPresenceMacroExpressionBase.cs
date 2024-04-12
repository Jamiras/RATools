namespace RATools.Parser.Expressions.Trigger
{
    internal abstract class RichPresenceMacroExpressionBase : ExpressionBase
    {
        protected RichPresenceMacroExpressionBase(StringConstantExpression name, ExpressionBase parameter)
            : base(ExpressionType.RichPresenceMacro)
        {
            Name = name;
            Parameter = parameter;
        }

        public abstract string FunctionName { get; }

        public StringConstantExpression Name { get; private set; }

        public ExpressionBase Parameter { get; private set; }

        public abstract ErrorExpression Attach(RichPresenceBuilder builder);
    }
}
