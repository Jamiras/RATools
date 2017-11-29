using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class MathematicExpressionTests
    {
        [Test]
        [TestCase(MathematicOperation.Add, "variable + 99")]
        [TestCase(MathematicOperation.Subtract, "variable - 99")]
        [TestCase(MathematicOperation.Multiply, "variable * 99")]
        [TestCase(MathematicOperation.Divide, "variable / 99")]
        public void TestAppendString(MathematicOperation op, string expected)
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new MathematicExpression(variable, op, value);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestRebalance()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new MathematicExpression(variable, MathematicOperation.Add, value);

            var result = expr.Rebalance() as MathematicExpression;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Left, Is.EqualTo(expr.Left));
            Assert.That(result.Operation, Is.EqualTo(expr.Operation));
            Assert.That(result.Right, Is.EqualTo(expr.Right));
        }

        [Test]
        [TestCase(MathematicOperation.Add, MathematicOperation.Add, false)]
        [TestCase(MathematicOperation.Add, MathematicOperation.Subtract, false)]
        [TestCase(MathematicOperation.Add, MathematicOperation.Multiply, false)]
        [TestCase(MathematicOperation.Add, MathematicOperation.Divide, false)]
        [TestCase(MathematicOperation.Subtract, MathematicOperation.Add, false)]
        [TestCase(MathematicOperation.Subtract, MathematicOperation.Subtract, false)]
        [TestCase(MathematicOperation.Subtract, MathematicOperation.Multiply, false)]
        [TestCase(MathematicOperation.Subtract, MathematicOperation.Divide, false)]
        [TestCase(MathematicOperation.Multiply, MathematicOperation.Add, true)]
        [TestCase(MathematicOperation.Multiply, MathematicOperation.Subtract, true)]
        [TestCase(MathematicOperation.Multiply, MathematicOperation.Multiply, false)]
        [TestCase(MathematicOperation.Multiply, MathematicOperation.Divide, false)]
        [TestCase(MathematicOperation.Divide, MathematicOperation.Add, true)]
        [TestCase(MathematicOperation.Divide, MathematicOperation.Subtract, true)]
        [TestCase(MathematicOperation.Divide, MathematicOperation.Multiply, false)]
        [TestCase(MathematicOperation.Divide, MathematicOperation.Divide, false)]
        public void TestRebalanceMathematic(MathematicOperation op1, MathematicOperation op2, bool rebalanceExpected)
        {
            // if the left operator is a multiple or divide and the right is an add or subtract,
            // the left operator takes precedence and tree should be rebalanced.
            //
            //   A * B + C => (A * B) + C
            //   A + B * C => A + (B * C)
            //
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value = new IntegerConstantExpression(99);
            var clause = new MathematicExpression(variable2, op2, value);
            var expr = new MathematicExpression(variable1, op1, clause);

            var result = expr.Rebalance() as MathematicExpression;
            Assert.That(result, Is.Not.Null);
            if (rebalanceExpected)
            {
                var expectedLeft = new MathematicExpression(variable1, op1, variable2);
                Assert.That(result.Left, Is.EqualTo(expectedLeft));
                Assert.That(result.Operation, Is.EqualTo(op2));
                Assert.That(result.Right, Is.EqualTo(value));
            }
            else
            {
                Assert.That(result.Left, Is.EqualTo(expr.Left));
                Assert.That(result.Operation, Is.EqualTo(expr.Operation));
                Assert.That(result.Right, Is.EqualTo(expr.Right));
            }
        }

        [Test]
        public void TestRebalanceComparison()
        {
            // A + B < C => (A + B) < C
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value = new IntegerConstantExpression(99);
            var clause = new ComparisonExpression(variable2, ComparisonOperation.LessThan, value);
            var expr = new MathematicExpression(variable1, MathematicOperation.Add, clause);

            var result = expr.Rebalance() as ComparisonExpression;
            Assert.That(result, Is.Not.Null);
            var expectedLeft = new MathematicExpression(variable1, MathematicOperation.Add, variable2);
            Assert.That(result.Left, Is.EqualTo(expectedLeft));
            Assert.That(result.Operation, Is.EqualTo(clause.Operation));
            Assert.That(result.Right, Is.EqualTo(value));
        }

        [Test]
        public void TestRebalanceCondition()
        {
            // A + B && C => (A + B) && C
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value = new IntegerConstantExpression(99);
            var clause = new ConditionalExpression(variable2, ConditionalOperation.And, value);
            var expr = new MathematicExpression(variable1, MathematicOperation.Add, clause);

            var result = expr.Rebalance() as ConditionalExpression;
            Assert.That(result, Is.Not.Null);
            var expectedLeft = new MathematicExpression(variable1, MathematicOperation.Add, variable2);
            Assert.That(result.Left, Is.EqualTo(expectedLeft));
            Assert.That(result.Operation, Is.EqualTo(clause.Operation));
            Assert.That(result.Right, Is.EqualTo(value));
        }

        [Test]
        public void TestAdd()
        {
            var left = new IntegerConstantExpression(1);
            var right = new IntegerConstantExpression(2);
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(3));
        }

        [Test]
        public void TestAddVariables()
        {
            var value1 = new IntegerConstantExpression(1);
            var value2 = new IntegerConstantExpression(2);
            var left = new VariableExpression("left");
            var right = new VariableExpression("right");
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();
            scope.AssignVariable(left, value1);
            scope.AssignVariable(right, value2);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(3));
        }

        [Test]
        public void TestAddStrings()
        {
            var left = new StringConstantExpression("ban");
            var right = new StringConstantExpression("ana");
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo("banana"));
        }

        [Test]
        public void TestAddStringNumber()
        {
            var left = new StringConstantExpression("ban");
            var right = new IntegerConstantExpression(2);
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo("ban2"));
        }

        [Test]
        public void TestAddNumberString()
        {
            var left = new IntegerConstantExpression(1);
            var right = new StringConstantExpression("ana");
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo("1ana"));
        }

        [Test]
        public void TestSubtract()
        {
            var left = new IntegerConstantExpression(8);
            var right = new IntegerConstantExpression(2);
            var expr = new MathematicExpression(left, MathematicOperation.Subtract, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(6));
        }

        [Test]
        public void TestMultiply()
        {
            var left = new IntegerConstantExpression(7);
            var right = new IntegerConstantExpression(3);
            var expr = new MathematicExpression(left, MathematicOperation.Multiply, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(21));
        }

        [Test]
        public void TestDivide()
        {
            var left = new IntegerConstantExpression(20);
            var right = new IntegerConstantExpression(3);
            var expr = new MathematicExpression(left, MathematicOperation.Divide, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(6));
        }
    }
}
