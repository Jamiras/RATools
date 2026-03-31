using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class FieldMapFunctionTests
    {
        private ClassDefinitionExpression _lookupItemClass;
        private ClassDefinitionExpression _difficultyEnumClass;

        [OneTimeSetUp]
        public void FixtureSetUp()
        {
            _lookupItemClass = ExpressionTests.Parse<ClassDefinitionExpression>(
                "class LookupItem\n" +
                "{\n" +
                "   id = 0\n" +
                "   label = \"\"\n" +
                "}\n"
            );

            _difficultyEnumClass = ExpressionTests.Parse<ClassDefinitionExpression>(
                "class DifficultyEnum\n" +
                "{\n" +
                "    function current() => byte(0x1234)\n" +
                "\n" +
                "    none = LookupItem(0, \"\")\n" +
                "    easy = LookupItem(1, \"Easy\")\n" +
                "    medium = LookupItem(2, \"Medium\")\n" +
                "    hard = LookupItem(3, \"Hard\")\n" +
                "}\n"
            );
        }

        private InterpreterScope InitializeScopeForLookupItem()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new AchievementScriptContext();
            _lookupItemClass.Execute(scope);

            var constructor = new FunctionCallExpression("LookupItem", new ExpressionBase[0]);
            var item = constructor.Evaluate(scope);
            Assert.That(item, Is.InstanceOf<ClassInstanceExpression>());

            scope.AssignVariable(new VariableExpression("item"), item);
            return scope;
        }

        private InterpreterScope InitializeScopeForDifficulty()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new AchievementScriptContext();
            _lookupItemClass.Execute(scope);
            _difficultyEnumClass.Execute(scope);

            var constructor = new FunctionCallExpression("DifficultyEnum", new ExpressionBase[0]);
            var difficulty = constructor.Evaluate(scope);
            Assert.That(difficulty, Is.InstanceOf<ClassInstanceExpression>());

            scope.AssignVariable(new VariableExpression("Difficulty"), difficulty);
            return scope;
        }


        [Test]
        public void TestDefinition()
        {
            var def = new FieldMapFunction();
            Assert.That(def.Name.Name, Is.EqualTo("field_map"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("input"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("predicate"));
        }

        private static string Evaluate(string input, InterpreterScope scope)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
            var funcCall = expr as FunctionCallExpression;
            if (funcCall != null)
            {
                var nestedScope = new InterpreterScope(scope);
                nestedScope.Context = new AssignmentExpression(new VariableExpression("t"), expr);
                if (funcCall.Evaluate(scope, out expr))
                {
                    var builder = new StringBuilder();
                    expr.AppendString(builder);
                    return builder.ToString();
                }
            }

            var error = expr as ErrorExpression;
            if (error != null)
                return error.InnermostError.Message;

            return expr.ToString();
        }

        private string Evaluate(string input)
        {
            return Evaluate(input, InitializeScopeForDifficulty());
        }

        [Test]
        public void TestFieldNames()
        {
            var scope = InitializeScopeForLookupItem();
            Assert.That(Evaluate("field_map(item, (f, v) => f)", scope),
                Is.EqualTo("[\"id\", \"label\"]"));
        }

        [Test]
        public void TestFieldNamesIgnoresFunction()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) => f)"),
                Is.EqualTo("[\"none\", \"easy\", \"medium\", \"hard\"]"));
        }

        [Test]
        public void TestValueIds()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) => v.id)"),
                Is.EqualTo("[0, 1, 2, 3]"));
        }

        [Test]
        public void TestValueMap()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) => { v.id: v.label })"),
                Is.EqualTo("{0: \"\", 1: \"Easy\", 2: \"Medium\", 3: \"Hard\"}"));
        }

        [Test]
        public void TestFilter()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) { if v.id == 0 { return false } else { return v.label } })"),
                Is.EqualTo("[\"Easy\", \"Medium\", \"Hard\"]"));
        }

        [Test]
        public void TestFilterAll()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) => false)"),
                Is.EqualTo("[]"));
        }

        [Test]
        public void TestArray()
        {
            Assert.That(Evaluate("field_map([1], (f, v) => f))"),
                Is.EqualTo("Cannot convert array to class instance"));
        }

        [Test]
        public void TestNonIterable()
        {
            Assert.That(Evaluate("field_map(1, a => byte(a))"),
                Is.EqualTo("Cannot convert integer to class instance"));
        }

        [Test]
        public void TestPredicateWithNoParameters()
        {
            Assert.That(Evaluate("field_map(Difficulty, () => byte(0x1234))"),
                Is.EqualTo("predicate function must accept two parameters"));
        }

        [Test]
        public void TestPredicateWithOneParameter()
        {
            Assert.That(Evaluate("field_map(Difficulty, (a) => byte(0x1234))"),
                Is.EqualTo("predicate function must accept two parameters"));
        }

        [Test]
        public void TestPredicateWithExtraParameters()
        {
            Assert.That(Evaluate("field_map(Difficulty, (a, b, c) => byte(a))"),
                Is.EqualTo("predicate function must accept two parameters"));
        }

        [Test]
        public void TestMissingReturn()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) { if (v.id == 2) return v.label })"),
                Is.EqualTo("predicate did not return a value"));
        }

        [Test]
        public void TestErrorInPredicate()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) { return b })"),
                Is.EqualTo("Unknown variable: b"));
        }

        [Test]
        public void TestMergeKeyValueAndValue()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) { if (v.id == 2) return v.label else return {v.id: v.label} })"),
                Is.EqualTo("Cannot combine mapped and unmapped values"));
        }

        [Test]
        public void TestReturnEmptyDictionary()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) { return {} })"),
                Is.EqualTo("Dictionary returned from predicate must have a single entry (found 0)"));
        }

        [Test]
        public void TestReturnComplexDictionary()
        {
            Assert.That(Evaluate("field_map(Difficulty, (f, v) { return { 0:\"0\", 1:\"1\" } })"),
                Is.EqualTo("Dictionary returned from predicate must have a single entry (found 2)"));
        }
    }
}
