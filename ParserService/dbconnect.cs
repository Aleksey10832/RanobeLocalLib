using Microsoft.EntityFrameworkCore;
namespace DbConnect;

public class Database: DbContext
{
    public DbSet<RanobeModel> Ranobes {get; set;}
    public DbSet<ChapterModel> Chapters {get; set;}
    public DbSet<ParagrafModel> Paragrafs {get; set;}
    public DbSet<ParseModel> Parsers {get; set;}

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(
            Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"));
    }
}