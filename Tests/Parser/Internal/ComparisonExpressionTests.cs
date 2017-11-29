using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class ComparisonExpressionTests
    {
        [Test]
        [TestCase(ComparisonOperation.Equal, "variable == 99")]
        [TestCase(ComparisonOperation.NotEqual, "variable != 99")]
        [TestCase(ComparisonOperation.LessThan, "variable < 99")]
        [TestCase(ComparisonOperation.LessThanOrEqual, "variable <= 99")]
        [TestCase(ComparisonOperation.GreaterThan, "variable > 99")]
        [TestCase(ComparisonOperation.GreaterThanOrEqual, "variable >= 99")]
        public void TestAppendString(ComparisonOperation op, string expected)
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new ComparisonExpression(variable, op, value);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(98);
            var value2 = new IntegerConstantExpression(99);
            var expr = new ComparisonExpression(variable1, ComparisonOperation.GreaterThan, variable2);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value2);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<ComparisonExpression>());
            Assert.That(((ComparisonExpression)result).Left, Is.EqualTo(value1));
            Assert.That(((ComparisonExpression)result).Operation, Is.EqualTo(expr.Operation));
            Assert.That(((ComparisonExpression)result).Right, Is.EqualTo(value2));
        }

        [Test]
        public void TestRebalance()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new ComparisonExpression(variable, ComparisonOperation.LessThan, value);

            var result = expr.Rebalance() as ComparisonExpression;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Left, Is.EqualTo(expr.Left));
            Assert.That(result.Operation, Is.EqualTo(expr.Operation));
            Assert.That(result.Right, Is.EqualTo(expr.Right));
        }

        [Test]
        public void TestRebalanceConditional()
        {
            // "A < B && C" => "(A < B) && C"
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value = new IntegerConstantExpression(99);
            var conditional = new ConditionalExpression(value, ConditionalOperation.And, variable2);
            var expr = new ComparisonExpression(variable1, ComparisonOperation.LessThan, conditional);

            var result = expr.Rebalance() as ConditionalExpression;
            Assert.That(result, Is.Not.Null);
            var expected = new ComparisonExpression(expr.Left, expr.Operation, value);
            Assert.That(result.Left, Is.EqualTo(expected));
            Assert.That(result.Operation, Is.EqualTo(ConditionalOperation.And));
            Assert.That(result.Right, Is.EqualTo(variable2));
        }

        [Test]
        public void TestRebalanceMathematical()
        {
            // "A < B + C" => "A < (B + C)"
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value = new IntegerConstantExpression(99);
            var conditional = new MathematicExpression(value, MathematicOperation.Add, variable2);
            var expr = new ComparisonExpression(variable1, ComparisonOperation.LessThan, conditional);

            var result = expr.Rebalance() as ComparisonExpression;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Left, Is.EqualTo(expr.Left));
            Assert.That(result.Operation, Is.EqualTo(expr.Operation));
            Assert.That(result.Right, Is.EqualTo(expr.Right));
        }
    }
}
