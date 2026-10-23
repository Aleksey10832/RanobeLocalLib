using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
namespace DbConnect;

public class Database: DbContext
{
    public DbSet<Ranobe> Ranobes {get; set;}
    public DbSet<PrivateUnlockUser> PrivateUnlockUsers {get; set;}
    public DbSet<Chapter> Chapters {get; set;}
    public DbSet<Paragraf> Paragrafs {get; set;}
    public DbSet<User> Users {get; set;}
    public DbSet<UserCheckChapter> UserCheckChapters {get; set;}
    public DbSet<UserSession> UserSessions {get; set;}
    public DbSet<Role> Roles {get; set;}
    public DbSet<Image> Image {get; set;}

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(
            Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
        );
        // .UseAsyncSeeding(async (context, _, cancellationToken) =>
        // {
        //     if (await this.Users.CountAsync() == 0)
        //     {
        //         this.Users.Add(new User("admin", "admin", "Admin", "Admin", "Admin@email.email"));
        //         await context.SaveChangesAsync(cancellationToken);
        //     }
        // });
    }
}
// public class RanobeParser
// {
//     public string Id {get; set;}
//     public string userId{get; set;}
//     public string name{get; set;}
//     public string ranobeURL{get; set;}
// }
public class Ranobe
{
    public string Id {get; set;} = Guid.NewGuid().ToString();
    public string Name {get; set;} = "";
    public string UserId {get; set;} = "";
    public bool PrivateContent {get; set;} = false;
    public bool PublicVisible {get; set;} = true;
    public List<Chapter> Chapters {get; set;} = [];
    public Ranobe () { }
    public Ranobe(string name, string userId, bool privat, bool visible, List<Chapter> chapters) {
        this.Name = name;
        this.UserId = userId;
        this.PrivateContent = privat;
        this.PublicVisible = visible;
        this.Chapters = chapters;
    }
}
public class PrivateUnlockUser {
    public Guid Id {get; set;}
    public string RanobeId {get; set;}
    public string UserId {get; set;}
}

public class Chapter
{
    public string Id {get; set;}
    public string ranobeId {get; set;}
    public string name {get; set;}
    public int number {get; set;}
    public List<Paragraf> paragrafs {get; set;}
}
public class Paragraf
{
    public string Id {get; set;}
    public int number {get; set;}
    public string text {get; set;}
    public string chapterId {get; set;}
}

[Index(nameof(Login), IsUnique = true)]
[Index(nameof(Email), IsUnique = true)]
public class User{
    public Guid Id {get; set;}
    public string Login {get; set;}
    public string Name {get; set;}
    public string Password {get; set;}
    public string Role {get; set;}
    public string Email {get; set;}
    public string? EmailCode {get; set;}
    [JsonConstructor]
    private User() { }
    public User( string login, string password, string role, string name, string email){
        Id = Guid.NewGuid();
        this.Login = login;
        this.Password = new PasswordHasher<User>().HashPassword(this, password);
        this.Role = role;
        this.Name = name;
        this.Email = email;
    }
    public bool checkPasword(string password){
        PasswordVerificationResult pshash = new PasswordHasher<User>().VerifyHashedPassword(this, this.Password,  password);
        return pshash != PasswordVerificationResult.Failed;
    }
}

[Index(nameof(CidUsId), IsUnique = true)]
public class UserCheckChapter{
    public Guid Id {get; set;}
    public Guid UserId {get; set;}
    public string ChapterId {get; set;} = "";
    public int PNumber {get; set;}
    public string RanobeId {get; set;}
    public string CidUsId {get; set;}
    public int ChapterNum {get; set;}
    public DateTime Date {get; set;}
    public UserCheckChapter(Guid userId, string chapterId, int pNumber, string ranobeId, int chapterNum){
        this.UserId = userId;
        this.ChapterId = chapterId;
        this.PNumber = pNumber;
        this.RanobeId = ranobeId;
        this.CidUsId = chapterId + userId;
        this.Date = DateTime.UtcNow;
        this.ChapterNum = chapterNum;
    }
}

public class UserSession{
    public Guid Id {get; set;}
    public string? RefershToken {get; set;}
    public string UserLogin {get; set;}
    public string UserRole {get; set;}
    public string SessionName {get; set;}
    public Guid UserId {get; set;}
    public UserSession(Guid UserId, string UserLogin, string UserRole, string SessionName)
    {
        this.Id = Guid.NewGuid();
        this.UserLogin = UserLogin;
        this.UserRole = UserRole;
        this.UserId = UserId;
        this.SessionName = SessionName;
    }
    public string SetRefToken()
    {
        string refToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        this.RefershToken = new PasswordHasher<UserSession>().HashPassword(this, refToken);
        return refToken;
    }
    public string? UpdateRefToken(string RefershToken){
        if(this.RefershToken != null && (byte)new PasswordHasher<UserSession>().VerifyHashedPassword(this, this.RefershToken,  RefershToken) == 1){
            string StringRefToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            this.RefershToken = new PasswordHasher<UserSession>().HashPassword(this, StringRefToken);
            return StringRefToken;
        }
        return null;
    } 
}
public class Image {
    public Guid Id {get; set;}
    public string Name {get; set;} = "";
    public byte[] file {get; set;} = [];
}