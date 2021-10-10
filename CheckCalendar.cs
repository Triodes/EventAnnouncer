using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace EventAnnouncer
{
    public static class CheckCalendar
    {
        private static readonly HttpClient client = new HttpClient();

        [FunctionName("CheckCalendar")]
        public static void Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log)
        {
            string message = "\{message: C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
