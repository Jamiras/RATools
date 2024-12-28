using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class ArrayReduceFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new ArrayReduceFunction();
            Assert.That(def.Name.Name, Is.EqualTo("array_reduce"));
            Assert.That(def.Parameters.Count, Is.EqualTo(3));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("inputs"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("initial"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("reducer"));
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
            Assert.That(Evaluate("array_reduce([1, 2, 3], 0, (acc, v) => acc + v)"),
                Is.EqualTo("6"));
        }

        [Test]
        public void TestComplex()
        {
            Assert.That(Evaluate("array_reduce([1, 2, 3], [], (acc, v) { if (v % 2 == 0) { return acc } else { array_push(acc, byte(v)) return acc } })"),
                Is.EqualTo("[byte(0x000001), byte(0x000003)]"));
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

            var result = FunctionTests.Evaluate<ArrayReduceFunction>("array_reduce(dict[0], 0, (acc, v) => acc + v)", scope);
            Assert.That(result, Is.Not.Null);

            var builder = new StringBuilder();
            result.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("6"));
        }

        [Test]
        public void TestSingleElement()
        {
            Assert.That(Evaluate("array_reduce([1], 3, (acc, v) => acc * v)"),
                Is.EqualTo("3"));
        }

        [Test]
        public void TestNonIterable()
        {
            Assert.That(Evaluate("array_reduce(1, 0, a => byte(a))"),
                Is.EqualTo("Cannot iterate over IntegerConstant: 1"));
        }

        [Test]
        public void TestNoElements()
        {
            Assert.That(Evaluate("array_reduce([], 100, (acc, v) => acc + v)"),
                Is.EqualTo("100"));
        }

        [Test]
        public void TestLogic()
        {
            // if the predicate returns false, ignore the item
            Assert.That(Evaluate("array_reduce([1, 2, 3], 0, (acc, v) { if (v % 2 == 0) { return acc } else { return acc + v }})"),
                Is.EqualTo("4"));
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("array_reduce(range(1,100), 0, (acc, v) => acc + v)"),
                Is.EqualTo("5050"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("array_reduce({1:\"One\",2:\"Two\",3:\"Three\"}, 0, (acc, v) => acc + v)"),
                Is.EqualTo("6"));
        }

        [Test]
        public void TestReducerWithNoParameters()
        {
            Assert.That(Evaluate("array_reduce([1], 0, () => byte(0x1234))"),
                Is.EqualTo("reducer function must accept two parameters (acc, value)"));
        }

        [Test]
        public void TestReducerWithExtraParameters()
        {
            Assert.That(Evaluate("array_reduce([1], 0, (a, b, c) => byte(a))"),
                Is.EqualTo("reducer function must accept two parameters (acc, value)"));
        }

        [Test]
        public void TestMissingReturn()
        {
            Assert.That(Evaluate("array_reduce([1, 2, 3], 0, (acc, v) { if (v > 4) return acc + v })"),
                Is.EqualTo("reducer function did not return a value"));
        }

        [Test]
        public void TestErrorInReducer()
        {
            Assert.That(Evaluate("array_reduce([1, 2, 3], 0, (acc, v) { return b })"),
                Is.EqualTo("Unknown variable: b"));
        }
    }
}
