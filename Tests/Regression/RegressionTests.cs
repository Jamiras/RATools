using Jamiras.Components;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;

namespace RATools.Tests.Regression
{
    [TestFixture]
    class RegressionTests
    {
        public const string NoScriptsError = "No scripts found.";

        public static string RegressionDir
        {
            get
            {
                if (regressionDir == null)
                {
                    var dir = Path.GetDirectoryName(typeof(RegressionTests).Assembly.Location);
                    do
                    {
                        var parent = Directory.GetParent(dir);
                        dir = Path.Combine(dir, "Regressions");
                        if (Directory.Exists(dir))
                        {
                            regressionDir = dir;
                            break;
                        }

                        if (parent == null)
                        {
                            regressionDir = NoScriptsError;
                            break;
                        }

                        dir = parent.FullName;
                    } while (true);
                }

                return regressionDir;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static string regressionDir = null;

        public static void AssertFileContents(string outputFileName, string expectedFileName)
        {
            var expectedFileContents = File.ReadAllText(expectedFileName);
            var outputFileContents = File.ReadAllText(outputFileName);

            if (expectedFileContents != outputFileContents) 
            {
                AssertContents(outputFileContents, expectedFileContents);

                // failed to find differing line, fallback to nunit assertion
                FileAssert.AreEqual(expectedFileName, outputFileName);
            }

            // file matched, delete temporary file
            File.Delete(outputFileName);
        }

        public static void AssertContents(string outputFileContents, string expectedFileContents)
        {
            if (expectedFileContents != outputFileContents)
            {
                // contents don't match. report first difference
                var expectedFileTokenizer = Tokenizer.CreateTokenizer(expectedFileContents);
                var outputFileTokenizer = Tokenizer.CreateTokenizer(outputFileContents);

                var line = 1;
                do
                {
                    var expectedFileLine = expectedFileTokenizer.ReadTo('\n').TrimRight();
                    var outputFileLine = outputFileTokenizer.ReadTo('\n').TrimRight();

                    if (expectedFileLine != outputFileLine)
                    {
                        var message = "Line " + line;
                        Assert.AreEqual(expectedFileLine.ToString(), outputFileLine.ToString(), message);
                    }

                    expectedFileTokenizer.Advance();
                    outputFileTokenizer.Advance();

                    ++line;
                } while (expectedFileTokenizer.NextChar != '\0' || outputFileTokenizer.NextChar != '\0');
            }
        }
    }
}
