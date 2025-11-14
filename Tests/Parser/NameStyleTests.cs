using NUnit.Framework;

namespace RATools.Parser.Tests
{
    [TestFixture]
    class NameStyleTests
    {
        [TestCase("a", NameStyle.SnakeCase, "a")]
        [TestCase("a", NameStyle.PascalCase, "A")]
        [TestCase("a", NameStyle.CamelCase, "a")]
        [TestCase("a", NameStyle.None, "a")]
        [TestCase("big ball", NameStyle.SnakeCase, "big_ball")]
        [TestCase("big ball", NameStyle.PascalCase, "BigBall")]
        [TestCase("big ball", NameStyle.CamelCase, "bigBall")]
        [TestCase("big ball", NameStyle.None, "bigball")]
        [TestCase("ONE TWO 3", NameStyle.SnakeCase, "one_two_3")]
        [TestCase("ONE TWO 3", NameStyle.PascalCase, "OneTwo3")]
        [TestCase("ONE TWO 3", NameStyle.CamelCase, "oneTwo3")]
        [TestCase("ONE TWO 3", NameStyle.None, "onetwo3")]
        [TestCase("12.3", NameStyle.SnakeCase, "_12_3")]
        [TestCase("12.3", NameStyle.PascalCase, "_12_3")]
        [TestCase("12.3", NameStyle.CamelCase, "_12_3")]
        [TestCase("12.3", NameStyle.None, "_123")]
        [TestCase("Bob's one-off", NameStyle.SnakeCase, "bobs_oneoff")]
        [TestCase("Bob's one-off", NameStyle.PascalCase, "BobsOneoff")]
        [TestCase("Bob's one-off", NameStyle.CamelCase, "bobsOneoff")]
        [TestCase("Bob's one-off", NameStyle.None, "bobsoneoff")]
        [TestCase("_italic fun_", NameStyle.SnakeCase, "italic_fun")]
        [TestCase("_italic fun_", NameStyle.PascalCase, "ItalicFun")]
        [TestCase("_italic fun_", NameStyle.CamelCase, "italicFun")]
        [TestCase("_italic fun_", NameStyle.None, "italicfun")]
        [TestCase("1-2 Time Attack", NameStyle.SnakeCase, "_1_2_time_attack")]
        [TestCase("1-2 Time Attack", NameStyle.PascalCase, "_1_2TimeAttack")]
        [TestCase("1-2 Time Attack", NameStyle.CamelCase, "_1_2TimeAttack")]
        [TestCase("1-2 Time Attack", NameStyle.None, "_12timeattack")]
        public void TestBuildName(string input, NameStyle nameStyle, string expected)
        {
            var output = nameStyle.BuildName(input);
            Assert.That(output, Is.EqualTo(expected));
        }
    }
}
