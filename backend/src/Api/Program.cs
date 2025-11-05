using System;
using HashingDemo.Api.Auth;
using HashingDemo.Data;
using HashingDemo.Logic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
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

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
