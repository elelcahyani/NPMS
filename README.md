# NPMS — New Product Management System

Aplikasi desktop WPF (.NET 10) untuk manajemen data produk manufaktur. Menggunakan SQLite sebagai database lokal yang di-generate otomatis saat pertama kali dijalankan.

---

## Struktur Folder

```
NPMS/
├── NPMS.Core/                  # Class library (domain layer)
│   ├── Data/
│   │   └── NpmsDbContext.cs    # EF Core DbContext + seed data
│   ├── DTOs/
│   │   └── ProductDtos.cs      # Data Transfer Objects
│   ├── Models/
│   │   ├── Entities.cs         # Entity / model classes
│   │   ├── ProductStatus.cs    # Enum status produk
│   │   └── UserRole.cs         # Enum role pengguna
│   ├── Services/
│   │   └── ProductService.cs   # Business logic & auth
│   └── NPMS.Core.csproj
│
├── NPMS.Desktop/               # WPF application (presentation layer)
│   ├── images/                 # Gambar produk default (disertakan)
│   ├── App.xaml / App.xaml.cs          # Entry point, inisialisasi DB
│   ├── LoginWindow.xaml/.cs            # Halaman login
│   ├── MainWindow.xaml/.cs             # Dashboard utama
│   ├── AccountSettingsWindow.xaml/.cs  # Pengaturan akun
│   ├── ChangePasswordWindow.xaml/.cs   # Ganti password
│   ├── AddUserDialog.xaml/.cs          # Tambah user (SuperAdmin)
│   └── NPMS.Desktop.csproj
│
└── README.md
```

---

## Konfigurasi Keamanan

Tidak ada akun bawaan yang dibundel ke dalam repo. Atur kredensial admin melalui variabel lingkungan sebelum menjalankan aplikasi:

```bash
set NPMS_API_KEY=replace-with-strong-api-key
set NPMS_ADMIN_USERNAME=replace-with-admin-username
set NPMS_ADMIN_PASSWORD=replace-with-strong-password
```

Pada Linux/macOS:

```bash
export NPMS_API_KEY=replace-with-strong-api-key
export NPMS_ADMIN_USERNAME=replace-with-admin-username
export NPMS_ADMIN_PASSWORD=replace-with-strong-password
```

> Aplikasi menolak semua request API sampai variabel `NPMS_API_KEY` diatur.

---

## Cara Clone dan Menjalankan

### Prasyarat

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows (aplikasi WPF)
- Git

### Langkah-langkah

**1. Clone repository**

```bash
git clone https://github.com/elelcahyani/NPMS.git
cd NPMS
```

**2. Restore dependencies**

```bash
dotnet restore
```

**3. Build project**

```bash
dotnet build
```

**4. Jalankan aplikasi**

```bash
dotnet run --project NPMS.Desktop
```

Atau buka `NPMS.Desktop.csproj` langsung di Visual Studio 2022+ dan tekan **F5**.

**5. Login**

Database SQLite (`npms_desktop.db`) akan otomatis dibuat di folder output (`bin/Debug/net10.0-windows/`) beserta seed data. Tidak ada akun default; gunakan kredensial yang dikonfigurasi melalui variabel lingkungan.

---

## Catatan

- File database (`.db`) dan folder `Storage/` (dokumen upload) tidak di-commit ke repo karena di-exclude via `.gitignore`. Keduanya dibuat otomatis saat runtime.
- Tidak ada konfigurasi koneksi eksternal yang dibutuhkan — semua data tersimpan lokal.
