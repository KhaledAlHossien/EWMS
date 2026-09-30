using API.Authorization;
using API.Hubs;
using Application;
using Application.Common;
using Application.Interfaces;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace API.SystemBuild
{
    public static class AddAPIRigstrationServices
    {
        public static IServiceCollection AddAPIServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddControllers();

            // الإشعارات اللحظية (SignalR)
            services.AddSignalR();
            services.AddScoped<INotificationPusher, SignalRNotificationPusher>();
            services.AddEndpointsApiExplorer();

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

            services.AddApplicationServices();
            services.AddInfrastructureServices(configuration);
            services.AddHttpContextAccessor();

            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, DeviceAccessAuthorizationHandler>();
            services.AddAuthorization(options =>
            {
                // سياسة بنفس الاسم لكل صلاحية في AppPermissions (المصدر الوحيد للصلاحيات)
                foreach (var permission in AppPermissions.All.Select(p => p.Name))
                {
                    if (AppPermissions.DeviceInventory.Contains(permission))
                        continue;

                    options.AddPolicy(permission, policy =>
                        policy.Requirements.Add(new PermissionRequirement(permission)));
                }

                // توثيق الأجهزة: صلاحية الدور أو الانتماء لقسم العمليات (DeviceInventory:OwnerDepartmentId)
                options.AddPolicy("ViewDevices", policy =>
                    policy.Requirements.Add(new DeviceAccessRequirement(DeviceOperation.View)));
                options.AddPolicy("CreateDevice", policy =>
                    policy.Requirements.Add(new DeviceAccessRequirement(DeviceOperation.Create)));
                options.AddPolicy("EditDevice", policy =>
                    policy.Requirements.Add(new DeviceAccessRequirement(DeviceOperation.Edit)));
                options.AddPolicy("DeleteDevice", policy =>
                    policy.Requirements.Add(new DeviceAccessRequirement(DeviceOperation.Delete)));
            });

            return services;
        }
    }
}
