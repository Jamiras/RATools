using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using RATools.Parser.Tests.Expressions.Trigger;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class AsciiStringEqualsFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new AsciiStringEqualsFunction();
            Assert.That(def.Name.Name, Is.EqualTo("ascii_string_equals"));
            Assert.That(def.Parameters.Count, Is.EqualTo(4));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("address"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("string"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("length"));
            Assert.That(def.Parameters.ElementAt(3).Name, Is.EqualTo("transform"));

            Assert.That(def.DefaultParameters.Count, Is.EqualTo(2));
            Assert.That(def.DefaultParameters.ElementAt(0).Key, Is.EqualTo("length"));
            Assert.That(def.DefaultParameters.ElementAt(0).Value, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)def.DefaultParameters.ElementAt(0).Value).Value, Is.EqualTo(int.MaxValue));
            Assert.That(def.DefaultParameters.ElementAt(1).Key, Is.EqualTo("transform"));
            Assert.That(def.DefaultParameters.ElementAt(1).Value, Is.InstanceOf<FunctionReferenceExpression>());
            Assert.That(((FunctionReferenceExpression)def.DefaultParameters.ElementAt(1).Value).Name, Is.EqualTo("identity_transform"));
        }

        [Test]
        [TestCase("0x1234", "test", 4, "0xX001234=1953719668")] // 0x74736574
        [TestCase("0x1234", "test", 3, "0xW001234=7562612")] // 0x736574
        [TestCase("0x1234", "test", 5, "0xX001234=1953719668_0xH001238=0")] // 0x74736574 00
        [TestCase("0x1234", "test1", 6, "0xX001234=1953719668_0x 001238=49")] // 0x74736574 3100
        [TestCase("0x1234", "Testing1234", 11, "0xX001234=1953719636_0xX001238=828862057_0xW00123c=3420978")] // 0x74736554 31676E69 343332
        [TestCase("0x1234", "", 1, "0xH001234=0")] // 0x00
        [TestCase("0x1234", "test1", Int32.MaxValue, "0xX001234=1953719668_0xH001238=49")] // 0x74736574 31
        [TestCase("dword(0x1234)", "test1", 6,
            "I:0xX001234_0xX000000=1953719668_I:0xX001234_0x 000004=49")] // 0x74736574 3100
        [TestCase("dword(0x1234) + 32", "Testing1234", 11,
            "I:0xX001234_0xX000020=1953719636_I:0xX001234_0xX000024=828862057_I:0xX001234_0xW000028=3420978")] // 0x74736554 31676E69 343332
        [TestCase("dword(dword(0x1234) + 8) + 0x2c", "test1", 6,
            "I:0xX001234_I:0xX000008_0xX00002c=1953719668_I:0xX001234_I:0xX000008_0x 000030=49")] // 0x74736574 3100
        public void TestEvaluate(string address, string input, int length, string expected)
        {
            var parameters = new List<ExpressionBase>();
            parameters.Add(ExpressionTests.Parse(address));
            parameters.Add(new StringConstantExpression(input));
            parameters.Add(new IntegerConstantExpression(length));

            var expression = new FunctionCallExpression("ascii_string_equals", parameters);
            var scope = new InterpreterScope();
            scope.AddFunction(new AsciiStringEqualsFunction());
            scope.AddFunction(new IdentityTransformFunction());

            ExpressionBase result;
            Assert.IsTrue(expression.Evaluate(scope, out result));

            Assert.That(result, Is.InstanceOf<RequirementClauseExpression>());
            TriggerExpressionTests.AssertSerialize((RequirementClauseExpression)result, expected);
        }

        [Test]
        [TestCase("0x1234", "test", 4, "d0xX001234=1953719668")] // 0x74736574
        [TestCase("0x1234", "test", 3, "d0xW001234=7562612")] // 0x736574
        [TestCase("0x1234", "test", 5, "d0xX001234=1953719668_d0xH001238=0")] // 0x74736574 00
        [TestCase("0x1234", "test1", 6, "d0xX001234=1953719668_d0x 001238=49")] // 0x74736574 3100
        [TestCase("0x1234", "Testing1234", 11, "d0xX001234=1953719636_d0xX001238=828862057_d0xW00123c=3420978")] // 0x74736554 31676E69 343332
        [TestCase("0x1234", "", 1, "d0xH001234=0")] // 0x00
        [TestCase("0x1234", "test1", Int32.MaxValue, "d0xX001234=1953719668_d0xH001238=49")] // 0x74736574 31
        [TestCase("dword(0x1234)", "test1", 6,
            "I:0xX001234_d0xX000000=1953719668_I:0xX001234_d0x 000004=49")] // 0x74736574 3100
        [TestCase("dword(0x1234) + 32", "Testing1234", 11,
            "I:0xX001234_d0xX000020=1953719636_I:0xX001234_d0xX000024=828862057_I:0xX001234_d0xW000028=3420978")] // 0x74736554 31676E69 343332
        [TestCase("dword(dword(0x1234) + 8) + 0x2c", "test1", 6,
            "I:0xX001234_I:0xX000008_d0xX00002c=1953719668_I:0xX001234_I:0xX000008_d0x 000030=49")] // 0x74736574 3100
        public void TestEvaluatePrev(string address, string input, int length, string expected)
        {
            var parameters = new List<ExpressionBase>();
            parameters.Add(ExpressionTests.Parse(address));
            parameters.Add(new StringConstantExpression(input));
            parameters.Add(new IntegerConstantExpression(length));
            parameters.Add(new FunctionReferenceExpression("prev"));

            var expression = new FunctionCallExpression("ascii_string_equals", parameters);
            var scope = new InterpreterScope();
            scope.AddFunction(new AsciiStringEqualsFunction());
            scope.AddFunction(new PrevPriorFunction("prev", RATools.Data.FieldType.PreviousValue));

            ExpressionBase result;
            Assert.IsTrue(expression.Evaluate(scope, out result));

            Assert.That(result, Is.InstanceOf<RequirementClauseExpression>());
            TriggerExpressionTests.AssertSerialize((RequirementClauseExpression)result, expected);
        }

        [Test]
        public void TestNot()
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>("!ascii_string_equals(0x1234, \"Testing1234\")");
            TriggerExpressionTests.AssertSerialize(clause, "O:0xX001234!=1953719636_O:0xX001238!=828862057_0xW00123c!=3420978");
        }

        [Test]
        [TestCase(0, "âeiou")]
        [TestCase(1, "aëiou")]
        [TestCase(2, "aeìou")]
        [TestCase(3, "aeiôu")]
        [TestCase(4, "aeioú")]
        public void TestInvalidCharacter(int errorOffset, string input)
        {
            var parameters = new List<ExpressionBase>();
            parameters.Add(new IntegerConstantExpression(0x1234));
            parameters.Add(new StringConstantExpression(input));
            parameters.Add(new IntegerConstantExpression(Int32.MaxValue));

            var expression = new FunctionCallExpression("ascii_string_equals", parameters);
            var scope = new InterpreterScope();
            scope.AddFunction(new AsciiStringEqualsFunction());
            scope.AddFunction(new IdentityTransformFunction());

            ExpressionBase result;
            Assert.IsFalse(expression.Evaluate(scope, out result));

            var expectedError = String.Format("Character {0} of string ({1}) cannot be converted to ASCII", errorOffset, input[errorOffset]);
            ExpressionTests.AssertError(result, expectedError);
        }

        [Test]
        public void TestLengthZero()
        {
            TriggerExpressionTests.AssertParseError("ascii_string_equals(0x1234, \"Test\", 0)",
                "length must be greater than 0");
        }
    }
}
