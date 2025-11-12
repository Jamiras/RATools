using Jamiras.Components;
using Jamiras.IO.Serialization;
using Jamiras.Services;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.Parser
{
    /// <summary>
    /// Class for interacting with the published assets file for a game.
    /// </summary>
    [DebuggerDisplay("PublishedAssets: {Title}")]
    public class PublishedAssets
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PublishedAssets"/> class.
        /// </summary>
        /// <param name="filename">The path to the 'XXX.json' file.</param>
        public PublishedAssets(string filename)
            : this(filename, ServiceRepository.Instance.FindService<IFileSystemService>())
        {
        }

        public PublishedAssets(string filename, IFileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;

            _sets = new List<AchievementSet>();
            _achievements = new List<Achievement>();
            _leaderboards = new List<Leaderboard>();
            RichPresence = null;

            _filename = filename;

            Read();
        }

        private readonly IFileSystemService _fileSystemService;
        private readonly string _filename;

        public int GameId { get; private set; }

        public int ConsoleId { get; private set; }

        /// <summary>
        /// Gets the title of the associated game.
        /// </summary>
        public string Title { get; set; }

        public string Filename { get { return _filename; } }

        /// <summary>
        /// Gets the achievement sets read from the file.
        /// </summary>
        public IEnumerable<AchievementSet> Sets
        {
            get { return _sets; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<AchievementSet> _sets;

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

        public RichPresence RichPresence { get; private set; }

        private readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private void Read()
        {
            _achievements.Clear();
            _leaderboards.Clear();
            RichPresence = null;

            using (var stream = _fileSystemService.OpenFile(_filename, OpenFileMode.Read))
            {
                if (stream == null)
                    return;

                var publishedData = new JsonObject(stream);
                Title = publishedData.GetField("Title").StringValue;
                ConsoleId = publishedData.GetField("ConsoleID").IntegerValue ?? 0;

                var sets = publishedData.GetField("Sets");
                if (sets.Type == JsonFieldType.ObjectArray)
                {
                    GameId = publishedData.GetField("GameId").IntegerValue.GetValueOrDefault();

                    ReadSets(sets);
                }
                else
                {
                    GameId = publishedData.GetField("ID").IntegerValue.GetValueOrDefault();
                    _sets.Add(new AchievementSet
                    {
                        OwnerGameId = GameId,
                        Title = Title,
                        Type = AchievementSetType.Core,
                    });

                    var publishedAchievements = publishedData.GetField("Achievements");
                    if (publishedAchievements.Type == JsonFieldType.ObjectArray)
                        ReadAchievements(publishedAchievements, GameId, 0);

                    var publishedLeaderboards = publishedData.GetField("Leaderboards");
                    if (publishedLeaderboards.Type == JsonFieldType.ObjectArray)
                        ReadLeaderboards(publishedLeaderboards, GameId, 0);
                }

                var publishedRichPresence = publishedData.GetField("RichPresencePatch");
                if (publishedRichPresence.Type == JsonFieldType.String)
                { 
                    RichPresence = new RichPresence
                    {
                        Script = publishedRichPresence.StringValue,
                        OwnerGameId = GameId,
                    };

                    var richPresenceGameId = publishedData.GetField("RichPresenceGameId");
                    if (richPresenceGameId.Type == JsonFieldType.Integer)
                        RichPresence.OwnerGameId = richPresenceGameId.IntegerValue.GetValueOrDefault();
                }
            }
        }

        private static AchievementSetType ConvertType(string type)
        {
            switch (type)
            {
                case "core": return AchievementSetType.Core;
                case "bonus": return AchievementSetType.Bonus;
                case "specialty": return AchievementSetType.Specialty;
                case "exclusive": return AchievementSetType.Exclusive;
                default: return AchievementSetType.None;
            }
        }

        private void ReadSets(JsonField sets)
        {
            foreach (var set in sets.ObjectArrayValue)
            {
                var gameId = set.GetField("GameId").IntegerValue.GetValueOrDefault();
                var setId = set.GetField("AchievementSetId").IntegerValue.GetValueOrDefault();

                _sets.Add(new AchievementSet
                {
                    Id = setId,
                    OwnerSetId = setId,
                    OwnerGameId = gameId,
                    Title = set.GetField("Title").StringValue ?? Title,
                    Type = ConvertType(set.GetField("Type").StringValue),
                });

                var publishedAchievements = set.GetField("Achievements");
                if (publishedAchievements.Type == JsonFieldType.ObjectArray)
                    ReadAchievements(publishedAchievements, gameId, setId);

                var publishedLeaderboards = set.GetField("Leaderboards");
                if (publishedLeaderboards.Type == JsonFieldType.ObjectArray)
                    ReadLeaderboards(publishedLeaderboards, gameId, setId);
            }
        }

        private void ReadAchievements(JsonField publishedAchievements, int gameId, int setId)
        {
            foreach (var publishedAchievement in publishedAchievements.ObjectArrayValue)
            {
                var builder = new AchievementBuilder();
                builder.Id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                builder.Title = publishedAchievement.GetField("Title").StringValue;
                builder.Description = publishedAchievement.GetField("Description").StringValue;
                builder.Points = publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault();
                builder.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;
                builder.ParseRequirements(Tokenizer.CreateTokenizer(publishedAchievement.GetField("MemAddr").StringValue));
                builder.Category = publishedAchievement.GetField("Flags").IntegerValue.GetValueOrDefault();

                var typeField = publishedAchievement.GetField("Type");
                if (!String.IsNullOrEmpty(typeField.StringValue))
                    builder.Type = Achievement.ParseType(typeField.StringValue);

                var builtAchievement = builder.ToAchievement();
                builtAchievement.Published = _unixEpoch.AddSeconds(publishedAchievement.GetField("Created").IntegerValue.GetValueOrDefault());
                builtAchievement.LastModified = _unixEpoch.AddSeconds(publishedAchievement.GetField("Modified").IntegerValue.GetValueOrDefault());
                builtAchievement.OwnerGameId = gameId;
                builtAchievement.OwnerSetId = setId;

                if (builtAchievement.Category == 5 || builtAchievement.Category == 3)
                    _achievements.Add(builtAchievement);
            }
        }

        private void ReadLeaderboards(JsonField publishedLeaderboards, int gameId, int setId)
        {
            foreach (var publishedLeaderboard in publishedLeaderboards.ObjectArrayValue)
            {
                var leaderboard = new Leaderboard();
                leaderboard.Id = publishedLeaderboard.GetField("ID").IntegerValue.GetValueOrDefault();
                leaderboard.Title = publishedLeaderboard.GetField("Title").StringValue;
                leaderboard.Description = publishedLeaderboard.GetField("Description").StringValue;
                leaderboard.Format = Leaderboard.ParseFormat(publishedLeaderboard.GetField("Format").StringValue);
                leaderboard.LowerIsBetter = publishedLeaderboard.GetField("LowerIsBetter").BooleanValue;

                var mem = publishedLeaderboard.GetField("Mem").StringValue;
                var tokenizer = Tokenizer.CreateTokenizer(mem);
                while (tokenizer.NextChar != '\0')
                {
                    var part = tokenizer.ReadTo("::");
                    if (part.StartsWith("STA:"))
                        leaderboard.Start = Trigger.Deserialize(part.Substring(4));
                    else if (part.StartsWith("CAN:"))
                        leaderboard.Cancel = Trigger.Deserialize(part.Substring(4));
                    else if (part.StartsWith("SUB:"))
                        leaderboard.Submit = Trigger.Deserialize(part.Substring(4));
                    else if (part.StartsWith("VAL:"))
                        leaderboard.Value = Value.Deserialize(part.Substring(4));

                    tokenizer.Advance(2);
                }

                if (leaderboard.Start == null)
                    leaderboard.Start = new Trigger();
                if (leaderboard.Cancel == null)
                    leaderboard.Cancel = new Trigger();
                if (leaderboard.Submit == null)
                    leaderboard.Submit = new Trigger();
                if (leaderboard.Value == null)
                    leaderboard.Value = new Value();

                leaderboard.OwnerGameId = gameId;
                leaderboard.OwnerSetId = setId;

                _leaderboards.Add(leaderboard);
            }
        }
    }
}
