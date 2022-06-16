using NUnit.Framework;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Tests.Parser.Expressions
{
    [TestFixture]
    class ArrayExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new ArrayExpression();
            expr.Entries.Add(new IntegerConstantExpression(1));
            expr.Entries.Add(new StringConstantExpression("banana"));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("[1, \"banana\"]"));
        }

        [Test]
        public void TestAppendStringEmpty()
        {
            var expr = new ArrayExpression();

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("[]"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(98);
            var value2 = new IntegerConstantExpression(99);
            var value3 = new IntegerConstantExpression(1);
            var expr = new ArrayExpression();
            expr.Entries.Add(variable1);
            expr.Entries.Add(variable2);
            expr.Entries.Add(value3);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, value1);
            scope.AssignVariable(variable2, value2);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<ArrayExpression>());
            var arrayResult = (ArrayExpression)result;
            Assert.That(arrayResult.Entries.Count, Is.EqualTo(3));
            Assert.That(arrayResult.Entries[0], Is.EqualTo(value1));
            Assert.That(arrayResult.Entries[1], Is.EqualTo(value2));
            Assert.That(arrayResult.Entries[2], Is.EqualTo(value3));
        }

        [Test]
        public void TestReplaceVariablesMemoryAccessor()
        {
            var value = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(1) });
            var expr = new ArrayExpression();
            expr.Entries.Add(value);

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);

            Assert.That(result, Is.InstanceOf<ArrayExpression>());
            var arrayResult = (ArrayExpression)result;
            Assert.That(arrayResult.Entries.Count, Is.EqualTo(1));
            Assert.That(arrayResult.Entries[0].ToString(), Is.EqualTo(value.ToString()));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(98);
            var value2 = new IntegerConstantExpression(99);
            var value3 = new IntegerConstantExpression(1);
            var expr = new ArrayExpression();
            expr.Entries.Add(variable1);
            expr.Entries.Add(value1);
            expr.Entries.Add(variable2);
            expr.Entries.Add(value2);
            expr.Entries.Add(value3);

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(5));
            Assert.That(nested.Contains(variable1));
            Assert.That(nested.Contains(variable2));
            Assert.That(nested.Contains(value1));
            Assert.That(nested.Contains(value2));
            Assert.That(nested.Contains(value3));
        }

        [Test]
        public void TestGetDependencies()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(98);
            var value2 = new IntegerConstantExpression(99);
            var value3 = new IntegerConstantExpression(1);
            var expr = new ArrayExpression();
            expr.Entries.Add(variable1);
            expr.Entries.Add(value1);
            expr.Entries.Add(variable2);
            expr.Entries.Add(value2);
            expr.Entries.Add(value3);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("variable1"));
            Assert.That(dependencies.Contains("variable2"));
        }

        [Test]
        public void TestGetModifications()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value1 = new IntegerConstantExpression(98);
            var value2 = new IntegerConstantExpression(99);
            var value3 = new IntegerConstantExpression(1);
            var expr = new ArrayExpression();
            expr.Entries.Add(variable1);
            expr.Entries.Add(value1);
            expr.Entries.Add(variable2);
            expr.Entries.Add(value2);
            expr.Entries.Add(value3);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(0));
        }
    }
}
