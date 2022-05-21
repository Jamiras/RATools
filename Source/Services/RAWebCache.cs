using Jamiras.Components;
using Jamiras.IO.Serialization;
using Jamiras.Services;
using System;
using System.Diagnostics;
using System.IO;

namespace RATools.Services
{
    public class RAWebCache
    {
        private RAWebCache()
        {
            _fileSystemService = ServiceRepository.Instance.FindService<IFileSystemService>();
            _httpRequestService = ServiceRepository.Instance.FindService<IHttpRequestService>();
            _settings = ServiceRepository.Instance.FindService<ISettings>();
        }

        private readonly IFileSystemService _fileSystemService;
        private readonly IHttpRequestService _httpRequestService;
        private readonly ISettings _settings;
 
        public static RAWebCache Instance
        {
            get { return _instance ?? (_instance = new RAWebCache()); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static RAWebCache _instance;

        public string GetGamePage(int gameId)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raGame{0}.html", gameId));
            var url = String.Format("https://retroachievements.org/game/{0}?v=1", gameId);
            return GetPage(filename, url, false);
        }

        public string GetAchievementPage(int achievementId)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raAch{0}.html", achievementId));
            var url = String.Format("https://retroachievements.org/achievement/{0}", achievementId);
            return GetPage(filename, url, false);
        }

        public string GetUserPage(string userName)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raUser{0}.html", userName));
            var url = String.Format("https://retroachievements.org/user/{0}", userName);
            return GetPage(filename, url, false);
        }

        public JsonObject GetOpenTicketsJson(int pageIndex)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raTickets{0}.json", pageIndex));
            var url = String.Format("https://retroachievements.org/API/API_GetTicketData.php?z={0}&y={1}&o={2}&c=100", apiUser, apiKey, pageIndex * 100);
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

        public string GetAllHashes()
        {
            var filename = Path.Combine(Path.GetTempPath(), "raHashes.json");
            var url = "https://retroachievements.org/dorequest.php?r=hashlibrary";
            return GetPage(filename, url, false);
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
