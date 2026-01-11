using System.Collections.Concurrent;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Distributed Game Server Load Tester ===");
        Console.Write("Enter Bot Count (ex. 500): ");
        int botCount = int.Parse(Console.ReadLine());
        
        ConcurrentBag<DummyBot> bots = new ConcurrentBag<DummyBot>();
        
        Task.Run(() =>
        {
            Console.WriteLine("[System] Update Loop Started.");
            while (true)
            {
                // 생성된 봇들만 순회하며 업데이트
                foreach (var bot in bots)
                {
                    bot.Update();
                }
                Thread.Sleep(15); // 약 60fps
            }
        });

        // 3. 봇 생성 및 접속 (메인 스레드)
        for (int i = 0; i < botCount; i++)
        {
            DummyBot bot = new DummyBot(i);
            bot.Connect("127.0.0.1", 12345);
            
            bots.Add(bot); // 여기서 추가하면 위 Task에서 즉시 가져다가 돌림

            if (i % 10 == 0) Thread.Sleep(10);
            if (i % 100 == 0) Console.WriteLine($"[System] Spawned {i} bots...");
        }

        Console.WriteLine("[System] All Bots Spawned. Main thread waiting...");

        // 메인 스레드 종료 방지
        while (true) Thread.Sleep(1000);
    }
}