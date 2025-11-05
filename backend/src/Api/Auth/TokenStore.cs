using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace HashingDemo.Api.Auth;

public class TokenStore
{
    private readonly ConcurrentDictionary<string, Guid> _tokenMap = new();

    public string IssueToken(Guid userId)
    {
        var buffer = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(buffer);
        _tokenMap[token] = userId;
        return token;
    }

    public bool TryGetUserId(string token, out Guid userId)
    {
        return _tokenMap.TryGetValue(token, out userId);
    }
}
