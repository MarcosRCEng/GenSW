using System.Text;
using Microsoft.Extensions.Options;

namespace GenSW.Infrastructure.Authentication;

internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const int MinimumSigningKeyBytes = 32;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add($"{JwtOptions.SectionName}:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"{JwtOptions.SectionName}:Audience is required.");
        }

        if (options.AccessTokenMinutes <= 0)
        {
            failures.Add($"{JwtOptions.SectionName}:AccessTokenMinutes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add(
                $"{JwtOptions.SectionName}:SigningKey is required and must be provided by an external secret source.");
        }
        else if (Encoding.UTF8.GetByteCount(options.SigningKey) < MinimumSigningKeyBytes)
        {
            failures.Add(
                $"{JwtOptions.SectionName}:SigningKey must contain at least {MinimumSigningKeyBytes} UTF-8 bytes.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
