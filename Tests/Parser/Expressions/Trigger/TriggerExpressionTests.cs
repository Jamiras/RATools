using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Collections.Generic;

namespace RATools.Tests.Parser.Expressions.Trigger
{
    internal static class TriggerExpressionTests
    {
        public static InterpreterScope CreateScope()
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();
            return scope;
        }

        public static ExpressionBase Parse(string input, InterpreterScope scope)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            ExpressionBase result;
            if (!expr.ReplaceVariables(scope, out result))
            {
                var error = result as ErrorExpression;
                if (error != null && error.InnerError != null)
                    Assert.Fail(error.InnerError.ToString());

                Assert.Fail(result.ToString());
            }

            return result;
        }

        public static ExpressionBase Parse(string input)
        {
            return Parse(input, CreateScope());
        }

        public static T Parse<T>(string input)
            where T : ExpressionBase
        {
            var result = Parse(input);
            Assert.That(result, Is.InstanceOf<T>());
            return (T)result;
        }

        public static T Parse<T>(string input, InterpreterScope scope)
            where T : ExpressionBase
        {
            var result = Parse(input, scope);
            Assert.That(result, Is.InstanceOf<T>());
            return (T)result;
        }

        public static void AssertParseError(string input, string message)
        {
            AssertParseError(input, CreateScope(), message);
        }

        public static void AssertParseError(string input, InterpreterScope scope, string message)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            ExpressionBase result;
            if (expr.ReplaceVariables(scope, out result))
                Assert.Fail(result.ToString());

            ExpressionTests.AssertError(result, message);
        }

        public static void AssertBuildTriggerError(string input, string message)
        {
            var clause = Parse<RequirementExpressionBase>(input);

            var requirements = new List<Requirement>();
            var context = new TriggerBuilderContext() { Trigger = requirements };

            var error = clause.BuildTrigger(context);
            ExpressionTests.AssertError(error, message);
        }

        public static string Serialize(ITriggerExpression expression)
        {
            var requirements = new List<Requirement>();
            var context = new TriggerBuilderContext() { Trigger = requirements };

            var result = expression.BuildTrigger(context);
            Assert.That(result, Is.Null);

            var builder = new AchievementBuilder();
            foreach (var requirement in requirements)
                builder.CoreRequirements.Add(requirement);

            return builder.SerializeRequirements();
        }

        public static void AssertSerialize(ITriggerExpression expression, string expected)
        {
            Assert.That(Serialize(expression), Is.EqualTo(expected));
        }

        public static string SerializeValue(ITriggerExpression expression)
        {
            var requirements = new List<Requirement>();
            var context = new ValueBuilderContext() { Trigger = requirements };

            var result = expression.BuildTrigger(context);
            Assert.That(result, Is.Null);

            var builder = new AchievementBuilder();
            foreach (var requirement in requirements)
                builder.CoreRequirements.Add(requirement);

            return builder.SerializeRequirements();
        }

        public static void AssertSerializeValue(ITriggerExpression expression, string expected)
        {
            Assert.That(SerializeValue(expression), Is.EqualTo(expected));
        }

        public static string SerializeAchievement(RequirementExpressionBase expression)
        {
            var builder = new ScriptInterpreterAchievementBuilder();
            var error = builder.PopulateFromExpression(expression);
            Assert.That(error, Is.Null);

            return builder.SerializeRequirements();
        }

        public static void AssertSerializeAchievement(RequirementExpressionBase expression, string expected)
        {
            Assert.That(SerializeAchievement(expression), Is.EqualTo(expected));
        }
    }
}
