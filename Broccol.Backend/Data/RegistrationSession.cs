namespace Broccol.Backend.Data
{
    public class RegistrationSession
    {
        public RsvpEntry Entry { get; set; } = new RsvpEntry();
        public DateTime Created { get; set; }
        public DateTime? EmailSent { get; set; }
        public string MagicCode { get; set; } = "";
    }
}
