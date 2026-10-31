using Microsoft.EntityFrameworkCore;

[Index(nameof(RanobeUrl), IsUnique = true)]
public class RanobeModel{
    public string Id {get; set;}
    public string Name {get; set;}
    public byte[] ImgHeader {get; set;}
    public string UserId {get; set;}
    public string RanobeUrl {get; set;}
    public List<ChapterModel> Chapters {get; set;}
}