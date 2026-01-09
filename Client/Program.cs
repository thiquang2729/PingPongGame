namespace Client;

class Program
{
    static NetworkClient? _networkClient;
    static Display? _display;
    static bool _isGameRunning;
    static bool _isGameOver;

    static async Task Main(string[] args)
    {
        Console.Title = "Pong Client";
        Console.CursorVisible = false;

        // Parse arguments
        string serverIp = "127.0.0.1";
        int serverPort = 5000;

        if (args.Length > 0) serverIp = args[0];
        if (args.Length > 1 && int.TryParse(args[1], out int port)) serverPort = port;

        Console.WriteLine("=== PONG CLIENT ===");
        Console.WriteLine($"Đang kết nối đến {serverIp}:{serverPort}...\n");

        // Khởi tạo
        _display = new Display();
        _networkClient = new NetworkClient(serverIp, serverPort);

        // Đăng ký events
        _networkClient.OnPlayerIdReceived += OnPlayerIdReceived;
        _networkClient.OnWaiting += OnWaiting;
        _networkClient.OnGameStart += OnGameStart;
        _networkClient.OnGameUpdate += OnGameUpdate;
        _networkClient.OnGameOver += OnGameOver;
        _networkClient.OnOpponentDisconnected += OnOpponentDisconnected;

        // Kết nối
        if (!await _networkClient.ConnectAsync())
        {
            Console.WriteLine("Không thể kết nối đến server!");
            Console.WriteLine("Nhấn Enter để thoát...");
            Console.ReadLine();
            return;
        }

        // Vòng lặp xử lý input
        await InputLoopAsync();
    }

    static void OnPlayerIdReceived(int playerId)
    {
        Console.WriteLine($"Bạn là Player {playerId} (Bên {(playerId == 1 ? "TRÁI" : "PHẢI")})");
    }

    static void OnWaiting()
    {
        Console.WriteLine("Đang chờ người chơi khác...");
    }

    static void OnGameStart(int width, int height)
    {
        Console.Clear();
        _display = new Display(width, height);
        _display.Initialize();
        _isGameRunning = true;
    }

    static void OnGameUpdate(int ballX, int ballY, int p1Y, int p2Y, int score1, int score2)
    {
        if (_display == null || !_isGameRunning) return;

        _display.Update(ballX, ballY, p1Y, p2Y, score1, score2, _networkClient?.Ping ?? 0);
    }

    static void OnGameOver(int winner)
    {
        _isGameRunning = false;
        _isGameOver = true;

        if (_display != null && _networkClient != null)
        {
            _display.ShowGameOver(winner, _networkClient.PlayerId);
        }
    }

    static void OnOpponentDisconnected(int disconnectedPlayer)
    {
        _isGameRunning = false;
        _display?.ShowDisconnected();
    }

    static async Task InputLoopAsync()
    {
        while (!_isGameOver)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        if (_networkClient != null && _isGameRunning)
                            await _networkClient.SendMoveAsync("UP");
                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        if (_networkClient != null && _isGameRunning)
                            await _networkClient.SendMoveAsync("DOWN");
                        break;

                    case ConsoleKey.Escape:
                        if (_networkClient != null)
                            await _networkClient.SendQuitAsync();
                        _networkClient?.Disconnect();
                        return;
                }
            }

            await Task.Delay(10); // Giảm CPU usage
        }

        // Game over - chờ Enter
        Console.ReadLine();
        _networkClient?.Disconnect();
    }
}
