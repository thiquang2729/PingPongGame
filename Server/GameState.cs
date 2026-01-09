namespace Server;

/// <summary>
/// Quản lý trạng thái game: vị trí bóng, vợt, điểm số và logic vật lý
/// </summary>
public class GameState
{
    // Kích thước bàn chơi
    public int BoardWidth { get; } = 80;
    public int BoardHeight { get; } = 24;

    // Vị trí bóng
    public int BallX { get; private set; }
    public int BallY { get; private set; }

    // Vận tốc bóng (hướng di chuyển)
    public int BallDX { get; private set; }
    public int BallDY { get; private set; }

    // Vị trí vợt (Y coordinate)
    public int Paddle1Y { get; private set; }
    public int Paddle2Y { get; private set; }

    // Kích thước vợt
    public int PaddleHeight { get; } = 5;

    // Điểm số
    public int Score1 { get; private set; }
    public int Score2 { get; private set; }

    // Điểm thắng
    public int WinningScore { get; } = 5;

    // Trạng thái game
    public bool IsGameOver => Score1 >= WinningScore || Score2 >= WinningScore;
    public int Winner => Score1 >= WinningScore ? 1 : (Score2 >= WinningScore ? 2 : 0);

    public GameState()
    {
        ResetGame();
    }

    /// <summary>
    /// Reset toàn bộ game về trạng thái ban đầu
    /// </summary>
    public void ResetGame()
    {
        Score1 = 0;
        Score2 = 0;
        ResetBall();
        ResetPaddles();
    }

    /// <summary>
    /// Reset bóng về giữa màn hình
    /// </summary>
    public void ResetBall()
    {
        BallX = BoardWidth / 2;
        BallY = BoardHeight / 2;

        // Random hướng bóng ban đầu
        Random rand = new Random();
        BallDX = rand.Next(0, 2) == 0 ? -1 : 1;
        BallDY = rand.Next(0, 2) == 0 ? -1 : 1;
    }

    /// <summary>
    /// Reset vợt về giữa màn hình
    /// </summary>
    private void ResetPaddles()
    {
        Paddle1Y = (BoardHeight - PaddleHeight) / 2;
        Paddle2Y = (BoardHeight - PaddleHeight) / 2;
    }

    /// <summary>
    /// Di chuyển vợt của người chơi
    /// </summary>
    /// <param name="playerId">1 hoặc 2</param>
    /// <param name="direction">UP hoặc DOWN</param>
    public void MovePaddle(int playerId, string direction)
    {
        if (playerId == 1)
        {
            if (direction == "UP" && Paddle1Y > 1)
                Paddle1Y--;
            else if (direction == "DOWN" && Paddle1Y < BoardHeight - PaddleHeight - 1)
                Paddle1Y++;
        }
        else if (playerId == 2)
        {
            if (direction == "UP" && Paddle2Y > 1)
                Paddle2Y--;
            else if (direction == "DOWN" && Paddle2Y < BoardHeight - PaddleHeight - 1)
                Paddle2Y++;
        }
    }

    /// <summary>
    /// Cập nhật vị trí bóng và xử lý va chạm - gọi mỗi tick
    /// </summary>
    /// <returns>True nếu có ghi điểm</returns>
    public bool Update()
    {
        if (IsGameOver) return false;

        // Di chuyển bóng
        BallX += BallDX;
        BallY += BallDY;

        // Va chạm biên trên/dưới
        if (BallY <= 1 || BallY >= BoardHeight - 2)
        {
            BallDY = -BallDY;
            BallY = Math.Clamp(BallY, 1, BoardHeight - 2);
        }

        // Va chạm vợt trái (Player 1)
        if (BallX == 2 && BallY >= Paddle1Y && BallY < Paddle1Y + PaddleHeight)
        {
            BallDX = -BallDX;
            BallX = 3; // Đẩy bóng ra khỏi vợt
        }

        // Va chạm vợt phải (Player 2)
        if (BallX == BoardWidth - 3 && BallY >= Paddle2Y && BallY < Paddle2Y + PaddleHeight)
        {
            BallDX = -BallDX;
            BallX = BoardWidth - 4; // Đẩy bóng ra khỏi vợt
        }

        // Bóng ra biên trái - Player 2 ghi điểm
        if (BallX <= 0)
        {
            Score2++;
            ResetBall();
            return true;
        }

        // Bóng ra biên phải - Player 1 ghi điểm
        if (BallX >= BoardWidth - 1)
        {
            Score1++;
            ResetBall();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tạo chuỗi UPDATE để gửi cho Client
    /// </summary>
    public string GetUpdateString()
    {
        return $"UPDATE|{BallX},{BallY},{Paddle1Y},{Paddle2Y},{Score1},{Score2}";
    }
}
