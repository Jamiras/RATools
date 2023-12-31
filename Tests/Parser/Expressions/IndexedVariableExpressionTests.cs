using NUnit.Framework;
using RATools.Parser;
using RATools.Parser.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Tests.Parser.Expressions
{
    [TestFixture]
    class IndexedVariableExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new IndexedVariableExpression(new VariableExpression("test"), new IntegerConstantExpression(1));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("test[1]"));
        }

        [Test]
        public void TestAppendStringTwoDimensional()
        {
            var second = new IndexedVariableExpression(new VariableExpression("test"), new IntegerConstantExpression(2));
            var expr = new IndexedVariableExpression(second, new IntegerConstantExpression(1));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("test[2][1]"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable = new VariableExpression("variable");
            var key = new StringConstantExpression("key");
            var value = new IntegerConstantExpression(99);
            var dict = new DictionaryExpression();
            dict.Add(key, value);
            var expr = new IndexedVariableExpression(variable, key);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, dict);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(99));
        }

        [Test]
        public void TestReplaceTwoDimensional()
        {
            var variable = new VariableExpression("variable");
            var key = new StringConstantExpression("key");
            var value = new IntegerConstantExpression(99);
            var dict2 = new DictionaryExpression();
            dict2.Add(key, value);
            var dict1 = new DictionaryExpression();
            dict1.Add(key, dict2);
            var expr2 = new IndexedVariableExpression(variable, key);
            var expr = new IndexedVariableExpression(expr2, key);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, dict1);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(99));
        }

        [Test]
        public void TestReplaceVariablesIndexVariable()
        {
            var variable = new VariableExpression("variable");
            var key = new StringConstantExpression("key");
            var index = new VariableExpression("index");
            var value = new IntegerConstantExpression(99);
            var dict = new DictionaryExpression();
            dict.Add(key, value);
            var expr = new IndexedVariableExpression(variable, index);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, dict);
            scope.AssignVariable(index, key);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(99));
        }

        [Test]
        public void TestReplaceVariablesInvalidKey()
        {
            var variable = new VariableExpression("variable");
            var key = new StringConstantExpression("key");
            var dict = new DictionaryExpression();
            var expr = new IndexedVariableExpression(variable, key);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, dict);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("No entry in dictionary for key: \"key\""));
        }

        [Test]
        public void TestReplaceVariablesNonDictionary()
        {
            var variable = new VariableExpression("variable");
            var key = new StringConstantExpression("key");
            var value = new IntegerConstantExpression(99);
            var expr = new IndexedVariableExpression(variable, key);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, value);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Cannot index: variable (IntegerConstant)"));
        }

        [Test]
        public void TestReplaceVariablesIndexMathematical()
        {
            var variable = new VariableExpression("variable");
            var key = new IntegerConstantExpression(6);
            var index = new MathematicExpression(new IntegerConstantExpression(2), MathematicOperation.Add, new IntegerConstantExpression(4));
            var value = new IntegerConstantExpression(99);
            var dict = new DictionaryExpression();
            dict.Add(key, value);
            var expr = new IndexedVariableExpression(variable, index);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, dict);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(99));
        }

        [Test]
        public void TestReplaceVariablesIndexFunctionCall()
        {
            var functionDefinition = UserFunctionDefinitionExpression.ParseForTest("function func(i) => 6");

            var functionCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(2) });
            var value = new IntegerConstantExpression(98);

            var variable = new VariableExpression("variable");
            var dict = new DictionaryExpression();
            dict.Add(new IntegerConstantExpression(6), value);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, dict);
            scope.AddFunction(functionDefinition);

            var expr = new IndexedVariableExpression(variable, functionCall);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(98));
        }

        [Test]
        public void TestReplaceVariablesArray()
        {
            var variable = new VariableExpression("variable");
            var index = new IntegerConstantExpression(0);
            var indexVariable = new VariableExpression("index");
            var value = new IntegerConstantExpression(99);
            var array = new ArrayExpression();
            array.Entries.Add(value);
            var expr = new IndexedVariableExpression(variable, indexVariable);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, array);
            scope.AssignVariable(indexVariable, index);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(99));
        }

        [Test]
        public void TestReplaceVariablesArrayIndexOutOfRange()
        {
            var variable = new VariableExpression("variable");
            var index = new IntegerConstantExpression(1);
            var value = new IntegerConstantExpression(99);
            var array = new ArrayExpression();
            array.Entries.Add(value);
            var expr = new IndexedVariableExpression(variable, index);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, array);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Index 1 not in range 0-0"));
        }

        [Test]
        public void TestReplaceVariablesArrayIndexNegative()
        {
            var variable = new VariableExpression("variable");
            var index = new IntegerConstantExpression(-1);
            var value = new IntegerConstantExpression(99);
            var array = new ArrayExpression();
            array.Entries.Add(value);
            var expr = new IndexedVariableExpression(variable, index);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, array);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Index -1 not in range 0-0"));
        }

        [Test]
        public void TestReplaceVariablesArrayIndexString()
        {
            var variable = new VariableExpression("variable");
            var index = new StringConstantExpression("str");
            var value = new IntegerConstantExpression(99);
            var array = new ArrayExpression();
            array.Entries.Add(value);
            var expr = new IndexedVariableExpression(variable, index);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, array);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Index does not evaluate to an integer constant"));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var variable = new VariableExpression("variable1");
            var index = new VariableExpression("variable2");
            var expr = new IndexedVariableExpression(variable, index);

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(2));
            Assert.That(nested.Contains(variable));
            Assert.That(nested.Contains(index));
        }

        [Test]
        public void TestGetDependencies()
        {
            var variable = new VariableExpression("variable1");
            var index = new VariableExpression("variable2");
            var expr = new IndexedVariableExpression(variable, index);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("variable1"));
            Assert.That(dependencies.Contains("variable2"));
        }

        [Test]
        public void TestGetModifications()
        {
            var variable = new VariableExpression("variable1");
            var index = new VariableExpression("variable2");
            var expr = new IndexedVariableExpression(variable, index);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(0));
        }
    }
}
