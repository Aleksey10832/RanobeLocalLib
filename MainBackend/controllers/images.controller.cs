using App.Controller.Filter.AuthFillter;
using App.Controller.Filter.PrivateFillter;
using DbConnect;
using Microsoft.AspNetCore.Mvc;

namespace App.Controller.ImageController;

[Route("/image")]
public class ImageController: ControllerBase
{
    private readonly Database database;
    public ImageController(Database database){
        this.database = database;
    }

    [HttpPost]
    [TypeFilter(typeof(AuthFillter), Arguments = ["uploadImg"])]
    public async Task<IActionResult>  UploadImage(IFormFile file, CancellationToken cancellation){
        
        System.Console.WriteLine("File name: " + file.FileName);

        Image dbImage = new Image();
        dbImage.Name = file.FileName;
        
        using(Stream fs = file.OpenReadStream()){
            byte[] buffer = new byte[fs.Length];
            await fs.ReadExactlyAsync(buffer);
            dbImage.file = buffer;
        }
        database.Image.Add(dbImage);
        await database.SaveChangesAsync();
        return File(file.OpenReadStream(), "image/" + file.FileName.Split('.')[^1], file.FileName);
    }

    [HttpGet("{ranobeId}/{fileId}")]
    [TypeFilter(typeof(PrivateFillter))]
    public async Task<IActionResult> GetImage([FromRoute] string fileId){
        Image? image = await database.Image.FindAsync(Guid.Parse(fileId));
        if(image != null){
            return File(image.file, "image/" + image.Name.Split('.')[^1], image.Name);
        }
        return NotFound();
    }
}