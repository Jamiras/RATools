using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Tests.Parser.Expressions
{
    [TestFixture]
    class AssignmentExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new AssignmentExpression(variable, value);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("variable = 99"));
        }

        [Test]
        public void TestReplaceVariables()
        {
            var variable = new VariableExpression("variable");
            var variable2 = new VariableExpression("variable2");
            var value = new IntegerConstantExpression(99);
            var expr = new AssignmentExpression(variable, variable2);

            var scope = new InterpreterScope();
            scope.AssignVariable(variable2, value);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)result).Variable, Is.EqualTo(variable));
            Assert.That(((AssignmentExpression)result).Value, Is.EqualTo(value));
        }

        [Test]
        public void TestReplaceVariablesAppendCondition()
        {
            var variable = new VariableExpression("variable");
            var condition = new ComparisonExpression(new IntegerConstantExpression(1), ComparisonOperation.LessThan, new IntegerConstantExpression(2));
            var append = new ConditionalExpression(variable, ConditionalOperation.And, condition);
            var expr = new AssignmentExpression(variable, append);

            var scope = new InterpreterScope();

            // variable doesn't have a current value, logical comparison not supported
            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Unknown variable: variable"));
        }

        [Test]
        public void TestReplaceVariablesAppendMathematic()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(1);
            var append = new MathematicExpression(variable, MathematicOperation.Add, value);
            var expr = new AssignmentExpression(variable, append);

            var scope = new InterpreterScope();

            // variable doesn't have a current value, addition not supported
            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Unknown variable: variable"));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new AssignmentExpression(variable, value);

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(2));
            Assert.That(nested.Contains(variable));
            Assert.That(nested.Contains(value));
        }

        [Test]
        public void TestGetDependencies()
        {
            var variable = new VariableExpression("variable1");
            var value = new VariableExpression("variable2");
            var expr = new AssignmentExpression(variable, value);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(1));
            Assert.That(dependencies.Contains("variable1"), Is.False); // variable1 is assigned, not read from
            Assert.That(dependencies.Contains("variable2"));
        }

        [Test]
        public void TestGetDependenciesUpdateSelf()
        {
            var variable = new VariableExpression("variable1");
            var value = new IntegerConstantExpression(1);
            var mathematic = new MathematicExpression(variable, MathematicOperation.Add, value);
            var expr = new AssignmentExpression(variable, mathematic);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(1));
            Assert.That(dependencies.Contains("variable1")); // variable1 is read from before it's assigned
        }

        [Test]
        public void TestGetDependenciesIndexed()
        {
            var variable = new VariableExpression("variable1");
            var value = new VariableExpression("variable2");
            var indexed = new IndexedVariableExpression(variable, value);
            var expr = new AssignmentExpression(indexed, value);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(1));
            Assert.That(dependencies.Contains("variable1"), Is.False); // variable1 is updated
            Assert.That(dependencies.Contains("variable2")); // variable2 is needed to get the index
        }

        [Test]
        public void TestGetDependenciesMultiIndexed()
        {
            // variable1[variable2][variable3] = 1
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var variable3 = new VariableExpression("variable3");
            var indexed1 = new IndexedVariableExpression(variable1, variable2);
            var indexed2 = new IndexedVariableExpression(indexed1, variable3);
            var value = new IntegerConstantExpression(1);
            var expr = new AssignmentExpression(indexed2, value);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("variable1"), Is.False); // variable1 is updated
            Assert.That(dependencies.Contains("variable2")); // variable2 is needed to get the index
            Assert.That(dependencies.Contains("variable3")); // variable3 is needed to get the index
        }

        [Test]
        public void TestGetDependenciesNestedIndexed()
        {
            // variable1[variable2[variable3]] = 1
            var variable1 = new VariableExpression("variable1");
            var variable2 = new VariableExpression("variable2");
            var variable3 = new VariableExpression("variable3");
            var indexed1 = new IndexedVariableExpression(variable2, variable3);
            var indexed2 = new IndexedVariableExpression(variable1, indexed1);
            var value = new IntegerConstantExpression(1);
            var expr = new AssignmentExpression(indexed2, value);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("variable1"), Is.False); // variable1 is updated
            Assert.That(dependencies.Contains("variable2")); // variable2 is needed to get the index
            Assert.That(dependencies.Contains("variable3")); // variable3 is needed to get the index
        }

        [Test]
        public void TestGetModifications()
        {
            var variable = new VariableExpression("variable1");
            var value = new VariableExpression("variable2");
            var expr = new AssignmentExpression(variable, value);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("variable1"));
            Assert.That(modifications.Contains("variable2"), Is.False); // variable2 is read from, not modified
        }

        [Test]
        public void TestGetModificationsUpdateSelf()
        {
            var variable = new VariableExpression("variable1");
            var value = new IntegerConstantExpression(1);
            var mathematic = new MathematicExpression(variable, MathematicOperation.Add, value);
            var expr = new AssignmentExpression(variable, mathematic);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("variable1"));
        }

        [Test]
        public void TestGetModificationsIndexed()
        {
            var variable = new VariableExpression("variable1");
            var index = new VariableExpression("variable2");
            var indexed = new IndexedVariableExpression(variable, index);
            var value = new IntegerConstantExpression(1);
            var expr = new AssignmentExpression(indexed, value);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("variable1")); // variable1 is updated
            Assert.That(modifications.Contains("variable2"), Is.False); // index variable is not updated
        }

        [Test]
        public void TestGetModificationsMultiIndexed()
        {
            // variable1[variable2][variable3] = 1
            var variable = new VariableExpression("variable1");
            var index1 = new VariableExpression("variable2");
            var index2 = new VariableExpression("variable3");
            var indexed1 = new IndexedVariableExpression(variable, index1);
            var indexed2 = new IndexedVariableExpression(indexed1, index2);
            var value = new IntegerConstantExpression(1);
            var expr = new AssignmentExpression(indexed2, value);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("variable1")); // variable1 is updated
            Assert.That(modifications.Contains("variable2"), Is.False); // index variable is not updated
            Assert.That(modifications.Contains("variable3"), Is.False); // index variable is not updated
        }
    }
}
