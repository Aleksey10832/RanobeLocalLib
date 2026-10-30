using System.Text.Json;
using App.Models.TokensModel;
using App.Result;
using DbConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace App.Services.SessionService;
public interface ISessionService{
    Task<Result<TokensM>> RefershToken(string? refershToken, string? login, string? jwtToken, string? userAgent);
    Task<Result<string>> ConfirmToken(string confirmToken, string accessToken);
}
public class SessionService : ISessionService {
    private readonly Database database;
    private readonly IDistributedCache cache;
    public SessionService(Database _db, IDistributedCache cache){
        this.database = _db;
        this.cache = cache;
    }
    public async Task<Result<TokensM>> RefershToken(string? refershToken, string? login, string? jwtToken, string? userAgent) {
        if(refershToken != null && jwtToken != null){
            string? SessionIdIsToken = Functions.GetSessionIdIsToken(jwtToken);
            UserSession? session = null;
            if(SessionIdIsToken != null){
                session = await database.UserSessions.FindAsync(Guid.Parse(SessionIdIsToken));
            }
            
            if(session != null){
                User? dbUser = await database.Users.FindAsync(session.UserId);
                if(dbUser != null){
                    string? newRefToken = session.UpdateRefToken(refershToken);
                    if(newRefToken != null) {
                        TokensM tokens = new (
                            Functions.GenerateAccessToken(dbUser.Id, dbUser.Login, session.Id),
                            newRefToken
                        );
                        await cache.SetStringAsync(
                            "confirmSession" + session.Id, 
                            JsonSerializer.Serialize<TokensM>(tokens), 
                            new DistributedCacheEntryOptions{
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                            }
                        );
                        return Result<TokensM>.Succesful(tokens);
                    }
                } else {
                    Result<TokensM>.Fail(401, "Нет, отказано0");
                }
            } else{
                Result<TokensM>.Fail(401, "Нет, отказано0");
            }
        } else if (login != null){
            User? dbUser = await database.Users.SingleOrDefaultAsync(us => us.Login == login);
            
            if(dbUser != null && userAgent != null){
                UserSession session = new(dbUser.Id, dbUser.Login, dbUser.Role, userAgent);
                TokensM tokens = new (
                    Functions.GenerateAccessToken(dbUser.Id, dbUser.Login, session.Id),
                    session.SetRefToken()
                );
                await database.UserSessions.AddAsync(session);
                await database.SaveChangesAsync();
                return Result<TokensM>.Succesful(tokens);
            }
            return Result<TokensM>.Fail(401, "Нет, отказано");
        }
        return Result<TokensM>.Fail(401, "Нет, отказано");
    }
    public async Task<Result<string>> ConfirmToken(string confirmToken, string accessToken){
        string? sessionId = Functions.GetSessionIdIsToken(accessToken);
        string? tokensCache = await cache.GetStringAsync("confirmSession" + sessionId);
        if(tokensCache != null && sessionId != null){
            TokensM? tokens = JsonSerializer.Deserialize<TokensM>(tokensCache);
            if(tokens != null && tokens.ConfirmToken == confirmToken){
                UserSession? session = await database.UserSessions.FindAsync(Guid.Parse(sessionId));
                if(session != null){
                    session.RefershToken = new PasswordHasher<UserSession>().HashPassword(session, tokens.RefershToken);
                    await database.SaveChangesAsync();
                    return Result<string>.Succesful("Successfully Update Tokens");
                }
            }
        }
        return Result<string>.Fail(401, "Отказано");
    }
}