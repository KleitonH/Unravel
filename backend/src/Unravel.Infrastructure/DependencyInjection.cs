using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unravel.Application.Ports;
using Unravel.Application.Services;
using Unravel.Application.UseCases;
using Unravel.Domain.Ports;
using Unravel.Infrastructure.Persistence;
using Unravel.Infrastructure.Repositories;
using Unravel.Infrastructure.Services;

namespace Unravel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddScoped<CreateUserUseCase>();
        services.AddScoped<AuthenticateUserUseCase>();
        services.AddScoped<GetUserUseCase>();

        services.AddScoped<ITrailService,     TrailService>();
        services.AddScoped<IContentService,  ContentService>();
        services.AddScoped<IChallengeService, ChallengeService>();

        return services;
    }
}
