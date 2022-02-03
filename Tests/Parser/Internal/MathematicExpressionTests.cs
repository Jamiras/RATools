using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class MathematicExpressionTests
    {
        [Test]
        [TestCase(MathematicOperation.Add, "variable + 99")]
        [TestCase(MathematicOperation.Subtract, "variable - 99")]
        [TestCase(MathematicOperation.Multiply, "variable * 99")]
        [TestCase(MathematicOperation.Divide, "variable / 99")]
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
            Assert.That(result.ToString(), Is.EqualTo(left.ToString()));
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
            Assert.That(result.ToString(), Is.EqualTo(left.ToString()));
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
            Assert.That(result.ToString(), Is.EqualTo(left.ToString()));
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
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("Division by zero"));
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
            Assert.That(result.ToString(), Is.EqualTo(left.ToString()));
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
            Assert.That(((ParseErrorExpression)result).Message, Is.EqualTo("Division by zero"));
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

        private MathematicOperation GetOperation(char c)
        {
            switch (c)
            {
                case '+': return MathematicOperation.Add;
                case '-': return MathematicOperation.Subtract;
                case '*': return MathematicOperation.Multiply;
                case '/': return MathematicOperation.Divide;
                default: return MathematicOperation.None;
            }
        }

        [Test]
        [TestCase("byte(0) + 3 + 1", "byte(0) + 4")]
        [TestCase("byte(0) + 3 - 1", "byte(0) + 2")]
        [TestCase("byte(0) + 1 - 3", "byte(0) - 2")]
        [TestCase("byte(0) + 3 - 3", "byte(0)")]
        [TestCase("byte(0) - 3 - 1", "byte(0) - 4")]
        [TestCase("byte(0) - 3 + 1", "byte(0) - 2")]
        [TestCase("byte(0) - 1 + 3", "byte(0) + 2")]
        [TestCase("byte(0) - 3 + 3", "byte(0)")]
        [TestCase("byte(0) * 4 * 2", "byte(0) * 8")]
        [TestCase("byte(0) * 4 / 2", "byte(0) * 2")]
        [TestCase("byte(0) * 2 / 4", "byte(0) * 2 / 4")] // don't convert integer division to float yet
        [TestCase("byte(0) * 2.0 / 4", "byte(0) * 0.5")] // do convert partial float division to float
        [TestCase("byte(0) * 2 / 4.0", "byte(0) * 0.5")] // do convert partial float division to float
        [TestCase("byte(0) * 2 / 3.0", "byte(0) * 0.666667")] // do convert partial float division to float
        [TestCase("byte(0) * 3 / 3", "byte(0)")]
        [TestCase("byte(0) / 4 / 2", "byte(0) / 8")]
        [TestCase("byte(0) / 4 * 2", "byte(0) / 4 * 2")] // divide followed by multiply removes the modulus portion, cannot combine
        [TestCase("byte(0) / 2 * 4", "byte(0) / 2 * 4")] // divide followed by multiply removes the modulus portion, cannot combine
        [TestCase("byte(0) / 3 * 3", "byte(0) / 3 * 3")] // divide followed by multiply removes the modulus portion, cannot combine
        [TestCase("byte(0) / 4.0 / 2", "byte(0) / 8.0")]
        [TestCase("byte(0) / 4.0 * 2", "byte(0) * 0.5")] // divide followed by multiply with floats can be merged
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
    }
}
