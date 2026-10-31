using GenSW.Application.EggBridge;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace GenSW.Infrastructure.EggBridge;

public sealed class EggBridgePreviewProtector(IDataProtectionProvider provider) : IEggBridgePreviewProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("gensw-f03-preview/1");
    public string Protect(string value) => protector.Protect(value);
    public string Unprotect(string token)
    {
        try { return protector.Unprotect(token); }
        catch (CryptographicException) { throw new ArgumentException("Prévia F03 inválida. Prepare uma nova prévia."); }
    }
}
