using DbConnect;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
namespace App.Controller.Filter.PrivateFillter;

public class PrivateFillter : IAsyncActionFilter
{
    private readonly IDistributedCache cache;
    private readonly Database database;
    public PrivateFillter(Database database, IDistributedCache cache){
        this.cache = cache;
        this.database = database;
    }
    public async Task OnActionExecutionAsync( ActionExecutingContext httpContext, ActionExecutionDelegate next)
    {
        try {
            object? ranobeObjectId = httpContext.RouteData.Values["ranobeId"];
            if(ranobeObjectId != null){
                string? ranobeId = ranobeObjectId.ToString();
                if(ranobeId != null){
                    Ranobe? ranobe = await database.Ranobes.FindAsync(ranobeId);
                    if(ranobe != null) {
                        if (!ranobe.PrivateContent) {
                            await next();
                            return;
                        }
                        string token = httpContext.HttpContext.Request.Headers.Authorization.ToString();
                        string? userId = Functions.GetUserIdIsToken(token);
                        if(userId != null) {
                            if(database.PrivateUnlockUsers.SingleOrDefault(pass => pass.RanobeId == ranobeId && pass.UserId == userId) != null) {
                                await next();
                                return;
                            }
                        }
                    }
                }
            }
            httpContext.HttpContext.Response.StatusCode = 403;
            await httpContext.HttpContext.Response.WriteAsJsonAsync(new {Error = $"Пользователь не имеет доступа "});
            return;
        } catch {
            httpContext.HttpContext.Response.StatusCode = 500;
            await httpContext.HttpContext.Response.WriteAsJsonAsync(new {Error = "naebnulsa server"});
            return;
        }
    }
}