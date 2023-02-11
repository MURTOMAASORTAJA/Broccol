using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Broccol.Backend.Data
{
    /// <summary>
    /// A server-side service handling RSVP stuff.
    /// </summary>
    public class RsvpService
    {
        private const string InvalidNameCharacters = "1234567890!\"#¤%&/()[]{}\\/|@£€$<>^\n\r\t\0,.;:";
        private const string InvalidEmailCharacters = "#¤%&/()[]{} \\/|<>$€^\n\r\t\0";
        private readonly EventSettings eventSettings;
        private readonly EventBook book;
        private readonly string bookFilePath;
        private bool saving = false;
        private readonly RocketChatAnnouncer? rocket;
        private readonly List<RegistrationSession> sessions = new();

        public RsvpService(EventSettings settings, RocketChatAnnouncer? rocketChatAnnouncer)
        {
            eventSettings = settings;
            if (eventSettings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            rocket = rocketChatAnnouncer;
            bookFilePath = GetBookFilePath();
            book = InitializeEventBook();
        }

        public void HandleBookChange()
        {
            if (saving) Thread.Sleep(500);
            saving = true;
            SaveBook();
            saving = false;
        }

        public string EventTitle => eventSettings.Title;

        private string GetBookFilePath()
        {
            var currentDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
            return Path.Combine(currentDir, eventSettings.BookFile);
        }

        private EventBook InitializeEventBook()
        {
            if (!File.Exists(bookFilePath))
            {
                var book = new EventBook();
                SaveBook(book);
                return book;
            } else
            {
                var json = File.ReadAllText(bookFilePath);
                var book = JsonSerializer.Deserialize<EventBook>(json);
                if (book == null)
                {
                    throw new Exception("The EventBook in book.json is null.");
                }
                return book;
            }
        }

        private void SaveBook(EventBook book)
        {
            if (book == null)
            {
                throw new ArgumentNullException(nameof(book));
            }

            File.WriteAllText(bookFilePath, JsonSerializer.Serialize(book));
        }
        private void SaveBook() => SaveBook(book);

        /// <summary>
        /// Attempts to add a new entry. If success, returns null and if failure, returns an explanation of failure.
        /// </summary>
        public RsvpResult AddEntry(RsvpEntry entry, string sender = "")
        {
            if (entry == null)
            {
                return Failure("Entry is null.");
            }

            var nameInvalidity = NameInvalidity(entry.Name);
            if (nameInvalidity.Failure)
            {
                return Failure(nameInvalidity.Message!);
            }

            if (entry.MessagingMethod == MessagingMethod.Email)
            {
                var emailInvalidity = EmailInvalidity(entry.EmailOrTgUserId);
                if (emailInvalidity.Failure)
                {
                    return Failure(emailInvalidity.Message!);
                }
            }

            book.Entries.Add(entry);
            Announce($"New signup via {sender}: {entry.Name}");
            HandleBookChange();
            return Success();
        }

        public bool UpdateLanguage(string emailOrTgId, string newLanguage)
        {
            var existing = GetEntryByEmailOrTg(emailOrTgId);
            if (existing == null)
            {
                return false;
            } else
            {
                existing.Language = newLanguage;
                HandleBookChange();
                return true;
            }
        }

        public RsvpEntry? GetEntryByEmailOrTg(string emailOrTgUserId)
        {
            return book.Entries.FirstOrDefault(e => e.EmailOrTgUserId == emailOrTgUserId);
        }

        public string[] ListNames()
        {
            return book.Entries.Select(e => e.Name)?.ToArray() ?? Array.Empty<string>();
        }

        public string? DeleteByName(string name)
        {
            var deletable = book.Entries.Where(e => e.Name == name).ToArray();
            if (!deletable.Any())
            {
                return "No entries with that name.";
            }

            var deletedEntries = new List<RsvpEntry>();
            RsvpEntry? failedToDelete = null;
            foreach (var entry in deletable)
            {
                if (book.Entries.Remove(entry))
                {
                    deletedEntries.Add(entry);
                } else
                {
                    failedToDelete = entry;
                    break;
                }
            }

            if (failedToDelete != null)
            {
                foreach (var deletedEntry in deletedEntries)
                {
                    book.Entries.Add(deletedEntry);
                }

                return "Deletion failed.";
            } else
            {
                if (deletedEntries.Any())
                {
                    Announce($"Signup removed: {name}");
                    HandleBookChange();
                }
                return null;
            }
        }

        public string? DeleteByEmail(string email)
        {
            var deletable = book.Entries.Where(e => e.EmailOrTgUserId == email).ToArray();
            if (!deletable.Any())
            {
                return "No entries with that email.";
            }

            foreach (var entry in deletable)
            {
                Announce($"Signup removed: {entry.Name}");
                book.Entries.Remove(entry);
            }

            return null;
        }

        public RsvpResult StartSession(RsvpEntry entry)
        {
            var nameInvalidity = NameInvalidity(entry.Name);
            if (nameInvalidity != null)
            {
                return Failure($"Invalid name: {nameInvalidity.Message}");
            }

            if (entry.MessagingMethod == MessagingMethod.Email)
            {
                var emailInvalidity = EmailInvalidity(entry.EmailOrTgUserId);
                if (emailInvalidity.Failure && emailInvalidity.Message!.Contains("exists"))
                {
                    return Failure($"Invalid email: {emailInvalidity.Message}");
                }
            }

            sessions.Add(new RegistrationSession()
            {
                Entry = entry,
                Created = DateTime.UtcNow,
                MagicCode = Guid.NewGuid().ToString()
            });

            return Success();
        }

        public RsvpResult NameInvalidity(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Failure("Name can't be empty.");
            }

            var split = name.Split(" ");
            if (split.Length < 2)
            {
                return Failure("Name has to contain first name and last name,.");
            }

            if (split.Any(name => name.Length < 2))
            {
                return Failure("At least one of the names is too short.");
            }

            if (name.Any(c => InvalidNameCharacters.Contains(c)))
            {
                return Failure("Name has invalid characters.");
            }

            if (ContainsName(name))
            {
                return Failure("Name already exists.");
            }

            return Success();
        }

        public RsvpResult EmailInvalidity(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return Failure("Email can't be empty.");
            }

            if
            (
                !email.Contains('@') ||
                email.StartsWith('@') ||
                email.EndsWith('@') ||
                !email.Contains('.') ||
                email.StartsWith('.') ||
                email.EndsWith('.')
            )
            {
                return Failure("Invalid email.");
            }

            if (email.Any(c => InvalidEmailCharacters.Contains(c)))
            {
                return Failure("Email has invalid characters.");
            }

            if (ContainsEmail(email))
            {
                return Failure("Email already exists.");
            }

            return Success();
        }

        private bool ContainsName(string name)
        {
            var nameAsLower = name.ToLowerInvariant();
            return book.Entries.Any(e => e.Name.ToLowerInvariant() == nameAsLower);
        }

        private bool ContainsEmail(string email)
        {
            var emailAsLower = email.ToLowerInvariant();
            return book.Entries.Any(e => e.EmailOrTgUserId.ToLowerInvariant() == emailAsLower);
        }

        private async void Announce(string message)
        {
            if (rocket != null)
            {
                await rocket.SendMessage(message);
            }
        }

        private static RsvpResult Success() => new(true);
        private static RsvpResult Failure(string message) => new(false, message);
    }

    public class RsvpResult
    {
        public bool Success { get; set; }

        public bool Failure => !Success;
        public string? Message { get; set; }

        public RsvpResult()
        {

        }

        public RsvpResult(bool success, string? message = null)
        {
            Success = success;
            Message = message;
        }

        public new string ToString()
        {
            return Message ?? "OK";
        }
    }

    public class RsvpRegistrationResult : RsvpResult
    {
        public RsvpEntry? Entry { get; set; }
        public bool NameOk { get; set; }
        public bool EmailOk { get; set; }
    }
}
