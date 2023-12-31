using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;

namespace RATools.Tests.Regression
{
    [TestFixture]
    class ScriptTests
    {
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
            public static IEnumerable<string[]> FilesNumeric
            {
                get { return GetFiles(s => Char.IsDigit(s[0])); }
            }

            public static IEnumerable<string[]> FilesPunctuation
            {
                get { return GetFiles(s => !Char.IsLetterOrDigit(s[0])); }
            }

            public static IEnumerable<string[]> FilesAB
            {
                get { return GetFiles(s => "AB".Contains(Char.ToUpper(s[0]))); }
            }

            public static IEnumerable<string[]> FilesCD
            {
                get { return GetFiles(s => "CD".Contains(Char.ToUpper(s[0]))); }
            }

            public static IEnumerable<string[]> FilesEFG
            {
                get { return GetFiles(s => "EFG".Contains(Char.ToUpper(s[0]))); }
            }

            public static IEnumerable<string[]> FilesHIJ
            {
                get { return GetFiles(s => "HIJ".Contains(Char.ToUpper(s[0]))); }
            }

            public static IEnumerable<string[]> FilesKLM
            {
                get { return GetFiles(s => "KLM".Contains(Char.ToUpper(s[0]))); }
            }

            public static IEnumerable<string[]> FilesNOPQ
            {
                get { return GetFiles(s => "NOPQ".Contains(Char.ToUpper(s[0]))); }
            }

            public static IEnumerable<string[]> FilesR
            {
                get { return GetFiles(s => Char.ToUpper(s[0]) == 'R'); }
            }

            public static IEnumerable<string[]> FilesS
            {
                get { return GetFiles(s => Char.ToUpper(s[0]) == 'S'); }
            }

            public static IEnumerable<string[]> FilesT
            {
                get { return GetFiles(s => Char.ToUpper(s[0]) == 'T'); }
            }

            public static IEnumerable<string[]> FilesUVWXYZ
            {
                get { return GetFiles(s => "UVWXYZ".Contains(Char.ToUpper(s[0]))); }
            }

            private static IEnumerable<string[]> GetFiles(Predicate<string> predicate)
            {
                var dir = RegressionTests.RegressionDir;
                if (dir == RegressionTests.NoScriptsError)
                {
                    yield return new string [] { RegressionTests.NoScriptsError, "" };
                }
                else
                {
                    var files = new List<string>();
                    GetScriptFiles(files, Path.Combine(dir, "scripts"));

                    if (!dir.EndsWith("\\"))
                        dir += "\\";

                    foreach (var file in files)
                    {
                        var filename = Path.GetFileNameWithoutExtension(file);
                        if (!predicate(filename))
                            continue;

                        yield return new string[]
                        {
                            // For some reason, Test Explorer splits tests containing ")." into
                            // a separate namespace. Trick it by changing the parenthesis to a,
                            // curly bracket, which can't be part of a legal path
                            filename,
                            Path.GetFullPath(file).Replace(dir, "").Replace(").", "}.")
                        };
                    }
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesNumeric")]
        public void Script0123456789(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesPunctuation")]
        public void Script_(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesAB")]
        public void ScriptAB(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesCD")]
        public void ScriptCD(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesEFG")]
        public void ScriptEFG(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesHIJ")]
        public void ScriptHIJ(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesKLM")]
        public void ScriptKLM(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesNOPQ")]
        public void ScriptNOPQ(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesR")]
        public void ScriptR(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesS")]
        public void ScriptS(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesT")]
        public void ScriptT(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        [Test]
        [TestCaseSource(typeof(RegressionTestScriptFactory), "FilesUVWXYZ")]
        public void ScriptUVWXYZ(string scriptFileName, string scriptPath)
        {
            TestScript(scriptFileName, scriptPath);
        }

        private void TestScript(string scriptFileName, string scriptPath)
        {
            if (scriptFileName == RegressionTests.NoScriptsError)
                return;

            scriptPath = scriptPath.Replace("}.", ")."); // reverse the hack (see above)
            var parts = scriptPath.Split('\\');
            int i = 0;

            if (!Path.IsPathRooted(scriptPath))
            {
                scriptPath = Path.Combine(RegressionTests.RegressionDir, scriptPath);
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

            expectedFileName = Path.Combine(RegressionTests.RegressionDir, "results", expectedFileName + ".txt");
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

            RegressionTests.AssertFileContents(outputFileName, expectedFileName);
        }
    }
}
