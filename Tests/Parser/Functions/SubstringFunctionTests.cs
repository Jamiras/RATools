using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Tests.Parser.Functions
{
    [TestFixture]
    class SubstringFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new SubstringFunction();
            Assert.That(def.Name.Name, Is.EqualTo("substring"));
            Assert.That(def.Parameters.Count, Is.EqualTo(3));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("string"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("offset"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("length"));

            Assert.That(def.DefaultParameters.Count, Is.EqualTo(1));
            Assert.That(def.DefaultParameters.ElementAt(0).Key, Is.EqualTo("length"));
            Assert.That(def.DefaultParameters.ElementAt(0).Value, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)def.DefaultParameters.ElementAt(0).Value).Value, Is.EqualTo(int.MaxValue));
        }

        [Test]
        [TestCase("abcdef", 0, 6, "abcdef")]
        [TestCase("abcdef", 0, 5, "abcde")]
        [TestCase("abcdef", 0, -1, "abcde")]
        [TestCase("abcdef", 0, -2, "abcd")]
        [TestCase("abcdef", 1, 5, "bcdef")]
        [TestCase("abcdef", 1, -1, "bcde")]
        [TestCase("abcdef", 1, -2, "bcd")]
        [TestCase("abcdef", 3, 1, "d")]
        [TestCase("abcdef", 3, 0, "")]
        [TestCase("abcdef", 3, -1, "de")] // from fourth character ignoring last character
        [TestCase("abcdef", 3, -2, "d")]
        [TestCase("abcdef", 3, -3, "")]
        [TestCase("abcdef", 3, -4, "")]
        [TestCase("abcdef", -3, 1, "d")] // one character starting three from the end
        [TestCase("abcdef", -3, -2, "d")] // from three from the end ignoring the last two
        [TestCase("abcdef", -8, 4, "ab")] // four characters starting at -2
        [TestCase("abcdef", -8, 1, "")] // one character starting at -2
        [TestCase("abcdef", 0, int.MaxValue, "abcdef")] // simulate default parameter
        [TestCase("abcdef", 3, int.MaxValue, "def")] // simulate default parameter
        public void TestEvaluate(string input, int offset, int length, string expected)
        {
            var parameters = new List<ExpressionBase>();
            parameters.Add(new StringConstantExpression(input));
            parameters.Add(new IntegerConstantExpression(offset));
            parameters.Add(new IntegerConstantExpression(length));

            var expression = new FunctionCallExpression("substring", parameters);
            var scope = new InterpreterScope();
            scope.AddFunction(new SubstringFunction());

            ExpressionBase result;
            Assert.IsTrue(expression.Evaluate(scope, out result));

            Assert.IsTrue(result is StringConstantExpression);
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo(expected));
        }
    }
}
