using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RATools.ViewModels
{
    public class OpenTicketsViewModel : DialogViewModelBase
    {
        public OpenTicketsViewModel()
            : this(ServiceRepository.Instance.FindService<IFileSystemService>(),
                   ServiceRepository.Instance.FindService<IHttpRequestService>(),
                   ServiceRepository.Instance.FindService<IBackgroundWorkerService>())
        {
        }

        public OpenTicketsViewModel(IFileSystemService fileSystemService, IHttpRequestService httpRequestService, IBackgroundWorkerService backgroundWorkerService)
        {
            _fileSystemService = fileSystemService;
            _httpRequestService = httpRequestService;
            _backgroundWorkerService = backgroundWorkerService;

            DialogTitle = "Open Tickets";
            CanClose = true;

            OpenGameCommand = new DelegateCommand<GameTickets>(OpenGame);
            OpenGameTicketsCommand = new DelegateCommand<GameTickets>(OpenGameTickets);
            OpenAchievementCommand = new DelegateCommand<AchievementTickets>(OpenAchievement);

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            backgroundWorkerService.RunAsync(LoadTickets);
        }

        private readonly IFileSystemService _fileSystemService;
        private readonly IHttpRequestService _httpRequestService;
        private readonly IBackgroundWorkerService _backgroundWorkerService;

        public ProgressFieldViewModel Progress { get; private set; }

        [DebuggerDisplay("{GameName} ({GameId})")]
        public class GameTickets
        {
            public int GameId { get; set; }
            public string GameName { get; set; }
            public int OpenTickets { get; set; }
        }

        [DebuggerDisplay("{AchievementName} ({AchievementId})")]
        public class AchievementTickets
        {
            public AchievementTickets()
            {
                OpenTickets = new List<int>();
            }

            public int AchievementId { get; set; }
            public string AchievementName { get; set; }
            public GameTickets Game { get; set; }
            public List<int> OpenTickets { get; set; }
        }

        public CommandBase<AchievementTickets> OpenAchievementCommand { get; private set; }
        private void OpenAchievement(AchievementTickets achievement)
        {
            var url = "https://retroachievements.org/achievement/" + achievement.AchievementId;
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl(url);
        }

        public CommandBase<GameTickets> OpenGameCommand { get; private set; }
        private void OpenGame(GameTickets game)
        {
            var url = "https://retroachievements.org/game/" + game.GameId;
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl(url);
        }

        public CommandBase<GameTickets> OpenGameTicketsCommand { get; private set; }
        private void OpenGameTickets(GameTickets game)
        {
            var url = "https://retroachievements.org/ticketmanager.php?ampt=1&g=" + game.GameId;
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl(url);
        }

        public static readonly ModelProperty OpenTicketsProperty = ModelProperty.Register(typeof(OpenTicketsViewModel), "OpenTickets", typeof(int), 0);

        public int OpenTickets
        {
            get { return (int)GetValue(OpenTicketsProperty); }
            private set { SetValue(OpenTicketsProperty, value); }
        }

        public static readonly ModelProperty TopAchievementsProperty = ModelProperty.Register(typeof(OpenTicketsViewModel), "TopAchievements", typeof(IEnumerable<AchievementTickets>), new AchievementTickets[0]);

        public IEnumerable<AchievementTickets> TopAchievements
        {
            get { return (IEnumerable<AchievementTickets>)GetValue(TopAchievementsProperty); }
            private set { SetValue(TopAchievementsProperty, value); }
        }

        public static readonly ModelProperty TopGamesProperty = ModelProperty.Register(typeof(OpenTicketsViewModel), "TopGames", typeof(IEnumerable<GameTickets>), new GameTickets[0]);

        public IEnumerable<GameTickets> TopGames
        {
            get { return (IEnumerable<GameTickets>)GetValue(TopGamesProperty); }
            private set { SetValue(TopGamesProperty, value); }
        }

        private void LoadTickets()
        {
            var games = new Dictionary<int, GameTickets>();
            var tickets = new Dictionary<int, AchievementTickets>();
            int totalTickets = 0;

            Progress.Reset(25);
            Progress.Label = "Fetching Open Tickets";

            int page = 0;
            do
            {
                var ticketsJson = RAWebCache.Instance.GetOpenTicketsJson(page);
                if (ticketsJson == null)
                    return;

                if (page == 0)
                {
                    var openTickets = ticketsJson.GetField("OpenTickets").IntegerValue;
                    if (openTickets == null)
                    {
                        _backgroundWorkerService.InvokeOnUiThread(() =>
                        {
                            MessageBoxViewModel.ShowMessage("Could not retrieve open tickets. Please make sure the Cookie value is up to date in your ini file.");
                            Progress.Label = String.Empty;
                        });
                        var filename = Path.Combine(Path.GetTempPath(), String.Format("raTickets{0}.json", page));
                        File.Delete(filename);
                        return;
                    }

                    totalTickets = openTickets.GetValueOrDefault();
                    Progress.Reset((totalTickets + 99) / 100);
                }

                foreach (var ticket in ticketsJson.GetField("RecentTickets").ObjectArrayValue)
                {
                    var ticketId = ticket.GetField("ID").IntegerValue.GetValueOrDefault();
                    if (ticketId == 0)
                        continue;

                    GameTickets gameTickets;
                    var gameId = ticket.GetField("GameID").IntegerValue.GetValueOrDefault();
                    if (!games.TryGetValue(gameId, out gameTickets))
                    {
                        gameTickets = new GameTickets { GameId = gameId, GameName = ticket.GetField("GameTitle").StringValue };
                        games[gameId] = gameTickets;
                    }

                    AchievementTickets achievementTickets;
                    var achievementId = ticket.GetField("AchievementID").IntegerValue.GetValueOrDefault();
                    if (!tickets.TryGetValue(achievementId, out achievementTickets))
                    {
                        achievementTickets = new AchievementTickets { AchievementId = achievementId, Game = gameTickets, AchievementName = ticket.GetField("AchievementTitle").StringValue };
                        tickets[achievementId] = achievementTickets;
                    }

                    achievementTickets.OpenTickets.Add(ticketId);
                    gameTickets.OpenTickets++;
                }

                ++page;
                Progress.Current++;
            } while (page * 100 < totalTickets);

            Progress.Label = "Sorting data";

            var ticketList = new List<AchievementTickets>(tickets.Count);
            foreach (var kvp in tickets)
                ticketList.Add(kvp.Value);
            ticketList.Sort((l, r) =>
            {
                var diff = r.OpenTickets.Count - l.OpenTickets.Count;
                if (diff == 0)
                    diff = String.Compare(l.AchievementName, r.AchievementName, StringComparison.OrdinalIgnoreCase);
                return diff;
            });

            TopAchievements = ticketList;

            var gameList = new List<GameTickets>(games.Count);
            foreach (var kvp in games)
                gameList.Add(kvp.Value);
            gameList.Sort((l, r) =>
            {
                var diff = r.OpenTickets - l.OpenTickets;
                if (diff == 0)
                    diff = String.Compare(l.GameName, r.GameName, StringComparison.OrdinalIgnoreCase);
                return diff;
            });

            TopGames = gameList;

            OpenTickets = totalTickets;
            DialogTitle = "Open Tickets: " + totalTickets;

            Progress.Label = String.Empty;
        }

        /// <summary>
        /// Gets the open tickets for the specified game.
        /// </summary>
        /// <param name="gameId">The game identifier.</param>
        /// <returns>Mapping of achievement id to ticket data, or <c>null</c> if the data cannot be retrieved.</returns>
        public static Dictionary<int, AchievementTickets> GetGameTickets(int gameId)
        {
            var ticketsPage = RAWebCache.Instance.GetOpenTicketsForGame(gameId);
            if (ticketsPage == null)
                return new Dictionary<int, AchievementTickets>();

            var games = new Dictionary<int, GameTickets>();
            var tickets = new Dictionary<int, AchievementTickets>();

            GetPageTickets(games, tickets, ticketsPage);

            return tickets;
        }

        private static int GetPageTickets(Dictionary<int, GameTickets> games, Dictionary<int, AchievementTickets> tickets, string ticketsPage)
        {
            int pageTickets;
            var tokenizer = Tokenizer.CreateTokenizer(ticketsPage);

            pageTickets = 0;
            do
            {
                tokenizer.ReadTo("<a href='/ticketmanager.php?i=");
                if (tokenizer.NextChar == '\0')
                    break;

                tokenizer.ReadTo("'>");
                tokenizer.Advance(2);
                if (tokenizer.Match("Show"))
                    continue;

                var ticketId = Int32.Parse(tokenizer.ReadNumber().ToString());

                tokenizer.ReadTo("<a href='/achievement/");
                tokenizer.Advance(22);
                var achievementId = Int32.Parse(tokenizer.ReadNumber().ToString());
                tokenizer.ReadTo("/>");
                tokenizer.Advance(2);
                var achievementName = tokenizer.ReadTo("</a>");

                tokenizer.ReadTo("<a href='/game/");
                tokenizer.Advance(15);
                var gameId = Int32.Parse(tokenizer.ReadNumber().ToString());
                tokenizer.ReadTo("/>");
                tokenizer.Advance(2);
                var gameName = tokenizer.ReadTo("</a>");

                GameTickets gameTickets;
                if (!games.TryGetValue(gameId, out gameTickets))
                {
                    gameTickets = new GameTickets { GameId = gameId, GameName = gameName.ToString() };
                    games[gameId] = gameTickets;
                }

                AchievementTickets achievementTickets;
                if (!tickets.TryGetValue(achievementId, out achievementTickets))
                {
                    achievementTickets = new AchievementTickets { AchievementId = achievementId, Game = gameTickets, AchievementName = achievementName.ToString() };
                    tickets[achievementId] = achievementTickets;
                }

                achievementTickets.OpenTickets.Add(ticketId);
                gameTickets.OpenTickets++;

                ++pageTickets;
            } while (true);
            return pageTickets;
        }
    }
}
