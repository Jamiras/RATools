using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RATools.Data;
using RATools.Parser.Internal;
using Jamiras.Components;

namespace RATools.Parser
{
    internal class LocalAchievements
    {
        public LocalAchievements(string filename)
        {
            _achievements = new List<Achievement>();
            _filename = filename;
            Read();
        }
        
        private readonly string _filename;

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
                reader.ReadLine(); // version
                reader.ReadLine(); // game

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

                    tokenizer.ReadTo(':'); // ?
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // ?
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // ?
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // Author
                    tokenizer.Advance();

                    part = tokenizer.ReadTo(':'); // points
                    if (Int32.TryParse(part.ToString(), out num))
                        achievement.Points = num;
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // ?
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // ?
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // ?
                    tokenizer.Advance();

                    tokenizer.ReadTo(':'); // ?
                    tokenizer.Advance();

                    achievement.BadgeName = tokenizer.ReadTo(':').ToString();

                    _achievements.Add(achievement.ToAchievement());
                }
            }
        }
    }
}
