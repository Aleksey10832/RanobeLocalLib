using AngleSharp;
using AngleSharp.Dom;
using DbConnect;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HttpMethod = System.Net.Http.HttpMethod;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace App.Parser;

public class ParsersMeneger
{
    public static ConcurrentDictionary<string, Parse> parsers { get; } = new();

    private readonly IServiceScopeFactory scopeFactory;
    private readonly string configPath;

    // ВАЖНО: класс должен быть зарегистрирован как Singleton,
    // иначе блокировка configLock не защищает файл конфигурации.
    private readonly SemaphoreSlim configLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ParsersMeneger(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;

        configPath = Path.Combine(AppContext.BaseDirectory, "ParserConfigs.json");

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
    // CONFIG ADD / UPDATE / DELETE
    // ==========================================

    public async Task<bool> AddConfig(ParserConfig config)
    {
        config.Normalize();

        if (string.IsNullOrWhiteSpace(config.SiteHost))
            throw new ArgumentException("SiteHost не указан");

        config.Validate();

        await configLock.WaitAsync();
        try
        {
            var configs = await ReadConfigsUnsafe();

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

    public async Task<bool> UpdateConfig(string id, ParserConfig updatedConfig)
    {
        updatedConfig.Normalize();

        if (string.IsNullOrWhiteSpace(updatedConfig.SiteHost))
            throw new ArgumentException("SiteHost не указан");

        updatedConfig.Validate();

        await configLock.WaitAsync();
        try
        {
            var configs = await ReadConfigsUnsafe();

            int index = configs.FindIndex(x => x.Id == id);

            if (index < 0)
                return false;

            updatedConfig.Id = id;
            updatedConfig.SiteHost = NormalizeHost(updatedConfig.SiteHost);

            configs[index] = updatedConfig;
            await SaveConfigsUnsafe(configs);

            return true;
        }
        finally
        {
            configLock.Release();
        }
    }

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

        var list = JsonSerializer.Deserialize<List<ParserConfig>>(json, JsonOptions)
                   ?? new List<ParserConfig>();

        // В ручном JSON могут быть null вместо списков/объектов
        list.RemoveAll(x => x == null);

        foreach (var config in list)
            config.Normalize();

        return list;
    }

    private async Task SaveConfigsUnsafe(List<ParserConfig> configs)
    {
        string json = JsonSerializer.Serialize(configs, JsonOptions);

        string tempPath = configPath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json);

        File.Move(tempPath, configPath, true);
    }

    private static string NormalizeHost(string host)
    {
        host = (host ?? "").Trim();

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
            return uri.Host.ToLowerInvariant();

        return host.TrimEnd('/').Split('/')[0].ToLowerInvariant();
    }

    // Совпадение хоста: точное или поддомен (www.site.com для site.com)
    internal static bool HostMatches(string siteHost, string host)
    {
        if (string.IsNullOrWhiteSpace(siteHost) || string.IsNullOrWhiteSpace(host))
            return false;

        return string.Equals(host, siteHost, StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith("." + siteHost, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // CONFIG SELECT
    // ==========================================

    public async Task<ParserConfig?> FindConfig(string url)
    {
        var configs = await GetConfigs();

        // Абсолютный URL: выбираем конфигурацию по домену
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var matches = configs
                .Where(x => string.Equals(x.SiteHost, uri.Host, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.UrlContains.Length == 0 ||
                            url.Contains(x.UrlContains, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                return null;

            int bestLength = matches.Max(x => x.UrlContains.Length);

            var bestMatches = matches
                .Where(x => x.UrlContains.Length == bestLength)
                .ToList();

            if (bestMatches.Count == 1)
                return bestMatches[0];

            // SingleOrDefault бросал исключение при нескольких IsDefault
            var bestDefaults = bestMatches.Where(x => x.IsDefault).ToList();

            return bestDefaults.Count == 1 ? bestDefaults[0] : null;
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

    public bool Start(string ranobeUrl, string userId)
    {
        return StartInternal(ranobeUrl, userId, null);
    }

    public bool Start(string ranobeUrl, string userId, string configId)
    {
        return StartInternal(ranobeUrl, userId, configId);
    }

    private bool StartInternal(string ranobeUrl, string userId, string? configId)
    {
        var scope = scopeFactory.CreateScope();
        Parse? parser = null;

        try
        {
            var database = scope.ServiceProvider.GetRequiredService<Database>();

            parser = new Parse(database, this, scope, ranobeUrl, userId, configId);
            if (!parsers.TryAdd(userId.ToString(), parser))
            {
                parser.Dispose();
                scope.Dispose();
                return false;
            }

            _ = parser.startParse();

            return true;
        }
        catch
        {
            parser?.Dispose();
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

public class Parse : IDisposable
{
    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    private const int MaxRedirects = 5;
    private const long MaxPageBytes = 10L * 1024 * 1024;
    private const long MaxImageBytes = 25L * 1024 * 1024;

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public string userId;

    public ParseStatus status = new();

    private readonly ParsersMeneger parser;
    private readonly Database database;
    private readonly IServiceScope scope;

    private readonly string ranobeUrl;
    private readonly string? configId;

    private readonly HttpClient http;
    private readonly IBrowsingContext context;

    private ParseModel? dbParseStatus;

    // Сетевые настройки из конфига (заполняются в ConfigureNetwork)
    private string userAgent = DefaultUserAgent;
    private string? cookieHeader;
    private readonly List<string> trustedHosts = new();

    private bool disposed;

    private static readonly HashSet<string> BlockTags = new(
        new[]
        {
            "p", "div", "section", "article", "blockquote", "pre",
            "li", "ul", "ol", "table", "tr", "td", "th",
            "figure", "figcaption", "dd", "dt", "header", "footer",
            "h1", "h2", "h3", "h4", "h5", "h6"
        },
        StringComparer.OrdinalIgnoreCase
    );

    public Parse(
        Database database,
        ParsersMeneger parser,
        IServiceScope scope,
        string ranobeUrl,
        string userId,
        string? configId)
    {
        this.database = database;
        this.parser = parser;
        this.scope = scope;
        this.ranobeUrl = ranobeUrl;
        this.userId = userId;
        this.configId = configId;

        // Редиректы обрабатываем вручную, чтобы не отправить cookies на чужой хост.
        // UseCookies = false, иначе ручной заголовок Cookie игнорируется.
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All
        };

        http = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // Без загрузчика: все запросы идут через наш HttpClient
        context = BrowsingContext.New(Configuration.Default);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        http.Dispose();
        (context as IDisposable)?.Dispose();
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

            Dispose();
            scope.Dispose();
        }
    }

    // ==========================================
    // MAIN PARSE
    // ==========================================

    private async Task ParseInternal()
    {
        ParserConfig? config;

        if (!string.IsNullOrWhiteSpace(configId))
            config = await parser.GetConfig(configId);
        else
            config = await parser.FindConfig(ranobeUrl);

        if (config == null)
            throw new Exception($"Parser config not found: {ranobeUrl}");

        string sourceUrl = BuildSourceUrl(config);
        Uri sourceUri = EnsureSafeUri(sourceUrl);

        // Защита от SSRF: адрес должен принадлежать сайту из конфигурации
        if (!string.IsNullOrWhiteSpace(config.SiteHost) &&
            !ParsersMeneger.HostMatches(config.SiteHost, sourceUri.Host))
        {
            throw new Exception(
                $"Хост {sourceUri.Host} не соответствует конфигурации ({config.SiteHost})"
            );
        }

        ConfigureNetwork(config, sourceUri);

        using var document = await OpenDocumentAsync(sourceUrl);
        string documentUrl = document.Url ?? sourceUrl;

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
        if (!string.IsNullOrWhiteSpace(config.HeaderPath.HeaderSelector))
        {
            ranobe.Name = document
                .QuerySelector(config.HeaderPath.HeaderSelector)?
                .TextContent.Trim() ?? "";
        }

        status.ranobeName = ranobe.Name;

        // Обложка
        if (!string.IsNullOrWhiteSpace(config.HeaderPath.ImgSelector))
        {
            var image = document.QuerySelector(config.HeaderPath.ImgSelector);

            string? imageUrl = image?.GetAttribute(config.HeaderPath.ImgUrl);

            if (!string.IsNullOrWhiteSpace(imageUrl))
                ranobe.ImgHeader = await DownloadImageAsync(documentUrl, imageUrl);
        }

        // Список глав
        List<string> chapterUrls = await GetChapterUrlsAsync(document, sourceUrl, config);

        if (chapterUrls.Count == 0)
            throw new Exception("Не найдено ни одной ссылки на главу (проверьте селекторы)");

        status.chaptersCount = chapterUrls.Count;
        status.chaptersDone = 0;
        status.status = 0;
        try {
            // Статус в БД
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
        } catch {
            return;
        }
        

        // Главы
        List<ChapterModel> chapters = new();

        for (int i = 0; i < chapterUrls.Count; i++)
        {
            string chapterUrl = chapterUrls[i];

            try
            {
                using var chapterDocument = await OpenDocumentAsync(chapterUrl);

                ChapterModel chapter = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    ranobeId = ranobe.Id,
                    number = i + 1,
                    name = "Глава " + (i + 1),
                    paragrafs = new List<ParagrafModel>()
                };

                if (!string.IsNullOrWhiteSpace(config.ChapterPath.HeaderSelector))
                {
                    chapter.name = chapterDocument
                        .QuerySelector(config.ChapterPath.HeaderSelector)?
                        .TextContent.Trim() ?? chapter.name;
                }

                chapter.paragrafs = await ParseChapterContentAsync(
                    chapterDocument,
                    chapterDocument.Url ?? chapterUrl,
                    config.ChapterPath,
                    chapter.Id
                );

                chapters.Add(chapter);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chapter error [{chapterUrl}]: {ex.Message}");
            }
            finally
            {
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
                        Console.WriteLine($"Progress save error: {ex.Message}");
                    }
                }
            }
        }

        // Раньше пустая новелла сохранялась со статусом 100%
        if (chapters.Count == 0)
            throw new Exception("Не удалось загрузить ни одной главы");

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
    // NETWORK: USER-AGENT, COOKIES, REDIRECTS
    // ==========================================

    private void ConfigureNetwork(ParserConfig config, Uri sourceUri)
    {
        userAgent = string.IsNullOrWhiteSpace(config.UserAgent)
            ? DefaultUserAgent
            : config.UserAgent.Trim();

        cookieHeader = BuildCookieHeader(config);

        // Cookies отправляются только на хосты самого сайта
        trustedHosts.Clear();
        trustedHosts.Add(sourceUri.Host);

        if (!string.IsNullOrWhiteSpace(config.SiteHost))
            trustedHosts.Add(config.SiteHost);
    }

    private static string? BuildCookieHeader(ParserConfig config)
    {
        var parts = new List<string>();

        // Готовая строка, скопированная из DevTools: "a=1; b=2"
        if (!string.IsNullOrWhiteSpace(config.CookieHeader))
            parts.Add(config.CookieHeader.Trim().TrimEnd(';'));

        foreach (var pair in config.Cookies)
            parts.Add($"{pair.Key}={pair.Value}");

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private bool IsTrustedHost(string host)
    {
        return trustedHosts.Any(t => ParsersMeneger.HostMatches(t, host));
    }

    private void ApplyHeaders(HttpRequestMessage request, Uri uri)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

        if (cookieHeader != null && IsTrustedHost(uri.Host))
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
    }

    private static bool IsRedirect(HttpStatusCode code)
    {
        return code == HttpStatusCode.MovedPermanently ||
               code == HttpStatusCode.Found ||
               code == HttpStatusCode.SeeOther ||
               code == HttpStatusCode.TemporaryRedirect ||
               code == HttpStatusCode.PermanentRedirect;
    }

    // GET с ручной обработкой редиректов. Вызывающий обязан освободить ответ.
    private async Task<HttpResponseMessage> SendAsync(string url)
    {
        Uri current = EnsureSafeUri(url);

        for (int hop = 0; hop <= MaxRedirects; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);

            ApplyHeaders(request, current);

            var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead
            );

            if (IsRedirect(response.StatusCode) && response.Headers.Location != null)
            {
                var location = response.Headers.Location;

                Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);

                response.Dispose();

                current = EnsureSafeUri(next.ToString());
                continue;
            }

            return response;
        }

        throw new HttpRequestException($"Слишком много редиректов: {url}");
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, long maxBytes)
    {
        if (response.Content.Headers.ContentLength > maxBytes)
            throw new InvalidOperationException("Ответ превышает допустимый размер");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();

        var chunk = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new InvalidOperationException("Ответ превышает допустимый размер");

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private async Task<IDocument> OpenDocumentAsync(string url)
    {
        using var response = await SendAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} при загрузке {url}");

        byte[] bytes = await ReadLimitedAsync(response, MaxPageBytes);

        string finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
        string? contentType = response.Content.Headers.ContentType?.ToString();

        return await context.OpenAsync(req =>
        {
            req.Address(finalUrl);
            req.Content(new MemoryStream(bytes));

            if (!string.IsNullOrEmpty(contentType))
                req.Header("Content-Type", contentType);
        });
    }

    // ==========================================
    // URL SAFETY
    // ==========================================

    private static Uri EnsureSafeUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Недопустимый URL: {url}");
        }

        if (IsPrivateHost(uri))
            throw new InvalidOperationException($"Адрес во внутренней сети запрещён: {uri.Host}");

        return uri;
    }

    // Проверка localhost и IP-литералов. DNS-имена, указывающие на
    // внутренние адреса, этим не ловятся (нужна проверка после резолва).
    private static bool IsPrivateHost(Uri uri)
    {
        string host = uri.DnsSafeHost;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        byte[] b = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return b[0] == 0 || b[0] == 10 || b[0] == 127 ||
                   (b[0] == 169 && b[1] == 254) ||
                   (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                   (b[0] == 192 && b[1] == 168) ||
                   (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }

        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (b[0] & 0xFE) == 0xFC;
    }

    // ==========================================
    // BUILD SOURCE URL
    // ==========================================

    private string BuildSourceUrl(ParserConfig config)
    {
        if (Uri.TryCreate(ranobeUrl, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp ||
             absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        if (string.IsNullOrWhiteSpace(config.SourceUrl))
            throw new Exception("SourceUrl не указан");

        string result = config.SourceUrl.Replace("{url}", ranobeUrl);

        if (Uri.TryCreate(result, UriKind.Absolute, out var resultUri))
            return resultUri.ToString();

        throw new Exception($"Некорректный SourceUrl: {result}");
    }

    private string BuildContentPageUrl(ParserConfig config, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(config.ContentPageUrl))
            return sourceUrl;

        string url = config.ContentPageUrl.Replace("{url}", ranobeUrl);

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.ToString();

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
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        // 1. Ссылки напрямую заданы в JSON
        if (config.ListChaptersUrls.Count > 0)
        {
            foreach (string url in config.ListChaptersUrls)
                AddUrl(result, seen, sourceUrl, url);

            return config.ManyChaptersUrls ? result : result.Take(1).ToList();
        }

        // 2. Стартовая страница содержания
        string currentPageUrl = BuildContentPageUrl(config, sourceUrl);

        HashSet<string> visitedPages = new(StringComparer.OrdinalIgnoreCase);

        int maxPages = config.MaxContentPages > 0 ? config.MaxContentPages : 100;
        maxPages = Math.Min(maxPages, 1000);

        // 3. Страницы содержания
        while (!string.IsNullOrWhiteSpace(currentPageUrl) &&
               visitedPages.Count < maxPages)
        {
            if (!visitedPages.Add(GetUrlKey(currentPageUrl)))
                break;

            // Основную страницу повторно не загружаем и не освобождаем
            bool ownsDocument = !SameUrl(currentPageUrl, sourceUrl);

            IDocument contentDocument = ownsDocument
                ? await OpenDocumentAsync(currentPageUrl)
                : mainDocument;

            try
            {
                string baseUrl = contentDocument.Url ?? currentPageUrl;

                List<string> selectors;

                if (!config.ManyChaptersUrls &&
                    !string.IsNullOrWhiteSpace(config.ChapterUrl))
                {
                    selectors = new List<string> { config.ChapterUrl };
                }
                else
                {
                    selectors = config.ContentSelectors;
                }

                foreach (string selector in selectors)
                {
                    if (string.IsNullOrWhiteSpace(selector))
                        continue;

                    foreach (var element in contentDocument.QuerySelectorAll(selector))
                    {
                        if (element.LocalName == "a")
                            AddUrl(result, seen, baseUrl, element.GetAttribute("href"));

                        foreach (var link in element.QuerySelectorAll("a"))
                            AddUrl(result, seen, baseUrl, link.GetAttribute("href"));
                    }
                }

                // Для одной главы дальше не идём
                if (!config.ManyChaptersUrls)
                    break;

                string? nextPageUrl = GetNextContentPageUrl(
                    contentDocument,
                    baseUrl,
                    config.NextContentPageSelectors
                );

                if (string.IsNullOrWhiteSpace(nextPageUrl))
                    break;

                if (visitedPages.Contains(GetUrlKey(nextPageUrl)))
                    break;

                currentPageUrl = nextPageUrl;
            }
            finally
            {
                if (ownsDocument)
                    contentDocument.Dispose();
            }
        }

        if (visitedPages.Count >= maxPages)
            Console.WriteLine($"Content pagination limit reached: {maxPages}");

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
                string? href = element.LocalName == "a"
                    ? element.GetAttribute("href")
                    : element.QuerySelector("a")?.GetAttribute("href");

                if (string.IsNullOrWhiteSpace(href))
                    continue;

                string absoluteUrl;

                try
                {
                    absoluteUrl = ResolveUrl(currentPageUrl, href);
                }
                catch
                {
                    continue;
                }

                if (SameUrl(absoluteUrl, currentPageUrl))
                    continue;

                return absoluteUrl;
            }
        }

        return null;
    }

    private void AddUrl(
        List<string> urls,
        HashSet<string> seen,
        string baseUrl,
        string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return;

        try
        {
            string url = ResolveUrl(baseUrl, href.Trim());

            // Только http/https (отсекаем javascript:, mailto: и т.п.)
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return;
            }

            if (seen.Add(url))
                urls.Add(url);
        }
        catch
        {
            // Некорректная ссылка пропускается
        }
    }

    private string ResolveUrl(string baseUrl, string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        return new Uri(new Uri(baseUrl), url).ToString();
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

    private async Task<List<ParagrafModel>> ParseChapterContentAsync(
        IDocument document,
        string chapterUrl,
        ChapterPath config,
        string chapterId)
    {
        if (config.RecursiveContentParsing)
            return await ParseRecursiveAsync(document, chapterUrl, config, chapterId);

        return await ParseFlatAsync(document, chapterUrl, config, chapterId);
    }

    private async Task<List<ParagrafModel>> ParseFlatAsync(
        IDocument document,
        string chapterUrl,
        ChapterPath config,
        string chapterId)
    {
        List<ParagrafModel> result = new();

        var textElements = GetTopLevelElements(
            document,
            SelectElements(document, config.ParagraphSelectors)
        ).ToHashSet();

        var imageElements = SelectImageElements(document, config.ImgSelectors);

        var selected = new HashSet<IElement>(textElements.Concat(imageElements));

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

    private async Task<List<ParagrafModel>> ParseRecursiveAsync(
        IDocument document,
        string chapterUrl,
        ChapterPath config,
        string chapterId)
    {
        var result = new List<ParagrafModel>();

        var imageElements = SelectImageElements(document, config.ImgSelectors);

        List<IElement> containers;

        if (!string.IsNullOrWhiteSpace(config.ContentContainerSelector))
        {
            containers = document
                .QuerySelectorAll(config.ContentContainerSelector)
                .ToList();
        }
        else
        {
            var textElements = SelectElements(document, config.ParagraphSelectors);

            containers = textElements.Concat(imageElements).Distinct().ToList();
        }

        // Убираем вложенные корни, чтобы не обработать текст дважды
        containers = GetTopLevelElements(document, containers);

        var state = new ContentBuildState(result, chapterId);

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

    private async Task TraverseNodeAsync(
        INode node,
        string chapterUrl,
        ChapterPath config,
        HashSet<IElement> imageElements,
        bool restrictImages,
        ContentBuildState state)
    {
        if (node.NodeType == NodeType.Text)
        {
            state.TextBuffer.Append(node.TextContent);
            return;
        }

        if (node is not IElement element)
            return;

        string tag = element.LocalName.ToLowerInvariant();

        if (tag == "script" || tag == "style" || tag == "noscript")
            return;

        if (tag == "img")
        {
            FlushText(state);

            bool shouldParseImage = !restrictImages || imageElements.Contains(element);

            if (shouldParseImage)
            {
                string? imageUrl = element.GetAttribute(config.ImgUrl);

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    byte[] image = await DownloadImageAsync(chapterUrl, imageUrl);

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

        if (tag == "br" || tag == "hr")
        {
            FlushText(state);
            return;
        }

        bool isBlock = BlockTags.Contains(tag);

        if (isBlock)
            FlushText(state);

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

    private void FlushText(ContentBuildState state)
    {
        string text = WhitespaceRegex.Replace(state.TextBuffer.ToString(), " ").Trim();

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

    private sealed class ContentBuildState
    {
        public List<ParagrafModel> Result { get; }

        public string ChapterId { get; }

        public StringBuilder TextBuffer { get; } = new();

        public int Number { get; set; } = 0;

        public ContentBuildState(List<ParagrafModel> result, string chapterId)
        {
            Result = result;
            ChapterId = chapterId;
        }
    }

    // ==========================================
    // SELECTORS
    // ==========================================

    private HashSet<IElement> SelectElements(IDocument document, List<string> selectors)
    {
        HashSet<IElement> result = new();

        foreach (string selector in selectors)
        {
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            foreach (var element in document.QuerySelectorAll(selector))
                result.Add(element);
        }

        return result;
    }

    private HashSet<IElement> SelectImageElements(IDocument document, List<string> selectors)
    {
        HashSet<IElement> result = new();

        foreach (string selector in selectors)
        {
            if (string.IsNullOrWhiteSpace(selector))
                continue;

            foreach (var element in document.QuerySelectorAll(selector))
            {
                if (element.LocalName == "img")
                {
                    result.Add(element);
                }
                else
                {
                    foreach (var img in element.QuerySelectorAll("img"))
                        result.Add(img);
                }
            }
        }

        return result;
    }

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

    private async Task<byte[]> DownloadImageAsync(string baseUrl, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Array.Empty<byte>();

        try
        {
            string url = ResolveUrl(baseUrl, imageUrl.Trim());

            using var response = await SendAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Image HTTP {(int)response.StatusCode}: {url}");
                return Array.Empty<byte>();
            }

            // Не сохраняем HTML-страницы ошибок как «картинки»
            string? mediaType = response.Content.Headers.ContentType?.MediaType;

            if (mediaType != null &&
                !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Image skipped, content-type {mediaType}: {url}");
                return Array.Empty<byte>();
            }

            return await ReadLimitedAsync(response, MaxImageBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Image download error: {ex.Message}");

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

        return Math.Round((double)chaptersDone / chaptersCount * 100, 2);
    }
}

public class ParserConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string SiteHost { get; set; } = "";

    // Для выбора конфигурации внутри одного домена
    public string UrlContains { get; set; } = "";

    public bool IsDefault { get; set; } = false;

    // Основная страница произведения. {url} заменяется значением из Start()
    public string SourceUrl { get; set; } = "";

    // Отдельная страница содержания (необязательно)
    public string ContentPageUrl { get; set; } = "";

    // Готовые ссылки на главы
    public List<string> ListChaptersUrls { get; set; } = new();

    public bool ManyChaptersUrls { get; set; } = true;

    // CSS-селектор ссылки на одну главу
    public string ChapterUrl { get; set; } = "";

    // CSS-селекторы ссылок на главы
    public List<string> ContentSelectors { get; set; } = new();

    // CSS-селекторы ссылок на следующую страницу содержания
    public List<string> NextContentPageSelectors { get; set; } = new();

    // Защита от бесконечной пагинации
    public int MaxContentPages { get; set; } = 100;

    // ---------- Сетевые настройки ----------

    // Пустое значение = User-Agent по умолчанию (обычный Chrome)
    public string UserAgent { get; set; } = "";

    // Cookies по именам: { "cf_clearance": "...", "session": "..." }
    public Dictionary<string, string> Cookies { get; set; } = new();

    // Готовая строка Cookie из DevTools: "a=1; b=2" (необязательно,
    // объединяется с Cookies)
    public string CookieHeader { get; set; } = "";

    public HeaderPath HeaderPath { get; set; } = new();

    public ChapterPath ChapterPath { get; set; } = new();

    // Заменяет null (из ручного JSON) значениями по умолчанию
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Id))
            Id = Guid.NewGuid().ToString();

        SiteHost = (SiteHost ?? "").Trim();
        UrlContains = (UrlContains ?? "").Trim();
        SourceUrl ??= "";
        ContentPageUrl ??= "";
        ChapterUrl ??= "";

        ListChaptersUrls ??= new();
        ContentSelectors ??= new();
        NextContentPageSelectors ??= new();

        UserAgent = (UserAgent ?? "").Trim();
        CookieHeader = (CookieHeader ?? "").Trim();

        var cookies = new Dictionary<string, string>();

        if (Cookies != null)
        {
            foreach (var pair in Cookies)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                cookies[pair.Key.Trim()] = pair.Value ?? "";
            }
        }

        Cookies = cookies;

        HeaderPath ??= new();
        HeaderPath.HeaderSelector ??= "";
        HeaderPath.ImgSelector ??= "";

        if (string.IsNullOrWhiteSpace(HeaderPath.ImgUrl))
            HeaderPath.ImgUrl = "src";

        ChapterPath ??= new();
        ChapterPath.HeaderSelector ??= "";
        ChapterPath.ParagraphSelectors ??= new();
        ChapterPath.ImgSelectors ??= new();
        ChapterPath.ContentContainerSelector ??= "";

        if (string.IsNullOrWhiteSpace(ChapterPath.ImgUrl))
            ChapterPath.ImgUrl = "src";
    }

    // Проверка значений, попадающих в HTTP-заголовки
    public void Validate()
    {
        if (UserAgent.Length > 1024)
            throw new ArgumentException("UserAgent слишком длинный");

        CheckHeaderValue("UserAgent", UserAgent);
        CheckHeaderValue("CookieHeader", CookieHeader);

        foreach (var pair in Cookies)
        {
            if (pair.Key.IndexOfAny(new[] { '=', ';', ',', ' ', '\t' }) >= 0)
                throw new ArgumentException($"Некорректное имя cookie: {pair.Key}");

            CheckHeaderValue($"Cookies[{pair.Key}]", pair.Value);

            if (pair.Value.Contains(';'))
                throw new ArgumentException($"Значение cookie '{pair.Key}' содержит ';'");
        }
    }

    private static void CheckHeaderValue(string field, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        foreach (char ch in value)
        {
            if ((ch < 0x20 && ch != '\t') || ch == 0x7F)
                throw new ArgumentException($"{field}: недопустимые управляющие символы");
        }
    }
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

    // Общий контейнер содержимого главы (при RecursiveContentParsing = true)
    public string ContentContainerSelector { get; set; } = "";
}
