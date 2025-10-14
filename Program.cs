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

// Database Configuration
var serverMonitorConnection = builder.Configuration.GetConnectionString("DefaultConnection");
var marketDataConnection = builder.Configuration.GetConnectionString("MarketDataConnection");

if (string.IsNullOrEmpty(serverMonitorConnection))
{
    Console.WriteLine("⚠️  No ServerMonitor connection string found. Using InMemory database as fallback.");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("ServerMonitor"));
}
else
{
    Console.WriteLine($"✅ Using ServerMonitor PostgreSQL: {GetDatabaseNameFromConnectionString(serverMonitorConnection)}");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(serverMonitorConnection));
}

// Register DbContextFactory for both contexts
builder.Services.AddDbContextFactory<ApplicationDbContext>();

// Custom services
builder.Services.AddScoped<SystemMonitorService>();
builder.Services.AddScoped<PostgresMonitorService>();
builder.Services.AddScoped<IFinancialDataService, FinancialDataService>();
builder.Services.AddScoped<IMarketApiService, MarketApiService>();

// Background services
builder.Services.AddHostedService<MonitoringBackgroundService>();
builder.Services.AddHostedService<FinancialDataBackgroundService>();

// Register HttpClient for Market API
builder.Services.AddHttpClient<IMarketApiService, MarketApiService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:140.0) Gecko/20100101 Firefox/140.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Helper function to extract database name from connection string
string GetDatabaseNameFromConnectionString(string connectionString)
{
    try
    {
        var keyValuePairs = connectionString.Split(';')
            .Select(part => part.Split('='))
            .Where(part => part.Length == 2)
            .ToDictionary(part => part[0].Trim().ToLower(), part => part[1].Trim());
        
        return keyValuePairs.ContainsKey("database") ? keyValuePairs["database"] : "unknown";
    }
    catch
    {
        return "unknown";
    }
}

// Log all registered endpoints
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        Console.WriteLine($"API Request: {context.Request.Method} {context.Request.Path}");
    }
    await next();
});

// Initialize databases
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    try
    {
        // Check if we can connect to the database
        var canConnect = await db.Database.CanConnectAsync();
        if (canConnect)
        {
            Console.WriteLine("✅ ServerMonitor database connection successful");
            
            // For development: ensure database is created and migrations are applied
            await db.Database.EnsureCreatedAsync();
            Console.WriteLine("✅ ServerMonitor database initialized");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ServerMonitor database connection failed: {ex.Message}");
        Console.WriteLine("🔄 Falling back to InMemory database...");
        
        // Fallback to InMemory
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseInMemoryDatabase("ServerMonitorFallback");
        
        // Recreate the context with InMemory
        using var fallbackContext = new ApplicationDbContext(optionsBuilder.Options);
        await fallbackContext.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ InMemory fallback database initialized");
    }
}

// Test service registration
using (var scope = app.Services.CreateScope())
{
    try
    {
        var postgresService = scope.ServiceProvider.GetRequiredService<PostgresMonitorService>();
        Console.WriteLine("✅ PostgresMonitorService registered successfully");
        
        var financialDataService = scope.ServiceProvider.GetRequiredService<IFinancialDataService>();
        Console.WriteLine("✅ FinancialDataService registered successfully");
        
        var marketApiService = scope.ServiceProvider.GetRequiredService<IMarketApiService>();
        Console.WriteLine("✅ MarketApiService registered successfully");
        
        // Test PostgreSQL connection through the service
        var postgresHealth = await postgresService.IsDatabaseHealthyAsync();
        Console.WriteLine($"✅ PostgreSQL Health Check: {(postgresHealth ? "Healthy" : "Unhealthy")}");
        
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Service registration failed: {ex.Message}");
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

// Database connection info endpoint
app.MapGet("/debug/databases", () =>
{
    var databases = new
    {
        serverMonitor = new
        {
            database = GetDatabaseNameFromConnectionString(serverMonitorConnection ?? ""),
            connected = !string.IsNullOrEmpty(serverMonitorConnection)
        },
        marketData = new
        {
            database = GetDatabaseNameFromConnectionString(marketDataConnection ?? ""),
            connected = !string.IsNullOrEmpty(marketDataConnection)
        }
    };
    
    return Results.Ok(databases);
});

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

// Financial data debug endpoints
app.MapGet("/debug/financial", async (IFinancialDataService financialService) =>
{
    try
    {
        var cauctionData = await financialService.GetCauctionDataAsync();
        var marketData = await financialService.GetLatestMarketDataAsync();
        var mepRate = await financialService.GetLatestMepCalculationAsync();
        var isHealthy = await financialService.IsFinancialDataAvailableAsync();

        // Ensure we don't return mixed anonymous/object types directly
        object mepPayload;
        if (mepRate is null)
        {
            mepPayload = new { message = "No MEP data available" };
        }
        else
        {
            mepPayload = mepRate;
            
        }

        return Results.Ok(new { 
            cauctionData = cauctionData,
            marketData = marketData,
            mepRate = mepPayload,
            financialDataAvailable = isHealthy,
            message = "Financial data services are working"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Financial data service error: {ex.Message}");
    }
});

app.MapGet("/debug/market-api", async (IMarketApiService marketApiService) =>
{
    try
    {
        var connectionTest = await marketApiService.TestConnectionAsync();
        var cauctionData = await marketApiService.FetchCauctionDataAsync();
        
        return Results.Ok(new { 
            apiConnection = connectionTest ? "Healthy" : "Unhealthy",
            cauctionDataCount = cauctionData.Count,
            cauctionData = cauctionData,
            message = "Market API service test completed"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Market API service error: {ex.Message}");
    }
});

// Financial data API endpoints
app.MapGet("/api/financial/cauction", async (IFinancialDataService financialService) =>
{
    try
    {
        var data = await financialService.GetCauctionDataAsync();
        return Results.Ok(data);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching cauction data: {ex.Message}");
    }
});

app.MapGet("/api/financial/market-data", async (IFinancialDataService financialService) =>
{
    try
    {
        var data = await financialService.GetLatestMarketDataAsync();
        return Results.Ok(data);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching market data: {ex.Message}");
    }
});

app.MapGet("/api/financial/mep-rate", async (IFinancialDataService financialService) =>
{
    try
    {
        var mep = await financialService.GetLatestMepCalculationAsync();
        if (mep is null)
            return Results.Ok(new { message = "No MEP data available" });

        return Results.Ok(mep);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching MEP rate: {ex.Message}");
    }
});

app.MapGet("/api/financial/health", async (IFinancialDataService financialService) =>
{
    try
    {
        var isHealthy = await financialService.IsFinancialDataAvailableAsync();
        return Results.Ok(new { 
            healthy = isHealthy, 
            timestamp = DateTime.UtcNow 
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error checking financial data health: {ex.Message}");
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
Console.WriteLine("  GET /debug/databases");
Console.WriteLine("Financial Data Endpoints:");
Console.WriteLine("  GET /api/financial/cauction");
Console.WriteLine("  GET /api/financial/market-data");
Console.WriteLine("  GET /api/financial/mep-rate");
Console.WriteLine("  GET /api/financial/health");
Console.WriteLine("Debug Endpoints:");
Console.WriteLine("  GET /debug/financial");
Console.WriteLine("  GET /debug/market-api");
Console.WriteLine("  GET /health");

app.Run();