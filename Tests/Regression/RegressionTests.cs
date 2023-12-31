using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Parser;
using RATools.Services;
using RATools.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static RATools.ViewModels.NewScriptDialogViewModel;

namespace RATools.Tests.Parser.Regression
{
    [TestFixture]
    class RegressionTests
    {
        static string regressionDir = null;
        const string NoScriptsError = "No scripts found.";

        static string RegressionDir
        {
            get
            {
                if (regressionDir == null)
                {
                    var dir = Path.GetDirectoryName(typeof(RegressionTestScriptFactory).Assembly.Location);
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

        static void GetScriptFiles(List<string> files, string dir)
        {
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.rascript"))
                    files.Add(file);

                foreach (var subdir in Directory.EnumerateDirectories(dir))
                    GetScriptFiles(files, Path.Combine(dir, subdir));
            }
        }

        class RegressionTestScriptFactory
        {
            public static IEnumerable<string[]> Files
            {
                get
                {
                    var dir = RegressionDir;
                    if (dir == NoScriptsError)
                    {
                        yield return new string [] { NoScriptsError, "" };
                    }
                    else
                    {
                        var files = new List<string>();
                        GetScriptFiles(files, Path.Combine(dir, "scripts"));

                        if (!dir.EndsWith("\\"))
                            dir += "\\";

                        foreach (var file in files)
                        {
                            yield return new string[]
                            {
                                // For some reason, Test Explorer splits tests containing ")." into
                                // a separate namespace. Trick it by changing the parenthesis to a,
                                // curly bracket, which can't be part of a legal path
                                Path.GetFileNameWithoutExtension(file),
                                Path.GetFullPath(file).Replace(dir, "").Replace(").", "}.")
                            };
                        }
                    }
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "Files")]
        public void ScriptTest(string scriptFileName, string scriptPath)
        {
            if (scriptFileName == NoScriptsError)
                return;

            scriptPath = scriptPath.Replace("}.", ")."); // reverse the hack (see above)
            var parts = scriptPath.Split('\\');
            int i = 0;

            if (!Path.IsPathRooted(scriptPath))
            {
                scriptPath = Path.Combine(RegressionDir, scriptPath);
            }
            else
            {
                while (parts[i] != "Regressions")
                {
                    ++i;
                    Assert.That(i < parts.Length);
                }

                ++i;
                Assert.That(i < parts.Length);
            }

            if (parts[i] == "scripts")
                ++i;

            var expectedFileName = Path.GetFileNameWithoutExtension(scriptPath);
            while (i < parts.Length - 1)
            {
                expectedFileName += "-" + parts[i];
                ++i;
            }

            expectedFileName = Path.Combine(RegressionDir, "results", expectedFileName + ".txt");
            var outputFileName = Path.ChangeExtension(expectedFileName, ".updated.txt");

            var interpreter = new AchievementScriptInterpreter();
            var content = File.ReadAllText(scriptPath);
            interpreter.Run(Tokenizer.CreateTokenizer(content));

            if (!String.IsNullOrEmpty(interpreter.ErrorMessage))
            {
                using (var file = File.Open(outputFileName, FileMode.Create))
                {
                    using (var fileWriter = new StreamWriter(file))
                        fileWriter.Write(interpreter.ErrorMessage);
                }

                if (!File.Exists(expectedFileName))
                    Assert.IsNull(interpreter.ErrorMessage);
            }
            else
            {
                var mockFileSystemService = new Mock<IFileSystemService>();
                mockFileSystemService.Setup(s => s.CreateFile(It.IsAny<string>())).Returns((string path) => File.Create(path));

                var localAchievements = new LocalAssets(outputFileName, mockFileSystemService.Object);
                localAchievements.Title = Path.GetFileNameWithoutExtension(scriptFileName);
                foreach (var achievement in interpreter.Achievements)
                    localAchievements.Replace(null, achievement);
                foreach (var leaderboard in interpreter.Leaderboards)
                    localAchievements.Replace(null, leaderboard);
                localAchievements.Commit("Author", null, null);

                if (!String.IsNullOrEmpty(interpreter.RichPresence))
                {
                    using (var file = File.Open(outputFileName, FileMode.Append))
                    {
                        using (var fileWriter = new StreamWriter(file))
                        {
                            fileWriter.WriteLine("=== Rich Presence ===");

                            var minimumVersion = Double.Parse(localAchievements.Version, System.Globalization.NumberFormatInfo.InvariantInfo);
                            if (minimumVersion < 1.0)
                            {
                                interpreter.RichPresenceBuilder.DisableBuiltInMacros = true;

                                if (minimumVersion < 0.79)
                                    interpreter.RichPresenceBuilder.DisableLookupCollapsing = true;

                                fileWriter.WriteLine(interpreter.RichPresenceBuilder.ToString());
                            }
                            else
                            {
                                fileWriter.WriteLine(interpreter.RichPresence);
                            }
                        }
                    }
                }

                Assert.IsTrue(File.Exists(expectedFileName), expectedFileName + " not found");
            }

            AssertFileContents(outputFileName, expectedFileName);
        }

        private static void AssertFileContents(string outputFileName, string expectedFileName)
        {
            var expectedFileContents = File.ReadAllText(expectedFileName);
            var outputFileContents = File.ReadAllText(outputFileName);

            // file didn't match, report first difference
            if (expectedFileContents != outputFileContents)
            {
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

                        if (line == 1)
                        {
                            // if the first line is not a version, it's an error, dump the entire error
                            if (outputFileLine.Contains(':'))
                                message += "\n" + outputFileContents;
                        }

                        Assert.AreEqual(expectedFileLine.ToString(), outputFileLine.ToString(), message);
                    }

                    expectedFileTokenizer.Advance();
                    outputFileTokenizer.Advance();

                    ++line;
                } while (expectedFileTokenizer.NextChar != '\0' || outputFileTokenizer.NextChar != '\0');

                // failed to find differing line, fallback to nunit assertion
                FileAssert.AreEqual(expectedFileName, outputFileName);
            }

            // file matched, delete temporary file
            File.Delete(outputFileName);
        }

        static void GetCacheFiles(List<string> files, string dir)
        {
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                {
                    if (!file.Contains("-Notes"))
                        files.Add(file);
                }
            }
        }

        class RegressionTestDumpFactory
        {
            public static IEnumerable<string[]> Files
            {
                get
                {
                    var dir = RegressionDir;
                    if (dir == NoScriptsError)
                    {
                        yield return new string[] { NoScriptsError };
                    }
                    else
                    {
                        var files = new List<string>();
                        GetCacheFiles(files, Path.Combine(dir, "dumps", "RACache", "Data"));

                        foreach (var file in files)
                        {
                            yield return new string[]
                            {
                                Path.GetFileNameWithoutExtension(file)
                            };
                        }
                    }
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestDumpFactory), "Files")]
        public void DumpTest(string patchDataFileName)
        {
            if (patchDataFileName == NoScriptsError)
                return;

            var baseDir = Path.Combine(RegressionDir, "dumps");
            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.EmulatorDirectories).Returns(new string[] { baseDir });
            mockSettings.Setup(s => s.HexValues).Returns(false);
            ServiceRepository.Reset();
            ServiceRepository.Instance.RegisterInstance(mockSettings.Object);

            var mockDialogService = new Mock<IDialogService>();

            var mockLogger = new Mock<ILogger>();

            var mockFileSystem = new Mock<IFileSystemService>();
            mockFileSystem.Setup(s => s.FileExists(It.IsAny<string>())).Returns((string p) => File.Exists(p));
            mockFileSystem.Setup(s => s.OpenFile(It.IsAny<string>(), OpenFileMode.Read)).
                Returns((string p, OpenFileMode m) => File.Open(p, FileMode.Open, FileAccess.Read, FileShare.Read));

            var vmNewScript = new NewScriptDialogViewModel(mockSettings.Object, 
                mockDialogService.Object, mockLogger.Object, mockFileSystem.Object);
            vmNewScript.GameId.Value = int.Parse(patchDataFileName);
            vmNewScript.SearchCommand.Execute();

            vmNewScript.SelectedCodeNotesFilter = CodeNoteFilter.ForSelectedAssets;
            vmNewScript.SelectedFunctionNameStyle = FunctionNameStyle.SnakeCase;
            vmNewScript.SelectedNoteDump = NoteDump.All;
            vmNewScript.CheckAllCommand.Execute();

            var expectedFileName = Path.Combine(baseDir, vmNewScript.GameId.Value + ".rascript");
            var outputFileName = Path.ChangeExtension(expectedFileName, ".updated.rascript");

            using (var file = File.Open(outputFileName, FileMode.Create))
                vmNewScript.Dump(file);

            ServiceRepository.Reset();

            Assert.IsTrue(File.Exists(expectedFileName), expectedFileName + " not found");
            AssertFileContents(outputFileName, expectedFileName);

            // file matched, delete temporary file
            File.Delete(outputFileName);
        }
    }
}
