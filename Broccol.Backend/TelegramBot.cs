using Broccol.Backend.Data;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection.Metadata.Ecma335;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Broccol.Backend
{
    public class TelegramBot
    {
        private readonly TelegramBotClient Client;
        private readonly CancellationTokenSource Cts = new ();
        private readonly WebApplication app;
        private readonly RsvpService Service;
        private List<TelegramSession> Sessions { get; set; } = new();
        private User Me { get; set; }
        private DateTime StartTime = DateTime.UtcNow;

        public TelegramBot(IConfiguration configuration, RsvpService rsvpService)
        {
            var eventSettings = configuration.GetSection("EventSettings").Get<EventSettings>()!;
            if (eventSettings == null)
            {
                throw new ArgumentNullException("EventSettings is null.");
            }
            Client = InitializeClient(eventSettings);
            Me = Client.GetMeAsync().Result;
            Service = rsvpService;
            StartReceiving();
        }

        private TelegramBotClient InitializeClient(EventSettings settings)
        {
            if (settings.TelegramBotToken == null)
            {
                throw new InvalidOperationException(nameof(settings.TelegramBotToken) + " of EventSettings is null.");
            }
            return new TelegramBotClient(settings.TelegramBotToken);
        }

        public async void StartReceiving()
        {

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
            };

            Client.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: Cts.Token
            );
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not { } message)
                return;
            // Only process text messages
            if (message.Text is not { } messageText)
                return;
            if (StartTime.CompareTo(update.Message?.Date) > 0)
            {
                Console.WriteLine("asdasd");
                return;
            }
            var chatId = message.Chat.Id;

            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

            var session = Sessions.FirstOrDefault(s => s.Chat.Id == message.Chat.Id);
            if (session == null)
            {
                session = new TelegramSession(message.Chat, message.From);
                Sessions.Add(session);
            }

            var responses = session.GetResponses(messageText, Service);
            foreach (var response in responses)
            {
                IReplyMarkup keyboard = response.Item2 != null 
                    ? response.Item2 
                    : new ReplyKeyboardRemove();

                Thread.Sleep(400);
                Telegram.Bot.Types.Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: response.Item1,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }

            if (session != null && session.Deletable)
            {
                Sessions.Remove(session);
            }
        }
        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }
    }

    public class TelegramSession
    {
        public Chat Chat { get; set; }
        public string Language { get; set; } = "en";
        public int State { get; set; } = 0;
        public int InvalidAnswers { get; set; }
        public string[] CurrentAnswerOptions { get; set; } = new[] { "/start" };
        public User? From { get; set; }
        public bool Deletable { get; set; } = false;

        public TelegramSession(Chat chat, User? from)
        {
            Chat = chat;
            From = from;
        }

        private string HumanizeString(string str)
        {
            var result = "";
            foreach (var c in str.Replace("/", ""))
            {
                if (char.IsUpper(c))
                {
                    result += " ";
                }
                result += char.ToLower(c);
            }
            return result.Trim();
        }

        private ReplyKeyboardMarkup? ReplyKeyboardMarkup
        {
            get
            {
                var sadasd = CurrentAnswerOptions != null && CurrentAnswerOptions.Any() ? 
                    new ReplyKeyboardMarkup(CurrentAnswerOptions.Select(o => new KeyboardButton(o))) 
                    : null;
                return sadasd;
            }
        }

        public (string,ReplyKeyboardMarkup?)[] GetResponses(string messageFromUser, RsvpService service)
        {
            var existingEntry = service.GetEntryByEmailOrTg(From.Id.ToString());
            switch (State)
            {
                case 0:
                    {
                        // greets and asks for language
                        
                        if (existingEntry == null)
                        {
                            InvalidAnswers = 0;
                            State++;
                            CurrentAnswerOptions = new[] { "/English", "/Finnish" };
                            return new (string, ReplyKeyboardMarkup?)[] { (Messages.MsgGreeting.Get(Language), null), (Messages.MsgLanguageQuestion.Get(Language), ReplyKeyboardMarkup)  };
                        }
                        else
                        {
                            Language = existingEntry.Language;
                            InvalidAnswers = 0;
                            State++;
                            CurrentAnswerOptions = Language == "en" 
                                ? new[] { "/CancelMySignup", "/VaihdaKieliSuomeksi" }
                                : new[] { "/PeruutaIlmoittautuminen", "/ChangeLanguageToEnglish" };
                            var msg = string.Format(Messages.MsgGreetingExistingEntry.Get(Language), existingEntry.Name.Split(" ")[0]);
                            return new (string, ReplyKeyboardMarkup?)[] 
                            { 
                                (msg, ReplyKeyboardMarkup)
                            };
                        }
                    }
                case 1:
                    {
                        // expects language answer and on success, asks for privacy interest
                        if (existingEntry == null)
                        {
                            switch (messageFromUser)
                            {
                                case "/English":
                                    {
                                        InvalidAnswers = 0;
                                        State++;
                                        Language = "en";
                                        CurrentAnswerOptions = new[] { "/Yes", "/No" };
                                        return new[] { (Messages.MsgLanguageOkResponse.Get(Language),null), (Messages.MsgAskPrivacyInterest.Get(Language), ReplyKeyboardMarkup) };
                                    }
                                case "/Finnish":
                                    {
                                        InvalidAnswers = 0;
                                        State++;
                                        Language = "fi";
                                        CurrentAnswerOptions = new[] { "/Kyllä", "/En" };
                                        return new[] { (Messages.MsgLanguageOkResponse.Get(Language), null), (Messages.MsgAskPrivacyInterest.Get(Language), ReplyKeyboardMarkup) };
                                    }
                                default:
                                    {
                                        InvalidAnswers++;
                                        var msg = Messages.MsgInvalidResponse.Get(Language) + "\n" + string.Join("\n", CurrentAnswerOptions);

                                        return new[] { (msg, ReplyKeyboardMarkup) };
                                    }
                            }
                        } else
                        {
                            Language = existingEntry.Language;
                            switch (messageFromUser)
                            {
                                case "/CancelMySignup":
                                case "/PeruutaIlmoittautuminen":
                                    InvalidAnswers = 0;
                                    State++;
                                    CurrentAnswerOptions = new[] { "/Yes", "/No" };
                                    return new[] { (Messages.AskConfirmCancel.Get(Language), ReplyKeyboardMarkup) };
                                case "/VaihdaKieliSuomeksi":
                                    InvalidAnswers = 0;
                                    Language = "fi";
                                    CurrentAnswerOptions = new[] { "/start" };
                                    service.UpdateLanguage(From.Id.ToString(), "fi");
                                    Deletable = true;
                                    return new[] { (Messages.MsgLanguageOkResponse.Get(Language), ReplyKeyboardMarkup) };
                                case "/ChangeLanguageToEnglish":
                                    InvalidAnswers = 0;
                                    Language = "en";
                                    CurrentAnswerOptions = new[] { "/start" };
                                    service.UpdateLanguage(From.Id.ToString(), "en");
                                    Deletable = true;
                                    return new[] { (Messages.MsgLanguageOkResponse.Get(Language), ReplyKeyboardMarkup) };
                                default:
                                    var optionsAsString = CurrentAnswerOptions.Any() ? "\n\n" + string.Join("\n", CurrentAnswerOptions) : "";
                                    return new[] { ($"🤔?{optionsAsString}", ReplyKeyboardMarkup) };
                            }
                        }
                    }
                case 2:
                    {
                        // expects answer to privacy interest and asks for full name.
                        if (existingEntry == null)
                        {
                            switch (messageFromUser)
                            {
                                case "/Yes":
                                case "/Kyllä":
                                    {
                                        InvalidAnswers = 0;
                                        State++;
                                        return new (string, ReplyKeyboardMarkup?)[] { (Messages.PrivacyPolicy.Get(Language), null), (Messages.MsgNameQuestion.Get(Language),null) };
                                    }
                                case "/No":
                                case "/En":
                                    {
                                        InvalidAnswers = 0;
                                        State++;
                                        return new (string, ReplyKeyboardMarkup?)[] { (Messages.MsgNameQuestion.Get(Language), null) };
                                    }
                                default:
                                    {
                                        InvalidAnswers++;
                                        return new (string, ReplyKeyboardMarkup?)[] { (Messages.MsgInvalidResponse.Get(Language) + "\n" + string.Join("\n", CurrentAnswerOptions),null) };
                                    }
                            }
                        } else
                        {
                            switch (messageFromUser)
                            {
                                case "/Yes":
                                case "/Kyllä":
                                    {
                                        InvalidAnswers = 0;
                                        Deletable = true;
                                        CurrentAnswerOptions = new[] { "/start" };
                                        var result = service.DeleteByEmail(From.Id.ToString());
                                        var msg = Messages.CancelConfirmed.Get(Language);
                                        return new[] { (msg, ReplyKeyboardMarkup) };
                                    }
                                case "/No":
                                case "/Ei":
                                    {
                                        InvalidAnswers = 0;
                                        CurrentAnswerOptions = new[] { "/start" };
                                        Deletable = true;
                                        return new[] { (Messages.CancelConfirmCanceled.Get(Language), ReplyKeyboardMarkup) };
                                    }
                                default:
                                    {
                                        InvalidAnswers++;
                                        return new[] { (Messages.MsgInvalidResponse.Get(Language) + "\n" + string.Join("\n", CurrentAnswerOptions), ReplyKeyboardMarkup) };
                                    }
                            }
                        }
                    }
                case 3:
                    {
                        // expects for full name and if valid, sends the final message
                        var nameInvalidity = service.NameInvalidity(messageFromUser);
                        if (nameInvalidity.Success)
                        {
                            InvalidAnswers = 0;
                            State++;
                            var entry = new RsvpEntry()
                            {
                                Name = messageFromUser,
                                EmailOrTgUserId = From.Id.ToString(),
                                TgUserName = From.Username,
                                Language = Language,
                                MessagingMethod = MessagingMethod.Telegram
                            };
                            var additionResult = service.AddEntry(entry, "Telegram");
                            Deletable = true;
                            CurrentAnswerOptions = new[] { "/start" };
                            return additionResult.Success
                                ? new (string, ReplyKeyboardMarkup ?)[]
                                {
                                    (string.Format(Messages.MsgNameOkResponse.Get(Language), messageFromUser.Split(" ")[0]), null),
                                    (Messages.FinalMessage.Get(Language), ReplyKeyboardMarkup)
                                }
                                : new (string, ReplyKeyboardMarkup?)[]
                                {
                                    (string.Format(Messages.EntryAdditionFailed.Get(Language), additionResult.Message), null),
                                };
                        } else
                        {
                            InvalidAnswers++;
                            return new (string, ReplyKeyboardMarkup?)[] { (Messages.MsgNameInvalidResponse.Get(Language) + $"\n({nameInvalidity.Message})",null) };
                        }
                    }
                default:
                    {
                        Deletable = true;
                        return new (string, ReplyKeyboardMarkup?)[] { ("Go away! We're done already.", null) };
                    }
            }
        }
    }

    public static class Messages
    {
        public static readonly BilingualString MsgGreeting = new(
            "Hi! I handle the RSVP of Pesutupa.",
            "Moi! Minä hoidan Pesutuvan nimilistaa.");
        public static readonly BilingualString MsgGreetingExistingEntry = new(
            "Hi {0}! What's up?",
            "Moi {0}! Mitä kuuluu?");
        public static readonly BilingualString AskConfirmCancel = new(
            "Are you sure you want to cancel? 😰",
            "Oletko varma, että haluat peruuttaa ilmoittautumisesi? 😰"
            );
        public static readonly BilingualString CancelConfirmed = new(
            "Allright. Your signup and the related data is deleted.",
            "Okei. Ilmoittautumisesi ja kaikki siihen liittyvä data on nyt poistettu."
            );
        public static readonly BilingualString CancelConfirmCanceled = new(
            "Whew! Ok, good. See you at the party 👍",
            "Huh! Okei, hyvä homma. Nähdään bileissä 👍"
            );
        public static readonly BilingualString MsgLanguageQuestion =
            new(
                "Would you like to be communicated to in 🇬🇧 English or in 🇫🇮 Finnish?",
                "Haluatko sinulle kommunikoitavan 🇬🇧 englanniksi vai 🇫🇮 suomeksi?");
        public static readonly BilingualString MsgLanguageOkResponse =
            new(
                "Ok, English it is!",
                "Ok, suomella mennään.");
        public static readonly BilingualString MsgInvalidResponse =
            new(
                "Invalid answer. Try again.",
                "Virheellinen vastaus. Yritä uudestaan.");
        public static readonly BilingualString MsgAskPrivacyInterest =
            new(
                "By signing up to Pesutupa, you agree to my privacy policy.\nBefore we continue, would you like to read my privacy policy?",
                "Hyväksyt tietosuojaehtoni ilmoittautumalla Pesutupaan. Ennen kuin jatketaan, haluatko lukea tietosuojaehtoni?");
        public static readonly BilingualString PrivacyPolicy = new(
            "What will be stored: The metadata of this chat, your full name and your language preference.\n" +
            "How long it will be stored: From the beginning of this chat, to the end of the event.\n" +
            "Who stores and accesses the data: A person or people delegated to handle the RSVP of the event.",
            "Mitä tallennetaan: Tämän chatin metadata, koko nimesi ja kielivalintasi.\n" +
            "Miten kauan pidetään tallessa: Tämän chatin alusta aina tapahtuman päättymiseen asti.\n" +
            "Kuka tallentaa ja voi lukea tätä dataa: Henkilö tai henkilöt, jotka ovat osoitettu hoitamaan tapahtuman nimilistaa.");
        public static readonly BilingualString MsgTgNameQuestion = new(
            "Is {0} {1} your full real name?",
            "Onko {0} {1} oikea koko nimesi?");
        public static readonly BilingualString MsgNameQuestion = new(
            "What's your full name?",
            "Mikä on koko nimesi?");
        public static readonly BilingualString MsgNameInvalidResponse = new(
            "That's an invalid name. Try writing your full name again.",
            "Nimi on virheellinen. Yritä kirjoittaa koko nimesi uudestaan.");
        public static readonly BilingualString MsgNameOkResponse = new(
            "Nice to meet you, {0}.",
            "Hauska tavata, {0}.");
        public static readonly BilingualString TooManyInvalidAnswers = new(
            "Too many invalid answers. I quit.\nYou can restart this with /start",
            "Liian monta virheellistä vastausta. Minä luovutan.\nVoit aloittaa uudestaan komennolla /start");
        public static readonly BilingualString EntryAdditionFailed = new(
            "Couldn't sign you up. Reason: {0}",
            "Ilmoittautuminen epäonnistui. Syy: {0}");
        public static readonly BilingualString FinalMessage = new(
            "Ok, you've now signed in to Pesutupa RSVP.\nDon't close this chat, as I'll be sending you info about the event.\nOh and talk me again if you want to cancel your signup.",
            "Okei, olet nyt Pesutuvan nimilistalla.\nEthän sulje tätä keskustelua, sillä minä lähetän sinulle infoa bileistä tätä kautta.\nAi niin: juttele minulle uudestaan jos haluat perua ilmoittautumisesi.");
    }
}
