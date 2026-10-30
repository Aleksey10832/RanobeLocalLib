using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using AngleSharp.Common;
using DbConnect;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
namespace App.Controller.Filter.AuthFillter;

public class AuthFillter : IAsyncActionFilter
{
    private readonly IDistributedCache cache;
    private readonly string rule;
    private readonly Database database;
    public AuthFillter(string rule, IDistributedCache cache, Database database){
        this.cache = cache;
        this.rule = rule;
        this.database = database;
    }
    public async Task OnActionExecutionAsync( ActionExecutingContext httpContext, ActionExecutionDelegate next)
    {
        try {
            bool redisOk = true;
            string token = httpContext.HttpContext.Request.Headers.Authorization.ToString();
            new JwtSecurityTokenHandler ().ValidateToken(token[7..], Functions.ValidateParams, out _);
            string? userId = Functions.GetUserIdIsToken(token);
            string? roleInToken = null;
            try {
                roleInToken = await cache.GetStringAsync("RoleNotAuth");
            }
            catch {
                redisOk = false;
            }
            Role? role = new ("", []);
            if (!redisOk) {
                role = await database.Roles.SingleOrDefaultAsync(rol => rol.Name == "NotAuth");
            }
            if(roleInToken != null) {
                role = JsonSerializer.Deserialize<Role>(roleInToken);
            }
            if(userId != null) {
                User? user = await database.Users.FindAsync(userId);
                if(user != null) {
                    roleInToken = await cache.GetStringAsync("Role" + user.Role);
                    if (redisOk) {
                        if(roleInToken != null) {
                            role = JsonSerializer.Deserialize<Role>(roleInToken);
                        }
                    }
                    else {
                        role = await database.Roles.SingleOrDefaultAsync(rol => rol.Name == roleInToken);
                    }
                }
            }
            if(role != null){
                if( role.Name != "RoleAdmin"){
                    foreach(string roleRule in role.Rules){
                        if(roleRule == this.rule){
                            await next();
                            return;
                        }
                    }
                } else {
                    await next();
                    return;
                }
            } 
            httpContext.HttpContext.Response.StatusCode = 403;
            await httpContext.HttpContext.Response.WriteAsJsonAsync(new {Error = $"Пользователь не имеет права {this.rule}"});
            return;
        } catch {
            string? notAuthRole = await cache.GetStringAsync("RolenotAuth");
            if(notAuthRole != null){
                Role? role = JsonSerializer.Deserialize<Role>(notAuthRole);
                if(role != null){
                    foreach(string roleRule in role.Rules){
                        if(roleRule == this.rule){
                            await next();
                            return;
                        }
                    }
                }
            }
            httpContext.HttpContext.Response.StatusCode = 401;
            await httpContext.HttpContext.Response.WriteAsJsonAsync(new {Error = "Пользователь должен пройти верефикацию"});
            return;
        }
    }
}