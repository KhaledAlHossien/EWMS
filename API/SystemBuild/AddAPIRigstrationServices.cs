using API.Authorization;
using Application;
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
            services.AddAuthorization(options =>
            {
                options.AddPolicy("ViewProjects", policy =>
                    policy.Requirements.Add(new PermissionRequirement("ViewProjects")));
                options.AddPolicy("CreateProject", policy =>
                    policy.Requirements.Add(new PermissionRequirement("CreateProject")));
                options.AddPolicy("EditProject", policy =>
                    policy.Requirements.Add(new PermissionRequirement("EditProject")));
                options.AddPolicy("DeleteProject", policy =>
                    policy.Requirements.Add(new PermissionRequirement("DeleteProject")));
                options.AddPolicy("AssignUser", policy =>
                    policy.Requirements.Add(new PermissionRequirement("AssignUser")));
                options.AddPolicy("TransferProject", policy =>
                    policy.Requirements.Add(new PermissionRequirement("TransferProject")));
                options.AddPolicy("UploadProjectFile", policy =>
                    policy.Requirements.Add(new PermissionRequirement("UploadProjectFile")));
                options.AddPolicy("ManageUsers", policy =>
                    policy.Requirements.Add(new PermissionRequirement("ManageUsers")));
                options.AddPolicy("ManageBranches", policy =>
                    policy.Requirements.Add(new PermissionRequirement("ManageBranches")));
                options.AddPolicy("ManageDepartments", policy =>
                    policy.Requirements.Add(new PermissionRequirement("ManageDepartments")));
                options.AddPolicy("ManageRoles", policy =>
                    policy.Requirements.Add(new PermissionRequirement("ManageRoles")));
            });

            return services;
        }
    }
}
