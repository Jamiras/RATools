using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;

namespace RATools.Tests.Parser.Functions
{
    [TestFixture]
    class LeaderboardFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new LeaderboardFunction();
            Assert.That(def.Name.Name, Is.EqualTo("leaderboard"));
            Assert.That(def.Parameters.Count, Is.EqualTo(9));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("title"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("description"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("start"));
            Assert.That(def.Parameters.ElementAt(3).Name, Is.EqualTo("cancel"));
            Assert.That(def.Parameters.ElementAt(4).Name, Is.EqualTo("submit"));
            Assert.That(def.Parameters.ElementAt(5).Name, Is.EqualTo("value"));
            Assert.That(def.Parameters.ElementAt(6).Name, Is.EqualTo("format"));
            Assert.That(def.Parameters.ElementAt(7).Name, Is.EqualTo("lower_is_better"));
            Assert.That(def.Parameters.ElementAt(8).Name, Is.EqualTo("id"));

            Assert.That(def.DefaultParameters.Count(), Is.EqualTo(3));
            Assert.That(def.DefaultParameters["format"], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)def.DefaultParameters["format"]).Value, Is.EqualTo("value"));
            Assert.That(def.DefaultParameters["lower_is_better"], Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)def.DefaultParameters["lower_is_better"]).Value, Is.False);
            Assert.That(def.DefaultParameters["id"], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)def.DefaultParameters["id"]).Value, Is.EqualTo(0));
        }

        private Leaderboard Evaluate(string input, string expectedError = null)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var parser = new AchievementScriptInterpreter();

            if (expectedError == null)
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
                Assert.That(parser.ErrorMessage, Is.Not.Null.And.EqualTo(expectedError));
            }

            return parser.Leaderboards.FirstOrDefault();
        }

        [Test]
        public void TestSimple()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, byte(0x4567))");
            Assert.That(leaderboard.Title, Is.EqualTo("T"));
            Assert.That(leaderboard.Description, Is.EqualTo("D"));
            Assert.That(leaderboard.Start, Is.EqualTo("0xH001234=1"));
            Assert.That(leaderboard.Cancel, Is.EqualTo("0xH001234=2"));
            Assert.That(leaderboard.Submit, Is.EqualTo("0xH001234=3"));
            Assert.That(leaderboard.Value, Is.EqualTo("0xH004567"));
            Assert.That(leaderboard.Format, Is.EqualTo(ValueFormat.Value));
        }

        [Test]
        public void TestFormat()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, byte(0x4567), \"secs\")");
            Assert.That(leaderboard.Format, Is.EqualTo(ValueFormat.TimeSecs));
        }

        [Test]
        public void TestFormatUnknown()
        {
            Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, byte(0x4567), \"banana\")",
                "1:1 leaderboard call failed\r\n- 1:94 banana is not a supported leaderboard format");
        }

        [Test]
        public void TestLowerIsBetter()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, byte(0x4567))");
            Assert.That(leaderboard.LowerIsBetter, Is.False);

            leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, byte(0x4567), lower_is_better = true)");
            Assert.That(leaderboard.LowerIsBetter, Is.True);
        }

        [Test]
        public void TestValueMaxOf()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "max_of(byte(0x1234) * 3, byte(0x1235) * 5, byte(0x1236) * 8))");
            Assert.That(leaderboard.Value, Is.EqualTo("0xH001234*3$0xH001235*5$0xH001236*8"));
        }

        [Test]
        public void TestValueMeasured()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "measured(byte(0x1234) * 10 + byte(0x2345)))");
            Assert.That(leaderboard.Value, Is.EqualTo("A:0xH001234*10_M:0xH002345"));
        }

        [Test]
        public void TestValueMeasuredWhen()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "measured(byte(0x1234) * 10 + byte(0x2345), when=byte(0x3456) > 4))");
            Assert.That(leaderboard.Value, Is.EqualTo("A:0xH001234*10_M:0xH002345_Q:0xH003456>4"));
        }

        [Test]
        public void TestValueMeasuredWhenMaxOf()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "max_of(measured(byte(0x1234), when=byte(0x3456) > 4), " +
                       "measured(byte(0x1235), when=byte(0x3456) < 4), " +
                       "measured(byte(0x1236), when=byte(0x3456) == 4)))");
            Assert.That(leaderboard.Value, Is.EqualTo("M:0xH001234_Q:0xH003456>4$M:0xH001235_Q:0xH003456<4$M:0xH001236_Q:0xH003456=4"));
        }

        [Test]
        public void TestValueMeasuredAndNext()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "measured(repeated(10, byte(0x1234) == 6 && word(0x2345) == 1)))");
            Assert.That(leaderboard.Value, Is.EqualTo("N:0xH001234=6_M:0x 002345=1.10."));
        }

        [Test]
        public void TestValueMeasuredRaw()
        {
            Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "measured(byte(0x1234) * 10 + byte(0x2345), format=\"percent\"))",
                "1:1 leaderboard call failed\r\n" +
                "- 1:80 Value fields only support raw measured values");
        }

        [Test]
        public void TestValueMeasuredRawWithRepeatedWhen()
        {
            // never inside when
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "measured(byte(0x1234), when=repeated(10, byte(0x2345) == 10) && never(byte(0x2345) == 20)))");
            Assert.That(leaderboard.Value, Is.EqualTo("M:0xH001234_Z:0xH002345=20_Q:0xH002345=10.10."));

            // never outside measured
            leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "never(byte(0x2345) == 20) && measured(byte(0x1234), when=repeated(10, byte(0x2345) == 10)))");
            Assert.That(leaderboard.Value, Is.EqualTo("R:0xH002345=20_M:0xH001234_Q:0xH002345=10.10."));
        }

        [Test]
        public void TestValueMeasuredAddSourceScaled()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "measured(repeated(10, word(0x1234) / 4 + byte(0x2345) == 6)))");
            Assert.That(leaderboard.Value, Is.EqualTo("A:0x 001234/4_M:0xH002345=6.10."));
        }

        [Test]
        public void TestValueMeasuredAddAddressScaled()
        {
            var leaderboard = Evaluate("leaderboard(\"T\", \"D\", " +
                "byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, " +
                "measured(repeated(10, byte(0x2345 + word(0x1234) * 4) == 6)))");
            Assert.That(leaderboard.Value, Is.EqualTo("I:0x 001234*4_M:0xH002345=6.10."));
        }
    }
}