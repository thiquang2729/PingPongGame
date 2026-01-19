# 🏓 Console Network Pong Game

Game Pong cổ điển chạy trên Console với khả năng chơi **2 người qua mạng** sử dụng TCP/IP Socket.

![C#](https://img.shields.io/badge/C%23-.NET%20Core-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-green)
![Players](https://img.shields.io/badge/Players-2-orange)

---

## 📋 1. Tính năng

| Tính năng | Mô tả |
|-----------|-------|
| 🎮 **Multiplayer** | Chơi 2 người qua mạng LAN hoặc Internet |
| 🔄 **Ready System** | Cả 2 người phải ấn Ready trước khi bắt đầu |
| 🔌 **Reconnect** | Tự động chờ 30 giây nếu đối thủ mất kết nối |
| 📊 **Ping Display** | Hiển thị độ trễ mạng real-time |
| 🖥️ **Anti-Flicker** | Kỹ thuật vẽ Console không nhấp nháy |
| ♻️ **Multi-Round** | Chơi nhiều ván liên tiếp |

---

## 🚀 2. Cách sử dụng

### Yêu cầu
- .NET 6.0 trở lên

### Build
```powershell
dotnet build PongGame.slnx
```

### Chạy trên cùng 1 máy (localhost)

```powershell
# Terminal 1 - Khởi động Server
dotnet run --project Server

# Terminal 2 - Client Player 1
dotnet run --project Client

# Terminal 3 - Client Player 2
dotnet run --project Client
```

### Chạy trên 2 máy khác nhau (LAN)

**Máy 1 (Server):**
```powershell
# Xem IP của máy
ipconfig

# Mở port firewall (chạy với Admin)
netsh advfirewall firewall add rule name="Pong Server" dir=in action=allow protocol=TCP localport=5000

# Chạy Server
dotnet run --project Server
```

**Máy 2 (Client):**
```powershell
# Thay IP_SERVER bằng IP thật của máy Server
dotnet run --project Client -- 192.168.1.xxx 5000
```

---

## 🎮 3. Điều khiển

| Phím | Chức năng |
|------|-----------|
| `↑` hoặc `W` | Di chuyển vợt lên |
| `↓` hoặc `S` | Di chuyển vợt xuống |
| `Enter` / `Space` | Sẵn sàng (trong phòng chờ) |
| `ESC` | Thoát game |

---

## 📁 4. Cấu trúc Project

```
PongGame/
├── PongGame.slnx           # Solution file
│
├── Server/
│   ├── Program.cs          # Entry point Server
│   ├── GameServer.cs       # Quản lý kết nối TCP, game loop
│   └── GameState.cs        # Logic vật lý, va chạm, điểm số
│
└── Client/
    ├── Program.cs          # Entry point, xử lý input
    ├── NetworkClient.cs    # Kết nối TCP, gửi/nhận message
    └── Display.cs          # Vẽ giao diện Console
```

---

## 📡 5. Giao thức truyền thông

### Client → Server
| Lệnh | Mô tả |
|------|-------|
| `READY` | Xác nhận sẵn sàng |
| `MOVE\|UP` | Di chuyển vợt lên |
| `MOVE\|DOWN` | Di chuyển vợt xuống |
| `QUIT` | Thoát game |

### Server → Client
| Lệnh | Mô tả |
|------|-------|
| `ID\|1` hoặc `ID\|2` | Gán vai trò Player |
| `WAIT` | Chờ người chơi khác |
| `ROOM\|width,height` | Vào phòng chờ |
| `READY_STATUS\|p1,p2` | Trạng thái Ready |
| `START\|width,height` | Bắt đầu game |
| `UPDATE\|ballX,ballY,p1Y,p2Y,score1,score2` | Cập nhật game |
| `OVER\|winner` | Kết thúc game |
| `OPPONENT_DISCONNECTED\|player` | Đối thủ mất kết nối |
| `OPPONENT_RECONNECTED` | Đối thủ kết nối lại |

---

## 🔧 6. Cấu hình

### Thay đổi Port Server
```powershell
dotnet run --project Server -- 8080
```

### Thay đổi điểm thắng
Sửa file `Server/GameState.cs`:
```csharp
public int WinningScore { get; } = 5; // Mặc định 5 điểm
```

---

## ⚠️ 7. Xử lý lỗi thường gặp

| Lỗi | Giải pháp |
|-----|-----------|
| Không kết nối được từ máy khác | Mở port 5000 trong Windows Firewall |
| Game bị lag | Kiểm tra kết nối mạng, ping cao |
| Client không hiển thị game | Đảm bảo Console đủ lớn (80x25) |

---

## 📝 8. License

MIT License - Tự do sử dụng và chỉnh sửa.
