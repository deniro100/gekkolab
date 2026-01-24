using GekkoLab.Models;
using GekkoLab.Services;
using GekkoLab.Services.Bme280Reader;
using GekkoLab.Services.Camera;
using GekkoLab.Services.PerformanceMonitoring;
using GekkoLab.Services.Repository;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add SQLite Database
builder.Services.AddDbContext<GekkoLabDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SQLite")));

// Register repositories
builder.Services.AddScoped<ISensorReadingRepository, SensorReadingRepository>();

// Register sensor reader
// Register sensor reader provider and resolve IBme280Reader from it
builder.Services.AddSingleton<IBme280SensorReaderProvider, Bme280SensorReaderProvider>();
builder.Services.AddSingleton<IBme280Reader>(sp => 
    sp.GetRequiredService<IBme280SensorReaderProvider>().GetReader());

// Register camera services
builder.Services.AddSingleton<ICameraCaptureProvider, CameraCaptureProvider>();
builder.Services.AddSingleton<IMotionDetector, SimpleMotionDetector>();

// Register performance monitoring services
builder.Services.AddSingleton<IMetricsStore, InMemoryMetricsStore>();
builder.Services.AddSingleton<ISystemMetricsCollectorProvider, SystemMetricsCollectorProvider>();
builder.Services.AddScoped<ISystemMetricsRepository, SystemMetricsRepository>();

// Register background services
builder.Services.AddHostedService<SensorPollingService>();
builder.Services.AddHostedService<MotionDetectionService>();
builder.Services.AddHostedService<PerformanceMonitoringService>();

var app = builder.Build();

// Ensure data directory exists
Directory.CreateDirectory("gekkodata");

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GekkoLabDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseDefaultFiles(); // Serve index.html as default
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();