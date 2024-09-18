using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;

namespace RATools.Parser.Tests.Functions
{
    internal static class FunctionTests
    {
        private static ExpressionBase CallFunction<T>(string input, InterpreterScope scope)
            where T : FunctionDefinitionExpression, new()
        {
            var functionDefinition = new T();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var functionCall = (FunctionCallExpression)expression;
            scope.Context = functionCall;

            Assert.That(functionCall.FunctionName.Name, Is.EqualTo(functionDefinition.Name.Name));

            ExpressionBase result;
            var parameterScope = functionCall.GetParameters(functionDefinition, scope, out result);
            if (result == null)
            {
                if (!functionDefinition.Evaluate(parameterScope, out result) && result is not ErrorExpression)
                    result = new ErrorExpression("Failure without ErrorExpression");
            }

            return result;
        }

        public static void Execute<T>(string input, InterpreterScope scope)
            where T : FunctionDefinitionExpression, new()
        {
            var result = CallFunction<T>(input, scope);

            var error = result as ErrorExpression;
            if (error != null)
                Assert.Fail(error.Message);

            Assert.That(result, Is.Null);
        }

        public static ErrorExpression AssertExecuteError<T>(string input, InterpreterScope scope, string expectedError)
            where T : FunctionDefinitionExpression, new()
        {
            var result = CallFunction<T>(input, scope);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());

            var error = (ErrorExpression)result;
            while (error.InnerError != null)
                error = error.InnerError;
            Assert.That(error.Message, Is.EqualTo(expectedError));

            return error;
        }

        public static ExpressionBase Evaluate<T>(string input, InterpreterScope scope)
            where T : FunctionDefinitionExpression, new()
        {
            var result = CallFunction<T>(input, scope);

            var error = result as ErrorExpression;
            if (error != null)
                Assert.Fail(error.Message);

            Assert.That(result, Is.Not.Null);
            return result;
        }

        public static ErrorExpression AssertEvaluateError<T>(string input, InterpreterScope scope, string expectedError)
            where T : FunctionDefinitionExpression, new()
        {
            var result = CallFunction<T>(input, scope);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());

            var error = (ErrorExpression)result;
            while (error.InnerError != null)
                error = error.InnerError;
            Assert.That(error.Message, Is.EqualTo(expectedError));

            return error;
        }
    }
}
