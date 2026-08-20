using GenSW.API.Authentication;
using GenSW.API.Contracts;
using GenSW.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GenSW.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthenticationSessionService authenticationSessions) : ControllerBase
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
        if (!Request.Cookies.TryGetValue(RefreshTokenCookie.Name, out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            RefreshTokenCookie.Delete(Response);
            return Unauthorized();
        }

        var result = await authenticationSessions.RefreshAsync(refreshToken, cancellationToken);

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
        Request.Cookies.TryGetValue(RefreshTokenCookie.Name, out var refreshToken);

        await authenticationSessions.LogoutAsync(refreshToken, cancellationToken);
        RefreshTokenCookie.Delete(Response);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResult>> Me(CancellationToken cancellationToken)
    {
        var subject = User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var result = await authenticationSessions.GetCurrentUserAsync(userId, cancellationToken);

        return result is null ? Unauthorized() : Ok(result);
    }
}
