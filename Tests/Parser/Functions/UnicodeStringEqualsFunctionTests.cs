using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using RATools.Tests.Parser.Expressions;
using RATools.Tests.Parser.Expressions.Trigger;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Tests.Parser.Functions
{
    [TestFixture]
    class UnicodeStringEqualsFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new UnicodeStringEqualsFunction();
            Assert.That(def.Name.Name, Is.EqualTo("unicode_string_equals"));
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
        [TestCase("0x1234", "me", 2, "0xX001234=6619245")] // 0x0065006D
        [TestCase("0x1234", "test", 3, "0xX001234=6619252_0x 001238=115")] // 0x00650074 0073
        [TestCase("0x1234", "test", 5, "0xX001234=6619252_0xX001238=7602291_0x 00123c=0")] // 0x00650074 00740073 0000
        [TestCase("0x1234", "tęst", Int32.MaxValue, "0xX001234=18415732_0xX001238=7602291")] // 0x01190074 00740073
        [TestCase("dword(0x1234)", "test1", 6, // 0x00650074 00740073 00000031
            "I:0xX001234_0xX000000=6619252_I:0xX001234_0xX000004=7602291_I:0xX001234_0xX000008=49")]
        [TestCase("dword(dword(0x1234) + 8) + 0x2c", "test1", 6, // 0x00650074 00740073 00000031
            "I:0xX001234_I:0xX000008_0xX00002c=6619252_I:0xX001234_I:0xX000008_0xX000030=7602291_I:0xX001234_I:0xX000008_0xX000034=49")]
        public void TestEvaluate(string address, string input, int length, string expected)
        {
            var parameters = new List<ExpressionBase>();
            parameters.Add(ExpressionTests.Parse(address));
            parameters.Add(new StringConstantExpression(input));
            parameters.Add(new IntegerConstantExpression(length));

            var expression = new FunctionCallExpression("unicode_string_equals", parameters);
            var scope = new InterpreterScope();
            scope.AddFunction(new UnicodeStringEqualsFunction());
            scope.AddFunction(new IdentityTransformFunction());

            ExpressionBase result;
            Assert.IsTrue(expression.Evaluate(scope, out result));

            Assert.That(result, Is.InstanceOf<RequirementClauseExpression>());
            TriggerExpressionTests.AssertSerialize((RequirementClauseExpression)result, expected);
        }

        [Test]
        [TestCase("0x1234", "me", 2, "d0xX001234=6619245")] // 0x0065006D
        [TestCase("0x1234", "test", 3, "d0xX001234=6619252_d0x 001238=115")] // 0x00650074 0073
        [TestCase("0x1234", "test", 5, "d0xX001234=6619252_d0xX001238=7602291_d0x 00123c=0")] // 0x00650074 00740073 0000
        [TestCase("0x1234", "tęst", Int32.MaxValue, "d0xX001234=18415732_d0xX001238=7602291")] // 0x01190074 00740073
        [TestCase("dword(0x1234)", "test1", 6, // 0x00650074 00740073 00000031
            "I:0xX001234_d0xX000000=6619252_I:0xX001234_d0xX000004=7602291_I:0xX001234_d0xX000008=49")]
        [TestCase("dword(dword(0x1234) + 8) + 0x2c", "test1", 6, // 0x00650074 00740073 00000031
            "I:0xX001234_I:0xX000008_d0xX00002c=6619252_I:0xX001234_I:0xX000008_d0xX000030=7602291_I:0xX001234_I:0xX000008_d0xX000034=49")]
        public void TestEvaluatePrev(string address, string input, int length, string expected)
        {
            var parameters = new List<ExpressionBase>();
            parameters.Add(ExpressionTests.Parse(address));
            parameters.Add(new StringConstantExpression(input));
            parameters.Add(new IntegerConstantExpression(length));
            parameters.Add(new FunctionReferenceExpression("prev"));

            var expression = new FunctionCallExpression("unicode_string_equals", parameters);
            var scope = new InterpreterScope();
            scope.AddFunction(new UnicodeStringEqualsFunction());
            scope.AddFunction(new IdentityTransformFunction());
            scope.AddFunction(new PrevPriorFunction("prev", RATools.Data.FieldType.PreviousValue));

            ExpressionBase result;
            Assert.IsTrue(expression.Evaluate(scope, out result));

            Assert.That(result, Is.InstanceOf<RequirementClauseExpression>());
            TriggerExpressionTests.AssertSerialize((RequirementClauseExpression)result, expected);
        }

        [Test]
        public void TestNot()
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>("!unicode_string_equals(0x1234, \"Test\")");
            TriggerExpressionTests.AssertSerialize(clause, "O:0xX001234!=6619220_0xX001238!=7602291");
        }

        [Test]
        public void TestLengthZero()
        {
            TriggerExpressionTests.AssertParseError("unicode_string_equals(0x1234, \"Test\", 0)",
                "length must be greater than 0");
        }
    }
}
