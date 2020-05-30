﻿using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Test.Parser.Functions
{
    [TestFixture]
    class LengthFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new LengthFunction();
            Assert.That(def.Name.Name, Is.EqualTo("length"));
            Assert.That(def.Parameters.Count, Is.EqualTo(1));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("object"));
        }

        private int Evaluate(string input, InterpreterScope scope, string expectedError = null)
        {
            var funcDef = new LengthFunction();

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
                Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
                return ((IntegerConstantExpression)result).Value;
            }
            else
            {
                if (error == null)
                    Assert.That(funcDef.Evaluate(parameterScope, out error), Is.False);

                Assert.That(error, Is.InstanceOf<ParseErrorExpression>());

                var parseError = (ParseErrorExpression)error;
                Assert.That(parseError.Message, Is.EqualTo(expectedError));

                return int.MinValue;
            }
        }

        [Test]
        public void TestUndefined()
        {
            var scope = new InterpreterScope();

            Evaluate("length(arr)", scope, "Unknown variable: arr");
        }

        [Test]
        public void TestArray()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            array.Entries.Add(new IntegerConstantExpression(1));
            array.Entries.Add(new IntegerConstantExpression(2));
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);

            Assert.That(Evaluate("length(arr)", scope), Is.EqualTo(2));

            array.Entries.Add(new IntegerConstantExpression(9));
            array.Entries.Add(new IntegerConstantExpression(8));
            array.Entries.Add(new IntegerConstantExpression(7));

            Assert.That(Evaluate("length(arr)", scope), Is.EqualTo(5));
        }

        [Test]
        public void TestDictionary()
        {
            var scope = new InterpreterScope();
            var dict = new DictionaryExpression();
            dict.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = new IntegerConstantExpression(1), Value = new StringConstantExpression("One") });
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            Assert.That(Evaluate("length(dict)", scope), Is.EqualTo(1));

            dict.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = new IntegerConstantExpression(5), Value = new StringConstantExpression("Five") });
            dict.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = new IntegerConstantExpression(9), Value = new StringConstantExpression("Nine") });

            Assert.That(Evaluate("length(dict)", scope), Is.EqualTo(3));
        }

        [Test]
        public void TestString()
        {
            var scope = new InterpreterScope();
            scope.DefineVariable(new VariableDefinitionExpression("str"), new StringConstantExpression("Five"));

            Assert.That(Evaluate("length(str)", scope), Is.EqualTo(4));
            Assert.That(Evaluate("length(\"str\")", scope), Is.EqualTo(3));
        }

        [Test]
        public void TestInteger()
        {
            var scope = new InterpreterScope();
            scope.DefineVariable(new VariableDefinitionExpression("i"), new IntegerConstantExpression(12345));

            Evaluate("length(i)", scope, "Cannot calculate length of IntegerConstant");
            Evaluate("length(123)", scope, "Cannot calculate length of IntegerConstant");
        }

        [Test]
        public void TestComparison()
        {
            var scope = new InterpreterScope();
            scope.DefineVariable(new VariableDefinitionExpression("i"), new IntegerConstantExpression(12345));

            Evaluate("length(i != 12)", scope, "Cannot calculate length of Comparison");
        }

        [Test]
        public void TestFunctionCall()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);
            var happyFunc = new FunctionDefinitionExpression("happy");
            happyFunc.Parameters.Add(new VariableDefinitionExpression("str1"));
            happyFunc.Expressions.Add(new ReturnExpression(new MathematicExpression(new VariableExpression("str1"), MathematicOperation.Add, new StringConstantExpression("rama"))));
            scope.AddFunction(happyFunc);

            Assert.That(Evaluate("length(happy(\"banana\"))", scope), Is.EqualTo(10));
        }
    }
}
