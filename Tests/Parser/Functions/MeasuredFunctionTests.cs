using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using RATools.Tests.Parser.Expressions;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Tests.Parser.Functions
{
    [TestFixture]
    class MeasuredFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new MeasuredFunction();
            Assert.That(def.Name.Name, Is.EqualTo("measured"));
            Assert.That(def.Parameters.Count, Is.EqualTo(3));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("when"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("format"));
        }

        private List<Requirement> Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();
            var funcDef = new MeasuredFunction();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            var context = new TriggerBuilderContext { Trigger = requirements };
            scope.Context = context;

            ExpressionBase evaluated;
            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);
            funcCall = (FunctionCallExpression)evaluated;

            if (expectedError == null)
            {
                Assert.That(funcDef.BuildTrigger(context, scope, funcCall), Is.Null);
            }
            else
            {
                var parseError = funcDef.BuildTrigger(context, scope, funcCall);
                Assert.That(parseError, Is.Not.Null);
                if (parseError.InnerError != null)
                    parseError = parseError.InnermostError;
                Assert.That(parseError.Message, Is.EqualTo(expectedError));
            }

            return requirements;
        }

        [Test]
        public void TestComparison()
        {
            var requirements = Evaluate("measured(byte(0x1234) == 120)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonAddSource()
        {
            var requirements = Evaluate("measured(byte(0x1234) + word(0x2345) == 120)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.None));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("word(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonAddAddress()
        {
            var requirements = Evaluate("measured(byte(0x1234 + word(0x2345)) == 120)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("word(0x002345)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.None));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonAndNext()
        {
            Evaluate("measured(byte(0x2345) == 6 && byte(0x1234) == 120)",
                "measured comparison can only have one logical clause");
        }

        [Test]
        public void TestRepeated()
        {
            var requirements = Evaluate("measured(repeated(10, byte(0x1234) == 20))");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("20"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(10));
        }

        [Test]
        public void TestRepeatedZero()
        {
            Evaluate("measured(repeated(0, byte(0x1234) == 20))",
                "Unbounded count is only supported in measured value expressions");
        }

        [Test]
        public void TestRepeatedZeroInValue()
        {
            var input = "measured(repeated(0, byte(0x1234) == 20))";

            var requirements = new List<Requirement>();
            var funcDef = new MeasuredFunction();
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var valueScope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope()) { Context = new ValueBuilderContext() };
            var funcCallScope = new InterpreterScope(valueScope) { Context = funcCall };
            var scope = funcCall.GetParameters(funcDef, funcCallScope, out error);
            var context = new TriggerBuilderContext { Trigger = requirements };
            scope.Context = context;

            ExpressionBase evaluated;
            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);
            funcCall = (FunctionCallExpression)evaluated;
            Assert.That(funcDef.BuildTrigger(context, scope, funcCall), Is.Null);

            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("20"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(uint.MaxValue)); // will be removed when serialized
        }

        [Test]
        public void TestRepeatedAddSource()
        {
            var requirements = Evaluate("measured(repeated(10, byte(0x1234) + byte(0x2345) == 1))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.None));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[1].HitCount, Is.EqualTo(10));
        }

        [Test]
        public void TestRepeatedAddAddress()
        {
            var requirements = Evaluate("measured(repeated(10, byte(0x1234 + word(0x2345)) == 1))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("word(0x002345)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.None));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[1].HitCount, Is.EqualTo(10));
        }

        [Test]
        public void TestRepeatedAndNext()
        {
            var requirements = Evaluate("measured(repeated(10, byte(0x1234) == 6 && word(0x2345) == 1))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("word(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[1].HitCount, Is.EqualTo(10));
        }

        [Test]
        public void TestSubtractedTarget()
        {
            var requirements = Evaluate("measured(300 - byte(0x1234) >= 100)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.None));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.SubSource));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("300"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.GreaterThanOrEqual));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("100"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
        }

        [Test]
        public void TestAdjustedTargetRaw()
        {
            // for raw measurements, make sure we keep the specified target for display
            // in this case, the bar will start at 22/100 and go to 100/100
            var requirements = Evaluate("measured(byte(0x1234) + 22 >= 100)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("22"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.None));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.GreaterThanOrEqual));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("100"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
        }

        [Test]
        public void TestAdjustedTargetRawExplicit()
        {
            // for raw measurements, make sure we keep the specified target for display
            // in this case, the bar will start at 22/100 and go to 100/100
            var requirements = Evaluate("measured(byte(0x1234) + 22 >= 100, format=\"raw\")");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("22"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.None));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.GreaterThanOrEqual));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("100"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
        }

        [Test]
        public void TestAdjustedTargetPercent()
        {
            // for percent measurements, we can ignore the specified target and optimize
            // the display bar. in this case, the bar will go from 0% to 100% representing
            // the values 22-100.
            var requirements = Evaluate("measured(byte(0x1234) + 22 >= 100, format=\"percent\")");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.GreaterThanOrEqual));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("78"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.MeasuredPercent));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonWhen()
        {
            var requirements = Evaluate("measured(byte(0x1234) == 120, when = (byte(0x2345) == 6))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("6"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.MeasuredIf));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonWhenMultiple()
        {
            var requirements = Evaluate("measured(byte(0x1234) == 120, when = (byte(0x2345) == 6 && byte(0x2346) == 7))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("6"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.MeasuredIf));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x002346)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("7"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.MeasuredIf));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonWhenMultipleRepeated()
        {
            var requirements = Evaluate("measured(byte(0x1234) == 120, when = once(byte(0x2345) == 6 && byte(0x2346) == 7))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("6"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x002346)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("7"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.MeasuredIf));
            Assert.That(requirements[2].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestComparisonWhenMultipleRepeatedSeparate()
        {
            // this _could_ be split into two separate MeasuredIf statements, but the logic to
            // separate this from the TestComparisonWhenMultipleRepeatedNested case is complicated.
            var requirements = Evaluate("measured(byte(0x1234) == 120, when = once(byte(0x2345) == 6) && once(byte(0x2346) == 7))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("6"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x002346)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("7"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.MeasuredIf));
            Assert.That(requirements[2].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestComparisonWhenMultipleRepeatedNested()
        {
            var requirements = Evaluate("measured(byte(0x1234) == 120, when = repeated(3, once(byte(0x2345) == 6) && byte(0x2346) == 7))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("6"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x002346)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("7"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.MeasuredIf));
            Assert.That(requirements[2].HitCount, Is.EqualTo(3));
        }

        [Test]
        public void TestComparisonWhenMultipleOr()
        {
            var requirements = Evaluate("measured(byte(0x1234) == 120, when = (byte(0x2345) == 6 || byte(0x2346) == 7))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("6"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.OrNext));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x002346)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("7"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.MeasuredIf));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonFormatRaw()
        {
            var requirements = Evaluate("measured(byte(0x1234) == 120, format=\"raw\")");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonFormatPercent()
        {
            var requirements = Evaluate("measured(byte(0x1234) == 120, format=\"percent\")");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.MeasuredPercent));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestComparisonFormatUnknown()
        {
            Evaluate("measured(byte(0x1234) == 120, format=\"unknown\")", "Unknown format: unknown");
        }

        [Test]
        public void TestTallyAddHits()
        {
            var requirements = Evaluate("measured(tally(2, byte(0x1234) == 120, byte(0x1234) == 126))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("126"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[1].HitCount, Is.EqualTo(2));
        }

        [Test]
        public void TestTallyZero()
        {
            Evaluate("measured(tally(0, byte(0x1234) == 20, byte(0x1234) == 67))",
                "Unbounded count is only supported in measured value expressions");
        }

        [Test]
        public void TestTallyZeroInValue()
        {
            var input = "measured(tally(0, byte(0x1234) == 20, byte(0x1234) == 67))";

            var requirements = new List<Requirement>();
            var funcDef = new MeasuredFunction();
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var valueScope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope()) { Context = new ValueBuilderContext() };
            var funcCallScope = new InterpreterScope(valueScope) { Context = funcCall };
            var scope = funcCall.GetParameters(funcDef, funcCallScope, out error);
            var context = new TriggerBuilderContext { Trigger = requirements };
            scope.Context = context;

            ExpressionBase evaluated;
            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);
            funcCall = (FunctionCallExpression)evaluated;
            Assert.That(funcDef.BuildTrigger(context, scope, funcCall), Is.Null);

            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("20"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[1].HitCount, Is.EqualTo(uint.MaxValue)); // will be removed when serialized
        }

        [Test]
        public void TestRepeatedOrNext()
        {
            var requirements = Evaluate("measured(repeated(2, byte(0x1234) == 120 || byte(0x1234) == 126))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("120"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.OrNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("126"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Measured));
            Assert.That(requirements[1].HitCount, Is.EqualTo(2));
        }
    }
}