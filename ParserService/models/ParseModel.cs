using Microsoft.EntityFrameworkCore;

[Index(nameof(RanobeUrl), IsUnique = true)]
public class ParseModel
{
    public Guid Id {get; set;}
    public string UserId {get; set;} = "";
    public string RanobeUrl { get; set;} = "";
    public double? ProcessStatus { get; set;}
    public bool? IsDone { get; set;}
}