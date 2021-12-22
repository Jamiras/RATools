using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RATools.ViewModels
{
    public class ConditionsAnalyzerViewModel : DialogViewModelBase
    {
        public ConditionsAnalyzerViewModel()
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>(), ServiceRepository.Instance.FindService<ISettings>())
        {
        }

        public ConditionsAnalyzerViewModel(IBackgroundWorkerService backgroundWorkerService, ISettings settings)
        {
            _backgroundWorkerService = backgroundWorkerService;
            _settings = settings;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Conditions Analyzer";
            CanClose = true;

            Snapshot = new GameDataSnapshotViewModel(Progress, backgroundWorkerService, settings);

            NumConditions = new RangeFilterFieldViewModel("Number of Conditions");
            NumAltGroups = new RangeFilterFieldViewModel("Number of Alt Groups");

            var flagLookup = new[]
            {
                new LookupItem(-1, "Any"),
                new LookupItem((int)RequirementType.None, "None"),
                new LookupItem((int)RequirementType.ResetIf, "ResetIf"),
                new LookupItem((int)RequirementType.PauseIf, "PauseIf"),
                new LookupItem((int)RequirementType.AddHits, "AddHits"),
                new LookupItem((int)RequirementType.SubHits, "SubHits"),
                new LookupItem((int)RequirementType.AddSource, "AddSource"),
                new LookupItem((int)RequirementType.SubSource, "SubSource"),
                new LookupItem((int)RequirementType.AddAddress, "AddAddress"),
                new LookupItem((int)RequirementType.ResetNextIf, "ResetNextIf"),
                new LookupItem((int)RequirementType.Trigger, "Trigger"),
                new LookupItem((int)RequirementType.AndNext, "AndNext"),
                new LookupItem((int)RequirementType.OrNext, "OrNext"),
                new LookupItem((int)RequirementType.Measured, "Measured"),
                new LookupItem((int)RequirementType.MeasuredPercent, "MeasuredPercent"),
                new LookupItem((int)RequirementType.MeasuredIf, "AddHits"),
            };

            var typeLookup = new[]
            {
                new LookupItem(-1, "Any"),
                new LookupItem((int)FieldType.MemoryAddress, "Mem"),
                new LookupItem((int)FieldType.PreviousValue, "Delta"),
                new LookupItem((int)FieldType.PriorValue, "Prior"),
                new LookupItem((int)FieldType.Value, "Value"),
            };

            var comparisonLookup = new[]
            {
                new LookupItem(-1, "Any"),
                new LookupItem((int)RequirementOperator.Equal, "=="),
                new LookupItem((int)RequirementOperator.NotEqual, "!="),
                new LookupItem((int)RequirementOperator.LessThan, "<"),
                new LookupItem((int)RequirementOperator.LessThanOrEqual, "<="),
                new LookupItem((int)RequirementOperator.GreaterThan, ">"),
                new LookupItem((int)RequirementOperator.GreaterThanOrEqual, ">="),
                new LookupItem((int)RequirementOperator.Multiply, "*"),
                new LookupItem((int)RequirementOperator.Divide, "/"),
                new LookupItem((int)RequirementOperator.LogicalAnd, "&"),
            };

            Flag = new LookupFieldViewModel("Flag", flagLookup) { SelectedId = -1 };
            SourceType = new LookupFieldViewModel("Source Type", typeLookup) { SelectedId = -1 };
            SourceValue = new RangeFilterFieldViewModel("Source Value");
            Comparison = new LookupFieldViewModel("Comparison", comparisonLookup) { SelectedId = -1 };
            TargetType = new LookupFieldViewModel("Target Type", typeLookup) { SelectedId = -1 };
            TargetValue = new RangeFilterFieldViewModel("Target Value");
            HitCount = new RangeFilterFieldViewModel("Hit Count");

            Results = new ObservableCollection<Result>();
            SearchCommand = new DelegateCommand(Search);
            ExportCommand = new DelegateCommand(Export);
            OpenGameCommand = new DelegateCommand<Result>(OpenGame);
            OpenItemCommand = new DelegateCommand<Result>(OpenItem);

            AddPropertyChangedHandler(DialogResultProperty, OnDialogResultPropertyChanged);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly ISettings _settings;

        public ProgressFieldViewModel Progress { get; private set; }

        public GameDataSnapshotViewModel Snapshot { get; private set; }

        public TextFieldViewModel NumConditions { get; private set; }
        public TextFieldViewModel NumAltGroups { get; private set; }

        public LookupFieldViewModel Flag { get; private set; }
        public LookupFieldViewModel SourceType { get; private set; }
        public TextFieldViewModel SourceValue { get; private set; }
        public LookupFieldViewModel Comparison { get; private set; }
        public LookupFieldViewModel TargetType { get; private set; }
        public TextFieldViewModel TargetValue { get; private set; }
        public TextFieldViewModel HitCount { get; private set; }

        public class Result
        {
            public string GameName { get; internal set; }
            public int GameId { get; internal set; }
            public string ItemName { get; internal set; }
            public int AchievementId { get; internal set; }
            public int LeaderboardId { get; internal set; }
            public string Details { get; internal set; }
            public bool IsUnofficial { get; internal set; }
        }

        public ObservableCollection<Result> Results { get; private set; }

        public CommandBase<Result> OpenGameCommand { get; private set; }
        private void OpenGame(Result result)
        {
            var url = "http://retroachievements.org/Game/" + result.GameId;
            Process.Start(url);
        }

        public CommandBase<Result> OpenItemCommand { get; private set; }
        private void OpenItem(Result result)
        {
            string url = "http://retroachievements.org/";
            if (result.AchievementId != 0)
                url = url + "Achievement/" + result.AchievementId;
            else if (result.LeaderboardId != 0)
                url = url + "leaderboardinfo.php?i=" + result.LeaderboardId;
            else
                url = url + "Game/" + result.GameId;

            Process.Start(url);
        }

        private class RangeFilterFieldViewModel : TextFieldViewModel
        {
            public RangeFilterFieldViewModel(string label)
                : base(label, 10)
            {
            }

            protected override string Validate(ModelProperty property, object value)
            {
                if (property == TextFieldViewModel.TextProperty)
                {
                    string expression = (string)value;
                    if (expression != null)
                    {
                        int num;
                        RequirementOperator comparison;
                        if (!Parse(out num, out comparison))
                            return "Expression must be numeric, or a comparison to a numeric";
                    }
                }

                return base.Validate(property, value);
            }

            public bool Parse(out int number, out RequirementOperator comparison)
            {
                string expression = Text;
                int index = 0;

                if (String.IsNullOrEmpty(expression))
                {
                    number = 0;
                    comparison = RequirementOperator.None;
                    return true;
                }

                switch (expression[0])
                {
                    case '<':
                        ++index;
                        if (expression[index] == '=')
                        {
                            ++index;
                            comparison = RequirementOperator.LessThanOrEqual;
                        }
                        else
                        {
                            comparison = RequirementOperator.LessThan;
                        }
                        break;

                    case '>':
                        ++index;
                        if (expression[index] == '=')
                        {
                            ++index;
                            comparison = RequirementOperator.GreaterThanOrEqual;
                        }
                        else
                        {
                            comparison = RequirementOperator.GreaterThan;
                        }
                        break;

                    case '!':
                        ++index;
                        if (expression[index] == '=')
                            ++index;
                        comparison = RequirementOperator.NotEqual;
                        break;

                    case '=':
                        ++index;
                        if (expression[index] == '=')
                            ++index;
                        comparison = RequirementOperator.Equal;
                        break;

                    default:
                        comparison = RequirementOperator.Equal;
                        break;
                }

                if (index > 0)
                    expression = expression.Substring(index);
                return Int32.TryParse(expression, out number);
            }
        }

        private void OnDialogResultPropertyChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            // stop any ongoing operations
            Progress.IsEnabled = false;
        }

        public static readonly ModelProperty IsCoreAchievementsScannedProperty = ModelProperty.Register(typeof(ConditionsAnalyzerViewModel), "IsAchievementsScanned", typeof(bool), true);
        public bool IsCoreAchievementsScanned
        {
            get { return (bool)GetValue(IsCoreAchievementsScannedProperty); }
            set { SetValue(IsCoreAchievementsScannedProperty, value); }
        }

        public static readonly ModelProperty IsNonCoreAchievementsScannedProperty = ModelProperty.Register(typeof(ConditionsAnalyzerViewModel), "IsAchievementsScanned", typeof(bool), true);
        public bool IsNonCoreAchievementsScanned
        {
            get { return (bool)GetValue(IsNonCoreAchievementsScannedProperty); }
            set { SetValue(IsNonCoreAchievementsScannedProperty, value); }
        }

        public static readonly ModelProperty IsLeaderboardStartScannedProperty = ModelProperty.Register(typeof(ConditionsAnalyzerViewModel), "IsLeaderboardStartScanned", typeof(bool), true);
        public bool IsLeaderboardStartScanned
        {
            get { return (bool)GetValue(IsLeaderboardStartScannedProperty); }
            set { SetValue(IsLeaderboardStartScannedProperty, value); }
        }

        public static readonly ModelProperty IsLeaderboardSubmitScannedProperty = ModelProperty.Register(typeof(ConditionsAnalyzerViewModel), "IsLeaderboardSubmitScanned", typeof(bool), true);
        public bool IsLeaderboardSubmitScanned
        {
            get { return (bool)GetValue(IsLeaderboardSubmitScannedProperty); }
            set { SetValue(IsLeaderboardSubmitScannedProperty, value); }
        }

        public static readonly ModelProperty IsLeaderboardCancelScannedProperty = ModelProperty.Register(typeof(ConditionsAnalyzerViewModel), "IsLeaderboardCancelScanned", typeof(bool), true);
        public bool IsLeaderboardCancelScanned
        {
            get { return (bool)GetValue(IsLeaderboardCancelScannedProperty); }
            set { SetValue(IsLeaderboardCancelScannedProperty, value); }
        }

        public static readonly ModelProperty IsRichPresenceScannedProperty = ModelProperty.Register(typeof(ConditionsAnalyzerViewModel), "IsRichPresenceScanned", typeof(bool), true);
        public bool IsRichPresenceScanned
        {
            get { return (bool)GetValue(IsRichPresenceScannedProperty); }
            set { SetValue(IsRichPresenceScannedProperty, value); }
        }

        private class SearchCriteria
        {
            public RequirementOperator NumConditionsComparison;
            public int NumConditions;
            public RequirementOperator NumAltGroupsComparison;
            public int NumAltGroups;

            public RequirementType Flag;
            public FieldType SourceType;
            public RequirementOperator SourceValueComparison;
            public int SourceValue;
            public RequirementOperator Comparison;
            public FieldType TargetType;
            public RequirementOperator TargetValueComparison;
            public int TargetValue;
            public RequirementOperator HitCountComparison;
            public int HitCount;
        }

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            _backgroundWorkerService.RunAsync(DoSearch);
        }

        private void DoSearch()
        { 
            var criteria = new SearchCriteria();
            ((RangeFilterFieldViewModel)NumConditions).Parse(out criteria.NumConditions, out criteria.NumConditionsComparison);
            ((RangeFilterFieldViewModel)NumAltGroups).Parse(out criteria.NumAltGroups, out criteria.NumAltGroupsComparison);

            criteria.Flag = (RequirementType)Flag.SelectedId;
            criteria.SourceType = (FieldType)SourceType.SelectedId;
            ((RangeFilterFieldViewModel)SourceValue).Parse(out criteria.SourceValue, out criteria.SourceValueComparison);
            criteria.Comparison = (RequirementOperator)Comparison.SelectedId;
            criteria.TargetType = (FieldType)TargetType.SelectedId;
            ((RangeFilterFieldViewModel)TargetValue).Parse(out criteria.TargetValue, out criteria.TargetValueComparison);
            ((RangeFilterFieldViewModel)HitCount).Parse(out criteria.HitCount, out criteria.HitCountComparison);

            var directory = _settings.DumpDirectory;

            var filesToRead = 0;
            if (IsCoreAchievementsScanned || IsNonCoreAchievementsScanned)
                filesToRead += Snapshot.AchievementGameCount;
            if (IsLeaderboardCancelScanned || IsLeaderboardStartScanned || IsLeaderboardSubmitScanned)
                filesToRead += Snapshot.LeaderboardGameCount;
            if (IsRichPresenceScanned)
                filesToRead += Snapshot.RichPresenceCount;

            Progress.Reset(filesToRead);
            Progress.IsEnabled = true;

            var results = new List<Result>();

            if (IsCoreAchievementsScanned || IsNonCoreAchievementsScanned)
            {
                Progress.Label = "Processing Achievements...";

                foreach (var gameId in Snapshot.GamesWithAchievements)
                {
                    Progress.Current++;
                    if (!Progress.IsEnabled)
                        break;

                    var file = Path.Combine(directory, gameId + ".json");
                    var contents = File.ReadAllText(file);
                    var json = new Jamiras.IO.Serialization.JsonObject(contents);
                    var patchData = json.GetField("PatchData");

                    var achievements = patchData.ObjectValue.GetField("Achievements").ObjectArrayValue;
                    if (achievements == null)
                        continue;

                    var gameName = patchData.ObjectValue.GetField("Title").StringValue;

                    foreach (var achievement in achievements)
                    {
                        var flags = achievement.GetField("Flags").IntegerValue;
                        if (!IsCoreAchievementsScanned)
                        {
                            if (flags == 3)
                                continue;
                        }
                        else if (!IsNonCoreAchievementsScanned)
                        {
                            if (flags != 3)
                                continue;
                        }

                        var memAddr = achievement.GetField("MemAddr").StringValue;
                        if (Matches(memAddr, criteria))
                        {
                            results.Add(new Result
                            {
                                GameId = gameId,
                                GameName = gameName,
                                AchievementId = achievement.GetField("ID").IntegerValue.GetValueOrDefault(),
                                ItemName = achievement.GetField("Title").StringValue,
                                Details = memAddr,
                                IsUnofficial = flags == 5
                            });
                        }
                    }
                }
            }

            if (IsLeaderboardStartScanned || IsLeaderboardSubmitScanned || IsLeaderboardCancelScanned)
            {
                Progress.Label = "Processing Leaderboards...";

                foreach (var gameId in Snapshot.GamesWithLeaderboards)
                {
                    Progress.Current++;
                    if (!Progress.IsEnabled)
                        break;

                    var file = Path.Combine(directory, gameId + ".json");
                    var contents = File.ReadAllText(file);
                    var json = new Jamiras.IO.Serialization.JsonObject(contents);
                    var patchData = json.GetField("PatchData");

                    var leaderboards = patchData.ObjectValue.GetField("Leaderboards").ObjectArrayValue;
                    if (leaderboards == null)
                        continue;

                    var gameName = patchData.ObjectValue.GetField("Title").StringValue;

                    foreach (var leaderboard in leaderboards)
                    {
                        var details = String.Empty;

                        var token = Token.Empty;
                        var memAddr = leaderboard.GetField("Mem").StringValue;
                        foreach (var part in Tokenizer.Split(memAddr, ':'))
                        {
                            if (part == "STA" && IsLeaderboardStartScanned)
                            {
                                token = part;
                                continue;
                            }
                            if (part == "CAN" && IsLeaderboardCancelScanned)
                            {
                                token = part;
                                continue;
                            }
                            if (part == "SUB" && IsLeaderboardSubmitScanned)
                            {
                                token = part;
                                continue;
                            }
                            if (token.IsEmpty)
                                continue;

                            if (Matches(part.ToString(), criteria))
                            {
                                if (details.Length > 0)
                                    details += ", ";
                                details += token.ToString();
                                details += ':';
                                details += part.ToString();
                            }

                            token = Token.Empty;
                        }

                        if (details.Length > 0)
                        {
                            results.Add(new Result
                            {
                                GameId = gameId,
                                GameName = gameName,
                                LeaderboardId = leaderboard.GetField("ID").IntegerValue.GetValueOrDefault(),
                                ItemName = "Leaderboard: " + leaderboard.GetField("Title").StringValue,
                                Details = details,
                            });
                        }
                    }
                }
            }

            if (IsRichPresenceScanned)
            {
                Progress.Label = "Processing Rich Presence...";

                foreach (var gameId in Snapshot.GamesWithRichPresence)
                {
                    Progress.Current++;
                    if (!Progress.IsEnabled)
                        break;

                    var file = Path.Combine(directory, gameId + ".json");
                    var contents = File.ReadAllText(file);
                    var json = new Jamiras.IO.Serialization.JsonObject(contents);
                    var patchData = json.GetField("PatchData");

                    var richPresence = patchData.ObjectValue.GetField("RichPresencePatch").StringValue;
                    if (richPresence != null)
                    {
                        int index = richPresence.IndexOf("Display:");
                        if (index != -1)
                        {
                            var details = String.Empty;

                            foreach (var line in richPresence.Substring(index).Split('\n'))
                            {
                                if (line.Trim().Length == 0)
                                    break;

                                if (line.StartsWith("?"))
                                {
                                    index = line.IndexOf('?', 1);
                                    if (index != -1)
                                    {
                                        var memAddr = line.Substring(1, index - 1);
                                        if (Matches(memAddr, criteria))
                                        {
                                            if (details.Length > 0)
                                                details += ", ";
                                            details += '?';
                                            details += memAddr;
                                            details += '?';
                                        }
                                    }
                                }
                            }

                            if (details.Length > 0)
                            {
                                results.Add(new Result
                                {
                                    GameId = gameId,
                                    GameName = patchData.ObjectValue.GetField("Title").StringValue,
                                    ItemName = "Rich Presence",
                                    Details = details,
                                });
                            }
                        }
                    }
                }
            }

            if (Progress.IsEnabled)
            {
                results.Sort((l, r) =>
                {
                    var diff = String.Compare(l.GameName, r.GameName);
                    if (diff == 0)
                        diff = String.Compare(l.ItemName, r.ItemName);

                    return diff;
                });

                _backgroundWorkerService.InvokeOnUiThread(() =>
                {
                    Results.Clear();
                    foreach (var result in results)
                        Results.Add(result);
                });

                Progress.IsEnabled = false;
                Progress.Label = String.Empty;
            }
        }

        private static bool Matches(string memAddr, SearchCriteria criteria)
        {
            AchievementBuilder builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(memAddr));

            if (criteria.NumConditionsComparison != RequirementOperator.None)
            {
                var numConditions = builder.CoreRequirements.Count;
                foreach (var altGroup in builder.AlternateRequirements)
                    numConditions += altGroup.Count;
                if (!ValueMatches(numConditions, criteria.NumConditions, criteria.NumConditionsComparison))
                    return false;
            }

            if (!ValueMatches(builder.AlternateRequirements.Count, criteria.NumAltGroups, criteria.NumAltGroupsComparison))
                return false;

            foreach (var condition in builder.CoreRequirements)
            {
                if (ConditionMatches(condition, criteria))
                    return true;
            }

            foreach (var altGroup in builder.AlternateRequirements)
            {
                foreach (var condition in altGroup)
                {
                    if (ConditionMatches(condition, criteria))
                        return true;
                }
            }

            return false;
        }

        private static bool ConditionMatches(Requirement condition, SearchCriteria criteria)
        {
            if (criteria.Flag != (RequirementType)(-1) && condition.Type != criteria.Flag)
                return false;
            if (criteria.Comparison != (RequirementOperator)(-1) && condition.Operator != criteria.Comparison)
                return false;
            if (criteria.SourceType != (FieldType)(-1) && condition.Left.Type != criteria.SourceType)
                return false;
            if (criteria.TargetType != (FieldType)(-1) && condition.Right.Type != criteria.TargetType)
                return false;

            if (!ValueMatches((int)condition.HitCount, criteria.HitCount, criteria.HitCountComparison))
                return false;
            if (!ValueMatches((int)condition.Left.Value, criteria.SourceValue, criteria.SourceValueComparison))
                return false;
            if (!ValueMatches((int)condition.Right.Value, criteria.TargetValue, criteria.TargetValueComparison))
                return false;

            return true;
        }

        private static bool ValueMatches(int value, int comparisonValue, RequirementOperator comparisonType)
        {
            switch (comparisonType)
            {
                case RequirementOperator.None:
                    return true;

                case RequirementOperator.Equal:
                    return value == comparisonValue;

                case RequirementOperator.NotEqual:
                    return value != comparisonValue;

                case RequirementOperator.LessThan:
                    return value < comparisonValue;

                case RequirementOperator.LessThanOrEqual:
                    return value <= comparisonValue;

                case RequirementOperator.GreaterThan:
                    return value > comparisonValue;

                case RequirementOperator.GreaterThanOrEqual:
                    return value >= comparisonValue;

                default:
                    return false;
            }
        }

        private static void AppendRange(StringBuilder builder, TextFieldViewModel viewModel)
        {
            int num;
            RequirementOperator comparison;
            if (((RangeFilterFieldViewModel)viewModel).Parse(out num, out comparison) && comparison != RequirementOperator.None)
            {
                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(viewModel.Label);
                
                switch (comparison)
                {
                    case RequirementOperator.Equal:
                        builder.Append(" = ");
                        break;
                    case RequirementOperator.NotEqual:
                        builder.Append(" != ");
                        break;
                    case RequirementOperator.LessThan:
                        builder.Append(" < ");
                        break;
                    case RequirementOperator.LessThanOrEqual:
                        builder.Append(" <= ");
                        break;
                    case RequirementOperator.GreaterThan:
                        builder.Append(" > ");
                        break;
                    case RequirementOperator.GreaterThanOrEqual:
                        builder.Append(" >= ");
                        break;
                }
                builder.Append(num);
            }
        }

        private static void AppendLookup(StringBuilder builder, LookupFieldViewModel viewModel)
        {
            if (viewModel.SelectedId != -1)
            {
                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(viewModel.Label);
                builder.Append(" = ");
                builder.Append(viewModel.SelectedLabel);
            }
        }

        public CommandBase ExportCommand { get; private set; }
        private void Export()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Export search results";
            vm.Filters["Text file"] = "*.txt";
            vm.FileNames = new[] { "results.txt" };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                using (var file = File.CreateText(vm.FileNames[0]))
                {
                    var builder = new StringBuilder();
                    AppendRange(builder, NumConditions);
                    AppendRange(builder, NumAltGroups);

                    AppendLookup(builder, Flag);
                    AppendLookup(builder, SourceType);
                    AppendRange(builder, SourceValue);
                    AppendLookup(builder, Comparison);
                    AppendLookup(builder, TargetType);
                    AppendRange(builder, TargetValue);
                    AppendRange(builder, HitCount);

                    if (builder.Length > 0)
                    {
                        file.Write("Search Criteria: ");
                        file.WriteLine(builder.ToString());
                        file.WriteLine();
                    }

                    foreach (var result in Results)
                        file.WriteLine(string.Format("{0},\"{1}\",{2},\"{3}\",\"{4}\"", result.GameId, result.GameName, 
                            result.AchievementId > 0 ? result.AchievementId : result.LeaderboardId,
                            result.ItemName, result.Details));
                }
            }
        }
    }
}
