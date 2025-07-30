using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using System.Text;

namespace RATools.Parser.Tests.Expressions
{
    [TestFixture]
    class ClassMemberReferenceExpressionTests
    {
        private ClassDefinitionExpression _pointClass;
        private ClassDefinitionExpression _rectClass;
        private ClassDefinitionExpression _characterClass;

        [OneTimeSetUp]
        public void FixtureSetUp() 
        {
            _pointClass = ExpressionTests.Parse<ClassDefinitionExpression>(
                "class Point\n" +
                "{\n" +
                "   x = 640\n" +
                "   y = 480\n" +
                "}\n"
            );

            _rectClass = ExpressionTests.Parse<ClassDefinitionExpression>(
                "class Rect\n" +
                "{\n" +
                "    p1 = Point(0, 0)\n" +
                "    p2 = Point()\n" +
                "\n" +
                "    function width() => this.p2.x - this.p1.x\n" +
                "    function height() => this.p2.y - this.p1.y\n" +
                "    function area() => this.width() * this.height()\n" +
                "\n" +
                "    function union(that)\n" +
                "    {\n" +
                "        result = Rect()\n" +
                "        result.p1 = this.p1\n" +
                "        result.p2 = that.p2\n" +
                "        return result\n" +
                "    }\n" +
                "}\n"
            );

            _characterClass = ExpressionTests.Parse<ClassDefinitionExpression>(
                "class CharacterData\n" + 
                "{\n" +
                "    base_address = 0\n" +
                "\n" + 
                "    function current_hp() => word(this.base_address)\n" +
                "    function max_hp() => word(this.base_address + 2)\n" +
                "    function level() => byte(this.base_address + 4)\n" +
                "    function class() => byte(this.base_address + 5)\n" +
                "    function afflictions() => word(this.base_address + 6)\n" +
                "    function exp() => dword(this.base_address + 8)\n" +
                "}\n"
            );
        }

        private InterpreterScope InitializeScopeForPoint()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new AchievementScriptContext();
            _pointClass.Execute(scope);

            var constructor = new FunctionCallExpression("Point", new ExpressionBase[0]);
            var pt = constructor.Evaluate(scope);
            Assert.That(pt, Is.InstanceOf<ClassInstanceExpression>());

            scope.AssignVariable(new VariableExpression("pt"), pt);
            return scope;
        }

        private InterpreterScope InitializeScopeForTwoPoints()
        {
            var scope = InitializeScopeForPoint();

            var constructor = new FunctionCallExpression("Point", new ExpressionBase[0]);
            var pt2 = constructor.Evaluate(scope);
            Assert.That(pt2, Is.InstanceOf<ClassInstanceExpression>());

            scope.AssignVariable(new VariableExpression("pt2"), pt2);
            return scope;
        }

        private InterpreterScope InitializeScopeForRect()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new AchievementScriptContext();
            _pointClass.Execute(scope);
            _rectClass.Execute(scope);

            var constructor = new FunctionCallExpression("Rect", new ExpressionBase[0]);
            var rect = constructor.Evaluate(scope);
            Assert.That(rect, Is.InstanceOf<ClassInstanceExpression>());

            scope.AssignVariable(new VariableExpression("rect"), rect);
            return scope;
        }

        private InterpreterScope InitializeScopeForCharacterData(int baseAddress)
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new AchievementScriptContext();
            _characterClass.Execute(scope);

            var constructor = new FunctionCallExpression("CharacterData",
                new ExpressionBase[] { new IntegerConstantExpression(baseAddress) });
            var data = constructor.Evaluate(scope);
            Assert.That(data, Is.InstanceOf<ClassInstanceExpression>());

            scope.AssignVariable(new VariableExpression("char1"), data);
            return scope;
        }

        private static AssignmentExpression Parse(string input)
        {
            // mimics Parse without calling
            // ReplaceVariables (which will try to find the class instance in the generic scope)
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr, Is.InstanceOf<AssignmentExpression>());
            return (AssignmentExpression)expr;
        }

        [Test]
        public void TestReadField()
        {
            var scope = InitializeScopeForPoint();
            var readX = Parse("a = pt.x");
            Assert.That(readX.Execute(scope), Is.Null);

            var value = scope.GetVariable("a");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(640));
        }

        [Test]
        public void TestWriteField()
        {
            var scope = InitializeScopeForPoint();
            var writeX = Parse("pt.x = 77");
            Assert.That(writeX.Execute(scope), Is.Null);

            var pt = (ClassInstanceExpression)scope.GetVariable("pt");
            var value = pt.GetFieldValue("x");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(77));
        }

        [Test]
        public void TestUniqueInstances()
        {
            var scope = InitializeScopeForTwoPoints();
            var writeX = Parse("pt.x = 77");
            Assert.That(writeX.Execute(scope), Is.Null);

            var readX = Parse("a = pt.x");
            Assert.That(readX.Execute(scope), Is.Null);

            var value = scope.GetVariable("a");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(77));

            var readX2 = Parse("a = pt2.x");
            Assert.That(readX2.Execute(scope), Is.Null);

            value = scope.GetVariable("a");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(640));
        }

        [Test]
        public void TestReadNestedField()
        {
            var scope = InitializeScopeForRect();
            var readX = Parse("a = rect.p2.x");
            Assert.That(readX.Execute(scope), Is.Null);

            var value = scope.GetVariable("a");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(640));
        }

        [Test]
        public void TestWriteNestedField()
        {
            var scope = InitializeScopeForRect();
            var writeX = Parse("rect.p1.x = 77");
            Assert.That(writeX.Execute(scope), Is.Null);

            var rect = (ClassInstanceExpression)scope.GetVariable("rect");
            var p1 = rect.GetFieldValue("p1");
            Assert.That(p1, Is.Not.Null.And.InstanceOf<ClassInstanceExpression>());
            var value = ((ClassInstanceExpression)p1).GetFieldValue("x");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(77));
        }

        [Test]
        public void TestFunctionCall()
        {
            var scope = InitializeScopeForRect();

            var tokenizer = Tokenizer.CreateTokenizer("w = rect.width()");
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr, Is.InstanceOf<AssignmentExpression>());
            var calc_width = (AssignmentExpression)expr;
            Assert.That(calc_width.Execute(scope), Is.Null);

            tokenizer = Tokenizer.CreateTokenizer("a = rect.area()");
            expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr, Is.InstanceOf<AssignmentExpression>());
            var calc_area = (AssignmentExpression)expr;
            Assert.That(calc_area.Execute(scope), Is.Null);

            var value = scope.GetVariable("w");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(640));
            value = scope.GetVariable("a");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(640 * 480));

            var writeX = Parse("rect.p1.x = 77");
            Assert.That(writeX.Execute(scope), Is.Null);
            Assert.That(calc_width.Execute(scope), Is.Null);
            Assert.That(calc_area.Execute(scope), Is.Null);

            value = scope.GetVariable("w");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(640 - 77));
            value = scope.GetVariable("a");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo((640 - 77) * 480));
        }

        [Test]
        public void TestSelfConstruction()
        {
            // verifies a function in Rect can construct a new Rect

            var scope = InitializeScopeForRect(); // rect = {0, 0, 640, 480}

            var tokenizer = Tokenizer.CreateTokenizer("rect2 = Rect(Point(60, 80), Point(100, 120))");
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)expr).Execute(scope), Is.Null);

            tokenizer = Tokenizer.CreateTokenizer("u = rect.union(rect2)");
            expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr, Is.InstanceOf<AssignmentExpression>());
            Assert.That(((AssignmentExpression)expr).Execute(scope), Is.Null);

            var value = scope.GetVariable("u");
            Assert.That(value, Is.Not.Null.And.InstanceOf<ClassInstanceExpression>());
            var instance = (ClassInstanceExpression)value;

            tokenizer = Tokenizer.CreateTokenizer("w = u.width()");
            expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            Assert.That(expr, Is.InstanceOf<AssignmentExpression>());
            var calc_width = (AssignmentExpression)expr;
            Assert.That(calc_width.Execute(scope), Is.Null);

            value = scope.GetVariable("w"); // u = {0, 0, 100, 120}
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(100));
        }

        [Test]
        public void TestMemoryStruct()
        {
            var scope = InitializeScopeForCharacterData(0x1234);
            var readX = Parse("a = char1.level()");
            Assert.That(readX.Execute(scope), Is.Null);

            var value = scope.GetVariable("a");
            Assert.That(value, Is.Not.Null.And.InstanceOf<MemoryAccessorExpression>());
            var builder = new StringBuilder();
            value.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("byte(0x001238)"));
        }

        [Test]
        public void TestReadUnknownField()
        {
            var scope = InitializeScopeForPoint();
            var readX = Parse("a = pt.z");
            var error = readX.Execute(scope);
            ExpressionTests.AssertError(error, "z is not a member of Point");
        }

        [Test]
        public void TestWriteUnknownField()
        {
            var scope = InitializeScopeForPoint();
            var readX = Parse("pt.z = 66");
            var error = readX.Execute(scope);
            ExpressionTests.AssertError(error, "z is not a member of Point");
        }

        [Test]
        public void TestParseError()
        {
            var tokenizer = Tokenizer.CreateTokenizer("a = pt.1");
            var error = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            ExpressionTests.AssertError(error, "Expected identifier not found after period");

            tokenizer = Tokenizer.CreateTokenizer("a = pt. x");
            error = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            ExpressionTests.AssertError(error, "Expected identifier not found after period");
        }

        [Test]
        public void TestReadFromObjectArray()
        {
            var scope = InitializeScopeForTwoPoints();
            var writeX = Parse("pt2.x = 77");
            Assert.That(writeX.Execute(scope), Is.Null);
            var assign = Parse("a = [pt, pt2]");
            Assert.That(assign.Execute(scope), Is.Null);

            var readX = Parse("x = a[1].x");
            Assert.That(readX.Execute(scope), Is.Null);
            var value = scope.GetVariable("x");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(77));
        }

        [Test]
        public void TestWriteToObjectArray()
        {
            var scope = InitializeScopeForTwoPoints();
            var assign = Parse("a = [pt, pt2]");
            Assert.That(assign.Execute(scope), Is.Null);

            var readX = Parse("a[1].x = 77");
            Assert.That(readX.Execute(scope), Is.Null);
            var pt = (ClassInstanceExpression)scope.GetVariable("pt2");
            var value = pt.GetFieldValue("x");
            Assert.That(value, Is.Not.Null.And.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)value).Value, Is.EqualTo(77));
        }
    }
}
