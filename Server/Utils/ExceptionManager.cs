namespace Server.Utils;

public static class ExceptionManager
{
    public static void Init()
    {
        // 일반 Exception 처리
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        
        // 비동기 Task에서 await 안 잡은 Exception 처리
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        
        LogManager.Info("ExceptionManager Initialized");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception;
        
        LogManager.Exception(ex, "[CRITICAL] Unhandled Exception");

        if (e.IsTerminating)
        {
            LogManager.Error("Server is terminating by unhandled exception.");
            LogManager.Stop();
        }
    }
    
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogManager.Exception(e.Exception, "[CRITICAL] Unobserved Task Exception");
        
        e.SetObserved(); // 프로세스 종료 방지
    }
}