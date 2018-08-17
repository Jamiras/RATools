using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
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

            // variable doesn't have a current value, allow && and || operators to build chain
            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)result).Variable, Is.EqualTo(variable));
            Assert.That(((AssignmentExpression)result).Value, Is.EqualTo(condition));

            scope.AssignVariable(variable, ((AssignmentExpression)result).Value);

            // now variable has the first part of the chain, append another
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)result).Variable, Is.EqualTo(variable));
            Assert.That(((AssignmentExpression)result).Value, Is.InstanceOf<ConditionalExpression>());
            var conditionalExpression = (ConditionalExpression)((AssignmentExpression)result).Value;
            Assert.That(conditionalExpression.Left, Is.EqualTo(condition));
            Assert.That(conditionalExpression.Operation, Is.EqualTo(ConditionalOperation.And));
            Assert.That(conditionalExpression.Right, Is.EqualTo(condition));
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
            Assert.That(result, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("Unknown variable: variable"));
        }
    }
}
