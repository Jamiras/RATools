using NUnit.Framework;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class LeftRightExpressionBaseTests
    {
        private class LeftRightExpression : LeftRightExpressionBase
        {
            public LeftRightExpression(ExpressionBase left, ExpressionBase right)
                : base(left, right, ExpressionType.None)
            {
            }

            public ExpressionBase DoRebalance()
            {
                return base.Rebalance((LeftRightExpressionBase)Right);
            }

            protected override bool Equals(ExpressionBase obj)
            {
                return true;
            }

            internal override void AppendString(StringBuilder builder)
            {
            }
        }

        private class TestExpression : IntegerConstantExpression
        {
            public TestExpression(int value, int column) : base(value)
            {
                Location = new Jamiras.Components.TextRange(column, column, column + 1, column + 1);
            }
        }

        [Test]
        public void TestColumns()
        {
            var left = new TestExpression(1, 1);
            var right = new TestExpression(2, 3);
            var node = new LeftRightExpression(left, right);
            Assert.That(node.Location.Start.Line, Is.EqualTo(left.Location.Start.Line));
            Assert.That(node.Location.Start.Column, Is.EqualTo(left.Location.Start.Column));
            Assert.That(node.Location.End.Line, Is.EqualTo(right.Location.End.Line));
            Assert.That(node.Location.End.Column, Is.EqualTo(right.Location.End.Column));
        }

        [Test]
        public void TestColumnsNoLeft()
        {
            var right = new TestExpression(2, 3);
            var node = new LeftRightExpression(null, right);
            Assert.That(node.Location.Start.Line, Is.EqualTo(right.Location.Start.Line));
            Assert.That(node.Location.Start.Column, Is.EqualTo(right.Location.Start.Column));
            Assert.That(node.Location.End.Line, Is.EqualTo(right.Location.End.Line));
            Assert.That(node.Location.End.Column, Is.EqualTo(right.Location.End.Column));
        }

        [Test]
        public void TestRebalance()
        {
            var one = new TestExpression(1, 1);
            var two = new TestExpression(2, 3);
            var three = new TestExpression(3, 5);

            var left = one;
            var right = new LeftRightExpression(two, three);
            var node = new LeftRightExpression(left, right);

            var newNode = node.DoRebalance() as LeftRightExpression;

            Assert.That(newNode, Is.SameAs(right));
            Assert.That(newNode.Location.Start.Line, Is.EqualTo(one.Location.Start.Line));
            Assert.That(newNode.Location.Start.Column, Is.EqualTo(one.Location.Start.Column));
            Assert.That(newNode.Location.End.Line, Is.EqualTo(three.Location.End.Line));
            Assert.That(newNode.Location.End.Column, Is.EqualTo(three.Location.End.Column));

            Assert.That(newNode.Right, Is.SameAs(three));

            var newLeft = newNode.Left as LeftRightExpression;
            Assert.That(newLeft, Is.Not.Null);
            Assert.That(newLeft.Left, Is.SameAs(one));
            Assert.That(newLeft.Right, Is.SameAs(two));

            Assert.That(newLeft.Location.Start.Line, Is.EqualTo(one.Location.Start.Line));
            Assert.That(newLeft.Location.Start.Column, Is.EqualTo(one.Location.Start.Column));
            Assert.That(newLeft.Location.End.Line, Is.EqualTo(two.Location.End.Line));
            Assert.That(newLeft.Location.End.Column, Is.EqualTo(two.Location.End.Column));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var left = new VariableExpression("variable1");
            var right = new VariableExpression("variable2");
            var expr = new LeftRightExpression(left, right);

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(2));
            Assert.That(nested.Contains(left));
            Assert.That(nested.Contains(right));
        }

        [Test]
        public void TestGetDependencies()
        {
            var left = new VariableExpression("variable1");
            var right = new VariableExpression("variable2");
            var expr = new LeftRightExpression(left, right);

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("variable1"));
            Assert.That(dependencies.Contains("variable2"));
        }

        [Test]
        public void TestGetModifications()
        {
            var left = new VariableExpression("variable1");
            var right = new VariableExpression("variable2");
            var expr = new LeftRightExpression(left, right);

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(0));
        }
    }
}
