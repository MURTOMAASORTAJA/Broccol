namespace Broccol.Backend.Data
{
    public class AdminBotSettings
    {
        public BotAdmin Owner { get; set; }
    }

    public class BotAdmin
    {
        public string UserName { get; set; }
        public string UserId { get; set; }
    }
}
