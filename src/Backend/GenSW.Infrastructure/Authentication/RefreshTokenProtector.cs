using System.Security.Cryptography;
using System.Text;

namespace GenSW.Infrastructure.Authentication;

public sealed class RefreshTokenProtector
{
    public const int TokenSizeInBytes = 32;

    private const int EncodedTokenLength = 43;

    public string Generate()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);

        try
        {
            return EncodeBase64Url(randomBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(randomBytes);
        }
    }

    public byte[] ComputeHash(string token)
    {
        if (!TryComputeHash(token, out var tokenHash))
        {
            throw new ArgumentException("Refresh token format is invalid.", nameof(token));
        }

        return tokenHash;
    }

    public bool TryComputeHash(string? token, out byte[] tokenHash)
    {
        tokenHash = [];

        if (!TryDecodeCanonicalToken(token, out var tokenBytes))
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(tokenBytes);
        Span<byte> encodedToken = stackalloc byte[EncodedTokenLength];
        Encoding.ASCII.GetBytes(token, encodedToken);

        try
        {
            tokenHash = SHA256.HashData(encodedToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encodedToken);
        }

        return true;
    }

    private static bool TryDecodeCanonicalToken(string? token, out byte[] tokenBytes)
    {
        tokenBytes = [];

        if (token is null || token.Length != EncodedTokenLength)
        {
            return false;
        }

        foreach (var character in token)
        {
            if (!(character is >= 'A' and <= 'Z') &&
                !(character is >= 'a' and <= 'z') &&
                !(character is >= '0' and <= '9') &&
                character is not '-' and not '_')
            {
                return false;
            }
        }

        try
        {
            var paddedToken = string.Concat(token.Replace('-', '+').Replace('_', '/'), "=");
            tokenBytes = Convert.FromBase64String(paddedToken);

            if (tokenBytes.Length != TokenSizeInBytes ||
                !string.Equals(EncodeBase64Url(tokenBytes), token, StringComparison.Ordinal))
            {
                CryptographicOperations.ZeroMemory(tokenBytes);
                tokenBytes = [];
                return false;
            }

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncodeBase64Url(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
