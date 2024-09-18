using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Expressions
{
    [TestFixture]
    class DictionaryExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new DictionaryExpression();
            expr.Add(new VariableExpression("a"), new IntegerConstantExpression(1));
            expr.Add(new IntegerConstantExpression(2), new StringConstantExpression("banana"));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("{a: 1, 2: \"banana\"}"));
        }

        [Test]
        public void TestAppendStringEmpty()
        {
            var expr = new DictionaryExpression();

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("{}"));
        }

        private static PositionalTokenizer CreateTokenizer(string input, ExpressionGroup group = null)
        {
            if (group == null)
                return new PositionalTokenizer(Tokenizer.CreateTokenizer(input));

            return new ExpressionTokenizer(Tokenizer.CreateTokenizer(input), group);
        }

        [Test]
        public void TestParse()
        {
            var tokenizer = CreateTokenizer("{1: \"a\", 2: \"b\"}");
            var expression = DictionaryExpression.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<DictionaryExpression>());
            var dict = (DictionaryExpression)expression;
            Assert.That(dict.Count, Is.EqualTo(2));
        }

        [Test]
        public void TestParseEquals()
        {
            var group = new ExpressionGroup();
            var tokenizer = CreateTokenizer("{1 = \"a\", 2 = \"b\"}", group);
            var expression = DictionaryExpression.Parse(tokenizer);
            ExpressionTests.AssertError(expression, "Expecting colon following key expression");
            Assert.That(expression.Location.Start.Line, Is.EqualTo(1));
            Assert.That(expression.Location.Start.Column, Is.EqualTo(4));
        }

        [Test]
        public void TestParseMissingComma()
        {
            var group = new ExpressionGroup();
            var tokenizer = CreateTokenizer("{1: \"a\"\n 2: \"b\"}", group);
            var expression = DictionaryExpression.Parse(tokenizer);
            ExpressionTests.AssertError(expression, "Expecting comma between entries");
            Assert.That(expression.Location.Start.Line, Is.EqualTo(2));
            Assert.That(expression.Location.Start.Column, Is.EqualTo(2));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(98);
            var value2 = new IntegerConstantExpression(99);
            var value3 = new IntegerConstantExpression(1);
            var value4 = new IntegerConstantExpression(2);
            var expr = new DictionaryExpression();
            expr.Add(variable1, value3);
            expr.Add(value4, variable2);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value2);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<DictionaryExpression>());
            var dictResult = (DictionaryExpression)result;
            Assert.That(dictResult.Count, Is.EqualTo(2));

            // resulting list will be sorted for quicker lookups
            Assert.That(dictResult[0].Key, Is.EqualTo(value4));
            Assert.That(dictResult[0].Value, Is.EqualTo(value2));
            Assert.That(dictResult[1].Key, Is.EqualTo(value1));
            Assert.That(dictResult[1].Value, Is.EqualTo(value3));
        }

        [Test]
        public void TestReplaceVariablesFunctionCall()
        {
            var functionDefinition = UserFunctionDefinitionExpression.ParseForTest("function func(i) => 6");

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(2) });
            var value1 = new IntegerConstantExpression(98);
            var expr = new DictionaryExpression();
            expr.Add(functionCall, value1);

            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<DictionaryExpression>());
            var dictResult = (DictionaryExpression)result;
            Assert.That(dictResult.Count, Is.EqualTo(1));
            Assert.That(dictResult[0].Key, Is.EqualTo(new IntegerConstantExpression(6)));
            Assert.That(dictResult[0].Value, Is.EqualTo(value1));
        }

        [Test]
        public void TestReplaceVariablesLogicalFunctionCall()
        {
            var functionDefinition = UserFunctionDefinitionExpression.ParseForTest("function func(i) => byte(i) == 1");

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(2) });
            var value1 = new IntegerConstantExpression(98);
            var expr = new DictionaryExpression();
            expr.Add(functionCall, value1);

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.AddFunction(functionDefinition);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Dictionary key must evaluate to a string or numeric constant"));
        }

        [Test]
        public void TestReplaceVariablesArray()
        {
            var arrayDefinition = new ArrayExpression();
            arrayDefinition.Entries.Add(new IntegerConstantExpression(2));

            var value1 = new IntegerConstantExpression(98);
            var expr = new DictionaryExpression();
            expr.Add(arrayDefinition, value1);

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Dictionary key must evaluate to a string or numeric constant"));
        }

        [Test]
        public void TestReplaceVariablesMethodCall()
        {
            var functionDefinition = UserFunctionDefinitionExpression.ParseForTest("function func(i) { j = i }");

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(2) });
            var value1 = new IntegerConstantExpression(98);
            var expr = new DictionaryExpression();
            expr.Add(functionCall, value1);

            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            var parseError = (ErrorExpression)result;
            while (parseError.InnerError != null)
                parseError = parseError.InnerError;
            Assert.That(parseError.Message, Is.EqualTo("func did not return a value"));
        }

        [Test]
        public void TestReplaceVariablesDuplicateKey()
        {
            var value1 = new IntegerConstantExpression(1);
            var value3 = new IntegerConstantExpression(3);
            var value4 = new IntegerConstantExpression(4);
            var expr = new DictionaryExpression();
            expr.Add(value1, value3);
            expr.Add(value1, value4);

            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("1 already exists in dictionary"));
        }

        [Test]
        public void TestReplaceVariablesDuplicateKeyResolved()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(1);
            var value3 = new IntegerConstantExpression(3);
            var value4 = new IntegerConstantExpression(4);
            var expr = new DictionaryExpression();
            expr.Add(variable1, value3);
            expr.Add(variable2, value4);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value1);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("1 already exists in dictionary"));
        }

        [Test]
        public void TestReplaceVariablesMemoryAccessor()
        {
            var key = new IntegerConstantExpression(6);
            var value = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(1) });
            var expr = new DictionaryExpression();
            expr.Add(key, value);

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);

            Assert.That(result, Is.InstanceOf<DictionaryExpression>());
            var arrayResult = (DictionaryExpression)result;
            Assert.That(arrayResult.Count, Is.EqualTo(1));

            var builder1 = new StringBuilder();
            arrayResult[0].Value.AppendString(builder1);
            Assert.That(builder1.ToString(), Is.EqualTo("byte(0x000001)"));
        }

        [Test]
        public void TestReplaceVariablesCreatesCopy()
        {
            var key1 = new IntegerConstantExpression(1);
            var key2 = new IntegerConstantExpression(2);
            var value1 = new StringConstantExpression("One");
            var value2 = new StringConstantExpression("Two");
            var dict1 = new DictionaryExpression();
            dict1.Add(key1, value1);
            dict1.Add(key2, value2);

            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(dict1.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<DictionaryExpression>());
            var dict2 = (DictionaryExpression)result;
            Assert.That(dict2.Count, Is.EqualTo(2));

            // item added to original dictionary does not appear in evaluated dictionary
            var key3 = new IntegerConstantExpression(3);
            var value3 = new StringConstantExpression("Three");
            dict1.Add(key3, value3);
            Assert.That(dict1.Count, Is.EqualTo(3));
            Assert.That(dict2.Count, Is.EqualTo(2));

            // item added to evaluated dictionary does not appear in original dictionary
            var key4 = new IntegerConstantExpression(4);
            var value4 = new StringConstantExpression("Four");
            dict2.Add(key4, value4);
            Assert.That(dict1.Count, Is.EqualTo(3));
            Assert.That(dict2.Count, Is.EqualTo(3));

            Assert.That(dict1.Keys.Contains(key3));
            Assert.That(!dict2.Keys.Contains(key3));
            Assert.That(!dict1.Keys.Contains(key4));
            Assert.That(dict2.Keys.Contains(key4));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var key1 = new VariableExpression("key1");
            var value1 = new IntegerConstantExpression(99);
            var key2 = new StringConstantExpression("key2");
            var value2 = new VariableExpression("value2");
            var expr = new DictionaryExpression();
            expr.Add(key1, value1);
            expr.Add(key2, value2);

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(4));
            Assert.That(nested.Contains(key1));
            Assert.That(nested.Contains(value1));
            Assert.That(nested.Contains(key2));
            Assert.That(nested.Contains(value2));
        }

        [Test]
        public void TestGetDependencies()
        {
            var key1 = new VariableExpression("key1");
            var value1 = new IntegerConstantExpression(99);
            var key2 = new StringConstantExpression("key2");
            var value2 = new VariableExpression("value2");
            var expr = new DictionaryExpression();
            expr.Add(key1, value1);
            expr.Add(key2, value2);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("key1"));
            Assert.That(dependencies.Contains("value2"));
        }

        [Test]
        public void TestGetModifications()
        {
            var key1 = new VariableExpression("key1");
            var value1 = new IntegerConstantExpression(99);
            var key2 = new StringConstantExpression("key2");
            var value2 = new VariableExpression("value2");
            var expr = new DictionaryExpression();
            expr.Add(key1, value1);
            expr.Add(key2, value2);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestAddNestedEntry()
        {
            var dict = new DictionaryExpression();
            var subdict = new DictionaryExpression();
            var key = new IntegerConstantExpression(1);
            dict.Add(key, subdict);

            var scope = new InterpreterScope();
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dict);

            var assignment = ExpressionTests.Parse<AssignmentExpression>("dict[1][2] = 3");
            Assert.That(assignment.Execute(scope), Is.Null);

            Assert.That(subdict.Entries.Count, Is.EqualTo(1));
            var entry = subdict.Entries.First();
            Assert.That(entry.Key, Is.EqualTo(new IntegerConstantExpression(2)));
            Assert.That(entry.Value, Is.EqualTo(new IntegerConstantExpression(3)));

            assignment = ExpressionTests.Parse<AssignmentExpression>("dict[1][2] = 4");
            Assert.That(assignment.Execute(scope), Is.Null);

            Assert.That(subdict.Entries.Count, Is.EqualTo(1));
            entry = subdict.Entries.First();
            Assert.That(entry.Key, Is.EqualTo(new IntegerConstantExpression(2)));
            Assert.That(entry.Value, Is.EqualTo(new IntegerConstantExpression(4)));
        }

        [Test]
        public void TestFunctionValue()
        {
            string input =
                "{" +
                    "\"a\": inc," +          // direct function reference
                    "\"b\": (n) => n + 1," + // anonymous function
                "}";
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var dictionary = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(dictionary, Is.InstanceOf<DictionaryExpression>());

            tokenizer = Tokenizer.CreateTokenizer("function inc(d) => d + 1");
            var function = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(function, Is.InstanceOf<FunctionDefinitionExpression>());

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.AddFunction((FunctionDefinitionExpression)function);
            scope.DefineVariable(new VariableDefinitionExpression("dict"), dictionary);

            tokenizer = Tokenizer.CreateTokenizer("v = dict[\"a\"](5)");
            var assignment = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(assignment, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)assignment).Execute(scope), Is.Null);

            var value = scope.GetVariable("v");
            Assert.That(value, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(6));

            tokenizer = Tokenizer.CreateTokenizer("v = dict[\"b\"](2)");
            assignment = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(assignment, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)assignment).Execute(scope), Is.Null);

            value = scope.GetVariable("v");
            Assert.That(value, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(3));

            tokenizer = Tokenizer.CreateTokenizer("x = dict[\"b\"]");
            assignment = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(assignment, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)assignment).Execute(scope), Is.Null);

            tokenizer = Tokenizer.CreateTokenizer("v = x(6)");
            assignment = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(assignment, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)assignment).Execute(scope), Is.Null);

            value = scope.GetVariable("v");
            Assert.That(value, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(7));
        }

        [Test]
        public void TestFunctionChainValue()
        {
            var scope = AchievementScriptTests.Evaluate(
                "function a(b) => b + 1\n" +
                "function c(d) => a(d)\n" +
                "function e(f) => c(f)\n" +
                "dict = { \"func\": e }\n" +
                "i = dict[\"func\"](6)\n");

            var i = scope.GetVariable("i");
            Assert.That(i, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)i).Value, Is.EqualTo(7));
        }
        
        [Test]
        public void TestSignedUnsignedKeys()
        {
            // Values in this dictionary are both near int.MinValue and near int.MaxValue. Values more
            // than int.MaxValue distance from each other will overflow into the sign bit and get sorted
            // incorrectly (see issue #487).
            var dictionary = new DictionaryExpression();
            dictionary.Add(new IntegerConstantExpression(0x6b450446), new StringConstantExpression("0x6b450446"));
            dictionary.Add(new IntegerConstantExpression(0x49e39403), new StringConstantExpression("0x49e39403"));
            dictionary.Add(new IntegerConstantExpression(0x57c4ea83), new StringConstantExpression("0x57c4ea83"));
            dictionary.Add(new IntegerConstantExpression(0x69e63d11), new StringConstantExpression("0x69e63d11"));
            dictionary.Add(new IntegerConstantExpression(0x75a03c4c), new StringConstantExpression("0x75a03c4c"));
            dictionary.Add(new IntegerConstantExpression(0x1ac0a35e), new StringConstantExpression("0x1ac0a35e"));
            dictionary.Add(new IntegerConstantExpression(0x131e5f8b), new StringConstantExpression("0x131e5f8b"));
            dictionary.Add(new UnsignedIntegerConstantExpression(0xcaac63ee), new StringConstantExpression("0xcaac63ee"));
            dictionary.Add(new UnsignedIntegerConstantExpression(0xf991002d), new StringConstantExpression("0xf991002d"));
            dictionary.Add(new IntegerConstantExpression(0x417b1aa8), new StringConstantExpression("0x417b1aa8"));
            dictionary.Add(new IntegerConstantExpression(0x2d1f51f8), new StringConstantExpression("0x2d1f51f8"));
            dictionary.Add(new IntegerConstantExpression(-10), new StringConstantExpression("-10"));

            Action<ExpressionBase, string> assertValue = (key, expected) =>
            {
                var value = dictionary.GetEntry(key);
                Assert.That(value, Is.InstanceOf<StringConstantExpression>());
                Assert.That(((StringConstantExpression)value).Value, Is.EqualTo(expected));
            };

            assertValue(new IntegerConstantExpression(0x6b450446), "0x6b450446");
            assertValue(new IntegerConstantExpression(0x49e39403), "0x49e39403");
            assertValue(new IntegerConstantExpression(0x57c4ea83), "0x57c4ea83");
            assertValue(new IntegerConstantExpression(0x69e63d11), "0x69e63d11");
            assertValue(new IntegerConstantExpression(0x75a03c4c), "0x75a03c4c");
            assertValue(new IntegerConstantExpression(0x1ac0a35e), "0x1ac0a35e");
            assertValue(new IntegerConstantExpression(0x131e5f8b), "0x131e5f8b");
            assertValue(new UnsignedIntegerConstantExpression(0xcaac63ee), "0xcaac63ee");
            assertValue(new UnsignedIntegerConstantExpression(0xf991002d), "0xf991002d");
            assertValue(new IntegerConstantExpression(0x417b1aa8), "0x417b1aa8");
            assertValue(new IntegerConstantExpression(0x2d1f51f8), "0x2d1f51f8");
            assertValue(new IntegerConstantExpression(-10), "-10");
        }
    }
}
