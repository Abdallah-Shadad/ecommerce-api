using ECommerce.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register Infrastructure Services (AppDbContext & SQL Server)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add API & OpenAPI Services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed Identity Roles and Users
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await ECommerce.Infrastructure.Persistence.IdentitySeeder.SeedAsync(services);
}

app.Run();