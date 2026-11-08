public class ParseModel
{
    public Guid Id {get; set;}
    public Guid UserId {get; set;}
    public string RanobeUrl { get; set;} = "";
    public double? ProcessStatus { get; set;}
    public bool? IsDone { get; set;}
    public ParseModel(Guid UserId, string RanobeUrl) {
        this.RanobeUrl = RanobeUrl;
        this.UserId = UserId;
        this.Id = Guid.NewGuid();
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

}