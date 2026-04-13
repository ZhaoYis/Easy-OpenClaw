using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace OpenClaw.Tests.SignalR;

internal static class TestJwtTokens
{
    internal static readonly byte[] SymmetricKeyBytes =
    [
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
    ];

    internal static string SymmetricKeyBase64 => Convert.ToBase64String(SymmetricKeyBytes);

    internal const string Issuer = "openclaw-signalr-test";
    internal const string Audience = "openclaw-signalr-test";

    internal static string CreateToken(string sub, string? tier = null, TimeSpan? lifetime = null)
    {
        var key = new SymmetricSecurityKey(SymmetricKeyBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim> { new("sub", sub) };
        if (tier is not null)
            claims.Add(new Claim("tier", tier));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow + (lifetime ?? TimeSpan.FromHours(1)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
