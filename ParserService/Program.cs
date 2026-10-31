using App.Parser;
using DbConnect;
dotenv.net.DotEnv.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<Database>();
builder.Services.AddControllers();
builder.Services.AddScoped<ParsersMeneger>();
var app = builder.Build();
app.MapControllers();
app.Use(async (context, next) =>
{
    string? key = Environment.GetEnvironmentVariable("CONNECT_KEY");
    string? bearer = context.Request.Headers["Authorization"];
    if(bearer != null)
    {
        if(bearer[7..] == key) {
            await next();
            return;
        }
    }
    
    else {
        context.Response.StatusCode = 401;
        return;
    }
});
app.Run();
