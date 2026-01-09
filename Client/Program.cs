namespace Client;

class Program
{
    static NetworkClient? _networkClient;
    static Display? _display;
    static bool _isGameRunning;
    static bool _isGameOver;
    static bool _isInRoom;
    static bool _isReady;
    static bool _opponentReady;
    static bool _waitingForOpponent;
    static int _boardWidth = 80;
    static int _boardHeight = 24;
    static int _boardWidth = 80;
    static int _boardHeight = 24;

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
        _networkClient.OnRoomJoined += OnRoomJoined;
        _networkClient.OnReadyStatusUpdate += OnReadyStatusUpdate;
        _networkClient.OnGameStart += OnGameStart;
        _networkClient.OnGameResume += OnGameResume;
        _networkClient.OnGameUpdate += OnGameUpdate;
        _networkClient.OnGameOver += OnGameOver;
        _networkClient.OnOpponentDisconnected += OnOpponentDisconnected;
        _networkClient.OnOpponentReconnected += OnOpponentReconnected;
        _networkClient.OnReconnected += OnReconnected;

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

    static void OnRoomJoined(int width, int height)
    {
        _isInRoom = true;
        _boardWidth = width;
        _boardHeight = height;
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║           PHÒNG CHỜ - PONG GAME        ║");
        Console.WriteLine("╠════════════════════════════════════════╣");
        Console.WriteLine($"║  Bạn là: Player {_networkClient?.PlayerId}                       ║");
        Console.WriteLine("║                                        ║");
        Console.WriteLine("║  Nhấn [ENTER] hoặc [SPACE] để SẴN SÀNG ║");
        Console.WriteLine("║                                        ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();
        UpdateReadyDisplay();
    }

    static void OnReadyStatusUpdate(bool p1Ready, bool p2Ready)
    {
        _opponentReady = _networkClient?.PlayerId == 1 ? p2Ready : p1Ready;
        UpdateReadyDisplay();
    }

    static void UpdateReadyDisplay()
    {
        if (!_isInRoom) return;

        Console.SetCursorPosition(0, 10);
        Console.WriteLine($"  Player 1: {(_networkClient?.PlayerId == 1 ? (_isReady ? "✓ SẴN SÀNG" : "⌛ Chưa sẵn sàng") : (_opponentReady || (_networkClient?.PlayerId == 2 && _isReady) ? "✓ SẴN SÀNG" : "⌛ Chưa sẵn sàng"))}    ");
        
        bool p2Status = _networkClient?.PlayerId == 2 ? _isReady : _opponentReady;
        Console.WriteLine($"  Player 2: {(p2Status ? "✓ SẴN SÀNG" : "⌛ Chưa sẵn sàng")}    ");

        if (_isReady)
        {
            Console.WriteLine("\n  Đang chờ đối thủ...");
        }
    }

    static void OnGameStart(int width, int height)
    {
        Console.Clear();
        _display = new Display(width, height);
        _display.Initialize();
        _isGameRunning = true;
        _isInRoom = false;
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
        _waitingForOpponent = true;
        if (_display != null && _isGameRunning)
        {
            Console.SetCursorPosition(20, 12);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Đối thủ mất kết nối! Đang chờ kết nối lại...");
            Console.ResetColor();
        }
    }

    static void OnOpponentReconnected()
    {
        _waitingForOpponent = false;
        if (_display != null && _isGameRunning)
        {
            Console.SetCursorPosition(20, 12);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Đối thủ đã kết nối lại! Tiếp tục game...     ");
            Console.ResetColor();
            Thread.Sleep(1000);
            // Xóa thông báo
            Console.SetCursorPosition(20, 12);
            Console.Write(new string(' ', 50));
        }
    }

    static void OnReconnected()
    {
        Console.Clear();
        Console.WriteLine("✓ Đã kết nối lại! Tiếp tục game...");
        Thread.Sleep(1000);
    }

    static void OnGameResume(int width, int height)
    {
        if (_display == null)
        {
            _display = new Display(width, height);
            _display.Initialize();
        }
        _isGameRunning = true;
        _waitingForOpponent = false;
    }

    static async Task InputLoopAsync()
    {
        while (!_isGameOver)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                // Xử lý Ready khi ở trong phòng chờ
                if (_isInRoom && !_isReady)
                {
                    if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
                    {
                        _isReady = true;
                        if (_networkClient != null)
                        {
                            await _networkClient.SendReadyAsync();
                        }
                        UpdateReadyDisplay();
                        continue;
                    }
                }

                // Xử lý game
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
