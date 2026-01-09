using System.Net.Sockets;
using System.Text;

namespace Client;

/// <summary>
/// Quản lý kết nối mạng với Server
/// </summary>
public class NetworkClient
{
    private readonly string _serverIp;
    private readonly int _serverPort;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _isConnected;

    public int PlayerId { get; private set; }
    public int Ping { get; private set; }

    // Events
    public event Action<int>? OnPlayerIdReceived;
    public event Action? OnWaiting;
    public event Action<int, int>? OnGameStart;
    public event Action<int, int, int, int, int, int>? OnGameUpdate;
    public event Action<int>? OnGameOver;
    public event Action<int>? OnOpponentDisconnected;

    private DateTime _lastMessageTime;

    public NetworkClient(string serverIp = "127.0.0.1", int serverPort = 5000)
    {
        _serverIp = serverIp;
        _serverPort = serverPort;
    }

    /// <summary>
    /// Kết nối đến server
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_serverIp, _serverPort);
            _stream = _client.GetStream();
            _isConnected = true;
            _lastMessageTime = DateTime.Now;

            // Bắt đầu luồng nhận dữ liệu
            _ = Task.Run(ReceiveLoopAsync);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Không thể kết nối: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Vòng lặp nhận dữ liệu từ server
    /// </summary>
    private async Task ReceiveLoopAsync()
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (_isConnected && _stream != null)
            {
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                // Tính ping
                Ping = (int)(DateTime.Now - _lastMessageTime).TotalMilliseconds;
                _lastMessageTime = DateTime.Now;

                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Xử lý nhiều message trong 1 packet
                string[] messages = data.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (string message in messages)
                {
                    ProcessMessage(message.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi nhận dữ liệu: {ex.Message}");
        }

        _isConnected = false;
    }

    /// <summary>
    /// Xử lý message từ server
    /// </summary>
    private void ProcessMessage(string message)
    {
        string[] parts = message.Split('|');
        string command = parts[0];

        switch (command)
        {
            case "ID":
                if (parts.Length > 1 && int.TryParse(parts[1], out int id))
                {
                    PlayerId = id;
                    OnPlayerIdReceived?.Invoke(id);
                }
                break;

            case "WAIT":
                OnWaiting?.Invoke();
                break;

            case "START":
                if (parts.Length > 1)
                {
                    string[] size = parts[1].Split(',');
                    if (size.Length >= 2 &&
                        int.TryParse(size[0], out int width) &&
                        int.TryParse(size[1], out int height))
                    {
                        OnGameStart?.Invoke(width, height);
                    }
                }
                break;

            case "UPDATE":
                if (parts.Length > 1)
                {
                    string[] data = parts[1].Split(',');
                    if (data.Length >= 6 &&
                        int.TryParse(data[0], out int ballX) &&
                        int.TryParse(data[1], out int ballY) &&
                        int.TryParse(data[2], out int p1Y) &&
                        int.TryParse(data[3], out int p2Y) &&
                        int.TryParse(data[4], out int s1) &&
                        int.TryParse(data[5], out int s2))
                    {
                        OnGameUpdate?.Invoke(ballX, ballY, p1Y, p2Y, s1, s2);
                    }
                }
                break;

            case "OVER":
                if (parts.Length > 1 && int.TryParse(parts[1], out int winner))
                {
                    OnGameOver?.Invoke(winner);
                }
                break;

            case "DISCONNECT":
                if (parts.Length > 1 && int.TryParse(parts[1], out int disconnectedPlayer))
                {
                    OnOpponentDisconnected?.Invoke(disconnectedPlayer);
                }
                break;
        }
    }

    /// <summary>
    /// Gửi lệnh di chuyển vợt
    /// </summary>
    public async Task SendMoveAsync(string direction)
    {
        await SendMessageAsync($"MOVE|{direction}");
    }

    /// <summary>
    /// Gửi lệnh thoát
    /// </summary>
    public async Task SendQuitAsync()
    {
        await SendMessageAsync("QUIT");
    }

    /// <summary>
    /// Gửi message đến server
    /// </summary>
    private async Task SendMessageAsync(string message)
    {
        if (_stream == null || !_isConnected) return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi gửi message: {ex.Message}");
        }
    }

    /// <summary>
    /// Ngắt kết nối
    /// </summary>
    public void Disconnect()
    {
        _isConnected = false;
        _stream?.Close();
        _client?.Close();
    }
}
