using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Test.Parser.Functions
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
                    var builder = new System.Text.StringBuilder();
                    expr.AppendString(builder);
                    return builder.ToString();
                }
            }

            var error = expr as ParseErrorExpression;
            if (error != null)
                return error.InnermostError.Message;

            return expr.ToString();
        }

        [Test]
        public void TestSimple()
        {
            Assert.That(Evaluate("array_map([1, 2, 3], a => byte(a))"),
                Is.EqualTo("[byte(1), byte(2), byte(3)]"));
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
                Is.EqualTo("[false, byte(2), false]"));
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("array_map(range(1,5,2), a => byte(a))"),
                Is.EqualTo("[byte(1), byte(3), byte(5)]"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("array_map({1:\"One\",2:\"Two\",3:\"Three\"}, a => byte(a))"),
                Is.EqualTo("[byte(1), byte(2), byte(3)]"));
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
    }
}
