# Deploy INFRA-CAP ke Windows Server 2022 (kantor)

**Target:** `http://10.89.6.237/INFRA-CAP`
**Server:** Windows Server 2022, IIS, URL Rewrite 2.1, ASP.NET Core Hosting Bundle, SQL Server 2022 SE
**Transfer:** flashdisk (build di rumah, copy ke server)

---

## 1. Build di mesin DEV

```cmd
deploy\build-release.cmd
```

Output:

```
dist\web\   -> static SPA   (salin ke C:\inetpub\INFRA-CAP)
dist\api\   -> ASP.NET Core (salin ke C:\inetpub\INFRA-CAP-api)
```

Penting: `NUXT_APP_BASE_URL=/INFRA-CAP/` di-set saat generate. Asset path
ter-bake ke dalam HTML, jadi **jangan** generate tanpa env var itu.

---

## 2. Setup IIS di server (sekali saja)

### 2a. Buat dua application di Default Web Site

1. IIS Manager -> Default Web Site -> **Add Application**
2. Application name: `INFRA-CAP`, Physical path: `C:\inetpub\INFRA-CAP`, OK
3. Ulangi -> Application name: `INFRA-CAP-api`, Physical path: `C:\inetpub\INFRA-CAP-api`

### 2b. Copy file

```cmd
xcopy D:\dist\web\*   C:\inetpub\INFRA-CAP\     /E /I /Q
xcopy D:\dist\api\*   C:\inetpub\INFRA-CAP-api\ /E /I /Q
```

### 2c. Application pool untuk API

Application pool `INFRA-CAP-api` harus **No Managed Code** (ASP.NET Core Module
menangani sendiri). Default sudah benar, tapi pastikan.

Pastikan ASP.NET Core Hosting Bundle terinstall di server (HIRO sudah bilang ada,
tetapi cek: `C:\Program Files\IIS` tidak boleh ada `aspnet_core_v2_inprocess.dll`
yg tertinggal dari versi lama).

---

## 3. Konfigurasi API (WAJIB, di server)

Edit `C:\inetpub\INFRA-CAP-api\appsettings.Production.json`:

| Key | Isi |
|---|---|
| `ConnectionStrings:Default` | server + kredensial SQL Server kantor |
| `Jwt:Key` | string random unik **min. 32 byte**, distinct dari dev |
| `Seed:AdminPassword` | password admin, **jangan** `Admin@123` |

Generate JWT key:
```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
```

### Cara edit connection string (SQL Auth)

Format WAJIB pakai **titik koma** (`;`) sebagai pemisah, **jangan koma**:

```
Server=localhost,1433;Database=InternalApp;User Id=infra_cap_app;Password=<PASSWORD_KANTOR>;TrustServerCertificate=True;Encrypt=False;Connect Timeout=30
```

Koma hanya boleh di dalam `Server=localhost,1433`. Kalau salah pakai koma, ASP.NET Core akan gagal parse dan start dengan error yang membingungkan.

### Buat akun SQL khusus (JANGAN pakai `sa`)

```sql
USE master;
CREATE LOGIN [infra_cap_app] WITH PASSWORD = 'PasswordYangKuat123!', CHECK_POLICY = ON;
USE InternalApp;
CREATE USER [infra_cap_app] FOR LOGIN [infra_cap_app];
-- app hanya butuh: baca metadata, CRUD data
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [infra_cap_app];
-- perlu CREATE untuk EF migration saat deploy pertama
GRANT CREATE ON SCHEMA::dbo TO [infra_cap_app];
```

Kalau tabel belum ada (deploy pertama), `GRANT` di atas harus dijalankan **sesudah** `InternalApp` dibuat. Urutan yang aman:
1. Deploy aplikasi sekali (EF akan membuat tabel) — sementara, connection string masih pakai `sa`
2. Jalankan script `CREATE USER` + `GRANT` di atas
3. Ganti connection string ke `infra_cap_app`
4. Restart app pool

### Offline: server tanpa internet

Server 10.89.6.237 tidak punya akses internet. Yang sudah dipastikan aman:

- **Ikon**: `nuxt generate` mem-bundle 56 ikon lucide ke dalam output (`Nuxt Icon client bundle consist of 56 icons`). Sudah diuji dengan seluruh CDN diblokir — semua ikon tetap tampil. Jangan hapus `@iconify-json/lucide` dari `package.json` dan jangan set `ui.icons` ke mode remote.
- **Font**: Nuxt UI memakai font system, bukan Google Fonts.
- **Tidak ada CDN/telemetry** di runtime.

Kalau nanti ada dependensi baru yang memanggil internet, uji dulu dengan cara yang sama: build, blokir `api.iconify.design` + `fonts.googleapis.com` di browser, lalu cek tampilan.

---

## 4. Pastikan Environment = Production

Application pool `INFRA-CAP-api` -> **Basic Settings** -> Environment = `Production`.

Kalau tidak, ASP.NET Core akan memuat `appsettings.Development.json` yang berisi
connection string ke **192.168.4.3** (server rumah). Itu akan gagal karena IP
tersebut tidak terjangkau dari kantor.

---

## 5. Verifikasi

| Cek | Cara |
|---|---|
| SPA | buka `http://10.89.6.237/INFRA-CAP` |
| Login | `admin` + password production |
| Refresh deep link | buka `/INFRA-CAP/data/customer` lalu F5 -> harus tetap 200 (bukan 404) |
| API | `http://10.89.6.237/INFRA-CAP-api/swagger` |
| DB | table `Users` terisi, `__EFMigrationsHistory` ada |

---

## 6. Update deployment berikutnya

Tidak perlu rebuild. Untuk ganti file statis:

```cmd
:: dari server, hentikan site dulu supaya tidak ada file lock
appcmd stop apppool /apppool.name:INFRA-CAP-api
xcopy D:\dist\web\* C:\inetpub\INFRA-CAP\ /E /I /Q /Y
appcmd start apppool /apppool.name:INFRA-CAP-api
iisreset
```

Database **tidak** perlu disentuh. Migration baru会自动 jalan saat API start.

---

## Catatan arsitektur

```
Browser
   |
   |  http://10.89.6.237  (satu origin, tidak ada CORS)
   |
   +-- /INFRA-CAP      -> IIS static files  (SPA, tanpa Node runtime)
   |
   +-- /INFRA-CAP-api  -> IIS -> ASP.NET Core Module -> Api.exe
                              |
                              +-- localhost / SQL Server 2022 SE
```

Kedua path ini same-origin, jadi JWT di `Authorization` header tanpa preflight.
Tidak butuh ARR, tidak butuh proxy, tidak butuh CORS di production.
