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
                Line = column;
                Column = column;
                EndLine = column + 1;
                EndColumn = column + 1;
            }
        }

        [Test]
        public void TestColumns()
        {
            var left = new TestExpression(1, 1);
            var right = new TestExpression(2, 3);
            var node = new LeftRightExpression(left, right);
            Assert.That(node.Line, Is.EqualTo(left.Line));
            Assert.That(node.Column, Is.EqualTo(left.Column));
            Assert.That(node.EndLine, Is.EqualTo(right.EndLine));
            Assert.That(node.EndColumn, Is.EqualTo(right.EndColumn));
        }

        [Test]
        public void TestColumnsNoLeft()
        {
            var right = new TestExpression(2, 3);
            var node = new LeftRightExpression(null, right);
            Assert.That(node.Line, Is.EqualTo(right.Line));
            Assert.That(node.Column, Is.EqualTo(right.Column));
            Assert.That(node.EndLine, Is.EqualTo(right.EndLine));
            Assert.That(node.EndColumn, Is.EqualTo(right.EndColumn));
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
            Assert.That(newNode.Line, Is.EqualTo(one.Line));
            Assert.That(newNode.Column, Is.EqualTo(one.Column));
            Assert.That(newNode.EndLine, Is.EqualTo(three.EndLine));
            Assert.That(newNode.EndColumn, Is.EqualTo(three.EndColumn));

            Assert.That(newNode.Right, Is.SameAs(three));

            var newLeft = newNode.Left as LeftRightExpression;
            Assert.That(newLeft, Is.Not.Null);
            Assert.That(newLeft.Left, Is.SameAs(one));
            Assert.That(newLeft.Right, Is.SameAs(two));

            Assert.That(newLeft.Line, Is.EqualTo(one.Line));
            Assert.That(newLeft.Column, Is.EqualTo(one.Column));
            Assert.That(newLeft.EndLine, Is.EqualTo(two.EndLine));
            Assert.That(newLeft.EndColumn, Is.EqualTo(two.EndColumn));
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
