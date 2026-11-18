using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using HISWEBAPI.Services;
using HISWEBAPI.Services.Interfaces;

namespace HISWEBAPI.Configuration
{
    public static class JwtConfigurationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind JWT settings
            var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>();
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

            // Register JWT service
            services.AddScoped<IJwtService, JwtService>();

            // Add Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = false; // Set to true in production
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings?.Issuer,
                    ValidAudience = jwtSettings?.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? "")),
                    ClockSkew = TimeSpan.Zero // Remove delay of token expiration
                };

                options.Events = new JwtBearerEvents
                {
                    // refuse a token that was cancelled by logout (see TokenRevocationService)
                    OnTokenValidated = context =>
                    {
                        var revocation = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
                        string? jti = context.SecurityToken?.Id;
                        if (string.IsNullOrEmpty(jti))
                            jti = context.Principal?.FindFirst("jti")?.Value;
                        long.TryParse(context.Principal?.FindFirst("sessionId")?.Value, out long sessionId);

                        if (revocation.IsRevoked(jti, sessionId))
                        {
                            context.Response.Headers["Token-Revoked"] = "true";
                            context.Fail("This access token was cancelled by logout.");
                        }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers.Add("Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }
    }
}