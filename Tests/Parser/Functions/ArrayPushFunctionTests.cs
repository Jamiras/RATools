using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
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

        private static void Evaluate(string input, InterpreterScope scope)
        {
            FunctionTests.Execute<ArrayPushFunction>(input, scope);
        }

        private static void AssertEvaluateError(string input, InterpreterScope scope, string expectedError)
        {
            FunctionTests.AssertEvaluateError<ArrayPushFunction>(input, scope, expectedError);
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
        public void TestNested()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            var dict = new DictionaryExpression();
            var key = new IntegerConstantExpression(0);
            dict.Add(key, array);
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            Evaluate("array_push(dict[0], 1)", scope);
            Assert.That(array.Entries.Count, Is.EqualTo(1));
            Assert.That(array.Entries[0], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)array.Entries[0]).Value, Is.EqualTo(1));

            Evaluate("array_push(dict[0], \"2\")", scope);
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

            AssertEvaluateError("array_push(arr, 1)", scope, "Unknown variable: arr");
        }

        [Test]
        public void TestDictionary()
        {
            var scope = new InterpreterScope();
            var dict = new DictionaryExpression();
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            AssertEvaluateError("array_push(dict, 1)", scope, "array: Cannot convert dictionary to array");
        }

        private static void AddHappyFunction(InterpreterScope scope)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(
                "function happy(num1) => num1"
            ));
            var function = ExpressionBase.Parse(tokenizer) as FunctionDefinitionExpression;
            scope.AddFunction(function);
        }

        [Test]
        public void TestPushFunctionCall()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);
            AddHappyFunction(scope);

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
            AddHappyFunction(scope);

            Evaluate("array_push(arr, happy(1) == 2)", scope);

            var comparison = (BooleanConstantExpression)array.Entries[0];
            Assert.That(comparison.Value, Is.False);
        }

        [Test]
        public void TestPushMemoryComparison()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            var array = new ArrayExpression();
            scope.DefineVariable(new VariableDefinitionExpression("arr"), array);
            AddHappyFunction(scope);

            Evaluate("array_push(arr, byte(1) == 2)", scope);

            var comparison = (RequirementConditionExpression)array.Entries[0];
            Assert.That(comparison.Left, Is.InstanceOf<MemoryAccessorExpression>());
            Assert.That(((MemoryAccessorExpression)comparison.Left).Field.Size, Is.EqualTo(FieldSize.Byte));
            Assert.That(((MemoryAccessorExpression)comparison.Left).Field.Value, Is.EqualTo(1));
            Assert.That(comparison.Right, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)comparison.Right).Value, Is.EqualTo(2));
        }
    }
}
