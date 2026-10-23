using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
public static class Functions
{
    public static string GenerateAccessToken (Guid userId, string login, string role, Guid sessionId)
    {
        JwtSecurityToken jwt = new (
            issuer: "MyAuthServer", 
            audience: "MyAPIClient", 
            claims: new List<Claim> () {
                new Claim(ClaimTypes.Role, role), 
                new Claim(ClaimTypes.Name, login),
                new Claim(ClaimTypes.Sid, sessionId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            },
            expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)), 
            signingCredentials: new SigningCredentials (new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    Environment.GetEnvironmentVariable("JWT_KEY")
                )),  
                SecurityAlgorithms.HmacSha256
            )
        );
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
    public static string? GetLoginIsToken (string jwtToken) {
        var validateParams = new TokenValidationParameters{
            ValidateIssuer = true,
            ValidIssuer = "MyAuthServer",
            ValidateAudience = true,
            ValidAudience = "MyAPIClient",
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY"))),
            ValidateIssuerSigningKey = true,
            
        };        
        try{
            var token = new JwtSecurityTokenHandler ().ValidateToken(
                jwtToken[7..], 
                validateParams, out _
            );
            return token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        }
        catch{
            return null;
        }
    }

    public static string? GetSessionIdIsToken (string jwtToken) {
        var validateParams = new TokenValidationParameters{
            ValidateIssuer = true,
            ValidIssuer = "MyAuthServer",
            ValidateAudience = true,
            ValidAudience = "MyAPIClient",
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY"))),
            ValidateIssuerSigningKey = true,
            
        };        
        try{
            var token = new JwtSecurityTokenHandler ().ValidateToken(
                jwtToken[7..], 
                validateParams, out _
            );
            return token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value;
        }
        catch{
            return null;
        }
    }
    public static string GetRoleIsToken(string jwtToken){
        var token = new JwtSecurityTokenHandler ().ValidateToken(
            jwtToken[7..], 
            ValidateParams, out _
        );
        return "Role" + token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
    }
    public static string? ValidateToken(string jwtToken){
        try{
            var token = new JwtSecurityTokenHandler ().ValidateToken(
                jwtToken[7..], 
                ValidateParams, out _
            );
            return token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value;
        } catch {
            return null;
        }
        
    }
    public static string? GetUserIdIsToken(string jwtToken){
        try{
            var token = new JwtSecurityTokenHandler ().ValidateToken(
                jwtToken[7..], 
                ValidateParams, out _
            );
            return token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        } catch {
            return null;
        }
        
    }

    public static TokenValidationParameters ValidateParams = new TokenValidationParameters{
        ValidateIssuer = true,
        ValidIssuer = "MyAuthServer",
        ValidateAudience = true,
        ValidAudience = "MyAPIClient",
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY"))),
        ValidateIssuerSigningKey = true,
    };

}