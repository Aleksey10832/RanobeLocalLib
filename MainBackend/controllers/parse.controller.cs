using Microsoft.AspNetCore.Mvc;
using App.Result;
using DbConnect;
using Microsoft.EntityFrameworkCore;
using App.HttpClients.Parse;
using App.Controller.Filter.AuthFillter;
using System.Collections.Concurrent;

namespace App.Controller.ParseController;

[ApiController]
[Route("/parse")]
public class ParseController: ControllerBase
{
    private const int PageSize = 20;
    private static readonly ConcurrentDictionary<string, byte> _running = new();

    private readonly Database database;
    private readonly ParsersMeneger parser;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ParseController> _logger;

    public ParseController(
        Database db,
        ParsersMeneger parser,
        IServiceScopeFactory scopeFactory,
        ILogger<ParseController> logger)
    {
        database = db;
        this.parser = parser;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
    [HttpGet("{ranobeUrl}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["parser"])]
    public async Task<Result<ParseModel>> GetParseStatus(string ranobeUrl, [FromHeader(Name = "Authorization")] string accessToken){
        string? userLogin = Functions.GetLoginIsToken(accessToken);
        Guid userId = Guid.NewGuid();
        if(userLogin != null)
        {
            User? user = await this.database.Users.FirstOrDefaultAsync(user => userLogin == user.Login);
            if(user != null) {userId = user.Id;}
        }
        return await parser.GetParseStatus(ranobeUrl, userId); 
    }
    [HttpPost("result/{ranobeUrl}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["parser"])]
    public async Task<Result<string>> ImportRanobe(string ranobeUrl)
    {
        if (!_running.TryAdd(ranobeUrl, 0))
            return Result<string>.Fail(409, "Import already in progress");

        _ = Task.Run(() => ImportInBackgroundAsync(ranobeUrl));

        return new Result<string>(202, ranobeUrl);
    }

    [HttpPost("{ranobeUrl}")]
    [TypeFilter(typeof(AuthFillter), Arguments = ["parser"])]
    public async Task<Result<ParseModel>> Start(string ranobeUrl, [FromHeader(Name = "Authorization")] string accessToken)
    {
        return await parser.SetParseStatus(ranobeUrl, accessToken);
    }


    private async Task ImportInBackgroundAsync(string ranobeUrl)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<Database>();
            var parserClient = scope.ServiceProvider.GetRequiredService<ParsersMeneger>();

            Ranobe? ranobe = null;

            for (int page = 0; ; page++)
            {
                var result = await parserClient.GetPageAsync(ranobeUrl, page);

                if (result.StatusCode != 200 || result.Value is null)
                {
                    _logger.LogWarning("Import of {Url} failed on page {Page}: {Status}",
                        ranobeUrl, page, result.StatusCode);
                    return; // не сохраняем частичные данные
                }

                var pageRanobe = result.Value;
                var pageChapterCount = pageRanobe.Chapters?.Count ?? 0;

                if (ranobe is null)
                {
                    ranobe = pageRanobe;
                    ranobe.Chapters ??= new();
                }
                else
                {
                    ranobe.Chapters ??= new();
                    ranobe.Chapters.AddRange(pageRanobe.Chapters ?? new());
                }

                if (pageChapterCount < PageSize)
                    break; // последняя страница
            }

            if (ranobe is null)
                return;

            db.Ranobes.Add(ranobe);
            await db.SaveChangesAsync();

            _logger.LogInformation("Import of {Url} finished, {Count} chapters",
                ranobeUrl, ranobe.Chapters?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import of {Url} crashed", ranobeUrl);
        }
        finally
        {
            _running.TryRemove(ranobeUrl, out _);
        }
    }
}