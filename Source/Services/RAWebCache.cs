using Jamiras.Components;
using Jamiras.IO.Serialization;
using Jamiras.Services;
using Jamiras.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

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
            return CallJsonAPI("API_GetGameExtended", "i=" + gameId,
                String.Format("raGame{0}.json", gameId));
        }

        public const int AchievementUnlocksPerPage = 500;

        public JsonObject GetAchievementUnlocksJson(int achievementId, int pageIndex)
        {
            return CallJsonAPI("API_GetAchievementUnlocks",
                String.Format("a={0}&o={1}&c={2}", achievementId, pageIndex * AchievementUnlocksPerPage, AchievementUnlocksPerPage),
                String.Format("raAch{0}_{1}.json", achievementId, pageIndex));
        }

        public JsonObject GetUserRankAndScore(string userName)
        {
            return CallJsonAPI("API_GetUserRankAndScore", "u=" + userName,
                String.Format("raUser{0}.json", userName));
        }

        public JsonObject GetGameTopScores(int gameId)
        {
            return CallJsonAPI("API_GetGameRankAndScore", "g=" + gameId,
                String.Format("raGameTopScores{0}.json", gameId));
        }

        public JsonObject GetGameAchievementDistribution(int gameId)
        {
            return CallJsonAPI("API_GetAchievementDistribution",
                String.Format("i={0}&h=1", gameId),
                String.Format("raGameAchDist{0}.json", gameId));
        }

        public const int OpenTicketsPerPage = 100;

        public JsonObject GetOpenTicketsJson(int pageIndex)
        {
            return CallJsonAPI("API_GetTicketData",
                String.Format("o={0}&c={1}", pageIndex * OpenTicketsPerPage, OpenTicketsPerPage),
                String.Format("raTickets{0}.json", pageIndex));
        }

        public JsonObject GetOpenTicketsForGame(int gameId)
        {
            return CallJsonAPI("API_GetTicketData",
                String.Format("g={0}&d=1", gameId),
                String.Format("raTickets_Game{0}.json", gameId));
        }

        public JsonObject GetAllHashes()
        {
            var filename = Path.Combine(Path.GetTempPath(), "raHashes.json");
            var url = "https://retroachievements.org/dorequest.php?r=hashlibrary";
            var page = GetPage(filename, url);
            return new JsonObject(page);
        }

        /// <summary>
        /// Specifies the number of hours before a downloaded file gets redownloaded.
        /// </summary>
        /// <remarks>Set to 0 for default behavior (16 hours).</remarks>
        public static int ExpireHours { get; set; }

        private JsonObject CallJsonAPI(string api, string parameters, string tempFilename)
        {
            var apiUser = _settings.UserName.Trim();
            var apiKey = _settings.ApiKey.Trim();
            if (String.IsNullOrEmpty(apiKey))
                return null;

            bool fileValid = false;
            var filename = Path.Combine(Path.GetTempPath(), tempFilename);
            if (_fileSystemService.FileExists(filename) && _fileSystemService.GetFileSize(filename) > 0)
            {
                var expireHours = ExpireHours;
                if (expireHours == 0)
                    expireHours = 16;

                fileValid = (DateTime.Now - _fileSystemService.GetFileLastModified(filename)) < TimeSpan.FromHours(expireHours);
            }

            if (!fileValid)
            {
                var logUrl = String.Format("https://retroachievements.org/API/{0}.php?z={1}", api, apiUser, apiKey);
                var url = logUrl + "&y=" + apiKey;
                if (!String.IsNullOrEmpty(parameters))
                {
                    logUrl += "&" + parameters;
                    url += "&" + parameters;
                }

                var request = _httpRequestService.CreateRequest(url);
                request.Headers["User-Agent"] = _userAgent;
                IHttpResponse response;

                var logService = ServiceRepository.Instance.FindService<ILogService>();
                var logger = logService.GetLogger("Jamiras.Core");
                if (logger.IsEnabled(LogLevel.General))
                {
                    logger.Write("Requesting " + logUrl);

                    // disable General logging to prevent capturing the user's API key
                    var oldLevel = logService.Level;
                    logService.Level = LogLevel.Warning;

                    response = _httpRequestService.Request(request);

                    logService.Level = oldLevel;
                }
                else
                {
                    response = _httpRequestService.Request(request);
                }

                if (response.Status != System.Net.HttpStatusCode.OK)
                    return null;

                using (var outputStream = _fileSystemService.CreateFile(filename))
                {
                    byte[] buffer = new byte[4096];
                    bool firstByte = true;
                    using (var stream = response.GetResponseStream())
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (firstByte)
                            {
                                if (buffer[0] != '{' && buffer[0] != '[')
                                {
                                    var error = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                                    var index = error.IndexOf('\0');
                                    if (index >= 0)
                                        error = error.Substring(0, index);
                                    ServiceRepository.Instance.FindService<IBackgroundWorkerService>().InvokeOnUiThread(() =>
                                    {
                                        MessageBoxViewModel.ShowMessage("Invalid response: " + error);
                                    });
                                    return null;
                                }

                                firstByte = false;
                            }
                            outputStream.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }

            using (var stream = _fileSystemService.OpenFile(filename, OpenFileMode.Read))
            {
                return new JsonObject(stream);
            }
        }

        private string GetPage(string filename, string url)
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
                var request = _httpRequestService.CreateRequest(url);
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
            return CallJsonAPI("API_GetGameInfoAndUserProgress",
                String.Format("u={0}&g={1}", user, gameId),
                String.Format("raUser{0}_Game{1}.json", user, gameId));
        }

        public JsonObject GetUserMasteriesJson(string user)
        {
            return CallJsonAPI("API_GetUserCompletedGames", "u=" + user,
                String.Format("raUser{0}_Masteries.json", user));
        }
    }
}
