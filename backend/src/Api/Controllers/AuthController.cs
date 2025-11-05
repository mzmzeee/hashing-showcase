using HashingDemo.Api.Auth;
using HashingDemo.Api.Models;
using HashingDemo.Data;
using HashingDemo.Data.Entities;
using HashingDemo.Logic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HashingDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    AppDbContext context,
    IConfiguration configuration,
    RsaKeyService rsaKeyService,
    TokenStore tokenStore) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
    var normalizedUsername = request.Username?.Trim() ?? string.Empty;

        if (normalizedUsername.Length < 3)
        {
            return BadRequest("Username must be at least 3 characters.");
        }

        if (await context.Users.AnyAsync(u => u.Username == normalizedUsername))
        {
            return BadRequest("Username already exists.");
        }

        var iterations = configuration.GetValue<int>("PasswordHashing:Iterations", 10000);
        if (iterations <= 0)
        {
            iterations = 10000;
        }

        var salt = PasswordHasher.GenerateSalt();
        var passwordHash = PasswordHasher.ComputePasswordHash(request.Password, salt, iterations);
        var (publicKey, privateKey) = rsaKeyService.GenerateKeys();

        var user = new User
        {
            Username = normalizedUsername,
            PasswordHash = passwordHash,
            Salt = PasswordHasher.ToHexString(salt),
            Iterations = iterations,
            PublicKey = publicKey,
            PrivateKey = privateKey
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var normalizedUsername = request.Username?.Trim() ?? string.Empty;
        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername);

        var iterations = user?.Iterations ?? configuration.GetValue<int>("PasswordHashing:Iterations", 10000);
        if (iterations <= 0)
        {
            iterations = 10000;
        }

        var saltBytes = user is not null
            ? PasswordHasher.FromHexString(user.Salt)
            : PasswordHasher.GenerateSalt();

        var computedHash = PasswordHasher.ComputePasswordHash(request.Password, saltBytes, iterations);
        var expectedHash = user?.PasswordHash ?? PasswordHasher.ComputePasswordHash(string.Empty, saltBytes, iterations);

        var isMatch = PasswordHasher.ConstantTimeEquals(expectedHash, computedHash);

        if (user is null || !isMatch)
        {
            return Unauthorized();
        }

        var token = tokenStore.IssueToken(user.Id);

        return Ok(new
        {
            token,
            userId = user.Id,
            username = user.Username,
            publicKey = user.PublicKey
        });
    }

    [HttpGet("public_keys")]
    public async Task<IActionResult> GetPublicKeys()
    {
        var users = await context.Users
            .Select(u => new { username = u.Username, publicKey = u.PublicKey })
            .ToListAsync();

        return Ok(users);
    }
}
