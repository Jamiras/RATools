using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Tests.Parser.Expressions;
using RATools.Tests.Parser.Expressions.Trigger;
using System.Linq;

namespace RATools.Tests.Parser.Functions
{
    [TestFixture]
    class RepeatedFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new RepeatedFunction();
            Assert.That(def.Name.Name, Is.EqualTo("repeated"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("count"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("comparison"));
        }

        [Test]
        [TestCase("repeated(3, low4(word(0x001234)) < 5)")]
        [TestCase("repeated(4, once(byte(0x001234) < 5) && byte(0x002345) == 6)")]
        [TestCase("repeated(5, dword(0x001234) < 5 && never(byte(0x002345) == 6))")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }

        [Test]
        [TestCase("repeated(3, low4(word(0x001234)) < 5)", "I:0x 001234_0xL000000<5.3.")]
        [TestCase("repeated(4, byte(0x001234) < 5 && byte(0x002345) == 6)", "N:0xH001234<5_0xH002345=6.4.")]
        [TestCase("repeated(5, byte(0x001234) < 5 || byte(0x002345) == 6)", "O:0xH001234<5_0xH002345=6.5.")]
        [TestCase("repeated(6, byte(0x001234) < 5 || byte(0x002345) == 6 || byte(0x003456) == 7 || byte(0x004567) == 8)",
            "O:0xH001234<5_O:0xH002345=6_O:0xH003456=7_0xH004567=8.6.")]
        [TestCase("repeated(7, once(byte(0x001234) < 5) && byte(0x002345) == 6)", "N:0xH001234<5.1._0xH002345=6.7.")]
        [TestCase("repeated(8, repeated(5, byte(0x001234) < 5) || repeated(6, byte(0x002345) == 6))",
            "O:0xH001234<5.5._C:0xH002345=6.6._0=1.8.")] // always_false final clause needed to separate total from individual hit counts
        [TestCase("repeated(9, byte(0x001234) == 5 || (byte(0x002345) == 6 && byte(0x003456) == 7))",
            "N:0xH002345=6_O:0xH003456=7_0xH001234=5.9.")]
        [TestCase("repeated(10, (byte(0x001234) == 5 || byte(0x002345) == 6) && byte(0x003456) == 7 && byte(0x004567) == 8)",
            "O:0xH001234=5_N:0xH002345=6_N:0xH003456=7_0xH004567=8.10.")]
        [TestCase("repeated(11, byte(0x003456) == 7 && byte(0x004567) == 8 && (byte(0x001234) == 5 || byte(0x002345) == 6))",
            "O:0xH001234=5_N:0xH002345=6_N:0xH003456=7_0xH004567=8.11.")]
        [TestCase("repeated(12, (byte(0x001234) == 5 && byte(0x002345) == 6) || byte(0x002345) == 6)",
            "0xH002345=6.12.")] // '(A && B) || B' is just 'B'
        [TestCase("repeated(13, byte(0x1234 + byte(0x2345)) == 56 || byte(0x1235 + byte(0x2345)) == 34)",
            "I:0xH002345_O:0xH001234=56_I:0xH002345_0xH001235=34.13.")]
        [TestCase("repeated(14, byte(0x1234 + byte(0x2345)) == 56 || byte(0x1234 + byte(0x2346)) == 34)",
            "I:0xH002345_O:0xH001234=56_I:0xH002346_0xH001234=34.14.")]
        [TestCase("repeated(15, byte(0x1234) + byte(0x2345) == 56 || byte(0x1234) + byte(0x2345) == 34)",
            "A:0xH001234_O:0xH002345=56_A:0xH001234_0xH002345=34.15.")]
        [TestCase("repeated(16, byte(0x1234) == 5 || repeated(4, byte(0x2345) == 6 || byte(0x3456) == 7))",
            "O:0xH002345=6_O:0xH003456=7.4._0xH001234=5.16.")]
        [TestCase("repeated(17, byte(0x1234) == 1 && byte(0x2345) == 2 && (byte(0x3456) == 3 || (byte(0x4567) == 4 && byte(0x5678) == 5))", 
            "N:0xH004567=4_O:0xH005678=5_N:0xH003456=3_N:0xH001234=1_0xH002345=2.17.")]
        [TestCase("repeated(18, byte(0x1234) == 1 && never(byte(0x2345) == 2))", "Z:0xH002345=2_0xH001234=1.18.")]
        [TestCase("repeated(19, byte(0x1234) == 1 && never(byte(0x2345) == 2) && never(byte(0x003456) == 7)", 
            "Z:0xH002345=2_Z:0xH003456=7_0xH001234=1.19.")]
        [TestCase("repeated(20, byte(0x1234) == 1 && never(repeated(3, byte(0x2345) == 2 && never(byte(0x3456) == 3))))",
            "Z:0xH003456=3_Z:0xH002345=2.3._0xH001234=1.20.")]
        [TestCase("repeated(21, byte(0x002345) == 1 && byte(0x003456) == 2 && never(byte(0x001234) < 5 || byte(0x001234) > 8))",
            "O:0xH001234<5_Z:0xH001234>8_N:0xH002345=1_0xH003456=2.21.")]
        [TestCase("repeated(22, never(byte(0x1234) == 1))", "R:0xH001234=1.22.")]
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        [TestCase("repeated(5, 6+2)")] // numeric
        [TestCase("repeated(5, byte(0x1234))")] // no comparison
        [TestCase("repeated(5, f)")] // function reference
        public void TestUnsupportedComparisons(string input)
        {
            var scope = TriggerExpressionTests.CreateScope();
            scope.AssignVariable(new VariableExpression("f"), new FunctionReferenceExpression("f2"));

            TriggerExpressionTests.AssertParseError(input, scope, "comparison did not evaluate to a valid comparison");
        }
    }
}
