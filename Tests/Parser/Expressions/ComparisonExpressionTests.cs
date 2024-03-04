using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Tests.Expressions
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
        [TestCase("byte(1) > variable2", "byte(0x000001) > 99")] // simple variable substitution
        [TestCase("variable1 > variable2", "false")] // evaluates to a constant
        [TestCase("byte(1) * 10 / 3 == 100", "byte(0x000001) == 30")] // factor out multiplication and division
        [TestCase("byte(1) * 10 + 10 == 100", "byte(0x000001) == 9")] // factor out multiplication and addition
        [TestCase("byte(1) * 10 - 10 == 100", "byte(0x000001) == 11")] // factor out multiplication and subtraction
        [TestCase("(byte(1) - 1) * 10 == 100", "byte(0x000001) == 11")] // factor out multiplication and subtraction
        [TestCase("(byte(1) - 1) / 10 == 10", "byte(0x000001) == 101")] // factor out division and subtraction
        [TestCase("(byte(1) - 1) * 10 < 99", "byte(0x000001) <= 10")] // factor out division and subtraction
        [TestCase("byte(1) + variable1 < byte(2) + 3", "byte(0x000001) + 95 < byte(0x000002)")] // differing modifier should be merged
        [TestCase("byte(2) + 1 == variable1", "byte(0x000002) == 97")] // differing modifier should be merged
        [TestCase("variable1 == byte(2) + 1", "byte(0x000002) == 97")] // differing modifier should be merged, move constant to right side
        [TestCase("0 + byte(1) + 0 == 9", "byte(0x000001) == 9")] // 0s should be removed without reordering
        [TestCase("0 + byte(1) - 9 == 0", "byte(0x000001) == 9")] // 9 should be moved to right hand side, then 0s removed
        [TestCase("bcd(byte(1)) == 24", "byte(0x000001) == 36")] // bcd should be factored out
        [TestCase("byte(1) != bcd(byte(2))", "byte(0x000001) != bcd(byte(0x000002))")] // bcd cannot be factored out
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
        [TestCase("\"bbb\" == 0", false)]
        [TestCase("\"bbb\" == -2.0", false)]
        [TestCase("1 == \"bbb\"", false)]
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

        [Test]
        [TestCase("a == a", true, null)]  // 1 == 1
        [TestCase("a == b", false, null)] // 1 == 2
        [TestCase("a == c", true, null)]  // 1 == 1
        [TestCase("a == d", false, null)] // 1 == "1"
        [TestCase("a == x", false, null)] // 1 == [1]
        [TestCase("x == x", true, null)]  // [1] == [1]
        [TestCase("x == y", true, null)]  // [1] == [1]
        [TestCase("x == z", false, null)] // [1] == [1,2]
        [TestCase("x == a", false, null)] // [1] == 1
        [TestCase("x == d", false, null)] // [1] == "1"
        [TestCase("a != b", true, null)]  // 1 != 2
        [TestCase("a != c", false, null)] // 1 != 1
        [TestCase("a != d", true, null)]  // 1 != "1"
        [TestCase("a != x", true, null)]  // 1 != [1]
        [TestCase("a <= b", true, null)]  // 1 <= 2
        [TestCase("a <= c", true, null)]  // 1 <= 1
        [TestCase("x != z", true, null)]  // [1] != [1,2]
        [TestCase("x <  z", null, "Cannot perform relative comparison on array")] // [1] < [1,2]
        [TestCase("x >  z", null, "Cannot perform relative comparison on array")] // [1] > [1,2]
        [TestCase("x <= z", null, "Cannot perform relative comparison on array")] // [1] <= [1,2]
        [TestCase("x >= z", null, "Cannot perform relative comparison on array")] // [1] >= [1,2]
        [TestCase("a <= d", null, "Cannot compare integer and string")] // 1 <= "1"
        [TestCase("a <= x", null, "Cannot compare integer and array")]  // 1 <= [1]
        [TestCase("a == g", null, "Unknown variable: g")]
        [TestCase("g == a", null, "Unknown variable: g")]
        public void TestIsTrueVariables(string input, bool? expected, string expectedError)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();
            scope.DefineVariable(new VariableDefinitionExpression("a"), new IntegerConstantExpression(1));
            scope.DefineVariable(new VariableDefinitionExpression("b"), new IntegerConstantExpression(2));
            scope.DefineVariable(new VariableDefinitionExpression("c"), new IntegerConstantExpression(1));
            scope.DefineVariable(new VariableDefinitionExpression("d"), new StringConstantExpression("1"));

            var array1 = new ArrayExpression();
            array1.Entries.Add(new IntegerConstantExpression(1));
            scope.DefineVariable(new VariableDefinitionExpression("x"), array1);
            var array2 = new ArrayExpression();
            array2.Entries.Add(new IntegerConstantExpression(1));
            scope.DefineVariable(new VariableDefinitionExpression("y"), array2);
            var array3 = new ArrayExpression();
            array3.Entries.Add(new IntegerConstantExpression(1));
            array3.Entries.Add(new IntegerConstantExpression(2));
            scope.DefineVariable(new VariableDefinitionExpression("z"), array3);

            ErrorExpression error;
            Assert.That(expr.IsTrue(scope, out error), Is.EqualTo(expected));

            if (expectedError != null)
            {
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Is.EqualTo(expectedError));
            }
            else
            {
                Assert.That(error, Is.Null);
            }
        }

        [Test]
        public void TestCompareFunctionReferences()
        {
            var tokenizer = Tokenizer.CreateTokenizer("function a() => 1");
            var exprA = (FunctionDefinitionExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            tokenizer = Tokenizer.CreateTokenizer("function b() => 2");
            var exprB = (FunctionDefinitionExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope();
            scope.AddFunction(exprA);
            scope.AddFunction(exprB);

            tokenizer = Tokenizer.CreateTokenizer("c = a");
            var exprC = (AssignmentExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            exprC.Evaluate(scope);

            ExpressionBase result;

            // direct comparison - true
            tokenizer = Tokenizer.CreateTokenizer("a == a");
            var expr1 = (ComparisonExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr1.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)result).Value, Is.True);

            // direct comparison - false
            tokenizer = Tokenizer.CreateTokenizer("a == b");
            var expr2 = (ComparisonExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr2.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)result).Value, Is.False);

            // indirect comparison - true
            tokenizer = Tokenizer.CreateTokenizer("a == c");
            var expr3 = (ComparisonExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr3.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)result).Value, Is.True);

            // indirect comparison - false
            tokenizer = Tokenizer.CreateTokenizer("b == c");
            var expr4 = (ComparisonExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr4.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)result).Value, Is.False);

            // invalid direct comparison
            tokenizer = Tokenizer.CreateTokenizer("a == 1");
            var expr5 = (ComparisonExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr5.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Cannot compare function reference and integer"));

            // invalid indirect comparison
            tokenizer = Tokenizer.CreateTokenizer("c == 1");
            var expr6 = (ComparisonExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr6.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Cannot compare function reference and integer"));

            // invalid direct comparison reversed
            tokenizer = Tokenizer.CreateTokenizer("b() == a");
            var expr7 = (ComparisonExpression)ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr7.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Cannot compare integer and function reference"));
        }
    }
}
