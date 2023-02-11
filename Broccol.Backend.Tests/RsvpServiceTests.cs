using Broccol.Backend.Data;
using System.Text.Json;
using System.Reflection;

namespace Broccol.Backend.Tests
{
    public class RsvpServiceTests
    {
        RsvpService? Service { get; set; }
        EventSettings? Settings { get; set; }
        string BookPath { get; set; } = "";

        private static string GetBookDirPath()
        {
            return Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
        }

        private void Setup()
        {
            Settings = new EventSettings() { BookFile = "unit-test.json" };
            BookPath = Path.Combine(GetBookDirPath(), Settings.BookFile);
            File.Delete(BookPath);
            Service = new Broccol.Backend.Data.RsvpService(Settings, null);
        }

        [Fact]
        public void ShouldSaveFreshBookOnFreshInit()
        {
            Setup();
            File.Delete(BookPath);
            Assert.False(File.Exists(BookPath), "File shouldn't exist.");
            Setup();
            Service = new RsvpService(Settings!, null);
            Assert.True(File.Exists(BookPath), "File should exist.");
            File.Delete(BookPath);
        }

        private static RsvpEntry GetJohnDoe() => new() { Name = "John Doe", EmailOrTgUserId = "john.doe@hotmail.com" };

        [Fact]
        public void ShouldAddValidEntry()
        {
            Setup();

            var entries = new[]
            {
                GetJohnDoe(),
                new RsvpEntry() { Name = "Aasdsd Dsdfs", EmailOrTgUserId = "aasdsd.dsdfs@sdjiosf.com" },
                new RsvpEntry() { Name = "Fdtyr Qioer", EmailOrTgUserId = "fdtyr.qioer@zzzzzz.com" }
            };
            foreach (var entry in entries)
            {
                var result = Service!.AddEntry(entry);
                Assert.True(result.Success);
                Assert.Contains(entry.Name, Service.ListNames());
            }

            File.Delete(BookPath);
        }

        [Fact]
        public void ShouldNotAddEntryWithInvalidName()
        {
            Setup();

            var entry = new RsvpEntry() { Name = "John", EmailOrTgUserId = "john.doe@hotmail.com" };
            var result = Service!.AddEntry(entry);
            Assert.NotNull(result);

            File.Delete(BookPath);
        }

        [Fact]
        public void ShouldNotAddEntryWithAlreadyExistingName()
        {
            Setup();

            Service!.AddEntry(GetJohnDoe());
            var result = Service.AddEntry(GetJohnDoe());
            Assert.NotNull(result);
            Assert.Contains("exists", result.Message);

            File.Delete(BookPath);
        }

        [Fact]
        public void ShouldNotAddEntryWithInvalidEmail()
        {
            Setup();

            var entry = new RsvpEntry() { Name = "John Doe", EmailOrTgUserId = "john.doe@ hotmail.com" };
            var result = Service!.AddEntry(entry);

            Assert.NotNull(result);

            File.Delete(BookPath);
        }

        [Fact]
        public void ShouldDeleteByName()
        {
            Setup();
            var entry = GetJohnDoe();
            Service!.AddEntry(entry);

            var result = Service.DeleteByName("John Doe");
            Assert.Null(result);

            Assert.DoesNotContain(GetJohnDoe().Name, Service.ListNames());
        }

        [Fact]
        public void ShouldNotDeleteByName()
        {
            Setup();
            var entry = GetJohnDoe();
            Service!.AddEntry(entry);

            var result = Service.DeleteByName("asdf dasdf");
            Assert.NotNull(result);

            result = Service.DeleteByName("john doe");
            Assert.NotNull(result);

            Assert.Contains(GetJohnDoe().Name, Service.ListNames());
        }

        [Fact] 
        public void ShouldDeleteByEmail()
        {
            Setup();
            Service!.AddEntry(GetJohnDoe());
            var result = Service.DeleteByEmail(GetJohnDoe().EmailOrTgUserId);
            Assert.Null(result);

            Assert.DoesNotContain(GetJohnDoe().Name, Service.ListNames());
        }
        
        [Fact]
        public void ShouldSaveFile()
        {
            Setup();
            var entries = new[] 
            { 
                GetJohnDoe(), 
                new RsvpEntry() { Name = "Aasdsd Dsdfs", EmailOrTgUserId = "aasdsd.dsdfs@sdjiosf.com" },
                new RsvpEntry() { Name = "Fdtyr Qioer", EmailOrTgUserId = "fdtyr.qioer@zzzzzz.com" }
            };

            foreach (var entry in entries)
            {
                Service!.AddEntry(entry);
            }

            var book = GetBookFromFile();
            Assert.NotNull(book);
            foreach (var entry in entries)
            {
                Assert.Contains(entry, book.Entries);
            }
        }

        private EventBook? GetBookFromFile() => JsonSerializer.Deserialize<EventBook>(File.ReadAllText(BookPath));
    }
}