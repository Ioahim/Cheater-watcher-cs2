using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CheaterWatcher.Api.Services.Auth;

public sealed class TokenService(IOptions<JwtOptions> options, JwtKeyProvider keyProvider)
{
    public string Issue(Domain.AppUser user)
    {
        var jwt = options.Value;
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            Expires = now.AddMinutes(jwt.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyProvider.Resolve())),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.UniqueName] = user.Username,
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
