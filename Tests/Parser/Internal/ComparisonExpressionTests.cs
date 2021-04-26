using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Functions;
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
        [TestCase("variable1 > variable2", "98 > 99")] // simple variable substitution
        [TestCase("1 == byte(2)", "byte(2) == 1")] // move constant to right side
        [TestCase("1 != byte(2)", "byte(2) != 1")] // move constant to right side
        [TestCase("1 < byte(2)", "byte(2) > 1")] // move constant to right side
        [TestCase("1 <= byte(2)", "byte(2) >= 1")] // move constant to right side
        [TestCase("1 > byte(2)", "byte(2) < 1")] // move constant to right side
        [TestCase("1 >= byte(2)", "byte(2) <= 1")] // move constant to right side
        [TestCase("byte(1) + 1 < byte(2) + 1", "byte(1) < byte(2)")] // same modifier on both sides can be eliminated
        [TestCase("byte(1) + 6 < byte(2) + 3", "byte(1) + 3 < byte(2)")] // differing modifier should be merged
        [TestCase("byte(1) + variable1 < byte(2) + 3", "byte(1) + 95 < byte(2)")] // differing modifier should be merged
        [TestCase("byte(1) < byte(2) + 1", "byte(1) - byte(2) + 256 < 257")] // underflow check needed for relative comparison
        [TestCase("byte(1) == byte(2) + 1", "byte(1) - 1 == byte(2)")] // underflow check not needed for equality comparison
        [TestCase("byte(1) * 10 == 100", "byte(1) == 10")] // factor out multiplication
        [TestCase("byte(1) * 10 == 99", "Result can never be true using integer math")] // multiplication cannot be factored out
        [TestCase("byte(1) * 10 != 100", "byte(1) != 10")] // factor out multiplication
        [TestCase("byte(1) * 10 != 99", "Result is always true using integer math")] // multiplication cannot be factored out
        [TestCase("byte(1) * 10 < 99", "byte(1) <= 9")] // factor out multiplication - become less than or equal
        [TestCase("byte(1) * 10 < 90", "byte(1) < 9")] // factor out multiplication - does not become less than or equal
        [TestCase("byte(1) * 10 <= 99", "byte(1) <= 9")] // factor out multiplication
        [TestCase("byte(1) * 10 > 99", "byte(1) > 9")] // factor out multiplication
        [TestCase("byte(1) * 10 >= 99", "byte(1) > 9")] // factor out multiplication - becomes greater than
        [TestCase("byte(1) * 10 >= 90", "byte(1) >= 9")] // factor out multiplication - does not become greater than
        [TestCase("byte(1) / 10 < 9", "byte(1) < 90")] // factor out division
        [TestCase("byte(1) * 10 * 2 == 100", "byte(1) == 5")] // factor out multiplication
        [TestCase("2 * byte(1) * 10 == 100", "byte(1) == 5")] // factor out multiplication
        [TestCase("byte(1) * 10 / 2 == 100", "byte(1) == 20")] // factor out multiplication and division
        [TestCase("byte(1) * 10 / 3 == 100", "byte(1) == 30")] // factor out multiplication and division
        [TestCase("byte(1) * 10 + 10 == 100", "byte(1) == 9")] // factor out multiplication and addition
        [TestCase("byte(1) * 10 - 10 == 100", "byte(1) == 11")] // factor out multiplication and subtraction
        [TestCase("(byte(1) - 1) * 10 == 100", "byte(1) == 11")] // factor out multiplication and subtraction
        [TestCase("(byte(1) - 1) / 10 == 10", "byte(1) == 101")] // factor out division and subtraction
        [TestCase("(byte(1) - 1) * 10 < 99", "byte(1) <= 10")] // factor out division and subtraction
        [TestCase("byte(1) * 10 + byte(2) == 100", "byte(1) * 10 + byte(2) == 100")] // multiplication cannot be factored out
        [TestCase("byte(1) * 10 == byte(2)", "Cannot eliminate division from right side of comparison")] // multiplication cannot be factored out
        [TestCase("byte(2) + 1 == variable1", "byte(2) == 97")] // differing modifier should be merged
        [TestCase("variable1 == byte(2) + 1", "byte(2) == 97")] // differing modifier should be merged, move constant to right side
        public void TestReplaceVariables(string input, string expected)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope();
            scope.Context = new RATools.Parser.TriggerBuilderContext();
            scope.AssignVariable(new VariableExpression("variable1"), new IntegerConstantExpression(98));
            scope.AssignVariable(new VariableExpression("variable2"), new IntegerConstantExpression(99));
            scope.AddFunction(new MemoryAccessorFunction("byte", RATools.Data.FieldSize.Byte));

            ExpressionBase result;
            if (!expr.ReplaceVariables(scope, out result))
                Assert.That(result, Is.InstanceOf<ParseErrorExpression>());

            var builder = new StringBuilder();
            result.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
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
