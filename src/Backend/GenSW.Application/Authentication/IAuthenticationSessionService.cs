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

    Task<AuthenticationSessionResult?> RefreshAsync(
        string refreshToken, Guid? expectedSessionId, CancellationToken cancellationToken) =>
        RefreshAsync(refreshToken, cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    Task<bool> LogoutAsync(
        string? refreshToken, Guid? expectedSessionId, Guid? bearerSubjectId, Guid? bearerSessionId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    Task<CurrentUserResult?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<CurrentUserResult?> GetSessionUserAsync(Guid subjectId, CancellationToken cancellationToken) =>
        GetCurrentUserAsync(subjectId, cancellationToken);
}

public sealed class AuthenticationSessionMismatchException : InvalidOperationException;

public interface IPlatformAuthenticationSessionService
{
    Task<AuthenticationSessionResult?> LoginAsync(string userName, string password, CancellationToken cancellationToken);

    Task<AuthenticationSessionResult?> RefreshAsync(
        string refreshToken, Guid? expectedSessionId, CancellationToken cancellationToken);

    Task<bool> LogoutAsync(
        string? refreshToken, Guid? expectedSessionId, Guid? bearerSubjectId, Guid? bearerSessionId,
        CancellationToken cancellationToken);

    Task<PlatformUserResult?> GetSessionUserAsync(
        Guid subjectId, Guid sessionId, CancellationToken cancellationToken);
}

public sealed record PlatformUserResult(Guid SubjectId, string UserName, string Nome, IReadOnlyCollection<string> Roles);
