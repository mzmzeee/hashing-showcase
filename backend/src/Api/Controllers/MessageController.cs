using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HashingDemo.Api.Auth;
using HashingDemo.Api.Models;
using HashingDemo.Data;
using HashingDemo.Data.Entities;
using HashingDemo.Logic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HashingDemo.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize(AuthenticationSchemes = TokenAuthenticationDefaults.AuthenticationScheme)]
public class MessageController(
    AppDbContext context,
    IHttpClientFactory httpClientFactory,
    SignatureVisualizationService visualizationService,
    IServiceScopeFactory serviceScopeFactory) : ControllerBase
{
    private const string AnimationVideosRoot = "/app/animation_videos";

    [HttpPost]
    public async Task<IActionResult> SendMessage(SendMessageRequest request)
    {
        var senderIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(senderIdClaim) || !Guid.TryParse(senderIdClaim, out var senderId))
        {
            return Unauthorized();
        }

        var sender = await context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
        if (sender is null)
        {
            return Unauthorized();
        }

        var normalizedRecipient = request.RecipientUsername?.Trim() ?? string.Empty;
        if (normalizedRecipient.Length == 0)
        {
            return BadRequest("Recipient username is required.");
        }
        var recipient = await context.Users.FirstOrDefaultAsync(u => u.Username == normalizedRecipient);
        if (recipient is null)
        {
            return NotFound("Recipient user not found.");
        }

        var messageBytes = Encoding.UTF8.GetBytes(request.Content);
        var messageHash = System.Security.Cryptography.SHA256.HashData(messageBytes);
        
        string signatureBase64;
        string verificationStatus;

        // Handle signed and unsigned messages
        if (string.IsNullOrWhiteSpace(sender.PrivateKey) || string.IsNullOrWhiteSpace(sender.PublicKey))
        {
            // Unsigned message (fake user or no key)
            signatureBase64 = string.Empty;
            verificationStatus = "Unsigned";
        }
        else
        {
            // Sign the message
            using var rsa = RSA.Create();
            rsa.ImportFromPem(sender.PrivateKey);
            var signature = rsa.SignHash(messageHash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if (IsEvilBob(sender.Username))
            {
                CorruptSignature(signature);
            }

            signatureBase64 = Convert.ToBase64String(signature);

            // Immediately verify the signature to determine status
            using var verifyRsa = RSA.Create();
            verifyRsa.ImportFromPem(sender.PublicKey);
            var isValid = verifyRsa.VerifyHash(messageHash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            verificationStatus = isValid ? "Valid" : "Invalid";
        }

        // Corrupt signature if sender is evil_bob
        if (sender.Username == "evil_bob")
        {
            var signature = Convert.FromBase64String(signatureBase64);
            signature[0] ^= 0xFF; // Flip the first byte
            signatureBase64 = Convert.ToBase64String(signature);
            verificationStatus = "Invalid";
        }

        var message = new Message
        {
            SenderId = sender.Id,
            RecipientId = recipient.Id,
            Content = request.Content,
            Signature = signatureBase64,
            VerificationStatus = verificationStatus,
            VisualizationUrl = null,
            CreatedAt = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync();

        // Trigger background task to generate video (non-blocking, fire-and-forget)
        // Using ConfigureAwait(false) to avoid capturing context for better performance
        _ = Task.Run(async () => await GenerateVideoAsync(message.Id, request.Content, signatureBase64, sender.PublicKey, verificationStatus).ConfigureAwait(false));

        return Ok(new { messageId = message.Id });
    }

    [HttpPost("{messageId}/reanimate")]
    public async Task<IActionResult> ReanimateMessage(Guid messageId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var message = await context.Messages.Include(m => m.Sender).FirstOrDefaultAsync(m => m.Id == messageId);
        if (message is null)
        {
            return NotFound("Message not found.");
        }

        if (message.RecipientId != userId && message.SenderId != userId)
        {
            return Forbid();
        }

        if (message.Sender is null)
        {
            return BadRequest("Message sender not found.");
        }

        DeleteExistingVideo(messageId);
        message.VisualizationUrl = null;
        await context.SaveChangesAsync();

        // Trigger background task to re-generate video
        _ = Task.Run(async () => await GenerateVideoAsync(message.Id, message.Content, message.Signature, message.Sender.PublicKey, message.VerificationStatus).ConfigureAwait(false));

        return Ok(new { message = "Re-animation process started." });
    }

    [HttpDelete("{messageId}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var message = await context.Messages.FirstOrDefaultAsync(m => m.Id == messageId && (m.RecipientId == userId || m.SenderId == userId));
        if (message is null)
        {
            return NotFound();
        }

        context.Messages.Remove(message);
        await context.SaveChangesAsync();
        DeleteExistingVideo(messageId);

        return NoContent();
    }

    private async Task GenerateVideoAsync(Guid messageId, string content, string signatureBase64, string? publicKeyPem, string verificationStatus)
    {
        try
        {
            // Create visualization data if we have a public key and signature
            SignatureVisualizationData? visualizationData = null;
            if (!string.IsNullOrWhiteSpace(publicKeyPem) && !string.IsNullOrWhiteSpace(signatureBase64))
            {
                visualizationData = visualizationService.Create(content, signatureBase64, publicKeyPem);
            }

            // Prepare payload for animation service
            var payload = new
            {
                message = content,
                message_hash_hex = visualizationData?.MessageHashHex ?? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant(),
                signature_base64 = signatureBase64,
                decrypted_hash_hex = visualizationData?.DecryptedHashHex ?? string.Empty,
                recomputed_hash_hex = visualizationData?.RecomputedHashHex ?? visualizationData?.MessageHashHex ?? string.Empty,
                hashes_match = visualizationData?.HashesMatch ?? false,
                verification_status = verificationStatus
            };

            // Call animation service
            var client = httpClientFactory.CreateClient("AnimationService");
            var response = await client.PostAsJsonAsync("generate-animation", payload);

            if (!response.IsSuccessStatusCode)
            {
                // Log error but don't throw - we'll leave VisualizationUrl as null
                return;
            }

            // Save video to shared volume and update message with URL
            var videoStream = await response.Content.ReadAsStreamAsync();
            var videoPath = BuildVideoFilePath(messageId);

            
            // Ensure directory exists (only if path exists, otherwise skip video saving for tests)
            var directory = Path.GetDirectoryName(videoPath);
            if (!string.IsNullOrEmpty(directory))
            {
                if (!Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch
                    {
                        // Directory creation failed (likely in test environment), skip video saving
                        return;
                    }
                }

                try
                {
                    using (var fileStream = new FileStream(videoPath, FileMode.Create, FileAccess.Write))
                    {
                        await videoStream.CopyToAsync(fileStream);
                    }

                    // Update message with visualization URL using a new scope
                    using (var scope = serviceScopeFactory.CreateScope())
                    {
                        var updateContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var message = await updateContext.Messages.FindAsync(messageId);
                        if (message is not null)
                        {
                            message.VisualizationUrl = BuildCacheBustedVisualizationUrl(messageId);
                            await updateContext.SaveChangesAsync();
                        }
                    }
                }
                catch
                {
                    // File saving failed, skip but don't throw
                    return;
                }
            }
        }
        catch (Exception)
        {
            // Silently handle errors - video generation is best-effort
            // The VisualizationUrl will remain null
        }
    }

    private static bool IsEvilBob(string username)
        => string.Equals(username, "evil_bob", StringComparison.OrdinalIgnoreCase);

    private static void CorruptSignature(byte[] signature)
    {
        if (signature.Length == 0)
        {
            return;
        }

        signature[0] ^= 0xFF;
        if (signature.Length > 5)
        {
            signature[5] ^= 0xFF;
        }
    }

    private static string BuildVideoFilePath(Guid messageId)
    {
        if (string.IsNullOrWhiteSpace(AnimationVideosRoot))
        {
            return Path.Combine(Path.GetTempPath(), $"{messageId}.mp4");
        }

        return Path.Combine(AnimationVideosRoot, $"{messageId}.mp4");
    }

    private static string BuildCacheBustedVisualizationUrl(Guid messageId)
    {
        var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"/animation_videos/{messageId}.mp4?cb={cacheBuster}";
    }

    private static void DeleteExistingVideo(Guid messageId)
    {
        try
        {
            var videoPath = BuildVideoFilePath(messageId);
            if (System.IO.File.Exists(videoPath))
            {
                System.IO.File.Delete(videoPath);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<IEnumerable<MessageSummaryResponse>>> GetInbox()
    {
        var recipientIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(recipientIdClaim) || !Guid.TryParse(recipientIdClaim, out var recipientId))
        {
            return Unauthorized();
        }

        var messages = await context.Messages
            .AsNoTracking() // Read-only query, no tracking needed
            .Where(m => m.RecipientId == recipientId)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MessageSummaryResponse(
                m.Id,
                m.Sender != null ? m.Sender.Username : "(unknown)",
                m.Content,
                m.VerificationStatus,
                m.VisualizationUrl,
                m.CreatedAt))
            .ToListAsync();

        return Ok(messages);
    }

    [HttpDelete("{messageId}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var message = await context.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.RecipientId == userId);
        if (message is null)
        {
            return NotFound("Message not found or you don't have permission to delete it.");
        }

        context.Messages.Remove(message);
        await context.SaveChangesAsync();

        return Ok(new { message = "Message deleted successfully." });
    }
}
