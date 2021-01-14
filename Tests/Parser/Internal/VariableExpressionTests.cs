using NUnit.Framework;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class VariableExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new VariableExpression("test");

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("test"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, value);

            ExpressionBase result;
            Assert.That(variable.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(99));
        }

        [Test]
        public void TestReplaceVariablesFunctionName()
        {
            var variable = new VariableExpression("func");

            var scope = new InterpreterScope();
            scope.AddFunction(new FunctionDefinitionExpression("func"));

            ExpressionBase result;
            Assert.That(variable.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("Function used like a variable: func"));
        }

        [Test]
        public void TestReplaceVariablesUnknown()
        {
            var variable = new VariableExpression("unknown");
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(variable.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("Unknown variable: unknown"));
        }

        [Test]
        public void TestReplaceVariablesNested()
        {
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var value = new IntegerConstantExpression(99);
            var scope = new InterpreterScope();
            scope.AssignVariable(variable1, variable2);
            scope.AssignVariable(variable2, value);

            ExpressionBase result;
            Assert.That(variable1.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(99));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var expr = new VariableExpression("variable1");

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(0));
        }

        [Test]
        public void TestGetDependencies()
        {
            var expr = new VariableExpression("variable1");

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(1));
            Assert.That(dependencies.Contains("variable1"));
        }

        [Test]
        public void TestGetModifications()
        {
            var expr = new VariableExpression("variable1");

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestDefinitionNestedExpressions()
        {
            var expr = new VariableDefinitionExpression("variable1");

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(0));
        }

        [Test]
        public void TestDefinitionGetDependencies()
        {
            var expr = new VariableDefinitionExpression("variable1");

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestDefinitionGetModifications()
        {
            var expr = new VariableDefinitionExpression("variable1");

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("variable1"));
        }
    }
}
