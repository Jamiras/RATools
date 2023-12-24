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
    class ConditionalExpressionTests
    {
        [Test]
        [TestCase(ConditionalOperation.And, "variable && 99")]
        [TestCase(ConditionalOperation.Or, "variable || 99")]
        [TestCase(ConditionalOperation.Not, "!99")]
        public void TestAppendString(ConditionalOperation op, string expected)
        {
            ExpressionBase variable = op == ConditionalOperation.Not ? null : new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new ConditionalExpression(variable, op, value);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("A == B", "A == B")]
        [TestCase("A == 1 && B == 1", "A == 1 && B == 1")]
        [TestCase("A == 1 || B == 1", "A == 1 || B == 1")]
        [TestCase("A == 1 || true", "true")]
        [TestCase("A == 1 || false", "A == 1")]
        [TestCase("A == 1 && true", "A == 1")]
        [TestCase("A == 1 && false", "false")]
        [TestCase("true || A == 1", "true")]
        [TestCase("false || A == 1", "A == 1")]
        [TestCase("true && A == 1", "A == 1")]
        [TestCase("false && A == 1", "false")]
        [TestCase("true || true", "true")]
        [TestCase("true || false", "true")]
        [TestCase("false || false", "false")]
        [TestCase("true && true", "true")]
        [TestCase("true && false", "false")]
        [TestCase("false && false", "false")]
        [TestCase("!true", "false")]
        [TestCase("!false", "true")]
        [TestCase("!(A == B)", "A != B")]
        [TestCase("!(A != B)", "A == B")]
        [TestCase("!(A < B)", "A >= B")]
        [TestCase("!(A <= B)", "A > B")]
        [TestCase("!(A > B)", "A <= B")]
        [TestCase("!(A >= B)", "A < B")]
        [TestCase("!(A == 1 || B == 1)", "A != 1 && B != 1")]
        [TestCase("!(A == 1 || B == 1 || C == 1)", "A != 1 && B != 1 && C != 1")]
        [TestCase("!(A == 1 && B == 1)", "A != 1 || B != 1")]
        [TestCase("!(A == 1 && B == 1 && C == 1)", "A != 1 || B != 1 || C != 1")]
        [TestCase("!(!(A == B))", "A == B")]
        [TestCase("!(A == 1 || !(B == 1 && C == 1))", "A != 1 && B == 1 && C == 1")]
        [TestCase("!always_true()", "always_false()")]
        [TestCase("!always_false()", "always_true()")]
        [TestCase("!(always_false() || A == 1)", "always_true() && A != 1")]
        [TestCase("(A == 1 && B != 1) || (A != 1 && B == 1)", "(A == 1 && B != 1) || (A != 1 && B == 1)")]
        public void TestReplaceVariables(string input, string expected)
        {
            input = input.Replace("A", "byte(10)");
            input = input.Replace("B", "byte(11)");
            input = input.Replace("C", "byte(12)");

            expected = expected.Replace("A", "byte(0x00000A)");
            expected = expected.Replace("B", "byte(0x00000B)");
            expected = expected.Replace("C", "byte(0x00000C)");

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer);

            var scope = new InterpreterScope();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));
            scope.AddFunction(new AlwaysTrueFunction());
            scope.AddFunction(new AlwaysFalseFunction());
            scope.Context = new TriggerBuilderContext();

            ExpressionBase result;
            Assert.That(expression.ReplaceVariables(scope, out result), Is.True);

            var builder = new StringBuilder();
            result.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        private static void SetLogicalUnit(ExpressionBase expressionBase)
        {
            var conditional = expressionBase as ConditionalExpression;
            if (conditional != null)
            {
                if (conditional.Operation != ConditionalOperation.Not)
                    conditional.IsLogicalUnit = true;

                foreach (var clause in conditional.Conditions)
                    SetLogicalUnit(clause);
            }
        }

        [Test]
        [TestCase("true || true", true)]
        [TestCase("true || false", true)]
        [TestCase("false || true", true)]
        [TestCase("false || false", false)]
        [TestCase("true && true", true)]
        [TestCase("true && false", false)]
        [TestCase("false && true", false)]
        [TestCase("false && false", false)]
        [TestCase("!true", false)]
        [TestCase("!false", true)]
        [TestCase("!true && !true", false)]
        [TestCase("!true && !false", false)]
        [TestCase("!false && !true", false)]
        [TestCase("!false && !false", true)]
        public void TestIsTrue(string input, bool expected)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());

            ErrorExpression error;
            var scope = AchievementScriptInterpreter.GetGlobalScope();
            var result = expression.IsTrue(scope, out error);
            Assert.That(error, Is.Null);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
