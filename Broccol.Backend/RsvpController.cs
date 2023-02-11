using Broccol.Backend.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Broccol.Backend
{
    [Route("api/rsvp")]
    [ApiController]
    public class RsvpController : ControllerBase
    {
        private RsvpService service { get; set; }

        public RsvpController(RsvpService service)
        {
            this.service = service;
        }

        [HttpPost]
        public RsvpResult SubmitEntry([FromBody] RsvpEntry entry)
        {
            return new RsvpResult();
        }

        [HttpPost("forms")]
        public IResult SubmitGoogleFormsEntry([FromBody] GoogleFormsSignup entry)
        {
            Console.WriteLine(JsonSerializer.Serialize(entry));
            var nameInval = service.NameInvalidity(entry.Name);
            if (!nameInval.Success) 
            {
                return Results.BadRequest($"Invalid name: {nameInval.Message}");
            }

            var emailInval = service.EmailInvalidity(entry.Email);
            if (!emailInval.Success)
            {
                return Results.BadRequest($"Invalid email: {emailInval.Message}");
            }

            var rsvpEntry = new RsvpEntry()
            {
                Name = entry.Name,
                EmailOrTgUserId = entry.Email,
                Language = GetLanguage(entry.Language),
                MessagingMethod = MessagingMethod.Email
            };

            var addResult = service.AddEntry(rsvpEntry, "Forms");
            if (addResult.Success)
            {
                return Results.Ok();
            } else
            {
                return Results.BadRequest($"Couldn't add entry:  {addResult.Message}");
            }
        }

        private string GetLanguage(string languageInForm)
        {
            if (string.IsNullOrEmpty(languageInForm))
            {
                return "en";
            }
            if (languageInForm.ToLowerInvariant().Contains("finnish"))
            {
                return "fi";
            }
            return "en";
        }
    }

    public class GoogleFormsSignup
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Language { get; set; } = "";
    }
}
