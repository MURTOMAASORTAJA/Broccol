using Broccol.Backend.Data;

namespace Broccol.Backend
{
    public class RocketChatAnnouncer
    {
        private string Token { get; set; }
        private string Url { get; set; }

        HttpClient client { get; set; }
        public RocketChatAnnouncer(EventSettings settings, HttpClient client)
        {
            Token = settings.RocketChatToken!;
            Url = settings.RocketChatUrl!;
            this.client = client;
        }

        public async Task SendMessage(string message)
        {
            var payload = new { text = message };
            var fullUri = new Uri(new Uri(Url), Token);
            try
            {
                await client.PostAsync(fullUri, JsonContent.Create(payload));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message to rocket chat: {ex.Message}");
            }
        }
    }
}
