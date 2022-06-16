using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser;
using RATools.Parser.Internal;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace RATools.Test.Parser.Internal
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
            tokenizer.Match("{");
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
            tokenizer.Match("{");
            var expression = DictionaryExpression.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<DictionaryExpression>());
            var dict = (DictionaryExpression)expression;
            Assert.That(dict.Count, Is.EqualTo(0));
            Assert.That(group.ParseErrors.Count(), Is.GreaterThan(0));
            Assert.That(group.ParseErrors.ElementAt(0).Message, Is.EqualTo("Expecting colon following key expression"));
            Assert.That(group.ParseErrors.ElementAt(0).Location.Start.Line, Is.EqualTo(1));
            Assert.That(group.ParseErrors.ElementAt(0).Location.Start.Column, Is.EqualTo(4));
        }

        [Test]
        public void TestParseMissingComma()
        {
            var group = new ExpressionGroup();
            var tokenizer = CreateTokenizer("{1: \"a\"\n 2: \"b\"}", group);
            tokenizer.Match("{");
            var expression = DictionaryExpression.Parse(tokenizer);
            Assert.That(expression, Is.InstanceOf<DictionaryExpression>());
            var dict = (DictionaryExpression)expression;
            Assert.That(group.ParseErrors.Count(), Is.GreaterThan(0));
            Assert.That(group.ParseErrors.ElementAt(0).Message, Is.EqualTo("Expecting comma between entries"));
            Assert.That(group.ParseErrors.ElementAt(0).Location.Start.Line, Is.EqualTo(2));
            Assert.That(group.ParseErrors.ElementAt(0).Location.Start.Column, Is.EqualTo(2));
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
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Dictionary key must evaluate to a constant"));
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
            Assert.That(arrayResult[0].Value.ToString(), Is.EqualTo(value.ToString()));
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
    }
}
