using ServerMonitor.Data;
using ServerMonitor.Services;
using ServerMonitor.BackgroundTasks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database - Use PostgreSQL for your monitoring app
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("ServerMonitor"));
// Custom services
builder.Services.AddScoped<SystemMonitorService>();
builder.Services.AddScoped<PostgresMonitorService>();
builder.Services.AddHostedService<MonitoringBackgroundService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("✅ InMemory database initialized");
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


app.MapGet("/health", () => Results.Ok(new { 
    status = "Healthy", 
    timestamp = DateTime.UtcNow,
    database = "InMemory"
}));

Console.WriteLine("🚀 Server Monitor Application Started Successfully!");

app.Run();