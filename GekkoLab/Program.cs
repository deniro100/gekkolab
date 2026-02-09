using GekkoLab.Models;
using GekkoLab.Services;
using GekkoLab.Services.Bme280Reader;
using GekkoLab.Services.Camera;
using GekkoLab.Services.GekkoDetector;
using GekkoLab.Services.PerformanceMonitoring;
using GekkoLab.Services.Repository;
using GekkoLab.Services.WeatherReader;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add SQLite Database
builder.Services.AddDbContext<GekkoLabDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SQLite")));

// Register repositories
builder.Services.AddScoped<ISensorReadingRepository, SensorReadingRepository>();
builder.Services.AddScoped<IWeatherReadingRepository, WeatherReadingRepository>();
builder.Services.AddScoped<IGekkoDetectionRepository, GekkoDetectionRepository>();
builder.Services.AddScoped<IGekkoSightingRepository, GekkoSightingRepository>();

// Register sensor reader
// Register sensor reader provider and resolve IBme280Reader from it
builder.Services.AddSingleton<IBme280SensorReaderProvider, Bme280SensorReaderProvider>();
builder.Services.AddSingleton<IBme280Reader>(sp => 
    sp.GetRequiredService<IBme280SensorReaderProvider>().GetReader());

// Register camera services
builder.Services.AddSingleton<ICameraCaptureProvider, CameraCaptureProvider>();
builder.Services.AddSingleton<IMotionDetector, SimpleMotionDetector>();

// Register weather reader
builder.Services.AddSingleton<IWeatherReader, OpenMeteoWeatherReader>();

// Register gecko detector services
builder.Services.AddSingleton<IGekkoDetectorProvider, GekkoDetectorProvider>();

// Register performance monitoring services
builder.Services.AddSingleton<IMetricsStore, InMemoryMetricsStore>();
builder.Services.AddSingleton<ISystemMetricsCollectorProvider, SystemMetricsCollectorProvider>();
builder.Services.AddScoped<ISystemMetricsRepository, SystemMetricsRepository>();

// Register background services
builder.Services.AddHostedService<SensorPollingService>();
builder.Services.AddHostedService<MotionDetectionService>();
builder.Services.AddHostedService<PerformanceMonitoringService>();
builder.Services.AddHostedService<WeatherPollingService>();
builder.Services.AddHostedService<GekkoDetectorService>();

var app = builder.Build();

// Ensure data directory exists
Directory.CreateDirectory("gekkodata");

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GekkoLabDbContext>();
    try
    {
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Migration");
        logger.LogError(ex, "An error occurred while migrating the database");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}
app.UseDefaultFiles(); // Serve index.html as default
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Disable caching for HTML files
        if (ctx.File.Name.EndsWith(".html"))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "0");
        }
    }
});
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();