using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
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
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new ConditionalExpression(variable, op, value);

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
            var expr = new ConditionalExpression(variable1, ConditionalOperation.And, variable2);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value2);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<ConditionalExpression>());
            Assert.That(((ConditionalExpression)result).Left, Is.EqualTo(value1));
            Assert.That(((ConditionalExpression)result).Operation, Is.EqualTo(expr.Operation));
            Assert.That(((ConditionalExpression)result).Right, Is.EqualTo(value2));
        }

        [Test]
        [TestCase("A == B", "A == B")]
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
        public void TestReplaceVariablesNormalizeNots(string input, string expected)
        {
            input = input.Replace("A", "byte(10)");
            input = input.Replace("B", "byte(11)");
            input = input.Replace("C", "byte(12)");

            expected = expected.Replace("A", "byte(10)");
            expected = expected.Replace("B", "byte(11)");
            expected = expected.Replace("C", "byte(12)");

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
            var leftRight = expressionBase as LeftRightExpressionBase;
            if (leftRight != null)
            {
                if (leftRight.Left != null) // ignore Not
                {
                    leftRight.IsLogicalUnit = true;
                    SetLogicalUnit(leftRight.Left);
                }

                SetLogicalUnit(leftRight.Right);
            }
        }

        [Test]
        [TestCase("A && B || C", "(A && B) || C")] // AND has higher priority than OR
        [TestCase("A && (B || C)", "A && (B || C)")]
        [TestCase("A || B && C", "A || (B && C)")] // AND has higher priority than OR
        [TestCase("A || (B && C)", "A || (B && C)")]
        [TestCase("A && B && C", "A && (B && C)")] // ungrouped tree is right-weighted
        [TestCase("A && (B && C)", "A && (B && C)")]
        [TestCase("!A && B || C", "(!A && B) || C")] // AND has higher priority than OR
        [TestCase("A && !B || C", "(A && !B) || C")] // AND has higher priority than OR
        [TestCase("A && B || !C", "(A && B) || !C")] // AND has higher priority than OR
        [TestCase("A && B || C && D", "(A && B) || (C && D)")] // AND has higher priority than OR
        [TestCase("(A && B) || (C && D)", "(A && B) || (C && D)")]
        [TestCase("A && B || (C && D)", "(A && B) || (C && D)")] // AND has higher priority than OR
        [TestCase("A && (B || C) && D", "A && ((B || C) && D)")] // ungrouped tree is right-weighted
        [TestCase("A || B && C || D", "A || ((B && C) || D)")] // AND has higher priority than OR
        [TestCase("(A || B) && (C || D)", "(A || B) && (C || D)")]
        [TestCase("A || B && (C || D)", "A || (B && (C || D))")]
        [TestCase("A || (B && C) || D", "A || ((B && C) || D)")] // ungrouped tree is right-weighted
        [TestCase("A && B && C || D && E && F", "(A && (B && C)) || (D && (E && F))")] // AND has higher priority than OR
        [TestCase("(A && B && C) || (D && E && F)", "(A && (B && C)) || (D && (E && F))")]
        [TestCase("A && B || C && D || E && F", "(A && B) || ((C && D) || (E && F))")] // AND has higher priority than OR
        [TestCase("(A && B || C) && (D || E && F)", "((A && B) || C) && (D || (E && F))")] // AND has higher priority than OR
        public void TestRebalance(string input, string expected)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer);

            var rebalanced = expression.Rebalance();
            SetLogicalUnit(rebalanced); // force parenthesis for evaluation

            var builder = new StringBuilder();
            rebalanced.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }
    }
}
