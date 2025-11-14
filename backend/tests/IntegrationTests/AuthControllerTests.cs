using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HashingDemo.Api.Models;
using HashingDemo.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        var messageHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(savedMessage.Content));
        var isValid = rsa.VerifyHash(messageHash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.True(isValid);

        using var visualizationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/visualize/signature")
        {
            Content = JsonContent.Create(new VisualizeSignatureRequest
            {
                MessageId = savedMessage.Id
            })
        };

        visualizationRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload.Token);

        // Use a shorter timeout because the animation service is stubbed for tests.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var visualizationResponse = await _client.SendAsync(visualizationRequest, cts.Token);
        visualizationResponse.EnsureSuccessStatusCode();
        Assert.Equal("video/mp4", visualizationResponse.Content.Headers.ContentType?.MediaType);

        using var stream = await visualizationResponse.Content.ReadAsStreamAsync(cts.Token);
        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
        Assert.True(bytesRead > 0, "Video file should have content");
    }

    private sealed record LoginResponsePayload(string Token, Guid UserId, string Username, string? PublicKey);

    private sealed record MessageCreatedResponse(Guid MessageId);

}

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["PasswordHashing:Iterations"] = "3"
            };

            config.AddInMemoryCollection(overrides);
        });

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

            // Replace AnimationService HTTP client with a fast in-memory stub for tests.
            services.AddHttpClient("AnimationService", client =>
            {
                client.BaseAddress = new Uri("http://localhost/");
                client.Timeout = TimeSpan.FromSeconds(5);
            }).ConfigurePrimaryHttpMessageHandler(static () => new FakeAnimationServiceHandler());

            using var scope = services.BuildServiceProvider().CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        });

        builder.UseEnvironment("Development");
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.Timeout = TimeSpan.FromMinutes(3); // Set timeout on test client
    }

    private sealed class FakeAnimationServiceHandler : HttpMessageHandler
    {
        private static readonly byte[] SampleVideoBytes = CreateSampleVideoBytes();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                // Drain the request content to mimic the real handler.
                _ = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(SampleVideoBytes)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
            return response;
        }

        private static byte[] CreateSampleVideoBytes() => Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
    }
}
