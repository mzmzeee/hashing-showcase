using System;
using HashingDemo.Data;
using HashingDemo.Data.Entities;
using HashingDemo.Logic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HashingDemo.Api.Infrastructure;

public sealed class DemoUserSeeder
{
    private static readonly string[] DefaultUsernames = ["alice", "bob", "evil_bob"];
    private const string DefaultPassword = "asdfasdf";

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly RsaKeyService _rsaKeyService;

    public DemoUserSeeder(AppDbContext context, IConfiguration configuration, RsaKeyService rsaKeyService)
    {
        _context = context;
        _configuration = configuration;
        _rsaKeyService = rsaKeyService;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var iterations = _configuration.GetValue("PasswordHashing:Iterations", 10000);
        iterations = Math.Max(1, iterations);

        foreach (var username in DefaultUsernames)
        {
            if (await _context.Users.AnyAsync(u => u.Username == username, cancellationToken))
            {
                continue;
            }

            var saltBytes = PasswordHasher.GenerateSalt();
            var passwordHash = PasswordHasher.ComputePasswordHash(DefaultPassword, saltBytes, iterations);
            var (publicKey, privateKey) = _rsaKeyService.GenerateKeys();

            var user = new User
            {
                Username = username,
                PasswordHash = passwordHash,
                Salt = PasswordHasher.ToHexString(saltBytes),
                Iterations = iterations,
                PublicKey = publicKey,
                PrivateKey = privateKey
            };

            _context.Users.Add(user);
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
