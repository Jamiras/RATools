using Jamiras.Components;
using Jamiras.Services;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions;
using System.Reflection;

namespace RATools
{
    public class RAScriptCLI
    {
        public RAScriptCLI()
            : this(ServiceRepository.Instance.FindService<IFileSystemService>())
        {

        }

        protected RAScriptCLI(IFileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;

            OutputStream = Console.Out;
            ErrorStream = Console.Error;
            InputFile = "";
            OutputDirectory = ".";
            Author = "Author";
        }

        protected TextWriter OutputStream { get; set; }
        protected TextWriter ErrorStream { get; set; }
        protected string InputFile { get; set; }
        protected string OutputDirectory { get; set; }
        protected string Author { get; set; }
        protected IFileSystemService _fileSystemService;

        protected string _inputFileName = "";
        protected bool _verbose = false;
        protected bool _quiet = false;

        public void Usage()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
            if (version.EndsWith(".0"))
                version = version.Substring(0, version.Length - 2);

            OutputStream.WriteLine("rascript-cli " + version);
            OutputStream.WriteLine("========================");
            OutputStream.WriteLine("Usage: rascript-cli [-v] [-q] [-i script] [-o outdir] [-a author]");
            OutputStream.WriteLine();
            OutputStream.WriteLine("  -v            (optional) enable verbose messages");
            OutputStream.WriteLine("  -q            (optional) disable all messages");
            OutputStream.WriteLine("  -i script     specifies the input file to process");
            OutputStream.WriteLine("  -o outdir     (optional) specifies the output directory to write to [default: current directory]");
            OutputStream.WriteLine("                will append RACache\\Data to the specified directory if an RACache\\Data subdirectoy exists");
            OutputStream.WriteLine("  -a author     (optional) specifies the author to attribute the achievements to [default: Author]");
        }

        public ReturnCode ProcessArgs(string[] args)
        {
            if (args.Length == 0)
            {
                Usage();
                return ReturnCode.Success;
            }

            if (args.Any(a => a == "-v"))
                _verbose = true;

            if (args.Any(a => a == "-q"))
            {
                _verbose = false;
                _quiet = true;
            }

            int i = 0;
            while (i < args.Length)
            {
                if (args[i] == "-v" || args[i] == "-q")
                {
                    // verbose and quiet handled above
                }
                else if (args[i] == "-i")
                {
                    var inputFile = args[++i];
                    if (!File.Exists(inputFile))
                    {
                        ErrorStream.WriteLine("File not found: " + inputFile);
                        return ReturnCode.InvalidParmeter;
                    }
                    InputFile = File.ReadAllText(inputFile);
                    _inputFileName = Path.GetFileName(inputFile);

                    if (_verbose)
                        OutputStream.WriteLine("Read {0} bytes from {1}", InputFile.Length, _inputFileName);

                }
                else if (args[i] == "-o")
                {
                    OutputDirectory = args[++i];
                    if (!Directory.Exists(OutputDirectory))
                    {
                        ErrorStream.WriteLine("Directory not found: " + OutputDirectory);
                        return ReturnCode.InvalidParmeter;
                    }

                    var subDirectory = Path.Combine(OutputDirectory, "RACache", "Data");
                    if (Directory.Exists(subDirectory))
                        OutputDirectory = subDirectory;
                }
                else if (args[i] == "-a")
                {
                    Author = args[++i];
                }
                else
                {
                    ErrorStream.WriteLine("Unknown parameters: " + args[i]);
                    return ReturnCode.InvalidParmeter;
                }

                ++i;
            }

            if (_verbose)
                OutputStream.WriteLine("Output directory set to: " + OutputDirectory);

            return ReturnCode.Proceed;
        }

        private class ErrorReporter
        {
            public ErrorReporter(string input)
            {
                _input = input;
                _tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
                _currentLineText = "";
                _currentLine = 0;
            }

            private readonly string _input;
            private PositionalTokenizer _tokenizer;
            private string _currentLineText;
            private int _currentLine;

            public void PrintError(TextWriter stream, ErrorExpression error)
            {
                while (error.InnerError != null)
                {
                    stream.Write("{0}:{1}: ", error.Location.Front.Line, error.Location.Front.Column);
                    stream.WriteLine(error.Message);
                    error = error.InnerError;
                }

                if (_currentLine > error.Location.Front.Line)
                {
                    _tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(_input));
                    _currentLine = 0;
                }

                if (_currentLine < error.Location.Front.Line)
                {
                    while (_tokenizer.Line < error.Location.Front.Line && _tokenizer.NextChar != '\0')
                    {
                        _tokenizer.ReadTo('\n');
                        _tokenizer.Advance();
                    }

                    _currentLine = _tokenizer.Line;
                    _currentLineText = _tokenizer.ReadTo('\n').TrimRight().ToString();
                    _tokenizer.Advance();
                }

                stream.Write("{0}:{1}: ", error.Location.Front.Line, error.Location.Front.Column);
                Console.ForegroundColor = ConsoleColor.DarkRed;
                stream.Write("error: ");
                Console.ResetColor();
                stream.WriteLine(error.Message);

                stream.WriteLine(_currentLineText.Replace('\t', ' '));
                if (error.Location.Front.Column > 0)
                {
                    stream.Write(new String(' ', error.Location.Front.Column - 1));
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    stream.Write('^');
                    if (error.Location.Front.Line == error.Location.Back.Line)
                    {
                        var distance = error.Location.Back.Column - error.Location.Front.Column;
                        if (distance > 0)
                            stream.Write(new String('~', distance));
                    }
                    Console.ResetColor();
                    stream.WriteLine();
                }
            }
        }

        public ReturnCode Run()
        {
            if (_inputFileName == "")
            {
                ErrorStream.WriteLine("No input file specified");
                return ReturnCode.InvalidParmeter;
            }

            // ===== load and parse the script =====
            var interpreter = new AchievementScriptInterpreter();
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(InputFile));
            var groups = interpreter.Parse(tokenizer);

            if (_verbose)
                OutputStream.WriteLine("Read {0} lines from {1}", tokenizer.Line, _inputFileName);

            if (groups.Errors.Any())
            {
                if (_verbose)
                    OutputStream.WriteLine("Found {0} parse errors", groups.Errors.Count());

                var reporter = new ErrorReporter(InputFile);

                foreach (var error in groups.Errors)
                    reporter.PrintError(ErrorStream, error);

                return ReturnCode.ParseError;
            }

            // ===== run the script =====
            interpreter.Run(groups, null);

            if (groups.HasEvaluationErrors)
            {
                if (_verbose)
                    OutputStream.WriteLine("Found {0} evaluation errors", groups.Errors.Count());

                var reporter = new ErrorReporter(InputFile);

                foreach (var error in groups.Errors)
                    reporter.PrintError(ErrorStream, error);

                return ReturnCode.EvaluationError;
            }

            // ===== load the published assets file =====
            var publishedAssetsFilename = Path.Combine(OutputDirectory, String.Format("{0}.json", interpreter.GameId));
            var publishedAssets = new PublishedAssets(publishedAssetsFilename, _fileSystemService);

            if (_verbose && File.Exists(publishedAssetsFilename))
            {
                OutputStream.WriteLine("Read {0} achievements and {1} leaderboards from {2}.json",
                    publishedAssets.Achievements.Count(), publishedAssets.Leaderboards.Count(), interpreter.GameId);
            }

            // ===== update IDs and badges for generated assets that don't specify IDs or badges and have since been published =====
            foreach (var achievement in publishedAssets.Achievements)
            {
                var generatedAchievement = Achievement.FindMergeAchievement(interpreter.Achievements, achievement);
                if (generatedAchievement != null)
                {
                    if (generatedAchievement.Id == 0)
                        generatedAchievement.Id = achievement.Id;
                    if (String.IsNullOrEmpty(generatedAchievement.BadgeName) || generatedAchievement.BadgeName == "0")
                        generatedAchievement.BadgeName = achievement.BadgeName;
                }
            }

            // ===== load the local assets file =====
            var outputFileName = Path.Combine(OutputDirectory, String.Format("{0}-User.txt", interpreter.GameId));
            var localAssets = new LocalAssets(outputFileName, _fileSystemService);
            localAssets.Title = interpreter.GameTitle ?? publishedAssets.Title ?? Path.GetFileNameWithoutExtension(_inputFileName);

            if (_verbose)
            {
                OutputStream.WriteLine("Read {0} achievements and {1} leaderboards from {2}-User.txt",
                    localAssets.Achievements.Count(), localAssets.Leaderboards.Count(), interpreter.GameId);
            }

            // ==== merge the generated assets into the local assets collection =====
            var nextLocalId = AssetBase.FirstLocalId;
            var unmatchedLocalAchievements = new List<Achievement>(localAssets.Achievements);
            foreach (var achievement in unmatchedLocalAchievements)
                nextLocalId = Math.Max(nextLocalId, achievement.Id + 1);

            foreach (var generatedAchievement in interpreter.Achievements)
            {
                var localAchievement = Achievement.FindMergeAchievement(unmatchedLocalAchievements, generatedAchievement);
                if (localAchievement != null)
                {
                    // generated achievement already exists in local file. keep the previous ID
                    unmatchedLocalAchievements.Remove(localAchievement);
                    generatedAchievement.Id = localAchievement.Id;
                }

                if (generatedAchievement.Id == 0)
                    generatedAchievement.Id = nextLocalId++;

                localAssets.Replace(localAchievement, generatedAchievement);
            }

            var unmatchedLeaderboards = new List<Leaderboard>(localAssets.Leaderboards);
            foreach (var generatedLeaderboard in interpreter.Leaderboards)
            {
                var localLeaderboard = Leaderboard.FindMergeLeaderboard(unmatchedLeaderboards, generatedLeaderboard);
                if (localLeaderboard != null)
                {
                    unmatchedLeaderboards.Remove(localLeaderboard);
                    generatedLeaderboard.Id = localLeaderboard.Id;
                }
                localAssets.Replace(localLeaderboard, generatedLeaderboard);
            }

            // ===== write the local assets file =====
            localAssets.Commit(Author, null, interpreter.SerializationContext, null);

            if (!_quiet)
            {
                OutputStream.WriteLine("Wrote {0} achievements and {1} leaderboards to {2}-User.txt",
                    localAssets.Achievements.Count(), localAssets.Leaderboards.Count(), interpreter.GameId);
            }

            // ===== write the rich presence file =====
            if (!String.IsNullOrEmpty(interpreter.RichPresence))
            {
                outputFileName = Path.Combine(OutputDirectory, String.Format("{0}-Rich.txt", interpreter.GameId));
                using (var stream = _fileSystemService.CreateFile(outputFileName))
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(interpreter.RichPresence);
                    }
                }

                if (!_quiet)
                    OutputStream.WriteLine("Wrote {0} bytes to {1}-Rich.txt", interpreter.RichPresence.Length, interpreter.GameId);
            }

            return ReturnCode.Success;
        }
    }

    public enum ReturnCode
    {
        Proceed = -1,
        Success = 0,
        InvalidParmeter = 1,
        ParseError = 2,
        EvaluationError = 3,
    }
}
