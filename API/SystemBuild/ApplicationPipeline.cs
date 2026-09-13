using API.Middlewares;

namespace API.SystemBuild
{
    public static class ApplicationPipeline
    {
        public static IApplicationBuilder UseApplicationPipeline(this IApplicationBuilder app)
        {
            // 1. Exception Handler (الأول دائماً)
            app.UseMiddleware<ExceptionMiddleware>();

            // 2. CORS
            app.UseCors("AllowAngular");

            // 3. Swagger
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Project Management API");
                c.DocumentTitle = "Project Management API";
            });

            // 4. HTTPS + Auth
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            return app;
        }
    }
}