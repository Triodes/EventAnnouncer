using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;

namespace EventAnnouncer
{
    public class Announcer
    {
        private readonly HttpClient client = new HttpClient();

        private readonly ILogger log;

        private readonly string CALENDAR_ID;
        private readonly string WEBHOOK_ID;

        private readonly string CALENDAR_APIKEY;
        private const string APPLICATION_NAME = "EventAnnouncer";

        [FunctionName("EventAnnouncer")]
        public static Task RunAsync([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();


            var announcer = new Announcer(
                log,
                config["CALENDAR_ID"],
                config["CALENDAR_APIKEY"],
                config["WEBHOOK_ID"]
            );
            announcer.ProcessCalendarEvents();

            return Task.CompletedTask;
        }

        public Announcer(ILogger log, string calendarId, string calendarApiKey, string webhookId)
        {
            this.log = log;
            this.CALENDAR_ID = calendarId;
            this.CALENDAR_APIKEY = calendarApiKey;
            this.WEBHOOK_ID = webhookId;
        }

        private void ProcessCalendarEvents()
        {
            // Create Google Calendar API service.
            var service = new CalendarService(new BaseClientService.Initializer()
            {
                ApiKey = CALENDAR_APIKEY,
                ApplicationName = APPLICATION_NAME,
            });

            // Define parameters of request.
            EventsResource.ListRequest request = service.Events.List(CALENDAR_ID);
            request.TimeMin = DateTime.UtcNow;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.MaxResults = 5;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            // List events.
            Events events = request.Execute();
            log.LogInformation($"Found {events.Items.Count} events");

            DateTime now = DateTime.Now;

            if (events.Items != null && events.Items.Count > 0)
            {
                foreach (Event eventItem in events.Items)
                {
                    DateTime? nDateTime = eventItem.Start.DateTime;
                    if (!nDateTime.HasValue)
                        continue;

                    DateTime dateTime = nDateTime.Value;

                    DateTime advance1 = dateTime.Subtract(TimeSpan.FromMinutes(1));
                    DateTime advance30 = dateTime.Subtract(TimeSpan.FromMinutes(30));

                    double diff1 = (advance1 - now).TotalMinutes;
                    double diff30 = (advance30 - now).TotalMinutes;
                    
                    if (diff1 >= -1d && diff1 < 0d)
                    {
                        string message = ContstructMessage("soon", eventItem);
                        SendDiscordMessage(message);
                    }

                    if (diff30 >= -1d && diff30 < 0d)
                    {
                        string message = ContstructMessage("in 30 minutes", eventItem);
                        SendDiscordMessage(message);
                    }
                }
            }
        }

        private string ContstructMessage(string advance, Event e)
        {
            StringBuilder builder = new StringBuilder();
            string time = e.Start.DateTime.Value.ToUniversalTime().ToString("HH:mm");
            builder.Append($"The next clan event will begin **{advance}**. The event is:\n\n**{time} - {e.Summary}**\n*{e.Description}*\n\n");
            if (e.Location != null)
                builder.Append($"We'll be meeting up at: *{e.Location}*\n\n");
            builder.Append("See you there!");

            return builder.ToString();
        }

        private async void SendDiscordMessage(string message)
        {
            DiscordMessage discordMessage = new DiscordMessage(message);
            StringContent content = new StringContent(JsonConvert.SerializeObject(discordMessage), Encoding.UTF8, "application/json");

            string url = $"https://discord.com/api/webhooks/{WEBHOOK_ID}";

            var response = await client.PostAsync(url, content);

            log.LogInformation(response.Content.ReadAsStringAsync().Result);
        }
    }

    struct DiscordMessage
    {
        public readonly string content;

        public DiscordMessage(string content)
        {
            this.content = content;
        }
    }
}
