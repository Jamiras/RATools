using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RATools.Data;
using RATools.Parser.Internal;
using Jamiras.Components;

namespace RATools.Parser
{
    [DebuggerDisplay("LocalAchievements: {_title}")]
    internal class LocalAchievements
    {
        public LocalAchievements(string filename)
        {
            _achievements = new List<Achievement>();
            _filename = filename;
            Read();
        }
        
        private readonly string _filename;
        private string _version;
        private string _title;

        public IEnumerable<Achievement> Achievements
        {
            get { return _achievements; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Achievement> _achievements;

        private void Read()
        {
            if (!File.Exists(_filename))
                return;

            using (var reader = File.OpenText(_filename))
            {
                _version = reader.ReadLine();
                _title = reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var tokenizer = Tokenizer.CreateTokenizer(line);
                    var achievement = new AchievementBuilder();

                    var part = tokenizer.ReadTo(':'); // id
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

                    tokenizer.ReadTo(':'); // created timestamp
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // updated timestamp
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // upvotes
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // downvotes
                    tokenizer.Advance();

                    achievement.BadgeName = tokenizer.ReadTo(':').ToString();
                    if (achievement.BadgeName.EndsWith("_lock"))
                        achievement.BadgeName.Remove(achievement.BadgeName.Length - 5);

                    _achievements.Add(achievement.ToAchievement());
                }
            }
        }

        public void Commit()
        {
            using (var writer = File.CreateText(_filename))
            {
                writer.WriteLine(_version);
                writer.WriteLine(_title);

                foreach (var achievement in _achievements)
                {
                    writer.Write(0); // id always 0 in local file
                    writer.Write(':');

                    var requirements = AchievementBuilder.SerializeRequirements(achievement);
                    writer.Write(requirements);
                    writer.Write(':');

                    writer.Write(achievement.Title);
                    writer.Write(':');

                    writer.Write(achievement.Description);
                    writer.Write(':');

                    writer.Write(" : : :Jamiras:"); // discontinued features,author

                    writer.Write(achievement.Points);
                    writer.Write(':');

                    writer.Write("0:0:59080:25:"); // created, modified, upvotes, downvotes

                    writer.Write(achievement.BadgeName);
                    writer.WriteLine();
                }
            }
        }
    }
}
