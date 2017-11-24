using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
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

            int pageTickets;
            int page = 0;
            do
            {
                var ticketsPage = GetTicketsPage(page);
                if (ticketsPage == null)
                    return;

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

                    tokenizer.ReadTo("<a href='/Game/");
                    tokenizer.Advance(15);
                    var gameId = Int32.Parse(tokenizer.ReadNumber().ToString());

                    GameTickets gameTickets;
                    if (!games.TryGetValue(gameId, out gameTickets))
                    {
                        gameTickets = new GameTickets { GameId = gameId };

                        tokenizer.ReadTo("/>");
                        tokenizer.Advance(2);
                        gameTickets.GameName = tokenizer.ReadTo("</a>").ToString();

                        games[gameId] = gameTickets;
                    }

                    tokenizer.ReadTo("<a href='/Achievement/");
                    tokenizer.Advance(22);
                    var achievementId = Int32.Parse(tokenizer.ReadNumber().ToString());

                    AchievementTickets achievementTickets;
                    if (!tickets.TryGetValue(achievementId, out achievementTickets))
                    {
                        achievementTickets = new AchievementTickets { AchievementId = achievementId, Game = gameTickets };

                        tokenizer.ReadTo("/>");
                        tokenizer.Advance(2);
                        achievementTickets.AchievementName = tokenizer.ReadTo("</a>").ToString();

                        tickets[ticketId] = achievementTickets;
                    }

                    achievementTickets.OpenTickets.Add(ticketId);
                    gameTickets.OpenTickets++;

                    ++pageTickets;
                } while (true);

                ++page;
                totalTickets += pageTickets;
                Progress.Current++;
            } while (pageTickets == 100);

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

            Progress.Label = String.Empty;
        }

        private string GetTicketsPage(int pageIndex)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raTickets{0}.html", pageIndex));
            if (!_fileSystemService.FileExists(filename))
            {
                var url = "http://retroachievements.org/ticketmanager.php";
                if (pageIndex > 0)
                    url += "?u=&t=1&o=" + (pageIndex * 100);

                var request = new HttpRequest(url);
                var response = _httpRequestService.Request(request);
                if (response.Status != System.Net.HttpStatusCode.OK)
                    return null;

                using (var outputStream = _fileSystemService.CreateFile(filename))
                {
                    byte[] buffer = new byte[4096];
                    using (var stream = response.GetResponseStream())
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                            outputStream.Write(buffer, 0, bytesRead);
                    }
                }
            }

            using (var stream = new StreamReader(_fileSystemService.OpenFile(filename, OpenFileMode.Read)))
            {
                return stream.ReadToEnd();
            }
        }
    }
}
