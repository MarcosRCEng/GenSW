using GenSW.Application.Accounts;
using GenSW.Application.EggBridge;
using Microsoft.AspNetCore.DataProtection;
using System.Globalization;
using System.Security.Cryptography;

namespace GenSW.Infrastructure.EggBridge;

public sealed class EggBridgePreviewProtector(IDataProtectionProvider provider, IErpAccountScope account) : IEggBridgePreviewProtector
{
    private IDataProtector Protector()
    {
        var key = account.Current.Key;
        return provider.CreateProtector("gensw-f03-preview/2",
            key.AccountId.ToString("D"), key.BindingId.ToString("D"),
            key.BindingRevision.ToString(CultureInfo.InvariantCulture),
            key.AccessGeneration.ToString(CultureInfo.InvariantCulture), key.ActorId.ToString("D"));
    }

    public string Protect(string value) => Protector().Protect(value);

    public string Unprotect(string token)
    {
        try { return Protector().Unprotect(token); }
        catch (CryptographicException) { throw new ArgumentException("Prévia F03 inválida. Prepare uma nova prévia."); }
    }
}
