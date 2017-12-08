using Jamiras.Components;
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
        }

        private readonly IFileSystemService _fileSystemService;
        private readonly IHttpRequestService _httpRequestService;
 
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
                url += "?u=&t=1&o=" + (pageIndex * 100);

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
    }
}
