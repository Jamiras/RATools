using Jamiras.Components;
using Jamiras.Services;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    /// <summary>
    /// Class for interacting with the local assets file for a game.
    /// </summary>
    [DebuggerDisplay("LocalAssets: {Title}")]
    public class LocalAssets
    {
        const int AchievementMaxLength = 65535;
        const int LeaderboardMaxLength = 65535;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalAssets"/> class.
        /// </summary>
        /// <param name="filename">The path to the 'XXX-User.txt' file.</param>
        public LocalAssets(string filename)
            : this(filename, ServiceRepository.Instance.FindService<IFileSystemService>())
        {
        }

        internal LocalAssets(string filename, IFileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
            _extraLines = new List<string>();

            _achievements = new List<Achievement>();
            _leaderboards = new List<Leaderboard>();
            _notes = new Dictionary<int, string>();
            RichPresence = null;

            _filename = filename;
            Version = "0.030";

            Read();
        }

        private readonly IFileSystemService _fileSystemService;
        private readonly string _filename;
        private readonly List<string> _extraLines;

        public string Version { get; private set; }

        /// <summary>
        /// Gets the title of the associated game.
        /// </summary>
        public string Title { get; internal set; }

        /// <summary>
        /// Gets the achievements read from the file.
        /// </summary>
        public IEnumerable<Achievement> Achievements
        {
            get { return _achievements; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Achievement> _achievements;

        /// <summary>
        /// Gets the leaderboards read from the file.
        /// </summary>
        public IEnumerable<Leaderboard> Leaderboards
        {
            get { return _leaderboards; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Leaderboard> _leaderboards;

        /// <summary>
        /// Gets the notes read from the file.
        /// </summary>
        public Dictionary<int, string> Notes
        {
            get { return _notes; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Dictionary<int, string> _notes;

        public RichPresence RichPresence { get; private set; }

        private static DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private void Read()
        {
            if (_fileSystemService.FileExists(_filename))
            {
                using (var reader = new StreamReader(_fileSystemService.OpenFile(_filename, OpenFileMode.Read)))
                {
                    Version = reader.ReadLine();
                    Title = reader.ReadLine();

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var tokenizer = Tokenizer.CreateTokenizer(line);

                        if (Char.IsDigit(tokenizer.NextChar))
                        {
                            ReadAchievement(tokenizer);
                        }
                        else if (tokenizer.NextChar == 'L')
                        {
                            tokenizer.Advance();
                            ReadLeaderboard(tokenizer);
                        }
                        else if (tokenizer.NextChar == 'N')
                        {
                            tokenizer.Advance(3); // 'N0:'
                            ReadNote(tokenizer);
                        }
                        else
                        {
                            _extraLines.Add(line);
                        }
                    }
                }
            }

            var richPresenceFilename = _filename.Replace("-User.txt", "-Rich.txt");
            if (_fileSystemService.FileExists(richPresenceFilename))
            {
                RichPresence = new RichPresence();

                using (var reader = new StreamReader(_fileSystemService.OpenFile(richPresenceFilename, OpenFileMode.Read)))
                {
                    RichPresence.Script = reader.ReadToEnd();
                }
            }
            else
            {
                RichPresence = null;
            }
        }

        private void ReadAchievement(Tokenizer tokenizer)
        {
            var achievement = new AchievementBuilder();

            var part = tokenizer.ReadTo(':'); // id

            int num;
            if (Int32.TryParse(part.ToString(), out num))
                achievement.Id = num;
            tokenizer.Advance();

            if (tokenizer.NextChar == '"')
            {
                var requirements = tokenizer.ReadQuotedString();
                achievement.ParseRequirements(Tokenizer.CreateTokenizer(requirements));
            }
            else
            {
                achievement.ParseRequirements(tokenizer);
            }
            tokenizer.Advance();

            if (tokenizer.NextChar == '"')
                achievement.Title = tokenizer.ReadQuotedString().ToString();
            else
                achievement.Title = tokenizer.ReadTo(':').ToString();
            tokenizer.Advance();

            if (tokenizer.NextChar == '"')
                achievement.Description = tokenizer.ReadQuotedString().ToString();
            else
                achievement.Description = tokenizer.ReadTo(':').ToString();
            tokenizer.Advance();

            tokenizer.ReadTo(':'); // deprecated
            tokenizer.Advance();

            tokenizer.ReadTo(':'); // deprecated
            tokenizer.Advance();

            tokenizer.ReadTo(':'); // deprecated
            tokenizer.Advance();

            tokenizer.ReadTo(':'); // author
            tokenizer.Advance();

            part = tokenizer.ReadTo(':'); // points
            if (Int32.TryParse(part.ToString(), out num))
                achievement.Points = num;
            tokenizer.Advance();

            var published = tokenizer.ReadTo(':'); // created timestamp
            tokenizer.Advance();

            var updated = tokenizer.ReadTo(':'); // updated timestamp
            tokenizer.Advance();

            tokenizer.ReadTo(':'); // upvotes
            tokenizer.Advance();

            tokenizer.ReadTo(':'); // downvotes
            tokenizer.Advance();

            if (tokenizer.NextChar == '"')
                achievement.BadgeName = tokenizer.ReadQuotedString().ToString();
            else
                achievement.BadgeName = tokenizer.ReadTo(':').ToString();
            if (achievement.BadgeName.EndsWith("_lock"))
                achievement.BadgeName.Remove(achievement.BadgeName.Length - 5);

            var builtAchievement = achievement.ToAchievement();
            if (published != "0" && Int32.TryParse(published.ToString(), out num))
                builtAchievement.Published = _unixEpoch.AddSeconds(num);
            if (updated != "0" && Int32.TryParse(updated.ToString(), out num))
                builtAchievement.LastModified = _unixEpoch.AddSeconds(num);

            _achievements.Add(builtAchievement);
        }

        private void ReadLeaderboard(Tokenizer tokenizer)
        {
            var leaderboard = new Leaderboard();
            var part = tokenizer.ReadTo(':'); // id

            int num;
            if (Int32.TryParse(part.ToString(), out num))
                leaderboard.Id = num;
            tokenizer.Advance();

            leaderboard.Start = tokenizer.ReadQuotedString().ToString();
            tokenizer.Advance();

            leaderboard.Cancel = tokenizer.ReadQuotedString().ToString();
            tokenizer.Advance();

            leaderboard.Submit = tokenizer.ReadQuotedString().ToString();
            tokenizer.Advance();

            leaderboard.Value = tokenizer.ReadQuotedString().ToString();
            tokenizer.Advance();

            leaderboard.Format = Leaderboard.ParseFormat(tokenizer.ReadIdentifier().ToString());
            tokenizer.Advance();

            if (tokenizer.NextChar == '"')
                leaderboard.Title = tokenizer.ReadQuotedString().ToString();
            else
                leaderboard.Title = tokenizer.ReadTo(':').ToString();
            tokenizer.Advance();

            if (tokenizer.NextChar == '"')
                leaderboard.Description = tokenizer.ReadQuotedString().ToString();
            else
                leaderboard.Description = tokenizer.ReadTo(':').ToString();
            tokenizer.Advance();

            part = tokenizer.ReadTo(':');
            if (Int32.TryParse(part.ToString(), out num))
                leaderboard.LowerIsBetter = (num != 0);

            _leaderboards.Add(leaderboard);
        }

        private void ReadNote(Tokenizer tokenizer)
        {
            var addressString = tokenizer.ReadTo(':');
            tokenizer.Advance();
            var note = tokenizer.ReadQuotedString();

            if (addressString.StartsWith("0x"))
            {
                int address;
                addressString = addressString.SubToken(2);
                if (Int32.TryParse(addressString.ToString(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out address))
                    _notes[address] = note.ToString();
            }
        }

        /// <summary>
        /// Replaces an achievement in the list with a new version, or appends a new achievement to the list.
        /// </summary>
        /// <param name="existingAchievement">The existing achievement.</param>
        /// <param name="newAchievement">The new achievement, <c>null</c> to remove.</param>
        /// <returns>The previous version if the item was replaced, <c>null</c> if the <paramref name="existingAchievement"/> was not in the list.</returns>
        public Achievement Replace(Achievement existingAchievement, Achievement newAchievement)
        {
            var index = _achievements.IndexOf(existingAchievement);
            if (index == -1)
            {
                if (newAchievement != null)
                    _achievements.Add(newAchievement);
                return null;
            }

            var previousAchievement = _achievements[index];
            if (newAchievement == null)
                _achievements.RemoveAt(index);
            else
                _achievements[index] = newAchievement;

            return previousAchievement;
        }

        /// <summary>
        /// Replaces a leaderboard in the list with a new version, or appends a new leaderboard to the list.
        /// </summary>
        /// <param name="existingLeaderboard">The existing leaderboard.</param>
        /// <param name="newLeaderboard">The new leaderboard, <c>null</c> to remove.</param>
        /// <returns>The previous version if the item was replaced, <c>null</c> if the <paramref name="existingLeaderboard"/> was not in the list.</returns>
        public Leaderboard Replace(Leaderboard existingLeaderboard, Leaderboard newLeaderboard)
        {
            var index = _leaderboards.IndexOf(existingLeaderboard);
            if (index == -1)
            {
                if (newLeaderboard != null)
                    _leaderboards.Add(newLeaderboard);
                return null;
            }

            var previousLeaderboard = _leaderboards[index];
            if (newLeaderboard == null)
                _leaderboards.RemoveAt(index);
            else
                _leaderboards[index] = newLeaderboard;

            return previousLeaderboard;
        }

        /// <summary>
        /// Replaces the rich presence with a new version
        /// </summary>
        /// <param name="existingRichPresence">The existing rich presence.</param>
        /// <param name="newRichPresence">The new rich presence, <c>null</c> to remove.</param>
        /// <returns>The previous version if the item was replaced, <c>null</c> if the <paramref name="existingRichPresence"/> was not in the list.</returns>
        public RichPresence Replace(RichPresence existingRichPresence, RichPresence newRichPresence)
        {
            var previousRichPresence = RichPresence;
            RichPresence = newRichPresence;

            if (previousRichPresence != null && ReferenceEquals(previousRichPresence, existingRichPresence))
                return previousRichPresence;

            return null;
        }

        private static void WriteEscaped(StreamWriter writer, string str)
        {
            if (str.IndexOf('"') == -1 && str.IndexOf('\\') == -1)
            {
                writer.Write(str);
                return;
            }

            foreach (var c in str)
            {
                if (c == '"')
                    writer.Write("\\\"");
                else if (c == '\\')
                    writer.Write("\\\\");
                else if (c == '\n')
                    writer.Write("\\n");
                else if (c == '\r')
                    writer.Write("\\r");
                else if (c == '\t')
                    writer.Write("\\t");
                else
                    writer.Write(c);
            }
        }

        /// <summary>
        /// Commits the asset list back to the 'XXX-User.txt' file.
        /// </summary>
        public void Commit(string author, StringBuilder warning, List<AssetBase> assetsToValidate)
        {
            double version = 0.30;

            foreach (var achievement in _achievements)
            {
                var achievementMinimumVersion = AchievementBuilder.GetMinimumVersion(achievement);
                if (achievementMinimumVersion > version)
                    version = achievementMinimumVersion;
            }

            foreach (var leaderboard in _leaderboards)
            {
                var leaderboardMinimumVersion = AchievementBuilder.GetMinimumVersion(leaderboard);
                if (leaderboardMinimumVersion > version)
                    version = leaderboardMinimumVersion;
            }

            if (_notes.Count > 0 && version < 1.1)
                Version = "1.0.5";
            else if (version > 0.30)
                Version = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2}", version);

            using (var writer = new StreamWriter(_fileSystemService.CreateFile(_filename)))
            {
                writer.WriteLine(Version);
                writer.WriteLine(Title);

                foreach (var note in _notes)
                {
                    writer.Write("N0:0x{0:x6}:\"", note.Key);
                    WriteEscaped(writer, note.Value);
                    writer.WriteLine("\"");
                }

                foreach (var achievement in _achievements)
                    WriteAchievement(writer, author, achievement, (assetsToValidate == null || assetsToValidate.Contains(achievement)) ? warning : null);

                foreach (var leaderboard in _leaderboards)
                    WriteLeaderboard(writer, leaderboard, (assetsToValidate == null || assetsToValidate.Contains(leaderboard)) ? warning : null);

                foreach (var line in _extraLines)
                    writer.WriteLine(line);
            }

            if (assetsToValidate == null || assetsToValidate.Any(a => a is RichPresence))
            {
                var richPresenceFilename = _filename.Replace("-User.txt", "-Rich.txt");

                if (richPresenceFilename == _filename)
                {
                    // don't overwrite the achievements file with rich presence data.
                    // this shouldn't happen outside of the regression tests.
                }
                else if (RichPresence != null && !String.IsNullOrEmpty(RichPresence.Script))
                {
                    if (warning != null)
                    {
                        if (RichPresence.Script.Length > RichPresence.ScriptMaxLength)
                        {
                            warning.AppendFormat("Rich Presence exceeds serialized limit ({1}/{2})", RichPresence.Script.Length, RichPresence.ScriptMaxLength);
                            warning.AppendLine();
                        }
                    }

                    using (var writer = new StreamWriter(_fileSystemService.CreateFile(richPresenceFilename)))
                    {
                        writer.Write(RichPresence.Script);
                    }
                }
                else
                {
                    File.Delete(richPresenceFilename);
                    // TODO: _fileSystemService.DeleteFile(richPresenceFilename);
                }
            }
        }

        private void WriteAchievement(StreamWriter writer, string author, Achievement achievement, StringBuilder warning)
        {
            writer.Write(achievement.Id);
            writer.Write(":\"");

            var requirements = AchievementBuilder.SerializeRequirements(achievement);
            if (requirements.Length > AchievementMaxLength && warning != null)
            {
                warning.AppendFormat("Achievement \"{0}\" exceeds serialized limit ({1}/{2})", achievement.Title, requirements.Length, AchievementMaxLength);
                warning.AppendLine();
            }

            writer.Write(requirements);
            writer.Write("\":\"");

            WriteEscaped(writer, achievement.Title);
            writer.Write("\":\"");

            WriteEscaped(writer, achievement.Description);
            writer.Write("\":");

            writer.Write(" : : :"); // discontinued features

            writer.Write(author); // author
            writer.Write(':');

            writer.Write(achievement.Points);
            writer.Write(':');

            writer.Write("0:0:0:0:"); // created, modified, upvotes, downvotes

            writer.Write(achievement.BadgeName);
            writer.WriteLine();
        }

        private void WriteLeaderboard(StreamWriter writer, Leaderboard leaderboard, StringBuilder warning)
        {
            writer.Write('L');
            writer.Write(leaderboard.Id);
            writer.Write(":\"");

            if (warning != null)
            {
                var totalLength = leaderboard.Start.Length + leaderboard.Cancel.Length + leaderboard.Submit.Length + leaderboard.Value.Length + 4 * 4 + 2 * 3;
                if (totalLength > LeaderboardMaxLength)
                {
                    warning.AppendFormat("Leaderboard \"{0}\" exceeds serialized limit ({1}/{2})", leaderboard.Title, totalLength, LeaderboardMaxLength);
                    warning.AppendLine();
                }
            }

            writer.Write(leaderboard.Start);
            writer.Write("\":\"");

            writer.Write(leaderboard.Cancel);
            writer.Write("\":\"");

            writer.Write(leaderboard.Submit);
            writer.Write("\":\"");

            writer.Write(leaderboard.Value);
            writer.Write("\":");

            writer.Write(Leaderboard.GetFormatString(leaderboard.Format));
            writer.Write(":\"");

            WriteEscaped(writer, leaderboard.Title);
            writer.Write("\":\"");

            WriteEscaped(writer, leaderboard.Description);
            writer.Write("\":");

            writer.Write(leaderboard.LowerIsBetter ? '1' : '0');

            writer.WriteLine();
        }
    }
}
