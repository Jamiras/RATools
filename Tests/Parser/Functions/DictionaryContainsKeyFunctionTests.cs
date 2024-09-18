using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class DictionaryContainsKeyFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new DictionaryContainsKeyFunction();
            Assert.That(def.Name.Name, Is.EqualTo("dictionary_contains_key"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("dictionary"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("key"));
        }

        [Test]
        [TestCase("{}", "1", false)]
        [TestCase("{1: true}", "1", true)]
        [TestCase("{1: true, 2: false}", "2", true)]
        [TestCase("{1: true, 2: false, 3: true}", "2", true)]
        [TestCase("{1: true, 2: false}", "2.0", false)] // type mismatch
        [TestCase("{1: true, 2: false}", "\"2\"", false)] // type mismatch
        [TestCase("{\"1\": true, \"2\": false}", "\"2\"", true)]
        [TestCase("{\"1\": true, \"2\": false}", "\"3\"", false)]
        [TestCase("{\"1\": true, \"2\": false}", "2", false)] // type mismatch
        public void TestEvaluate(string array, string value, bool expected)
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            var result = FunctionTests.Evaluate<DictionaryContainsKeyFunction>(
                string.Format("dictionary_contains_key({0}, {1}", array, value), scope);

            Assert.That(result, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)result).Value, Is.EqualTo(expected));
        }
    }
}
