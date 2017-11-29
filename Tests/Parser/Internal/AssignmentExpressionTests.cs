using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class AssignmentExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new AssignmentExpression(variable, value);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("variable = 99"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable = new VariableExpression("variable");
            var variable2 = new VariableExpression("variable2");
            var value = new IntegerConstantExpression(99);
            var expr = new AssignmentExpression(variable, variable2);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable2, value);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)result).Variable, Is.EqualTo(variable));
            Assert.That(((AssignmentExpression)result).Value, Is.EqualTo(value));
        }
    }
}
