namespace GenSW.Application.Authentication;

public interface IAccessTokenService
{
    AccessTokenResult Create(Guid userId, IReadOnlyCollection<string> roles);
}
