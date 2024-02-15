using Jamiras.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

// $ for file in *; do mv "${file}" "${file/.updated/}"; done

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

        private class RAScriptTestHarness : RAScriptCLI, IFileSystemService
        {
            public RAScriptTestHarness(string path)
                : base((IFileSystemService)null)
            {
                _writer = new StringWriter();
                OutputStream = ErrorStream = _writer;

                _inputFileName = path;
                InputFile = File.ReadAllText(path);

                _fileSystemService = this;
                _quiet = true;
            }

            private readonly StringWriter _writer;

            private class StringWriterStream : MemoryStream
            {
                public StringWriterStream(StringWriter writer)
                {
                    _writer = writer;
                }

                private bool _isClosed = false;

                public override void Close()
                {
                    if (!_isClosed)
                    {
                        _isClosed = true;

                        Position = 0;
                        using (var reader = new StreamReader(this))
                        {
                            _writer.Write(reader.ReadToEnd());
                        }
                    }

                    base.Close();
                }

                private readonly StringWriter _writer;
            }

            public string GenerateContents()
            {
                return _writer.ToString();
            }

            Stream IFileSystemService.CreateFile(string path)
            {
                if (path.EndsWith("-Rich.txt"))
                    _writer.WriteLine("=== Rich Presence ===");

                return new StringWriterStream(_writer);
            }

            Stream IFileSystemService.OpenFile(string path, OpenFileMode mode)
            {
                throw new NotImplementedException();
            }

            bool IFileSystemService.FileExists(string path)
            {
                return false;
            }

            bool IFileSystemService.DirectoryExists(string path)
            {
                throw new NotImplementedException();
            }

            bool IFileSystemService.CreateDirectory(string path)
            {
                throw new NotImplementedException();
            }

            long IFileSystemService.GetFileSize(string path)
            {
                throw new NotImplementedException();
            }

            DateTime IFileSystemService.GetFileLastModified(string path)
            {
                throw new NotImplementedException();
            }
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

            var harness = new RAScriptTestHarness(scriptPath);
            harness.Run();

            expectedFileName = Path.Combine(RegressionTests.RegressionDir, "results", expectedFileName + ".txt");
            var expectedFileContents = File.Exists(expectedFileName) ? File.ReadAllText(expectedFileName) : "[Results file not found]";

            var outputFileName = Path.ChangeExtension(expectedFileName, ".updated.txt");
            var outputFileContents = harness.GenerateContents();
            if (expectedFileContents != outputFileContents)
            {
                File.WriteAllText(outputFileName, outputFileContents);

                RegressionTests.AssertContents(expectedFileContents, outputFileContents);
            }

            // file matched, delete temporary file
            File.Delete(outputFileName);
        }
    }
}
