using System;
using HashingDemo.Api.Auth;
using HashingDemo.Api.Infrastructure;
using HashingDemo.Data;
using HashingDemo.Logic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        // Add response compression for better performance
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        });
    builder.Services.AddScoped<RsaKeyService>();
    builder.Services.AddSingleton<SignatureVisualizationService>();
    builder.Services.AddScoped<DemoUserSeeder>();
        builder.Services.AddSingleton<TokenStore>();
        builder.Services
            .AddAuthentication(TokenAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>(
                TokenAuthenticationDefaults.AuthenticationScheme,
                _ => { });
        builder.Services.AddAuthorization();

        var animationBaseUrl = builder.Configuration.GetValue<string>("AnimationService:BaseUrl")
            ?? "http://127.0.0.1:5000/";
        if (!animationBaseUrl.EndsWith('/'))
        {
            animationBaseUrl += "/";
        }

        builder.Services.AddHttpClient("AnimationService", client =>
        {
            client.BaseAddress = new Uri(animationBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var provider = db.Database.ProviderName;
            if (!string.IsNullOrEmpty(provider) && provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                db.Database.EnsureCreated();
            }
            else
            {
                db.Database.Migrate();
            }

            var seeder = scope.ServiceProvider.GetRequiredService<DemoUserSeeder>();
            seeder.SeedAsync().GetAwaiter().GetResult();
        }

        await SeedData.Initialize(app.Services);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Enable response compression
        app.UseResponseCompression();

        app.UseAuthentication();
        app.UseAuthorization();

        // Serve animation videos from shared volume. Ensure the directory exists so static files
        // middleware is always registered even if the shared volume is created after container start.
        var animationVideosPath = "/app/animation_videos";
        try
        {
            if (!Directory.Exists(animationVideosPath))
            {
                Directory.CreateDirectory(animationVideosPath);
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(animationVideosPath),
                RequestPath = "/animation_videos"
            });
        }
        catch
        {
            // Best-effort: if creating the directory or registering static files fails
            // (permission issues, read-only filesystem, etc.), continue running without
            // the static files middleware. Video serving will fail until resolved.
        }

        app.MapControllers();

        app.Run();
    }
}
