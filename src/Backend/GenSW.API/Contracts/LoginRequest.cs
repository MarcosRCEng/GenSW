using System.ComponentModel.DataAnnotations;

namespace GenSW.API.Contracts;

public sealed class LoginRequest
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [StringLength(1024, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}
