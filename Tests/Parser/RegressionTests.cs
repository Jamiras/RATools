using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RATools.Test.Parser
{
    [TestFixture]
    class RegressionTests
    {
        class RegressionTestFactory
        {
            public static IEnumerable<string> Files
            {
                get
                {
                    var dir = Path.GetDirectoryName(typeof(RegressionTestFactory).Assembly.Location);
                    do
                    {
                        var parent = Directory.GetParent(dir);
                        dir = Path.Combine(dir, "Regressions");
                        if (Directory.Exists(dir))
                            break;

                        if (parent == null)
                        {
                            yield return "No scripts found.";
                            yield break;
                        }

                        dir = parent.FullName;
                    } while (true);

                    foreach (var file in Directory.EnumerateFiles(dir, "*.rascript"))
                        yield return file;
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestFactory), "Files")]
        public void RegressionTest(string scriptFileName)
        {
            if (scriptFileName == "No scripts found.")
                return;

            var outputFileName = scriptFileName.Substring(0, scriptFileName.Length - 9) + ".updated.txt";
            var expectedFileName = scriptFileName.Substring(0, scriptFileName.Length - 9) + ".txt";

            var interpreter = new AchievementScriptInterpreter();
            interpreter.Run(Tokenizer.CreateTokenizer(File.Open(scriptFileName, FileMode.Open)));

            Assert.IsNull(interpreter.ErrorMessage);

            var mockFileSystemService = new Mock<IFileSystemService>();
            mockFileSystemService.Setup(s => s.CreateFile(It.IsAny<string>())).Returns((string path) => File.Create(path));

            var localAchievements = new LocalAchievements(outputFileName, mockFileSystemService.Object);
            localAchievements.Title = Path.GetFileNameWithoutExtension(scriptFileName);
            foreach (var achievement in interpreter.Achievements)
                localAchievements.Replace(null, achievement);
            localAchievements.Commit("Author", null, null);

            if (interpreter.Leaderboards.Any())
            {
                using (var file = File.Open(outputFileName, FileMode.Append))
                {
                    using (var fileWriter = new StreamWriter(file))
                    {
                        fileWriter.WriteLine("=== Leaderboards ===");
                        foreach (var leaderboard in interpreter.Leaderboards)
                        {
                            fileWriter.Write("0:");
                            fileWriter.Write("\"STA:");
                            fileWriter.Write(leaderboard.Start);
                            fileWriter.Write("::CAN:");
                            fileWriter.Write(leaderboard.Cancel);
                            fileWriter.Write("::SUB:");
                            fileWriter.Write(leaderboard.Submit);
                            fileWriter.Write("::VAL:");
                            fileWriter.Write(leaderboard.Value);
                            fileWriter.Write("\":");
                            fileWriter.Write(leaderboard.Format);
                            fileWriter.Write(":\"");
                            fileWriter.Write(leaderboard.Title);
                            fileWriter.Write("\":\"");
                            fileWriter.Write(leaderboard.Description);
                            fileWriter.WriteLine("\"");
                        }
                    }
                }
            }

            if (!String.IsNullOrEmpty(interpreter.RichPresence))
            {
                using (var file = File.Open(outputFileName, FileMode.Append))
                {
                    using (var fileWriter = new StreamWriter(file))
                    {
                        fileWriter.WriteLine("=== Rich Presence ===");
                        fileWriter.WriteLine(interpreter.RichPresence);
                    }
                }
            }

            Assert.IsTrue(File.Exists(expectedFileName), expectedFileName + " not found");

            FileAssert.AreEqual(expectedFileName, outputFileName);

            File.Delete(outputFileName);
        }
    }
}
