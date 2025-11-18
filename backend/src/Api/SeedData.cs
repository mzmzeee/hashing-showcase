using HashingDemo.Data;
using HashingDemo.Data.Entities;
using HashingDemo.Logic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;

public static class SeedData
{
    public static async Task Initialize(IServiceProvider services)
    {
        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rsaKeyService = scope.ServiceProvider.GetRequiredService<RsaKeyService>();

            if (await context.Users.AnyAsync())
            {
                return; // DB has been seeded
            }

            var users = new[]
            {
                new { Username = "alice", Password = "asdfasdf" },
                new { Username = "bob", Password = "asdfasdf" },
                new { Username = "evil_bob", Password = "asdfasdf" }
            };

            foreach (var userData in users)
            {
                var salt = PasswordHasher.GenerateSalt();
                var (privateKey, publicKey) = rsaKeyService.GenerateKeys();
                var user = new User
                {
                    Username = userData.Username,
                    PasswordHash = PasswordHasher.ComputePasswordHash(userData.Password, salt, 10000),
                    Salt = Convert.ToBase64String(salt),
                    Iterations = 10000,
                    PrivateKey = privateKey,
                    PublicKey = publicKey
                };
                context.Users.Add(user);
            }

            await context.SaveChangesAsync();
        }
    }
}
