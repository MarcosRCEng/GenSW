namespace GenSW.Application.Authentication;

public interface IAccessTokenService
{
    AccessTokenResult Create(Guid subjectId, Guid sessionId, DateTimeOffset sessionExpiry, ErpAccessClaims? erp);
}

public sealed record ErpAccessClaims(Guid AccountId, Guid BindingId, long BindingRevision, long AccessGeneration);
