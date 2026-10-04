using App.HttpClients.Parse;
using App.Services.SessionService;
using DbConnect;
using dotenv.net;
using dotenv;
// using Microsoft.EntityFrameworkCore;


DotEnv.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<Database>();

// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("NextJSPolicy", policy =>
//     {
//         policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000", "http://192.168.1.186:3000")
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//     });
// });
builder.Services.AddHttpClient<ParsersMeneger>(client =>
{
    client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("ParserAdress"));
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", Environment.GetEnvironmentVariable("CONNECT_KEY"));
});
builder.Services.AddControllers();
builder.Services.AddScoped<ISessionService, SessionService>();
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