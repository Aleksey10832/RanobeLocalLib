using System.Net;
using System.Text;
using System.Text.Json;
using App.Result;
using DbConnect;
using Microsoft.EntityFrameworkCore;

namespace App.HttpClients.Parse;
public class ParsersMeneger {
    private readonly HttpClient _httpClient;
    private readonly Database _database;
    public ParsersMeneger(HttpClient httpClient, Database database) {
        this._httpClient = httpClient;
        this._database = database;
    }
    public async Task<Result<ParseModel>> GetParseStatus(string ranobeUrl, Guid userId){
        System.Console.WriteLine(await _httpClient.GetStringAsync($"parse/status/{userId}"));
        ParseModel? response = await _httpClient.GetFromJsonAsync<ParseModel>($"parse/status/{userId}");
        if(response != null){
            return Result<ParseModel>.Succesful(response);
        } else{
            return Result<ParseModel>.Fail(400, "Request is not valid");
        }
    }
     public async Task<Result<Ranobe>> GetPageAsync(string ranobeUrl, int page, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(
            $"parse/result/{Uri.EscapeDataString(ranobeUrl)}/{page}", ct);

        if (!response.IsSuccessStatusCode)
            return Result<Ranobe>.Fail((int)response.StatusCode, "Parser service error");

        var ranobe = await response.Content.ReadFromJsonAsync<Ranobe>(cancellationToken: ct);

        return ranobe is null
            ? Result<Ranobe>.Fail(400, "Request is not valid")
            : Result<Ranobe>.Succesful(ranobe);
    }
    public async Task<Result<ParseModel>> SetParseStatus(string ranobeUrl, string accessToken ){
        User? user = await _database.Users.SingleOrDefaultAsync(user => user.Login == Functions.GetLoginIsToken(accessToken));
        StringContent content =  new StringContent(JsonSerializer.Serialize(
            new ParseModel(user.Id, ranobeUrl)),
            Encoding.UTF8,
            "application/json"
        );
        HttpRequestMessage request = new HttpRequestMessage{
            RequestUri = new Uri(Environment.GetEnvironmentVariable("ParserAdress") + "/parse"),
            Content = content,
            Method = HttpMethod.Post,
        };
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        try{
            Result<ParseModel>? status = await response.Content.ReadFromJsonAsync<Result<ParseModel>>();
            return status;
        } catch {
            if(response.StatusCode == HttpStatusCode.NotFound){
                return Result<ParseModel>.Fail(404, "not found");
            } else if(response.StatusCode == HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.RequestTimeout) {
                return Result<ParseModel>.Fail(500, "server not connect or server error");
            }
            else{
                return Result<ParseModel>.Fail(400, "Request is not valid");
            }
        }
    }
}