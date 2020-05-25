using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Test.Parser.Functions
{
    [TestFixture]
    class ArrayPushFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new ArrayPushFunction();
            Assert.That(def.Name.Name, Is.EqualTo("array_push"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("array"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("value"));
        }

        private void Evaluate(string input, InterpreterScope scope, string expectedError = null)
        {
            var funcDef = new ArrayPushFunction();

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
                Assert.That(result, Is.InstanceOf<ArrayExpression>());
            }
            else
            {
                if (error == null)
                    Assert.That(funcDef.Evaluate(parameterScope, out error), Is.False);

                Assert.That(error, Is.InstanceOf<ParseErrorExpression>());

                var parseError = (ParseErrorExpression)error;
                Assert.That(parseError.Message, Is.EqualTo(expectedError));
            }
        }

        [Test]
        public void TestSimple()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);

            Evaluate("array_push(arr, 1)", scope);
            Assert.That(array.Entries.Count, Is.EqualTo(1));
            Assert.That(array.Entries[0], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)array.Entries[0]).Value, Is.EqualTo(1));

            Evaluate("array_push(arr, \"2\")", scope);
            Assert.That(array.Entries.Count, Is.EqualTo(2));
            Assert.That(array.Entries[0], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)array.Entries[0]).Value, Is.EqualTo(1));
            Assert.That(array.Entries[1], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)array.Entries[1]).Value, Is.EqualTo("2"));
        }

        [Test]
        public void TestUndefined()
        {
            var scope = new InterpreterScope();

            Evaluate("array_push(arr, 1)", scope, "Unknown variable: arr");
        }

        [Test]
        public void TestDictionary()
        {
            var scope = new InterpreterScope();
            var dict = new DictionaryExpression();
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            Evaluate("array_push(dict, 1)", scope, "array did not evaluate to an array");
        }

        [Test]
        public void TestPushFunctionCall()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);
            var happyFunc = new FunctionDefinitionExpression("happy");
            happyFunc.Parameters.Add(new VariableDefinitionExpression("num1"));
            happyFunc.Expressions.Add(new ReturnExpression(new VariableExpression("num1")));
            scope.AddFunction(happyFunc);

            Evaluate("array_push(arr, happy(1))", scope);

            // function call should be evaluated before its pushed onto the array
            Assert.That(array.Entries.Count, Is.EqualTo(1));
            Assert.That(array.Entries[0], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)array.Entries[0]).Value, Is.EqualTo(1));
        }

        [Test]
        public void TestPushComparison()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);
            var happyFunc = new FunctionDefinitionExpression("happy");
            happyFunc.Parameters.Add(new VariableDefinitionExpression("num1"));
            happyFunc.Expressions.Add(new ReturnExpression(new VariableExpression("num1")));
            scope.AddFunction(happyFunc);

            Evaluate("array_push(arr, happy(1) == 2)", scope);

            var comparison = (ComparisonExpression)array.Entries[0];
            Assert.That(comparison.Left, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)comparison.Left).Value, Is.EqualTo(1));
            Assert.That(comparison.Right, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)comparison.Right).Value, Is.EqualTo(2));
        }

        [Test]
        public void TestPushMemoryComparison()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            var array = new ArrayExpression();
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);
            var happyFunc = new FunctionDefinitionExpression("happy");
            happyFunc.Parameters.Add(new VariableDefinitionExpression("num1"));
            happyFunc.Expressions.Add(new ReturnExpression(new VariableExpression("num1")));
            scope.AddFunction(happyFunc);

            Evaluate("array_push(arr, byte(1) == 2)", scope);

            var comparison = (ComparisonExpression)array.Entries[0];
            Assert.That(comparison.Left, Is.InstanceOf<FunctionCallExpression>());
            Assert.That(((FunctionCallExpression)comparison.Left).FunctionName.Name, Is.EqualTo("byte"));
            Assert.That(comparison.Right, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)comparison.Right).Value, Is.EqualTo(2));
        }
    }
}
