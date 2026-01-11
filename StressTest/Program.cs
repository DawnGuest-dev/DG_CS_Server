class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Distributed Game Server Load Tester ===");
        Console.Write("Enter Bot Count (ex. 500): ");
        int botCount = int.Parse(Console.ReadLine());

        List<DummyBot> bots = new List<DummyBot>();

        // 1. 봇 생성 및 접속
        for (int i = 0; i < botCount; i++)
        {
            DummyBot bot = new DummyBot(i);
            // Zone 1 포트로 접속
            bot.Connect("127.0.0.1", 12345); 
            bots.Add(bot);

            // 한 번에 너무 많이 붙으면 로그인 처리가 밀릴 수 있으니 약간의 딜레이
            if (i % 10 == 0) Thread.Sleep(10);
            if (i % 100 == 0) Console.WriteLine($"[System] Spawned {i} bots...");
        }

        Console.WriteLine("[System] All Bots Spawned. Starting Loop...");

        // 2. 무한 루프 (업데이트)
        while (true)
        {
            foreach (var bot in bots)
            {
                bot.Update();
            }

            // 15ms 대기 (약 60fps 시뮬레이션)
            Thread.Sleep(15);
        }
    }
}