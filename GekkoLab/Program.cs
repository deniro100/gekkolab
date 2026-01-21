using GekkoLab.Models;
using GekkoLab.Services;
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
builder.Services.AddSingleton<IBme280Reader, Bme280Reader>();

// Register background services
builder.Services.AddHostedService<SensorPollingService>();

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GekkoLabDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
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

app.Run();