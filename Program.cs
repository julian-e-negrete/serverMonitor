
using ServerMonitor.Data;
using ServerMonitor.Services;
using ServerMonitor.BackgroundTasks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add detailed logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database - Use InMemory for app data
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("ServerMonitor"));

// Custom services
builder.Services.AddScoped<SystemMonitorService>();
builder.Services.AddScoped<PostgresMonitorService>();
builder.Services.AddHostedService<MonitoringBackgroundService>();

var app = builder.Build();

// Log all registered endpoints
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        Console.WriteLine($"API Request: {context.Request.Method} {context.Request.Path}");
    }
    await next();
});

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("✅ InMemory database initialized");
}

// Test service registration
using (var scope = app.Services.CreateScope())
{
    try
    {
        var postgresService = scope.ServiceProvider.GetRequiredService<PostgresMonitorService>();
        Console.WriteLine("✅ PostgresMonitorService registered successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ PostgresMonitorService registration failed: {ex.Message}");
    }
}

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
    status = "Healthy", 
    timestamp = DateTime.UtcNow 
}));

// Debug endpoint to test PostgreSQL service directly
app.MapGet("/debug/postgres", async (PostgresMonitorService postgresService) =>
{
    try
    {
        var stats = await postgresService.GetPostgresStatsAsync();
        var health = await postgresService.IsDatabaseHealthyAsync();
        return Results.Ok(new { 
            stats = stats,
            health = health,
            message = "PostgreSQL service is working"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"PostgreSQL service error: {ex.Message}");
    }
});

Console.WriteLine("🚀 Server Monitor Application Starting...");
Console.WriteLine("Available endpoints:");
Console.WriteLine("  GET /api/monitoring/current");
Console.WriteLine("  GET /api/monitoring/processes"); 
Console.WriteLine("  GET /api/monitoring/services");
Console.WriteLine("  GET /api/monitoring/postgres/stats");
Console.WriteLine("  GET /api/monitoring/postgres/health");
Console.WriteLine("  GET /debug/postgres");

app.Run();
