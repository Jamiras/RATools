using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
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
                Assert.Fail(result.ToString());

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
    }
}
