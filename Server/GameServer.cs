using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server;

/// <summary>
/// Quản lý kết nối Socket cho game Pong
/// Hỗ trợ 2 người chơi qua TCP/IP
/// </summary>
public class GameServer
{
    private readonly int _port;
    private TcpListener? _listener;
    private TcpClient? _client1;
    private TcpClient? _client2;
    private NetworkStream? _stream1;
    private NetworkStream? _stream2;

    private readonly GameState _gameState;
    private bool _isRunning;
    private readonly object _lock = new object();

    // Ping tracking
    private DateTime _lastPingTime1;
    private DateTime _lastPingTime2;
    private int _ping1;
    private int _ping2;

    public GameServer(int port = 5000)
    {
        _port = port;
        _gameState = new GameState();
    }

    /// <summary>
    /// Khởi động server và chờ kết nối
    /// </summary>
    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _isRunning = true;

        Console.WriteLine($"=== PONG SERVER ===");
        Console.WriteLine($"Đang lắng nghe tại port {_port}...");
        Console.WriteLine($"Chờ 2 người chơi kết nối...\n");

        // Chờ Player 1
        Console.WriteLine("Đang chờ Player 1...");
        _client1 = await _listener.AcceptTcpClientAsync();
        _stream1 = _client1.GetStream();
        await SendMessageAsync(_stream1, "ID|1");
        Console.WriteLine($"✓ Player 1 đã kết nối từ {_client1.Client.RemoteEndPoint}");

        // Thông báo chờ Player 2
        await SendMessageAsync(_stream1, "WAIT");

        // Chờ Player 2
        Console.WriteLine("Đang chờ Player 2...");
        _client2 = await _listener.AcceptTcpClientAsync();
        _stream2 = _client2.GetStream();
        await SendMessageAsync(_stream2, "ID|2");
        Console.WriteLine($"✓ Player 2 đã kết nối từ {_client2.Client.RemoteEndPoint}");

        // Bắt đầu game
        string startInfo = $"START|{_gameState.BoardWidth},{_gameState.BoardHeight}";
        await SendMessageAsync(_stream1, startInfo);
        await SendMessageAsync(_stream2, startInfo);
        Console.WriteLine("\n=== GAME BẮT ĐẦU ===\n");

        // Khởi động các luồng xử lý
        _ = Task.Run(() => ReceiveFromClientAsync(_stream1, 1));
        _ = Task.Run(() => ReceiveFromClientAsync(_stream2, 2));

        // Game loop
        await RunGameLoopAsync();
    }

    /// <summary>
    /// Vòng lặp chính của game - chạy ở 30 FPS
    /// </summary>
    private async Task RunGameLoopAsync()
    {
        const int tickRate = 33; // ~30 FPS

        while (_isRunning && !_gameState.IsGameOver)
        {
            try
            {
                // Cập nhật trạng thái game
                _gameState.Update();

                // Gửi UPDATE cho cả 2 client
                string updateMsg = _gameState.GetUpdateString();
                await BroadcastAsync(updateMsg);

                // Hiển thị trạng thái trên server
                Console.SetCursorPosition(0, 8);
                Console.WriteLine($"Ball: ({_gameState.BallX}, {_gameState.BallY})    ");
                Console.WriteLine($"P1 Paddle: {_gameState.Paddle1Y}  |  P2 Paddle: {_gameState.Paddle2Y}    ");
                Console.WriteLine($"Score: {_gameState.Score1} - {_gameState.Score2}    ");
                Console.WriteLine($"Ping: P1={_ping1}ms  P2={_ping2}ms    ");

                await Task.Delay(tickRate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi game loop: {ex.Message}");
                break;
            }
        }

        // Game kết thúc
        if (_gameState.IsGameOver)
        {
            string overMsg = $"OVER|{_gameState.Winner}";
            await BroadcastAsync(overMsg);
            Console.WriteLine($"\n=== GAME KẾT THÚC - Player {_gameState.Winner} THẮNG! ===");
        }

        await StopAsync();
    }

    /// <summary>
    /// Nhận dữ liệu từ client
    /// </summary>
    private async Task ReceiveFromClientAsync(NetworkStream stream, int playerId)
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (_isRunning)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                ProcessMessage(message, playerId);

                // Cập nhật ping
                if (playerId == 1)
                {
                    _ping1 = (int)(DateTime.Now - _lastPingTime1).TotalMilliseconds;
                    _lastPingTime1 = DateTime.Now;
                }
                else
                {
                    _ping2 = (int)(DateTime.Now - _lastPingTime2).TotalMilliseconds;
                    _lastPingTime2 = DateTime.Now;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Player {playerId} ngắt kết nối: {ex.Message}");
            await HandleDisconnectAsync(playerId);
        }
    }

    /// <summary>
    /// Xử lý message từ client
    /// </summary>
    private void ProcessMessage(string message, int playerId)
    {
        string[] parts = message.Split('|');
        string command = parts[0];

        switch (command)
        {
            case "MOVE":
                if (parts.Length > 1)
                {
                    lock (_lock)
                    {
                        _gameState.MovePaddle(playerId, parts[1]);
                    }
                }
                break;

            case "QUIT":
                Console.WriteLine($"Player {playerId} thoát game");
                _ = HandleDisconnectAsync(playerId);
                break;
        }
    }

    /// <summary>
    /// Xử lý khi client ngắt kết nối
    /// </summary>
    private async Task HandleDisconnectAsync(int disconnectedPlayer)
    {
        int otherPlayer = disconnectedPlayer == 1 ? 2 : 1;
        NetworkStream? otherStream = disconnectedPlayer == 1 ? _stream2 : _stream1;

        if (otherStream != null)
        {
            await SendMessageAsync(otherStream, $"DISCONNECT|{disconnectedPlayer}");
            Console.WriteLine($"Đã thông báo Player {otherPlayer} về việc mất kết nối");
        }

        _isRunning = false;
    }

    /// <summary>
    /// Gửi message đến một client
    /// </summary>
    private async Task SendMessageAsync(NetworkStream stream, string message)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi gửi message: {ex.Message}");
        }
    }

    /// <summary>
    /// Gửi message đến cả 2 client
    /// </summary>
    private async Task BroadcastAsync(string message)
    {
        if (_stream1 != null) await SendMessageAsync(_stream1, message);
        if (_stream2 != null) await SendMessageAsync(_stream2, message);
    }

    /// <summary>
    /// Dừng server
    /// </summary>
    public async Task StopAsync()
    {
        _isRunning = false;

        _stream1?.Close();
        _stream2?.Close();
        _client1?.Close();
        _client2?.Close();
        _listener?.Stop();

        Console.WriteLine("Server đã dừng.");
        await Task.CompletedTask;
    }
}
