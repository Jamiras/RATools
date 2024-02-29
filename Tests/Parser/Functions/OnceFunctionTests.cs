using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using RATools.Parser.Tests.Expressions.Trigger;
using System.Linq;

namespace RATools.Parser.Tests.Functions
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
        [TestCase("once(((byte(0x001234) == 1 && byte(0x002345) == 2) || byte(0x003456) == 3) && never(byte(0x004567) == 4 && byte(0x005678) == 5))")]
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
        [TestCase("once(never(byte(0x001234) == 1))", "0xH001234!=1.1.")]
        [TestCase("once(byte(0x1234) != 0 && byte(0x1234) == 5)", "0xH001234=5.1.")]
        [TestCase("once(((byte(0x1234) == 1 && byte(0x2345) == 2) || byte(0x3456) == 3) && never(byte(0x4567) == 4 && byte(0x5678) == 5))",
            "N:0xH004567=4_Z:0xH005678=5_N:0xH001234=1_O:0xH002345=2_0xH003456=3.1.")]
        [TestCase("once(bit3(0x1234) < prev(bit3(0x1234)) && never(repeated(3, bit3(0x1234) == 0)))", "Z:0xP001234=0.3._0xP001234<d0xP001234.1.")]
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [TestCase("once(6+2)", "integer")] // numeric
        [TestCase("once(byte(0x1234))", "memory accessor")] // no comparison
        [TestCase("once(f)", "variable")] // function reference
        public void TestUnsupportedComparisons(string input, string unsupportedType)
        {
            var scope = TriggerExpressionTests.CreateScope();
            scope.AssignVariable(new VariableExpression("f"), new FunctionReferenceExpression("f2"));

            TriggerExpressionTests.AssertParseError(input, scope, "comparison: cannot convert " + unsupportedType + " to requirement");
        }
    }
}
