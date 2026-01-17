using System.Collections.Concurrent;
using System.Diagnostics;

class Program
{
    // 스레드 안전한 리스트 (모든 스레드가 여기에 접근)
    static ConcurrentBag<DummyBot> bots = new();
    static bool isRunning = true;

    static void Main(string[] args)
    {
        Console.WriteLine("=== Distributed Game Server Load Tester (High Perf) ===");
        Console.Write("Enter Bot Count (ex. 1000): ");
        if (!int.TryParse(Console.ReadLine(), out int botCount)) return;

        Console.Write("Target IP (default: 127.0.0.1): ");
        string ip = Console.ReadLine();
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

        Console.Write("Target Port (default: 12345): ");
        string portStr = Console.ReadLine();
        int port = string.IsNullOrEmpty(portStr) ? 12345 : int.Parse(portStr);

        // 1. 업데이트 루프 실행 (별도 태스크)
        // 봇이 추가되는 즉시 관리하기 시작함
        Task.Run(UpdateLoop);

        Console.WriteLine($"[System] Spawning {botCount} bots to {ip}:{port}...");
        Stopwatch sw = Stopwatch.StartNew();

        // 2. [최적화 핵심] 병렬 접속 (Parallel Connection)
        // Main 스레드 혼자 만드는 게 아니라, CPU 코어를 다 써서 동시에 접속 요청을 날림
        Parallel.For(0, botCount, new ParallelOptions { MaxDegreeOfParallelism = 32 }, (i) =>
        {
            try
            {
                DummyBot bot = new DummyBot(i);
                
                // Connect 내부가 동기 방식이면 여기서 약간의 블로킹이 발생하지만 
                // Parallel 덕분에 32개가 동시에 시도됨.
                bot.Connect(ip, port); 
                
                bots.Add(bot);

                // 로그 너무 많이 찍으면 콘솔이 느려지므로 100단위만 출력
                if (bots.Count % 100 == 0) 
                    Console.WriteLine($"[System] Connected: {bots.Count}/{botCount}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] Bot {i} failed: {e.Message}");
            }
        });

        sw.Stop();
        Console.WriteLine($"[System] All {bots.Count} Bots Spawned in {sw.Elapsed.TotalSeconds:F2}s!");

        // 종료 방지
        while (true)
        {
            string cmd = Console.ReadLine();
            if (cmd == "q") break;
        }
    }

    // 3. [최적화 핵심] 병렬 업데이트 루프
    static void UpdateLoop()
    {
        Console.WriteLine("[System] Parallel Update Loop Started.");
        
        while (isRunning)
        {
            long start = Environment.TickCount64;

            // 기존 foreach (var bot in bots) -> 싱글 스레드 (느림)
            // 변경 Parallel.ForEach -> 멀티 스레드 (빠름)
            // 봇이 1000명이면 CPU 코어들이 나눠서 업데이트를 실행함
            Parallel.ForEach(bots, bot =>
            {
                try
                {
                    bot.Update();
                }
                catch { /* 에러 무시 */ }
            });

            long end = Environment.TickCount64;
            long elapsed = end - start;

            // 60FPS(16ms) 유지를 위한 Sleep 계산
            // 처리가 5ms 걸렸으면 11ms를 쉼. 처리가 20ms 걸렸으면 안 쉬고 바로 다음 프레임.
            int sleepTime = (int)(16 - elapsed);
            if (sleepTime > 0)
            {
                Thread.Sleep(sleepTime);
            }
        }
    }
}