using System;
using HashingDemo.Api.Auth;
using HashingDemo.Data;
using HashingDemo.Logic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

public class Program
{
    public static void Main(string[] args)
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
        builder.Services.AddScoped<SignatureVisualizationService>();
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
        }

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

        // Serve animation videos from shared volume (only if directory exists)
        var animationVideosPath = "/app/animation_videos";
        if (Directory.Exists(animationVideosPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(animationVideosPath),
                RequestPath = "/animation_videos"
            });
        }

        app.MapControllers();

        app.Run();
    }
}
