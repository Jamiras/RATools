using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Tests.Parser.Expressions
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
        [TestCase("byte(1) > variable2", "byte(1) > 99")] // simple variable substitution
        [TestCase("variable1 > variable2", "false")] // evaluates to a constant
        [TestCase("byte(1) * 10 / 3 == 100", "byte(1) == 30")] // factor out multiplication and division
        [TestCase("byte(1) * 10 + 10 == 100", "byte(1) == 9")] // factor out multiplication and addition
        [TestCase("byte(1) * 10 - 10 == 100", "byte(1) == 11")] // factor out multiplication and subtraction
        [TestCase("(byte(1) - 1) * 10 == 100", "byte(1) == 11")] // factor out multiplication and subtraction
        [TestCase("(byte(1) - 1) / 10 == 10", "byte(1) == 101")] // factor out division and subtraction
        [TestCase("(byte(1) - 1) * 10 < 99", "byte(1) <= 10")] // factor out division and subtraction
        [TestCase("byte(1) + variable1 < byte(2) + 3", "byte(1) + 95 < byte(2)")] // differing modifier should be merged
        [TestCase("byte(2) + 1 == variable1", "byte(2) == 97")] // differing modifier should be merged
        [TestCase("variable1 == byte(2) + 1", "byte(2) == 97")] // differing modifier should be merged, move constant to right side
        [TestCase("0 + byte(1) + 0 == 9", "byte(1) == 9")] // 0s should be removed without reordering
        [TestCase("0 + byte(1) - 9 == 0", "byte(1) == 9")] // 9 should be moved to right hand side, then 0s removed

        [TestCase("bcd(byte(1)) == 24", "byte(1) == 36")] // bcd should be factored out
        [TestCase("byte(1) != bcd(byte(2))", "byte(1) != bcd(byte(2))")] // bcd cannot be factored out
        [TestCase("bcd(byte(1)) != prev(bcd(byte(1)))", "byte(1) != prev(byte(1))")] // bcd should be factored out
        public void TestReplaceVariables(string input, string expected)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();
            scope.AssignVariable(new VariableExpression("variable1"), new IntegerConstantExpression(98));
            scope.AssignVariable(new VariableExpression("variable2"), new IntegerConstantExpression(99));

            ExpressionBase result;
            if (!expr.ReplaceVariables(scope, out result))
                Assert.That(result, Is.InstanceOf<ErrorExpression>());

            var builder = new StringBuilder();
            result.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("0 == 0", true)]
        [TestCase("0 == 1", false)]
        [TestCase("byte(0) == 0", null)]
        [TestCase("0 == 0.0", true)]
        [TestCase("0 == 1.0", false)]
        [TestCase("0.0 == 0.0", true)]
        [TestCase("0.0 == 1.0", false)]
        [TestCase("0.0 == 0", true)]
        [TestCase("0.0 == 1", false)]
        [TestCase("1 == 1", true)]
        [TestCase("1 != 1", false)]
        [TestCase("1 < 1", false)]
        [TestCase("1 <= 1", true)]
        [TestCase("1 > 1", false)]
        [TestCase("1 >= 1", true)]
        [TestCase("1 == 2", false)]
        [TestCase("1 != 2", true)]
        [TestCase("1 < 2", true)]
        [TestCase("1 <= 2", true)]
        [TestCase("1 > 2", false)]
        [TestCase("1 >= 2", false)]
        [TestCase("2 == 1", false)]
        [TestCase("2 != 1", true)]
        [TestCase("2 < 1", false)]
        [TestCase("2 <= 1", false)]
        [TestCase("2 > 1", true)]
        [TestCase("2 >= 1", true)]
        [TestCase("1.2 == 1.2", true)]
        [TestCase("1.2 != 1.2", false)]
        [TestCase("1.2 < 1.2", false)]
        [TestCase("1.2 <= 1.2", true)]
        [TestCase("1.2 > 1.2", false)]
        [TestCase("1.2 >= 1.2", true)]
        [TestCase("1.2 == 1.3", false)]
        [TestCase("1.2 != 1.3", true)]
        [TestCase("1.2 < 1.3", true)]
        [TestCase("1.2 <= 1.3", true)]
        [TestCase("1.2 > 1.3", false)]
        [TestCase("1.2 >= 1.3", false)]
        [TestCase("1.3 == 1.2", false)]
        [TestCase("1.3 != 1.2", true)]
        [TestCase("1.3 < 1.2", false)]
        [TestCase("1.3 <= 1.2", false)]
        [TestCase("1.3 > 1.2", true)]
        [TestCase("1.3 >= 1.2", true)]
        [TestCase("1.2 == 1", false)]
        [TestCase("1.2 != 1", true)]
        [TestCase("1.2 < 1", false)]
        [TestCase("1.2 <= 1", false)]
        [TestCase("1.2 > 1", true)]
        [TestCase("1.2 >= 1", true)]
        [TestCase("true == true", true)]
        [TestCase("true == false", false)]
        [TestCase("false == false", true)]
        [TestCase("false == true", false)]
        [TestCase("true != true", false)]
        [TestCase("true != false", true)]
        [TestCase("false != false", false)]
        [TestCase("false != true", true)]
        [TestCase("\"bbb\" == \"bbb\"", true)]
        [TestCase("\"bbb\" != \"bbb\"", false)]
        [TestCase("\"bbb\" < \"bbb\"", false)]
        [TestCase("\"bbb\" <= \"bbb\"", true)]
        [TestCase("\"bbb\" > \"bbb\"", false)]
        [TestCase("\"bbb\" >= \"bbb\"", true)]
        [TestCase("\"bbb\" == \"bba\"", false)]
        [TestCase("\"bbb\" != \"bba\"", true)]
        [TestCase("\"bbb\" < \"bba\"", false)]
        [TestCase("\"bbb\" <= \"bba\"", false)]
        [TestCase("\"bbb\" > \"bba\"", true)]
        [TestCase("\"bbb\" >= \"bba\"", true)]
        [TestCase("\"bba\" == \"bbb\"", false)]
        [TestCase("\"bba\" != \"bbb\"", true)]
        [TestCase("\"bba\" < \"bbb\"", true)]
        [TestCase("\"bba\" <= \"bbb\"", true)]
        [TestCase("\"bba\" > \"bbb\"", false)]
        [TestCase("\"bba\" >= \"bbb\"", false)]
        [TestCase("\"bbb\" == \"bbbb\"", false)]
        [TestCase("\"bbb\" != \"bbbb\"", true)]
        [TestCase("\"bbb\" < \"bbbb\"", true)]
        [TestCase("\"bbb\" <= \"bbbb\"", true)]
        [TestCase("\"bbb\" > \"bbbb\"", false)]
        [TestCase("\"bbb\" >= \"bbbb\"", false)]
        [TestCase("\"bbb\" == 0", null)]
        [TestCase("\"bbb\" == -2.0", null)]
        [TestCase("1 == \"bbb\"", null)]
        [TestCase("2.0 == -2.0", false)]
        public void TestIsTrue(string input, bool? expected)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();
            ErrorExpression error;
            Assert.That(expr.IsTrue(scope, out error), Is.EqualTo(expected));
            Assert.That(error, Is.Null);
        }
    }
}
