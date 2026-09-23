using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NPMS.WebAPI.Authentication;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string HeaderName = "X-API-Key";
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration configuration)
        : base(options, logger, encoder, clock)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var providedValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API key."));
        }

        var expected = _configuration["Authentication:ApiKey"] ?? Environment.GetEnvironmentVariable("NPMS_API_KEY") ?? string.Empty;
        var provided = providedValues.ToString();

        var isValid = !string.IsNullOrWhiteSpace(expected) &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(expected));
        if (!isValid)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "npms-server")
        }, Scheme.Name);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
