﻿using Jamiras.Components;
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
    class FunctionDefinitionExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new FunctionDefinitionExpression("func");
            expr.Parameters.Add(new VariableDefinitionExpression("p1"));
            expr.Parameters.Add(new VariableDefinitionExpression("p2"));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("function func(p1, p2)"));
            // NOTE: does not output Expressions block
        }

        [Test]
        public void TestAppendStringNoParameters()
        {
            var expr = new FunctionDefinitionExpression("func");

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("function func()"));
            // NOTE: does not output Expressions block
        }

        private static FunctionDefinitionExpression Parse(string input, string expectedError = null)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("function");
            var expr = UserFunctionDefinitionExpression.Parse(tokenizer);

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
                Assert.That(expr, Is.InstanceOf<FunctionDefinitionExpression>());
                return (FunctionDefinitionExpression)expr;
            }
        }

        [Test]
        public void TestParseNoParameters()
        {
            var expr = Parse("function func() { j = i }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(0));
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }

        [Test]
        public void TestParseParameters()
        {
            var expr = Parse("function func(i) { j = i }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(1));
            Assert.That(expr.Parameters.First().Name, Is.EqualTo("i"));
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }

        [Test]
        public void TestParseDefaultParameters()
        {
            var expr = Parse("function func(i, j = 2) { k = i + j }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(2));
            Assert.That(expr.Parameters.ElementAt(0).Name, Is.EqualTo("i"));
            Assert.That(expr.Parameters.ElementAt(1).Name, Is.EqualTo("j"));
            Assert.That(expr.DefaultParameters.ContainsKey("j"));
            Assert.That(expr.DefaultParameters["j"], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expr.DefaultParameters["j"]).Value, Is.EqualTo(2));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("k = i + j"));
        }

        [Test]
        public void TestParseNonDefaultAfterDefaultParameters()
        {
            Parse("function func(i, j = 2, k) { l = i + j + k }",
                "1:25 Non-default parameter k appears after default parameters");
        }

        [Test]
        public void TestParseDefaultParameterComplex()
        {
            var expr = Parse("function func(i, j = 4 * 3 + 2) { k = i + j }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(2));
            Assert.That(expr.Parameters.ElementAt(0).Name, Is.EqualTo("i"));
            Assert.That(expr.Parameters.ElementAt(1).Name, Is.EqualTo("j"));
            Assert.That(expr.DefaultParameters.ContainsKey("j"));
            Assert.That(expr.DefaultParameters["j"], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)expr.DefaultParameters["j"]).Value, Is.EqualTo(14));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("k = i + j"));
        }

        [Test]
        public void TestParseDefaultParameterMemoryReferece()
        {
            var expr = Parse("function func(i, j = byte(0x1234)) { k = i + j }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(2));
            Assert.That(expr.Parameters.ElementAt(0).Name, Is.EqualTo("i"));
            Assert.That(expr.Parameters.ElementAt(1).Name, Is.EqualTo("j"));
            Assert.That(expr.DefaultParameters.ContainsKey("j"));
            Assert.That(expr.DefaultParameters["j"], Is.InstanceOf<MemoryAccessorExpression>());

            var builder = new StringBuilder();
            expr.DefaultParameters["j"].AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("byte(0x001234)"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("k = i + j"));
        }

        [Test]
        public void TestParseDefaultParameterVariable()
        {
            var expr = Parse("function func(i, j = z) { k = i + j }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(2));
            Assert.That(expr.Parameters.ElementAt(0).Name, Is.EqualTo("i"));
            Assert.That(expr.Parameters.ElementAt(1).Name, Is.EqualTo("j"));
            Assert.That(expr.DefaultParameters.ContainsKey("j"));
            Assert.That(expr.DefaultParameters["j"], Is.InstanceOf<VariableExpression>());
            Assert.That(((VariableExpression)expr.DefaultParameters["j"]).Name, Is.EqualTo("z"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("k = i + j"));

            var scope = new InterpreterScope();
            scope.AddFunction(expr);
            scope.DefineVariable(new VariableDefinitionExpression("z"), new IntegerConstantExpression(2));
            scope.DefineVariable(new VariableDefinitionExpression("k"), new IntegerConstantExpression(0));

            ExpressionBase result;
            var funcCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(6) });
            Assert.That(funcCall.Invoke(scope, out result), Is.True);

            result = scope.GetVariable("k");
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(8));
        }

        [Test]
        public void TestParseDefaultParameterIndexedVariable()
        {
            var expr = Parse("function func(i, j = z[3]) { k = i + j }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(2));
            Assert.That(expr.Parameters.ElementAt(0).Name, Is.EqualTo("i"));
            Assert.That(expr.Parameters.ElementAt(1).Name, Is.EqualTo("j"));
            Assert.That(expr.DefaultParameters.ContainsKey("j"));
            Assert.That(expr.DefaultParameters["j"], Is.InstanceOf<IndexedVariableExpression>());

            var builder = new StringBuilder();
            expr.DefaultParameters["j"].AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("z[3]"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));
            builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("k = i + j"));

            var dict = new DictionaryExpression();
            dict.Add(new IntegerConstantExpression(3), new IntegerConstantExpression(2));

            var scope = new InterpreterScope();
            scope.AddFunction(expr);
            scope.DefineVariable(new VariableDefinitionExpression("z"), dict);
            scope.DefineVariable(new VariableDefinitionExpression("k"), new IntegerConstantExpression(0));

            ExpressionBase result;
            var funcCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(6) });
            Assert.That(funcCall.Invoke(scope, out result), Is.True);

            result = scope.GetVariable("k");
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(8));
        }

        [Test]
        [TestCase("function func(i, j = a => a * 2) { k = j(i) }")]
        [TestCase("function func(i, j = (a) => a * 2) { k = j(i) }")]
        [TestCase("function func(i, j = (a) { return a * 2 } ) { k = j(i) }")]
        public void TestParseDefaultParameterAnonymousFunction(string input)
        {
            var expr = Parse(input);
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(2));
            Assert.That(expr.Parameters.ElementAt(0).Name, Is.EqualTo("i"));
            Assert.That(expr.Parameters.ElementAt(1).Name, Is.EqualTo("j"));
            Assert.That(expr.DefaultParameters.ContainsKey("j"));
            Assert.That(expr.DefaultParameters["j"], Is.InstanceOf<FunctionDefinitionExpression>());
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var scope = new InterpreterScope();
            scope.DefineVariable(new VariableDefinitionExpression("k"), new IntegerConstantExpression(0));
            scope.AddFunction(expr);

            ExpressionBase result;
            var funcCall = new FunctionCallExpression("func", new ExpressionBase[] { new IntegerConstantExpression(6) });
            var parameterScope = funcCall.GetParameters(expr, scope, out result);
            Assert.That(result, Is.Null);

            Assert.That(funcCall.Invoke(parameterScope, out result), Is.True);

            result = scope.GetVariable("k");
            Assert.That(result, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)result).Value, Is.EqualTo(12));
        }

        [Test]
        public void TestParseDefaultParameterNonConstant()
        {
            Parse("function func(i, j = z + 2) { k = i + j }",
                "1:22 Default value for j is not constant");
        }

        [Test]
        public void TestParseDefaultParameterReferencingOtherParameter()
        {
            Parse("function func(i, j = i) { k = i + j }",
                "1:22 Default value for j is not constant");
        }

        [Test]
        public void TestParseShorthand()
        {
            var expr = Parse("function func(i) => i");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(1));
            Assert.That(expr.Parameters.First().Name, Is.EqualTo("i"));
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("return i"));
        }

        [Test]
        public void TestParseErrorInsideDefinition()
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer("function func() { j = }"));
            tokenizer.Match("function");
            var expr = UserFunctionDefinitionExpression.Parse(tokenizer);

            Assert.That(expr, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)expr).Message, Is.EqualTo("Unexpected end of script"));
        }

        [Test]
        public void TestParseErrorInsideDefinitionExpressionTokenizer()
        {
            var group = new ExpressionGroup();
            var tokenizer = new ExpressionTokenizer(Tokenizer.CreateTokenizer("function func() { j = }"), group);
            tokenizer.Match("function");
            var expr = UserFunctionDefinitionExpression.Parse(tokenizer);

            Assert.That(expr, Is.InstanceOf<FunctionDefinitionExpression>());
            Assert.That(group.ParseErrors.Count(), Is.EqualTo(2));
            Assert.That(group.ParseErrors.First().Message, Is.EqualTo("Unexpected character: }"));
        }

        [Test]
        public void TestParseErrorInsideShorthandDefinition()
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer("function func() => > 0"));
            tokenizer.Match("function");
            var expr = UserFunctionDefinitionExpression.Parse(tokenizer);

            ExpressionTests.AssertError(expr, "Unexpected character: >");
        }

        [Test]
        public void TestParseErrorInsideShorthandDefinitionExpressionTokenizer()
        {
            var group = new ExpressionGroup();
            var tokenizer = new ExpressionTokenizer(Tokenizer.CreateTokenizer("function func() => > 0"), group);
            tokenizer.Match("function");
            var expr = UserFunctionDefinitionExpression.Parse(tokenizer);

            Assert.That(expr, Is.InstanceOf<FunctionDefinitionExpression>());

            Assert.That(group.ParseErrors.Count(), Is.EqualTo(1));
            Assert.That(group.ParseErrors.First().Message, Is.EqualTo("Unexpected character: >"));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var expr = Parse("function func(i) => func2(i) + j");

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(4));
            Assert.That(nested.ElementAt(0), Is.InstanceOf<KeywordExpression>());            // function
            Assert.That(nested.ElementAt(1), Is.InstanceOf<VariableDefinitionExpression>()); // func
            Assert.That(nested.ElementAt(2), Is.InstanceOf<VariableDefinitionExpression>()); // i
            Assert.That(nested.ElementAt(3), Is.InstanceOf<ReturnExpression>());             // return func2(i) + j
        }

        [Test]
        public void TestGetDependencies()
        {
            var expr = Parse("function func(i) => func2(i) + j");

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

        [Test]
        public void TestGetModifications2()
        {
            var expr = Parse("function func(i) {\n" +
                             "  j = func2(i)\n" +
                             "  i = j + 3\n" +
                             "  return i\n" +
                             "}");

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(2));
            Assert.That(modifications.Contains("func"));
            Assert.That(modifications.Contains("j"));
            Assert.That(modifications.Contains("i"), Is.False); // parameter is self-contained
        }
    }
}
