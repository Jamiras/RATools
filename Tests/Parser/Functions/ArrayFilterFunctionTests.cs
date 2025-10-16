using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class ArrayFilterFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new ArrayFilterFunction();
            Assert.That(def.Name.Name, Is.EqualTo("array_filter"));
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
            Assert.That(Evaluate("array_filter([1, 2, 3], a => a > 1)"),
                Is.EqualTo("[2, 3]"));
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

            var result = FunctionTests.Evaluate<ArrayFilterFunction>("array_filter(dict[0], a => a > 1)", scope);
            Assert.That(result, Is.Not.Null);

            var builder = new StringBuilder();
            result.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("[2, 3]"));
        }

        [Test]
        public void TestSingleElement()
        {
            Assert.That(Evaluate("array_filter([1], a => a < 3)"),
                Is.EqualTo("[1]"));
        }

        [Test]
        public void TestNonIterable()
        {
            Assert.That(Evaluate("array_filter(1, a => a > 1)"),
                Is.EqualTo("Cannot iterate over IntegerConstant: 1"));
        }

        [Test]
        public void TestNoElements()
        {
            Assert.That(Evaluate("array_filter([], a => true)"),
                Is.EqualTo("[]"));
        }

        [Test]
        public void TestLogic()
        {
            // if the predicate returns false, ignore the item
            Assert.That(Evaluate("array_filter([1, 2, 3], (a) { if (a % 2 == 0) { return true } else { return false }})"),
                Is.EqualTo("[2]"));
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("array_filter(range(1,20), a => a % 3 == 0)"),
                Is.EqualTo("[3, 6, 9, 12, 15, 18]"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("array_filter({1:\"One\",2:\"Two\",3:\"Three\"}, a => a > 1)"),
                Is.EqualTo("[2, 3]"));
        }

        [Test]
        public void TestPredicateWithNoParameters()
        {
            Assert.That(Evaluate("array_filter([1], () => true)"),
                Is.EqualTo("predicate function must accept a single parameter"));
        }

        [Test]
        public void TestPredicateWithExtraParameters()
        {
            Assert.That(Evaluate("array_filter([1], (a, b) => true"),
                Is.EqualTo("predicate function must accept a single parameter"));
        }

        [Test]
        public void TestMissingReturn()
        {
            Assert.That(Evaluate("array_filter([1, 2, 3], (a) { if (a == 2) return true })"),
                Is.EqualTo("predicate did not return a value"));
        }

        [Test]
        public void TestErrorInPredicate()
        {
            Assert.That(Evaluate("array_filter([1, 2, 3], (a) { return b })"),
                Is.EqualTo("Unknown variable: b"));
        }

        [Test]
        public void TestIntegerFilter()
        {
            Assert.That(Evaluate("array_filter([1, 2, 3], a => a)"),
                Is.EqualTo("Cannot convert integer to boolean"));
        }

        [Test]
        public void TestRuntimeLogicFilter()
        {
            Assert.That(Evaluate("array_filter([1, 2, 3], a => byte(a) > 1)"),
                Is.EqualTo("Cannot filter on runtime logic"));
        }
    }
}
