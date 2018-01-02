using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class DictionaryExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new DictionaryExpression();
            expr.Entries.Add(new DictionaryExpression.DictionaryEntry {
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
            Assert.That(dictResult.Entries[0].Key, Is.EqualTo(value1));
            Assert.That(dictResult.Entries[0].Value, Is.EqualTo(value3));
            Assert.That(dictResult.Entries[1].Key, Is.EqualTo(value4));
            Assert.That(dictResult.Entries[1].Value, Is.EqualTo(value2));
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
        public void TestReplaceVariablesNestedFunctionCall()
        {
            var input = "function func(i) => func2(i)";
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
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("func did not return a value"));
        }
    }
}
