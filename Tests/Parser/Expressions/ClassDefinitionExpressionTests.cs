using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Expressions
{
    [TestFixture]
    class ClassDefinitionExpressionTests
    {
        private static ClassDefinitionExpression Parse(string input, string expectedError = null)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("class");
            var expr = ClassDefinitionExpression.Parse(tokenizer);

            if (expectedError != null)
            {
                Assert.That(expr, Is.InstanceOf<ErrorExpression>());

                var error = (ErrorExpression)expr;
                var formattedErrorMessage = string.Format("{0}:{1} {2}", error.Location.Start.Line, error.Location.Start.Column, error.Message);
                Assert.That(formattedErrorMessage, Is.EqualTo(expectedError));
                return null;
            }
            else
            {
                Assert.That(expr, Is.InstanceOf<ClassDefinitionExpression>());
                return (ClassDefinitionExpression)expr;
            }
        }

        [Test]
        public void TestParseEmpty()
        {
            var expr = Parse("class Empty {}");
            Assert.That(expr.Name.Name, Is.EqualTo("Empty"));
            Assert.That(expr.ToString(), Is.EqualTo("class Empty(0 fields, 0 functions)"));
        }

        [Test]
        public void TestParseFields()
        {
            var expr = Parse(
                "class Point {\n" +
                "  x = 0\n" +
                "  y = 0\n" +
                "}");
            Assert.That(expr.Name.Name, Is.EqualTo("Point"));
            Assert.That(expr.ToString(), Is.EqualTo("class Point(2 fields, 0 functions)"));
        }

        [Test]
        public void TestParseShorthandFunction()
        {
            var expr = Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(addr + 8)\n" +
                "}");
            Assert.That(expr.Name.Name, Is.EqualTo("Entity"));
            Assert.That(expr.ToString(), Is.EqualTo("class Entity(1 field, 1 function)"));
        }

        [Test]
        public void TestParseFunction()
        {
            var expr = Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp()\n" +
                "  {\n" +
                "     return word(addr + 8)\n" +
                "  }\n" +
                "}");
            Assert.That(expr.Name.Name, Is.EqualTo("Entity"));
            Assert.That(expr.ToString(), Is.EqualTo("class Entity(1 field, 1 function)"));
        }

        [Test]
        public void TestParseErrorInsideField()
        {
            Parse(
                "class Entity {\n" +
                "  addr = + +\n" +
                "  function hp() => word(addr + 8)\n" +
                "}",

                "2:12 Incompatible mathematical operation");
        }

        [Test]
        public void TestParseErrorInsideFunction()
        {
            Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(addr + +)\n" +
                "}",

                "3:33 Unexpected character: )");
        }

        [Test]
        public void TestParseErrorLogicAtClassLevel()
        {
            Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  if (addr == 8) { }\n" +
                "}",

                "3:3 Only variable and function definitions allowed inside a class definition");
        }


        [Test]
        public void TestParseErrorIndexedField()
        {
            Parse(
                "class Entity {\n" +
                "  addr[1] = 0\n" +
                "  function hp() => word(addr + 8)\n" +
                "}",

                "2:3 Complex field name not allowed");
        }


        [Test]
        public void TestNestedExpressions()
        {
            var expr = Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(addr + 8)\n" +
                "}");

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(4));
            Assert.That(nested.ElementAt(0), Is.InstanceOf<KeywordExpression>());            // class
            Assert.That(nested.ElementAt(1), Is.InstanceOf<VariableDefinitionExpression>()); // Entity
            Assert.That(nested.ElementAt(2), Is.InstanceOf<AssignmentExpression>());         // addr = 0
            Assert.That(nested.ElementAt(3), Is.InstanceOf<FunctionDefinitionExpression>()); // function hp() => ...
        }
/* TODO
        [Test]
        public void TestGetDependencies()
        {
            var expr = Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(addr + 8)\n" +
                "}");

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("func2"));
            Assert.That(dependencies.Contains("j"));
            Assert.That(dependencies.Contains("i"), Is.False); // parameter is self-contained
        }

        [Test]
        public void TestGetModifications()
        {
            var expr = Parse("function func(i) => func2(i) + j");

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("func"));
        }
*/
    }
}
