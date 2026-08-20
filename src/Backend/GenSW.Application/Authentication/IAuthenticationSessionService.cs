namespace GenSW.Application.Authentication;

public interface IAuthenticationSessionService
{
    Task<AuthenticationSessionResult?> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken);

    Task<AuthenticationSessionResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    Task<CurrentUserResult?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
