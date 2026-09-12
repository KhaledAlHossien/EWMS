using Application;
using Infrastructure;
using Microsoft.OpenApi;

namespace API.SystemBuild
{
    public static class AddAPIRigstrationServices
    {
        public static IServiceCollection AddAPIServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // ==================== Controllers + OpenAPI ====================
            services.AddControllers();
            services.AddEndpointsApiExplorer();

            // ==================== Swagger ====================
           

                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "EWMS",
                        Version = "v1",
                        Description = "featured API",
                        Contact = new OpenApiContact
                        {
                            Name = "Khaled Alhossien",
                            Email = "hossink131@gmail.com"
                        }
                    });



                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Enter: {your token} without {Bearer}",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            });

            // ==================== Application + Infrastructure ====================
            services.AddApplicationServices();
            services.AddInfrastructureServices(configuration);
            services.AddHttpContextAccessor();

            // ==================== Auth (Placeholder) ====================
            // سيتم تفعيل JwtBearer في الخطوة التالية
            //services.AddAuthentication();
            services.AddAuthorization();

            return services;
        }
    }
}