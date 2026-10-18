using App.Models.TokensModel;
using App.Result;
using App.Services.SessionService;
using DbConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Controller.SessionController;

[Route("session")]
public class SessionController : ControllerBase{
    private readonly Database database;
    private readonly ISessionService service;
    public SessionController(Database _db, ISessionService service) {
        this.database = _db;
        this.service = service;
    }

    [HttpPost("refersh/{refershToken}")]
    public async Task<Result<TokensM>> UpdateTokens(string refershToken, string? userAgent, [FromHeader(Name = "Authorization")] string? jwtToken){
        try {
            return await service.RefershToken(refershToken, null, jwtToken, userAgent);
        } catch {
            return Result<TokensM>.Fail(401, "Отказано");
        }
    }
    
    [HttpPost("confirm/{confirmToken}")]
    public async Task<Result<string>> ConfirmUserTokens(string confirmToken, [FromHeader(Name = "Authorization")] string accessToken){
        try {
            return await service.ConfirmToken(confirmToken, accessToken);
        } catch (Exception ex) {
            return Result<string>.Fail(500, "Server Error Message: " + ex);
        }
    }

    [HttpPost("logout")]
    public async Task<Result<string>> LogoutUser( [FromHeader(Name = "Authorization")] string token)
    {
        string? sessionId = Functions.ValidateToken(token);
        if ( sessionId != null ) {
            try {
                UserSession? session = await database.UserSessions.SingleOrDefaultAsync(session => session.Id == Guid.Parse(sessionId));
                if(session != null) {
                    database.UserSessions.Remove(session);
                    await database.SaveChangesAsync();
                    return Result<string>.Succesful("OK");
                }
                return Result<string>.Succesful("OK");
            } catch {
                return Result<string>.Fail(500, "DATABASE EBANULSA");
            }
            
        }
        return Result<string>.Fail(401, "Токен не валиден");
    }
}