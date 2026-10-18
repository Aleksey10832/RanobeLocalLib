using System.Text.Json;
using App.Controller.Filter.AuthFillter;
using App.Models.TokensModel;
using App.Models.UserModel;
using App.Result;
using App.Services.MailService;
using App.Services.SessionService;
using DbConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace App.Controller.UserController;

[Route("user")]
public class UserController : ControllerBase
{
    private readonly Database database;
    private readonly ISessionService service;
    private readonly IMailService mailService;
    private readonly IDistributedCache cache;
    public UserController(Database _db, ISessionService service, IMailService mailService, IDistributedCache cache) {
        this.database = _db;
        this.service = service;
        this.mailService = mailService;
        this.cache = cache;
    }
    [HttpPost("create")]
    [TypeFilter(typeof(AuthFillter),  Arguments = ["userCreate"])]
    public async Task<Result<User>> CreateUser( [FromBody] IUserM user)
    {
        if(user.Role != null){
            try{
                database.Users.Add(new User(user.Login, user.Password, user.Role, "name", user.Mail));
                await database.SaveChangesAsync();
                return Result<User>.Succesful(await database.Users.Where(us => us.Login == user.Login).FirstAsync());
            } catch{
                return Result<User>.Fail(400, "Пользователь с таким именем уже существует");
            }
        }
        else{
            return Result<User>.Fail(400, "роль не указана");
        }
    }

    [HttpPost("login")]
    public async Task<Result<TokensM>> LoginUser( [FromBody] IUserM user, [FromHeader(Name = "User-Agent")] string userAgent)
    {
        try{
            User? dbUser = await database.Users.SingleOrDefaultAsync(us => us.Login == user.Login);
            if ( dbUser != null && dbUser.checkPasword(user.Password)){
                return await service.RefershToken(null, user.Login, null, userAgent);
            }
            return Result<TokensM>.Fail(401, "Логин или пароль не верен");
        } catch (Exception error){
            return Result<TokensM>.Fail(500, "Server Error Message: " + error.Message);
        }
        
    }

    [HttpPost("register")]
    public async Task<Result<string>> RegisterUser( [FromBody] IUserM? user)
    {
        try{
            if(
                user != null && 
                user.Password.Length > 6 && 
                user.Login != "" && 
                user.Mail.LastIndexOf('@') > 1 &&
                user.Mail.Substring(user.Mail.LastIndexOf('@')).LastIndexOf(".") > 0
            ) {
                if(
                    await database.Users.SingleOrDefaultAsync(us => us.Login == user.Login) != null || 
                    await database.Users.SingleOrDefaultAsync(us => us.Email == user.Mail) != null
                ) {
                    return Result<string>.Fail(409, "Пользователь с таким логином или почтой уже существует");
                }
                if(await cache.GetStringAsync("EmailLimit" + user.Mail) != null) {
                    return Result<string>.Fail(429, "Повторите попытку через 1 минуту");
                }
                string mailCode = new Random().Next(0, 1000000).ToString("D6");
                string confirmToken = Guid.NewGuid().ToString();
                User dbUser = new User(user.Login, user.Password, "User", "name", user.Mail) {
                    EmailCode = mailCode
                };
                await cache.SetStringAsync(
                    "confirmEmail" + confirmToken, 
                    JsonSerializer.Serialize(dbUser), 
                    new DistributedCacheEntryOptions{
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(600)
                    }
                );
                await cache.SetStringAsync(
                    "EmailLimit" + user.Mail, "",
                    new DistributedCacheEntryOptions{
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                    }
                );
                await mailService.sendMailMessage("Ranobe Local Lib", $"Проверочный код для регистрации аккаунта: {mailCode}", user.Mail, "Регистрация аккаунта на ресурсе Ranobe Local Lib", user.Login);
                return Result<string>.Succesful(confirmToken);
            } else {
                return Result<string>.Fail(401, "Неккоректные данные для регестрации");
            }
        } catch (Exception error){
            return Result<string>.Fail(500, "Server Error Message: " + error.Message);
        }
    }

    [HttpPost("register/{confirmToken}/{mailCode}")]
    public async Task<Result<TokensM>> RegisterConfirmUser( string confirmToken, string mailCode, [FromHeader(Name = "User-Agent")] string userAgent)
    {
        try{
            string? caheUser = await cache.GetStringAsync("confirmEmail" + confirmToken);
            if(caheUser != null){
                User? user = JsonSerializer.Deserialize<User>(caheUser);
                if(user != null){
                    if(mailCode == user.EmailCode) {
                        await cache.RemoveAsync("confirmEmail" + confirmToken);
                        database.Users.Add(user);
                        await database.SaveChangesAsync();
                        return await service.RefershToken(null, user.Login, null, userAgent);
                    } else {
                        return Result<TokensM>.Fail(401, "Неверный код");
                    }
                    
                } else {
                    return Result<TokensM>.Fail(400, "Электронная почта не найдена");
                }
            } else {
                return Result<TokensM>.Fail(400, "Пользователь не найден");
            }
        } catch (DbUpdateException) {
            return Result<TokensM>.Fail(401, "Данный логин уже занят, попробуйте другой");
        } catch (Exception error){
            return Result<TokensM>.Fail(500, "Server Error Message: " + error.Message);
        }
    }

    

    [HttpGet("profile")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["profile"])]
    public async Task<Result<ProfileM>> GetProfile([FromHeader(Name = "Authorization")] string jwtToken)
    {
        try{
            User user = await database.Users.FirstAsync(user => user.Login == Functions.GetLoginIsToken(jwtToken));
            return Result<ProfileM>.Succesful(new ProfileM(user.Id, user.Role, user.Name));
        }
        catch{
            return Result<ProfileM>.Fail(401, "Аккаунт не найден");
        }
    }
    [HttpGet("getAll/{page}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["usersMenegment"])]
    public async Task<Result<List<User>>> GetUsers(int page)
    {
        try{
            List<User> users = await database.Users.OrderBy(user => user.Name).Skip(page * 50).Take(50).ToListAsync();
            return Result<List<User>>.Succesful(users);
        }
        catch{
            return Result<List<User>>.Fail(500, "Server Error");
        }
    }

    
}
