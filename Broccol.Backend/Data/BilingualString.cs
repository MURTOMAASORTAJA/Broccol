namespace Broccol.Backend.Data
{
    public class BilingualString
    {
        public string English {get; set;}
        public string Finnish { get; set; }

        public string Get(string language)
        {
            return language switch
            {
                "fi" => Finnish,
                "en" => English,
                _ => English
            };
        }

        public BilingualString() { }
        public BilingualString(string english, string finnish) 
        {
            English = english;
            Finnish = finnish;
        }
    }
}
