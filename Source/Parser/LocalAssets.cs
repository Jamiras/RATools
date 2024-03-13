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

        public LocalAssets(string filename, IFileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
            _extraLines = new List<string>();

            _achievements = new List<Achievement>();
            _leaderboards = new List<Leaderboard>();
            _notes = new Dictionary<uint, string>();
            RichPresence = null;

            _filename = filename;
            Version = Data.Version.MinimumVersion;

            Read();
        }

        private readonly IFileSystemService _fileSystemService;
        private readonly string _filename;
        private readonly List<string> _extraLines;
        private DateTime _lastSave;

        public SoftwareVersion Version { get; private set; }

        /// <summary>
        /// Gets the title of the associated game.
        /// </summary>
        public string Title { get; set; }

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
        public Dictionary<uint, string> Notes
        {
            get { return _notes; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Dictionary<uint, string> _notes;

        public RichPresence RichPresence { get; private set; }

        private static DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private void Read()
        {
            if (_fileSystemService.FileExists(_filename))
            {
                _lastSave = _fileSystemService.GetFileLastModified(_filename);

                using (var reader = new StreamReader(_fileSystemService.OpenFile(_filename, OpenFileMode.Read)))
                {
                    SoftwareVersion version;
                    if (SoftwareVersion.TryParse(reader.ReadLine(), out version))
                        Version = version;

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

            var type = tokenizer.ReadTo(':'); // type
            achievement.Type = Achievement.ParseType(type.Trim().ToString());
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

            leaderboard.Start = Trigger.Deserialize(tokenizer.ReadQuotedString().ToString());
            tokenizer.Advance();

            leaderboard.Cancel = Trigger.Deserialize(tokenizer.ReadQuotedString().ToString());
            tokenizer.Advance();

            leaderboard.Submit = Trigger.Deserialize(tokenizer.ReadQuotedString().ToString());
            tokenizer.Advance();

            leaderboard.Value = Value.Deserialize(tokenizer.ReadQuotedString().ToString());
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
                uint address;
                addressString = addressString.SubToken(2);
                if (UInt32.TryParse(addressString.ToString(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out address))
                    _notes[address] = note.ToString();
            }
        }

        public enum LocalAssetChange
        {
            None = 0,
            Modified,
            Added,
            Removed
        }

        public void MergeExternalChanges(Action<AssetBase, LocalAssetChange> changeHandler)
        {
            var fileDate = _fileSystemService.GetFileLastModified(_filename);
            if (fileDate - _lastSave < TimeSpan.FromSeconds(15))
                return;

            // store old values
            var achievements = _achievements;
            _achievements = new List<Achievement>();

            var leaderboards = _leaderboards;
            _leaderboards = new List<Leaderboard>();

            var richPresence = RichPresence;

            // local notes are always fully reconstructed
            _notes.Clear();

            // fetch new values
            Read();

            // merge achievements
            for (int i = achievements.Count -1; i >= 0; i--)
            {
                var existingAchievement = achievements[i];
                if (!_achievements.Any(a => a.Id == existingAchievement.Id))
                {
                    achievements.RemoveAt(i);
                    changeHandler(existingAchievement, LocalAssetChange.Removed);
                }
            }

            foreach (var externalAchievement in _achievements)
            {
                var achievement = achievements.FirstOrDefault(a => a.Id == externalAchievement.Id);
                if (achievement == null)
                {
                    // new external achievement - probably deleted by us, but something externally changed
                    changeHandler(externalAchievement, LocalAssetChange.Added);
                    achievements.Add(externalAchievement);
                }
                else
                {
                    if (achievement.Points != externalAchievement.Points ||
                        achievement.Category != externalAchievement.Category ||
                        achievement.BadgeName != externalAchievement.BadgeName ||
                        achievement.Title != externalAchievement.Title ||
                        achievement.Description != externalAchievement.Description ||
                        !AchievementBuilder.AreRequirementsSame(achievement, externalAchievement))
                    {
                        var index = achievements.IndexOf(achievement);
                        achievements[index] = externalAchievement;
                        changeHandler(externalAchievement, LocalAssetChange.Modified);
                    }
                }
            }
            _achievements = achievements;

            // merge leaderboards
            for (int i = leaderboards.Count - 1; i >= 0; i--)
            {
                var existingLeaderboard = leaderboards[i];
                if (!_leaderboards.Any(a => a.Id == existingLeaderboard.Id))
                {
                    leaderboards.RemoveAt(i);
                    changeHandler(existingLeaderboard, LocalAssetChange.Removed);
                }
            }

            foreach (var externalLeaderboard in _leaderboards)
            {
                var leaderboard = leaderboards.FirstOrDefault(a => a.Id == externalLeaderboard.Id);
                if (leaderboard == null)
                {
                    // new external leaderboard
                    changeHandler(externalLeaderboard, LocalAssetChange.Added);
                    leaderboards.Add(externalLeaderboard);
                }
                else
                {
                    if (leaderboard.Format != externalLeaderboard.Format ||
                        leaderboard.LowerIsBetter != externalLeaderboard.LowerIsBetter ||
                        leaderboard.Title != externalLeaderboard.Title ||
                        leaderboard.Description != externalLeaderboard.Description ||
                        leaderboard.Start != externalLeaderboard.Start ||
                        leaderboard.Submit != externalLeaderboard.Submit ||
                        leaderboard.Cancel != externalLeaderboard.Cancel ||
                        leaderboard.Value != externalLeaderboard.Value)
                    {
                        var index = leaderboards.IndexOf(leaderboard);
                        leaderboards[index] = externalLeaderboard;
                        changeHandler(externalLeaderboard, LocalAssetChange.Modified);
                    }
                }
            }
            _leaderboards = leaderboards;

            // merge rich presence
            if (richPresence == null)
            {
                if (RichPresence != null)
                    changeHandler(richPresence, LocalAssetChange.Added);
            }
            else if (RichPresence == null)
            {
                changeHandler(richPresence, LocalAssetChange.Removed);
            }
            else if (RichPresence.Script != richPresence.Script)
            {
                richPresence.Script = RichPresence.Script;
                RichPresence = richPresence;
                changeHandler(richPresence, LocalAssetChange.Modified);
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
            if (str.IndexOfAny(new char[] { '"', '\\', '\n', '\r', '\t' }) == -1)
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
        public void Commit(string author, StringBuilder warning, SerializationContext serializationContext, List<AssetBase> assetsToValidate)
        {
            SoftwareVersion minimumVersion = Version;

            foreach (var achievement in _achievements)
            {
                var achievementMinimumVersion = AchievementBuilder.GetMinimumVersion(achievement);
                minimumVersion = minimumVersion.OrNewer(achievementMinimumVersion);
            }

            foreach (var leaderboard in _leaderboards)
            {
                var leaderboardMinimumVersion = LeaderboardBuilder.GetMinimumVersion(leaderboard);
                minimumVersion = minimumVersion.OrNewer(leaderboardMinimumVersion);
            }

            if (_notes.Count > 0)
                minimumVersion = minimumVersion.OrNewer(Data.Version._1_1);

            if (minimumVersion > serializationContext.MinimumVersion)
                serializationContext = serializationContext.WithVersion(minimumVersion);

            using (var writer = new StreamWriter(_fileSystemService.CreateFile(_filename)))
            {
                writer.WriteLine(minimumVersion);
                writer.WriteLine(Title);

                foreach (var note in _notes)
                {
                    writer.Write("N0:0x{0}:\"", serializationContext.FormatAddress((uint)note.Key));
                    WriteEscaped(writer, note.Value);
                    writer.WriteLine("\"");
                }

                foreach (var achievement in _achievements)
                    WriteAchievement(writer, author, achievement, serializationContext, (assetsToValidate == null || assetsToValidate.Contains(achievement)) ? warning : null);

                foreach (var leaderboard in _leaderboards)
                    WriteLeaderboard(writer, leaderboard, serializationContext, (assetsToValidate == null || assetsToValidate.Contains(leaderboard)) ? warning : null);

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

        private static void WriteAchievement(StreamWriter writer, string author, Achievement achievement, SerializationContext serializationContext, StringBuilder warning)
        {
            writer.Write(achievement.Id);
            writer.Write(":\"");

            var requirements = AchievementBuilder.SerializeRequirements(achievement, serializationContext);
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

            writer.Write(" : :"); // discontinued features

            writer.Write(Achievement.GetTypeString(achievement.Type));
            writer.Write(':');

            writer.Write(author); // author
            writer.Write(':');

            writer.Write(achievement.Points);
            writer.Write(':');

            writer.Write("0:0:0:0:"); // created, modified, upvotes, downvotes

            writer.Write(achievement.BadgeName);
            writer.WriteLine();
        }

        private static void WriteLeaderboard(StreamWriter writer, Leaderboard leaderboard, SerializationContext serializationContext, StringBuilder warning)
        {
            writer.Write('L');
            writer.Write(leaderboard.Id);
            writer.Write(":\"");

            var start = leaderboard.Start.Serialize(serializationContext);
            var cancel = leaderboard.Cancel.Serialize(serializationContext);
            var submit = leaderboard.Submit.Serialize(serializationContext);
            var value = leaderboard.Value.Serialize(serializationContext);

            if (warning != null)
            {
                var totalLength = start.Length + cancel.Length + submit.Length + value.Length + 4 * 4 + 2 * 3;
                if (totalLength > LeaderboardMaxLength)
                {
                    warning.AppendFormat("Leaderboard \"{0}\" exceeds serialized limit ({1}/{2})", leaderboard.Title, totalLength, LeaderboardMaxLength);
                    warning.AppendLine();
                }
            }

            writer.Write(start);
            writer.Write("\":\"");

            writer.Write(cancel);
            writer.Write("\":\"");

            writer.Write(submit);
            writer.Write("\":\"");

            writer.Write(value);
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
