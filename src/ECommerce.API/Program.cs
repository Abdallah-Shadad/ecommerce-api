using ECommerce.API.Extensions;
using ECommerce.API.Middleware;
using ECommerce.Application;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Layer Services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

builder.Services.AddControllers();

var app = builder.Build();

// 2. Exception Handling Middleware (First in Pipeline)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 3. Environment-specific Configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECommerce API v1");
    });
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// 4. Security & Routing Middleware
app.UseCors(AppConstants.Cors.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 5. Seed Initial Data (Roles, Admin, Demo Customers & 1:1 Carts)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        await IdentitySeeder.SeedAsync(services);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding initial database state.");
    }
}

app.Run();

// For Integration Test WebApplicationFactory support
public partial class Program { }