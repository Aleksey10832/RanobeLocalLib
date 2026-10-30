using Microsoft.AspNetCore.Mvc;
using App.Result;
using DbConnect;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using App.Controller.Filter.AuthFillter;
using AngleSharp.Text;
using App.Controller.Filter.PrivateFillter;

namespace App.Controller.RanobeController;



[Route("/ranobe")]
public class RanobeController: ControllerBase
{
    private readonly Database database;
    public RanobeController(Database _db) {
        this.database = _db;
    }

    [HttpGet("{ranobeId}/{page}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["read"]), TypeFilter(typeof(PrivateFillter))]
    public async Task<Result<Ranobe>> GetById([FromRoute(Name="ranobeId")] string id, [FromRoute(Name="page")] string page)
    {
        Ranobe? ranobe = await database.Ranobes.FindAsync(id);
        if(ranobe != null){
            try {
                ranobe.Chapters = await database.Chapters.Where(c => c.ranobeId == id).OrderBy(c => c.number).Skip(page.ToInteger(0) * 100).Take(100).ToListAsync();
                if(ranobe.Chapters.Count == 0){
                    return Result<Ranobe>.Fail(404, "Ну блять, нету больше нихуя");
                }
                return Result<Ranobe>.Succesful(ranobe);
            } catch {
                return Result<Ranobe>.Fail(400, "Ну блять, нету больше нихуя");
            }
        }
        return Result<Ranobe>.Fail(404, "Куда ты блять лезешь");
    }

    [HttpGet("{page}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["read"])]
    public async Task<Result<RanobeP>> GetAllRanobe([FromRoute] string? page){
        return Result<RanobeP>.Succesful(new RanobeP(await database.Ranobes.Where(el => el.PublicVisible == true).OrderBy(el => el.Name).Skip(30 * page.ToInteger(0)).Take(30).ToListAsync(), await database.Ranobes.CountAsync()));
    }

    [HttpGet("{ranobeId}/chapter/{number}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["read"])]
    [TypeFilter(typeof(PrivateFillter))]
    public async Task<Result<Chapter>> GetChapter(string ranobeId, int number){
        try{
            Chapter? chapter = await database.Chapters.Where(el => el.ranobeId == ranobeId).FirstAsync(el => el.number == number);
            chapter.paragrafs = await database.Paragrafs.Where(p => p.chapterId == chapter.Id).OrderBy(p => p.number).ToListAsync();
            return Result<Chapter>.Succesful(chapter);
        } catch{
            return Result<Chapter>.Fail(404, "Нет такой главы");
        }
        
    }
    public class RanobeP{
        public List<Ranobe> ranobe {get; set;}
        public double pages {get; set;}
        public RanobeP(List<Ranobe> ranobe, int count){
            this.ranobe = ranobe;
            this.pages = Math.Round((double)count / 20, 2);
        }
    }

    [HttpDelete("{RanobeId}")]
    [TypeFilter(typeof(AuthFillter),  Arguments = ["ranobeDelete"])]
    public async Task<Result<string>> DeleteRanobe( string RanobeId )
    {
        if(await database.Ranobes.FindAsync(RanobeId) == null)
        {
            return Result<string>.Fail(404, "Not found");
        }
        List<Chapter> chapters = await database.Chapters.Where(r => r.ranobeId == RanobeId).ToListAsync();
        chapters.ForEach(chapter =>{
            database.Paragrafs.RemoveRange(database.Paragrafs.Where(paragraf => paragraf.chapterId == chapter.Id));
        });
        database.Chapters.RemoveRange(chapters);
        Ranobe? delRanobe = await database.Ranobes.FindAsync(RanobeId);
        if(delRanobe != null){
            database.Ranobes.Remove(delRanobe);
        }
        database.SaveChanges();
        return Result<string>.Succesful(RanobeId);
    }
}