using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HashingDemo.Data.Entities;

[Table("messages")]
public class Message
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [Column("sender_id")]
    public Guid SenderId { get; set; }

    public User? Sender { get; set; }

    [Required]
    [Column("recipient_id")]
    public Guid RecipientId { get; set; }

    public User? Recipient { get; set; }

    [Required]
    [Column("content", TypeName = "text")]
    public required string Content { get; set; }

    [Required]
    [Column("signature", TypeName = "text")]
    public required string Signature { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("verification_status", TypeName = "varchar(20)")]
    public string VerificationStatus { get; set; } = "Unsigned";

    [Column("visualization_url", TypeName = "text")]
    public string? VisualizationUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
