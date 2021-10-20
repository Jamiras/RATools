using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser;
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
                while (parseError.InnerError != null)
                    parseError = parseError.InnerError;
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
            dict.Add(new IntegerConstantExpression(1), new StringConstantExpression("One"));
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            Assert.That(Evaluate("length(dict)", scope), Is.EqualTo(1));

            dict.Add(new IntegerConstantExpression(5), new StringConstantExpression("Five"));
            dict.Add(new IntegerConstantExpression(9), new StringConstantExpression("Nine"));

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
        public void TestBoolean()
        {
            var scope = new InterpreterScope();
            scope.DefineVariable(new VariableDefinitionExpression("i"), new IntegerConstantExpression(12345));

            Evaluate("length(true)", scope, "Cannot calculate length of BooleanConstant");
        }

        [Test]
        public void TestComparison()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            Evaluate("length(byte(0x1234) != 12)", scope, "Cannot calculate length of Comparison");
        }

        [Test]
        public void TestFunctionCall()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);
            scope.AddFunction(UserFunctionDefinitionExpression.ParseForTest(
                "function happy(val) => val + \"rama\""
            ));

            Assert.That(Evaluate("length(happy(\"banana\"))", scope), Is.EqualTo(10));
        }
    }
}
