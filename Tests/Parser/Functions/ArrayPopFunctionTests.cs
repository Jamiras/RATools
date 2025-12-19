using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Tests.Functions
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
            Assert.That(def.Parameters.ElementAt(0).IsMutableReference, Is.True);
        }

        private static ExpressionBase Evaluate(string input, InterpreterScope scope)
        {
            return FunctionTests.Evaluate<ArrayPopFunction>(input, scope);
        }

        private static void AssertEvaluateError(string input, InterpreterScope scope, string expectedError)
        {
            FunctionTests.AssertEvaluateError<ArrayPopFunction>(input, scope, expectedError);
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
        public void TestNested()
        {
            var scope = new InterpreterScope();
            var array = new ArrayExpression();
            array.Entries.Add(new IntegerConstantExpression(1));
            array.Entries.Add(new IntegerConstantExpression(2));
            var dict = new DictionaryExpression();
            var key = new IntegerConstantExpression(0);
            dict.Add(key, array);
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            var entry = Evaluate("array_pop(dict[0])", scope);
            Assert.That(entry, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)entry).Value, Is.EqualTo(2));
            Assert.That(array.Entries.Count, Is.EqualTo(1));
            Assert.That(array.Entries[0], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)array.Entries[0]).Value, Is.EqualTo(1));

            entry = Evaluate("array_pop(dict[0])", scope);
            Assert.That(entry, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)entry).Value, Is.EqualTo(1));
            Assert.That(array.Entries.Count, Is.EqualTo(0));

            // empty array always returns 0
            entry = Evaluate("array_pop(dict[0])", scope);
            Assert.That(entry, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)entry).Value, Is.EqualTo(0));
            Assert.That(array.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestUndefined()
        {
            var scope = new InterpreterScope();

            AssertEvaluateError("array_pop(arr)", scope, "Unknown variable: arr");
        }

        [Test]
        public void TestDictionary()
        {
            var scope = new InterpreterScope();
            var dict = new DictionaryExpression();
            dict.Add(new IntegerConstantExpression(1), new StringConstantExpression("One"));
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            AssertEvaluateError("array_pop(dict)", scope, "array: Cannot convert dictionary to array");
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

        [Test]
        public void TestGetModifications()
        {
            var input =
                "arr = [1,2,3,4]\r\n" +
                "b = array_pop(arr)";
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new AchievementScriptInterpreter();
            var groups = parser.Parse(tokenizer);

            // before execution, we don't know if a parameter will be a reference
            var expr = groups.Groups.ElementAt(1).Expressions.ElementAt(0);
            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);
            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("b"));

            AchievementScriptInterpreter.InitializeScope(groups, null);
            parser.Run(groups);

            // after execution, we do
            ((INestedExpressions)expr).GetModifications(modifications);
            Assert.That(modifications.Count, Is.EqualTo(2));
            Assert.That(modifications.Contains("b"));
            Assert.That(modifications.Contains("arr"));
        }

        [Test]
        public void TestGetModificationsNested()
        {
            var input =
                "function f(a) { array_pop(a) }\r\n" +
                "arr = [1,2,3,4]\r\n" +
                "f(arr)";
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new AchievementScriptInterpreter();
            var groups = parser.Parse(tokenizer);

            // before execution, we don't know if a parameter will be a reference
            var expr = groups.Groups.ElementAt(2).Expressions.ElementAt(0);
            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            AchievementScriptInterpreter.InitializeScope(groups, null);
            parser.Run(groups);

            // after execution, we do
            ((INestedExpressions)expr).GetModifications(modifications);
            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("arr"));
        }
    }
}
