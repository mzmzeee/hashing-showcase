using System.ComponentModel.DataAnnotations;

namespace HashingDemo.Api.Models;

public class RegisterRequest
{
    [Required]
    [MinLength(3)]
    public required string Username { get; set; }

    [Required]
    [MinLength(8)]
    public required string Password { get; set; }
}

public class LoginRequest
{
    [Required]
    public required string Username { get; set; }

    [Required]
    public required string Password { get; set; }
}
