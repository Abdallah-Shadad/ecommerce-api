using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.Interfaces.Services;
using ECommerce.Domain.Exceptions;
using ECommerce.Shared.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshTokenValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshTokenValidator = refreshTokenValidator;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(AppConstants.RateLimiting.StrictAuthPolicy)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var validationResult = await _registerValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var response = await _authService.RegisterAsync(request);
        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiresOn);
        return Ok(response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AppConstants.RateLimiting.StrictAuthPolicy)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var validationResult = await _loginValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var response = await _authService.LoginAsync(request);
        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiresOn);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(AppConstants.RateLimiting.AuthPolicy)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? request)
    {
        var token = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = Request.Cookies[RefreshTokenCookieName];
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthorizedException("Refresh token was not provided in request body or cookie.");
        }

        var refreshRequest = new RefreshTokenRequest(token);
        var validationResult = await _refreshTokenValidator.ValidateAsync(refreshRequest);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var response = await _authService.RefreshTokenAsync(refreshRequest);
        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiresOn);
        return Ok(response);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshTokenRequest? request)
    {
        var token = request?.RefreshToken ?? Request.Cookies[RefreshTokenCookieName];

        if (!string.IsNullOrWhiteSpace(token))
        {
            await _authService.RevokeTokenAsync(new RevokeTokenRequest(token));
        }

        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });

        return NoContent();
    }

    private void SetRefreshTokenCookie(string token, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = expires,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true
        };

        Response.Cookies.Append(RefreshTokenCookieName, token, cookieOptions);
    }
}