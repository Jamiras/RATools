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
            var url = String.Format("http://retroachievements.org/Game/{0}", gameId);
            return GetPage(filename, url);
        }

        public string GetAchievementPage(int achievementId)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raAch{0}.html", achievementId));
            var url = String.Format("http://retroachievements.org/Achievement/{0}", achievementId);
            return GetPage(filename, url);
        }

        public string GetOpenTicketsPage(int pageIndex)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raTickets{0}.html", pageIndex));
            var url = "http://retroachievements.org/ticketmanager.php";
            if (pageIndex > 0)
                url += "?u=&t=2041&o=" + (pageIndex * 100);

            return GetPage(filename, url);
        }

        public string GetOpenTicketsForGame(int gameId)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raGameTickets{0}.html", gameId));
            var url = "http://retroachievements.org/ticketmanager.php?ampt=1&g=" + gameId;
            return GetPage(filename, url);
        }

        public string GetTicketPage(int ticketId)
        {
            var filename = Path.Combine(Path.GetTempPath(), String.Format("raTicket{0}.html", ticketId));
            var url = "http://retroachievements.org/ticketmanager.php?i=" + ticketId;
            return GetPage(filename, url);
        }

        private string GetPage(string filename, string url)
        {
            bool fileValid = false;
            if (_fileSystemService.FileExists(filename))
                fileValid = (DateTime.Now - _fileSystemService.GetFileLastModified(filename)) < TimeSpan.FromHours(16);

            if (!fileValid)
            {
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

        public JsonObject GetUserGameMasteryJson(string user, int gameId)
        {
            var apiUser = _settings.UserName;
            var apiKey = _settings.ApiKey;
            if (String.IsNullOrEmpty(apiKey))
                return null;

            var filename = Path.Combine(Path.GetTempPath(), String.Format("raUser{0}_Game{1}.html", user, gameId));
            var url = String.Format("http://retroachievements.org/API/API_GetGameInfoAndUserProgress.php?z={0}&y={1}&u={2}&g={3}", apiUser, apiKey, user, gameId);
            var page = GetPage(filename, url);
            return new JsonObject(page);
        }
    }
}
