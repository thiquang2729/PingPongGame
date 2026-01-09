namespace Client;

/// <summary>
/// Quản lý hiển thị giao diện Console cho game Pong
/// Sử dụng kỹ thuật anti-flicker
/// </summary>
public class Display
{
    private readonly int _width;
    private readonly int _height;

    // Vị trí cũ để xóa
    private int _oldBallX;
    private int _oldBallY;
    private int _oldPaddle1Y;
    private int _oldPaddle2Y;

    private bool _isInitialized;

    public Display(int width = 80, int height = 24)
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Khởi tạo màn hình Console
    /// </summary>
    public void Initialize()
    {
        Console.Title = "Pong Client";
        Console.CursorVisible = false;
        Console.Clear();

        try
        {
            Console.SetWindowSize(_width, _height + 2);
            Console.SetBufferSize(_width, _height + 2);
        }
        catch
        {
            // Bỏ qua nếu không set được size (ví dụ trên Linux)
        }

        DrawBorder();
        _isInitialized = true;
    }

    /// <summary>
    /// Vẽ viền bàn chơi
    /// </summary>
    private void DrawBorder()
    {
        Console.ForegroundColor = ConsoleColor.White;

        // Viền trên
        Console.SetCursorPosition(0, 0);
        Console.Write("╔" + new string('═', _width - 2) + "╗");

        // Viền dưới
        Console.SetCursorPosition(0, _height - 1);
        Console.Write("╚" + new string('═', _width - 2) + "╝");

        // Viền trái và phải
        for (int y = 1; y < _height - 1; y++)
        {
            Console.SetCursorPosition(0, y);
            Console.Write("║");
            Console.SetCursorPosition(_width - 1, y);
            Console.Write("║");
        }

        // Đường kẻ giữa (nét đứt)
        int midX = _width / 2;
        for (int y = 1; y < _height - 1; y++)
        {
            Console.SetCursorPosition(midX, y);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(y % 2 == 0 ? "│" : " ");
        }

        Console.ResetColor();
    }

    /// <summary>
    /// Hiển thị thông báo chờ
    /// </summary>
    public void ShowWaiting()
    {
        string msg = "Đang chờ người chơi khác...";
        Console.SetCursorPosition((_width - msg.Length) / 2, _height / 2);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(msg);
        Console.ResetColor();
    }

    /// <summary>
    /// Hiển thị vai trò người chơi
    /// </summary>
    public void ShowPlayerRole(int playerId)
    {
        string side = playerId == 1 ? "TRÁI" : "PHẢI";
        string msg = $"Bạn là Player {playerId} (Bên {side})";
        Console.SetCursorPosition((_width - msg.Length) / 2, _height / 2 + 2);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(msg);
        Console.ResetColor();
    }

    /// <summary>
    /// Cập nhật và vẽ lại trạng thái game
    /// </summary>
    public void Update(int ballX, int ballY, int paddle1Y, int paddle2Y, int score1, int score2, int ping)
    {
        if (!_isInitialized) return;

        // Xóa vị trí cũ
        ClearOldPositions();

        // Vẽ vị trí mới
        DrawBall(ballX, ballY);
        DrawPaddle(1, paddle1Y);
        DrawPaddle(_width - 2, paddle2Y);
        DrawScore(score1, score2, ping);

        // Lưu vị trí hiện tại làm vị trí cũ cho lần sau
        _oldBallX = ballX;
        _oldBallY = ballY;
        _oldPaddle1Y = paddle1Y;
        _oldPaddle2Y = paddle2Y;
    }

    /// <summary>
    /// Xóa các đối tượng ở vị trí cũ
    /// </summary>
    private void ClearOldPositions()
    {
        // Xóa bóng cũ
        if (_oldBallX > 0 && _oldBallY > 0)
        {
            Console.SetCursorPosition(_oldBallX, _oldBallY);
            Console.Write(" ");
        }

        // Xóa vợt 1 cũ
        for (int i = 0; i < 5; i++)
        {
            int y = _oldPaddle1Y + i;
            if (y > 0 && y < _height - 1)
            {
                Console.SetCursorPosition(1, y);
                Console.Write(" ");
            }
        }

        // Xóa vợt 2 cũ
        for (int i = 0; i < 5; i++)
        {
            int y = _oldPaddle2Y + i;
            if (y > 0 && y < _height - 1)
            {
                Console.SetCursorPosition(_width - 2, y);
                Console.Write(" ");
            }
        }
    }

    /// <summary>
    /// Vẽ bóng
    /// </summary>
    private void DrawBall(int x, int y)
    {
        if (x > 0 && x < _width - 1 && y > 0 && y < _height - 1)
        {
            Console.SetCursorPosition(x, y);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("O");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Vẽ vợt
    /// </summary>
    private void DrawPaddle(int x, int y)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        for (int i = 0; i < 5; i++)
        {
            int posY = y + i;
            if (posY > 0 && posY < _height - 1)
            {
                Console.SetCursorPosition(x, posY);
                Console.Write("█");
            }
        }
        Console.ResetColor();
    }

    /// <summary>
    /// Hiển thị điểm số và ping
    /// </summary>
    private void DrawScore(int score1, int score2, int ping)
    {
        string scoreText = $"  Player 1: {score1}  |  Player 2: {score2}  |  Ping: {ping}ms  ";
        Console.SetCursorPosition((_width - scoreText.Length) / 2, _height);
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(scoreText);
        Console.ResetColor();
    }

    /// <summary>
    /// Hiển thị kết quả game
    /// </summary>
    public void ShowGameOver(int winner, int myPlayerId)
    {
        Console.Clear();
        DrawBorder();

        string result = winner == myPlayerId ? "BẠN THẮNG!" : "BẠN THUA!";
        ConsoleColor color = winner == myPlayerId ? ConsoleColor.Green : ConsoleColor.Red;

        Console.SetCursorPosition((_width - result.Length) / 2, _height / 2);
        Console.ForegroundColor = color;
        Console.Write(result);

        string msg = "Nhấn Enter để thoát...";
        Console.SetCursorPosition((_width - msg.Length) / 2, _height / 2 + 2);
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(msg);
        Console.ResetColor();
    }

    /// <summary>
    /// Hiển thị thông báo mất kết nối
    /// </summary>
    public void ShowDisconnected()
    {
        string msg = "Đối thủ đã ngắt kết nối!";
        Console.SetCursorPosition((_width - msg.Length) / 2, _height / 2);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(msg);
        Console.ResetColor();
    }
}
