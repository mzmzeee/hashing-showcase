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

    [Fact]
    public async Task SendMessage_CreatesSignatureAndTriggersAnimation()
    {
        // Arrange
        var senderUsername = "sender_anim_test";
        var recipientUsername = "recipient_anim_test";
        var password = "password123";
        var messageContent = "Test message for animation";

        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Username = senderUsername, Password = password });
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Username = recipientUsername, Password = password });

        var senderToken = await LoginAndGetTokenAsync(senderUsername, password);

        // Act
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/messages")
        {
            Content = JsonContent.Create(new SendMessageRequest
            {
                RecipientUsername = recipientUsername,
                Content = messageContent
            })
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", senderToken);

        var sendResponse = await _client.SendAsync(requestMessage);
        sendResponse.EnsureSuccessStatusCode();

        var messageResult = await sendResponse.Content.ReadFromJsonAsync<MessageCreatedResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(messageResult);

        // Assert: Check database for signature and initial state
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedMessage = await dbContext.Messages.FindAsync(messageResult.MessageId);

        Assert.NotNull(savedMessage);
        Assert.False(string.IsNullOrWhiteSpace(savedMessage.Signature));
        Assert.Null(savedMessage.VisualizationUrl); // Should be null initially

        // Assert: Wait for background processing and check for visualization URL
        var attempts = 0;
        while (attempts < 30)
        {
            await Task.Delay(500);
            await dbContext.Entry(savedMessage).ReloadAsync();
            if (!string.IsNullOrEmpty(savedMessage.VisualizationUrl))
            {
                break;
            }
            attempts++;
        }

        Assert.False(string.IsNullOrWhiteSpace(savedMessage.VisualizationUrl), "VisualizationUrl should be set after background processing.");
        Assert.Contains(".mp4", savedMessage.VisualizationUrl);
    }

    [Fact]
    public async Task EvilBobMessagesAreMarkedInvalid()
    {
        var evilBobPassword = "asdfasdf";
        var recipientUsername = "recipient_evil_target";
        var recipientPassword = "recipient-password";

        // Ensure evil_bob exists (registration may fail if the seeder already created the account).
        _ = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "evil_bob",
            Password = evilBobPassword
        });

        var recipientRegisterResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = recipientUsername,
            Password = recipientPassword
        });
        recipientRegisterResponse.EnsureSuccessStatusCode();

        var evilBobToken = await LoginAndGetTokenAsync("evil_bob", evilBobPassword);

        using var maliciousMessageRequest = new HttpRequestMessage(HttpMethod.Post, "/api/messages")
        {
            Content = JsonContent.Create(new SendMessageRequest
            {
                RecipientUsername = recipientUsername,
                Content = "Malicious payload"
            })
        };
        maliciousMessageRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", evilBobToken);

        var maliciousSendResponse = await _client.SendAsync(maliciousMessageRequest);
        maliciousSendResponse.EnsureSuccessStatusCode();

        var recipientToken = await LoginAndGetTokenAsync(recipientUsername, recipientPassword);

        using var inboxRequest = new HttpRequestMessage(HttpMethod.Get, "/api/messages/inbox");
        inboxRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", recipientToken);

        var inboxResponse = await _client.SendAsync(inboxRequest);
        inboxResponse.EnsureSuccessStatusCode();

        var inboxMessages = await inboxResponse.Content.ReadFromJsonAsync<List<MessageSummaryResponse>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(inboxMessages);
        Assert.Contains(inboxMessages!, message =>
            message.SenderUsername == "evil_bob" &&
            string.Equals(message.VerificationStatus, "Invalid", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record LoginResponsePayload(string Token, Guid UserId, string Username, string? PublicKey);

    private sealed record MessageCreatedResponse(Guid MessageId);

    private async Task<string> LoginAndGetTokenAsync(string username, string password)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        loginResponse.EnsureSuccessStatusCode();

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponsePayload>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(loginPayload);
        return loginPayload!.Token;
    }

}

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["PasswordHashing:Iterations"] = "3",
                ["AnimationVideos:RootPath"] = Path.Combine(Path.GetTempPath(), "HashingDemoTests", Guid.NewGuid().ToString())
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
