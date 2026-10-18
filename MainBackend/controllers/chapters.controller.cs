using App.Controller.Filter.AuthFillter;
using App.Result;
using DbConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Controller.RanobeController;



[Route("chapter")]
public class ChapterController: ControllerBase
{
    private readonly Database database;
    public ChapterController(Database _db) => this.database = _db;

    [HttpGet("check/{ranobeId}/{page}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["read"])]
    public async Task<Result<List<UserCheckChapter>>> userCheck(int page, string ranobeId, [FromHeader(Name = "Authorization")] string jwtToken){
        User? user = await database.Users.Where(user => user.Login == Functions.GetLoginIsToken(jwtToken)).FirstOrDefaultAsync();
        if(user != null){
            Guid userId = user.Id;
            int start = page * 100;
            int stop = (page + 1) * 100;
            List<UserCheckChapter> chapters = await database.UserCheckChapters.Where(check => check.UserId == userId && check.RanobeId == ranobeId && check.ChapterNum >= start && check.ChapterNum < stop).OrderBy(c => c.ChapterNum).Take(100).ToListAsync();
            if(chapters.Count == 0){
                return Result<List<UserCheckChapter>>.Fail(400, "Нихуя ты тут не читал");
            }
            return Result<List<UserCheckChapter>>.Succesful(chapters);
        } else {
            return Result<List<UserCheckChapter>>.Fail(401, "Юзер не найден");
        }
        
    }

    [HttpGet("last/{ranobeId}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["read"])]
    public async Task<Result<UserCheckChapter>> userCheckLast(string ranobeId, [FromHeader(Name = "Authorization")] string jwtToken){
        User? user = await database.Users.Where(user => user.Login == Functions.GetLoginIsToken(jwtToken)).FirstOrDefaultAsync();
        if(user == null)
        {
            return Result<UserCheckChapter>.Fail(401, "Юзер не найден");
        }
        try{
            UserCheckChapter? chapter = await database.UserCheckChapters.Where(check => check.UserId == user.Id && check.RanobeId == ranobeId).OrderByDescending(c => c.Date).FirstAsync();
            return Result<UserCheckChapter>.Succesful(chapter);
        } catch{
            return Result<UserCheckChapter>.Fail(404, "Нечитано");
        }
        
    }

    [HttpPost("check")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["read"])]
    public async Task<Result<UserCheckChapter>> SetCheckChapterStatus([FromBody] UserCheckChapterP inStatus, [FromHeader(Name = "Authorization")] string jwtToken){
        try{
            Chapter? chapter = await database.Chapters.FindAsync(inStatus.chapterId);
            string? login = Functions.GetLoginIsToken(jwtToken);
            if(login != null && chapter != null){
                Guid userId = (await database.Users.FirstAsync(el => el.Login == login)).Id;
                try {
                    UserCheckChapter status = new (userId, inStatus.chapterId, inStatus.pNumber, chapter.ranobeId, chapter.number);
                    await database.UserCheckChapters.AddAsync(status);
                    await database.SaveChangesAsync();
                    return Result<UserCheckChapter>.Succesful(status);
                } catch {
                    return Result<UserCheckChapter>.Fail(400, "глава уже прочитана");
                }
            }
            return Result<UserCheckChapter>.Fail(403, "пользователя не существует");
        } catch{
            return Result<UserCheckChapter>.Fail(404, "Нет такой главы или пользователь неавторизирован");
        }
    }

    [HttpPut("check")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["read"])]
    public async Task<Result<UserCheckChapter>> UpdateCheckChapterStatus([FromBody] UserCheckChapterP inStatus, [FromHeader(Name = "Authorization")] string jwtToken){
        Chapter? chapter = await database.Chapters.FindAsync(inStatus.chapterId);
        string? login = Functions.GetLoginIsToken(jwtToken);
        if(login != null){
            Guid userId = (await database.Users.FirstAsync(el => el.Login == login)).Id;
            if(chapter != null){
                try{
                    UserCheckChapter userCheck = await database.UserCheckChapters.SingleAsync(cChapter => cChapter.CidUsId == chapter.Id + userId);
                    userCheck.PNumber = inStatus.pNumber;
                    userCheck.Date = DateTime.UtcNow;
                    await database.SaveChangesAsync();
                    return Result<UserCheckChapter>.Succesful(userCheck);
                } catch{
                    return Result<UserCheckChapter>.Fail(400, "Глава не читана");
                }
                
            }
            return Result<UserCheckChapter>.Fail(404, "Глава не найдена");
        }
        return Result<UserCheckChapter>.Fail(403, "пользователя не существует");
    }

    [HttpGet("info/{chapterId}")]
    public async Task<Result<Chapter>> getChapterInfo(string chapterId){
        try{
            return Result<Chapter>.Succesful(await database.Chapters.SingleAsync(chapter => chapter.Id == chapterId));
        }
        catch{
            return Result<Chapter>.Fail(404, "Глава не найдена");
        }
    }

    [HttpGet("checkstatus/{chapterId}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["read"])]
    public async Task<Result<UserCheckChapter>> UserGetCheckChapterStatus(string chapterId, [FromHeader(Name = "Authorization")] string jwtToken){
        User? user = await database.Users.Where(user => user.Login == Functions.GetLoginIsToken(jwtToken)).FirstOrDefaultAsync();
        if(user != null){
            UserCheckChapter? chapter = await database.UserCheckChapters.Where(check => check.UserId == user.Id && check.ChapterId == chapterId).FirstOrDefaultAsync();
            if(chapter != null){
                return Result<UserCheckChapter>.Succesful(chapter);
            }
            return Result<UserCheckChapter>.Fail(404, "Непрочитано");
        }
        return Result<UserCheckChapter>.Fail(401, "Иди нахуй");
    }

    public class UserCheckChapterP{
        public string chapterId {get; set;} = "";
        public int pNumber {get; set;}
    }
}