using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title}")]

    public class AchievementSetViewModel : ViewModelBase
    {
        public AchievementSetViewModel(AchievementSet achievementSet)
            : this(achievementSet,
                  ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools"),
                  ServiceRepository.Instance.FindService<IFileSystemService>())
        {
        }

        public AchievementSetViewModel(AchievementSet subset, AchievementSetViewModel coreSetViewModel)
            : this(subset, coreSetViewModel._logger, coreSetViewModel._fileSystemService)
        {
            PublishedAssets = coreSetViewModel.PublishedAssets;
            LocalAssets = coreSetViewModel.LocalAssets;
        }

        internal AchievementSetViewModel(AchievementSet achievementSet,
            ILogger logger, IFileSystemService fileSystemService)
        {
            /* unit tests call this constructor directly and will provide their own Script object and don't need Resources */
            _achievementSet = achievementSet;
            Title = achievementSet.Title;

            _logger = logger;
            _fileSystemService = fileSystemService;
        }

        public AchievementSet AchievementSet
        {
            get { return _achievementSet; } 
            internal set
            {
                _achievementSet = value;
                Title = value.Title;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private AchievementSet _achievementSet;

        protected readonly ILogger _logger;
        protected readonly IFileSystemService _fileSystemService;
        public PublishedAssets PublishedAssets { get; private set; }
        public LocalAssets LocalAssets { get; private set; }

        public int GameId { get { return _achievementSet.OwnerGameId; } }
        public int Id { get { return _achievementSet.OwnerSetId; } }

        public static readonly ModelProperty TitleProperty = ModelProperty.Register(typeof(AchievementSetViewModel), "Title", typeof(string), String.Empty);
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            private set { SetValue(TitleProperty, value); }
        }

        public static readonly ModelProperty BadgeNameProperty = ModelProperty.Register(typeof(AchievementSetViewModel), "BadgeName", typeof(string), String.Empty);
        public string BadgeName
        {
            get { return (string)GetValue(BadgeNameProperty); }
            set { SetValue(BadgeNameProperty, value); }
        }

        public void AssociateRACacheDirectory(string raCacheDirectory, List<AchievementSetViewModel> sets = null)
        {
            ReadPublished(raCacheDirectory);

            var fileName = Path.Combine(raCacheDirectory, GameId + "-User.txt");
            LocalAssets = new LocalAssets(fileName, _fileSystemService);

            if (String.IsNullOrEmpty(LocalAssets.Title))
                LocalAssets.Title = Title;

            if (_logger.IsEnabled(LogLevel.Verbose))
            {
                var localAchievementCount = LocalAssets.Achievements.Count();
                var localAchievementPoints = LocalAssets.Achievements.Sum(a => a.Points);
                _logger.WriteVerbose(String.Format("Read {0} local achievements ({1} points) from {2}-User.txt", localAchievementCount, localAchievementPoints, GameId));
            }

            if (sets != null)
            {
                foreach (var set in PublishedAssets.Sets)
                {
                    if (!ReferenceEquals(_achievementSet, set))
                        sets.Add(new AchievementSetViewModel(set, this));
                }
            }
        }

        private void ReadPublished(string raCacheDirectory)
        {
            var fileName = Path.Combine(raCacheDirectory, GameId + ".json");
            var publishedAssets = new PublishedAssets(fileName, _fileSystemService);

            var coreSet = publishedAssets.Sets.FirstOrDefault(s => s.Type == AchievementSetType.Core);
            if (coreSet != null)
            {
                _achievementSet = coreSet;
                Title = coreSet.Title;
            }
            else
            {
                Title = publishedAssets.Title;
            }

            if (_logger.IsEnabled(LogLevel.Verbose))
            {
                var promotedCount = publishedAssets.Achievements.Count(a => !a.IsUnpromoted);
                var promotedPoints = publishedAssets.Achievements.Where(a => !a.IsUnpromoted).Sum(a => a.Points);
                _logger.WriteVerbose(String.Format("Identified {0} promoted achievements ({1} points) for game {2}", promotedCount, promotedPoints, GameId));

                var unpromotedCount = publishedAssets.Achievements.Count(a => a.IsUnpromoted);
                var unpromotedPoints = publishedAssets.Achievements.Where(a => a.IsUnpromoted).Sum(a => a.Points);
                _logger.WriteVerbose(String.Format("Identified {0} unpromoted achievements ({1} points) for game {2}", unpromotedCount, unpromotedPoints, GameId));
            }

            if (AchievementSet.Type == AchievementSetType.Core)
            {
                publishedAssets.LoadNotes();
                _logger.WriteVerbose(String.Format("Read {0} code notes for game {1}", publishedAssets.Notes.Count, GameId));
            }

            PublishedAssets = publishedAssets;
        }

        public bool UpdateLocal(Achievement achievement, Achievement localAchievement, Action<AssetBase, LocalAssets.LocalAssetChange> assetChangedHandler, bool refresh)
        {
            if (achievement.OwnerSetId != AchievementSet.OwnerSetId)
                return false;

            if (refresh)
            {
                if (!AchievementSet.Type.CanLoadWithBaseSet())
                {
                    LocalAssets.MergeExternalChanges((asset, change) =>
                    {
                        var modifiedAchievement = asset as Achievement;
                        if (modifiedAchievement != null && modifiedAchievement.Id == achievement.Id)
                            localAchievement = (change == LocalAssets.LocalAssetChange.Removed) ? null : modifiedAchievement;
                        else
                            assetChangedHandler(asset, change);
                    });
                }
            }

            if (_logger.IsEnabled(LogLevel.Verbose))
            {
                if (achievement == null)
                    _logger.WriteVerbose(String.Format("Deleting {0} from {1}", localAchievement.Title, Path.GetFileName(LocalAssets.Filename)));
                else if (localAchievement != null)
                    _logger.WriteVerbose(String.Format("Updating {0} in {1}", achievement.Title, Path.GetFileName(LocalAssets.Filename)));
                else
                    _logger.WriteVerbose(String.Format("Committing {0} to {1}", achievement.Title, Path.GetFileName(LocalAssets.Filename)));
            }

            LocalAssets.Replace(localAchievement, achievement);

            return true;
        }

        public bool UpdateLocal(Leaderboard leaderboard, Leaderboard localLeaderboard, Action<AssetBase, LocalAssets.LocalAssetChange> assetChangedHandler, bool refresh)
        {
            if (leaderboard.OwnerSetId != AchievementSet.OwnerSetId)
                return false;

            if (refresh)
            {
                if (!AchievementSet.Type.CanLoadWithBaseSet())
                {
                    LocalAssets.MergeExternalChanges((asset, change) =>
                    {
                        var modifiedLeaderboard = asset as Leaderboard;
                        if (modifiedLeaderboard != null && modifiedLeaderboard.Id == leaderboard.Id)
                            localLeaderboard = (change == LocalAssets.LocalAssetChange.Removed) ? null : modifiedLeaderboard;
                        else
                            assetChangedHandler(asset, change);
                    });
                }
            }

            if (_logger.IsEnabled(LogLevel.Verbose))
            {
                if (leaderboard == null)
                    _logger.WriteVerbose(String.Format("Deleting {0} from {1}", localLeaderboard.Title, Path.GetFileName(LocalAssets.Filename)));
                else if (localLeaderboard != null)
                    _logger.WriteVerbose(String.Format("Updating {0} in {1}", leaderboard.Title, Path.GetFileName(LocalAssets.Filename)));
                else
                    _logger.WriteVerbose(String.Format("Committing {0} to {1}", leaderboard.Title, Path.GetFileName(LocalAssets.Filename)));
            }

            LocalAssets.Replace(localLeaderboard, leaderboard);

            return true;
        }

        internal bool UpdateLocal(RichPresence richPresence, RichPresence localRichPresence, Action<AssetBase, LocalAssets.LocalAssetChange> assetChangedHandler, bool refresh)
        {
            if (richPresence.OwnerSetId != AchievementSet.OwnerSetId)
                return false;

            if (refresh)
            {
                if (!AchievementSet.Type.CanLoadWithBaseSet())
                {
                    LocalAssets.MergeExternalChanges((asset, change) =>
                    {
                        var modifiedRichPresence = asset as RichPresence;
                        if (modifiedRichPresence != null)
                            localRichPresence = (change == LocalAssets.LocalAssetChange.Removed) ? null : modifiedRichPresence;
                        else
                            assetChangedHandler(asset, change);
                    });
                }
            }

            if (_logger.IsEnabled(LogLevel.Verbose))
            {
                if (richPresence == null)
                    _logger.WriteVerbose("Deleting local rich presence");
                else if (localRichPresence != null)
                    _logger.WriteVerbose("Updating rich presence");
                else
                    _logger.WriteVerbose("Committing rich presence");
            }

            LocalAssets.Replace(localRichPresence, richPresence);

            return true;
        }

        public void MergeExternalChanges(Action<AssetBase, LocalAssets.LocalAssetChange> assetChangedHandler)
        {
            if (!AchievementSet.Type.CanLoadWithBaseSet())
                LocalAssets.MergeExternalChanges(assetChangedHandler);
        }
    }
}
