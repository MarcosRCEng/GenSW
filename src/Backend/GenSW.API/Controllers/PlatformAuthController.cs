using GenSW.API.Authentication;
using GenSW.API.Contracts;
using GenSW.Application.Authentication;
using GenSW.Infrastructure.Authentication;
using GenSW.Infrastructure.Catalog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GenSW.API.Controllers;

[ApiController]
[Route("api/v1/platform/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformAuthController(
    IPlatformAuthenticationSessionService sessions, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(ApiPolicyNames.LoginRateLimit)]
    [HttpPost("login")]
    public async Task<ActionResult<AccessTokenResult>> Login(LoginRequest request, CancellationToken ct)
    {
        if (!SessionRequest.IsTrustedOrigin(Request, configuration))
            return StatusCode(StatusCodes.Status403Forbidden);

        var result = await sessions.LoginAsync(request.UserName, request.Password, ct);
        if (result is null)
            return Unauthorized();

        RefreshTokenCookie.Append(Response, result.RefreshToken, result.RefreshTokenExpiresAtUtc, platform: true);
        return Ok(result.AccessToken);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokenResult>> Refresh(CancellationToken ct)
    {
        if (!SessionRequest.IsTrustedOrigin(Request, configuration))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (!SessionRequest.TryGetExpectedSession(Request, out var expectedSession))
            return BadRequest();

        var cookieName = RefreshTokenCookie.CookieName(platform: true);
        Request.Cookies.TryGetValue(cookieName, out var refreshToken);
        try
        {
            var result = refreshToken is null ? null : await sessions.RefreshAsync(refreshToken, expectedSession, ct);
            if (result is null)
            {
                RefreshTokenCookie.Delete(Response, platform: true);
                return Unauthorized();
            }

            RefreshTokenCookie.Append(Response, result.RefreshToken, result.RefreshTokenExpiresAtUtc, platform: true);
            return Ok(result.AccessToken);
        }
        catch (AuthenticationSessionMismatchException)
        {
            return Unauthorized();
        }
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!SessionRequest.IsTrustedOrigin(Request, configuration))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (!SessionRequest.TryGetExpectedSession(Request, out var expectedSession))
            return BadRequest();

        var cookieName = RefreshTokenCookie.CookieName(platform: true);
        Request.Cookies.TryGetValue(cookieName, out var refreshToken);
        var auth = await HttpContext.AuthenticateAsync(PlatformAuthentication.Scheme);
        Guid? subjectId = null, sessionId = null;
        if (auth is { Succeeded: true, Principal: { } principal } &&
            CatalogSessionIdentity.TryReadSubjectAndSession(principal, out var subject, out var session))
        {
            subjectId = subject;
            sessionId = session;
        }

        try
        {
            if (await sessions.LogoutAsync(refreshToken, expectedSession, subjectId, sessionId, ct))
                RefreshTokenCookie.Delete(Response, platform: true);
            return NoContent();
        }
        catch (AuthenticationSessionMismatchException)
        {
            return Unauthorized();
        }
    }

    [Authorize(Policy = PlatformAuthentication.UserPolicy)]
    [HttpGet("me")]
    public async Task<ActionResult<PlatformUserResult>> Me(CancellationToken ct)
    {
        if (!CatalogSessionIdentity.TryReadSubjectAndSession(User, out var subjectId, out var sessionId))
            return Unauthorized();

        var user = await sessions.GetSessionUserAsync(subjectId, sessionId, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}
