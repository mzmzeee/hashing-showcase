using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HashingDemo.Api.Models;

public class SendMessageRequest
{
    [Required]
    [JsonPropertyName("recipient_username")]
    public required string RecipientUsername { get; set; }

    [Required]
    [MinLength(1)]
    [JsonPropertyName("content")]
    public required string Content { get; set; }
}

public sealed record MessageSummaryResponse(
    [property: JsonPropertyName("message_id")] Guid MessageId,
    [property: JsonPropertyName("sender_username")] string SenderUsername,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("verification_status")] string VerificationStatus,
    [property: JsonPropertyName("visualization_url")] string? VisualizationUrl,
    [property: JsonPropertyName("created_at_utc")] DateTime CreatedAtUtc);

public class VisualizeSignatureRequest
{
    [Required]
    [JsonPropertyName("message_id")]
    public Guid MessageId { get; set; }
}
