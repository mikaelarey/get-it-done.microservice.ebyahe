using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace EByahe.Application.Extensions;

public static class KeycloakAuthenticationExtensions
{
  public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services,
    IConfiguration configuration, IHostEnvironment environment)
  {
    var authority = configuration["Keycloak:Authority"];
    var audience = configuration["Keycloak:Audience"];
    if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) || string.IsNullOrWhiteSpace(audience)
      || issuer.UserInfo.Length > 0 || issuer.Query.Length > 0 || issuer.Fragment.Length > 0)
      throw new InvalidOperationException("Configure Keycloak:Authority and Keycloak:Audience.");
    var localDevelopment = environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.IsLoopback;
    if (issuer.Scheme != Uri.UriSchemeHttps && !localDevelopment)
      throw new InvalidOperationException("Keycloak:Authority must use HTTPS, except for loopback HTTP in Development.");

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
      options.Authority = authority.TrimEnd('/');
      options.Audience = audience;
      options.RequireHttpsMetadata = !localDevelopment;
      options.MapInboundClaims = false;
      options.IncludeErrorDetails = false;
      options.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true, ValidIssuer = options.Authority, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(30)
      };
      options.Events = new JwtBearerEvents
      {
        OnTokenValidated = context =>
        {
          var subject = context.Principal?.FindFirst("sub")?.Value;
          if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255 || subject != subject.Trim()
            || !string.Equals(context.Principal?.FindFirst("typ")?.Value, "Bearer", StringComparison.OrdinalIgnoreCase))
            context.Fail("A Keycloak access token with a valid subject is required.");
          return Task.CompletedTask;
        }
      };
    });
    services.AddAuthorization();
    return services;
  }
}
