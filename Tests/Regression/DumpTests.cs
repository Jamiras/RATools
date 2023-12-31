using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Services;
using RATools.ViewModels;
using System.Collections.Generic;
using System.IO;
using static RATools.ViewModels.NewScriptDialogViewModel;

namespace RATools.Tests.Regression
{
    [TestFixture]
    class DumpTests
    {
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
                    var dir = RegressionTests.RegressionDir;
                    if (dir == RegressionTests.NoScriptsError)
                    {
                        yield return new string[] { RegressionTests.NoScriptsError };
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
            if (patchDataFileName == RegressionTests.NoScriptsError)
                return;

            var baseDir = Path.Combine(RegressionTests.RegressionDir, "dumps");
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
            RegressionTests.AssertFileContents(outputFileName, expectedFileName);

            // file matched, delete temporary file
            File.Delete(outputFileName);
        }
    }
}
