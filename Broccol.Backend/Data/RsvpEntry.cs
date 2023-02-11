namespace Broccol.Backend.Data
{
    public class RsvpEntry : IEquatable<RsvpEntry>
    {
        public string Name { get; set; } = "";
        public string EmailOrTgUserId { get; set; } = "";
        public string? TgUserName { get; set; }
        public MessagingMethod MessagingMethod { get; set; } = MessagingMethod.Email;
        public string Language { get; set; } = "en";

        public bool Equals(RsvpEntry? other)
        {
            if (this == null && other == null)
            {
                return true;
            } else if (this != null && other != null)
            {
                return Name == other.Name && EmailOrTgUserId == other.EmailOrTgUserId;
            }
            return false;
        }
    }
    public enum MessagingMethod
    {
        Email, Telegram
    }
}
