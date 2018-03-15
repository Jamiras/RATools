using RATools.Data;
using RATools.Parser.Internal;
using System.Diagnostics;

namespace RATools.Parser.Functions
{
    internal class LeaderboardFunction : FunctionDefinitionExpression
    {
        public LeaderboardFunction()
            : base("leaderboard")
        {
            Parameters.Add(new VariableExpression("title"));
            Parameters.Add(new VariableExpression("description"));
            Parameters.Add(new VariableExpression("start"));
            Parameters.Add(new VariableExpression("cancel"));
            Parameters.Add(new VariableExpression("submit"));
            Parameters.Add(new VariableExpression("value"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var leaderboard = new Leaderboard();

            var stringExpression = GetStringParameter(scope, "title", out result);
            if (stringExpression == null)
                return false;
            leaderboard.Title = stringExpression.Value;

            stringExpression = GetStringParameter(scope, "description", out result);
            if (stringExpression == null)
                return false;
            leaderboard.Description = stringExpression.Value;

            leaderboard.Start = ProcessTrigger(scope, "start", out result);
            if (leaderboard.Start == null)
                return false;

            leaderboard.Cancel = ProcessTrigger(scope, "cancel", out result);
            if (leaderboard.Cancel == null)
                return false;

            leaderboard.Submit = ProcessTrigger(scope, "submit", out result);
            if (leaderboard.Submit == null)
                return false;

            leaderboard.Value = ProcessValue(scope, "value", out result);
            if (leaderboard.Value == null)
                return false;

            var context = scope.GetContext<AchievementScriptContext>();
            Debug.Assert(context != null);
            context.Leaderboards.Add(leaderboard);
            return true;
        }

        private string ProcessTrigger(InterpreterScope scope, string parameter, out ExpressionBase result)
        {
            var expression = GetParameter(scope, parameter, out result);
            if (expression == null)
                return null;

            return TriggerBuilderContext.GetConditionString(expression, scope, out result);
        }

        private string ProcessValue(InterpreterScope scope, string parameter, out ExpressionBase result)
        {
            var expression = GetParameter(scope, parameter, out result);
            if (expression == null)
                return null;

            return TriggerBuilderContext.GetValueString(expression, scope, out result);
        }
    }
}
