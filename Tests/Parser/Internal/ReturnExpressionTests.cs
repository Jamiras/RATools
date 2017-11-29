using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class ReturnExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new ReturnExpression(new VariableExpression("variable"));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("return variable"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new ReturnExpression(variable);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, value);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<ReturnExpression>());
            Assert.That(((ReturnExpression)result).Value, Is.EqualTo(value));
        }
    }
}
