namespace Broccol.Backend.Data
{
    public class EventSettings
    {
        public string BaseUrl { get; set; } = "";
        public string Title { get; set; } = "";
        public string BookFile { get; set; } = "book.json";
        public int? MaximumSignups { get; set; }
        public int? VerificationTimeoutHours { get; set; } = 48;
        public string? TelegramBotToken { get; set; } = "";
        public string? RocketChatToken { get; set; } = "";
        public string? RocketChatUrl { get; set; } = "";
    }
}
