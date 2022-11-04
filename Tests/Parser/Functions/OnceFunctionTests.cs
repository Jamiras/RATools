using NUnit.Framework;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Tests.Parser.Expressions.Trigger;
using RATools.Tests.Parser.Expressions;
using System.Linq;

namespace RATools.Tests.Parser.Functions
{
    [TestFixture]
    class OnceFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new OnceFunction();
            Assert.That(def.Name.Name, Is.EqualTo("once"));
            Assert.That(def.Parameters.Count, Is.EqualTo(1));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
        }

        [Test]
        [TestCase("once(byte(0x001234) == 3)")]
        [TestCase("once(byte(0x001234) == 56 && byte(0x002345) == 67)")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }

        [Test]
        [TestCase("once(byte(0x1234) == 3)", "0xH001234=3.1.")]
        [TestCase("once(byte(0x1234) == 56 && byte(0x2345) == 67)", "N:0xH001234=56_0xH002345=67.1.")]
        [TestCase("once(byte(0x1234) == 56 || byte(0x2345) == 67)", "O:0xH001234=56_0xH002345=67.1.")]
        [TestCase("once(prev(byte(0x1234)) == byte(0x2345) - 1)", "A:1=0_d0xH001234=0xH002345.1.")]
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        [TestCase("once(6+2)")] // numeric
        [TestCase("once(byte(0x1234))")] // no comparison
        [TestCase("once(f)")] // function reference
        public void TestUnsupportedComparisons(string input)
        {
            var scope = TriggerExpressionTests.CreateScope();
            scope.AssignVariable(new VariableExpression("f"), new FunctionReferenceExpression("f2"));

            TriggerExpressionTests.AssertParseError(input, scope, "comparison did not evaluate to a valid comparison");
        }
    }
}
