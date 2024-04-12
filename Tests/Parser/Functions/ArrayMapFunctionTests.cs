using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class ArrayMapFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new ArrayMapFunction();
            Assert.That(def.Name.Name, Is.EqualTo("array_map"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("inputs"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("predicate"));
        }

        private static string Evaluate(string input)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            var funcCall = expr as FunctionCallExpression;
            if (funcCall != null)
            {
                var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
                scope.Context = new AssignmentExpression(new VariableExpression("t"), expr);
                if (funcCall.Evaluate(scope, out expr))
                {
                    var builder = new StringBuilder();
                    expr.AppendString(builder);
                    return builder.ToString();
                }
            }

            var error = expr as ErrorExpression;
            if (error != null)
                return error.InnermostError.Message;

            return expr.ToString();
        }

        [Test]
        public void TestSimple()
        {
            Assert.That(Evaluate("array_map([1, 2, 3], a => byte(a))"),
                Is.EqualTo("[byte(0x000001), byte(0x000002), byte(0x000003)]"));
        }

        [Test]
        public void TestNested()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            var array = new ArrayExpression();
            array.Entries.Add(new IntegerConstantExpression(1));
            array.Entries.Add(new IntegerConstantExpression(2));
            array.Entries.Add(new IntegerConstantExpression(3));
            var dict = new DictionaryExpression();
            var key = new IntegerConstantExpression(0);
            dict.Add(key, array);
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            var result = FunctionTests.Evaluate<ArrayMapFunction>("array_map(dict[0], a => byte(a))", scope);
            Assert.That(result, Is.Not.Null);

            var builder = new StringBuilder();
            result.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("[byte(0x000001), byte(0x000002), byte(0x000003)]"));
        }

        [Test]
        public void TestSingleElement()
        {
            Assert.That(Evaluate("array_map([1], a => a + 2)"),
                Is.EqualTo("[3]"));
        }

        [Test]
        public void TestNonIterable()
        {
            Assert.That(Evaluate("array_map(1, a => byte(a))"),
                Is.EqualTo("Cannot iterate over IntegerConstant: 1"));
        }

        [Test]
        public void TestNoElements()
        {
            Assert.That(Evaluate("array_map([], a => byte(a))"),
                Is.EqualTo("[]"));
        }

        [Test]
        public void TestLogic()
        {
            // if the predicate returns false, ignore the item
            Assert.That(Evaluate("array_map([1, 2, 3], (a) { if (a % 2 == 0) { return byte(a) } else { return false }})"),
                Is.EqualTo("[false, byte(0x000002), false]"));
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("array_map(range(1,5,2), a => byte(a))"),
                Is.EqualTo("[byte(0x000001), byte(0x000003), byte(0x000005)]"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("array_map({1:\"One\",2:\"Two\",3:\"Three\"}, a => byte(a))"),
                Is.EqualTo("[byte(0x000001), byte(0x000002), byte(0x000003)]"));
        }

        [Test]
        public void TestPredicateWithNoParameters()
        {
            Assert.That(Evaluate("array_map([1], () => byte(0x1234))"),
                Is.EqualTo("predicate function must accept a single parameter"));
        }

        [Test]
        public void TestPredicateWithExtraParameters()
        {
            Assert.That(Evaluate("array_map([1], (a, b) => byte(a))"),
                Is.EqualTo("predicate function must accept a single parameter"));
        }

        [Test]
        public void TestMissingReturn()
        {
            Assert.That(Evaluate("array_map([1, 2, 3], (a) { if (a == 2) return a })"),
                Is.EqualTo("predicate did not return a value"));
        }

        [Test]
        public void TestErrorInPredicate()
        {
            Assert.That(Evaluate("array_map([1, 2, 3], (a) { return b })"),
                Is.EqualTo("Unknown variable: b"));
        }
    }
}
