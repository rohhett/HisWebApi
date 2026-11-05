using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Repositories.Implementations;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services.Interfaces;
using HISWEBAPI.Services.Implementations;
using HISWEBAPI.Services;
using HISWEBAPI.Utilities;

namespace HISWEBAPI.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // CORS Configuration
        RegisterCors(services, configuration);

        // Data Access Layer
        RegisterDataAccess(services);

        // Repositories
        RegisterRepositories(services);

        // Business Services
        RegisterBusinessServices(services);

        // Utilities
        RegisterUtilities(services);

        // Caching
        RegisterCaching(services, configuration);

        return services;
    }

    private static void RegisterCors(IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("_myAllowSpecificOrigins", policy =>
            {
                policy.WithOrigins(configuration.GetSection("AllowedOrigins").Get<string[]>()
                                   ?? new[] { "http://localhost:3000" })
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });
    }

    private static void RegisterDataAccess(IServiceCollection services)
    {
        services.AddScoped<ICustomSqlHelper, CustomSqlHelper>();
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IHomeRepository, HomeRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPageConfigRepository, PageConfigRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<ILabRepository, LabRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IPatientLabReport, PatientLabReport>();
        services.AddScoped<IEMRRepository, EMRRepository>();
        services.AddScoped<IIPDRepository, IPDRepository>();
        services.AddScoped<ILabReportRepository, LabReportRepository>();


    }

    private static void RegisterBusinessServices(IServiceCollection services)
    {
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IResponseMessageService, ResponseMessageService>();
        services.AddScoped<IPatientInvestigationReportPdfService, PatientInvestigationReportPdfService>();
    }

    private static void RegisterUtilities(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<FileUploadHelper>();
    }

    private static void RegisterCaching(IServiceCollection services, IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetValue<string>("Redis:Configuration") ?? "localhost:6379";
        });

        // Also add memory cache as fallback
        services.AddMemoryCache();
    }
}
