using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.Formats.Asn1.AsnWriter;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class AchievementSetFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new AchievementSetFunction();
            Assert.That(def.Name.Name, Is.EqualTo("achievement_set"));
            Assert.That(def.Parameters.Count, Is.EqualTo(4));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("title"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("type"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("id"));
            Assert.That(def.Parameters.ElementAt(3).Name, Is.EqualTo("game_id"));

            Assert.That(def.DefaultParameters.Count(), Is.EqualTo(3));
            Assert.That(def.DefaultParameters["type"], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)def.DefaultParameters["type"]).Value, Is.EqualTo("BONUS"));
            Assert.That(def.DefaultParameters["id"], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)def.DefaultParameters["id"]).Value, Is.EqualTo(0));
            Assert.That(def.DefaultParameters["game_id"], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)def.DefaultParameters["game_id"]).Value, Is.EqualTo(0));
        }

        private class AchievementSetFunctionHarness
        {
            public AchievementSetFunctionHarness()
            {
                Context = new AchievementScriptContext();
                Scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
                Scope.Context = Context;
            }

            public InterpreterScope Scope { get; private set; }
            public AchievementScriptContext Context { get; private set; }

            public int? Evaluate(string script, string expectedError = null)
            {
                AchievementScriptTests.Evaluate(script, Scope, expectedError);

                if (expectedError == null)
                {
                    var returnValue = Scope.GetVariable("set_id");
                    Assert.That(returnValue, Is.InstanceOf<IntegerConstantExpression>());
                    return ((IntegerConstantExpression)returnValue).Value;
                }

                return null;
            }

            public void AssertSet(int setId, int gameId, string title, AchievementSetType type)
            {
                var set = Context.Sets.FirstOrDefault(s => s.Id == setId);

                Assert.That(set, Is.Not.Null);
                Assert.That(set.Id, Is.EqualTo(setId));
                Assert.That(set.OwnerSetId, Is.EqualTo(setId));
                Assert.That(set.OwnerGameId, Is.EqualTo(gameId));
                Assert.That(set.Title, Is.EqualTo(title));
            }
        }

        [Test]
        public void TestDefaultParameters()
        {
            var harness = new AchievementSetFunctionHarness();
            var result = harness.Evaluate(
                "// #ID=1234\r\n" +
                "set_id = achievement_set(\"Game Bonus\")"
            );

            Assert.That(result, Is.EqualTo(11100001));
            Assert.That(harness.Context.Sets.Count, Is.EqualTo(1));
            harness.AssertSet(result.Value, 1234, "Game Bonus", AchievementSetType.Bonus);
        }

        [Test]
        [TestCase("BONUS", AchievementSetType.Bonus)]
        [TestCase("SPECIALTY", AchievementSetType.Specialty)]
        [TestCase("EXCLUSIVE", AchievementSetType.Exclusive)]
        public void TestType(string type, AchievementSetType expectedType)
        {
            var harness = new AchievementSetFunctionHarness();
            var result = harness.Evaluate(
                "// #ID=2222\r\n" +
                "set_id = achievement_set(\"Subset\", type=\"" + type + "\")"
            );

            Assert.That(result, Is.EqualTo(11100001));
            Assert.That(harness.Context.Sets.Count, Is.EqualTo(1));
            harness.AssertSet(result.Value, 2222, "Subset", expectedType);
        }

        [Test]
        public void TestTypeCore()
        {
            var harness = new AchievementSetFunctionHarness();
            harness.Evaluate(
                "// #ID=2222\r\n" +
                "set_id = achievement_set(\"Subset\", type=\"CORE\")",

                "2:10 achievement_set call failed\r\n" +
                "- 2:41 Cannot add CORE set. Only one is allowed, and is provided by default."
            );
        }

        [Test]
        public void TestTypeUnknown()
        {
            var harness = new AchievementSetFunctionHarness();
            harness.Evaluate(
                "// #ID=2222\r\n" +
                "set_id = achievement_set(\"Subset\", type=\"UNKNOWN\")",

                "2:10 achievement_set call failed\r\n" +
                "- 2:41 Unknown type: UNKNOWN"
            );
        }

        [Test]
        public void TestTypeBlank()
        {
            var harness = new AchievementSetFunctionHarness();
            harness.Evaluate(
                "// #ID=2222\r\n" +
                "set_id = achievement_set(\"Subset\", type=\"\")",

                "2:10 achievement_set call failed\r\n" +
                "- 2:41 Unknown type: "
            );
        }

        [Test]
        public void TestExistingById()
        {
            var harness = new AchievementSetFunctionHarness();
            harness.Context.Sets.Add(new AchievementSet
            {
                Id = 5555,
                OwnerSetId = 5555,
                OwnerGameId = 6666,
                Title = "Banana",
                Type = AchievementSetType.Specialty,
            });
            var result = harness.Evaluate(
                "// #ID=1234\r\n" +
                "set_id = achievement_set(\"Game Bonus\", id=5555)"
            );

            // existing set should be returned (unmodified)
            Assert.That(result, Is.EqualTo(5555));
            Assert.That(harness.Context.Sets.Count, Is.EqualTo(1));
            harness.AssertSet(5555, 6666, "Banana", AchievementSetType.Specialty);
        }

        [Test]
        public void TestExistingByIdNotFound()
        {
            var harness = new AchievementSetFunctionHarness();
            harness.Context.Sets.Add(new AchievementSet
            {
                Id = 5555,
                OwnerSetId = 5555,
                OwnerGameId = 6666,
                Title = "Banana",
                Type = AchievementSetType.Specialty,
            });
            harness.Evaluate(
                "// #ID=1234\r\n" +
                "set_id = achievement_set(\"Game Bonus\", id=6666)",

                "2:10 achievement_set call failed\r\n" +
                "- 2:43 Could not find set 6666"
            );
        }

        [Test]
        public void TestExistingByGameId()
        {
            var harness = new AchievementSetFunctionHarness();
            harness.Context.Sets.Add(new AchievementSet
            {
                Id = 5555,
                OwnerSetId = 5555,
                OwnerGameId = 6666,
                Title = "Banana",
                Type = AchievementSetType.Specialty,
            });
            var result = harness.Evaluate(
                "// #ID=1234\r\n" +
                "set_id = achievement_set(\"Game Bonus\", game_id=6666)"
            );

            // existing set should be returned (unmodified)
            Assert.That(result, Is.EqualTo(5555));
            Assert.That(harness.Context.Sets.Count, Is.EqualTo(1));
            harness.AssertSet(5555, 6666, "Banana", AchievementSetType.Specialty);
        }

        [Test]
        public void TestExistingByGameIdNotFound()
        {
            var harness = new AchievementSetFunctionHarness();
            harness.Context.Sets.Add(new AchievementSet
            {
                Id = 5555,
                OwnerSetId = 5555,
                OwnerGameId = 6666,
                Title = "Banana",
                Type = AchievementSetType.Specialty,
            });
            harness.Evaluate(
                "// #ID=1234\r\n" +
                "set_id = achievement_set(\"Game Bonus\", game_id=5555)",

                "2:10 achievement_set call failed\r\n" +
                "- 2:48 Could not find set for game 5555"
            );
        }

        [Test]
        public void TestExistingByTitle()
        {
            var harness = new AchievementSetFunctionHarness();
            harness.Context.Sets.Add(new AchievementSet
            {
                Id = 5555,
                OwnerSetId = 5555,
                OwnerGameId = 6666,
                Title = "Banana",
                Type = AchievementSetType.Specialty,
            });
            var result = harness.Evaluate(
                "// #ID=1234\r\n" +
                "set_id = achievement_set(\"Banana\")"
            );

            // existing set should be returned (unmodified)
            Assert.That(result, Is.EqualTo(5555));
            Assert.That(harness.Context.Sets.Count, Is.EqualTo(1));
            harness.AssertSet(5555, 6666, "Banana", AchievementSetType.Specialty);
        }

        [Test]
        public void TestMultiple()
        {
            var harness = new AchievementSetFunctionHarness();
            var result = harness.Evaluate(
                "// #ID=1234\r\n" +
                "set_id = achievement_set(\"Game Bonus\")" +
                "set_id2 = achievement_set(\"Glitch\")" +
                "set_id3 = achievement_set(\"Special\", type=\"SPECIALTY\")"
            );

            Assert.That(result, Is.EqualTo(11100001));
            Assert.That(harness.Context.Sets.Count, Is.EqualTo(3));
            harness.AssertSet(result.Value, 1234, "Game Bonus", AchievementSetType.Bonus);
            harness.AssertSet(result.Value + 1, 1234, "Glitch", AchievementSetType.Bonus);
            harness.AssertSet(result.Value + 2, 1234, "Special", AchievementSetType.Specialty);
        }
    }
}
