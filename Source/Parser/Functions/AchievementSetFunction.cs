using RATools.Data;
using RATools.Parser.Expressions;
using System.Diagnostics;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class AchievementSetFunction : FunctionDefinitionExpression
    {
        public AchievementSetFunction()
            : base("achievement_set")
        {
            Parameters.Add(new VariableDefinitionExpression("title"));

            Parameters.Add(new VariableDefinitionExpression("type"));
            DefaultParameters["type"] = new StringConstantExpression("BONUS");
            Parameters.Add(new VariableDefinitionExpression("id"));
            DefaultParameters["id"] = new IntegerConstantExpression(0);
            Parameters.Add(new VariableDefinitionExpression("game_id"));
            DefaultParameters["game_id"] = new IntegerConstantExpression(0);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            AchievementSet set = null;

            var context = scope.GetContext<AchievementScriptContext>();
            Debug.Assert(context != null);

            var title = GetStringParameter(scope, "title", out result);
            if (title == null)
                return false;

            var gameId = GetIntegerParameter(scope, "game_id", out result);

            var id = GetIntegerParameter(scope, "id", out result);
            if (id != null && id.Value > 0)
                set = context.Sets.FirstOrDefault(s => s.Id == id.Value);

            if (set == null)
            {
                if (gameId != null && gameId.Value > 0)
                    set = context.Sets.FirstOrDefault(s => s.OwnerGameId == gameId.Value);

                if (set == null)
                    set = context.Sets.FirstOrDefault(s => s.Title == title.Value);
            }

            if (set == null)
            {
                set = new AchievementSet
                {
                    Id = id?.Value ?? 0,
                    OwnerSetId = id?.Value ?? 0,
                    OwnerGameId = gameId?.Value ?? 0,
                    Title = title.Value,
                };

                if ((set.Id != 0 || set.OwnerGameId != 0) &&
                    context.Sets.Any(s => s.Id < AssetBase.FirstLocalId / 10))
                {
                    if (set.Id != 0)
                        result = new ErrorExpression("Could not find set " + set.Id, id);
                    else
                        result = new ErrorExpression("Could not find set for game " + set.OwnerGameId, gameId);
                    return false;
                }

                if (set.Id == 0)
                    set.Id = set.OwnerSetId = AssetBase.FirstLocalId / 10 + context.Sets.Count + 1;
                if (set.OwnerGameId == 0)
                    set.OwnerGameId = context.GameId;

                var type = GetStringParameter(scope, "type", out result);
                if (type != null)
                {
                    switch (type.Value)
                    {
                        case "BONUS": set.Type = AchievementSetType.Bonus; break;
                        case "SPECIALTY": set.Type = AchievementSetType.Specialty; break;
                        case "EXCLUSIVE": set.Type = AchievementSetType.Exclusive; break;

                        case "CORE":
                            result = new ErrorExpression("Cannot add CORE set. Only one is allowed, and is provided by default.", type);
                            return false;

                        default:
                            result = new ErrorExpression("Unknown type: " + type.Value, type);
                            return false;
                    }
                }

                context.Sets.Add(set);
            }

            result = new IntegerConstantExpression(set.Id);
            return true;
        }
    }
}
