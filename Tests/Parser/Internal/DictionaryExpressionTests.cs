using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser;
using RATools.Parser.Internal;
using System.Text;
using System.Linq;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class DictionaryExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new DictionaryExpression();
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry
            {
                Key = new VariableExpression("a"),
                Value = new IntegerConstantExpression(1)
            });

            expr.Entries.Add(new DictionaryExpression.DictionaryEntry
            {
                Key = new IntegerConstantExpression(2),
                Value = new StringConstantExpression("banana")
            });

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
            Assert.That(dict.Entries.Count, Is.EqualTo(2));
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
            Assert.That(dict.Entries.Count, Is.EqualTo(0));
            Assert.That(group.Errors.Count(), Is.GreaterThan(0));
            Assert.That(group.Errors.ElementAt(0).Message, Is.EqualTo("Expecting colon following key expression"));
            Assert.That(group.Errors.ElementAt(0).Line, Is.EqualTo(1));
            Assert.That(group.Errors.ElementAt(0).Column, Is.EqualTo(4));
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
            Assert.That(group.Errors.Count(), Is.GreaterThan(0));
            Assert.That(group.Errors.ElementAt(0).Message, Is.EqualTo("Expecting comma between entries"));
            Assert.That(group.Errors.ElementAt(0).Line, Is.EqualTo(2));
            Assert.That(group.Errors.ElementAt(0).Column, Is.EqualTo(2));
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
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = variable1, Value = value3 });
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = value4, Value = variable2 });

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value2);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<DictionaryExpression>());
            var dictResult = (DictionaryExpression)result;
            Assert.That(dictResult.Entries.Count, Is.EqualTo(2));

            // resulting list will be sorted for quicker lookups
            Assert.That(dictResult.Entries[0].Key, Is.EqualTo(value4));
            Assert.That(dictResult.Entries[0].Value, Is.EqualTo(value2));
            Assert.That(dictResult.Entries[1].Key, Is.EqualTo(value1));
            Assert.That(dictResult.Entries[1].Value, Is.EqualTo(value3));
        }

        [Test]
        public void TestReplaceVariablesFunctionCall()
        {
            var input = "function func(i) => 6";
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("function");
            var functionDefinition = (FunctionDefinitionExpression)FunctionDefinitionExpression.Parse(tokenizer);

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(2) });
            var value1 = new IntegerConstantExpression(98);
            var expr = new DictionaryExpression();
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = functionCall, Value = value1 });

            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<DictionaryExpression>());
            var dictResult = (DictionaryExpression)result;
            Assert.That(dictResult.Entries.Count, Is.EqualTo(1));
            Assert.That(dictResult.Entries[0].Key, Is.EqualTo(new IntegerConstantExpression(6)));
            Assert.That(dictResult.Entries[0].Value, Is.EqualTo(value1));
        }

        [Test]
        public void TestReplaceVariablesLogicalFunctionCall()
        {
            var input = "function func(i) => i == 1";
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("function");
            var functionDefinition = (FunctionDefinitionExpression)FunctionDefinitionExpression.Parse(tokenizer);

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(2) });
            var value1 = new IntegerConstantExpression(98);
            var expr = new DictionaryExpression();
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = functionCall, Value = value1 });

            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("Dictionary key must evaluate to a constant"));
        }

        [Test]
        public void TestReplaceVariablesMethodCall()
        {
            var input = "function func(i) { j = i }";
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("function");
            var functionDefinition = (FunctionDefinitionExpression)FunctionDefinitionExpression.Parse(tokenizer);

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(2) });
            var value1 = new IntegerConstantExpression(98);
            var expr = new DictionaryExpression();
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = functionCall, Value = value1 });

            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            var parseError = (ParseErrorExpression)result;
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
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = value1, Value = value3 });
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = value1, Value = value4 });

            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("1 already exists in dictionary"));
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
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = variable1, Value = value3 });
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = variable2, Value = value4 });

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value1);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("1 already exists in dictionary"));
        }

        [Test]
        public void TestReplaceVariablesMemoryAccessor()
        {
            var key = new IntegerConstantExpression(6);
            var value = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(1) });
            var expr = new DictionaryExpression();
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = key, Value = value });

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);

            Assert.That(result, Is.InstanceOf<DictionaryExpression>());
            var arrayResult = (DictionaryExpression)result;
            Assert.That(arrayResult.Entries.Count, Is.EqualTo(1));
            Assert.That(arrayResult.Entries[0].Value.ToString(), Is.EqualTo(value.ToString()));
        }
    }
}
