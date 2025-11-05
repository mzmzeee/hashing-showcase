using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HashingDemo.Api.Models;
using HashingDemo.Data;
using HashingDemo.Logic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationTests;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public AuthControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_And_Login_Succeeds()
    {
        // Arrange
        var username = "testuser";
        var password = "testpassword";
        var registerRequest = new RegisterRequest { Username = username, Password = password };
        var loginRequest = new LoginRequest { Username = username, Password = password };

        // Act: Register
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert: Register
        registerResponse.EnsureSuccessStatusCode();

        // Act: Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert: Login
        loginResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Fails()
    {
        // Arrange
        var username = "testuser2";
        var password = "testpassword";
        var wrongPassword = "wrongpassword";
        var registerRequest = new RegisterRequest { Username = username, Password = password };
        var loginRequest = new LoginRequest { Username = username, Password = wrongPassword };

        // Act: Register
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        // Act: Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert: Login
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task SendMessage_SignsContentWithSenderKey()
    {
        var senderUsername = "sender";
        var senderPassword = "sender-password";
        var recipientUsername = "recipient";
        var recipientPassword = "recipient-password";

        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = senderUsername,
            Password = senderPassword
        });

        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = recipientUsername,
            Password = recipientPassword
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = senderUsername,
            Password = senderPassword
        });
        loginResponse.EnsureSuccessStatusCode();

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponsePayload>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(loginPayload);
        Assert.False(string.IsNullOrWhiteSpace(loginPayload!.Token));

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/messages")
        {
            Content = JsonContent.Create(new SendMessageRequest
            {
                RecipientUsername = recipientUsername,
                Content = "Hello from sender"
            })
        };

        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload.Token);

        var sendResponse = await _client.SendAsync(requestMessage);
        sendResponse.EnsureSuccessStatusCode();

        var messageResult = await sendResponse.Content.ReadFromJsonAsync<MessageCreatedResponse>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(messageResult);
        Assert.NotEqual(Guid.Empty, messageResult!.MessageId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedMessage = await dbContext.Messages.FirstOrDefaultAsync(m => m.Id == messageResult.MessageId);

        Assert.NotNull(savedMessage);
        Assert.Equal("Hello from sender", savedMessage!.Content);
        Assert.False(string.IsNullOrWhiteSpace(savedMessage.Signature));

        var sender = await dbContext.Users.FirstAsync(u => u.Id == savedMessage.SenderId);
        Assert.False(string.IsNullOrWhiteSpace(sender.PublicKey));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(sender.PublicKey);

        var signatureBytes = Convert.FromBase64String(savedMessage.Signature);
        var messageHash = Sha256Pure.ComputeHash(Encoding.UTF8.GetBytes(savedMessage.Content));
        var isValid = rsa.VerifyHash(messageHash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.True(isValid);

        var repoRoot = GetRepositoryRoot();
        using var flaskProcess = StartAnimationService(repoRoot);
        try
        {
            var animationBaseUri = new Uri("http://127.0.0.1:5000/");
            await WaitForAnimationServiceAsync(animationBaseUri, TimeSpan.FromSeconds(30));

            using var visualizationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/visualize/signature")
            {
                Content = JsonContent.Create(new VisualizeSignatureRequest
                {
                    MessageId = savedMessage.Id
                })
            };

            visualizationRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload.Token);

            using var visualizationResponse = await _client.SendAsync(visualizationRequest);
            visualizationResponse.EnsureSuccessStatusCode();
            Assert.Equal("video/mp4", visualizationResponse.Content.Headers.ContentType?.MediaType);
            var animationBytes = await visualizationResponse.Content.ReadAsByteArrayAsync();
            Assert.NotEmpty(animationBytes);
        }
        finally
        {
            if (!flaskProcess.HasExited)
            {
                flaskProcess.Kill(entireProcessTree: true);
                flaskProcess.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
    }

    private sealed record LoginResponsePayload(string Token, Guid UserId, string Username, string? PublicKey);

    private sealed record MessageCreatedResponse(Guid MessageId);

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
    }

    private static Process StartAnimationService(string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = "animation_service/app.py",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start animation service process.");
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static async Task WaitForAnimationServiceAsync(Uri baseAddress, TimeSpan timeout)
    {
        using var client = new HttpClient();
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                using var response = await client.GetAsync(baseAddress);
                _ = response.StatusCode; // we only care that the server responded
                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException($"Animation service did not start within {timeout.TotalSeconds} seconds.");
    }
}

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            var provider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting");
                options.UseInternalServiceProvider(provider);
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        });

        builder.UseEnvironment("Development");
    }
}
