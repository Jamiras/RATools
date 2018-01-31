using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Linq;

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
        public void TestParseNumberLeadingZero()
        {
            var tokenizer = CreateTokenizer("0123456");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expression).Value, Is.EqualTo(123456));
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

        [Test]
        public void TestParseComparison()
        {
            var tokenizer = CreateTokenizer("var < 3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ComparisonExpression>());
            var comp = (ComparisonExpression)expression;
            Assert.That(comp.Left, Is.InstanceOf<VariableExpression>());
            Assert.That(comp.Operation, Is.EqualTo(ComparisonOperation.LessThan));
            Assert.That(comp.Right, Is.InstanceOf<IntegerConstantExpression>());
        }

        [Test]
        public void TestParseConditional()
        {
            var tokenizer = CreateTokenizer("var1 || var2");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Left, Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Right, Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseConditionalMultiple()
        {
            var tokenizer = CreateTokenizer("var1 || var2 || var3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Left, Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Right, Is.InstanceOf<ConditionalExpression>());
        }

        [Test]
        public void TestParseConditionalMultipleGrouped()
        {
            var tokenizer = CreateTokenizer("(var1 || var2) || var3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Left, Is.InstanceOf<ConditionalExpression>());
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Right, Is.InstanceOf<VariableExpression>());
        }

        [Test]
        public void TestParseConditionalMultipleLines()
        {
            var tokenizer = CreateTokenizer("var1 || \n var2 \n || var3");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Left, Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Right, Is.InstanceOf<ConditionalExpression>());
        }

        [Test]
        public void TestParseConditionalMultipleLinesWithComments()
        {
            var tokenizer = CreateTokenizer("var1 || // comment \n var2 // comment \n || var3 // comment");
            var expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ConditionalExpression>());
            var cond = (ConditionalExpression)expression;
            Assert.That(cond.Left, Is.InstanceOf<VariableExpression>());
            Assert.That(cond.Operation, Is.EqualTo(ConditionalOperation.Or));
            Assert.That(cond.Right, Is.InstanceOf<ConditionalExpression>());
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
            Assert.That(cond.Left, Is.Null);
            Assert.That(cond.Right, Is.InstanceOf<FunctionCallExpression>());
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
            Assert.That(expression.Line, Is.EqualTo(4));
            Assert.That(expression.Column, Is.EqualTo(1));
            Assert.That(expression.EndLine, Is.EqualTo(4));
            Assert.That(expression.EndColumn, Is.EqualTo(5));

            expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<AssignmentExpression>());
            Assert.That(expression.Line, Is.EqualTo(5));
            Assert.That(expression.Column, Is.EqualTo(3));
            Assert.That(expression.EndLine, Is.EqualTo(5));
            Assert.That(expression.EndColumn, Is.EqualTo(11));

            expression = ((AssignmentExpression)expression).Value;
            Assert.That(expression, Is.InstanceOf<MathematicExpression>());
            Assert.That(expression.Line, Is.EqualTo(5));
            Assert.That(expression.Column, Is.EqualTo(7));
            Assert.That(expression.EndLine, Is.EqualTo(5));
            Assert.That(expression.EndColumn, Is.EqualTo(11));

            expression = ((MathematicExpression)expression).Right;
            Assert.That(expression, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(expression.Line, Is.EqualTo(5));
            Assert.That(expression.Column, Is.EqualTo(11));
            Assert.That(expression.EndLine, Is.EqualTo(5));
            Assert.That(expression.EndColumn, Is.EqualTo(11));

            expression = ExpressionBase.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<ReturnExpression>());
            Assert.That(expression.Line, Is.EqualTo(7));
            Assert.That(expression.Column, Is.EqualTo(1));
            Assert.That(expression.EndLine, Is.EqualTo(7));
            Assert.That(expression.EndColumn, Is.EqualTo(8));
        }
    }
}
