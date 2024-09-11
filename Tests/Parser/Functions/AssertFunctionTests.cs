using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class AssertFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new AssertFunction();
            Assert.That(def.Name.Name, Is.EqualTo("assert"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("condition"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("message"));
            Assert.That(def.DefaultParameters["message"], Is.EqualTo(new StringConstantExpression("")));
        }

        [Test]
        public void TestUndefined()
        {
            var scope = new InterpreterScope();

            FunctionTests.AssertExecuteError<AssertFunction>("assert(arr)", scope, "Unknown variable: arr");
        }

        [Test]
        public void TestMessageTrue()
        {
            var scope = new InterpreterScope();

            FunctionTests.Execute<AssertFunction>("assert(true, \"This should not be seen\")", scope);
        }

        [Test]
        public void TestMessageFalse()
        {
            var scope = new InterpreterScope();

            var error = FunctionTests.AssertExecuteError<AssertFunction>(
                "assert(false, \"This should be seen\")", scope, "Assertion failed: This should be seen");
            Assert.That(error.Location.ToString(), Is.EqualTo("1,8-1,12"));
        }


        [Test]
        public void TestNoMessageTrue()
        {
            var scope = new InterpreterScope();

            FunctionTests.Execute<AssertFunction>("assert(true)", scope);
        }

        [Test]
        public void TestNoMessageFalse()
        {
            var scope = new InterpreterScope();

            var error = FunctionTests.AssertExecuteError<AssertFunction>(
                "assert(false)", scope, "Assertion failed: false");
            Assert.That(error.Location.ToString(), Is.EqualTo("1,8-1,12"));

            AchievementScriptTests.Evaluate("assert(false)", "1:8 Assertion failed: false");
        }

        [Test]
        public void TestNoMessageComparison()
        {
            var scope = new InterpreterScope();
            scope.AssignVariable(new VariableExpression("a"), new IntegerConstantExpression(6));

            FunctionTests.Execute<AssertFunction>("assert(a == 6)", scope);

            var error = FunctionTests.AssertExecuteError<AssertFunction>(
                "assert(a == 4)", scope, "Assertion failed: a == 4");
            Assert.That(error.Location.ToString(), Is.EqualTo("1,8-1,13"));
        }

        [Test]
        public void TestMessageComparison()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.AssignVariable(new VariableExpression("a"), new IntegerConstantExpression(6));

            FunctionTests.Execute<AssertFunction>("assert(a == 6, format(\"{0} != 6\", a))", scope);

            var error = FunctionTests.AssertExecuteError<AssertFunction>(
                "assert(a == 4, format(\"{0} != 4\", a))", scope, "Assertion failed: 6 != 4");
            Assert.That(error.Location.ToString(), Is.EqualTo("1,8-1,13"));
        }

        [Test]
        public void TestInFunctionTrue()
        {
            AchievementScriptTests.Evaluate(
                "function f(a)\n" +
                "{\n" +
                "   assert(a < 5)\n" +
                "   return a + 3\n" +
                "}\n" +
                "b = f(2)"
             );
        }

        [Test]
        public void TestInFunctionFalse()
        {
            AchievementScriptTests.Evaluate(
                "function f(a)\n" +
                "{\n" +
                "   assert(a < 5)\n" +
                "   return a + 3\n" +
                "}\n" +
                "b = f(8)",

                "6:5 f call failed\r\n" +
                "- 3:11 Assertion failed: a < 5"
             );
        }
    }
}
