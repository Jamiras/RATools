using Jamiras.Components;
using Jamiras.IO.Serialization;
using Jamiras.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace RATools.Services
{
    public class RAWebCache
    {
        private RAWebCache()
        {
            _fileSystemService = ServiceRepository.Instance.FindService<IFileSystemService>();
            _httpRequestService = ServiceRepository.Instance.FindService<IHttpRequestService>();
            _settings = ServiceRepository.Instance.FindService<ISettings>();

            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if (version.EndsWith(".0"))
                version = version.Substring(0, version.Length - 2);
            _userAgent = String.Format("{0}/{1} ({2})", GetAssemblyAttribute<AssemblyTitleAttribute>().Title,
                version, System.Environment.OSVersion.VersionString);
        }

        private readonly IFileSystemService _fileSystemService;
        private readonly IHttpRequestService _httpRequestService;
        private readonly ISettings _settings;

        private static T GetAssemblyAttribute<T>()
            where T : Attribute
        {
            return (T)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(T), false);
        }

        public static RAWebCache Instance
        {
            get { return _instance ?? (_instance = new RAWebCache()); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static RAWebCache _instance;

        private string _userAgent;

        public JsonObject GetGameJson(int gameId)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raGame{0}.json", gameId));
            var url = String.Format("https://retroachievements.org/API/API_GetGameExtended.php?z={0}&y={1}&i={2}", apiUser, apiKey, gameId);
            var page = GetPage(filename, url, false);
            return new JsonObject(page);
        }

        public const int AchievementUnlocksPerPage = 500;

        public JsonObject GetAchievementUnlocksJson(int achievementId, int pageIndex)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raAch{0}_{1}.json", achievementId, pageIndex));
            var url = String.Format("https://retroachievements.org/API/API_GetAchievementUnlocks.php?z={0}&y={1}&a={2}&o={3}&c={4}",
                apiUser, apiKey, achievementId, pageIndex * AchievementUnlocksPerPage, AchievementUnlocksPerPage);
            var page = GetPage(filename, url, false);
            return new JsonObject(page);
        }

        public JsonObject GetUserRankAndScore(string userName)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raUser{0}.json", userName));
            var url = String.Format("https://retroachievements.org/API/API_GetUserRankAndScore.php?z={0}&y={1}&u={2}", apiUser, apiKey, userName);
            var page = GetPage(filename, url, false);
            return new JsonObject(page);
        }

        public JsonObject GetGameTopScores(int gameId)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raGameTopScores{0}.json", gameId));
            var url = String.Format("https://retroachievements.org/API/API_GetGameRankAndScore.php?z={0}&y={1}&g={2}", apiUser, apiKey, gameId);
            var page = GetPage(filename, url, false);
            return new JsonObject(page);
        }

        public JsonObject GetGameAchievementDistribution(int gameId)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raGameAchDist{0}.json", gameId));
            var url = String.Format("https://retroachievements.org/API/API_GetAchievementDistribution.php?z={0}&y={1}&i={2}&h=1", apiUser, apiKey, gameId);
            var page = GetPage(filename, url, false);
            return new JsonObject(page);
        }

        public const int OpenTicketsPerPage = 100;

        public JsonObject GetOpenTicketsJson(int pageIndex)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raTickets{0}.json", pageIndex));
            var url = String.Format("https://retroachievements.org/API/API_GetTicketData.php?z={0}&y={1}&o={2}&c={3}", 
                apiUser, apiKey, pageIndex * OpenTicketsPerPage, OpenTicketsPerPage);
            var page = GetPage(filename, url, false);
            return new JsonObject(page);
        }

        public string GetOpenTicketsForGame(int gameId)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raGameTickets{0}.html", gameId));
            var url = "https://retroachievements.org/ticketmanager.php?ampt=1&g=" + gameId;
            return GetPage(filename, url, true);
        }

        public string GetTicketPage(int ticketId)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raTicket{0}.html", ticketId));
            var url = "https://retroachievements.org/ticketmanager.php?i=" + ticketId;
            return GetPage(filename, url, true);
        }

        public JsonObject GetAllHashes()
        {
            var filename = Path.Combine(Path.GetTempPath(), "raHashes.json");
            var url = "https://retroachievements.org/dorequest.php?r=hashlibrary";
            var page = GetPage(filename, url, false);
            return new JsonObject(page);
        }

        /// <summary>
        /// Specifies the number of hours before a downloaded file gets redownloaded.
        /// </summary>
        /// <remarks>Set to 0 for default behavior (16 hours).</remarks>
        public static int ExpireHours { get; set; }

        private string GetPage(string filename, string url, bool requiresCookie)
        {
            bool fileValid = false;
            if (_fileSystemService.FileExists(filename))
            {
                var expireHours = ExpireHours;
                if (expireHours == 0)
                    expireHours = 16;

                fileValid = (DateTime.Now - _fileSystemService.GetFileLastModified(filename)) < TimeSpan.FromHours(expireHours);
            }

            if (!fileValid)
            {
                var request = new HttpRequest(url);
                if (requiresCookie)
                {
                    var settings = ServiceRepository.Instance.FindService<ISettings>();
                    if (String.IsNullOrEmpty(settings.Cookie) || String.IsNullOrEmpty(settings.UserName))
                        return null;
                    request.Headers["Cookie"] = String.Format("RA_User={0}; RA_Cookie={1}", settings.UserName, settings.Cookie);
                }

                request.Headers["User-Agent"] = _userAgent;

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

        public JsonObject GetUserGameMasteryJson(string user, int gameId)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raUser{0}_Game{1}.json", user, gameId));
            var url = String.Format("https://retroachievements.org/API/API_GetGameInfoAndUserProgress.php?z={0}&y={1}&u={2}&g={3}", apiUser, apiKey, user, gameId);
            var page = GetPage(filename, url, false);
            return new JsonObject(page);
        }
    }
}
