using App.HttpClients.Parse;
using App.Services.SessionService;
using DbConnect;
using dotenv.net;
using App.Services.MailService;


DotEnv.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<Database>();

string? parserAdress = Environment.GetEnvironmentVariable("ParserAdress");
if(parserAdress == null){
    System.Console.WriteLine("[WARN] Parser service environment not found, using default addr [http://localhost:5066]");
    parserAdress = "http://localhost:5066";
}
builder.Services.AddHttpClient<ParsersMeneger>(client =>
{
    client.BaseAddress = new Uri(parserAdress);
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", Environment.GetEnvironmentVariable("CONNECT_KEY"));
});
builder.Services.AddControllers();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddStackExchangeRedisCache(options => {
    options.Configuration = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
    options.InstanceName = "RanobeLib";
});
var app = builder.Build();
app.UseRouting();
app.UseCors("NextJSPolicy");
app.UseAuthorization();
app.MapControllers();
app.Run();