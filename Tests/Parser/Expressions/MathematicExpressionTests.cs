using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using RATools.Parser.Tests.Expressions.Trigger;
using System.Text;

namespace RATools.Parser.Tests.Expressions
{
    [TestFixture]
    class MathematicExpressionTests
    {
        [Test]
        [TestCase(MathematicOperation.Add, "variable + 99")]
        [TestCase(MathematicOperation.Subtract, "variable - 99")]
        [TestCase(MathematicOperation.Multiply, "variable * 99")]
        [TestCase(MathematicOperation.Divide, "variable / 99")]
        [TestCase(MathematicOperation.Modulus, "variable % 99")]
        [TestCase(MathematicOperation.BitwiseAnd, "variable & 99")]
        [TestCase(MathematicOperation.BitwiseXor, "variable ^ 99")]
        public void TestAppendString(MathematicOperation op, string expected)
        {
            var variable = new VariableExpression("variable");
            var value = new IntegerConstantExpression(99);
            var expr = new MathematicExpression(variable, op, value);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestAdd()
        {
            var left = new IntegerConstantExpression(1);
            var right = new IntegerConstantExpression(2);
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(3));
        }

        [Test]
        public void TestAddZero()
        {
            var left = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(0) });
            var right = new IntegerConstantExpression(0);
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            ExpressionTests.AssertAppendString(result, "byte(0x000000)");
        }

        [Test]
        public void TestAddVariables()
        {
            var value1 = new IntegerConstantExpression(1);
            var value2 = new IntegerConstantExpression(2);
            var left = new VariableExpression("left");
            var right = new VariableExpression("right");
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();
            scope.AssignVariable(left, value1);
            scope.AssignVariable(right, value2);

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(3));
        }

        [Test]
        public void TestAddStrings()
        {
            var left = new StringConstantExpression("ban");
            var right = new StringConstantExpression("ana");
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo("banana"));
        }

        [Test]
        public void TestAddStringNumber()
        {
            var left = new StringConstantExpression("ban");
            var right = new IntegerConstantExpression(2);
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo("ban2"));
        }

        [Test]
        public void TestAddNumberString()
        {
            var left = new IntegerConstantExpression(1);
            var right = new StringConstantExpression("ana");
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo("1ana"));
        }

        [Test]
        public void TestAddStringExpression()
        {
            var left = new StringConstantExpression("ban");
            var right1 = new IntegerConstantExpression(6);
            var right2 = new IntegerConstantExpression(2);
            var right = new MathematicExpression(right1, MathematicOperation.Subtract, right2);
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo("ban4"));
        }

        [Test]
        public void TestAddExpressionString()
        {
            var left1 = new IntegerConstantExpression(6);
            var left2 = new IntegerConstantExpression(2);
            var left = new MathematicExpression(left1, MathematicOperation.Subtract, left2);
            var right = new StringConstantExpression("ana");
            var expr = new MathematicExpression(left, MathematicOperation.Add, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)result).Value, Is.EqualTo("4ana"));
        }

        [Test]
        public void TestSubtract()
        {
            var left = new IntegerConstantExpression(8);
            var right = new IntegerConstantExpression(2);
            var expr = new MathematicExpression(left, MathematicOperation.Subtract, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(6));
        }

        [Test]
        public void TestSubtractZero()
        {
            var left = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(0) });
            var right = new IntegerConstantExpression(0);
            var expr = new MathematicExpression(left, MathematicOperation.Subtract, right);
            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            ExpressionTests.AssertAppendString(result, "byte(0x000000)");
        }

        [Test]
        public void TestMultiply()
        {
            var left = new IntegerConstantExpression(7);
            var right = new IntegerConstantExpression(3);
            var expr = new MathematicExpression(left, MathematicOperation.Multiply, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(21));
        }

        [Test]
        public void TestMultiplyZero()
        {
            var left = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(0) });
            var right = new IntegerConstantExpression(0);
            var expr = new MathematicExpression(left, MathematicOperation.Multiply, right);
            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result.ToString(), Is.EqualTo(right.ToString()));
        }

        [Test]
        public void TestMultiplyOne()
        {
            var left = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(0) });
            var right = new IntegerConstantExpression(1);
            var expr = new MathematicExpression(left, MathematicOperation.Multiply, right);
            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            ExpressionTests.AssertAppendString(result, "byte(0x000000)");
        }

        [Test]
        public void TestDivide()
        {
            var left = new IntegerConstantExpression(20);
            var right = new IntegerConstantExpression(3);
            var expr = new MathematicExpression(left, MathematicOperation.Divide, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(6));
        }

        [Test]
        public void TestDivideZero()
        {
            var left = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(0) });
            var right = new IntegerConstantExpression(0);
            var expr = new MathematicExpression(left, MathematicOperation.Divide, right);
            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Division by zero"));
        }

        [Test]
        public void TestDivideOne()
        {
            var left = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(0) });
            var right = new IntegerConstantExpression(1);
            var expr = new MathematicExpression(left, MathematicOperation.Divide, right);
            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            ExpressionTests.AssertAppendString(result, "byte(0x000000)");
        }

        [Test]
        public void TestModulus()
        {
            var left = new IntegerConstantExpression(20);
            var right = new IntegerConstantExpression(3);
            var expr = new MathematicExpression(left, MathematicOperation.Modulus, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(2));
        }

        [Test]
        public void TestModulusZero()
        {
            var left = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(0) });
            var right = new IntegerConstantExpression(0);
            var expr = new MathematicExpression(left, MathematicOperation.Modulus, right);
            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Division by zero"));
        }

        [Test]
        public void TestModulusOne()
        {
            var left = new FunctionCallExpression("byte", new[] { new IntegerConstantExpression(0) });
            var right = new IntegerConstantExpression(1);
            var expr = new MathematicExpression(left, MathematicOperation.Modulus, right);
            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result.ToString(), Is.EqualTo(new IntegerConstantExpression(0).ToString()));
        }

        [Test]
        public void TestBitwiseAnd()
        {
            var left = new IntegerConstantExpression(0xCF);
            var right = new IntegerConstantExpression(0x56);
            var expr = new MathematicExpression(left, MathematicOperation.BitwiseAnd, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(0x46));
        }

        [Test]
        public void TestBitwiseAndZero()
        {
            var left = new IntegerConstantExpression(0xCF);
            var right = new IntegerConstantExpression(0);
            var expr = new MathematicExpression(left, MathematicOperation.BitwiseAnd, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(0));
        }

        [Test]
        public void TestBitwiseXor()
        {
            var left = new IntegerConstantExpression(0xCF);
            var right = new IntegerConstantExpression(0x56);
            var expr = new MathematicExpression(left, MathematicOperation.BitwiseXor, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(0x99));
        }

        [Test]
        public void TestBitwiseXorZero()
        {
            var left = new IntegerConstantExpression(0xCF);
            var right = new IntegerConstantExpression(0);
            var expr = new MathematicExpression(left, MathematicOperation.BitwiseXor, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(0xCF));
        }

        [Test]
        public void TestBitwiseInvert()
        {
            var right = new IntegerConstantExpression(0x56);
            var expr = new MathematicExpression(null, MathematicOperation.BitwiseInvert, right);
            var scope = new InterpreterScope();

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That((uint)((IntegerConstantExpression)result).Value, Is.EqualTo(0xFFFFFFA9));
        }

        [Test]
        [TestCase("byte(0) + 3 + 1", "byte(0x000000) + 4")]
        [TestCase("byte(0) + 3 - 1", "byte(0x000000) + 2")]
        [TestCase("byte(0) + 1 - 3", "byte(0x000000) - 2")]
        [TestCase("byte(0) + 3 - 3", "byte(0x000000)")]
        [TestCase("byte(0) - 3 - 1", "byte(0x000000) - 4")]
        [TestCase("byte(0) - 3 + 1", "byte(0x000000) - 2")]
        [TestCase("byte(0) - 1 + 3", "byte(0x000000) + 2")]
        [TestCase("byte(0) - 3 + 3", "byte(0x000000)")]
        [TestCase("byte(0) * 4 * 2", "byte(0x000000) * 8")]
        [TestCase("byte(0) * 4 / 2", "byte(0x000000) * 2")]
        [TestCase("byte(0) * 2 / 4", "byte(0x000000) / 2")] // whole integer division can be converted
        [TestCase("byte(0) * 3 / 4", "remembered(byte(0x000000) * 3) / 4")] // don't convert integer division to float yet
        [TestCase("byte(0) * 2.0 / 4", "byte(0x000000) * 0.5")] // do convert partial float division to float
        [TestCase("byte(0) * 2 / 4.0", "byte(0x000000) * 0.5")] // do convert partial float division to float
        [TestCase("byte(0) * 2 / 3.0", "byte(0x000000) * 0.666667")] // do convert partial float division to float
        [TestCase("byte(0) * 3 / 3", "byte(0x000000)")]
        [TestCase("byte(0) / 4 / 2", "byte(0x000000) / 8")]
        [TestCase("byte(0) / 4 * 2", "remembered(byte(0x000000) / 4) * 2")] // divide followed by multiply removes the modulus portion, cannot combine
        [TestCase("byte(0) / 2 * 4", "remembered(byte(0x000000) / 2) * 4")] // divide followed by multiply removes the modulus portion, cannot combine
        [TestCase("byte(0) / 3 * 3", "remembered(byte(0x000000) / 3) * 3")] // divide followed by multiply removes the modulus portion, cannot combine
        [TestCase("byte(0) / 4.0 / 2", "byte(0x000000) / 8.0")]
        [TestCase("byte(0) / 4.0 * 2", "byte(0x000000) / 2.0")] // divide followed by multiply with floats can be merged
        [TestCase("byte(0) & 12 & 5", "byte(0x000000) & 0x00000004")] // bitwise and is commutative. prefer merging constants
        [TestCase("byte(0) & 12 * 5", "byte(0x000000) & 0x0000003C")] // multiplication has higher precedence
        [TestCase("byte(0) & 12 - 5", "byte(0x000000) & 0x00000007")] // addition has higher precedence
        [TestCase("byte(0) ^ 12 ^ 5", "byte(0x000000) ^ 0x00000009")] // bitwise and is commutative. merge constants
        [TestCase("byte(0) ^ 12 & 5", "byte(0x000000) ^ 0x00000004")] // bitwise and has higher precedence
        public void TestCombining(string input, string expected)
        {
            var expr = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expr, Is.InstanceOf<MathematicExpression>());

            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.True);

            var builder = new StringBuilder();
            result.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestUnaryMinusVariable()
        {
            var scope = AchievementScriptTests.Evaluate(
                "a = 8\n" +
                "b = -a"
            );

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(-8));
        }

        [Test]
        public void TestUnaryMinusAddition()
        {
            var scope = AchievementScriptTests.Evaluate(
                "a = 8\n" +
                "b = -a + 2"
            );

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(-6));
        }

        [Test]
        public void TestUnaryMinusMultiplication()
        {
            var scope = AchievementScriptTests.Evaluate(
                "a = 8\n" +
                "b = -a * 2"
            );

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(-16));
        }

        [Test]
        public void TestUnaryMinusParenthesis()
        {
            var scope = AchievementScriptTests.Evaluate(
                "a = 8\n" +
                "b = -(a + 2)"
            );

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(-10));
        }

        [Test]
        public void TestUnaryMinusArrayValue()
        {
            var scope = AchievementScriptTests.Evaluate(
                "a = [8]\n" +
                "b = -a[0]"
            );

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(-8));
        }

        [Test]
        public void TestUnaryMinusFunctionCall()
        {
            var scope = AchievementScriptTests.Evaluate(
                "function a(n) => n\n" +
                "b = -a(8)"
            );

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(-8));
        }
    }
}
