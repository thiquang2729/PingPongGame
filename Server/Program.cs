namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "Pong Server";
        Console.CursorVisible = false;

        int port = 5000;

        // Cho phép chỉ định port qua command line
        if (args.Length > 0 && int.TryParse(args[0], out int customPort))
        {
            port = customPort;
        }

        var server = new GameServer(port);

        // Xử lý Ctrl+C để tắt server an toàn
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nĐang tắt server...");
            await server.StopAsync();
            Environment.Exit(0);
        };

        try
        {
            await server.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi: {ex.Message}");
        }

        Console.WriteLine("\nNhấn Enter để thoát...");
        Console.ReadLine();
    }
}
