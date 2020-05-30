using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Test.Parser.Functions
{
    [TestFixture]
    class ArrayPopFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new ArrayPopFunction();
            Assert.That(def.Name.Name, Is.EqualTo("array_pop"));
            Assert.That(def.Parameters.Count, Is.EqualTo(1));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("array"));
        }

        private ExpressionBase Evaluate(string input, InterpreterScope scope, string expectedError = null)
        {
            var funcDef = new ArrayPopFunction();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var parameterScope = funcCall.GetParameters(funcDef, scope, out error);

            if (expectedError == null)
            {
                Assert.That(error, Is.Null);

                ExpressionBase result;
                Assert.That(funcDef.Evaluate(parameterScope, out result), Is.True);
                return result;
            }
            else
            {
                if (error == null)
                    Assert.That(funcDef.Evaluate(parameterScope, out error), Is.False);

                Assert.That(error, Is.InstanceOf<ParseErrorExpression>());

                var parseError = (ParseErrorExpression)error;
                Assert.That(parseError.Message, Is.EqualTo(expectedError));

                return null;
            }
        }

        [Test]
        public void TestSimple()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            array.Entries.Add(new IntegerConstantExpression(1));
            array.Entries.Add(new IntegerConstantExpression(2));
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);

            var entry = Evaluate("array_pop(arr)", scope);
            Assert.That(entry, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)entry).Value, Is.EqualTo(2));
            Assert.That(array.Entries.Count, Is.EqualTo(1));
            Assert.That(array.Entries[0], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)array.Entries[0]).Value, Is.EqualTo(1));

            entry = Evaluate("array_pop(arr)", scope);
            Assert.That(entry, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)entry).Value, Is.EqualTo(1));
            Assert.That(array.Entries.Count, Is.EqualTo(0));

            // empty array always returns 0
            entry = Evaluate("array_pop(arr)", scope);
            Assert.That(entry, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)entry).Value, Is.EqualTo(0));
            Assert.That(array.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestUndefined()
        {
            var scope = new InterpreterScope();

            Evaluate("array_pop(arr)", scope, "Unknown variable: arr");
        }

        [Test]
        public void TestDictionary()
        {
            var scope = new InterpreterScope();
            var dict = new DictionaryExpression();
            dict.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = new IntegerConstantExpression(1), Value = new StringConstantExpression("One") });
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            Evaluate("array_push(dict)", scope, "array did not evaluate to an array");
        }

        [Test]
        public void TestPopFunctionCall()
        {
            var scope = new InterpreterScope();

            var array = new ArrayExpression();
            array.Entries.Add(new FunctionCallExpression("happy", new ExpressionBase[] { new IntegerConstantExpression(1) }));
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);

            var happyFunc = new FunctionDefinitionExpression("happy");
            happyFunc.Parameters.Add(new VariableDefinitionExpression("num1"));
            happyFunc.Expressions.Add(new ReturnExpression(new VariableExpression("num1")));
            scope.AddFunction(happyFunc);

            var entry = Evaluate("array_pop(arr)", scope);

            // function call should not be evaluated when it's popped off the array
            Assert.That(entry, Is.InstanceOf<FunctionCallExpression>());
            Assert.That(((FunctionCallExpression)entry).FunctionName.Name, Is.EqualTo("happy"));
            Assert.That(((FunctionCallExpression)entry).Parameters.Count, Is.EqualTo(1));

            Assert.That(array.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestPopComparison()
        {
            var scope = new InterpreterScope();

            var array = new ArrayExpression();
            var funcCall = new FunctionCallExpression("happy", new ExpressionBase[] { new IntegerConstantExpression(1) });
            array.Entries.Add(new ComparisonExpression(funcCall, ComparisonOperation.Equal, new IntegerConstantExpression(2)));
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);

            var happyFunc = new FunctionDefinitionExpression("happy");
            happyFunc.Parameters.Add(new VariableDefinitionExpression("num1"));
            happyFunc.Expressions.Add(new ReturnExpression(new VariableExpression("num1")));
            scope.AddFunction(happyFunc);

            var entry = Evaluate("array_pop(arr)", scope);
            Assert.That(entry, Is.InstanceOf<ComparisonExpression>());

            var comparison = (ComparisonExpression)entry;
            Assert.That(comparison.Left, Is.InstanceOf<FunctionCallExpression>());
            Assert.That(((FunctionCallExpression)comparison.Left).FunctionName.Name, Is.EqualTo("happy"));
            Assert.That(comparison.Right, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)comparison.Right).Value, Is.EqualTo(2));
        }
    }
}
