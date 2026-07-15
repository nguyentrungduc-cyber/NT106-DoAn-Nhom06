<div align="center">

# 🔒 SecureChat

### Ứng dụng nhắn tin mã hoá đầu-cuối kiểu Telegram — Đồ án NT106 (Nhóm 06)

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WinForms](https://img.shields.io/badge/WinForms-Desktop%20Client-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-Realtime-1E90FF?style=for-the-badge)
![MySQL](https://img.shields.io/badge/MySQL-Database-4479A1?style=for-the-badge&logo=mysql&logoColor=white)
![E2EE](https://img.shields.io/badge/E2EE-AES--256%20%2B%20RSA--2048-critical?style=for-the-badge)

*Đồ án môn NT106 — Lập trình mạng căn bản, Nhóm 06, University of Information Technology (VNU-HCM).*

</div>

---

## 📌 Giới thiệu

**SecureChat** là ứng dụng chat desktop lấy cảm hứng từ Telegram, gồm **Client WinForms** + **Server ASP.NET Core** giao tiếp qua **SignalR** (realtime) và **REST API**, tập trung vào **bảo mật đầu-cuối**:

- 🔐 Mã hoá tin nhắn **AES-256-CBC** (nội dung) kết hợp **RSA-2048** (trao đổi khoá) — chuẩn End-to-End Encryption.
- 🔑 Băm mật khẩu bằng **Argon2id** (chống brute-force/rainbow table).
- 🎫 Xác thực qua **JWT**, có hỗ trợ **2FA (OTP qua email, gửi bằng SendGrid)**.
- 💬 Nhắn tin realtime (SignalR Hub), gọi thoại/video (OpenCvSharp cho webcam, NAudio cho âm thanh).
- 👥 Chat nhóm, Friends List, Forward/Delete/Pin tin nhắn, gửi file (mã hoá khi upload/download).

---

## 🏗️ Kiến trúc

```
 SecureChat.Client (WinForms, .NET 8)
        │  SignalR (realtime)  +  REST API (JWT)
        ▼
 SecureChat.Server (ASP.NET Core, .NET 8)
        │
        ├── Hubs/ChatHub          → endpoint: /hubs/chat
        ├── Controllers/          → REST API (auth, users, groups, messages...)
        ├── Repositories/         → Entity Framework Core (Pomelo MySQL)
        └── Services/             → Business logic, mã hoá, gửi mail OTP (SendGrid)
        ▼
 MySQL Database
```

`SecureChat.Shared` chứa các Model/DTO và logic mã hoá dùng chung giữa Client và Server, đảm bảo đồng nhất định dạng dữ liệu và thuật toán mã hoá.

---

## ⚙️ Yêu cầu môi trường

- **Windows** + **Visual Studio 2022** (bản mới, hỗ trợ .NET 8) — Client là WinForms nên chỉ build/chạy được trên Windows.
- **.NET SDK 8.0**
- **MySQL Server** (local hoặc cloud, VD: Aiven)
- Tài khoản **SendGrid** (để gửi email OTP cho 2FA) — không bắt buộc nếu chỉ test tính năng chat cơ bản.

---

## 🚀 Cài đặt và chạy dự án

### 1. Clone repository

```bash
git clone https://github.com/nguyentrungduc-cyber/NT106-DoAn-Nhom06.git
cd NT106-DoAn-Nhom06
```

### 2. Cấu hình Server

```bash
cd SecureChat.Server
cp appsettings.example.json appsettings.json
```

Mở `appsettings.json` vừa tạo, điền:
- Connection string MySQL (`ConnectionStrings`)
- JWT secret key
- Thông tin SendGrid API key (nếu dùng tính năng OTP)

> ⚠️ File `appsettings.json`/`appsettings.Development.json` đã được `.gitignore`, **không commit** thông tin nhạy cảm lên Git.

### 3. Chạy migration để tạo database

```bash
dotnet ef database update
```

### 4. Chạy Server

```bash
dotnet run
```

Server mặc định lắng nghe tại `http://0.0.0.0:<PORT>` (cấu hình qua biến môi trường `PORT`, mặc định dùng khi deploy trên Railway). SignalR Hub tại endpoint: **`/hubs/chat`**.

### 5. Chạy Client

Mở `SecureChatDesktop.sln` bằng Visual Studio → chọn `SecureChat.Client` làm Startup Project → F5 để build & chạy.

Trong màn hình đăng nhập/cấu hình, trỏ Client tới đúng địa chỉ Server (local: `http://localhost:<PORT>`, hoặc URL deploy trên Railway).

---

## ☁️ Deploy

Repo có sẵn cấu hình deploy **Server** lên **Railway** qua **Nixpacks** (`nixpacks.toml`, `railway.json`) — tự động cài .NET 8 SDK, restore, build, và chạy `SecureChat.Server` dưới dạng Release build khi push lên Railway.

---

## 🌿 Nhánh phát triển

Dự án dùng quy trình nhiều nhánh song song theo tính năng (`feature/...`, `fix/...`) trước khi merge vào `dev` rồi `main`. Một số nhánh tiêu biểu:

| Loại nhánh | Ví dụ | Mục đích |
| :--- | :--- | :--- |
| `feature/*` | `feature/signalr-hub`, `feature/self-destruct-messages`, `feature/message-recall` | Phát triển tính năng mới độc lập |
| `fix/*` | `fix/chat-scroll-performance`, `fix/db-schema-constraints` | Sửa lỗi cụ thể |
| `Duck_*` | `Duck_Test_UI_Dung`, `Duck_LoginRegister` | Nhánh cá nhân (Nguyễn Trung Đức) |
| `dev` | — | Nhánh tích hợp trước khi lên `main` |

---

<div align="center">

*Đồ án học phần NT106 — Nhóm 06*

</div>
