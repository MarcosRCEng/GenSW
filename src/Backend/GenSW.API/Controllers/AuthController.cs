using GenSW.API.Authentication;
using GenSW.API.Contracts;
using GenSW.Application.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GenSW.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(
    IAuthenticationSessionService authenticationSessions, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(ApiPolicyNames.LoginRateLimit)]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AccessTokenResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AccessTokenResult>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!SessionRequest.IsTrustedOrigin(Request, configuration))
            return StatusCode(StatusCodes.Status403Forbidden);

        var result = await authenticationSessions.LoginAsync(
            request.UserName,
            request.Password,
            cancellationToken);

        if (result is null)
        {
            return Unauthorized();
        }

        RefreshTokenCookie.Append(Response, result.RefreshToken, result.RefreshTokenExpiresAtUtc);

        return Ok(result.AccessToken);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AccessTokenResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccessTokenResult>> Refresh(CancellationToken cancellationToken)
    {
        if (!SessionRequest.IsTrustedOrigin(Request, configuration))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (!SessionRequest.TryGetExpectedSession(Request, out var expectedSession))
            return BadRequest();

        if (!Request.Cookies.TryGetValue(RefreshTokenCookie.Name, out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            RefreshTokenCookie.Delete(Response);
            return Unauthorized();
        }

        AuthenticationSessionResult? result;
        try
        {
            result = await authenticationSessions.RefreshAsync(refreshToken, expectedSession, cancellationToken);
        }
        catch (AuthenticationSessionMismatchException)
        {
            return Unauthorized();
        }

        if (result is null)
        {
            RefreshTokenCookie.Delete(Response);
            return Unauthorized();
        }

        RefreshTokenCookie.Append(Response, result.RefreshToken, result.RefreshTokenExpiresAtUtc);

        return Ok(result.AccessToken);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!SessionRequest.IsTrustedOrigin(Request, configuration))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (!SessionRequest.TryGetExpectedSession(Request, out var expectedSession))
            return BadRequest();

        Request.Cookies.TryGetValue(RefreshTokenCookie.Name, out var refreshToken);

        var hasBearer = CatalogSessionIdentity.TryRead(User, out var bearer) && bearer is not null;
        try
        {
            var deleteCookie = await authenticationSessions.LogoutAsync(refreshToken, expectedSession,
                hasBearer ? bearer!.SubjectId : null, hasBearer ? bearer!.SessionId : null, cancellationToken);
            if (deleteCookie)
                RefreshTokenCookie.Delete(Response);
        }
        catch (AuthenticationSessionMismatchException)
        {
            return Unauthorized();
        }

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResult>> Me(CancellationToken cancellationToken)
    {
        if (!CatalogSessionIdentity.TryRead(User, out var session) || session is null)
        {
            return Unauthorized();
        }

        var result = await authenticationSessions.GetSessionUserAsync(session.SubjectId, cancellationToken);

        return result is null ? Unauthorized() : Ok(result);
    }
}
