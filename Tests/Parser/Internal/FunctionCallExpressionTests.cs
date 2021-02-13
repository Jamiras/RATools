using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;
using System.Linq;
using Jamiras.Components;
using System.Collections.Generic;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class FunctionCallExpressionTests
    {
        [Test]
        public void TestAppendStringEmpty()
        {
            var expr = new FunctionCallExpression("func", new ExpressionBase[0]);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("func()"));
        }

        [Test]
        public void TestAppendString()
        {
            var expr = new FunctionCallExpression("func", new ExpressionBase[] {
                new VariableExpression("a"),
                new IntegerConstantExpression(1),
                new StringConstantExpression("b"),
                new FunctionCallExpression("func2", new ExpressionBase[0])
            });

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("func(a, 1, \"b\", func2())"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var functionDefinition = Parse("function func(i,j,k) => i*j+k");
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(98);
            var value2 = new IntegerConstantExpression(99);
            var value3 = new IntegerConstantExpression(3);
            var expr = new FunctionCallExpression("func", new ExpressionBase[] {
                variable1, value3, variable2
            });

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value2);
            scope.AddFunction(functionDefinition);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(98 * 3 + 99));
        }

        [Test]
        public void TestGetParametersNone()
        {
            var functionDefinition = new FunctionDefinitionExpression("f");
            var scope = new InterpreterScope();
            var functionCall = new FunctionCallExpression("f", new ExpressionBase[0]);

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Not.Null);
            Assert.That(error, Is.Null);
            Assert.That(innerScope.VariableCount, Is.EqualTo(0));
        }

        private FunctionDefinitionExpression Parse(string input)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("function");
            var expr = FunctionDefinitionExpression.Parse(tokenizer);
            Assert.That(expr, Is.InstanceOf<FunctionDefinitionExpression>());
            return (FunctionDefinitionExpression)expr;
        }

        [Test]
        public void TestGetParametersByIndex()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value1 = new IntegerConstantExpression(6);
            var value2 = new StringConstantExpression("a");
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value1, value2 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Not.Null);
            Assert.That(error, Is.Null);
            Assert.That(innerScope.VariableCount, Is.EqualTo(2));
            Assert.That(innerScope.GetVariable("i"), Is.EqualTo(value1));
            Assert.That(innerScope.GetVariable("j"), Is.EqualTo(value2));
        }

        [Test]
        public void TestGetParametersByIndexMissing()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value1 = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value1 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Null);
            Assert.That(error, Is.Not.Null);
            var parseError = (ParseErrorExpression)error;
            Assert.That(parseError.Message, Is.EqualTo("Required parameter 'j' not provided"));
        }

        [Test]
        public void TestGetParametersByIndexTooMany()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value1 = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value1, value1, value1 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Null);
            Assert.That(error, Is.Not.Null);
            var parseError = (ParseErrorExpression)error;
            Assert.That(parseError.Message, Is.EqualTo("Too many parameters passed to function"));
        }

        [Test]
        public void TestGetParametersByIndexUnknownVariable()
        {
            var functionDefinition = Parse("function func(i) { }");
            var scope = new InterpreterScope();
            var value1 = new VariableExpression("var");
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value1 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Not.Null);
            Assert.That(error, Is.Not.Null);
            var parseError = (ParseErrorExpression)error;
            Assert.That(parseError.Message, Is.EqualTo("Invalid value for parameter: i"));
            Assert.That(parseError.InnermostError.Message, Is.EqualTo("Unknown variable: var"));
        }

        [Test]
        public void TestGetParametersByIndexUnknownVariableInExpression()
        {
            var functionDefinition = Parse("function func(i) { }");
            var scope = new InterpreterScope();
            var value1 = new VariableExpression("var");
            var expr1 = new ComparisonExpression(value1, ComparisonOperation.Equal, new IntegerConstantExpression(1));
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { expr1 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Not.Null);
            Assert.That(error, Is.Not.Null);
            var parseError = (ParseErrorExpression)error;
            Assert.That(parseError.Message, Is.EqualTo("Invalid value for parameter: i"));
            Assert.That(parseError.InnermostError.Message, Is.EqualTo("Unknown variable: var"));
        }

        [Test]
        public void TestGetParametersByName()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value1 = new IntegerConstantExpression(6);
            var value2 = new StringConstantExpression("a");
            var assign1 = new AssignmentExpression(new VariableExpression("i"), value1);
            var assign2 = new AssignmentExpression(new VariableExpression("j"), value2);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { assign1, assign2 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Not.Null);
            Assert.That(error, Is.Null);
            Assert.That(innerScope.VariableCount, Is.EqualTo(2));
            Assert.That(innerScope.GetVariable("i"), Is.EqualTo(value1));
            Assert.That(innerScope.GetVariable("j"), Is.EqualTo(value2));
        }

        [Test]
        public void TestGetParametersByNameMissing()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value2 = new StringConstantExpression("a");
            var assign2 = new AssignmentExpression(new VariableExpression("j"), value2);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { assign2 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Null);
            Assert.That(error, Is.Not.Null);
            var parseError = (ParseErrorExpression)error;
            Assert.That(parseError.Message, Is.EqualTo("Required parameter 'i' not provided"));
        }

        [Test]
        public void TestGetParametersByNameUnknown()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value2 = new StringConstantExpression("a");
            var assign2 = new AssignmentExpression(new VariableExpression("k"), value2);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { assign2 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Null);
            Assert.That(error, Is.Not.Null);
            var parseError = (ParseErrorExpression)error;
            Assert.That(parseError.Message, Is.EqualTo("'func' does not have a 'k' parameter"));
        }

        [Test]
        public void TestGetParametersByNameRepeated()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value2 = new StringConstantExpression("a");
            var assign2 = new AssignmentExpression(new VariableExpression("i"), value2);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { assign2, assign2 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Null);
            Assert.That(error, Is.Not.Null);
            var parseError = (ParseErrorExpression)error;
            Assert.That(parseError.Message, Is.EqualTo("'i' already has a value"));
        }

        [Test]
        public void TestGetParametersByIndexBeforeName()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value1 = new IntegerConstantExpression(6);
            var value2 = new StringConstantExpression("a");
            var assign2 = new AssignmentExpression(new VariableExpression("j"), value2);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value1, assign2 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Not.Null);
            Assert.That(error, Is.Null);
            Assert.That(innerScope.VariableCount, Is.EqualTo(2));
            Assert.That(innerScope.GetVariable("i"), Is.EqualTo(value1));
            Assert.That(innerScope.GetVariable("j"), Is.EqualTo(value2));
        }

        [Test]
        public void TestGetParametersByNameBeforeIndex()
        {
            var functionDefinition = Parse("function func(i,j) { }");
            var scope = new InterpreterScope();
            var value1 = new IntegerConstantExpression(6);
            var value2 = new StringConstantExpression("a");
            var assign1 = new AssignmentExpression(new VariableExpression("i"), value1);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { assign1, value2 });

            ExpressionBase error;
            var innerScope = functionCall.GetParameters(functionDefinition, scope, out error);
            Assert.That(innerScope, Is.Null);
            Assert.That(error, Is.Not.Null);
            var parseError = (ParseErrorExpression)error;
            Assert.That(parseError.Message, Is.EqualTo("Non-named parameter following named parameter"));
        }

        [Test]
        public void TestReplaceVariablesConstant()
        {
            var functionDefinition = Parse("function func(i) { return 2 }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(new IntegerConstantExpression(2)));
        }

        [Test]
        public void TestReplaceVariablesVariable()
        {
            var functionDefinition = Parse("function func(i) { return i }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void TestReplaceVariablesUnknownVariable()
        {
            var functionDefinition = Parse("function func(i) { return var }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            var parseError = (ParseErrorExpression)result;
            Assert.That(parseError.InnermostError.Message, Is.EqualTo("Unknown variable: var"));
        }

        [Test]
        public void TestReplaceVariablesUnknownVariableInExpression()
        {
            var functionDefinition = Parse("function func(i) { return var == 3 }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            var parseError = (ParseErrorExpression)result;
            Assert.That(parseError.InnermostError.Message, Is.EqualTo("Unknown variable: var"));
        }
        [Test]
        public void TestReplaceVariablesUnknownParameter()
        {
            var functionDefinition = Parse("function func(i) { return i }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new VariableExpression("var");
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            var parseError = (ParseErrorExpression)result;
            Assert.That(parseError.InnermostError.Message, Is.EqualTo("Unknown variable: var"));
        }

        [Test]
        public void TestReplaceVariablesUnknownParameterInExpression()
        {
            var functionDefinition = Parse("function func(i) { return i }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new VariableExpression("var");
            var expr = new ComparisonExpression(value, ComparisonOperation.Equal, new IntegerConstantExpression(6));
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { expr });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            var parseError = (ParseErrorExpression)result;
            Assert.That(parseError.InnermostError.Message, Is.EqualTo("Unknown variable: var"));
        }

        [Test]
        public void TestReplaceVariablesMathematical()
        {
            var functionDefinition = Parse("function func(i) { return i * 2 }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(new IntegerConstantExpression(12)));
        }

        [Test]
        public void TestReplaceVariablesConditional()
        {
            var functionDefinition = Parse("function func(i) { if (i < 3) return 4 else return 8 }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(new IntegerConstantExpression(8)));

            value = new IntegerConstantExpression(2);
            functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(new IntegerConstantExpression(4)));
        }

        [Test]
        public void TestReplaceVariablesMethod()
        {
            var functionDefinition = Parse("function func(i) { j = i }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            var parseError = (ParseErrorExpression)result;
            while (parseError.InnerError != null)
                parseError = parseError.InnerError;
            Assert.That(parseError.Message, Is.EqualTo("func did not return a value"));
        }

        [Test]
        public void TestReplaceVariablesUnknownFunction()
        {
            var scope = new InterpreterScope();
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("Unknown function: func"));
        }

        [Test]
        public void TestEvaluateConstant()
        {
            var functionDefinition = Parse("function func(i) { return 2 }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(new IntegerConstantExpression(2)));
        }

        [Test]
        public void TestEvaluateVariable()
        {
            var functionDefinition = Parse("function func(i) { return i }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void TestEvaluateMathematical()
        {
            var functionDefinition = Parse("function func(i) { return i * 2 }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(new IntegerConstantExpression(12)));
        }

        [Test]
        public void TestEvaluateConditional()
        {
            var functionDefinition = Parse("function func(i) { if (i < 3) return 4 else return 8 }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(new IntegerConstantExpression(8)));

            value = new IntegerConstantExpression(2);
            functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.EqualTo(new IntegerConstantExpression(4)));
        }

        [Test]
        public void TestEvaluateMethod()
        {
            var functionDefinition = Parse("function func(i) { j = i }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void TestEvaluateUnknownFunction()
        {
            var scope = new InterpreterScope();
            var value = new IntegerConstantExpression(6);
            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { value });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("Unknown function: func"));
        }

        [Test]
        public void TestEvaluateDictionaryByReference()
        {
            // ensures the dictionary is passed by reference to func(), so it can be modified
            // within func(). it's also much more efficient to pass the dictionary by reference
            // instead of evaluating it (which creates a copy).
            var functionDefinition = Parse("function func(d) { d[\"key\"] = 2 }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);

            var dict = new DictionaryExpression();
            dict.Entries.Add(new DictionaryExpression.DictionaryEntry()
            {
                Key = new StringConstantExpression("key"),
                Value = new IntegerConstantExpression(1)
            });
            scope.AssignVariable(new VariableExpression("dict"), dict);

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { new VariableExpression("dict") });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.Null);

            Assert.That(dict.Entries.Count(), Is.EqualTo(1));
            Assert.That(dict.Entries[0].Value, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)dict.Entries[0].Value).Value, Is.EqualTo(2));
        }

        [Test]
        public void TestEvaluateDictionaryDirect()
        {
            // ensures that a dictionary being passed directly to a function is evaluated
            var functionDefinition = Parse("function func(d) { return d[\"key\"] }");
            var scope = new InterpreterScope();
            scope.AddFunction(functionDefinition);

            var dict = new DictionaryExpression();
            dict.Entries.Add(new DictionaryExpression.DictionaryEntry()
            {
                Key = new StringConstantExpression("key"),
                Value = new VariableExpression("variable")
            });
            scope.AssignVariable(new VariableExpression("variable"), new IntegerConstantExpression(123));

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { dict });

            ExpressionBase result;
            Assert.That(functionCall.Evaluate(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(123));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var parameter = new VariableExpression("variable1");
            var name = new FunctionNameExpression("func");
            var expr = new FunctionCallExpression(name, new ExpressionBase[] { parameter });

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(2));
            Assert.That(nested.Contains(name));
            Assert.That(nested.Contains(parameter));
        }

        [Test]
        public void TestGetDependencies()
        {
            var parameter = new VariableExpression("variable1");
            var name = new FunctionNameExpression("func");
            var expr = new FunctionCallExpression(name, new ExpressionBase[] { parameter });

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("func"));
            Assert.That(dependencies.Contains("variable1"));
        }

        [Test]
        public void TestGetModifications()
        {
            var parameter = new VariableExpression("variable1");
            var name = new FunctionNameExpression("func");
            var expr = new FunctionCallExpression(name, new ExpressionBase[] { parameter });

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(0));
        }
    }
}
