using Serilog;
using Serilog.Formatting.Compact;

namespace Server.Utils;

public static class LogManager
{
    private static ILogger _logger;

    public static void Init()
    {
        string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        
        if(!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Async(a => a.File(
                new CompactJsonFormatter(),
                Path.Combine(logDir, "log.json"),
                rollingInterval: RollingInterval.Day, // 하루마다 새 파일 생성
                encoding: System.Text.Encoding.UTF8))
            .CreateLogger();
        
        Info("Serilog initialized");
    }

    public static void Stop()
    {
        Log.CloseAndFlush();
    }
    
    public static void Info(string msg) => _logger.Information(msg);
    public static void Warning(string msg) => _logger.Warning(msg);
    public static void Error(string msg) => _logger.Error(msg);
    public static void Exception(Exception e, string msg) => _logger.Error(e, msg);
}