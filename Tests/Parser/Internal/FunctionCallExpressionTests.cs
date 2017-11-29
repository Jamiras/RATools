using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;
using System.Linq;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class FunctionCallExpressionTests
    {
        [Test]
        public void TestAppendStringEmpty()
        {
            var expr = new FunctionCallExpression("func", new ExpressionBase[0]);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("func()"));
        }

        [Test]
        public void TestAppendString()
        {
            var expr = new FunctionCallExpression("func", new ExpressionBase[] {
                new VariableExpression("a"),
                new IntegerConstantExpression(1),
                new StringConstantExpression("b"),
                new FunctionCallExpression("func2", new ExpressionBase[0])
            });

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("func(a, 1, \"b\", func2())"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(98);
            var value2 = new IntegerConstantExpression(99);
            var value3 = new IntegerConstantExpression(3);
            var expr = new FunctionCallExpression("func", new ExpressionBase[] {
                variable1, value3, variable2
            });

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value2);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<FunctionCallExpression>());
            var funcResult = (FunctionCallExpression)result;
            Assert.That(funcResult.FunctionName, Is.EqualTo(expr.FunctionName));
            Assert.That(funcResult.Parameters.First(), Is.EqualTo(value1));
            Assert.That(funcResult.Parameters.ElementAt(1), Is.EqualTo(value3));
            Assert.That(funcResult.Parameters.ElementAt(2), Is.EqualTo(value2));
        }
    }
}
