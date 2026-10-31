using AngleSharp;
using AngleSharp.Dom;
using DbConnect;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
// using DbConnect;
using System.Collections.Concurrent;
using System.Text.Json;


namespace App.Parser;

public class ParsersMeneger
{
    public static ConcurrentDictionary<string, Parse> parsers { get; }
        = new();

    private readonly IServiceScopeFactory scopeFactory;

    private readonly string configPath;

    private readonly SemaphoreSlim configLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ParsersMeneger(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;

        configPath = Path.Combine(
            AppContext.BaseDirectory,
            "ParserConfigs.json"
        );

        if (!File.Exists(configPath))
            File.WriteAllText(configPath, "[]");
    }

    // ==========================================
    // CONFIG READ
    // ==========================================

    public async Task<List<ParserConfig>> GetConfigs()
    {
        await configLock.WaitAsync();

        try
        {
            return await ReadConfigsUnsafe();
        }
        finally
        {
            configLock.Release();
        }
    }

    public async Task<ParserConfig?> GetConfig(string id)
    {
        var configs = await GetConfigs();

        return configs.FirstOrDefault(x => x.Id == id);
    }

    // ==========================================
    // CONFIG ADD
    // ==========================================

    public async Task<bool> AddConfig(ParserConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.SiteHost))
            throw new ArgumentException("SiteHost не указан");

        await configLock.WaitAsync();

        try
        {
            var configs = await ReadConfigsUnsafe();

            if (string.IsNullOrWhiteSpace(config.Id))
                config.Id = Guid.NewGuid().ToString();

            if (configs.Any(x => x.Id == config.Id))
                return false;

            config.SiteHost = NormalizeHost(config.SiteHost);

            configs.Add(config);

            await SaveConfigsUnsafe(configs);

            return true;
        }
        finally
        {
            configLock.Release();
        }
    }

    // ==========================================
    // CONFIG UPDATE
    // ==========================================

    public async Task<bool> UpdateConfig(
        string id,
        ParserConfig updatedConfig)
    {
        await configLock.WaitAsync();

        try
        {
            var configs = await ReadConfigsUnsafe();

            int index = configs.FindIndex(x => x.Id == id);

            if (index < 0)
                return false;

            updatedConfig.Id = id;
            updatedConfig.SiteHost = NormalizeHost(
                updatedConfig.SiteHost
            );

            configs[index] = updatedConfig;

            await SaveConfigsUnsafe(configs);

            return true;
        }
        finally
        {
            configLock.Release();
        }
    }

    // ==========================================
    // CONFIG DELETE
    // ==========================================

    public async Task<bool> RemoveConfig(string id)
    {
        await configLock.WaitAsync();

        try
        {
            var configs = await ReadConfigsUnsafe();

            var config = configs.FirstOrDefault(x => x.Id == id);

            if (config == null)
                return false;

            configs.Remove(config);

            await SaveConfigsUnsafe(configs);

            return true;
        }
        finally
        {
            configLock.Release();
        }
    }

    // ==========================================
    // JSON HELPERS
    // ==========================================

    private async Task<List<ParserConfig>> ReadConfigsUnsafe()
    {
        string json = await File.ReadAllTextAsync(configPath);

        return JsonSerializer.Deserialize<List<ParserConfig>>(
            json,
            JsonOptions
        ) ?? new List<ParserConfig>();
    }

    private async Task SaveConfigsUnsafe(
        List<ParserConfig> configs)
    {
        string json = JsonSerializer.Serialize(
            configs,
            JsonOptions
        );

        string tempPath = configPath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json);

        File.Move(tempPath, configPath, true);
    }

    private static string NormalizeHost(string host)
    {
        host = host.Trim();

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
            return uri.Host.ToLowerInvariant();

        return host.TrimEnd('/')
            .Split('/')[0]
            .ToLowerInvariant();
    }

    // ==========================================
    // CONFIG SELECT
    // ==========================================

    public async Task<ParserConfig?> FindConfig(string url)
    {
        var configs = await GetConfigs();

        // Абсолютный URL: выбираем конфигурацию по домену
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps))
        {
            var matches = configs
                .Where(x => string.Equals(
                    x.SiteHost,
                    uri.Host,
                    StringComparison.OrdinalIgnoreCase
                ))
                .Where(x =>
                    string.IsNullOrWhiteSpace(x.UrlContains) ||
                    url.Contains(
                        x.UrlContains,
                        StringComparison.OrdinalIgnoreCase
                    ))
                .OrderByDescending(x => x.UrlContains.Length)
                .ToList();

            if (matches.Count == 0)
                return null;

            int bestLength = matches[0].UrlContains.Length;

            var bestMatches = matches
                .Where(x => x.UrlContains.Length == bestLength)
                .ToList();

            if (bestMatches.Count == 1)
                return bestMatches[0];

            return bestMatches.SingleOrDefault(x => x.IsDefault);
        }

        // Относительный URL: используем конфигурацию по умолчанию
        var defaults = configs.Where(x => x.IsDefault).ToList();

        if (defaults.Count == 1)
            return defaults[0];

        if (configs.Count == 1)
            return configs[0];

        return null;
    }

    // ==========================================
    // START PARSER
    // ==========================================

    // Старый интерфейс сохранён
    public bool Start(string ranobeUrl, Guid userId)
    {
        return StartInternal(ranobeUrl, userId, null);
    }

    // Запуск с конкретным конфигом
    public bool Start(
        string ranobeUrl,
        Guid userId,
        string configId)
    {
        return StartInternal(ranobeUrl, userId, configId);
    }

    private bool StartInternal(
        string ranobeUrl,
        Guid userId,
        string? configId)
    {
        var scope = scopeFactory.CreateScope();

        try
        {
            var database = scope.ServiceProvider
                .GetRequiredService<Database>();

            Parse parser = new(
                database,
                this,
                scope,
                ranobeUrl,
                userId,
                configId
            );

            if (!parsers.TryAdd(userId.ToString(), parser))
            {
                scope.Dispose();
                return false;
            }

            _ = parser.startParse();

            return true;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public Parse? GetParse(string userId)
    {
        parsers.TryGetValue(userId, out var parse);

        return parse;
    }

    public void Remove(string userId)
    {
        parsers.TryRemove(userId, out _);
    }
}

public class Parse
{
    public Guid userId;

    public ParseStatus status = new();

    private readonly ParsersMeneger parser;
    private readonly Database database;
    private readonly IServiceScope scope;

    private readonly string ranobeUrl;
    private readonly string? configId;

    private readonly HttpClient http = new();

    private readonly IBrowsingContext context;

    private ParseModel? dbParseStatus;

    private static readonly HashSet<string> BlockTags = new(
        new[]
        {
            "p", "div", "section", "article",
            "blockquote", "pre", "li",
            "h1", "h2", "h3", "h4", "h5", "h6"
        },
        StringComparer.OrdinalIgnoreCase
    );

    public Parse(
        Database database,
        ParsersMeneger parser,
        IServiceScope scope,
        string ranobeUrl,
        Guid userId,
        string? configId)
    {
        this.database = database;
        this.parser = parser;
        this.scope = scope;
        this.ranobeUrl = ranobeUrl;
        this.userId = userId;
        this.configId = configId;

        context = BrowsingContext.New(
            Configuration.Default.WithDefaultLoader()
        );
    }

    // ==========================================
    // START
    // ==========================================

    public async Task startParse()
    {
        try
        {
            await ParseInternal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parser error: {ex}");

            if (dbParseStatus != null)
            {
                try
                {
                    dbParseStatus.IsDone = true;
                    dbParseStatus.ProcessStatus = status.getStatus();

                    await database.SaveChangesAsync();
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"Database error: {dbEx.Message}");
                }
            }
        }
        finally
        {
            parser.Remove(userId.ToString());

            http.Dispose();
            scope.Dispose();
        }
    }

    // ==========================================
    // MAIN PARSE
    // ==========================================

    private async Task ParseInternal()
    {
        // Получение конфигурации
        ParserConfig? config;

        if (!string.IsNullOrWhiteSpace(configId))
            config = await parser.GetConfig(configId);
        else
            config = await parser.FindConfig(ranobeUrl);

        if (config == null)
            throw new Exception(
                $"Parser config not found: {ranobeUrl}"
            );

        // Основной URL
        string sourceUrl = BuildSourceUrl(config);

        // Загружаем главную страницу
        var document = await context.OpenAsync(sourceUrl);

        RanobeModel ranobe = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            RanobeUrl = ranobeUrl,
            Name = "",
            ImgHeader = Array.Empty<byte>(),
            Chapters = new List<ChapterModel>()
        };

        // Название
        if (!string.IsNullOrWhiteSpace(
            config.HeaderPath.HeaderSelector))
        {
            ranobe.Name = document
                .QuerySelector(config.HeaderPath.HeaderSelector)?
                .TextContent.Trim() ?? "";
        }

        status.ranobeName = ranobe.Name;

        // Обложка
        if (!string.IsNullOrWhiteSpace(
            config.HeaderPath.ImgSelector))
        {
            var image = document.QuerySelector(
                config.HeaderPath.ImgSelector
            );

            string? imageUrl = image?.GetAttribute(
                config.HeaderPath.ImgUrl
            );

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                ranobe.ImgHeader = await DownloadImageAsync(
                    sourceUrl,
                    imageUrl
                );
            }
        }

        // ======================================
        // GET CHAPTER URLS WITH PAGINATION
        // ======================================

        List<string> chapterUrls = await GetChapterUrlsAsync(
            document,
            sourceUrl,
            config
        );

        status.chaptersCount = chapterUrls.Count;
        status.chaptersDone = 0;
        status.status = 0;

        // ======================================
        // CREATE PARSE STATUS IN DATABASE
        // ======================================

        dbParseStatus = new ParseModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RanobeUrl = ranobeUrl,
            ProcessStatus = status.getStatus(),
            IsDone = false
        };

        database.Parsers.Add(dbParseStatus);

        await database.SaveChangesAsync();

        // ======================================
        // PARSE CHAPTERS
        // ======================================

        List<ChapterModel> chapters = new();

        for (int i = 0; i < chapterUrls.Count; i++)
        {
            string chapterUrl = chapterUrls[i];

            try
            {
                var chapterDocument = await context.OpenAsync(
                    chapterUrl
                );

                ChapterModel chapter = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    ranobeId = ranobe.Id,
                    number = i + 1,
                    name = "Глава " + (i + 1),
                    paragrafs = new List<ParagrafModel>()
                };

                // Название главы
                if (!string.IsNullOrWhiteSpace(
                    config.ChapterPath.HeaderSelector))
                {
                    chapter.name = chapterDocument
                        .QuerySelector(
                            config.ChapterPath.HeaderSelector
                        )?.TextContent.Trim() ?? chapter.name;
                }

                // Получаем содержимое главы
                chapter.paragrafs = await ParseChapterContentAsync(
                    chapterDocument,
                    chapterUrl,
                    config.ChapterPath,
                    chapter.Id
                );

                chapters.Add(chapter);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Chapter error [{chapterUrl}]: {ex.Message}"
                );
            }
            finally
            {
                // Учитываем обработку главы, даже если она завершилась ошибкой
                status.chaptersDone++;
                status.status = status.getStatus();

                if (dbParseStatus != null)
                {
                    dbParseStatus.ProcessStatus = status.status;

                    try
                    {
                        await database.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Progress save error: {ex.Message}"
                        );
                    }
                }
            }
        }

        // ======================================
        // SAVE RANOBE
        // ======================================

        ranobe.Chapters = chapters;

        database.Ranobes.Add(ranobe);

        if (dbParseStatus != null)
        {
            dbParseStatus.IsDone = true;
            dbParseStatus.ProcessStatus = 100;
        }

        await database.SaveChangesAsync();

        status.status = 100;
    }

    // ==========================================
    // BUILD SOURCE URL
    // ==========================================

    private string BuildSourceUrl(ParserConfig config)
    {
        if (Uri.TryCreate(
            ranobeUrl,
            UriKind.Absolute,
            out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp ||
             absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        if (string.IsNullOrWhiteSpace(config.SourceUrl))
            throw new Exception("SourceUrl не указан");

        string result = config.SourceUrl.Replace(
            "{url}",
            ranobeUrl
        );

        if (Uri.TryCreate(
            result,
            UriKind.Absolute,
            out var resultUri))
        {
            return resultUri.ToString();
        }

        throw new Exception(
            $"Некорректный SourceUrl: {result}"
        );
    }

    // ==========================================
    // CONTENT PAGE URL
    // ==========================================

    private string BuildContentPageUrl(
        ParserConfig config,
        string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(config.ContentPageUrl))
            return sourceUrl;

        string url = config.ContentPageUrl.Replace(
            "{url}",
            ranobeUrl
        );

        if (Uri.TryCreate(
            url,
            UriKind.Absolute,
            out var absolute))
        {
            return absolute.ToString();
        }

        return ResolveUrl(sourceUrl, url);
    }

    // ==========================================
    // CHAPTER URLS + PAGINATION
    // ==========================================

    private async Task<List<string>> GetChapterUrlsAsync(
        IDocument mainDocument,
        string sourceUrl,
        ParserConfig config)
    {
        List<string> result = new();

        // 1. Ссылки напрямую заданы в JSON
        if (config.ListChaptersUrls.Count > 0)
        {
            foreach (string url in config.ListChaptersUrls)
            {
                AddUrl(result, sourceUrl, url);
            }

            return config.ManyChaptersUrls
                ? result
                : result.Take(1).ToList();
        }

        // 2. Стартовая страница содержания
        string currentPageUrl = BuildContentPageUrl(
            config,
            sourceUrl
        );

        // 3. Защита от повторной загрузки страниц
        HashSet<string> visitedPages = new(
            StringComparer.OrdinalIgnoreCase
        );

        int maxPages = config.MaxContentPages > 0
            ? config.MaxContentPages
            : 100;

        // Дополнительное ограничение
        maxPages = Math.Min(maxPages, 1000);

        // 4. Проходим по всем страницам содержания
        while (!string.IsNullOrWhiteSpace(currentPageUrl) &&
               visitedPages.Count < maxPages)
        {
            if (!visitedPages.Add(GetUrlKey(currentPageUrl)))
                break;

            IDocument contentDocument;

            // Не загружаем повторно основную страницу
            if (SameUrl(currentPageUrl, sourceUrl))
            {
                contentDocument = mainDocument;
            }
            else
            {
                contentDocument = await context.OpenAsync(
                    currentPageUrl
                );
            }

            // Выбираем селекторы ссылок
            List<string> selectors;

            if (!config.ManyChaptersUrls &&
                !string.IsNullOrWhiteSpace(config.ChapterUrl))
            {
                selectors = new List<string>
                {
                    config.ChapterUrl
                };
            }
            else
            {
                selectors = config.ContentSelectors;
            }

            // Получаем ссылки с текущей страницы
            foreach (string selector in selectors)
            {
                if (string.IsNullOrWhiteSpace(selector))
                    continue;

                foreach (var element in
                    contentDocument.QuerySelectorAll(selector))
                {
                    // Сам элемент является ссылкой
                    if (element.LocalName == "a")
                    {
                        AddUrl(
                            result,
                            currentPageUrl,
                            element.GetAttribute("href")
                        );
                    }

                    // Ссылка находится внутри элемента
                    foreach (var link in
                        element.QuerySelectorAll("a"))
                    {
                        AddUrl(
                            result,
                            currentPageUrl,
                            link.GetAttribute("href")
                        );
                    }
                }
            }

            // Для одной главы дальше не идём
            if (!config.ManyChaptersUrls)
                break;

            // ==================================
            // FIND NEXT CONTENT PAGE
            // ==================================

            string? nextPageUrl = GetNextContentPageUrl(
                contentDocument,
                currentPageUrl,
                config.NextContentPageSelectors
            );

            if (string.IsNullOrWhiteSpace(nextPageUrl))
                break;

            if (visitedPages.Contains(GetUrlKey(nextPageUrl)))
                break;

            currentPageUrl = nextPageUrl;
        }

        if (visitedPages.Count >= maxPages)
        {
            Console.WriteLine(
                $"Content pagination limit reached: {maxPages}"
            );
        }

        return result;
    }

    private string GetUrlKey(string url)
    {
        var uri = new Uri(url);

        var builder = new UriBuilder(uri)
        {
            Fragment = "",
            Path = uri.AbsolutePath.TrimEnd('/')
        };

        return builder.Uri.AbsoluteUri;
    }
    // ==========================================
    // FIND NEXT CONTENT PAGE
    // ==========================================

    private string? GetNextContentPageUrl(
        IDocument document,
        string currentPageUrl,
        List<string> selectors)
    {
        foreach (string selector in selectors)
        {
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            foreach (var element in document.QuerySelectorAll(selector))
            {
                string? href = null;

                if (element.LocalName == "a")
                {
                    href = element.GetAttribute("href");
                }
                else
                {
                    href = element.QuerySelector("a")?
                        .GetAttribute("href");
                }

                if (string.IsNullOrWhiteSpace(href))
                    continue;

                string absoluteUrl = ResolveUrl(
                    currentPageUrl,
                    href
                );

                if (SameUrl(absoluteUrl, currentPageUrl))
                    continue;

                return absoluteUrl;
            }
        }

        return null;
    }

    // ==========================================
    // ADD URL
    // ==========================================

    private void AddUrl(
        List<string> urls,
        string baseUrl,
        string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return;

        try
        {
            string url = ResolveUrl(baseUrl, href);

            if (!urls.Contains(
                url,
                StringComparer.OrdinalIgnoreCase))
            {
                urls.Add(url);
            }
        }
        catch
        {
            // Некорректная ссылка пропускается
        }
    }

    // ==========================================
    // RESOLVE URL
    // ==========================================

    private string ResolveUrl(string baseUrl, string url)
    {
        if (Uri.TryCreate(
            url,
            UriKind.Absolute,
            out var absolute))
        {
            return absolute.ToString();
        }

        return new Uri(
            new Uri(baseUrl),
            url
        ).ToString();
    }

    private bool SameUrl(string first, string second)
    {
        if (!Uri.TryCreate(first, UriKind.Absolute, out var a))
            return false;

        if (!Uri.TryCreate(second, UriKind.Absolute, out var b))
            return false;

        return Uri.Compare(
            a,
            b,
            UriComponents.AbsoluteUri,
            UriFormat.SafeUnescaped,
            StringComparison.OrdinalIgnoreCase
        ) == 0;
    }

    // ==========================================
    // CHAPTER CONTENT
    // ==========================================

    private async Task<List<ParagrafModel>>
        ParseChapterContentAsync(
            IDocument document,
            string chapterUrl,
            ChapterPath config,
            string chapterId)
    {
        // Режим выбирается конфигурацией
        if (config.RecursiveContentParsing)
        {
            return await ParseRecursiveAsync(
                document,
                chapterUrl,
                config,
                chapterId
            );
        }

        return await ParseFlatAsync(
            document,
            chapterUrl,
            config,
            chapterId
        );
    }

    // ==========================================
    // FLAT PARSING
    // ==========================================

    private async Task<List<ParagrafModel>> ParseFlatAsync(
        IDocument document,
        string chapterUrl,
        ChapterPath config,
        string chapterId)
    {
        List<ParagrafModel> result = new();

        var textElements = SelectElements(
            document,
            config.ParagraphSelectors
        );

        textElements = textElements = GetTopLevelElements(
            document,
            textElements
        ).ToHashSet();

        var imageElements = SelectImageElements(
            document,
            config.ImgSelectors
        );

        var selected = new HashSet<IElement>(
            textElements.Concat(imageElements)
        );

        int number = 0;

        // Обход в порядке расположения элементов в HTML
        foreach (var element in document.QuerySelectorAll("*"))
        {
            if (!selected.Contains(element))
                continue;

            if (imageElements.Contains(element))
            {
                byte[] image = await DownloadImageAsync(
                    chapterUrl,
                    element.GetAttribute(config.ImgUrl)
                );

                if (image.Length == 0)
                    continue;

                result.Add(new ParagrafModel
                {
                    Id = Guid.NewGuid().ToString(),
                    chapterId = chapterId,
                    number = number++,
                    text = "",
                    ImgHeader = image
                });

                continue;
            }

            if (textElements.Contains(element))
            {
                string text = element.TextContent.Trim();

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                result.Add(new ParagrafModel
                {
                    Id = Guid.NewGuid().ToString(),
                    chapterId = chapterId,
                    number = number++,
                    text = text,
                    ImgHeader = Array.Empty<byte>()
                });
            }
        }

        return result;
    }

    // ==========================================
    // RECURSIVE PARSING
    // ==========================================

    private async Task<List<ParagrafModel>> ParseRecursiveAsync(
        IDocument document,
        string chapterUrl,
        ChapterPath config,
        string chapterId)
    {
        var result = new List<ParagrafModel>();

        // Все изображения, подходящие под конфигурацию
        var imageElements = SelectImageElements(
            document,
            config.ImgSelectors
        );

        // Если задан общий контейнер, используем его.
        // Иначе собираем контейнеры из ParagraphSelectors
        // и ImgSelectors.
        List<IElement> containers;

        if (!string.IsNullOrWhiteSpace(
            config.ContentContainerSelector))
        {
            containers = document
                .QuerySelectorAll(
                    config.ContentContainerSelector
                )
                .ToList();
        }
        else
        {
            var textElements = SelectElements(
                document,
                config.ParagraphSelectors
            );

            containers = textElements
                .Concat(imageElements)
                .Distinct()
                .ToList();
        }

        // Убираем вложенные контейнеры из списка корней,
        // чтобы не обработать один и тот же текст дважды
        containers = GetTopLevelElements(
            document,
            containers
        );

        var state = new ContentBuildState(
            result,
            chapterId
        );

        bool restrictImages = config.ImgSelectors.Count > 0;

        foreach (var container in containers)
        {
            await TraverseNodeAsync(
                container,
                chapterUrl,
                config,
                imageElements,
                restrictImages,
                state
            );

            FlushText(state);
        }

        return result;
    }

    // ==========================================
    // RECURSIVE DOM TRAVERSAL
    // ==========================================

    private async Task TraverseNodeAsync(
        INode node,
        string chapterUrl,
        ChapterPath config,
        HashSet<IElement> imageElements,
        bool restrictImages,
        ContentBuildState state)
    {
        // Текстовый узел
        if (node.NodeType == NodeType.Text)
        {
            state.TextBuffer.Append(node.TextContent);
            return;
        }

        if (node is not IElement element)
            return;

        string tag = element.LocalName.ToLowerInvariant();

        // Не обрабатываем технические элементы
        if (tag == "script" ||
            tag == "style" ||
            tag == "noscript")
        {
            return;
        }

        // Изображение
        if (tag == "img")
        {
            FlushText(state);

            bool shouldParseImage =
                !restrictImages ||
                imageElements.Contains(element);

            if (shouldParseImage)
            {
                string? imageUrl = element.GetAttribute(
                    config.ImgUrl
                );

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    byte[] image = await DownloadImageAsync(
                        chapterUrl,
                        imageUrl
                    );

                    if (image.Length > 0)
                    {
                        state.Result.Add(new ParagrafModel
                        {
                            Id = Guid.NewGuid().ToString(),
                            chapterId = state.ChapterId,
                            number = state.Number++,
                            text = "",
                            ImgHeader = image
                        });
                    }
                }
            }

            return;
        }

        // Перенос строки разделяет текстовые блоки
        if (tag == "br" || tag == "hr")
        {
            FlushText(state);
            return;
        }

        bool isBlock = BlockTags.Contains(tag);

        if (isBlock)
            FlushText(state);

        // Рекурсивно обходим дочерние элементы
        foreach (var child in element.ChildNodes.ToArray())
        {
            await TraverseNodeAsync(
                child,
                chapterUrl,
                config,
                imageElements,
                restrictImages,
                state
            );
        }

        if (isBlock)
            FlushText(state);
    }

    // ==========================================
    // TEXT BUFFER FLUSH
    // ==========================================

    private void FlushText(ContentBuildState state)
    {
        string text = Regex.Replace(
            state.TextBuffer.ToString(),
            @"\s+",
            " "
        ).Trim();

        state.TextBuffer.Clear();

        if (string.IsNullOrWhiteSpace(text))
            return;

        state.Result.Add(new ParagrafModel
        {
            Id = Guid.NewGuid().ToString(),
            chapterId = state.ChapterId,
            number = state.Number++,
            text = text,
            ImgHeader = Array.Empty<byte>()
        });
    }

    // ==========================================
    // CONTENT STATE
    // ==========================================

    private sealed class ContentBuildState
    {
        public List<ParagrafModel> Result { get; }

        public string ChapterId { get; }

        public StringBuilder TextBuffer { get; } = new();

        public int Number { get; set; } = 0;

        public ContentBuildState(
            List<ParagrafModel> result,
            string chapterId)
        {
            Result = result;
            ChapterId = chapterId;
        }
    }

    // ==========================================
    // SELECT ELEMENTS
    // ==========================================

    private HashSet<IElement> SelectElements(
        IDocument document,
        List<string> selectors)
    {
        HashSet<IElement> result = new();

        foreach (string selector in selectors)
        {
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            foreach (var element in
                document.QuerySelectorAll(selector))
            {
                result.Add(element);
            }
        }

        return result;
    }

    // ==========================================
    // SELECT IMAGES
    // ==========================================

    private HashSet<IElement> SelectImageElements(
        IDocument document,
        List<string> selectors)
    {
        HashSet<IElement> result = new();

        foreach (string selector in selectors)
        {
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            foreach (var element in
                document.QuerySelectorAll(selector))
            {
                if (element.LocalName == "img")
                {
                    result.Add(element);
                }
                else
                {
                    foreach (var img in
                        element.QuerySelectorAll("img"))
                    {
                        result.Add(img);
                    }
                }
            }
        }

        return result;
    }

    // ==========================================
    // REMOVE NESTED SELECTED ELEMENTS
    // ==========================================

    private List<IElement> GetTopLevelElements(
        IDocument document,
        IEnumerable<IElement> elements)
    {
        var selected = new HashSet<IElement>(elements);

        return document.QuerySelectorAll("*")
            .Where(element => selected.Contains(element))
            .Where(element =>
            {
                var parent = element.ParentElement;

                while (parent != null)
                {
                    if (selected.Contains(parent))
                        return false;

                    parent = parent.ParentElement;
                }

                return true;
            })
            .ToList();
    }

    // ==========================================
    // DOWNLOAD IMAGE
    // ==========================================

    private async Task<byte[]> DownloadImageAsync(
        string baseUrl,
        string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Array.Empty<byte>();

        try
        {
            string url = ResolveUrl(baseUrl, imageUrl);

            return await http.GetByteArrayAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Image download error: {ex.Message}"
            );

            return Array.Empty<byte>();
        }
    }
}

public class ParseStatus
{
    public double status { get; set; }

    public string ranobeName { get; set; } = "";

    public int chaptersCount { get; set; }

    public int chaptersDone { get; set; }

    public double getStatus()
    {
        if (chaptersCount <= 0)
            return 0;

        return Math.Round(
            (double)chaptersDone / chaptersCount * 100,
            2
        );
    }
}
public class ParserConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string SiteHost { get; set; } = "";

    // Для выбора конфигурации внутри одного домена
    public string UrlContains { get; set; } = "";

    public bool IsDefault { get; set; } = false;

    // Основная страница произведения
    // {url} заменяется значением из Start()
    public string SourceUrl { get; set; } = "";

    // Отдельная страница содержания (необязательно)
    // Если пустая, используется SourceUrl
    public string ContentPageUrl { get; set; } = "";

    // Готовые ссылки на главы
    public List<string> ListChaptersUrls { get; set; } = new();

    public bool ManyChaptersUrls { get; set; } = true;

    // CSS-селектор ссылки на одну главу
    public string ChapterUrl { get; set; } = "";

    // CSS-селекторы ссылок на главы
    public List<string> ContentSelectors { get; set; } = new();

    // CSS-селекторы ссылок на следующую страницу содержания
    // Например: a[rel='next'], .pagination a.next
    public List<string> NextContentPageSelectors { get; set; } = new();

    // Защита от бесконечной пагинации
    public int MaxContentPages { get; set; } = 100;

    public HeaderPath HeaderPath { get; set; } = new();

    public ChapterPath ChapterPath { get; set; } = new();
}

public class HeaderPath
{
    public string HeaderSelector { get; set; } = "";

    public string ImgSelector { get; set; } = "";

    public string ImgUrl { get; set; } = "src";
}

public class ChapterPath
{
    public string HeaderSelector { get; set; } = "";

    public List<string> ParagraphSelectors { get; set; } = new();

    public List<string> ImgSelectors { get; set; } = new();

    public string ImgUrl { get; set; } = "src";

    // Включить рекурсивную обработку вложенных элементов
    public bool RecursiveContentParsing { get; set; } = false;

    // Общий контейнер содержимого главы
    // Используется при RecursiveContentParsing = true
    public string ContentContainerSelector { get; set; } = "";
}