using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Infrastructure
{
    public static class AddInfrastructureRigstrationServices
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // ==================== DbContext ====================
            services.AddDbContext<DataContext>(opt =>
                opt.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // ==================== Services / Repositories ====================
            services.AddScoped<IBranchService, BranchService>();
            services.AddScoped<IDepartmentService, DepartmentService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IRolePermissionService, RolePermissionService>();
            services.AddScoped<IProjectService, ProjectService>();
            services.AddScoped<IProjectAssignmentService, ProjectAssignmentService>();
            services.AddScoped<IProjectTransferService, ProjectTransferService>();
            services.AddScoped<IProjectFileService, ProjectFileService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();

            // ==================== JWT Services ====================
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAuthService, AuthService>();

            // ==================== JWT Authentication ====================
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidIssuer = configuration["JwtSettings:Issuer"],
                    ValidAudience = configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["JwtSettings:Key"]!))
                };

                options.Events = new JwtBearerEvents
                {
                    // ✅ التحقق من أن التوكن موجود في قاعدة البيانات ولم يُبطَل
                    OnTokenValidated = async context =>
                    {
                        try
                        {
                            var db = context.HttpContext.RequestServices.GetRequiredService<DataContext>();

                            var tokenString = context.HttpContext.Request.Headers["Authorization"]
                                .ToString()
                                .Replace("Bearer ", "");

                            if (string.IsNullOrEmpty(tokenString))
                            {
                                context.Fail("Missing token");
                                return;
                            }

                            var userToken = await db.UserTokens
                                .FirstOrDefaultAsync(t => t.Token == tokenString);

                            if (userToken == null || userToken.IsRevoked || userToken.ExpiresAt < DateTime.UtcNow)
                            {
                                context.Fail("Token revoked or expired");
                            }
                        }
                        catch (Exception ex)
                        {
                            context.Fail($"Token validation error: {ex.Message}");
                        }
                    },

                    // ⚠️ لا نكتب أي شيء هنا — نترك OnChallenge يتولى الرد
                    OnAuthenticationFailed = context =>
                    {
                        // فقط نسجّل الحدث
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");
                        logger.LogWarning("Authentication failed: {Message}", context.Exception?.Message);
                        return Task.CompletedTask;
                    },

                    // ✅ هنا نكتب الرد الوحيد
                    OnChallenge = context =>
                    {
                        context.HandleResponse(); // يمنع ASP.NET من الكتابة الافتراضية

                        if (context.Response.HasStarted)
                            return Task.CompletedTask;

                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";

                        return context.Response.WriteAsync(
                            System.Text.Json.JsonSerializer.Serialize(new
                            {
                                statusCode = 401,
                                message = "Authorization required - please login"
                            }));
                    },

                    // ✅ 403
                    OnForbidden = context =>
                    {
                        if (context.Response.HasStarted)
                            return Task.CompletedTask;

                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";

                        return context.Response.WriteAsync(
                            System.Text.Json.JsonSerializer.Serialize(new
                            {
                                statusCode = 403,
                                message = "You don't have permission to access this resource"
                            }));
                    }
                };
            });

            return services;
        }
    }
}
