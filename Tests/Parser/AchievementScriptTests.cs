using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;

namespace RATools.Parser.Tests
{
    internal static class AchievementScriptTests
    {
        public static AchievementScriptInterpreter Parse(string input, bool expectedSuccess = true)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new AchievementScriptInterpreter();

            if (expectedSuccess)
            {
                if (!parser.Run(tokenizer))
                {
                    Assert.That(parser.ErrorMessage, Is.Null);
                    Assert.Fail("AchievementScriptInterpreter.Run failed with no error message");
                }
            }
            else
            {
                Assert.That(parser.Run(tokenizer), Is.False);
                Assert.That(parser.ErrorMessage, Is.Not.Null);
            }

            return parser;
        }

        public static InterpreterScope Evaluate(string script, string expectedError = null)
        {
            var groups = new ExpressionGroupCollection();
            groups.Scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());

            groups.Parse(Tokenizer.CreateTokenizer(script));

            foreach (var error in groups.Errors)
                Assert.Fail(error.Message);

            var interpreter = new AchievementScriptInterpreter();

            if (expectedError != null)
            {
                Assert.That(interpreter.Run(groups, null), Is.False);
                Assert.That(interpreter.ErrorMessage, Is.EqualTo(expectedError));
                return null;
            }

            if (!interpreter.Run(groups, null))
                Assert.Fail(interpreter.ErrorMessage);

            return groups.Scope;
        }

        public static string GetInnerErrorMessage(AchievementScriptInterpreter parser)
        {
            if (parser.Error == null)
                return null;

            var err = parser.Error;
            while (err.InnerError != null)
                err = err.InnerError;

            return string.Format("{0}:{1} {2}", err.Location.Start.Line, err.Location.Start.Column, err.Message);
        }
    }
}
