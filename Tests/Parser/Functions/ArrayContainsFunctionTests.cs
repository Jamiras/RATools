using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class ArrayContainsFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new ArrayContainsFunction();
            Assert.That(def.Name.Name, Is.EqualTo("array_contains"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("array"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("value"));
        }

        [Test]
        [TestCase("[]", "1", false)]
        [TestCase("[1]", "1", true)]
        [TestCase("[1,2]", "2", true)]
        [TestCase("[1,2,3]", "2", true)]
        [TestCase("[1,2]", "2.0", false)] // type mismatch
        [TestCase("[1,2]", "\"2\"", false)] // type mismatch
        [TestCase("[1.0,2.0]", "2.0", true)]
        [TestCase("[1.0,2.0]", "1.5", false)]
        [TestCase("[1.0,2.0]", "2", false)] // type mismatch
        [TestCase("[\"1\",\"2\"]", "\"2\"", true)]
        [TestCase("[\"1\",\"2\"]", "\"3\"", false)]
        [TestCase("[\"1\",\"2\"]", "2", false)] // type mismatch
        [TestCase("[byte(0x1234),byte(0x2345)]", "byte(0x2345)", true)]
        [TestCase("[byte(0x1234),byte(0x2345)]", "byte(0x3456)", false)]
        [TestCase("[byte(0x1234),byte(0x2345)]", "word(0x2345)", false)]
        [TestCase("[byte(0x1234),byte(0x2345)]", "byte(0x2345) + 1", false)]
        public void TestEvaluate(string array, string value, bool expected)
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            var result = FunctionTests.Evaluate<ArrayContainsFunction>(
                string.Format("array_contains({0}, {1}", array, value), scope);

            Assert.That(result, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)result).Value, Is.EqualTo(expected));
        }
    }
}
