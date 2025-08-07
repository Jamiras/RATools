using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Tests.Expressions
{
    [TestFixture]
    class ClassDefinitionExpressionTests
    {
        private static ClassDefinitionExpression Parse(string input, string expectedError = null)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expr = ExpressionBase.Parse(tokenizer);

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
                "  function hp() => word(this.addr + 8)\n" +
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
                "     return word(this.addr + 8)\n" +
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
                "  function hp() => word(this.addr + 8)\n" +
                "}",

                "2:10 Unexpected character: +");
        }

        [Test]
        public void TestParseErrorInsideFunction()
        {
            Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(this.addr + +)\n" +
                "}",

                "3:38 Unexpected character: )");
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
                "  function hp() => word(this.addr + 8)\n" +
                "}",

                "2:3 Complex field name not allowed");
        }

        [Test]
        public void TestThisReservedField()
        {
            Parse(
                "class Entity {\n" +
                "  this = 3\n" +
                "  function hp() => word(8)\n" +
                "}",

                "2:3 this is a reserved word");
        }

        [Test]
        public void TestThisReservedImmutable()
        {
            Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp()\n" +
                "  {\n" +
                "     this = Entity(7)\n" +
                "     return word(this.addr + 8)\n" +
                "  }\n" +
                "}",

                "5:6 this is a reserved word");
        }

        [Test]
        public void TestThisReservedMissingMember()
        {
            string input =
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(this + 8)\n" +
                "}";
            var groups = new ExpressionGroup();
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expr = ExpressionBase.Parse(new ExpressionTokenizer(tokenizer, groups));

            // parse should be successful
            Assert.That(expr, Is.InstanceOf<ClassDefinitionExpression>());

            var error = groups.ParseErrors.FirstOrDefault();
            var formattedErrorMessage = string.Format("{0}:{1} {2}", error.Location.Start.Line, error.Location.Start.Column, error.Message);
            Assert.That(formattedErrorMessage, Is.EqualTo("3:25 this is a reserved word"));
        }


        [Test]
        public void TestNestedExpressions()
        {
            var expr = Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(this.addr + 8)\n" +
                "}");

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(4));
            Assert.That(nested.ElementAt(0), Is.InstanceOf<KeywordExpression>());            // class
            Assert.That(nested.ElementAt(1), Is.InstanceOf<VariableDefinitionExpression>()); // Entity
            Assert.That(nested.ElementAt(2), Is.InstanceOf<AssignmentExpression>());         // addr = 0
            Assert.That(nested.ElementAt(3), Is.InstanceOf<FunctionDefinitionExpression>()); // function hp() => ...
        }

        [Test]
        public void TestExecute()
        {
            var expr = Parse(
                "class Point {\n" +
                "  x = 0\n" +
                "  y = 1\n" +
                "}");

            var scope = new InterpreterScope();
            Assert.That(expr.Execute(scope), Is.Null);

            var functionDefinition = scope.GetFunction("Point");
            Assert.That(functionDefinition.Parameters.Count, Is.EqualTo(2));
            Assert.That(functionDefinition.Parameters.ElementAt(0).Name, Is.EqualTo("x"));
            Assert.That(functionDefinition.Parameters.ElementAt(1).Name, Is.EqualTo("y"));

            Assert.That(functionDefinition.DefaultParameters.Count, Is.EqualTo(2));
            Assert.That(functionDefinition.DefaultParameters["x"], Is.EqualTo(new IntegerConstantExpression(0)));
            Assert.That(functionDefinition.DefaultParameters["y"], Is.EqualTo(new IntegerConstantExpression(1)));

            void testPoint(int? x, int? y)
            {
                var parameters = new List<ExpressionBase>();
                if (x != null)
                    parameters.Add(new AssignmentExpression(new VariableExpression("x"), new IntegerConstantExpression(x.Value)));
                if (y != null)
                    parameters.Add(new AssignmentExpression(new VariableExpression("y"), new IntegerConstantExpression(y.Value)));

                ExpressionBase result;
                var functionCall = new FunctionCallExpression("Point", parameters);
                Assert.That(functionCall.Evaluate(scope, out result), Is.True);

                Assert.That(result, Is.Not.Null.And.InstanceOf<ClassInstanceExpression>());
                var instance = (ClassInstanceExpression)result;

                var xValue = instance.GetFieldValue("x");
                Assert.That(xValue, Is.InstanceOf<IntegerConstantExpression>());
                if (x != null)
                    Assert.That(((IntegerConstantExpression)xValue).Value, Is.EqualTo(x.Value));
                else
                    Assert.That(((IntegerConstantExpression)xValue).Value, Is.EqualTo(0));

                var yValue = instance.GetFieldValue("y");
                Assert.That(yValue, Is.InstanceOf<IntegerConstantExpression>());
                if (y != null)
                    Assert.That(((IntegerConstantExpression)yValue).Value, Is.EqualTo(y.Value));
                else
                    Assert.That(((IntegerConstantExpression)yValue).Value, Is.EqualTo(1));
            }

            testPoint(null, null);
            testPoint(99, null);
            testPoint(4, -7);
            testPoint(null, 11);
        }

        [Test]
        public void TestGetDependencies()
        {
            var expr = Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(this.addr + global_var)\n" +
                "}");

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(3));
            Assert.That(dependencies.Contains("word"));
            Assert.That(dependencies.Contains(".addr"));
            Assert.That(dependencies.Contains("global_var"));
        }

        [Test]
        public void TestGetDependenciesNested()
        {
            var expr = Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function hp() => word(this.addr.x + global.val)\n" +
                "}");

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(5));
            Assert.That(dependencies.Contains("word"));
            Assert.That(dependencies.Contains(".addr"));
            Assert.That(dependencies.Contains(".x"));
            Assert.That(dependencies.Contains("global"));
            Assert.That(dependencies.Contains(".val"));
        }

        [Test]
        public void TestGetModifications()
        {
            var expr = Parse(
                "class Entity {\n" +
                "  addr = 0\n" +
                "  function do() {" +
                "    global.val = this.addr\n" +
                "    this.addr = global_func(this.addr)\n" +
                "  }\n" +
                "}");

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(4));
            Assert.That(modifications.Contains("Entity"));
            Assert.That(modifications.Contains(".addr"));
            Assert.That(modifications.Contains(".do"));
            Assert.That(modifications.Contains(".val"));
        }
    }
}
