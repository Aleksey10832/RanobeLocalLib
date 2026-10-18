using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using AngleSharp.Common;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
namespace App.Controller.Filter.AuthFillter;

public class AuthFillter : IAsyncActionFilter
{
    private readonly IDistributedCache cache;
    private readonly string rule;
    public AuthFillter(string rule, IDistributedCache cache){
        this.cache = cache;
        this.rule = rule;
    }
    public async Task OnActionExecutionAsync( ActionExecutingContext httpContext, ActionExecutionDelegate next)
    {
        try {
            string token = httpContext.HttpContext.Request.Headers.Authorization.ToString();
            new JwtSecurityTokenHandler ().ValidateToken(token[7..], Functions.ValidateParams, out _);
            string roleInToken = Functions.GetRoleIsToken(token);
            string? StringRole = await cache.GetStringAsync(roleInToken);
            if(StringRole != null) {
                if(roleInToken != "RoleAdmin"){
                    Role? role = JsonSerializer.Deserialize<Role>(StringRole);
                    if(role != null){
                        foreach(string roleRule in role.Rules){
                            if(roleRule == this.rule){
                                await next();
                                return;
                            }
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