using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using HashingDemo.Api.Auth;
using HashingDemo.Api.Models;
using HashingDemo.Data;
using HashingDemo.Logic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HashingDemo.Api.Controllers;

[ApiController]
[Route("api/visualize")]
[Authorize(AuthenticationSchemes = TokenAuthenticationDefaults.AuthenticationScheme)]
public sealed class VisualizationController(
    AppDbContext context,
    SignatureVisualizationService visualizationService,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpPost("signature")]
    public async Task<IActionResult> VisualizeSignature(VisualizeSignatureRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var message = await context.Messages
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId);

        if (message is null)
        {
            return NotFound(new { error = "Message not found." });
        }

        if (message.RecipientId != userId && message.SenderId != userId)
        {
            return Forbid();
        }

        if (message.Sender is null || string.IsNullOrWhiteSpace(message.Sender.PublicKey))
        {
            return BadRequest(new { error = "Sender public key is missing." });
        }

        var visualizationData = visualizationService.Create(
            message.Content,
            message.Signature,
            message.Sender.PublicKey);

        var payload = new
        {
            message = visualizationData.Message,
            message_hash_hex = visualizationData.MessageHashHex,
            signature_base64 = visualizationData.SignatureBase64,
            decrypted_hash_hex = visualizationData.DecryptedHashHex,
            recomputed_hash_hex = visualizationData.RecomputedHashHex,
            hashes_match = visualizationData.HashesMatch
        };

        HttpResponseMessage animationResponse;
        try
        {
            var client = httpClientFactory.CreateClient("AnimationService");
            animationResponse = await client.PostAsJsonAsync("generate-animation", payload);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode((int)HttpStatusCode.BadGateway, new
            {
                error = "Unable to reach animation service.",
                details = ex.Message
            });
        }

        if (!animationResponse.IsSuccessStatusCode)
        {
            var errorBody = await animationResponse.Content.ReadAsStringAsync();
            return StatusCode((int)HttpStatusCode.BadGateway, new
            {
                error = "Animation service returned an error.",
                status = animationResponse.StatusCode,
                details = errorBody
            });
        }

        var contentType = animationResponse.Content.Headers.ContentType?.ToString() ?? "video/mp4";
        var animationStream = await animationResponse.Content.ReadAsStreamAsync();
        var buffered = new MemoryStream();
        await animationStream.CopyToAsync(buffered);
        buffered.Position = 0;

        Response.Headers.CacheControl = "no-store";
        return File(buffered, contentType, fileDownloadName: null, enableRangeProcessing: false);
    }
}
