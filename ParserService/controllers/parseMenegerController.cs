using Microsoft.AspNetCore.Mvc;
using App.Result;
using App.Parser;
using DbConnect;
using Microsoft.EntityFrameworkCore;

namespace App.Controller.ParseController;

[Route("/parse")]
public class ParseController: ControllerBase
{
    private readonly Database database;
    private readonly ParsersMeneger parser;
    public ParseController(Database _db, ParsersMeneger parser){
        this.database = _db;
        this.parser = parser;
    }
    [HttpGet("status/{userId}")]
    public async Task<Result<ParseModel>> GetParseStatus(string userId){
        ParseModel? parse = await this.database.Parsers.FirstOrDefaultAsync(p => p.UserId == userId); 
        if(parse == null)
        {
            return Result<ParseModel>.Fail(404, "Not Found");
        }
        return Result<ParseModel>.Succesful(parse);
    }
    [HttpGet("{ranobeUrl}")]
    public async Task<Result<RanobeModel>> GetRanobe(string ranobeUrl){
        RanobeModel? ranobe = await database.Ranobes.FirstOrDefaultAsync(el => el.RanobeUrl == ranobeUrl); 
        if(ranobe == null)
        {
            return Result<RanobeModel>.Fail(404, "Not Found");
        }
        ranobe.Chapters = await database.Chapters.Where(el => el.ranobeId == ranobe.Id).ToListAsync();
        return Result<RanobeModel>.Succesful(ranobe);
    }
    [HttpGet("{ranobeUrl}/{chapterId}")]
    public async Task<Result<ChapterModel>> GetChapter(string chapterId){
        ChapterModel? chapter = await database.Chapters.SingleOrDefaultAsync(el => el.Id == chapterId);
        if(chapter != null) {
            chapter.paragrafs = await database.Paragrafs.Where(el => el.chapterId == chapter.Id).ToListAsync();
            return Result<ChapterModel>.Succesful(chapter);
        }
        return Result<ChapterModel>.Fail(404, "Not Found");
    }
    [HttpPost]
    public Result<ParseStatus> Start([FromBody] ParseModel userDate) {
        System.Console.WriteLine(userDate.UserId);
        if(userDate == null || !this.parser.Start(userDate.RanobeUrl, userDate.UserId, "ranobe-me"))
        {
            return Result<ParseStatus>.Fail(400, "Чёто не чисто");
        }
        Parse? status = this.parser.GetParse(userDate.UserId.ToString());
        if(status == null)
        {
            return Result<ParseStatus>.Fail(500, "Потеряшка");
        }
        return Result<ParseStatus>.Succesful(status.status);
    }
    [HttpPost("/config")]
    public async Task<Result<bool>> AddConfig([FromBody] ParserConfig Config) {
        if(await this.parser.AddConfig(config: Config)){
            return Result<bool>.Succesful(true);
        }
        return Result<bool>.Fail(400, "Config not valid");
    }
    [HttpPost("/config/{configId}")]
    public async Task<Result<bool>> UpdateConfig([FromRoute] string configId, [FromBody] ParserConfig config) {
        if(await this.parser.UpdateConfig(id: configId, updatedConfig: config)){
            return Result<bool>.Succesful(true);
        }
        return Result<bool>.Fail(400, "Config not valid");
    }
    [HttpDelete("/config/{configId}")]
    public async Task<Result<bool>> DeleteConfig([FromRoute] string configId) {
        if(await this.parser.RemoveConfig(id: configId)){
            return Result<bool>.Succesful(true);
        }
        return Result<bool>.Fail(400, "Config not valid");
    }
    [HttpGet("config")]
    public async Task<Result<List<ParserConfig>>> GetAllConfigs() {
        List<ParserConfig> configs = await this.parser.GetConfigs();
        return Result<List<ParserConfig>>.Succesful(configs);
    }
    [HttpGet("config/{configId}")]
    public async Task<Result<ParserConfig>> getConfig([FromRoute] string configId) {
        ParserConfig? config = await this.parser.GetConfig(configId);
        if(config != null){
            return Result<ParserConfig>.Succesful(config);
        }
        return Result<ParserConfig>.Fail(404, "Not Found");
    }
}