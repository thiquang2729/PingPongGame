using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server;

/// <summary>
/// Quản lý kết nối Socket cho game Pong
/// Hỗ trợ 2 người chơi với Ready, Reconnect và restart game
/// </summary>
public class GameServer
{
    private readonly int _port;
    private TcpListener? _listener;
    private TcpClient? _client1;
    private TcpClient? _client2;
    private NetworkStream? _stream1;
    private NetworkStream? _stream2;

    private GameState _gameState;
    private bool _isRunning;
    private bool _serverActive;
    private bool _player1Ready;
    private bool _player2Ready;
    private bool _player1Connected;
    private bool _player2Connected;
    private bool _waitingForReconnect;
    private int _disconnectedPlayer;
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
    /// Khởi động server và chạy vòng lặp chính
    /// </summary>
    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _serverActive = true;

        Console.WriteLine($"=== PONG SERVER ===");
        Console.WriteLine($"Đang lắng nghe tại port {_port}...");

        // Vòng lặp chính - cho phép chơi nhiều ván
        while (_serverActive)
        {
            await WaitForPlayersAndPlayAsync();

            if (_serverActive)
            {
                Console.WriteLine("\n--- Chuẩn bị ván mới ---\n");
            }
        }

        _listener.Stop();
        Console.WriteLine("Server đã tắt.");
    }

    /// <summary>
    /// Chờ 2 người chơi kết nối và chơi 1 ván
    /// </summary>
    private async Task WaitForPlayersAndPlayAsync()
    {
        // Reset game state
        _gameState = new GameState();
        _isRunning = true;
        _player1Ready = false;
        _player2Ready = false;
        _player1Connected = false;
        _player2Connected = false;
        _waitingForReconnect = false;

        try
        {
            // Đóng kết nối cũ nếu có
            CleanupConnections();

            Console.WriteLine("Chờ 2 người chơi kết nối...\n");

            // Chờ Player 1
            Console.WriteLine("Đang chờ Player 1...");
            _client1 = await _listener!.AcceptTcpClientAsync();
            _stream1 = _client1.GetStream();
            _player1Connected = true;
            await SendMessageAsync(_stream1, "ID|1");
            Console.WriteLine($"✓ Player 1 đã kết nối từ {_client1.Client.RemoteEndPoint}");

            // Thông báo chờ Player 2
            await SendMessageAsync(_stream1, "WAIT");

            // Chờ Player 2
            Console.WriteLine("Đang chờ Player 2...");
            _client2 = await _listener.AcceptTcpClientAsync();
            _stream2 = _client2.GetStream();
            _player2Connected = true;
            await SendMessageAsync(_stream2, "ID|2");
            Console.WriteLine($"✓ Player 2 đã kết nối từ {_client2.Client.RemoteEndPoint}");

            // Bắt đầu luồng nhận
            _ = Task.Run(() => ReceiveFromClientAsync(1));
            _ = Task.Run(() => ReceiveFromClientAsync(2));

            // Vào phòng chờ Ready
            await EnterRoomAndWaitReadyAsync();

            if (!_isRunning) return;

            // Game loop
            await RunGameLoopAsync();

        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is SocketException)
        {
            Console.WriteLine($"Kết nối bị ngắt: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
            CleanupConnections();
        }
    }

    /// <summary>
    /// Vào phòng chờ và đợi cả 2 Ready
    /// </summary>
    private async Task EnterRoomAndWaitReadyAsync()
    {
        _player1Ready = false;
        _player2Ready = false;

        // Thông báo vào phòng
        string roomInfo = $"ROOM|{_gameState.BoardWidth},{_gameState.BoardHeight}";
        await BroadcastAsync(roomInfo);
        Console.WriteLine("\n=== CHỜ CẢ 2 NGƯỜI CHƠI SẴN SÀNG ===\n");

        // Chờ cả 2 Ready
        while (_isRunning && (!_player1Ready || !_player2Ready))
        {
            string readyStatus = $"READY_STATUS|{(_player1Ready ? 1 : 0)},{(_player2Ready ? 1 : 0)}";
            await BroadcastAsync(readyStatus);

            Console.SetCursorPosition(0, 10);
            Console.WriteLine($"Player 1: {(_player1Ready ? "✓ SẴN SÀNG" : "⌛ Chờ...")}    ");
            Console.WriteLine($"Player 2: {(_player2Ready ? "✓ SẴN SÀNG" : "⌛ Chờ...")}    ");

            await Task.Delay(100);
        }

        if (!_isRunning) return;

        // Bắt đầu game
        string startInfo = $"START|{_gameState.BoardWidth},{_gameState.BoardHeight}";
        await BroadcastAsync(startInfo);
        Console.WriteLine("\n=== GAME BẮT ĐẦU ===\n");
    }

    /// <summary>
    /// Dọn dẹp kết nối
    /// </summary>
    private void CleanupConnections()
    {
        try { _stream1?.Close(); } catch { }
        try { _stream2?.Close(); } catch { }
        try { _client1?.Close(); } catch { }
        try { _client2?.Close(); } catch { }

        _stream1 = null;
        _stream2 = null;
        _client1 = null;
        _client2 = null;
        _player1Connected = false;
        _player2Connected = false;
    }

    /// <summary>
    /// Vòng lặp game với hỗ trợ reconnect
    /// </summary>
    private async Task RunGameLoopAsync()
    {
        const int tickRate = 33;

        while (_isRunning && !_gameState.IsGameOver)
        {
            try
            {
                // Kiểm tra mất kết nối
                if (!_player1Connected || !_player2Connected)
                {
                    _disconnectedPlayer = !_player1Connected ? 1 : 2;
                    
                    // Thông báo cho người còn lại
                    var connectedStream = _disconnectedPlayer == 1 ? _stream2 : _stream1;
                    if (connectedStream != null)
                    {
                        await SendMessageAsync(connectedStream, $"OPPONENT_DISCONNECTED|{_disconnectedPlayer}");
                    }

                    Console.WriteLine($"\n⚠ Player {_disconnectedPlayer} mất kết nối!");
                    Console.WriteLine("Đang chờ kết nối lại (30 giây)...\n");

                    // Chờ reconnect
                    bool reconnected = await WaitForReconnectAsync(_disconnectedPlayer);
                    
                    if (!reconnected)
                    {
                        Console.WriteLine("Hết thời gian chờ. Game kết thúc.");
                        break;
                    }

                    Console.WriteLine($"✓ Player {_disconnectedPlayer} đã kết nối lại!");
                    
                    // Gửi lại trạng thái game hiện tại
                    await BroadcastAsync($"RESUME|{_gameState.BoardWidth},{_gameState.BoardHeight}");
                    await Task.Delay(500);
                }

                // Game logic
                lock (_lock)
                {
                    _gameState.Update();
                }

                string updateMsg = _gameState.GetUpdateString();
                await BroadcastAsync(updateMsg);

                // Hiển thị trên server
                Console.SetCursorPosition(0, 8);
                Console.WriteLine($"Ball: ({_gameState.BallX}, {_gameState.BallY})    ");
                Console.WriteLine($"P1 Paddle: {_gameState.Paddle1Y}  |  P2 Paddle: {_gameState.Paddle2Y}    ");
                Console.WriteLine($"Score: {_gameState.Score1} - {_gameState.Score2}    ");
                Console.WriteLine($"Ping: P1={_ping1}ms  P2={_ping2}ms    ");
                Console.WriteLine($"P1: {(_player1Connected ? "✓" : "✗")}  P2: {(_player2Connected ? "✓" : "✗")}    ");

                await Task.Delay(tickRate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi game loop: {ex.Message}");
            }
        }

        // Game kết thúc
        if (_gameState.IsGameOver)
        {
            string overMsg = $"OVER|{_gameState.Winner}";
            await BroadcastAsync(overMsg);
            Console.WriteLine($"\n=== GAME KẾT THÚC - Player {_gameState.Winner} THẮNG! ===");
        }
    }

    /// <summary>
    /// Chờ người chơi kết nối lại (30 giây)
    /// </summary>
    private async Task<bool> WaitForReconnectAsync(int playerToWait)
    {
        _waitingForReconnect = true;
        var timeout = DateTime.Now.AddSeconds(30);

        while (DateTime.Now < timeout && _isRunning)
        {
            // Kiểm tra có client mới kết nối không
            if (_listener!.Pending())
            {
                var newClient = await _listener.AcceptTcpClientAsync();
                var newStream = newClient.GetStream();

                // Gán cho player bị mất kết nối
                if (playerToWait == 1)
                {
                    try { _client1?.Close(); } catch { }
                    _client1 = newClient;
                    _stream1 = newStream;
                    _player1Connected = true;
                    await SendMessageAsync(_stream1, "ID|1");
                    await SendMessageAsync(_stream1, "RECONNECTED");
                    _ = Task.Run(() => ReceiveFromClientAsync(1));
                }
                else
                {
                    try { _client2?.Close(); } catch { }
                    _client2 = newClient;
                    _stream2 = newStream;
                    _player2Connected = true;
                    await SendMessageAsync(_stream2, "ID|2");
                    await SendMessageAsync(_stream2, "RECONNECTED");
                    _ = Task.Run(() => ReceiveFromClientAsync(2));
                }

                // Thông báo cho người còn lại
                var otherStream = playerToWait == 1 ? _stream2 : _stream1;
                if (otherStream != null)
                {
                    await SendMessageAsync(otherStream, "OPPONENT_RECONNECTED");
                }

                _waitingForReconnect = false;
                return true;
            }

            // Hiển thị countdown
            int remaining = (int)(timeout - DateTime.Now).TotalSeconds;
            Console.SetCursorPosition(0, 14);
            Console.WriteLine($"Còn {remaining} giây...    ");

            await Task.Delay(500);
        }

        _waitingForReconnect = false;
        return false;
    }

    /// <summary>
    /// Nhận dữ liệu từ client
    /// </summary>
    private async Task ReceiveFromClientAsync(int playerId)
    {
        byte[] buffer = new byte[1024];
        NetworkStream? stream = playerId == 1 ? _stream1 : _stream2;

        try
        {
            while (_isRunning && stream != null && stream.CanRead)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // Client đóng kết nối
                    lock (_lock)
                    {
                        if (playerId == 1) _player1Connected = false;
                        else _player2Connected = false;
                    }
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                string[] messages = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var msg in messages)
                {
                    ProcessMessage(msg.Trim(), playerId);
                }

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
        catch (Exception)
        {
            lock (_lock)
            {
                if (playerId == 1) _player1Connected = false;
                else _player2Connected = false;
            }
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
            case "READY":
                lock (_lock)
                {
                    if (playerId == 1) _player1Ready = true;
                    else _player2Ready = true;
                }
                Console.WriteLine($"Player {playerId} đã sẵn sàng!");
                break;

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
                _isRunning = false;
                break;
        }
    }

    /// <summary>
    /// Gửi message đến một client
    /// </summary>
    private async Task SendMessageAsync(NetworkStream? stream, string message)
    {
        if (stream == null || !stream.CanWrite) return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }
        catch { }
    }

    /// <summary>
    /// Gửi message đến cả 2 client
    /// </summary>
    private async Task BroadcastAsync(string message)
    {
        if (_player1Connected) await SendMessageAsync(_stream1, message);
        if (_player2Connected) await SendMessageAsync(_stream2, message);
    }

    /// <summary>
    /// Dừng server
    /// </summary>
    public async Task StopAsync()
    {
        _isRunning = false;
        _serverActive = false;
        CleanupConnections();
        _listener?.Stop();
        Console.WriteLine("Server đã dừng.");
        await Task.CompletedTask;
    }
}
