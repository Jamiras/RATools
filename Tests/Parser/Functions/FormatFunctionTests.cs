using NUnit.Framework;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Test.Parser.Functions
{
    [TestFixture]
    class FormatFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new FormatFunction();
            Assert.That(def.Name.Name, Is.EqualTo("format"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("format_string"));
        }

        private class AddFunction : FunctionDefinitionExpression
        {
            public AddFunction() : base("add")
            {
                Parameters.Add(new VariableDefinitionExpression("left"));
                Parameters.Add(new VariableDefinitionExpression("right"));
            }

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                var left = GetIntegerParameter(scope, "left", out result);
                var right = GetIntegerParameter(scope, "right", out result);
                var mathematic = new MathematicExpression(left, MathematicOperation.Add, right);
                return mathematic.ReplaceVariables(scope, out result);
            }
        }

        private string Evaluate(string formatString, ExpressionBase[] parameters)
        {
            var newParameters = new List<ExpressionBase>();
            newParameters.Add(new StringConstantExpression(formatString));
            newParameters.AddRange(parameters);

            var expression = new FunctionCallExpression("format", newParameters);
            var scope = new InterpreterScope();
            scope.AddFunction(new FormatFunction());
            scope.AddFunction(new AddFunction());

            ExpressionBase result;
            Assert.IsTrue(expression.Evaluate(scope, out result));

            Assert.IsTrue(result is StringConstantExpression);
            return ((StringConstantExpression)result).Value;
        }

        [Test]
        public void TestNoParameters()
        {
            Assert.That(Evaluate("Test", new ExpressionBase[0]), Is.EqualTo("Test"));
        }

        [Test]
        public void TestIntegerParameters()
        {
            Assert.That(Evaluate("{0}{1}{2}", new ExpressionBase[] {
                new IntegerConstantExpression(1),
                new IntegerConstantExpression(23),
                new IntegerConstantExpression(45)
            }), Is.EqualTo("12345"));
        }

        [Test]
        public void TestStringParameters()
        {
            Assert.That(Evaluate("{0}{1}{2}", new ExpressionBase[] {
                new StringConstantExpression("Hello"),
                new StringConstantExpression(", "),
                new StringConstantExpression("World")
            }), Is.EqualTo("Hello, World"));
        }

        [Test]
        public void TestMixedParameters()
        {
            Assert.That(Evaluate("{0} had {1} points", new ExpressionBase[] {
                new StringConstantExpression("Bob"),
                new IntegerConstantExpression(1000)
            }), Is.EqualTo("Bob had 1000 points"));
        }

        [Test]
        public void TestOutOfOrder()
        {
            Assert.That(Evaluate("But {1} had {0} points", new ExpressionBase[] {
                new IntegerConstantExpression(500),
                new StringConstantExpression("Jane")
            }), Is.EqualTo("But Jane had 500 points"));
        }

        [Test]
        public void TestRepeated()
        {
            Assert.That(Evaluate("{0}-{0}-{0}-{0}-{1}", new ExpressionBase[] {
                new StringConstantExpression("Ba"),
                new StringConstantExpression("Barbara Ann")
            }), Is.EqualTo("Ba-Ba-Ba-Ba-Barbara Ann"));
        }

        [Test]
        public void TestMathematic()
        {
            Assert.That(Evaluate("{0} + {1} = {2}", new ExpressionBase[] {
                new IntegerConstantExpression(1),
                new IntegerConstantExpression(2),
                new MathematicExpression(new IntegerConstantExpression(1), MathematicOperation.Add, new IntegerConstantExpression(2))
            }), Is.EqualTo("1 + 2 = 3"));
        }

        [Test]
        public void TestFunction()
        {
            Assert.That(Evaluate("{0} + {1} = {2}", new ExpressionBase[] {
                new IntegerConstantExpression(1),
                new IntegerConstantExpression(2),
                new FunctionCallExpression("add", new ExpressionBase[]
                {
                    new IntegerConstantExpression(1),
                    new IntegerConstantExpression(2)
                })
            }), Is.EqualTo("1 + 2 = 3"));
        }

        private string EvaluateError(string formatString, ExpressionBase[] parameters)
        {
            var newParameters = new List<ExpressionBase>();
            newParameters.Add(new StringConstantExpression(formatString));
            newParameters.AddRange(parameters);

            var expression = new FunctionCallExpression("format", newParameters);
            var scope = new InterpreterScope();
            scope.AddFunction(new FormatFunction());
            scope.AddFunction(new AddFunction());

            ExpressionBase result;
            Assert.That(expression.Evaluate(scope, out result), Is.False);

            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            var parseError = (ErrorExpression)result;
            while (parseError.InnerError != null)
                parseError = parseError.InnerError;
            return parseError.Message;
        }

        [Test]
        public void TestEmptyParameterIndex()
        {
            Assert.That(EvaluateError("{} is empty", new ExpressionBase[] {
                new IntegerConstantExpression(1)
            }), Is.EqualTo("Empty parameter index"));
        }

        [Test]
        public void TestInvalidPositionalToken()
        {
            Assert.That(EvaluateError("{a} is empty", new ExpressionBase[] {
                new IntegerConstantExpression(1)
            }), Is.EqualTo("Invalid positional token"));

            Assert.That(EvaluateError("{-1} is empty", new ExpressionBase[] {
                new IntegerConstantExpression(1)
            }), Is.EqualTo("Invalid positional token"));
        }

        [Test]
        public void TestInvalidParameterIndex()
        {
            Assert.That(EvaluateError("{1} is empty", new ExpressionBase[] {
                new IntegerConstantExpression(1)
            }), Is.EqualTo("Invalid parameter index: 1"));
        }

        [Test]
        public void TestComparisonParameter()
        {
            Assert.That(EvaluateError("{0}", new ExpressionBase[] {
                new ComparisonExpression(new IntegerConstantExpression(1), ComparisonOperation.LessThan, new IntegerConstantExpression(2))
            }), Is.EqualTo("Cannot convert expression to string"));
        }
    }
}
