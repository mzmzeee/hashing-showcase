using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HashingDemo.Data.Entities;

[Table("users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    public required string Username { get; set; }

    [Required]
    public required string PasswordHash { get; set; }

    [Required]
    public required string Salt { get; set; }

    [Required]
    public int Iterations { get; set; }

    [Column("public_key", TypeName = "text")]
    public string? PublicKey { get; set; }

    [Column("private_key", TypeName = "text")]
    public string? PrivateKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
