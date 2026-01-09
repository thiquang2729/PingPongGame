# **TÀI LIỆU THIẾT KẾ: GAME PONG CLASSIC (CONSOLE & SOCKET)**

## **1\. Tổng quan dự án**

* **Tên dự án:** Console Network Pong  
* **Ngôn ngữ:** C\# (.NET Core hoặc .NET Framework)  .net10
* **Giao diện:** Console Application  
* **Mô hình mạng:** Client \- Server (Multi-client)  
* **Giao thức:** TCP/IP thông qua thư viện System.Net.Sockets  
* **Số lượng người chơi:** 2 người (Player 1 vs Player 2\)

## **2\. Kiến trúc hệ thống**

Hệ thống hoạt động theo mô hình **Authoritative Server** (Server toàn quyền). Server sẽ chịu trách nhiệm tính toán vật lý, vị trí bóng, điểm số và gửi trạng thái về cho các Client. Client chỉ đóng vai trò là thiết bị hiển thị và gửi tín hiệu điều khiển.

### **2.1. Server (Máy chủ)**

* Lắng nghe kết nối tại một Port cố định.  
* Chấp nhận tối đa 2 kết nối đồng thời.  
* **Game Loop:** Chạy một vòng lặp vô hạn để tính toán vị trí bóng và kiểm tra va chạm.  
* **Broadcasting:** Gửi dữ liệu cập nhật vị trí mới nhất cho cả 2 Client sau mỗi khung hình tính toán.

### **2.2. Client (Máy khách)**

* Kết nối tới IP và Port của Server.  
* **Luồng Gửi (Sender):** Lắng nghe sự kiện bàn phím (Mũi tên lên/xuống) và gửi yêu cầu di chuyển về Server.  
* **Luồng Nhận (Receiver):** Lắng nghe dữ liệu từ Server, giải mã gói tin và vẽ lại bàn cờ, bóng, vợt lên màn hình Console.

## **3\. Thiết kế Giao thức Truyền thông (Protocol)**

Để Server và Client hiểu nhau, cần quy định cấu trúc gói tin. Vì đây là Console Game đơn giản, ta có thể sử dụng chuỗi ký tự (String) hoặc mảng byte (Byte Array). Dưới đây là thiết kế dạng chuỗi để dễ xử lý (có thể tối ưu bằng byte sau này).

### **3.1. Cấu trúc gói tin**

Dữ liệu gửi đi sẽ có dạng: \[COMMAND\_ID\]|\[DATA\]

### **3.2. Các lệnh từ Client gửi lên Server**

| Lệnh | Ý nghĩa | Tham số | Ví dụ |
| :---- | :---- | :---- | :---- |
| MOVE | Yêu cầu di chuyển vợt | Hướng (UP/DOWN) | \`MOVE |
| QUIT | Ngắt kết nối | Không | \`QUIT |

### **3.3. Các lệnh từ Server gửi về Client**

| Lệnh | Ý nghĩa | Dữ liệu kèm theo |
| :---- | :---- | :---- |
| ID | Xác nhận kết nối và chỉ định vai trò | 1 (Player 1 \- Trái) hoặc 2 (Player 2 \- Phải) |
| WAIT | Thông báo chờ người chơi thứ 2 | Không |
| START | Bắt đầu game | Thông tin kích thước bàn cờ |
| UPDATE | Cập nhật trạng thái game | BallX,BallY,P1\_Y,P2\_Y,Score1,Score2 |
| OVER | Kết thúc game | Người thắng cuộc |

## **4\. Thiết kế Chi tiết Server**

### **4.1. Quản lý kết nối**

Server cần một danh sách hoặc 2 biến riêng biệt để lưu trữ Socket của 2 người chơi: ClientSocket1 và ClientSocket2.

* Khi Client 1 kết nối: Gửi lệnh ID|1, trạng thái chuyển sang "Waiting".  
* Khi Client 2 kết nối: Gửi lệnh ID|2, gửi lệnh START cho cả hai, trạng thái chuyển sang "Playing".

### **4.2. Logic Vật lý (Physics Engine)**

Server sẽ duy trì các biến trạng thái:

* BallPosition (X, Y)  
* BallVelocity (DX, DY) \- Hướng di chuyển  
* Paddle1Position (Y)  
* Paddle2Position (Y)  
* BoardSize (Width, Height)

**Quy tắc va chạm:**

1. **Chạm biên trên/dưới:** Đảo ngược DY (DY \= \-DY).  
2. **Chạm vợt:** Kiểm tra tọa độ bóng có nằm trong vùng phủ của vợt không. Nếu có, đảo ngược DX (DX \= \-DX).  
3. **Ra ngoài biên trái/phải:**  
   * Nếu bóng ra biên trái: Player 2 ghi điểm. Reset bóng về giữa.  
   * Nếu bóng ra biên phải: Player 1 ghi điểm. Reset bóng về giữa.

### **4.3. Tốc độ khung hình (Tick Rate)**

Server không thể gửi dữ liệu liên tục vì sẽ gây nghẽn mạng. Cần sử dụng Thread.Sleep để giới hạn tốc độ cập nhật.

* Khuyến nghị: 20 \- 30 lần cập nhật/giây (khoảng 30ms \- 50ms mỗi vòng lặp).

## **5\. Thiết kế Chi tiết Client**

### **5.1. Kỹ thuật hiển thị Console (Rendering)**

Vấn đề lớn nhất của Console là hiện tượng nhấp nháy (flickering) khi dùng lệnh xóa màn hình Console.Clear().

Giải pháp:  
Không xóa toàn bộ màn hình. Chỉ xóa và vẽ lại những đối tượng di chuyển (Bóng, Vợt).

* **Bước 1:** Di chuyển con trỏ đến vị trí cũ của bóng \-\> Ghi đè bằng ký tự khoảng trắng (xóa).  
* **Bước 2:** Di chuyển con trỏ đến vị trí mới của bóng \-\> Ghi ký tự O (vẽ).  
* Thực hiện tương tự cho hai thanh vợt.  
* Sử dụng Console.SetCursorPosition(x, y) để thực hiện việc này.  
* Ẩn con trỏ chuột mặc định để giao diện đẹp hơn (Console.CursorVisible \= false).

### **5.2. Đa luồng (Multi-threading)**

Client cần chạy ít nhất 2 luồng song song:

1. **Main Thread (Input):**  
   * Dùng Console.ReadKey(true) để bắt phím mũi tên.  
   * Nếu phím Lên \-\> Gửi MOVE|UP.  
   * Nếu phím Xuống \-\> Gửi MOVE|DOWN.  
2. **Network Thread (Listener):**  
   * Vòng lặp vô hạn đọc dữ liệu từ NetworkStream.  
   * Phân tích gói tin UPDATE.  
   * Gọi hàm vẽ lại màn hình dựa trên tọa độ mới nhận được.

## **6\. Luồng đi dữ liệu (Data Flow) ví dụ**

1. **Khởi tạo:** Server chạy, chờ kết nối.  
2. **Kết nối:** Client A vào \-\> Server gán P1. Client B vào \-\> Server gán P2 \-\> Game bắt đầu.  
3. **Input:** Client A ấn nút "Lên". Gửi MOVE|UP đến Server.  
4. **Xử lý:** Server nhận lệnh, cập nhật Paddle1\_Y \= Paddle1\_Y \- 1\.  
5. **Tính toán:** Server tính vị trí bóng mới (cộng vận tốc vào tọa độ). Kiểm tra va chạm.  
6. **Phản hồi:** Server tạo chuỗi: UPDATE|40,12,5,10,0,0 (Bóng tại 40,12; Vợt 1 tại 5; Vợt 2 tại 10; Tỉ số 0-0).  
7. **Hiển thị:** Cả Client A và B nhận chuỗi này, xóa vị trí cũ, vẽ vị trí mới.

## **7\. Các trường hợp ngoại lệ cần xử lý**

* **Client ngắt kết nối đột ngột:** Server tạm dừng và chờ  cần bắt SocketException, thông báo cho người chơi còn lại muốn chờ để người 2 kết nối lại để tiếp tục ván game hay thắng cuộc và reset game về trạng thái chờ.   
* **Độ trễ mạng (Lag):** Vì là authoritative server, Client có thể cảm thấy độ trễ giữa lúc ấn phím và lúc vợt di chuyển. Trong môi trường mạng LAN (localhost), điều này không đáng kể.  
* **Đồng bộ màn hình:** Kích thước cửa sổ Console của 2 Client phải giống nhau (ví dụ: 80x25) để tránh vỡ giao diện. Nên thiết lập cứng kích thước này ngay khi khởi động Client.  
  có hiển thị ping ms để 2 người chơi biết 

## **8\. Cấu trúc thư mục mã nguồn đề xuất**

Solution PongGame  
|  
|-- Project Server  
|   |-- Program.cs (Main entry)  
|   |-- GameServer.cs (Quản lý socket)  
|   |-- GameState.cs (Logic vật lý, điểm số)  
|  
|-- Project Client  
    |-- Program.cs (Main entry & Loop gửi phím)  
    |-- Display.cs (Các hàm vẽ Console)  
    |-- NetworkClient.cs (Nhận dữ liệu)  
