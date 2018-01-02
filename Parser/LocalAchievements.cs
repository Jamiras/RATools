using Jamiras.Components;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RATools.Parser
{
    /// <summary>
    /// Class for interacting with the local achievements file for a game.
    /// </summary>
    [DebuggerDisplay("LocalAchievements: {_title}")]
    public class LocalAchievements
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalAchievements"/> class.
        /// </summary>
        /// <param name="filename">The path to the 'XXX-User.txt' file.</param>
        public LocalAchievements(string filename)
            : this(filename, ServiceRepository.Instance.FindService<IFileSystemService>())
        {
        }

        internal LocalAchievements(string filename, IFileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
            _achievements = new List<Achievement>();
            _filename = filename;
            _version = "0.030";

            Read();
        }

        private readonly IFileSystemService _fileSystemService;
        private readonly string _filename;
        private string _version;

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

        private void Read()
        {
            if (!_fileSystemService.FileExists(_filename))
                return;

            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            using (var reader = new StreamReader(_fileSystemService.OpenFile(_filename, OpenFileMode.Read)))
            {
                _version = reader.ReadLine();
                Title = reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var tokenizer = Tokenizer.CreateTokenizer(line);
                    var achievement = new AchievementBuilder();

                    var part = tokenizer.ReadTo(':'); // id

                    if (part.StartsWith("L")) // ignore leaderboards in N64 file
                        continue;

                    int num;
                    if (Int32.TryParse(part.ToString(), out num))
                        achievement.Id = num;
                    tokenizer.Advance();

                    achievement.ParseRequirements(tokenizer);
                    tokenizer.Advance();

                    achievement.Title = tokenizer.ReadTo(':').ToString();
                    tokenizer.Advance();

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

                    achievement.BadgeName = tokenizer.ReadTo(':').ToString();
                    if (achievement.BadgeName.EndsWith("_lock"))
                        achievement.BadgeName.Remove(achievement.BadgeName.Length - 5);

                    var builtAchievement = achievement.ToAchievement();
                    if (published != "0" && Int32.TryParse(published.ToString(), out num))
                        builtAchievement.Published = unixEpoch.AddSeconds(num);
                    if (updated != "0" && Int32.TryParse(updated.ToString(), out num))
                        builtAchievement.LastModified = unixEpoch.AddSeconds(num);

                    _achievements.Add(builtAchievement);
                }
            }
        }

        /// <summary>
        /// Replaces the an achievement in the list with a new version, or appends a new achievement to the list.
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
        /// Commits the achivement list back to the 'XXX-User.txt' file.
        /// </summary>
        public void Commit(string author)
        {
            var warning = new StringBuilder();

            using (var writer = new StreamWriter(_fileSystemService.CreateFile(_filename)))
            {
                writer.WriteLine(_version);
                writer.WriteLine(Title);

                foreach (var achievement in _achievements)
                {
                    writer.Write(0); // id always 0 in local file
                    writer.Write(':');

                    var requirements = AchievementBuilder.SerializeRequirements(achievement);
                    if (requirements.Length > 1024)
                    {
                        warning.AppendFormat("Achievement \"{0}\" exceeds serialized limit ({1}/{2})", achievement.Title, requirements.Length, 1024);
                        warning.AppendLine();
                    }

                    writer.Write(requirements);
                    writer.Write(':');

                    writer.Write(achievement.Title);
                    writer.Write(':');

                    writer.Write(achievement.Description);
                    writer.Write(':');

                    writer.Write(" : : :"); // discontinued features

                    writer.Write(author); // author
                    writer.Write(':');

                    writer.Write(achievement.Points);
                    writer.Write(':');

                    writer.Write("0:0:0:0:"); // created, modified, upvotes, downvotes

                    writer.Write(achievement.BadgeName);
                    writer.WriteLine();
                }
            }

            if (warning.Length > 0)
                MessageBoxViewModel.ShowMessage(warning.ToString());
        }
    }
}
