using System.Text.Json;
using DbConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
namespace App.Services.Hosted.Redis;
public class RedisHostedService : IHostedService {
    private readonly IDistributedCache cache;
    private readonly IServiceScopeFactory scopeFactory;
    public RedisHostedService(IServiceScopeFactory scopeFactory, IDistributedCache cache) {
        this.cache = cache;
        this.scopeFactory = scopeFactory;
    }
    public async Task StartAsync(CancellationToken cancellation) {
        using var scope = this.scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<Database>();
        List<Role> dbRoles = await database.Roles.Where(el => true).ToListAsync();
        if(dbRoles.Count > 0) {
            foreach(Role role in dbRoles) {
                await this.cache.SetStringAsync("Role" + role.Name, JsonSerializer.Serialize(role));
            }
        }
    }
    public Task StopAsync(CancellationToken cancellation) {
        return Task.CompletedTask;
    }
}