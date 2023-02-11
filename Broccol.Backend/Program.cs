using Broccol.Backend;
using Broccol.Backend.Data;

var builder = WebApplication.CreateBuilder(args);
var conf = builder.Configuration.GetSection("EventSettings").Get<EventSettings>();
if (conf == null)
{
	throw new InvalidOperationException("EventSettings is null.");
}

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton(conf);
if (conf.RocketChatToken != null && !string.IsNullOrEmpty(conf.RocketChatToken) && conf.RocketChatUrl != null & !string.IsNullOrEmpty(conf.RocketChatUrl))
{
    builder.Services.AddSingleton<RocketChatAnnouncer>();
    builder.Services.AddHttpClient<RocketChatAnnouncer>();
}
builder.Services.AddSingleton<RsvpService>();

builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

TelegramBot? tgBot = conf.TelegramBotToken != null && !string.IsNullOrEmpty(conf.TelegramBotToken)
	? new TelegramBot(app.Configuration, app.Services.GetService<RsvpService>()!)
	: null;

app.Run();