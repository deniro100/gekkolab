using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using GekkoLab.Services.Camera;
using GekkoLab.Services.PerformanceMonitoring;
using GekkoLab.Services.Repository;

namespace GekkoLab.Services.Telegram;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMetricsStore _metricsStore;
    private readonly ICameraCaptureProvider _cameraProvider;

    public TelegramBotService(
        ILogger<TelegramBotService> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IMetricsStore metricsStore,
        ICameraCaptureProvider cameraProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _metricsStore = metricsStore;
        _cameraProvider = cameraProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration["TelegramBot:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogInformation("Telegram bot is not configured (no token). Skipping.");
            return;
        }

        _logger.LogInformation("Starting Telegram bot...");

        var client = new TelegramBotClient(token);

        client.StartReceiving(
            updateHandler: (bot, update, ct) => HandleUpdateAsync(bot, update, ct),
            errorHandler: (bot, ex, source, ct) =>
            {
                _logger.LogError(ex, "Telegram bot error from {Source}", source);
                return Task.CompletedTask;
            },
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message]
            },
            cancellationToken: stoppingToken);

        var me = await client.GetMe(stoppingToken);
        _logger.LogInformation("Telegram bot started: @{BotUsername}", me.Username);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message?.Text == null) return;

        var chatId = update.Message.Chat.Id;
        var text = update.Message.Text.Trim().ToLower();

        // Strip bot username from commands (e.g., /status@mybotname)
        if (text.Contains('@'))
            text = text.Split('@')[0];

        try
        {
            switch (text)
            {
                case "/start":
                    await bot.SendMessage(chatId,
                        "🦎 *GekkoLab Bot*\n\n" +
                        "Available commands:\n" +
                        "/status — Sensor & weather readings\n" +
                        "/snapshot — Take a camera snapshot\n" +
                        "/health — System health info",
                        parseMode: ParseMode.Markdown, cancellationToken: ct);
                    break;

                case "/status":
                    await HandleStatusAsync(bot, chatId, ct);
                    break;

                case "/snapshot":
                    await HandleSnapshotAsync(bot, chatId, ct);
                    break;

                case "/health":
                    await HandleHealthAsync(bot, chatId, ct);
                    break;

                default:
                    await bot.SendMessage(chatId,
                        "Unknown command. Use /start to see available commands.",
                        cancellationToken: ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Telegram command: {Command}", text);
            await bot.SendMessage(chatId, "❌ Error processing command.", cancellationToken: ct);
        }
    }

    private async Task HandleStatusAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sensorRepo = scope.ServiceProvider.GetRequiredService<ISensorReadingRepository>();
        var weatherRepo = scope.ServiceProvider.GetRequiredService<IWeatherReadingRepository>();

        var sensor = await sensorRepo.GetLatestReadingAsync();
        var weather = await weatherRepo.GetLatestAsync();

        var msg = "🦎 *GekkoLab Status*\n\n";

        if (sensor != null)
        {
            msg += "🏠 *Indoor (BME280)*\n" +
                   $"🌡 Temperature: `{sensor.Temperature:F1}°C`\n" +
                   $"💧 Humidity: `{sensor.Humidity:F1}%`\n" +
                   $"🌬 Pressure: `{sensor.Pressure:F1} mmHg`\n" +
                   $"🕐 Updated: {sensor.Timestamp:HH:mm:ss}\n\n";
        }
        else
        {
            msg += "🏠 Indoor: _No data_\n\n";
        }

        if (weather != null)
        {
            msg += $"🌍 *Outdoor ({weather.Location})*\n" +
                   $"🌡 Temperature: `{weather.Temperature:F1}°C`\n" +
                   $"💧 Humidity: `{weather.Humidity:F1}%`\n" +
                   $"🕐 Updated: {weather.Timestamp:HH:mm:ss}";
        }
        else
        {
            msg += "🌍 Outdoor: _No data_";
        }

        await bot.SendMessage(chatId, msg, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }

    private async Task HandleSnapshotAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        await bot.SendMessage(chatId, "📷 Capturing snapshot...", cancellationToken: ct);

        var camera = _cameraProvider.GetCapture();
        if (!camera.IsAvailable)
        {
            await bot.SendMessage(chatId, "❌ Camera is not available.", cancellationToken: ct);
            return;
        }

        var imageBytes = await camera.CaptureFrameAsync();
        if (imageBytes == null || imageBytes.Length == 0)
        {
            await bot.SendMessage(chatId, "❌ Failed to capture snapshot.", cancellationToken: ct);
            return;
        }

        using var stream = new MemoryStream(imageBytes);
        await bot.SendPhoto(chatId,
            InputFile.FromStream(stream, $"snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg"),
            caption: $"📸 Snapshot — {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            cancellationToken: ct);
    }

    private async Task HandleHealthAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        var metrics = _metricsStore.GetLatest();
        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();

        var msg = "🖥 *System Health*\n\n" +
                  $"⏱ Uptime: `{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m`\n";

        if (metrics != null)
        {
            msg += $"🔲 CPU: `{metrics.CpuUsagePercent:F1}%`\n" +
                   $"🧠 RAM: `{metrics.MemoryUsagePercent:F1}%` ({FormatBytes(metrics.MemoryUsedBytes)}/{FormatBytes(metrics.MemoryTotalBytes)})\n" +
                   $"💾 Disk: `{metrics.DiskUsagePercent:F1}%` ({FormatBytes(metrics.DiskUsedBytes)}/{FormatBytes(metrics.DiskTotalBytes)})\n" +
                   $"🕐 Sampled: {metrics.Timestamp:HH:mm:ss}";
        }
        else
        {
            msg += "_No metrics data available yet_";
        }

        await bot.SendMessage(chatId, msg, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F0} MB";
        return $"{bytes / 1024.0:F0} KB";
    }
}
