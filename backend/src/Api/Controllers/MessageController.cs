using System.Collections.Generic;
using System.Linq;
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

namespace HashingDemo.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize(AuthenticationSchemes = TokenAuthenticationDefaults.AuthenticationScheme)]
public class MessageController(AppDbContext context) : ControllerBase
{
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

        if (string.IsNullOrWhiteSpace(sender.PrivateKey))
        {
            return BadRequest("Sender's private key is missing.");
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
        var messageHash = Sha256Pure.ComputeHash(messageBytes);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(sender.PrivateKey);
        var signature = rsa.SignHash(messageHash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureBase64 = Convert.ToBase64String(signature);

        var message = new Message
        {
            SenderId = sender.Id,
            RecipientId = recipient.Id,
            Content = request.Content,
            Signature = signatureBase64,
            CreatedAt = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync();

        return Created($"/api/messages/{message.Id}", new { messageId = message.Id });
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
            .Where(m => m.RecipientId == recipientId)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MessageSummaryResponse(
                m.Id,
                m.Sender != null ? m.Sender.Username : "(unknown)",
                m.Content,
                m.CreatedAt))
            .ToListAsync();

        return Ok(messages);
    }
}
