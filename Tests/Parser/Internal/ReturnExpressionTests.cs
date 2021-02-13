using NUnit.Framework;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class ReturnExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new ReturnExpression(new VariableExpression("variable"));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("return variable"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new ReturnExpression(variable);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable, value);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<ReturnExpression>());
            Assert.That(((ReturnExpression)result).Value, Is.EqualTo(value));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var variable = new VariableExpression("variable");
            var expr = new ReturnExpression(variable);

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(1));
            Assert.That(nested.Contains(variable));
        }

        [Test]
        public void TestGetDependencies()
        {
            var variable = new VariableExpression("variable");
            var expr = new ReturnExpression(variable);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(1));
            Assert.That(dependencies.Contains("variable"));
        }

        [Test]
        public void TestGetModifications()
        {
            var variable = new VariableExpression("variable");
            var expr = new ReturnExpression(variable);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(0));
        }
    }
}
