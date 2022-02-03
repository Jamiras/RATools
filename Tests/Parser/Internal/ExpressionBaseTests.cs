using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class ExpressionBaseTests
    {
        [Test]
        public void TestSkipWhitespace()
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(" \t\r\n    hi"));
            ExpressionBase.SkipWhitespace(tokenizer);
            Assert.That(tokenizer.NextChar, Is.EqualTo('h'));
        }

        [Test]
        public void TestSkipWhitespaceComment()
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer("  // comment\r\nhi"));
            ExpressionBase.SkipWhitespace(tokenizer);
            Assert.That(tokenizer.NextChar, Is.EqualTo('h'));
        }

        [Test]
        public void TestSkipWhitespaceMultiComment()
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer("  // comment\r\n// comment2\r\nhi"));
            ExpressionBase.SkipWhitespace(tokenizer);
            Assert.That(tokenizer.NextChar, Is.EqualTo('h'));
        }

        private static PositionalTokenizer CreateTokenizer(string input)
        {
            return new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
        }

        [Test]
        public void TestParseNumber()
        {
            var tokenizer = CreateTokenizer("123456");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(123456));
        }

        [Test]
        public void TestParseNegativeNumber()
        {
            var tokenizer = CreateTokenizer("-123456");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(-123456));
        }

        [Test]
        public void TestParseHexNumber()
        {
            var tokenizer = CreateTokenizer("0x123456");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(0x123456));
        }

        [Test]
        public void TestParseNegativeHexNumber()
        {
            var tokenizer = CreateTokenizer("-0x123456");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(-0x123456));
        }

        [Test]
        public void TestParseNumberLeadingZero()
        {
            var tokenizer = CreateTokenizer("0123456");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(123456));
        }

        [Test]
        public void TestParseNumberOverflow()
        {
            var tokenizer = CreateTokenizer("4294967295");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(-1));

            tokenizer = CreateTokenizer("4294967296");
            expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)expression).Message, Is.EqualTo("Number too large"));
        }

        [Test]
        public void TestParseNumberOverflowHex()
        {
            var tokenizer = CreateTokenizer("0xFFFFFFFF");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(-1));

            tokenizer = CreateTokenizer("0x100000000");
            expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)expression).Message, Is.EqualTo("Number too large"));
        }

        [Test]
        public void TestParseNumberOverflowNegative()
        {
            var tokenizer = CreateTokenizer("-2147483647");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(-2147483647));

            tokenizer = CreateTokenizer("-2147483648");
            expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)expression).Message, Is.EqualTo("Number too large"));
        }

        [Test]
        public void TestParseString()
        {
            var tokenizer = CreateTokenizer("\"This is a string.\"");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)expression).Value, Is.EqualTo("This is a string."));
        }

        [Test]
        public void TestParseVariable()
        {
            var tokenizer = CreateTokenizer("variable");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<VariableExpression>());
            var variable = (VariableExpression)expression;
            Assert.That(variable.Name, Is.EqualTo("variable"));
        }

        [Test]
        public void TestParseAssignment()
        {
            var tokenizer = CreateTokenizer("var = 3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<AssignmentExpression>());
            var assign = (AssignmentExpression)expression;
            Assert.That(assign.Variable, Is.InstanceOf<VariableExpression>());
            Assert.That(assign.Value, Is.InstanceOf<IntegerConstantExpression>());
        }

        [Test]
        public void TestParseAssignmentArray()
        {
            var tokenizer = CreateTokenizer("var = [3]");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<AssignmentExpression>());
            var assign = (AssignmentExpression)expression;
            Assert.That(assign.Variable, Is.InstanceOf<VariableExpression>());
            Assert.That(assign.Value, Is.InstanceOf<ArrayExpression>());
        }

        [Test]
        public void TestParseFunctionDefinition()
        {
            var tokenizer = CreateTokenizer("function test() => 1");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<FunctionDefinitionExpression>());
            var func = (FunctionDefinitionExpression)expression;
            Assert.That(func.Name.Name, Is.EqualTo("test"));
            Assert.That(func.Parameters.Count, Is.EqualTo(0));
            Assert.That(func.Expressions.Count, Is.EqualTo(1));
            Assert.That(func.Expressions.First(), Is.InstanceOf<ReturnExpression>());
        }

        private void SetLogicalUnit(ExpressionBase expression)
        {
            var nestedExpressions = expression as INestedExpressions;
            if (nestedExpressions != null)
            {
                int count = 0;

                foreach (var nestedExpression in nestedExpressions.NestedExpressions)
                {
                    count++;
                    SetLogicalUnit(nestedExpression);
                }

                if (count > 1)
                    expression.IsLogicalUnit = true;
            }
        }

        [Test]
        [TestCase("A < 3", "A < 3")]
        [TestCase("A < 3 && B < 3", "(A < 3) && (B < 3)")]
        [TestCase("A && B || C", "(A && B) || C")] // AND has higher priority than OR
        [TestCase("A && (B || C)", "A && (B || C)")]
        [TestCase("A || B && C", "A || (B && C)")] // AND has higher priority than OR
        [TestCase("A || (B && C)", "A || (B && C)")]
        [TestCase("A && B && C", "A && B && C")]
        [TestCase("A && (B && C)", "A && B && C")]
        [TestCase("!A && B || C", "(!A && B) || C")] // AND has higher priority than OR
        [TestCase("A && !B || C", "(A && !B) || C")] // AND has higher priority than OR
        [TestCase("A && B || !C", "(A && B) || !C")] // AND has higher priority than OR
        [TestCase("A && B || C && D", "(A && B) || (C && D)")] // AND has higher priority than OR
        [TestCase("(A && B) || (C && D)", "(A && B) || (C && D)")]
        [TestCase("A && B || (C && D)", "(A && B) || (C && D)")] // AND has higher priority than OR
        [TestCase("A && (B || C) && D", "A && (B || C) && D")]
        [TestCase("A || B && C || D", "A || (B && C) || D")] // AND has higher priority than OR
        [TestCase("(A || B) && (C || D)", "(A || B) && (C || D)")]
        [TestCase("A || B && (C || D)", "A || (B && (C || D))")]
        [TestCase("A || (B && C) || D", "A || (B && C) || D")]
        [TestCase("A && B && C || D && E && F", "(A && B && C) || (D && E && F)")] // AND has higher priority than OR
        [TestCase("(A && B && C) || (D && E && F)", "(A && B && C) || (D && E && F)")]
        [TestCase("A && B || C && D || E && F", "(A && B) || (C && D) || (E && F)")] // AND has higher priority than OR
        [TestCase("(A && B || C) && (D || E && F)", "((A && B) || C) && (D || (E && F))")] // AND has higher priority than OR
        public void TestParseExpressionGrouping(string input, string expected)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer);

            SetLogicalUnit(expression);

            var builder = new StringBuilder();
            expression.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("A + B + C", true)]
        [TestCase("A + B - C", true)]
        [TestCase("A + B * C", false)]
        [TestCase("A + B / C", false)]
        [TestCase("A + B % C", false)]
        [TestCase("A - B + C", true)]
        [TestCase("A - B - C", true)]
        [TestCase("A - B * C", false)]
        [TestCase("A - B / C", false)]
        [TestCase("A - B % C", false)]
        [TestCase("A * B + C", true)]
        [TestCase("A * B - C", true)]
        [TestCase("A * B * C", true)]
        [TestCase("A * B / C", true)]
        [TestCase("A * B % C", true)]
        [TestCase("A / B + C", true)]
        [TestCase("A / B - C", true)]
        [TestCase("A / B * C", true)]
        [TestCase("A / B / C", true)]
        [TestCase("A / B % C", true)]
        [TestCase("A % B + C", true)]
        [TestCase("A % B - C", true)]
        [TestCase("A % B * C", true)]
        [TestCase("A % B / C", true)]
        [TestCase("A % B % C", true)]
        [TestCase("\"A\" + B + C", false)]
        [TestCase("A + B + \"C\"", true)]
        public void TestParseExpressionGroupingMathematic(string input, bool leftPrioritized)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer) as MathematicExpression;

            Assert.That(expression, Is.Not.Null);

            var parts = input.Split(' ');
            string expectedLeft, expectedRight;

            if (leftPrioritized)
            {
                expectedLeft = parts[0] + ' ' + parts[1] + ' ' + parts[2];
                expectedRight = parts[4];
            }
            else
            {
                expectedLeft = parts[0];
                expectedRight = parts[2] + ' ' + parts[3] + ' ' + parts[4];
            }

            var builder = new StringBuilder();
            expression.Left.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expectedLeft));

            builder.Clear();
            expression.Right.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expectedRight));
        }

        [TestCase("A + B < C", true)]
        [TestCase("A < B + C", false)]
        public void TestParseExpressionGroupingComparison(string input, bool leftPrioritized)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer) as ComparisonExpression;

            Assert.That(expression, Is.Not.Null);
            if (leftPrioritized)
            {
                var builder = new StringBuilder();
                expression.Left.AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(input.Substring(0, 5)));

                builder.Clear();
                expression.Right.AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(input.Substring(8, 1)));
            }
            else
            {
                var builder = new StringBuilder();
                expression.Left.AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(input.Substring(0, 1)));

                builder.Clear();
                expression.Right.AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(input.Substring(4, 5)));
            }
        }

        [TestCase("A < B &&C", true)]
        [TestCase("A&& B < C", false)]
        public void TestParseExpressionGroupingLogical(string input, bool leftPrioritized)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer) as ConditionalExpression;

            Assert.That(expression, Is.Not.Null);
            if (leftPrioritized)
            {
                var builder = new StringBuilder();
                expression.Conditions.ElementAt(0).AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(input.Substring(0, 5)));

                builder.Clear();
                expression.Conditions.ElementAt(1).AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(input.Substring(8, 1)));
            }
            else
            {
                var builder = new StringBuilder();
                expression.Conditions.ElementAt(0).AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(input.Substring(0, 1)));

                builder.Clear();
                expression.Conditions.ElementAt(1).AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(input.Substring(4, 5)));
            }
        }

        [Test]
        public void TestParseExpressionGroupingStringBuildingWithAddition()
        {
            var input = "\"A\" + B + \"C\" + D + E + \"F\"";
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer) as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Left.ToString(), Is.EqualTo("StringConstant: \"A\""));

            expression = expression.Right as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Left.ToString(), Is.EqualTo("Variable: B"));

            expression = expression.Right as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Left.ToString(), Is.EqualTo("StringConstant: \"C\""));

            expression = expression.Right as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Right.ToString(), Is.EqualTo("StringConstant: \"F\""));

            expression = expression.Left as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.ToString(), Is.EqualTo("Mathematic: D + E"));
        }

        [Test]
        public void TestParseExpressionGroupingStringBuildingWithSubtraction()
        {
            var input = "\"A\" + B + \"C\" + D - E + \"F\"";
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer) as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Left.ToString(), Is.EqualTo("StringConstant: \"A\""));

            expression = expression.Right as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Left.ToString(), Is.EqualTo("Variable: B"));

            expression = expression.Right as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Left.ToString(), Is.EqualTo("StringConstant: \"C\""));

            expression = expression.Right as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Right.ToString(), Is.EqualTo("StringConstant: \"F\""));

            expression = expression.Left as MathematicExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.ToString(), Is.EqualTo("Mathematic: D - E"));
        }

        [Test]
        public void TestParseConditional()
        {
            var tokenizer = CreateTokenizer("var1 || var2");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Conditions.Count(), Is.EqualTo(2));
            Assert.That(cond.Conditions.ElementAt(0), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(1), Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseConditionalMultiple()
        {
            var tokenizer = CreateTokenizer("var1 || var2 || var3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Conditions.Count(), Is.EqualTo(3));
            Assert.That(cond.Conditions.ElementAt(0), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(1), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(2), Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseConditionalMultipleGrouped()
        {
            var tokenizer = CreateTokenizer("(var1 || var2) || var3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Conditions.Count(), Is.EqualTo(3));
            Assert.That(cond.Conditions.ElementAt(0), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(1), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(2), Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseConditionalMultipleLines()
        {
            var tokenizer = CreateTokenizer("var1 || \n var2 \n || var3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Conditions.Count(), Is.EqualTo(3));
            Assert.That(cond.Conditions.ElementAt(0), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(1), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(2), Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseConditionalMultipleLinesWithComments()
        {
            var tokenizer = CreateTokenizer("var1 || // comment \n var2 // comment \n || var3 // comment");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Conditions.Count(), Is.EqualTo(3));
            Assert.That(cond.Conditions.ElementAt(0), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(1), Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Conditions.ElementAt(2), Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseMathematic()
        {
            var tokenizer = CreateTokenizer("var1 + var2");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<MathematicExpression>());
            var math = (MathematicExpression)expression;
            Assert.That(math.Left, Is.InstanceOf<VariableExpression>());
            Assert.That(math.Operation, Is.EqualTo(MathematicOperation.Add));
            Assert.That(math.Right, Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseFunctionCall()
        {
            var tokenizer = CreateTokenizer("func()");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var call = (FunctionCallExpression)expression;
            Assert.That(call.FunctionName.Name, Is.EqualTo("func"));
            Assert.That(call.Parameters.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestParseReturn()
        {
            var tokenizer = CreateTokenizer("return 3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ReturnExpression>());
            var ret = (ReturnExpression)expression;
            Assert.That(ret.Value, Is.InstanceOf<IntegerConstantExpression>());
        }

        [Test]
        public void TestParseFunctionCallWithParameters()
        {
            var tokenizer = CreateTokenizer("func(p1)");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var call = (FunctionCallExpression)expression;
            Assert.That(call.FunctionName.Name, Is.EqualTo("func"));
            Assert.That(call.Parameters.Count, Is.EqualTo(1));
            Assert.That(call.Parameters.First(), Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseFunctionCallWithNamedParameters()
        {
            var tokenizer = CreateTokenizer("func(p1 = 3)");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var call = (FunctionCallExpression)expression;
            Assert.That(call.FunctionName.Name, Is.EqualTo("func"));
            Assert.That(call.Parameters.Count, Is.EqualTo(1));
            Assert.That(call.Parameters.First(), Is.InstanceOf<AssignmentExpression>());
        }

        [Test]
        public void TestParseNotOperator()
        {
            var tokenizer = CreateTokenizer("!func()");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Not));
            Assert.That(cond.Conditions.Count(), Is.EqualTo(1));
            Assert.That(cond.Conditions.ElementAt(0), Is.InstanceOf<FunctionCallExpression>());
        }

        [Test]
        public void TestParseDictionary()
        {
            var tokenizer = CreateTokenizer("{1: \"a\", 2: \"b\"}");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<DictionaryExpression>());
            var dict = (DictionaryExpression)expression;
            Assert.That(dict.Entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void TestParseDictionaryIndexed()
        {
            var tokenizer = CreateTokenizer("dict[3]");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IndexedVariableExpression>());
            var variable = (IndexedVariableExpression)expression;
            Assert.That(variable.Variable, Is.InstanceOf<VariableExpression>());
            Assert.That(((VariableExpression)variable.Variable).Name, Is.EqualTo("dict"));
            Assert.That(variable.Index, Is.InstanceOf<IntegerConstantExpression>());
        }

        [Test]
        public void TestParseDictionaryIndexedMulti()
        {
            var tokenizer = CreateTokenizer("dict[3][4]");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IndexedVariableExpression>());
            var variable = (IndexedVariableExpression)expression;
            Assert.That(variable.Name, Is.EqualTo(""));
            Assert.That(variable.Variable, Is.InstanceOf<IndexedVariableExpression>());
            Assert.That(variable.Index, Is.InstanceOf<IntegerConstantExpression>());
            variable = (IndexedVariableExpression)variable.Variable;
            Assert.That(variable.Variable, Is.InstanceOf<VariableExpression>());
            Assert.That(((VariableExpression)variable.Variable).Name, Is.EqualTo("dict"));
            Assert.That(variable.Index, Is.InstanceOf<IntegerConstantExpression>());
        }

        [Test]
        public void TestParseFor()
        {
            var tokenizer = CreateTokenizer("for i in dict { j = i }");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ForExpression>());
            var loop = (ForExpression)expression;
            Assert.That(loop.IteratorName.Name, Is.EqualTo("i"));
            Assert.That(loop.Range, Is.InstanceOf<VariableExpression>());
            Assert.That(loop.Expressions.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestParseIf()
        {
            var tokenizer = CreateTokenizer("if (i == 1) { j = 10 } else { j = i }");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IfExpression>());
            var expr = (IfExpression)expression;
            Assert.That(expr.Condition, Is.InstanceOf<ComparisonExpression>());
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));
            Assert.That(expr.ElseExpressions.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestParseRebalance()
        {
            // A + B < C => (A + B) < C
            var tokenizer = CreateTokenizer("a + b < c");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ComparisonExpression>());
            var comp = (ComparisonExpression)expression;
            Assert.That(comp.Left, Is.InstanceOf<MathematicExpression>());
            Assert.That(comp.Right, Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseLineColumnValues()
        {
            var tokenizer = CreateTokenizer(
                "// this is a test\n" +                             // 1
                "// of multiple lines and token positions\n" +      // 2
                "\n" +                                              // 3
                "a = 3\n" +                                         // 4
                "  b = a + 1\n" +                                   // 5
                "\n" +                                              // 6
                "return b\n"                                        // 7
                );
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<AssignmentExpression>());
            Assert.That(expression.Location.Start.Line, Is.EqualTo(4));
            Assert.That(expression.Location.Start.Column, Is.EqualTo(1));
            Assert.That(expression.Location.End.Line, Is.EqualTo(4));
            Assert.That(expression.Location.End.Column, Is.EqualTo(5));

            expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<AssignmentExpression>());
            Assert.That(expression.Location.Start.Line, Is.EqualTo(5));
            Assert.That(expression.Location.Start.Column, Is.EqualTo(3));
            Assert.That(expression.Location.End.Line, Is.EqualTo(5));
            Assert.That(expression.Location.End.Column, Is.EqualTo(11));

            expression = ((AssignmentExpression)expression).Value;
            Assert.That(expression, Is.InstanceOf<MathematicExpression>());
            Assert.That(expression.Location.Start.Line, Is.EqualTo(5));
            Assert.That(expression.Location.Start.Column, Is.EqualTo(7));
            Assert.That(expression.Location.End.Line, Is.EqualTo(5));
            Assert.That(expression.Location.End.Column, Is.EqualTo(11));

            expression = ((MathematicExpression)expression).Right;
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(expression.Location.Start.Line, Is.EqualTo(5));
            Assert.That(expression.Location.Start.Column, Is.EqualTo(11));
            Assert.That(expression.Location.End.Line, Is.EqualTo(5));
            Assert.That(expression.Location.End.Column, Is.EqualTo(11));

            expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ReturnExpression>());
            Assert.That(expression.Location.Start.Line, Is.EqualTo(7));
            Assert.That(expression.Location.Start.Column, Is.EqualTo(1));
            Assert.That(expression.Location.End.Line, Is.EqualTo(7));
            Assert.That(expression.Location.End.Column, Is.EqualTo(8));
        }
    }
}
