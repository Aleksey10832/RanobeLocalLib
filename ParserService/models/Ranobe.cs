public class RanobeModel{
    public string Id {get; set;}
    public string Name {get; set;}
    public byte[] ImgHeader {get; set;}
    public Guid UserId {get; set;}
    public string RanobeUrl {get; set;}
    public List<ChapterModel> Chapters {get; set;}
}