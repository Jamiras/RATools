using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;

namespace RATools.Tests.Parser.Internal
{
    [TestFixture]
    class InterpreterScopeTests
    {
        [Test]
        public void TestGetFunctionUndefined()
        {
            var scope = new InterpreterScope();
            Assert.That(scope.GetFunction("undefined"), Is.Null);
        }

        [Test]
        public void TestAddAndGetFunction()
        {
            var function = new FunctionDefinitionExpression("test");
            var scope = new InterpreterScope();
            scope.AddFunction(function);
            Assert.That(scope.GetFunction("test"), Is.SameAs(function));
        }

        [Test]
        public void TestAddAndGetFunctionNested()
        {
            var function = new FunctionDefinitionExpression("test");
            var scope = new InterpreterScope();
            scope.AddFunction(function);
            var scope2 = new InterpreterScope(scope);
            Assert.That(scope2.GetFunction("test"), Is.SameAs(function));
        }

        [Test]
        public void TestGetVariableUndefined()
        {
            var scope = new InterpreterScope();
            Assert.That(scope.GetVariable("undefined"), Is.Null);
        }

        [Test]
        public void TestAssignAndGetVariable()
        {
            var variable = new VariableExpression("test");
            var value = new IntegerConstantExpression(99);
            var scope = new InterpreterScope();
            scope.AssignVariable(variable, value);
            Assert.That(scope.GetVariable("test"), Is.SameAs(value));
        }

        [Test]
        public void TestAssignAndGetVariableNested()
        {
            var variable = new VariableExpression("test");
            var value = new IntegerConstantExpression(99);
            var scope = new InterpreterScope();
            scope.AssignVariable(variable, value);
            var scope2 = new InterpreterScope(scope);
            Assert.That(scope2.GetVariable("test"), Is.SameAs(value));
            Assert.That(scope.GetVariable("test"), Is.SameAs(value));
        }

        [Test]
        public void TestAssignAndGetVariableNestedOverride()
        {
            var variable = new VariableExpression("test");
            var value = new IntegerConstantExpression(99);
            var value2 = new IntegerConstantExpression(98);
            var scope = new InterpreterScope();
            var scope2 = new InterpreterScope(scope);
            scope.AssignVariable(variable, value);
            scope2.AssignVariable(variable, value2);
            Assert.That(scope2.GetVariable("test"), Is.SameAs(value2));
            Assert.That(scope.GetVariable("test"), Is.SameAs(value2));
        }

        [Test]
        public void TestDefineAndGetVariableNested()
        {
            var variable = new VariableDefinitionExpression("test");
            var value = new IntegerConstantExpression(99);
            var scope = new InterpreterScope();
            scope.DefineVariable(variable, value);
            var scope2 = new InterpreterScope(scope);
            Assert.That(scope2.GetVariable("test"), Is.SameAs(value));
            Assert.That(scope.GetVariable("test"), Is.SameAs(value));
        }

        [Test]
        public void TestDefineAndGetVariableNestedOverride()
        {
            var variable = new VariableDefinitionExpression("test");
            var value = new IntegerConstantExpression(99);
            var value2 = new IntegerConstantExpression(98);
            var scope = new InterpreterScope();
            var scope2 = new InterpreterScope(scope);
            scope.DefineVariable(variable, value);
            scope2.DefineVariable(variable, value2);
            Assert.That(scope2.GetVariable("test"), Is.SameAs(value2));
            Assert.That(scope.GetVariable("test"), Is.SameAs(value));
        }

        [Test]
        public void TestAssignVariableIndexed()
        {
            var variable = new VariableExpression("test");
            var value = new IntegerConstantExpression(99);
            var dict = new DictionaryExpression();
            var key = new IntegerConstantExpression(6);
            var scope = new InterpreterScope();
            scope.AssignVariable(variable, dict);

            var index = new IndexedVariableExpression(variable, key);
            scope.AssignVariable(index, value);

            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict[0].Value, Is.SameAs(value));
        }

        [Test]
        public void TestAssignVariableIndexedUpdate()
        {
            var variable = new VariableExpression("test");
            var value = new IntegerConstantExpression(99);
            var value2 = new IntegerConstantExpression(98);
            var dict = new DictionaryExpression();
            var key = new IntegerConstantExpression(6);
            dict.Add(key, value);
            var scope = new InterpreterScope();
            scope.AssignVariable(variable, dict);

            var index = new IndexedVariableExpression(variable, key);
            scope.AssignVariable(index, value2);

            Assert.That(dict[0].Value, Is.SameAs(value2));
        }

        [Test]
        public void TestAssignVariableIndexedArrayUpdate()
        {
            var variable = new VariableExpression("test");
            var value = new IntegerConstantExpression(99);
            var value2 = new IntegerConstantExpression(98);
            var array = new ArrayExpression();
            var key = new IntegerConstantExpression(0);
            array.Entries.Add(value);
            var scope = new InterpreterScope();
            scope.AssignVariable(variable, array);

            var index = new IndexedVariableExpression(variable, key);
            scope.AssignVariable(index, value2);

            Assert.That(array.Entries[0], Is.SameAs(value2));
        }

        [Test]
        public void TestAssignVariableIndexedArrayOutOfRange()
        {
            var variable = new VariableExpression("test");
            var value = new IntegerConstantExpression(99);
            var value2 = new IntegerConstantExpression(98);
            var array = new ArrayExpression();
            var key = new IntegerConstantExpression(1);
            array.Entries.Add(value);
            var scope = new InterpreterScope();
            scope.AssignVariable(variable, array);

            var index = new IndexedVariableExpression(variable, key);
            scope.AssignVariable(index, value2); // silently does nothing, TODO: make error

            Assert.That(array.Entries[0], Is.SameAs(value));
        }

        [Test]
        public void TestMerge()
        {
            var scope1 = new InterpreterScope();
            scope1.DefineVariable(new VariableDefinitionExpression("a"), new IntegerConstantExpression(1));

            var scope2 = new InterpreterScope();
            scope2.DefineVariable(new VariableDefinitionExpression("a"), new IntegerConstantExpression(4));
            scope2.DefineVariable(new VariableDefinitionExpression("d"), new IntegerConstantExpression(5));
            scope2.AddFunction(new FunctionDefinitionExpression("f1"));

            var scope3 = new InterpreterScope();
            scope3.DefineVariable(new VariableDefinitionExpression("b"), new IntegerConstantExpression(2));
            scope3.DefineVariable(new VariableDefinitionExpression("c"), new IntegerConstantExpression(3));

            var scope4 = new InterpreterScope();
            scope4.AddFunction(new FunctionDefinitionExpression("f1"));
            scope4.AddFunction(new FunctionDefinitionExpression("f2"));

            var union = new InterpreterScope();
            Assert.That(union.VariableCount, Is.EqualTo(0));
            Assert.That(union.FunctionCount, Is.EqualTo(0));

            union.Merge(scope1);
            Assert.That(union.VariableCount, Is.EqualTo(1));
            Assert.That(union.FunctionCount, Is.EqualTo(0));

            union.Merge(scope2);
            Assert.That(union.VariableCount, Is.EqualTo(2));
            Assert.That(union.FunctionCount, Is.EqualTo(1));
            Assert.That(union.GetVariable("a"), Is.EqualTo(new IntegerConstantExpression(4)));

            union.Merge(scope3);
            Assert.That(union.VariableCount, Is.EqualTo(4));
            Assert.That(union.FunctionCount, Is.EqualTo(1));

            union.Merge(scope4);
            Assert.That(union.VariableCount, Is.EqualTo(4));
            Assert.That(union.FunctionCount, Is.EqualTo(2));
        }
    }
}
