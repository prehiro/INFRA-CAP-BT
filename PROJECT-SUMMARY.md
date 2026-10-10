# INFRA-CAP — Ringkasan Project

> File ini adalah **backup** dari holographic memory. Kalau memory agent hilang/reset, baca file ini untuk melanjutkan.
> Terakhir diupdate: 2026-10-08

## 1. Konsep

Aplikasi web internal dengan **metadata-driven generic CRUD engine** (pola EAV).
User bisa membuat entitas baru (master data / transaksi) **lewat UI tanpa rewrite kode** —
satu set endpoint melayani semua tipe data.

## 2. Tech Stack

| Layer | Teknologi |
|---|---|
| Backend | ASP.NET Core 10 (`net10.0`), SDK 10.0.401 |
| ORM | EF Core 10 + SQL Server (EF migrations) |
| Auth | JWT HMAC-SHA256, password di-hash dengan BCrypt |
| Frontend | Nuxt 4.5.2 (`ssr: false` = SPA statis murni) + Nuxt UI 4.11.2 + Tailwind v4 |
| Deploy | IIS (2 application), tanpa Node.js runtime di server |

**Lokasi project:** `C:\Users\HIRO\Projects\InternalApp`

```
InternalApp\
  api\                 ASP.NET Core 10 Web API
    Domain\            Identity.cs (AppUser/AppRole/AppUserRole), Dynamic.cs (DynamicEntity/Field/Record/RecordValue)
    Data\              AppDbContext.cs + Migrations\
    Services\          TokenService, DynamicRecordService (inti CRUD engine), DynamicSchemaService, SeedService
    Controllers\       Auth, Users, Entities, Fields, Records
    test-e2e.ps1       15 test E2E (PowerShell)
  web\                 Nuxt 4 SPA
    app\               srcDir Nuxt 4: pages/, components/, composables/, layouts/, middleware/, types/
      pages\           login.vue, index.vue, users.vue, entities.vue, data/[slug].vue
      components\      DynamicFieldInput.vue
      composables\     useApi.ts, useAuth.ts
      assets\css\      main.css
    deploy\            web.config.spa
    nuxt.config.ts     ssr:false, baseURL /INFRA-CAP, offline icon bundling
  deploy\              build-release.cmd
  DEPLOY.md            panduan IIS lengkap
  PROJECT-SUMMARY.md   file ini (backup holographic memory)
```

> **Catatan offline:** dependency `@iconify-json/lucide` WAJIB ada di `package.json`.
> Kalau dihapus, ikon kembali diambil dari `api.iconify.design` saat runtime dan akan hilang
> di server kantor yang tanpa internet.

## 3. Model Data (EAV)

| Tabel | Isi |
|---|---|
| `DynamicEntity` | definisi entitas (Name, Slug, Kind=Master/Transaction, DisplayField) |
| `DynamicField` | definisi kolom (Type, Required, Unique, LookupEntityId, MaxLength) |
| `Record` | satu baris data (soft delete via `IsDeleted`) |
| `RecordValue` | nilai per field — kolom terpisah: `TextValue`/`NumberValue`/`DateValue`/`BoolValue`/`LookupValue` |

`FieldType`: Text, TextArea, Number, Decimal, Date, Boolean, **Lookup**, Email.
`RecordValue.NumberValue` → `HasPrecision(18,4)` (WAJIB untuk nilai uang; default SQL Server `decimal(18,2)` akan truncate diam-diam).

### Endpoint utama
```
POST   /api/auth/login              → JWT
GET    /api/auth/me
GET    /api/entities                → semua entitas + metadata field
POST   /api/entities                → buat entitas runtime (Admin)
GET    /api/records/{entityId}      → list + search + filter + paging
POST   /api/records/{entityId}      → create
PUT    /api/records/{entityId}/{id} → update
DELETE /api/records/{entityId}/{id} → soft delete
GET/POST/PUT/DELETE /api/users      → user management (role Admin)
```
Role: **Admin** (full), **Manager** (data, bukan user), **Staff** (transaksi).
Seed: `admin` / `Admin@123`, sample entity Customer / Product / Sales Order.

## 4. Environment

| | DEV (rumah) | PROD (kantor) |
|---|---|---|
| Client | PC 192.168.2.8 | — |
| DB | 192.168.4.3:1433 (**Express/Linux Ubuntu**, db `InternalApp_Dev`, sa) | SQL Server 2022 **SE lokal server** (db `InternalApp`) |
| API | `localhost:5099` | `10.89.6.237/INFRA-CAP-api` |
| Web | `localhost:3000` (Vite proxy `/api`) | `10.89.6.237/INFRA-CAP` |
| CORS | tidak perlu (proxy) | tidak perlu (**same origin**, 2 IIS app) |

> ⚠️ DB prod **tidak bisa** dijangkau dari 192.168.4.3 (IP private, jaringan terpisah) — itu sebabnya config dipisah.

## 5. Deploy (IIS)

Dua application di bawah Default Web Site → **same origin, nol CORS, tanpa ARR**:
- `/INFRA-CAP` → static SPA → `C:\inetpub\INFRA-CAP`
- `/INFRA-CAP-api` → ASP.NET Core → `C:\inetpub\INFRA-CAP-api`

Build: `deploy\build-release.cmd` → `dist\web` + `dist\api` (untuk flashdisk).
`web/deploy/web.config.spa` di-copy jadi `web.config` di root site SPA (rewrite `.*` → `index.html` untuk SPA fallback).

**Edit di server (3 wajib):** `appsettings.Production.json` → `ConnectionStrings:Default`, `Jwt:Key` (min 32 byte, unik), `Seed:AdminPassword`.
**WAJIB:** app pool `INFRA-CAP-api` → Environment = **Production** (selain itu load connection string dev yang tidak terjangkau).

## 6. Progress

### ✅ Selesai & terverifikasi (real output)
- [x] API build 0 error
- [x] EF migration `20260930135956_InitialCreate` applied ke `192.168.4.3/InternalApp_Dev`
- [x] **15/15 test E2E lulus** (`api/test-e2e.ps1`): login, CRUD, unique/required/lookup guards, search-pierces-lookup, 401 guard, runtime-entity-creation, paging, soft-delete
- [x] `nuxt generate` → 1.2M SPA statis, **nol artefak Node**, 4 route ter-prerender, `/INFRA-CAP/` ter-bake di asset path
- [x] **Offline-safe**: 56 ikon lucide ter-bundle lokal (`@iconify-json/lucide`) — diuji dengan semua CDN diblokir, ikon tetap tampil
- [x] Browser: login → dashboard (data live dari SQL Server)
- [x] Browser: `/data/customer` & `/data/sales_order` — **kolom & widget auto dari metadata**, lookup dropdown berisi record master, format angka id-ID
- [x] Browser: create-from-UI (customer, user, warehouse), search "Kabel" (=nama produk) menemukan SO
- [x] Browser: **EDIT** — prefill data lama, simpan, dikonfirmasi di DB (`updatedBy=admin`, `updatedAt` terisi)
- [x] Browser: **DELETE** — konfirmasi menyebut nama record, soft delete terverifikasi (hilang dari list, `GET` tunggal → 404)
- [x] **Exception handler global** — exception domain dulu membocorkan stack trace mentah ke browser; sekarang JSON bersih: 404 / 400+errors / 403 / 500 generic
- [x] **Entity Builder UI** (`/entities`) — buat/edit entity & field, 8 tipe data, soft-disable, delete, lookup self-reference dicegah
- [x] **Info audit UI** — toggle "Info audit" + badge per record. **VERIFIED di browser 2026-09-30**: badge `admin · 30 Sep 2026, 16.16` muncul di record yang sudah diedit, record yang belum diedit tetap `14.02`
- [x] **CCTV Log Book** (`/logbook/cctv`) — halaman khusus 12 kolom meniru form kertas "RECORDABLE MEDIA LOG BOOK", A4 landscape print. Data tetap di engine generic (entity `cctv_log_book`, 12 field). Backend kustom hanya `LogbookNumberService` + `CctvLogbookController` untuk nomor urut `1/2026/001` reset per tahun. Tanda tangan = canvas PNG (base64) di field Text tanpa MaxLength. **14/14 test di `api/test-logbook.ps1`**, alur UI lengkap diuji di browser (isi → tanda tangan → simpan → tampil → hapus)
- [x] **Tema dashboard** (2026-10-01) — sidebar `UDashboardSidebar` collapsible, command palette Ctrl+K, dark mode **sebagai default**, primary lime `#00C16A`, Public Sans. Ganti 17 warna aksen + 9 netral dan toggle terang/gelap **terverifikasi bertahan setelah refresh** (cookie `infra-cap.theme`, composable `useThemeChoice`). Offline-safe: Public Sans di-bundle dari `@fontsource`, bukan CDN
- [x] **Pembersihan dead code** (2026-10-02) — `app/pages/data/[slug].vue` (halaman CRUD generik metadata, tak terjangkau sejak Master Data/Transaksi dihapus) **dihapus**, beserta satu-satunya dependensinya `app/components/DynamicFieldInput.vue`, dan direktorinya. `apiGetRecord` di `composables/useApi.ts` ikut dibuang karena tak terpakai. Sisa halaman: `index.vue`, `login.vue`, `users.vue`, `logbook/cctv.vue`. Sisa komponen: `BrandMenu`, `PageHeader`, `SignaturePad`, `UserMenu`
- [x] **Lebar sidebar 240px → 208px** (2026-10-02) — lewat `:default-size="13" :min-size="11" :max-size="18"` pada `UDashboardSidebar`. Default vendor `defaultSize` = 15 dengan `UDashboardGroup unit="rem"` → 15rem = 240px. **Lebar collapsed tidak dikontrol prop ini** — itu dari `min-w-16` (64px) di theme sidebar root
- [x] **Welcome banner di body Dashboard** (2026-10-02) — komponen baru `web/app/components/WelcomeBanner.vue`, ditaruh di **body** halaman (bukan PageHeader — navbar terkunci 64px). Isi: kartu beraksen gradient (dua blob blur `aria-hidden`), avatar inisial (tanpa gambar remote — server kantor tanpa internet), sapaan sesuai waktu + nama lengkap + badge role, dan blok tanggal "Today" (`en-GB`). Greeting: <11 pagi, <15 siang, <18 sore, sisanya malam. **Waktu diambil dari ref `now` yang di-refresh tiap 60 detik, bukan `new Date()` langsung di template** — yang terakhir hanya ter-update bila ada re-render lain dan bisa basi diam-diam. `index.vue` perlu `useAuth()` lagi untuk ini. **Terverifikasi: nama + badge role di banner cocok persis dengan `GET /api/auth/me`** (Administrator / Admin), tidak ada yang di-hard-code; logika batas greeting diuji di jam 0/5/10/11/14/15/17/18/23. **Terverifikasi dengan DUA identitas**: admin → "Good night, Administrator" + badge Admin (cocok persis dengan `GET /api/auth/me`, tidak ada yang di-hard-code); operator01 (Staff) → "Good night, Operator Satu" + badge Staff, avatar "OS", sidebar "Operator Satu / Staff". Staff juga benar-benar see alert "Restricted access" di `/users` (bukan tabel) dan tombol Add User tersembunyi. User multi-role akan dapat beberapa badge — belum terlihat live
- [x] **Header final: title saja, subtitle dihapus** (2026-10-02) — `PageHeader` hanya punya prop `title`; tidak ada elemen lain setelah `<h1>` (terverifikasi `nextElementSibling` = null di ketiga halaman). Riwayat percobaan: (a) ditumpuk — navbar vendor terkunci `h-(--ui-header-height)` = 4rem = **64px**, isi dua baris (28px + 20px) = 48px menyisakan 8px, **jarak terukur tepat 0px** sehingga keduanya bersentuhan; (b) inline — secara teknis aman tapi HIRO lebih suka tanpa subtitle sama sekali. Karena subtitle dihapus, `index.vue` tak lagi butuh `useAuth()` — `welcomeLine` dan destructure `user`/`roleNames` ikut dibuang sebagai dead code. **Kalau subtitle mau dikembalikan, taruh di `UDashboardToolbar` DI BAWAH navbar** (pola resmi template), jangan di dalam navbar 64px
- [x] **Purpose di modal hapus tidak lagi di bawah labelnya — kelima field seragam** (2026-10-04) — HIRO menolak hasil sebelumnya dengan *"purpose nya jangan letak bawah. design yang bagus posisinya"*. Saya sempat memberi Purpose baris penuh dengan label di atas nilainya untuk mengakomodasi teks bebasnya; **persis itulah yang ditolak** — itu membuat Purpose satu-satunya field yang tidak berbagi ritme "label di kolom kiri" seperti empat lainnya. **AKHIR: kelima entri berbentuk identik.** `deleteDetails` **tidak lagi membawa flag `wide` sama sekali** — mesin itu dihapus, bukan dibiarkan tertidur: **0 referensi** `d.wide`, `wide: true`, atau `mt-0.5 line-clamp-3` tersisa di halaman yang tersaji; satu-satunya `col-span-2` yang tersisa milik field `tujuan` di form Add/Edit, bukan modal ini. `dl` kini `grid-cols-[7.5rem_1fr] items-baseline gap-x-5 gap-y-3 rounded-xl px-5 py-4` — **kolom label lebar tetap** sehingga kelima label sejajar dan kelima nilai mulai di x yang sama; itulah yang membuat blok ini terbaca sebagai satu unit yang dirancang, bukan lima pasang label/nilai yang longgar. Label jadi `text-xs font-medium uppercase tracking-wide text-dimmed`; nilai `text-sm font-medium text-default`. Purpose tetap `line-clamp-2 break-words` sehingga ia membungkus **DI DALAM** kolom kanan, bukan turun ke bawah labelnya, dengan teks penuh di `title`. **TERUKUR:** kelima baris melaporkan value-top dikurangi label-top sebesar **−3px**, artinya semuanya berbagi baseline yang sama — Purpose benar-benar sebaris dengan yang lain, termasuk saat membungkus dua baris ("Record a new CCTV access request."). Dialog 448×396. ESC menutup tanpa menghapus; server tetap 10 baris, 0 probe. **Catatan harness yang layak diingat:** tombol aksi sebuah baris bisa berada **di luar viewport** meski tabel tampak sudah ter-scroll — koordinat klik dari `getBoundingClientRect` mendarat di x=1393 pada viewport selebar 1280 dan kliknya ditelan tanpa error sama sekali; selalu `scrollIntoView({block:'center'})` target lalu ukur ulang sebelum mengirim event mouse sungguhan
- [x] **Field di modal konfirmasi hapus: Employee No, PIC, Purpose, Date, Section** (2026-10-04) — HIRO: *"yang ditampilkan di modal adalah employee no, PIC, purpose, date, section"*. Mengganti No / Date / Section / PIC menjadi, **persis urutan itu**: Employee No (`no_pegawai`), PIC (`nama_pemohon`), Purpose (`tujuan`), Date (`tanggal`), Section (`departemen`). Nomor `nomor` yang tersembunyi **dihapus dari dialog** — HIRO menggantinya dengan Employee No, karena itulah field yang benar-benar dipakai orang untuk mengenali sebuah baris; `nomor` adalah kunci berurutan yang tidak pernah muncul di UI. Computed `deleteNo` yang crisscross喂给它 dihapus outright, bukan dibiarkan menggantung: **0 referensi `deleteNo`** tersisa di source yang tersaji. Sekarang datanya digerakkan `deleteDetails` computed yang mengembalikan `{label, value, wide?}[]`, jadi **urutan field adalah data, bukan markup**. **Bagian yang tidak tb awhile jelas, layak diingat:** Purpose memakai `wide: true` dan mendapat baris penuh tersendiri, karena ia teks bebas yang bisa jadi beberapa kalimat — dipaksakan ke kolom nilai yang sempit bersama empat lainnya, ia akan membungkus jadi stub yang tak terbaca. Tapi membungkusnya sebagai dua anak `col-span-2` telanjang dari `grid-cols-[auto_1fr]` berarti `gap-y-2` milik grid **ikut** memisahkan label dari nilainya, sehingga "Purpose" terbaca sebagai keterangan-yatim melayang di atas teks, bukan sebagai sebuah field. Screenshot pertama mengonfirmasi masalahnya secara visual. **FIX:** bungkus tiap baris dengan `<div>` — sah di dalam `<dl>`, karena HTML5 mengizinkan wadah `<div>` yang mengelompokkan pasangan dt/dd — memakai `class="contents"` untuk baris biasa supaya tetap berperilaku sebagai item grid telanjang, dan blok `col-span-2` sungguhan untuk Purpose dengan `mt-0.5`. Jarak label-ke-nilai terukur turun dari satu baris *grid gap* menjadi **2px**, dan semua baris kini konsisten. Purpose juga `line-clamp-3` dengan teks penuh di `title`, sehingga entri yang sangat panjang tidak dapat meregangkan dialog tanpa batas. **TERVERIFIKASI:** dialog pada baris terakhir terbaca persis "Employee No 321331 / PIC Ngizzatuloh / Purpose biasa quality issue bro / Date 30 Sep 2026 / Section Stacking"; ESC menutupnya, server tetap 10 baris, 0 probe, 0 referensi `deleteNo`
- [x] **Subheader "User Directory" di halaman User Management, sama persis dengan sheet header CCTV** (2026-10-04) — HIRO: *"tambahkan subheader di halaman users. seperti CCTV Access Request Log"*. Dibangun **meniru blok itu persis**, bukan sekadar mirip: kartu `rounded-lg border border-default bg-elevated p-4` yang sama, split `flex flex-wrap items-center justify-between gap-3` yang sama dengan judul di kiri dan aksi di kanan, dan h1 `text-lg font-bold tracking-wide` yang sama. Judul dipilih **"Registered User"** supaya halamannya terbaca sebagai dua hal yang berbeda: "User Management" di navbar menamai **LAYAR**, "User Directory" di bawah menamai **REGISTER**. Hubungannya sama persis dengan "CCTV Access" (navbar) versus "CCTV Access Request Log" (sheet header). Kalau mau judul lain, cukup ganti satu string. Search, penghitung "N of M", dan tombol Add User semuanya pindah ke dalam kartu header itu; baris toolbar telanjang yang sebelumnya ada sudah hilang. **KENAPA BUKAN SUBTITLE PageHeader — dan ini bagian yang layak diingat, karena sudah dicoba dan gagal dua kali di project ini:** navbar dikunci ke tinggi tetap `h-(--ui-header-height)` = 4rem = 64px. Itulah sebabnya `PageHeader` hanya berisi judul dan **sama sekali tidak punya prop subtitle**; judul bertumpuk dengan subtitle terukur jaraknya **0px** dan keduanya terlihat menyentuh. Jadi sheet header diletakkan di BODY panel, di bawah navbar — persis di tempat halaman CCTV menaruhnya. **TERVERIFIKASI** kedua levelnya benar-benar terpisah dan berskala benar: h1 navbar "User Management" 20px dengan kelas `truncate text-xl font-bold`, h1 sheet "User Directory" 18px dengan kelas `text-lg font-bold tracking-wide`, yang latter berada di dalam kartu berbingkai. Search tetap berfungsi dari posisi barunya ("staf" → 2 baris, penghitung "2 of 3", dihapus → 3). Server tidak tersentuh: admin, operator01, staf04. **Catatan harness:** `document.querySelector('h1')` mengembalikan judul **NAVBAR**, bukan sheet header baru, karena `PageHeader` juga merender `<h1>` — probe pertama melaporkan "User Management" dan terlihat seperti perubahannya sama sekali tidak diterapkan. Gunakan `document.querySelectorAll('h1')[1]` di halaman ini
- [x] **"Create user" mustang bisa jalan dari UI — akar masalahnya roleIds bukan integer** (2026-10-04) — Toast error HIRO justru memuat jawabannya, dan saya melewatkannya karena menguji buta: *"The req field is required; The JSON value could not be converted to System.Int32. Path: $.roleIds[0] | LineNumber: 0 | BytePositionInLine: 109."* **PELAJARAN PENTINGNYA:** itu **satu error dengan bayangannya**, bukan dua. Body yang gagal bind membuat parameter `req` jadi null, sehingga ASP.NET **juga** melaporkan "The req field is required" — pesan yang terkenal itu hanyalah gejala turunan. **Penyebab sebenarnya ada di kalimat setelah titik koma.** Saya sudah menguji endpoint create langsung dengan tujuh cara dan semuanya lolos, karena saya mengirim `roleIds: [3]`, angka asli; `USelectMenu` mengirim bentuk lain. **FIX:** helper `normaliseRoleIds()` yang dipakai di create, update, **dan** guard "at least one role" — sehingga pemeriksaan tidak mungkin berbeda dengan yang dikirim — menerima number, string angka, atau objek `{label, value}` utuh, menghapus duplikat, dan **membuang** apa pun yang bukan angka finit, sehingga entri liar menghasilkan error validasi bersih, bukan body yang tidak bisa di-bind. **TIGA CACAT LAINNYA** ikut diperbaiki di pass yang sama, ketiganya ditemukan dengan mengukur, bukan dengan membaca. (1) Field Role adalah field **terakhir** sebelum footer, dan `USelectMenu` itu multiple select sehingga **tetap terbuka** setelah dicentang; karena tak ada apa-apa di bawahnya kecuali tombol, popup-nya terbuka tepat di atas "Create user" dan **menelan klik** — nol request terkirim. Satu field lebih atas, popup-nya membuka ke bawah menimpa input teks biasa dan tidak menutupi satu pun tombol. (2) Reaksi alami terhadap popup yang macet, menekan Escape, **membuang role** yang sudah dicentang, sehingga percobaan kedua gagal dengan "Select at least one role". (3) Modal dibangun ulang ke bentuk persis dialog CCTV (`title`/`description` sebagai prop UModal, UForm di slot `#body`, tombol di dalam form) dan setiap UFormField diberi `name` yang cocok dengan key state, ditambah `@error="onFormError"` — karena `onSubmitWrapper` UForm menjalankan `_validate()` **lebih dulu** dan kalau gagal hanya `emits("error")` **tanpa pernah memanggil `props.onSubmit`**, dan tidak ada yang mendengarkan di sini, sehingga submit yang ditolak terlihat identik dengan tombol mati. **TERVERIFIKASI end-to-end setelah perbaikan:** membuat `zzok(Manager)` lewat UI sungguhan, lalu `zzmulti(Admin+Staff)` dengan mencentang dua role — yang sekaligus menjawab pertanyaan HIRO: **YA, satu user bisa memegang beberapa role** (`AppUser.UserRoles` sebuah koleksi, `CreateUserRequest` menerima `List<int> RoleIds`, select-nya `multiple`, dan `[Authorize(Roles=...)]` membaca semua claim). Ketiga user sekali pakai dihapus (204 ×3); server kembali ke admin, operator01, staf04; CCTV 10 baris, 0 probe. **Catatan:** recorder `window.fetch` tidak membuktikan apa pun di sini — ofetch menangkap `fetch` sebelum patch, sehingga request asli tidak terlihat; hanya respons server yang bisa diandalkan
- [x] **Toggle Status tidak bisa menonaktifkan Admin aktif terakhir** (2026-10-04) — HIRO bertanya *"bagaimana kalau akun administrator deactivated?"*; setelah saya jelaskan lockout-nya dan menawarkan tiga lapis, ia memilih yang UI saja: *"ya disable saja toggle inactive user untuk administrator"*. **LOCKOUT-nya, diukur dari kode bukan dikira-kira:** `/api/users` dijaga `[Authorize(Roles = "Admin")]` jadi hanya Admin yang bisa menyalakan kembali; cek login di AuthController adalah `if (user is null || !user.IsActive || !BCrypt.Verify(...))` jadi user inactive tidak bisa masuk; dan **tidak ada guard admin-terakhir di mana pun** — grep UsersController untuk "last"/"Last" kosong. Jadi menonaktifkan Admin aktif terakhir mengunci seluruh akun admin secara permanen, hanya bisa dipulihkan dengan edit database manual. UI bisa menyebabkannya tanpa suara, karena tombol hapus disembunyikan untuk `admin` tapi switch Status **tidak punya guard sama sekali**. Dua detail Worse yang ditemukan sambil menyelidiki: pesan gagal login adalah `Username atau password salah` — **string yang sama persis** dengan password salah, jadi admin tidak akan pernah tahu penyebab sebenarnya; dan JWT-nya **sepenuhnya stateless** (tidak ada hook `OnTokenValidated` yang consultar DB, `ExpireHours` default 8), jadi menonaktifkan diri sendiri **tidak langsung mengubah apa pun** — akses Admin penuh tetap berlaku sampai token kedaluwarsa. **IMPLEMENTASI:** computed `deactivateBlocked` — true bila akun yang diedit aktif, punya role Admin, dan **tidak ada user lain** yang aktif sekaligus Admin — dengan switch diberi `:disabled` dan penjelasan yang terlihat ("This is the only active Admin. Deactivating it would lock every admin account out, with no one left to switch it back on."). **DIKUNCI PADA "ADMIN AKTIF TERAKHIR", bukan pada username**, supaya pembatasan ini tidak hidup lebih lama daripada alasaneya. **TERVERIFIKASI di keempat kasus, bukan hanya kasus yang diblokir:** admin sendirian → switch checked DAN disabled, help tampil; operator01 (Staff) → checked, enabled, tanpa help; setelah membuat Admin kedua sekali pakai lewat API, **kedua akun admin jadi enabled tanpa help** — membuktikan kondisinya soal "yang terakhir", bukan sekadar soal memegang role; setelah account sekali pakai dihapus (204) guard-nya kembali. `isActive` admin tetap true sepanjang pengujian dan server kembali ke admin, operator01, staf04. **BATASAN JUJUR, juga tercatat di komentar kode:** ini hanya guard UI, jadi API tetap menerima `isActive:false` dari apa pun yang memegang token Admin yang valid — menutup kasus tidak sengaja, bukan kasus sengaja. Guard backend akan jadi perbaikan sebenarnya kalau HIRO mau. **Catatan harness:** meng-Probe "the switch" dengan `[data-state]` di dalam dialog akan mengenai trigger `USelectMenu` lebih dulu dan melaporkan `state: closed` yang menyesatkan; kontrol yang sebenarnya adalah `[role=dialog] [role=switch]`
- [x] **Footer tabel ala CCTV + teks "3 of 3" di sebelah search dihapus** (2026-10-04) — HIRO: *"buat tabel footer nya seperti table cctv access. lalu hapus teks 3 of 3 disebelah kotak search"*. **POIN STRUKTURALNYA**, dan kenapa ini butuh restrukturisasi dan bukan sekadar satu div tambahan: di register CCTV, **border-nya ada di kartu luar**, `overflow-hidden rounded-xl border border-default bg-elevated`, yang membungkus **area scroll (`.logbook-scroll`) dan footer sebagai saudara**. Halaman users sebelumnya menaruh border di container scroll itu sendiri, sehingga footer yang ditambahkan di dalamnya akan ikut ter-scroll bersama baris dan duduk di tepi yang salah. Jadi border dan radius **dipindahkan ke atas** ke kartu luar yang baru, area scroll tetap `max-h-[70vh] overflow-y-auto scrollbar-gutter-stable`, dan footer menjadi anak terakhir kartu. Markup footer persis sama dengan CCTV — `flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted` dengan jumlah di kiri dan `Showing {{ visibleUsers.length }} of {{ users.length }}` di kanan — kecuali kata tunggal/pluralnya **user/users**, sesuai halaman ini. Kartu header sekarang hanya berisi "Registered User" + search + Add User. **Kedua angka di footer mengikuti SEARCH**, tidak pernah total mentah, supaya tidak mungkin berbeda dengan baris di layar — alasan yang sama dengan count kiri CCTV yang mengikuti filternya. **TERVERIFIKASI:** diam "3 users | Showing 3 of 3"; search "staf" → 2 baris dan "2 users | Showing 2 of 3"; search tanpa hasil → "0 users | Showing 0 of 3" (nol yang benar, bukan bug lama CCTV di mana daftar yang terfilter jadi kosong terbaca "1 row / Showing 0 of 1"); dihapus → kembali 3. Dikonfirmasi footer adalah anak dari kartu berbingkai, dan teks "N of M" sudah hilang dari sebelah kotak search. Server tidak tersentuh: admin, operator01, staf04
- [x] **Subheader "Registered User" di halaman User Management** (2026-10-04) — HIRO: *"now remake users page design"*. **Pelajaran besarnya adalah jebakan CSS yang saya alami sendiri:** saya menata avatar dengan aturan scoped `:global(.dark) .users-avatar`, dan **transform scoped-CSS Vue tidak bertahan bila ada `:global()` di depan** — selector-nya runtuh jadi `.dark { background: color-mix(in oklab, var(--ui-primary) 18%, transparent) }` telanjang, yang **match ke elemen `<html>`** dan mewarnai **seluruh dokumen**, sidebar termasuk. Gejalanya sangat membingungkan: `getComputedStyle` pada html melaporkan `oklab(0.786 -0.175 0.079 / 0.18)` translucent oranye-ish sementara setiap elemen anak tetap melaporkan nilai gelap yang benar, dan screenshot `/cctvacc` yang diambil beberapa detik kemudian sempurna — jadi ini terlihat seperti artefak screenshot/transisi, bukan bug. **YANG MEMBUKA:** kueri "aturan mana yang benar-benar match html dan menyetel background", yang mengembalikan tepat satu entri, yaitu aturan `.dark` itu. **ATURAN YANG HARUS DIBAWA: jangan pernah menaruh `:global(...)` di depan selector scoped; dan kalau sebuah screenshot berbeda dengan getComputedStyle, cari aturan yang match elemen dengan nilai odd itu — bukan mengambil ulang screenshot.** Perbaikannya: hapus CSS kustom sepenuhnya dan pakai prop milik `UAvatar` sendiri, `color="primary" variant="soft"`, yang mengikuti aksen secara native tanpa aturan kustom dan tanpa risiko seperti itu. Perubahan desain: identitas kini **satu kolom** — avatar, nama lengkap, dan @username bertumpuk — karena Username dan Full Name sebelumnya dua kolom terpisah, sehingga satu-satunya hal yang benar-benar dikenali orang terbelah di tabel; header sticky uppercase, tabel dibatasi `max-h-[70vh]` dengan scroll sendiri, hover, skeleton loading, dan empty state; pencarian client-side dengan penghitung "N of M"; kolom Created kini menaruh `createdBy` di baris sendiri yang diredupkan, bukan menempel sebelah tanggal tanpa pemisah sehingga keduanya terbaca sebagai satu rentasan. Add/Edit menjadi UModal sungguhan (sebelumnya UCard inline di bawah tabel — penyebabnya slot `UModal #footer` yang menelan `@click` di tombol submit; solved dengan menaruh tombol **di dalam** `UForm`, sama seperti modal CCTV). Delete kehilangan `confirm()` telanjang dan mendapat dialog yang cocok dengan dialog CCTV. **Semua string Indonesia diterjemahkan** sesuai aturan app berbahasa Inggris (`Gagal memuat user`, `Pilih minimal satu role`, `Username tidak bisa diubah`); dead `removeUser` dan `toggleActive` dihapus. `overflow-y-scroll` diganti `overflow-y-auto` + `scrollbar-gutter-stable`: varian `scroll` selalu melukis track, yang dengan hanya tiga user memunculkan scrollbar permanen yang tak berguna. **TERVERIFIKASI:** search "operator" → 1 baris dan penghitung "1 of 3", dihapus → 3; modal edit menampilkan username disabled dengan "Save changes"; dialog delete membaca operator01 / Operator Satu / Staff; ESC menutup tanpa menghapus; baris admin tetap tanpa tombol hapus; server tetap Listed admin, operator01, staf04. **BUKAN BUG, SENGAJA TINGGALKAN:** tombol primary solid menampilkan teks **gelap** di atas hijau terang di dark mode (Create user terukur oklch(0.208) di atas rgb(0,220,130)). Itu `text-inverted` milik Nuxt UI sendiri yang berbalik untuk dark mode, tombol Save Row di CCTV terukur identik, dan mengubahnya akan merusak konsistensi seluruh aplikasi
- [x] **Modal konfirmasi hapus baris di-redesign penuh (dari `confirm()` native ke UModal)** (2026-10-04) — HIRO meminta *"redesign delete row confirmation modal. buat design dan animasi dan transisi smooth yang keren"*. Semula hanya `confirm()` native — dialog abu-abu milik browser, tidak bisa di-style, **tidak menampilkan baris mana** yang akan hilang, danEARCH tidak nyambung di aplikasi bertema. Diganti UModal sungguhan plus ref `showDelete` / `deleteTarget` / `deleting` dan fungsi `askDelete(row)` / `confirmDelete()`. **KEPUTUSAN DESAIN YANG BERARTI:** dialog mengulang **identitas baris** (No, Date, Section, PIC) dalam sebuah *definition list*. Aksi destruktif pada register bernomor harus membuktikan bahwa yang dikonfirmasi adalah baris yang **diamaksud**, dan baris tabelnya sudah tidak terlihat di balik backdrop. Ditambah: sapuan lembut `bg-error/20 blur-3xl` yang merembes dari kanan-atas, dipotong oleh `overflow-hidden` panel itu sendiri (trik yang sama dengan orb welcome dashboard, dikunci ke `error`); piringan bahaya 44px dengan `bg-error/10 ring-1 ring-inset ring-error/25 dark:bg-error/15`; Cancel sebagai neutral ghost dan Delete record sebagai `color="error"` solid dengan `:loading="deleting"`. **SOAL GERAKAN** — ditulis sebagai keyframe CSS biasa, **bukan** pembungkus `<Transition>`, karena elemennya selalu ada begitu UModal me-mount kontennya dan sebuah transition Vue butuh `v-if`/`appear` untuk memicu; menambahkan v-if sekadar untuk timing akan menaruh detail baris di jalur render yang berbeda dari sisa dialog. *Stagger*: ikon scale-in 300ms pada 0ms, judul rise 360ms pada 60ms, subjudul 110ms, daftar identitas 420ms pada 160ms, semuanya pada `cubic-bezier(0.22, 1, 0.36, 1)` yang **monotonik**, dengan sapuan bernapas 0,75→1 opacity dan 1→1,08 skala selama 5,5s mulai 420ms. Semua kurva monotonik — aturan app-wide tanpa scale itu soal **TEKS** (glif yang di-scale ter-raster pada ukuran antara dan terasa lembut, dan kurva non-monotonik bisa menyeret frame akhir menjauh dari posisi diam); keduanya tidak berlaku pada piringan ikon 44px, jadi yang itu boleh scale, tetap pada kurva monotonik agar mendarat persis di tempatnya. `transform` aman meski dialog-nya dipusatkan karena **Tailwind v4]** memancarkan pemusatan sebagai properti `translate` terpisah, bukan `transform`, jadi `transform` anak tidak bisa berkomposisi atau berebut dengan pemusatan induknya. `prefers-reduced-motion` mematikan kelima animasi. **TERVERIFIKASI END-TO-END di record sekali pakai, tidak pernah di data HIRO:** record yang ada dikloning jadi PIC "ZZ Delprobe" / Section "ZZ Del" (nomor otomatis 14), dialognya dibuka, identitas terbaca "No 14, 04 Okt 2026, ZZ Del, ZZ Delprobe", klik Delete record, dialog menutup, baris hilang, tabel kembali ke 10. Kebenaran server setelahnya: total 10, nol baris probe, nomor 4–13 berurutan. ESC menutup tanpa menghapus (10 baris tetap). Pencarian `confirm(` di halaman yang tersaji kini mengembalikan **0 kemunculan**, jadi tidak ada jalur mati yang ditinggalkan
- [x] **Counter tombol Filter jadi ikon sudut, dan UserMenu: menu menutup + Sign Out merah** (2026-10-04) — Dua revisi dari HIRO. **(1) TOMBOL FILTER** — *"user select jangan hilangkan teks Filter. lalu filter Counternya dijadikan icon di atas kanan tombol filter... pastikan jangan ada css yang bergeser"*. **PENYEBAB TEKSNYA HILANG:** `UButton` merender prop `label` ke dalam **DEFAULT slot**, jadi **konten apa pun di default slot akan MENIMPA label**. Markup lama menaruh `UBadge` di slot itu — itulah sebabnya kata "Filter" lenyap begitu ada filter aktif. Dan `ml-1` pada badge berada di baris flex tombol itu sendiri, sehingga aktivasi filter membuat tombol melebar dan mendorong Excel serta Add Record ke samping. **FIX:** `UButton` kini dibungkus `<div class="relative inline-flex">` dengan label dibiarkan utuh, dan counternya adalah **saudara yang diposisikan absolut** — `-right-1.5 -top-1.5`, 18×18, `bg-primary text-inverted ring-2 ring-elevated`, dengan marker class stabil `infra-filter-count`. Karena keluar dari flow, tombol kini **identik di setiap jumlah**: terukur `filterW 81px, filterX 921, excelX 1010` dengan 0, 1, dan 2 filter aktif, dan label terbaca "Filter" di ketiganya. Badge sengaja menonjol 6px dari sudut kanan atas dan memakai `ring` supaya terbaca **melayang di atas** tombol, bukan seperti takahan. Enter/leave opacity + translateY saja, tanpa scale, sejalan dengan sisa aplikasi. **(2) USERMENU** — *"setelah user pilih mode Light atau dark, close context menu nya. lalu font Sign Out buat warna merah dan sesuaikan warna efek hovernya"*. Ditambah `const menuOpen = ref(false)` yang di-bind sebagai `v-model:open` pada UDropdownMenu, di-set false di onSelect Light maupun Dark. Sign Out kini membawa `class: 'text-error data-highlighted:text-error data-[state=open]:text-error [&_svg]:text-error data-highlighted:before:bg-error/10'` — perlu karena kelas bawaan vendor adalah `text-default data-highlighted:text-highlighted` dengan ikon depan `text-dimmed group-data-highlighted:text-default`, jadi tanpa override item akan menjadi pucat saat hover dan kehilangan makna destruktifnya tepat saat kursor berada di atasnya. `[&_svg]` diperlukan karena ikon punya kelas warna sendiri yang menang secara spesifisitas. **TERVERIFIKASI:** warna Sign Out saat diam `oklch(0.704 0.191 22.216)` (merah), saat hover tetap merah dengan latar merah lembut `oklab(0.704 0.177 0.072 / 0.1)`; memilih Dark menutup menu dan menerapkan mode gelap, memilih Light menutup menu dan menerapkan mode terang, lalu gelap dipulihkan. Filter dibiarkan kosong, 10 baris utuh. Catatan harness: `tabular-nums` juga dipakai kolom angka tabel, sehingga probe "badge" dengan kelas itu memberi false positive — makanya saya buat kelas `infra-filter-count` khusus, yang memang praktik lebih baik
- [x] **Scrollbar tabel CCTV yang tak terlihat di dark mode — penyebabnya token yang TIDAK ADA** (2026-10-04) — HIRO melaporkan **"srollbar pada table list saat dark mode kurang terlihat jelas"**. Dengan membaca nilai terhitung alih-alih menebak, ditemukan **DUA cacat, bukan satu**. **(1)** Thumb dideklarasikan `background: var(--ui-border-strong)`, dan **`--ui-border-strong` TIDAK DIDEFINISIKAN DI MANA PUN** di Nuxt UI v4 — nama yang sebenarnya `--ui-border-muted` dan `--ui-border-accented`. `var()` yang tak ter-resolve membuat **seluruh deklarasi tidak valid**, sehingga thumb tidak melukis warna sama sekali. Yang krusial: **rule-nya tetap ter-compile**, itu sebabnya tidak ada yang tampak rusak di dev-tools — dia hanya tidak merender apa pun. **(2)** Fallback Firefox memakai `--ui-border`, yang di dark resolve ke `oklch(27.9% 0.041 260.031)` — **nilai yang sama persis** dengan `--ui-bg-elevated`, yaitu permukaan tempat scrollbar berada. Thumb yang dilukis dengan warna yang sama dengan latar-benef部分是 **tidak terlihat secara konstruktsi** di tema mana pun. **Yang kedua inilah penyebab sebenarnya**, dan ia akan tetap ada kalau hanya cacat pertama yang diperbaiki. **FIX:** thumb sekarang `--ui-text-dimmed` (dark: oklch(55,4%), jauh di atas permukaan 27,9%), hover `--ui-text-muted`, dan override `.dark` menaikkan tint track (14% vs 10% color-mix) serta hover ke `--ui-text-highlighted`. Firefox mendapat pemisahan dark/light yang sama lewat `scrollbar-color`, karena ia mengabaikan rule `::-webkit` sepenuhnya. Juga saya ganti dari shorthand `background` ke `background-color`, karena **shorthand itu me-reset `background-clip`** ke nilai awal dan akan membatalkan thumb berbentuk pil. **TERVERIFIKASI secara VISUAL, bukan cuma angka:** saya tambah 12 baris sementara agar tabel benar-benar overflow (`scrollHeight 1399 > clientHeight 756`), lalu ambil crop 4× tepi kanan tabel — dark menampilkan pil abu-abu terang yang **jelas terbaca** di atas permukaan navy, light menampilkan pil abu-abu tengah yang terlihat tanpa jadi ramai. Baris probe dihapus (12 × **204**), kembali ke **10 baris milik HIRO**, mode gelap dipulihkan. **PELAJARAN YANG BISA DIPERLUAS:** kalau sebuah scrollbar atau elemen bertema terlihat "tidak terlihat", **baca nilai token yang sudah ter-resolve di elemen itu dan bandingkan dengan permukaan di belakangnya**. `var()` yang invalid **gagal dengan diam-diam** dan tetap meninggalkan rule-nya di stylesheet. Yang juga layak diingat: tabel hanya overflow — jadi hanya menampilkan scrollbar — bila baris cukup banyak untuk melampaui `max-h-[70vh]`, jadi **semua pekerjaan scrollbar wajib diuji dengan baris sementara** atauokerskyutnya terlihat seperti tidak ada yang perlu diperbaiki
- [x] **Brand mark sidebar pakai ikon `streamline-cyber:network` (bukan huruf "IC")** (2026-10-04) — Di `web/app/components/BrandMenu.vue`, `<span>… IC</span>` diganti `<UIcon name="i-streamline-cyber:network" class="size-5" />` di dalam badge `size-8 bg-primary rounded-lg` yang sama; class `font-bold` dihapus karena memang untuk hurufnya. **TERVERIFIKASI rendering-nya — dan, yang lebih penting, bahwaTetap jalan OFFLINE,** karena project ini tidak bisa bergantung pada Iconify API. `nuxt.config.ts` menetapkan `clientBundle: { scan: true, size: 200000 }` sehingga ikon yang dirujuk di source **di-resolve saat BUILD time** lalu di-embed, dengan `clientBundleFallback: ''` sehingga **tidak pernah** diambil dari `api.iconify.design` saat runtime; komentar di sana menyatakan terang bahwa server kantor 10.89.6.237 tidak punya internet dan fetch runtime akan membuat **setiap ikon lenyap tanpa jejak**. Dikonfirmasi dengan inspeksi: span ikon membawa `class="iconify i-streamline-cyber:network size-5"` dan dilukis lewat **CSS mask** yang nilainya berupa `data:image/svg+xml,…` sepanjang **601 karakter** — di-embed, bukan di-fetch. Satu-satunya resource yang cocok `/iconify/` adalah dev server lokal yang menyajikan modul bundel. **YANG PERLU DIINGAT UNTUK NANTI:** `streamline` **bukan** collection yang terpasang lokal (hanya `@iconify-json/lucide`), jadi ikon ini di-bundle dari jaringan oleh mesin **build**, bukan dibaca dari `node_modules`. Itu aman untuk build di mesin ini, tetapi `npm install` di mesin tanpa internet tidak akan bisa me-resolve-nya. Kalau ikon ini suatu saat hilang, **cek dulu budget `size` clientBundle** (sekarang 200000) — overrun dropping ikon secara senyap. **Dua hal yang membuang waktu dan sebaiknya tidak diulang:** **(1)** endpoint `.svg` milik Iconify API mengembalikan **HTTP 403 untuk SEMUA ikon** dari mesin ini, termasuk `lucide:network` yang jelas ada — jadi 403 di sana **bukan bukti ikon tidak ada**; hanya halaman yang sudah ter-render yang jadi bukti. Endpoint `?prefix=`collection bekerja dan mengonfirmasi collection `streamline-cyber` ada dengan **500 ikon**, meski hanya mengembalikan metadata, bukan daftar nama. **(2)** Percobaan pertama saya menulis namanya `i-streamline-cyber-network` (dash, tanpa titik dua) dan **tetap tergambar**, menyelesaikan ke ikon yang sama — karena semua ikon lain di codebase memakai bentuk dash (`i-lucide-plus`) sebab lucide adalah collection default. Saya tetap mengubahnya ke bentuk kanonik dengan titik dua agar source **persis** seperti yang HIRO minta
- [x] **Avatar sambutan punya glowing breath effect** (2026-10-04) — HIRO meminta **"buat avatarnya punya glowing breath effect"**. Glow-nya diletakkan pada **wrapper `span` baru** di sekeliling `UAvatar`, **bukan** pada avatar itu sendiri, dan itu-load-bearing bukan sekadar estetika: avatar memakai `ring-4 ring-primary/15`, dan **ring Tailwind diimplementasikan sebagai `box-shadow`** — jadi menganimasikan `box-shadow` di avatar akan **menghapus ring itu sejak frame pertama** denyut. Wrapper punya kotak sendiri, sehingga glow bernapas mengelilingi ring yang tetap utuh. `.infra-avatar-glow` menjalankan `infra-breathe` selama **4,5s** dengan easing simetris `cubic-bezier(0.4, 0, 0.6, 1)` sehingga tarikan dan hembusan sama lama dan loop-nya **tidak punya sambungan yang terlihat**; keyframes menganimasikan cincin spread dekat (0,25rem 16% → 0,3rem 28%) ditambah bloom luar (0 spread/0% → blur 26px di 42%). **WARNA TIDAK DI-HARDCOD** — memakai `color-mix(in oklab, var(--ui-primary) N%, transparent)`, variabel yang sama dengan bintang dan orb glow, jadi halo **mengikuti aksen user otomatis**; hex literal akan jadi satu-satunya bagian banner ini yang tidak ikut tema. **TERVERIFIKASI lewat rekaman rAF:** **133 nilai `box-shadow` berbeda di 133 frame** selama 2,2s — jadi benar-benar bergerak tiap frame, bukan berkedip. Ganti aksen lewat UserMenu sungguhan terkonfirmasi: hijau → violet → rose → hijau, warna halo mengikuti tiap langkah. `prefers-reduced-motion` menghentikan denyut dan meninggalkan **halo statis** alih-alih menghilangkan glow-nya, supaya cincin aksen tetap terbaca sebagai hal yang disengaja; terverifikasi lewat `cdp Emulation.setEmulatedMedia`, `animationName` terbaca `none` dan shadow **identik byte-per-byte setelah 1,5s**. **DUA BUG SAYA SENDIRI YANG LAYAK DICATAT:** **(1) Uji reduced-motion pertama saya GAGAL** dan hampir saya kirim begitu saja: saya men-*patch* file dengan tool `patch()`, lalu **langsung menulis ulang file yang sama** dari variabel Python yang diambil **sebelum** patch itu — sehingga edit-nya tertimpa diam-diam dan guard-nya tidak pernah ada. Petunjuknya: reduced motion tetap melaporkan `animationName 'infra-breathe'`. Pelajaran: kalau satu gilir既能 patch file sekaligus menulisnya wholesale, **baca ulang file setelahnya dan pastikan perubahannya ada di DISK**. **(2) Red herring yang saya kejar dulu:** `box-shadow` terhitung pada avatar terbaca `rgba(0,0,0,0) 0px 0px 0px 0px` tiga kali padahal `--tw-ring-shadow` terdefinisi benar — **terlihat seperti ring-nya musnah**. Crop 3× avatar memperlihatkan ring-nya **tergambar jelas**, jadi serialisasi computed value untuk multi-layer `color-mix` shadow memang tidak bisa dipercaya; **konfirmasi secara visual sebelum bertindak** atas sebuah `box-shadow` terhitung
- [x] **Glow aksen lembut dari template nuxt.dev ditambahkan ke WelcomeBanner** (2026-10-04) — HIRO meminta **"tambahkan warna gradient soft nya seperti template yang saya tunjukkan"**. **DIUKUR DULU DI REFERENSI, dan temuannya mengoreksi sebuah asumsi: TIDAK ADA CSS gradient sama sekali di changelog-template.nuxt.dev.** Memindai seluruh elemen untuk `backgroundImage` yang mengandung "gradient", dan seluruh `::before`/`::after`, hasilnya **kosong**. Yang menghasilkan cahaya lembut itu merely **lingkaran biasa**: `<div class="absolute -right-1/2 z-[-1] rounded-full bg-primary blur-[300px] size-60 sm:size-100 transform -translate-y-1/2 top-1/2">` — terukur **400×400px** pada lebar 1920, background `rgb(0,220,130)` (primary tema), `filter: blur(300px)`, border-radius efektif lingkaran penuh, `z-index: -1`, terpusat vertikal. Jadi "gradient" itu **murni falloff blur dari cakram warna aksen pekat**. **SATU PENYIMPANGAN YANG SENGAJA:** referensi memakai `-right-1/2`, yang berarti `right: -50%` **DARI CONTAINING BLOCK**, jadi itu hanya berfungsi karena panel hero-nya (terukur 1249px) jauh lebih lebar dari orb 400px — overhang-nya saya ukur 625px. Banner sambutan jauh lebih sempit (terukur **1008px**), jadi `-right-1/2` akan menempatkan orb **seluruhnya di luar** banner dan ter-clip jadi tidak terlihat. Karena itu offset-nya diberikan dalam **piksel**: `right-[-120px] size-[420px] top-1/2 -translate-y-1/2 rounded-full bg-primary blur-[300px]`. `overflow-hidden` milik banner sendiri yang melakukan clipping — dan justru itulah yang membuatnya terbaca sebagai cahaya yang **merembes masuk dari tepi kanan**, bukan gumpalan melayang. Orb diletakkan di dalam wrapper dekoratif yang sudah ada (`absolute inset-0`, `pointer-events-none`, `overflow-hidden`, `z-0`) **sebelum** v-for bintang, sehingga bintang tergambar di atas glow. `bg-primary` adalah token semantik, jadi mengikuti aksen user. **TERVERIFIKASI** dengan mengganti aksen lewat UserMenu sungguhan dan membaca **orb DAN bintang** di setiap langkah: hijau `rgb(0,220,130)` → violet `oklch(0.702 0.183 293.541)` → amber `oklch(0.828 0.189 84.429)`, **langsung dan bersamaan**, tanpa reload dan tanpa JS saya. Dikembalikan ke hijau; cookie `infra-cap.theme` kembali green/slate, mode gelap tidak berubah; setelah reload **46 bintang** dan glow tetap `rgb(0,220,130)`
- [x] **Background WelcomeBanner jadi starfield twinkling seperti hero panel nuxt.dev** (2026-10-04) — HIRO meminta background sambutan welcome pada Dashboard dibuat seperti efek `section.relative.isolate...` di https://changelog-template.nuxt.dev/, **dengan syarat warna aksen harus ikut berubah saat user mengganti tema**. Saya **tidak menebak** efeknya — saya membuka situs referensi di browser dan **mengukur** elemen yang dia tunjuk. Temuan: section itu sendiri `background: transparent` dan `background-image: none`, jadi seluruh visualnya datang dari lapisan anak `<div class="absolute inset-0 pointer-events-none z-[-1] overflow-hidden">` yang memuat **50** span `.star`. Tiap bintang adalah lingkaran kecil: `width/height: var(--star-size)` terukur **1,04–3,00px**, `border-radius: 50%`, `background-color: var(--ui-primary)`, `transform: translate(-50%,-50%)`, `animation: twinkle 2s ease-in-out infinite` dengan delay acak per bintang terukur **0,93–4,88s**, dan keyframes-nya **opacity-saja**: `0%,100% { opacity: 0.2 } 50% { opacity: 1 }`. Dua blob blur aksen lama di `WelcomeBanner.vue` diganti **46 bintang** dengan konstruksi yang sama persis. **DUA DETAIL YANG BERARTI:** **(1) WARNA** — semua bintang memakai `var(--ui-primary)`, persis seperti referensinya; itu variabel yang ditulis ulang Nuxt UI setiap kali user mengganti aksen, jadi **tidak ada warna hard-coded** di mana pun. **TERBUKTI dengan benar-benar mengganti aksen lewat UserMenu sungguhan:** background terhitung bintang berubah dari `rgb(0, 220, 130)` menjadi `oklch(0.702 0.183 293.541)` **saat itu juga** begitu violet dipilih — **tanpa reload dan tanpa JS saya involvement** — dan bertahan setelah reload lewat cookie `infra-cap.theme`; lalu dikembalikan ke hijau. **(2) SEED-nya DIKUNCI** — posisi bintang dihasilkan dari PRNG mulberry32 ber-seed (20261004), bukan `Math.random`, karena posisi dihitung di `setup()` yang jalan di server untuk SSR **lalu lagi** di browser saat hydration; `Math.random` akan menghasilkan field berbeda tiap kali dan memicu hydration mismatch. Terverifikasi stabil lintas reload: tiga bintang pertama di `76,37%,85,15% / 27,45%,73,95% / 54,11%,4,85%`. Animasi **opacity-saja** dengan sengaja, seperti referensi — menganimasikan transform atau size akan me-raster ulang 46 elemen tiap frame tanpa imbalan visual. `prefers-reduced-motion` menghentikan kedipan tetapi bintang tetap terlihat pada opacity tetap 0,55 (terverifikasi lewat `cdp Emulation.setEmulatedMedia`, `animationName` menjadi `none`) — bukan dihilangkan, supaya banner tetap bertekstur. Bentuk wrapper persis mengikuti referensi (`absolute inset-0`, `pointer-events-none`, `overflow-hidden`) dengan konten dipindah ke `z-10`
- [x] **"Clear all" di popover filter sekarang MENUTUP popover** (2026-10-04) — HIRO meminta **"ketika user klik clear popup filter close"**. `web/app/components/LogbookFilterPopover.vue` dapat state lokal `const open = ref(false)` yang di-bind sebagai `v-model:open` pada UPopover-nya — sebelumnya UPopover berjalan dengan state internal saja sehingga tidak bisa ditutup secara programatis. Handler baru `clearAll()` melakukan `emit('clear')` lalu `open.value = false`, dan tombol Clear all memanggil handler itu alih-alih emit langsung. Ini **sengaja mengecualikan** Clear all dari aturan komponen yang popover **tetap terbuka** setelah kontrol lain dipakai supaya beberapa filter bisa digabung tanpa membuka ulang — setelah semuanya dibersihkan tidak ada lagi yang perlu digabung, dan panel kosong yang melayang di atas tabel yang pulih terlihat seperti kliknya tidak kena. **TERVERIFIKASI:** setelah memilih Today, popover tetap terbuka (baris 10 → 6, chip "Date: 2026-10-04 → 2026-10-04"); klik Clear all **di dalam** popover menutupnya dan mengembalikan 10 baris dengan chip kosong. Membuka ulang berfungsi dan sorotan preset kembali ke "All time" dengan benar. **CEK REGRESI PENTING setelah menambah `v-model:open`**, karena mengikatnya bisa merusak dismiss milik popover itu sendiri — klik pointer nyata di luar pada (700,700) **tetap** menutupnya, dan tombol **Escape** juga tetap menutupnya. Tombol Clear all terpisah di baris chip (di luar popover) tidak terganggu dan tetap mengembalikan 10 baris. **Catatan harness yang layak diingat:** `document.body.click()` adalah **false negative** untuk menguji dismiss popover — ia melaporkan popover masih terbuka padahal klik `cdp Input.dispatchMouseEvent` yang sungguhan di batch yang sama sudah menutupnya; selalu uji dismiss dengan rangkaian pointer nyata. Filter dan search dibiarkan kosong; 10 baris utuh
- [x] **Sinkronisasi memory ronde kedua, 2026-10-04** — `MEMORY.md` masih di 68% dan **tidak disentuh** sejak sinkron pertama, padahal sekitar sepuluh pelajaran menumpuk selama sisa sesi. **Ditambahkan ke MEMORY:** **(1) Disiplin scope** — pakai mekanisme tersempit yang memperbaiki gejala; theme override global `dashboardPanel` ditolak HIRO karena membocorkan scrollbar ke tiga halaman lain, jadi gejala per-halaman harus pakai `:ui` per-instance. **(2) Slot vendor tanpa `data-slot`** — body `dashboardPanel` memang tidak punya, dan itulah justru pemicu saya tergoda memakai override global; kontrol yang tidak punya elemen DOM sendiri (trigger `UButton` yang butuh cincin merah milik `UInput`) butuh **prop khusus**, bukan `:ui`. **(3) Entri harness ditulis ulang** untuk menyerap tiga jebakan baru: variabel `window` dari `browser_exec` sebelumnya **tidak bertahan** melewati `Page.reload` (menyvakan 404 saat hapus), dan yang paling penting — **halaman kosong dengan HTTP 200** yang disebabkan revert ter-skrip saya sendiri yang menjatuhkan kurung penutup `defineAppConfig`; kelas kegagalan itu didiagnosis dengan `git diff` dan `document.querySelector('#__nuxt').children.length`, **tidak pernah** dengan HTTP status. **Dipangkas:** entri jebakan INFRA-CAP ditulis ulang sekitar **45% lebih pendek** — enam jebakan dan bukti pentingnya dipertahankan, penjelasan panjang dibuang, karena write-up lengkapnya sudah ada di `fact_store` dan `MEMORY.md` diinjeksi ke **setiap** turn sehingga budget karakternya adalah sumber daya langka. **Ditambahkan ke USER:** garis keras HIRO antara bug per-halaman dan sisa aplikasi, serta pembedaan yang dia buat antara **"hide"** dan **"delete"** — "bisa di hide aja ... jangan di delete" berarti menekan **tampilan** scrollbar sambil mempertahankan panel tetap bisa digulir, dan dua kata itu tidak bisa dipertukarkan
- [x] **Scrollbar body halaman CCTV disembunyikan, tapi tetap bisa scroll** (2026-10-04) — HIRO meminta **"ok sekarang bisa di hide aja gak scrollbar pada body page cctvacc, jangan di delete"**. `:ui` pada `UDashboardPanel` milik cctvacc menjadi `body: 'cctv-panel-body overflow-y-scroll scrollbar-gutter-stable'`, dan style block non-scoped halaman menyembunyikan **tampilan** bar-nya pada `.cctv-panel-body`: `scrollbar-width: none` plus `-ms-overflow-style: none` untuk Firefox/Edge lama, dan `::-webkit-scrollbar { width: 0; height: 0 }` dengan track serta thumb transparan untuk Chrome/Edge. **Kedua keluarga properti itu perlu**, karena browser yang tidak mendukung keduanya tetap akan menggambar scrollbar. `overflow-y-scroll` **sengaja dipertahankan** — panel body tetap menjadi region yang benar-benar bisa di-scroll, dan wheel, keyboard, serta trackpad tetap berfungsi; hanya **tampilannya** yang ditekan, dan itulah arti "jangan di delete" dari HIRO. Ini justru membuat **fix flicker makin kuat**, bukan lemah: scrollbar selebar nol tidak memakan ruang layout, sehingga gutter terukur turun dari **15px menjadi 0px** — artinya lebar konten tidak mungkin berubah sama sekali. **TERVERIFIKASI:** panel body cctvacc terbaca `hasClass true, overflowY scroll, scrollbarWidth none, gutter stable, reservedPx 0`; Dashboard, User Management, dan Handover semuanya masih `hasCctvClass false, overflowY auto, scrollbarWidth auto`, jadi perubahan ini mustahil bocor. **SCROLLING TETAP JALAN — dibuktikan, bukan diasumsikan:** dengan 10 baris sementara ditambahkan (total 20) dan jendela diperpendek ke **1440×620** supaya panel benar-benar overflow (`scrollHeight 619 > clientHeight 556`), satu peristiwa wheel nyata memindahkan `scrollTop` dari 0 ke **63** — yang adalah tepat nilai maksimum (619−556), jadi panel ter-scroll sampai bawah, dan screenshot memperlihatkan panel sudah bergeser dengan **tidak ada scrollbar tergambar sama sekali**. Catatan: panel **tidak** overflow di 1920×1080 berapa pun jumlah baris, karena tabel dibatasi `max-h-[70vh]` sehingga tinggi konten panel konstan; jalur scroll hanya aktif di jendela pendek. Baris probe dihapus (10 × **204**), kembali ke **10 baris milik HIRO**, filter dan search dibiarkan kosong
- [x] **Fix scrollbar diFOKUSKAN ke halaman CCTV saja** (2026-10-04) — koreksi HIRO: **"waduh kenapa jadi muncul scrollbar disetiap page? yang terjadi flicker hanya di page cctvacc, harusnya lu fokus di page tersebut saja jangan mengubah tampilan page lain"**. **Dia benar:** saya memperbaiki secara global lewat theme override `dashboardPanel` di `app.config.ts`, dan itu membuat scrollbar terlihat permanen juga di Dashboard, User Management, dan Handover — tiga halaman yang tidak pernah punya bug itu. **Dua perubahan global dikembalikan:** blok `dashboardPanel` di `web/app/app.config.ts` (kembali hanya colors + button) dan `html { scrollbar-gutter: stable }` di `web/app/assets/css/main.css` (dihapus). Fix sekarang **per-instance di halaman cctvacc saja**: `<UDashboardPanel :ui="{ body: 'overflow-y-scroll scrollbar-gutter-stable' }">` di `web/app/pages/logbook/cctvacc.vue`. `:ui` bersifat **per instance**, jadi secara struktural tidak bisa bocor seperti theme override. **TERVERIFIKASI scope-nya persis benar:** CCTV Access membaca `overflowY=scroll gutter=stable reserved=15px`, sedangkan Dashboard, User Management, dan Handover semuanya membaca `overflowY=auto gutter=auto` (bawaan vendor), `html scrollbar-gutter` kembali `auto`, dan scrollbar dokumen terukur 0. Dashboard dan User Management saya screenshot untuk memastikan tidak ada strip. CCTV Access tetap berfungsi penuh: toolbar `Filter / Excel / Add Record`, chip Today menyaring 10 baris jadi 6 dengan chip "Date: 2026-10-04 → 2026-10-04", Clear all mengembalikan 10 baris. **PELAJARAN:** pakai mekanisme paling sempit lebih dulu. Theme override di `app.config.ts` adalah jawaban "memperbaiki semuanya sekaligus" — dan itu **insting yang salah** kalau gejalanya spesifik per halaman, karena diam-diam mengubah visual **setiap** layar. **KERUSAKAN SAYA SENDIRI YANG LAYAK DICATAT:** revert `app.config.ts` yang saya skrip Rebuild file itu dengan operasi string yang **menjatuhkan tanda kurung penutup `defineAppConfig`**, sehingga tertinggal `... }\n}` alih-alih `... }\n})`. **Seluruh aplikasi berhenti mount** — `#__nuxt` punya 0 anak dan `__NUXT_DATA__` hanya 28 karakter — padahal `/login` tetap mengembalikan **HTTP 200**, jadi pengecekan status code terlihat sehat. Gejalanya halaman benar-benar kosong tanpa satu pun input, yang terlihat seperti masalah browser atau server, bukan error sintaks. Ketemu lewat `git diff`, bukan dengan menebak. Aplikasi web gagal **SENGAJA** seperti ini: selalu diff file konfigurasi yang diedit tangan, dan setelah memperbaiki halaman kosong periksa `document.querySelector('#__nuxt').children.length` alih-alih memercayai HTTP status
- [x] **Sumber sebenarnya kedipan scrollbar: BODY `UDashboardPanel`, bukan tabel** (2026-10-04) — HIRO akhirnya **menyebut elemennya sendiri**: `#dashboard-panel-v-0-8-0 > div.flex.flex-col.gap-4.sm\:gap-6.flex-1.overflow-y-auto.p-4.sm\:p-6`, plus syarat pemicunya — terjadi begitu tabel **melewati 9 record** dan tabel itu sendiri punya scrollbar. Itu adalah scroll container milik panel, `flex-1 overflow-y-auto`. Dua perbaikan saya sebelumnya (scrollbar-gutter pada container tabel, dan pada `html`) **tidak mungkin menyentuhnya**, karena dia bukan salah satunya. **Terukur sebelum fix:** panel body punya `overflowY auto`, `scrollbarGutter auto`, `scrollHeight 583 > clientHeight 505`, dan scrollbar hidup **15px**. Saat filter mengubah tinggi konten sehingga melintasi ambang itu, scrollbar 15px panel datang-pergi, lebar body berubah, dan seluruh konten halaman meluncur kiri-kanan — persis gejala yang HIRO laporkan. **FIX, global bukan per halaman:** slot `dashboardPanel.body` milik vendor **tidak punya atribut `data-slot`**, jadi tidak bisa ditarget dari CSS halaman tanpa mengikat diri ke string class-nya; **override theme Nuxt UI di `web/app/app.config.ts`** adalah cara yang didukung framework, dan itu memperbaiki **semua panel sekaligus**. Override mengganti slot string menjadi `... flex-1 overflow-y-scroll p-4 sm:p-6 scrollbar-gutter-stable`. **Keduanya perlu, dan justru itu pelajaran dari dua ronde sebelumnya:** `scrollbar-gutter-stable` mereservasi 15px sehingga **tidak ada yang BERGERAK**, tapi tidak menghentikan scrollbar **BERKEDIP**; `overflow-y-scroll` (bukan `auto`) menggambar track secara permanen sehingga tidak bisa muncul-hilang. Terverifikasi terpasang di keempat panel (Dashboard, CCTV Access, User Management, Handover) dengan `overflowY: scroll, scrollbarGutter: stable`. **BATASAN JUJUR: SAYA TIDAK BERAPA-REPRODUKSI KEDIPANNYA DI LOKAL SAMA SEKALI.** Saya membuat 9 baris sementara sampai 18 baris, lalu melakukan A/B dengan menyuntik style runtime yang mengembalikan `scrollbar-gutter` ke `auto` — gutter panel tetap **15px** dan div dalam tetap **x=232**, **baik dengan maupun tanpa** fix, karena Chromium headless ini memakai **overlay scrollbar** yang tidak memakan ruang layout sehingga bug-nya tidak terlihat secara konstruktsi di sini. Fix-nya adalah obat standar yang benar dan angkanya mengonfirmasi sudah terpasang, tetapi **konfirmasi visual harus dilakukan di Chrome Windows milik HIRO** yang memakai scrollbar klasik. Catatan harness: variabel `window` yang di-set oleh `browser_exec` sebelumnya **tidak bertahan** melewati `Page.reload`, yang memicuMiles 404 saat hapus — selalu ambil ulang id entity setelah reload. Baris probe sudah dihapus (9 × **204**) dan tabel kembali ke **9 baris milik HIRO**
- [x] **Kedipan scrollbar itu sendiri dihilangkan, bukan hanya geser layout** (2026-10-04) — HIRO melaporkan scrollbar **"masih muncul sesaat lalu menghilang"** pada chip Today, select Section, dan select PIC Name. **Dia benar:** `scrollbar-gutter: stable` hanya mencegah 15px itu diambil/dikembalikan — itu **tidak** mencegah thumb muncul dan lenyap saat jumlah baris melintasi ambang 70vh. **FIX-nya membuat track selalu digambar:** `overflow-auto` pada container tabel diganti menjadi `overflow-y-scroll` + `overflow-x-auto`, **dikerjakan sebagai utility Tailwind di atribut `class`**, bukan sebagai deklarasi `overflow-y` di dalam CSS halaman — selector kelas biasa seri dengan `.overflow-auto` milik Tailwind pada spesifisitas (0,1,0) lalu kalah pada urutan sumber, jebakan yang sama seperti temuan `-translate-x-1/2` tadi. Jadi track scrollbar kini selalu mereservi ruangnya berapa pun jumlah baris. Styling scrollbar di halaman yang sama: track **12px**, track transparan, thumb `--ui-border-strong` dengan border transparan 2px sehingga thumb terlihat setebal 8px, hover `--ui-text-dimmed`, ditambah `scrollbar-width: thin` / `scrollbar-color` untuk Firefox. Versi pertama memakai border 3px di track 10px sehingga hanya menyisakan 4px — crop 2× memperlihatkan itu praktis tak terlihat, dan itu **menukar kedipan dengan user tidak tahu tabel bisa digulir**, WHICH adalah trade yang salah. **BATASAN LINGKUNGAN UJI SAYA:** Chromium headless di sini memakai **overlay scrollbar**, sehingga thumb tidak muncul di screenshot dan `::-webkit-scrollbar` diabaikan untuk layout (dideklarasikan 12px, `offsetWidth - clientWidth` tetap mengukur 10). Jadi saya **TIDAK bisa** mengonfirmasi thumb secara visual di sini; di Chrome Windows milik HIRO track-nya adalah scrollbar klasik yang memakan ruang layout dan akan terlihat. Yang bisa diverifikasi — dan sudah — adalah bahwa **tidak ada yang bergerak**. **VERIFIKASI FINAL di 1920×1080** lintas ketiga skenario yang disebut HIRO (chip Today, select Section, select PIC Name) plus Clear all dan mengetik di search: gutter dalam terpaku di satu nilai, kolom PIC SIGN terpaku di satu nilai (**1074**), gutter dokumen **0** sepanjang, jumlah baris berubah 12→7, 12→1, dan kembali — **STABLE di semua**
- [x] **Flicker scrollbar saat filter mengubah jumlah baris — dihilangkan** (2026-10-04) — HIRO melaporkan **"ketika saya klik chip Today, ada scrollbar flicker di luar tabel / pada bagian kanan layar"**. **AKAR MASALAHNYA, direproduksi dan diukur bukan ditebak:** tabel berada di dalam `.max-h-[70vh] overflow-auto` (kini `.logbook-scroll`). Rekaman saat klik Today memberi `t=0ms innerScrollbar=15px, PIC SIGN x=1079`, lalu `t=281ms innerScrollbar=0px, PIC SIGN x=1086`. Saat filter menyusutkan baris di bawah 70vh, **scrollbar di dalam hilang, container jadi 15px lebih LEBAR**, dan karena kolomnya `table-fixed` dengan persentase, seluruh kolom **berbagi ulang** — jadi seluruh grid meluncur sideways dalam satu frame; di tepi kanan layar itu terlihat sebagai scrollbar yang berkedip lenyap. Catatan: bug ini hanya muncul bila baris cukup banyak untuk menggulir; dengan segelintir baris dia tidak terlihat sama sekali. **FIX:** `scrollbar-gutter: stable` di **dua** tempat. `.logbook-scroll { scrollbar-gutter: stable }` di cctvacc.vue mereservasi gutter dalam secara permanen, dan `html { scrollbar-gutter: stable }` di `web/app/assets/css/main.css` melakukan yang sama untuk scrollbar **dokumen**, yang berganti-ganti setiap kali baris chip filter tumbuh dan menyusut (terukur 0 → 38px) pada jendela yang tingginya mendekati tinggi konten. Scrollbar tetap muncul dan hilang seperlunya — hanya saja tidak lagi menggeser apa pun. **TERVERIFIKASI di 1920×1080:** kolom PIC SIGN bertahan tepat di **x=1071** lintas Today, This month, This week, All time, Clear all, dan mengetik di search, dengan gutter dalam terpaku di **15px** di semua kasus (STABLE di enam skenario, sebelumnya melompat 7px). Juga diverifikasi di **1440×900, 1440×760, dan 1440×640**: kolom x tetap **839** di ketiganya, scrollbar dokumen 0 → 0 sepanjang. Filter, search, dan chip dibiarkan kosong; 12 baris utuh
- [x] **Animasi modal diperhalus berdasarkan pengukuran velocity** (2026-10-04) — HIRO meminta **"buat lebih smooth animasinya"**. Perbaikannya datang dari **MENGUKUR profil kecepatan**, bukan menebak angka. Yang terekam frame-by-frame dari versi pertama — semuanya tidak terlihat oleh mata: (1) opacity mencapai **1,00 di t=284ms** sementara dialog masih bergerak sampai **t=468ms**, jadi selama **184ms** konten meluncur **tanpa fade** pendukungnya; (2) kurva posisi `cubic-bezier(0.34, 1.36, 0.52, 1)` punya **y1 > 1**, jadi overshoot ke −0,52px lalu butuh **134ms lagi** untuk bergoyang balik ke nol — **melintas lalu menggantung di target itulah yang terbaca sebagai "belum settle"**; (3) kecepatannya **tidak monotonik** (−2,26, −2,07, **−3,40**, −1,34 px/frame) — ada lonjakan percepatan di tengah gerakan; (4) kurva per-field `cubic-bezier(0.22, 1, 0.36, 1)` punya kemiringan awal `1/0,22 = 4,5`, sehingga field yang bangun **melompat 0,23 opacity dalam SATU frame**. **FIX:** posisi dan fade kini menjadi **dua animasi terpisah dengan kurva dan durasi berbeda**, sehingga fade selesai sementara slide masih melambat. Semua kurva menjaga setiap titik kontrol y di bawah 1, yang menjamin gerak **monotonik** — settle, bukan bergoyang. `cubic-bezier(0.3, 0, 0.2, 1)` dipakai untuk kedua track posisi: kemiringan awal nol (tanpa jerk di frame pertama) dan kemiringan akhir nol (tanpa snap di frame terakhir), dengan puncak di tengah. Keluar **sengaja dipercepat** (`cubic-bezier(0.4, 0, 1, 1)`), sehingga modal pergi tegas, bukan menggantung. **TERVERIFIKASI setelah:** kecepatan dialog `0,00 → −0,08 → −0,27 → −0,52 → −0,81 → −1,09 → −1,30 → −1,35 (puncak) → −1,27 → −1,15 → −1,00 → −0,85 → −0,73 → −0,62 → −0,54 → −0,46 → −0,40 → −0,33 → −0,28 → −0,24 → −0,20 → −0,16` — monotonik, tanpa overshoot, tanpa spike; fade selesai di **t=362ms** sementara slide berjalan sampai ~440ms, jadi **fade mendahului slide**. Opasitas field naik progresif `0,003 → 0,032 → 0,102 → 0,221 → 0,384 → 0,553 → 0,696 → 0,805 → 0,884 → 0,938` dan lompatan terburuk per frame turun dari **0,230 → 0,169**. Total urutan justru **lebih pendek sekaligus lebih halus**: delay stagger dirapatkan dari 40/85/130/175/220/265/300ms menjadi **0/38/76/114/150/182/210ms**, sehingga field terakhir selesai di 530ms, bukan 600ms. Pemusatan tetap utuh (centreX 632 = pusat viewport, `translate: -50% -50%` dengan `transform matrix(1,0,0,1,0,0)` saat diam) dan semua field mencapai opacity 1. **ATURAN UMUM:** kurva springy yang overshoot terasa hidup sendirian, tetapi di dalam dialog terbaca ceroboh karena tidak ada elemen lain di layar yang mengalihkan perhatian dari goyangnya. Simpan overshoot untuk elemen dekoratif kecil (bounce panel User, yang memang HIRO minta) dan jadikan dialog selalu monotonik
- [x] **Reveal halus pada modal New/Edit Record** (2026-10-04) — HIRO meminta **"add effect motion smooth reveal pada komponen modal new record"**. Dibuat sebagai CSS di `web/app/pages/logbook/cctvacc.vue`, dengan dua marker class yang diteruskan dari template: `cctv-record-modal` pada UModal lewat `:ui="{ content: 'sm:max-w-4xl cctv-record-modal' }"` dan `cctv-record-grid` pada grid 3 kolom form. **Dua lapis:** dialognya sendiri `cctv-modal-reveal` (opacity 0→1 + translateY 12px→0, 340ms, `cubic-bezier(0.34, 1.36, 0.52, 1)` sehingga kurvanya sedikit overshoot) plus `cctv-modal-dismiss` yang lebih cepat saat menutup; lalu field-fieldnya **stagger** masuk sesuai urutan baca lewat `cctv-row-reveal` dengan delay 40/85/130/175/220/265/300ms dan `animation-fill-mode: both` sehingga tiap field menunggu di posisi awal sampai gilirannya. **FAKTA KRITIS, diukur bukan diasumsikan, dan itulah yang membuat ini aman:** vendor memusatkan dialog ini dengan `left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2`, dan **Tailwind v4 men.emit itu sebagai properti `translate` yang berdiri sendiri** — computed style-nya `translate: -50% -50%` dengan `transform: none`. Jadi menganimasikan `transform` itu **berkomposisi (compose) dengan** `translate`, bukan menimpanya, sehingga dialog tetap terpusat di setiap frame. Kalau pemusatannya memakai bentuk `transform` yang lama, keyframe yang sama akan melempar dialog ke sudut kiri atas. **Hanya translate + opacity, tidak pernah scale**, demi alasan re-rasterisasi/ghost text yang sama seperti bounce panel User; spring-nya datang dari kurva. Untuk mengalahkan utility vendor `data-[state=open]:animate-[scale-in_200ms...]` yang spesifisitasnya (0,2,0), selector-nya sengaja membawa **tiga komponen**: `.cctv-record-modal[data-slot='content'][data-state='open']` = (0,3,0), sehingga transisi vendor **tidak pernah dimatikan** dan timing focus-trap-nya tetap terjaga. CSS-nya diletakkan di dalam `<style>` non-scoped yang sudah ada, karena UModal teleport ke body dan aturan scoped tidak akan pernah sampai ke elemen content. `prefers-reduced-motion` dan print sama-sama membatalkan animasinya. **TERVERIFIKASI lewat rekaman rAF:** dialog opacity 0→1 dengan translateY `12 → 9,76 → 7,71 → 5,85 → 4,26 → 2,91 → 1,83 → 0,98 → 0,35 → −0,08 → −0,34` (overshoot sungguhan) sementara **`centreX` tetap 632 di setiap frame** — bukti pemusatan selamat; opasitas field terekam `0/0/0/0/0 → 0.14/0/0/0/0 → 0.67/0.21/0/0/0 → 0.84/0.58/0.04/0/0 → 0.98/0.95/0.87/0.66/0.19`, stagger yang bersih. Edit Row memakai UModal yang sama dan mendapat animasi yang sama. Data tidak disentuh: 12 baris, search dan filter dibiarkan kosong. **JEBAKAN HARNESS YANG BIKIN 500 DI SINI:** menambahkan CSS di akhir file `.vue` yang sudah diakhiri `<style>` akan meletakkannya **DI LUAR** block itu dan Vite gagal dengan "Failed to fetch dynamically imported module" — halamannya hanya menampilkan `h1` bertuliskan "500". Selalu selipkan **sebelum** `</style>`. Catatan lain: regex seperti `<style[^>]*>` memberi false positive bila sebuah komentar CSS menyebut teks literal `<style>`
- [x] **Sinkronisasi memory 2026-10-04** — `MEMORY.md` (lapisan yang **diinjeksi tiap turn**) dan `USER` sama-sama **belum disentuh** sepanjang sesi ini; semua turn sebelumnya hanya menyinkronkan `fact_store` + `PROJECT-SUMMARY.md`, yaitu lapisan deep-recall dan cadangan. Perintah "update memory" HIRO促使 pengisian lapisan yang tiap turn ikut terbaca juga. **Yang ditambahkan ke MEMORY:** enam jebakan INFRA-CAP yang tidak boleh dimunculkan lagi — search **tidak boleh** pernah mencocokkan kolom tanda tangan base64; auto-kapitalisasi **tidak boleh** menyentuh `/login` dan `/users`; persentase pada `<th>` hanya hint sehingga wajib `table-fixed` + `<colgroup>`; lebar popover bersarang milik **div panel dalam**; Tailwind v4 tidak menghasilkan utilitas `fill-*` semantik; API membatasi `pageSize` ke 500. Juga aturan **ukur di resolusi asli 1920** sebelum menyatakan perbaikan layout beres, dan aturan **"kebetulan yang cocok dengan gejala yang diharapkan bukanlah tes yang lulus"**; catatan pemulihan daemon browser serta rAF-recorder dan `innerText` yang melaporkan huruf besar; dan entri current-state yang menyebut route, toolbar `Filter | Excel | Add Record`, 为什么 `@media print` sengaja dipertahankan setelah tombol Print dihapus, satu computed `visibleRows` dipakai bersama oleh tabel/filter/export, dan route hapus `/api/records/{entityId}/{recordId}`. **Yang ditambahkan ke USER:** laporan bug satu-dua kata dalam bahasa Indonesia ("ngawur", "bahaya!", "masih sama") adalah **diagnosis literal** yang layak dijalankan sampai ke akar masalah — dan HIRO membedakan dead code dari kode yang kehilangan pemanggil
- [x] **Search cctvacc memperbaiki diri: tidak lagi mencocokkan base64 tanda tangan** (2026-10-04): tidak lagi mencocokkan base64 tanda tangan** (2026-10-04) — HIRO melaporkan hasil search **"masih ngawur"**. **BUG-nya, dibuktikan terhadap data nyata HIRO bukan ditebak:** `visibleRows` menyaring baris dengan `JSON.stringify(r.values).toLowerCase().includes(q)`, dan `r.values` memuat **kedua kolom tanda tangan sebagai data-URL PNG base64** sepanjang kurang lebih 2000 karakter masing-masing. Jadi pencarian itu **mencocokkan base64-nya sendiri**. Diukur sebelum/sesudah pada 12 baris miliknya: `sya` mengembalikan **4 baris** bukan 1 (Syaifuloh), `06` mengembalikan **10** bukan 2, dan yang paling menentukan `zzz` mengembalikan **2 baris padahal seharusnya NOL** — `zzz` tidak mungkin ada di field mana pun, jadi dua hasil itu murni derau tanda tangan. **Yang memperburuk:** search juga memanggil ulang API dengan debounce 300ms (`watch(search)` → `loadRows()` dengan param `search`) sementara computed menyaring ulang baris yang sama di klien — jadi **setiap ketikan mengunduh ulang seluruh register** termasuk ±4KB base64 per baris, dan dua request yang berlomba bisa mendarat tidak berurutan sehingga meninggalkan baris basi di layar. **Catatan penting: pencarian di server justru benar sejak awal** — `DynamicRecordService.ListAsync` hanya mencari field `IsSearchable` bertipe Text/TextArea/Email — jadi yang rusak hanya sisi klien. **FIX:** (1) `NON_SEARCHABLE = new Set(['tanda_pemohon','tanda_isd'])` plus `searchHaystack(values)` yang menyambung hanya nilai field nyata, melewati null/undefined; (2) `watch(search)` beserta bolak-balik ke API **dihapus** — search kini computed di klien seperti filter, jadi instan, tanpa payload, dan tidak bisa balapan; (3) komentar yang menyesatkan diperbaiki: halaman meminta `pageSize: 1000` tetapi `DynamicRecordService` membatasi ke `1..500`, jadi sebenarnya ia diam-diam mendapat 500; komentar sekarang menyebut 500 sebagai batas keras dan menyuruh menaikkan batas API lebih dulu bila register tumbuh melampaui itu. **TERVERIFIKASI:** `jono`→1, `sya`→1 (sebelumnya 4), `zzz`→**0** (sebelumnya 2), `06`→2 dan kedua hasilnya dikonfirmasi benar-benar baris **06 Okt** (Syaifuloh/Blasting, Kanri/ISD), `stacking`→2 (Rosmiwati, Ngizzatuloh), `ngizz`→1. Mengetik 5 karakter kini Issuing **NOL** fetch call (sebelumnya satu unduhan register penuh per jeda). Mengosongkan search mengembalikan 12 baris dengan footer "12 rows / Showing 12 of 12", dan Filter popover tetap bekerja mandiri ("This month" → 7 baris dengan chip "Date: 2026-10-01 → 2026-10-04"); filter dibiarkan kosong. Catatan harness: saat daemon browser macet dengan `PermissionError` di `bu-default.port`, jalankan `taskkill chrome.exe` dan `msedge.exe` lalu hapus file port tersebut
- [x] **Tombol Print dihapus, Add Record dan Excel diswap** (2026-10-04) — HIRO: **"hapus tombol print saya tidak butuh hapus juga codenya biar tidak meninggalkan deadcode. lalu swap posisi tombol add record dan excel"**. Toolbar sekarang **Filter | Excel | Add Record** — "Excel" tetap `soft` supaya keduanya masih terbaca sebagai satu kelompok, dan aksi utama Add Record berakhir paling kanan di tempat mata mendarat terakhir. Fungsi `printSheet()` dihapus bersama tombolnya; pencarian menyeluruh `printSheet`, `i-lucide-printer`, dan `label="Print"` di `web/app` sekarang mengembalikan **nihil**. **PEMBEDAAN PENTING yang saya angkat dan HIRO setujui: blok CSS `@media print` DIPERTAHANKAN dan itu BUKAN dead code.** Aturan itu tetap membentuk lembar untuk **Ctrl+P** yang dipicu browser — aplikasinya saja yang tidak lagi menawarkan tombolnya. Menghapus aturan tersebut akan membuat Ctrl+P mengeluarkan tabel **terpotong dan tanpa garis**, yaitu regresi nyata yang dibayar tanpa imbalan. Tersisa **empat** blok `@media print` dan semuanya bearsa: mematikan animasi untuk print/reduced-motion, guard `filter: none` agar tinta putih-di-kertas-putih tidak tercetak kosong, pentralisasi `.print:scroll-area` / `.print:sticky-head`, serta `@page` A4 landscape plus grid hitam solid. **TERVERIFIKASI:** urutan toolbar terbaca **Filter / Excel / Add Record**, tidak ada tombol Print, tidak ada ikon printer di DOM, tabel tetap ter-render, dan **export Excel tetap berfungsi setelah dipindah** (blob ter-intercept **81.587 byte**, MIME spreadsheet benar, ZIP magic `PK\x03\x04`) sehingga pemindahannya tidak merusak handler-nya
- [x] **DatePicker bertema dipakai juga di form Add/Edit Record** (2026-10-04) — HIRO meminta datepicker pada form add/edit record adapt sama dengan yang di filter. `UInput type="date"` di field Date modal diganti `<DatePicker v-model="form.tanggal" name="tanggal" placeholder="Pick a date" :invalid="fieldInvalid('tanggal')" />`. v-model berupa `yyyy-mm-dd` polos yang **persis sama** dengan bentuk `form.tanggal` yang sudah ada, jadi tidak ada plumbing tanggal yang berubah dan API tetap menerima `2026-10-20T00:00:00`. DatePicker mendapat prop baru `invalid?: boolean` yang memasang **cincin merah yang sama** dengan field wajib lainnya, karena trigger `UButton` tidak bisa menerima `:ui="{ base: 'ring-2 ring-error' }"` seperti `UInput`. **Picker terbuka di dalam modal tanpa meluber:** panel terukur **256×318** di y=198 dengan `panelBottom 516` vs `modalBottom 536` pada viewport 1280×569 — jadi sepenuhnya di dalam dialog; bahaya **popover bersarang** yang pernah menimpa TimePicker **tidak terulang**, karena DatePicker sudah meletakkan lebarnya di panel dalam. **TERVERIFIKASI END-TO-END ke API sungguhan, bukan dikira-kira:** Create dengan tanggal 12 Okt 2026 tersimpan dan kembali dari `/api/records` sebagai `tanggal "2026-10-12T00:00:00"` dengan `nomor "12"` otomatis dan kedua tanda tangan tersimpan (1934 / 1938 karakter); tabel merender "12 Okt 2026". Edit terbuka ter-prefill "12 Oct 2026", diubah ke 20 Okt tersimpan `"2026-10-20T00:00:00"`, **nomor "12" dipertahankan** (auto-number tersembunyi) dan kedua tanda tangan **utuh tidak berubah**. Validasi wajib tetap jalan meskipun DatePicker tidak merender hidden input: dengan tanggal dikosongkan peringatan berbunyi **"These fields are required: Date, Section, Employee No, PIC Name, Purpose / Details, PIC Sign, ISD Sign"** — **Date masuk daftar**, dan trigger-nya membawa cincin merah. Baris uji lalu dihapus; kembali ke **11 baris milik HIRO** (Ismedra, Gunawan, Simbol, Rosmiwati, Poki, Ayu, Kanri, Ngizzatuloh, Jono, Riswantod, Mahmudin). Dua jebakan harness yang layak diingat: route hapus adalah `/api/records/{entityId}/{recordId}` — memanggil `/api/records/{recordId}` mengembalikan **405**; dan header hari DatePicker di DOM tertulis "Mo Tu We" tetapi `innerText` melaporkannya **HURUF BESAR** oleh CSS, jadi pilih kalender yang terbuka lewat `div.w-64` di dalam `[data-slot=content]`, jangan mencocokkan huruf hari
- [x] **Auto-kapitalisasi DILARANG di `/login` dan `/users`** (2026-10-04) — koreksi eksplisit HIRO: **"jangan terapkan plugin ini pada halaman login dan user management. bahaya!"** Dia benar, dan saya terlalu longgar saat mengirimnya lebih dulu — saya Luck describing risikonya sebagai sekadar teoretis karena **"login tetap berhasil"**. Ditambahkan `EXCLUDED_PATHS = ['/login', '/users']` di `web/app/plugins/autocapitalize.client.ts`, diperiksa di awal handler `input` lewat `window.location.pathname`, cocok persis atau prefix subpath. **KENAPA KESELURUHAN HALAMAN, bukan opt-out per field:** kedua halaman itu deals dengan **IDENTITAS**, bukan prosa, dan username adalah **kredensial**, bukan keterangan. Di `/users` bahayanya adalah **kerusakan data**, bukan解决方法 kosmetik — mengapitalkan di sana berarti membuat user "budi" akan tersimpan **"Budi"**, dan sejak itu orang itu tidak bisa login dengan username yang diberikan. Di `/login` artinya aplikasi berhenti membandingkan yang diketik dengan yang tersimpan — yang kebetulan jalan hanya karena API kebetulan membandingkan username case-insensitive, dan itu **kebetulan, bukan jaminan**. Mengecualikan seluruh halaman sekaligus juga menutupi field apa pun yang ditambahkan ke kedua layar itu di kemudian hari, yang tidak bisa dijamin atribut `data-no-capitalize` per-field. **TERVERIFIKASI setelah perubahan:** username `/login` `budi santoso` tetap huruf kecil dan login dengan `admin` berhasil — jadi formnya kini mengirim persis seperti diketik; panel Add `/users` username `budi`, nama lengkap `siti aminah`, email `budi@corp.id` semuanya tetap huruf kecil; `/logbook/cctvacc` masih kapital seperti biasa (search `final inspection` → `Final inspection`, PIC Name `ngizzatuloh` → `Ngizzatuloh`). Tidak ada yang disimpan saat pengujian. **Pelajaran ke depan:** default-nya mengecualikan permukaan identitas dan kredensial dari perilaku apa pun yang menulis ulang input, tanpa perlu diminta
- [x] **Semua textbox otomatis kapitalkan huruf pertama** (2026-10-04) — HIRO meminta **"untuk semua textbox pada project ini pastikan huruf pertama otomatis convert ke kapital"**. **DIBUAT SEBAGAI DELEGATED LISTENER, BUKAN PER KOMPONEN:** plugin baru `web/app/plugins/autocapitalize.client.ts` memasang **satu** listener `input` di `document` pada fase **capture**. Ini penting karena setiap field teks di project ini adalah `UInput`/`UTextarea` yang merender `<input>` sendiri di dalamnya — helper v-model atau directive per call-site harus ditambahkan satu per satu, dan halaman baru berikutnya akan diam-diam melewatinya. Fase capture dipilih sengaja: huruf pertama diperbaiki **sebelum** handler v-model milik Vue membaca nilainya, sehingga model tidak pernah melihat versi huruf kecil dan tidak ada watcher yang menyala dua kali. **TIGA JEJAK YANG SECARA SENGAJA DIHINDARI, masing-masing terverifikasi di browser:** **(1) PASSWORD DILEWATI** — mengapitalkan password mengubah kredensial dan mengunci user keluar, karena hash BCrypt dibuat dari versi huruf kecil. Terbukti: mengetik `admin@123` tetap `admin@123`. **(2) DATA TERSIMPAN TIDAK PERNAH DITULIS ULANG** — hanya yang diketik *user* yang disentuh. Menulis ulang nilai yang sudah ada akan diam-diam mengganti nama akun (buka Edit pada user "budi", kotaknya menampilkan "Budi", di-save nama stored berubah, dan user itu tidak lagi bisa login dengan nama yang dia kenal). **Terbukti:** membuka Edit pada baris yang tersimpan sebagai `tujuan="biasa quality issue bro"` menampilkan persis string huruf kecil itu, tidak berubah. **(3) EMAIL juga dilewati** selain password, agar `budi@company.local` tidak pernah jadi `Budi@company.local`. Leapatan lain: `url`, `tel`, `number`, `date`, `time`, `datetime-local`, `month`, `week`, `search`, `color`, `range`, `file`, `hidden`. **ESCAPE:** atribut `data-no-capitalize`, dan `autocapitalize="off"/"none"` bawaan dihormati. Dua detail kecil: keystroke dengan `isComposing` diabaikan agar jendela kandidat IME tidak hancur di tengah komposisi; dan karakter yang kapitalnya **berbeda panjang** (`ß` → `SS`, `ﬁ` → `FI`) dibiarkan, karena menulis ulang itu mengubah panjang string — dan вместе itu posisi caret. Selain itu tidak perlu perbaikan caret, karena substitusinya selalu tepat satu karakter. **TERVERIFIKASI:** username login `budi santoso` → `Budi santoso`, dan **MASUK BERHASIL** meski field mengirim `Admin` alih-alih `admin` — ini membuktikan API membandingkan username secara **case-insensitive**, layak diingat kalau itu suatu saat berubah. Modal CCTV: `departemen`, `no_pegawai`, `nama_pemohon`, `tujuan` (textarea) dan `pic_isd` semuanya kapital; field tanggal dilewati. Panel User: username → `Operator01`, nama lengkap → `Siti aminah`, password tidak tersentuh, email tidak tersentuh. **Tidak ada yang disimpan saat pengujian** — tabel CCTV dan users dibiarkan persis seperti semula
- [x] **Kolom tanda tangan baru benar-benar sama di semua lebar layar** (2026-10-04) — HIRO melaporkan PIC Sign masih terlihat lebih lebar. **Akar masalahnya ternyata `table-fixed`.** Dengan layout **auto** bawaan, `w-[n%]` pada setiap `<th>` itu hanya **hint**, dan browser membagi sisa ruang **tidak rata**. Terbukti: **identik** di viewport sempit 1280px (132/132) tetapi **175px vs 132px** di layar kantor 1920px — padahal keduanya sama-sama mendeklarasikan `w-[11%]`. Jadi perbaikan chip `min-w` sebelumnya memang terlihat berhasil justru karena **saya mengujinya di viewport sempit**, bukan di resolusi asli HIRO. **Pelajaran: selalu verifikasi lebar kolom di 1920×1080 lewat `cdp Emulation.setDeviceMetricsOverride` sebelum menyatakan perbaikan lebar berhasil.** Fix: `<table class="table-fixed">` + `<colgroup>`. Dengan `table-fixed` persentase jadi otoritatif; karena kedua kolom tanda tangan mendeklarasikan **persentase yang sama**, keduanya diskalakan faktor yang sama dan karena itu **tetap sama persis di semua lebar layar**. **TERVERIFIKASI `diff=0` di 1280 (117/117), 1440 (117/117), 1600 (133/133) dan 1920 (165/165)**, tanpa horizontal overflow dari 1440 ke atas. **Bug yang saya buat sendiri dan langsung tertangkap:** `<colgroup>` pertama berisi **12 `<col>` untuk 11 kolom**, sehingga `Actions` diam-diam dapat 11% bukan 6% dan kolom Start/End Time melebar jadi 142px. Kedua kolom tanda tangan tetap sama di versi itu, tetapi **hanya secara kebetulan** — kesamaan itu bukan hasil yang dirancang. Pelajaran yang layak diingat: **kebetulan yang cocok dengan gejala yang diharapkan bukanlah tes yang lulus — hitung elemennya.** Guard print juga Finally dibuktikan benar-benar aktif dengan menyimulasikan media print lewat `cdp Emulation.setEmulatedMedia`: filter signature `invert(1)` di layar dan `none` di media print, background chip tetap transparan di keduanya, dan kolom tetap 165/165
- [x] **Signature di tabel: tinta dibalik, bukan kertas putih** (2026-10-04) — HIRO **menolak** solusi chip putih: **"saya tidak mau pakai bg-white"**. Pendekatan yang dia minta dan sekarang dipakai: **balik tintanya, bukan kertasnya.** `dark:invert` pada `<img>` = `filter: invert(1)`, yang membalik kanal RGB (hitam → putih) **serta membiarkan kanal ALPHA apa adanya** — jadi sel tetap memakai background tema dan tidak perlu piring putih di belakangnya. **TERVERIFIKASI:** computed `filter` = `invert(1)` di dark dan `none` di light, background chip `rgba(0,0,0,0)` di keduanya, dan screenshot menampilkan tinta putih di atas gelap / hitam di atas terang. Murni presentasi — PNG yang tersimpan tidak disentuh. **KONSEKUENSI PENTING yang saya angkat dan sudah di-guard:** tinta putih di kertas putih itu **tidak terlihat**, sehingga hasil print akan menangkap kolom tanda tangan kosong pada register bertanda tangan — persis kegagalan yang_register ini誕生cipkan untuk cegah. Ditambahkan `@media print { tbody img[alt="signature"] { filter: none !important } }` supaya print selalu memakai tinta hitam asli — sudah diverifikasi ada di stylesheet. **Lebar kolom:** kedua sel tanda tangan kini memakai chip **`w-[108px]` fixed, bukan `min-w`** — persentase pada `<th>` hanya *hint*, sehingga baris yang kebetulan punya tanda tangan ter-render lebih lebar daripada tetangganya yang cuma menampilkan "—". Kedua kolom kini tepat **132px**, di dark maupun light
- [x] **Tiga perbaikan: lebar kolom PIC Sign, tooltip collapse stuck, signature pucat di dark mode** (2026-10-04) — **(1) Kolom tanda tangan terlalu sempit, diukur bukan ditebak:** persentase pada `<th>` itu hanya **hint**, dan kedua kolom tanda tangan sama-sama mendeklarasikan `w-[8%]` padahal hasil render-nya **84px dan 58px** — membuat gambar terjepit hanya **60px**. Fix: `min-w-[104px]` pada chip putih pembungkus `<img>` (jadi dijamin punya ruang/ecara apa pun browser menormalisasi persentasenya), plus kedua kolom dinaikkan ke `w-[11%]` dengan mengambil ruang dari Purpose / Details (22% → 18%, kolom itu paling lega). Sekarang **129px dan 128px** — sama besar, seharusnya begitu dari awal. **(2) Tanda tangan pucat di dark theme:** PNG yang tersimpan adalah **tinta hitam di atas background TRANSPARAN**, karena background putih pada canvas SignaturePad itu CSS — dan CSS tidak ikut ter-*capture* oleh `toDataURL`. Di atas tabel gelap itu tampil sebagai **bercak pucat**, bukan tanda tangan yang terbaca. Fix: tabel sekarang menampilkan tanda tangan di **chip putih** (`bg-white dark:bg-white`), jadi tinta-di-kertas di **kedua tema**, sama seperti pad di form entry. Data tersimpan tidak disentuh sama sekali. **(3) Tooltip tombol collapse stuck di "Expand":** akar masalahnya `:key="bounceKey"` yang me-remount tombol — `MutationObserver` dibuat **sekali** di `onMounted`, jadi sejak remount pertama ia mengawasi node yang sudah **detached**; perubahan `aria-label` pada tombol hidup tidak pernah terlihat, `collapsed` membeku, dan tooltip-nya macet. **Ini mode kegagalan yang sama persis** dengan composable sidebar lama, dan alasan spring dibangun ulang dengan CSS murni: remount + observer tidak bisa digabung. Fix: observer sekarang hidup di callback template-ref `setCollapseBtn` yang **disconnect observer lama dan memasang yang baru ke elemen BARU tiap mount**, ditambah pembacaan ulang `nextTick` karena vendor mungkin menyetel `aria-label` satu tick setelah mount. **TERVERIFIKASI lewat 6 klik berturut-turut:** sidebar 64 → title **"Expand sidebar"**, 208 → **"Collapse sidebar"**, bergantian benar setiap kali
- [x] **Bounce gentle saat panel Add/Edit User tampil** (2026-10-04) — HIRO meminta bounce setelah panel tampil, dengan syarat **"jangan terlalu kuat efeknya"**. Konteks yang perlu diingat: **panel di halaman users itu BUKAN UModal** — melainkan `<UCard v-if="modalOpen" class="mt-6">` inline, dan memang **harus** begitu karena `#footer` slot UModal menelan `@click` tombol submit (terverifikasi: `onclick` null di node hasil render). Sisi buruknya: panel itu **tidak punya transisi sama sekali**, hanya muncul mendadak. Fix: animasi CSS `modal-bounce-in` pada kelas `.modal-bounce`, **300ms**, `translateY(14px) → 0` plus fade opacity. **HANYA TRANSLATE — bounce-nya datang dari kurva back-out yang overshoot** `cubic-bezier(0.34, 1.4, 0.52, 1)`, **bukan** dari scale. Scaling memaksa browser me-raster ulang teks, dan itu persis penyebab bug "ghost text" di sidebar; translate memindahkan piksel yang sudah di-raster, jadi rasa spring-nya sama tetapi teks tetap tajam. Jarak tempuh **14px** dengan overshoot **di bawah 1px** menjaga efeknya lembut — sesuai permintaan HIRO. `prefers-reduced-motion` membatalkannya. **TERVERIFIKASI lewat rekaman rAF pada kedua panel:** Add User `14 → 10,94 → 8,22 → 5,84 → 3,86 → 2,29 → 1,07 → 0,22 → −0,33 → −0,62 → −0,73` (melewati target) lalu settle di `transform: none`; Edit User memakai kurva yang sama dan ter-prefill benar dengan `operator01`. Hanya animasi masuk, tanpa animasi keluar — konsisten dengan keputusan pada baris tabel. Catatan: tombol Edit berikon saja tanpa `aria-label` maupun teks, jadi probe dengan `/Edit/` tidak menemukan apa pun — pilih berdasarkan posisi baris
- [x] **DatePicker kustom menggantikan input date native** (2026-10-04) — HIRO meminta tampilan datepicker di filter yang "keren". Komponen baru `web/app/components/DatePicker.vue`, bahasa desain sama dengan TimePicker: UPopover bertema dengan token semantik Nuxt UI sehingga ikut accent dan mode terang/gelap. **KENAPA BUKAN `<input type="date">`:** kontrol OS itu sama sekali tidak bisa distile, merender chrome browser sendiri plus ikon kalender yang tidak bisa distile, dan di mesin ini menampilkan urutan **US mm/dd/yyyy** padahal lembar kertas ditulis **dd/mm/yyyy**. **Desain:** header dengan prev/next bulan, baris hari kerja mulai **Senin**, grid **42 sel (6 minggu) tetap** supaya kalender tidak berubah tinggi saat paging, hari ini ditandai **ring primary**, dan **kedua ujung rentang diberi isian primary solid dengan hari di antaranya diberi tint** — sehingga rentang From..To terbaca sebagai satu pita, bukan dua tanggal terpisah. v-model berupa `yyyy-mm-dd` polos atau kosong. `parseIso` menyusun tanggal dari bagian **lokal**, alasan yang sama seperti perbaikan form entry: `new Date('2026-10-04')` di-parse sebagai UTC dan jatuh ke hari sebelumnya di WIB. **Dua bug yang ketahuan lewat pengukuran, bukan penglihatan:** (1) `w-64` pada `:_ui` content **diabaikan** karena picker ini **popover bersarang** dan vendor membatasi lebar content popover bersarang — panel keluar hanya **173px**, tiap sel cuma **19,6px** sehingga angka dua digit berdempetan. Fix: lebarkan diletakkan di div panel dalam (`w-64 p-3`) dan content disetel `w-max`. Sesudah fix: panel **256px**, sel **31×32**. (2) sorotan `between` butuh **kedua** ujung rentang, tapi picker From hanya menerima `rangeEnd`, sehingga hari di dalam pita tidak pernah menyala; sekarang kedua picker menerima kedua ujung. **TERVERIFIKASI:** **0** input date native tersisa di halaman; pilih 10 Okt lalu 20 Okt menghasilkan chip "Date: 2026-10-10 → 2026-10-20"; ujung 1 dan 4 solid sementara 2 dan 3 bertint; animasi paging bulan jalan (leave slide `0 → −1,78 → −5,34 → −8,64 → −10,89`, bulan baru masuk dari kanan). Sekalian memperbaiki **bug accessibility** yang saya temukan saat mengecek: karena slot chip harus selalu ada untuk animasi tinggi, label "Filtered by" dan tombol "Clear all" **tetap ada di DOM pada tinggi 0px** — screen reader tetap membacanya dan pengguna keyboard bisa fokus ke tombol yang tak terlihat. `v-if` sekarang diletakkan di **baris dalam**, bukan di slot; track tetap beranimasi karena kelas dan konten berubah dalam update yang sama
- [x] **Tabel tidak lagi melompat saat chip filter muncul** (2026-10-04) — HIRO melaporkan tabel bergeser ke bawah dengan tidak smooth. Baris chip sebelumnya dipakai `v-if` langsung di dalam flow, jadi begitu satu chip muncul tabel langsung terdorong **satu line-height dalam satu frame** — lompatan. **Fix: baris itu selalu ada, hanya TINGGI-nya yang dianimasikan**, lewat `grid-template-rows: 0fr → 1fr` pada wrapper `.chips-slot` (kelas `.is-open` menyetel 1fr) dengan `overflow:hidden; min-height:0` pada `.chips-clip` di dalamnya. Menganimasikan `grid-template-rows` adalah satu-satunya cara **CSS murni** untuk bertransisi ke tinggi konten yang **tidak diketahui**: track-nya menginterpolasi sehingga tabel terdorong secara progresif. `max-height` menuntut angka hard-coded yang langsung rusak begitu chip kedua membungkus ke baris baru; FLIP dengan JavaScript adalah pendekatan pengukuran rapuh yang sudah dua kali menyakitkan di project ini. `min-height:0` pada clip **wajib**, kalau tidak track 0fr tetap menyisakan tinggi intrinsik konten dan tidak pernah menutup. Kurva sama dengan animasi baris: **220ms cubic-bezier(0.22, 1, 0.36, 1)**, dan dibatalkan oleh `@media print` + `prefers-reduced-motion`. **TERVERIFIKASI lewat rekaman rAF** atas tinggi slot dan posisi tabel selama perubahan — baik lewat toggle kelas maupun lewat **jalur UI sungguhan** (klik preset "This month"), dan keduanya menghasilkan jejak yang identik: slot `0→12→21→28→32→34→36→37→38`, tabel `187→199→208→215→219→221→223→224→225`. **Sembilan langkah progresif** dengan ease-out, bukan lompatan tunggal 187→225. ⚠️ **Jebakan pengujian yang saya alami:** tes isolasi pertama saya menghapus `is-open` lalu menambahkannya kembali dua frame kemudian, sehingga slot tidak pernah benar-benar bergerak dan saya sempat menyimpulkan transisinya tidak berjalan. **Untuk menguji transisi, biarkan elemen sudah settle di state awal dulu, baru toggle lalu rekam. Catatan lain: computed `gridTemplateRows` untuk `1fr` terbaca **"38px"** — periksa `0px` vs `38px`, bukan keyword `fr`
- [x] **Animasi smooth saat semua filter di-apply** (2026-10-04) — Baris data dipindahkan ke dalam `<TransitionGroup tag="tbody">` di cctvacc.vue. State **loading** dan **empty** dipisah ke `<tbody>` masing-masing, karena tabel memang mengizinkan beberapa elemen tbody — itulah cara bersih mendapatkan daftar baris ber-key yang bisa dianimasikan tanpa wrapper div yang merusak layout tabel. (Catatan: pemisahan ini sempat menghasilkan **500 "Invalid end tag"** karena saya kehilangan pembuka `<tr>` untuk baris empty-state — selalu periksa seluruh tbody setelah merestrukturisasinya.) **CSS:** enter = opacity 0 + `translateY(-8px)` yang meluruh ke normal; move = FLIP; keduanya **220ms cubic-bezier(0.22, 1, 0.36, 1)**, dilindungi baik oleh blok `@media print` maupun blok `prefers-reduced-motion` yang **membatalkan sepenuhnya** (mengikuti aturan yang sudah dipakai utilitas `.anim-*` di main.css). **TIDAK ada transisi leave, dan itu disengaja** — sebuah `<tr>` yang sedang animasi keluar masih menempati slotnya sampai transisi selesai, sedangkan mencabutnya dari flow dengan `position:absolute` pada baris tabel membuat border dan perataan kolom seluruh tabel berantakan di tengah animasi; menghapusnya seketika sambil baris lain meluncur jauh lebih bersih. **Hanya `transform`, tidak pernah width/height** — aturan yang sama yang dulu menyebabkan bug "ghost text" di sidebar. **TERVERIFIKASI dengan merekam computed transform frame-by-frame lewat `requestAnimationFrame` di dalam halaman**, setelah membuat 4 record sementara (satu baris tidak bisa membuktikan apa pun): **ENTER** `matrix(1,0,0,1,0,-8)` opacity 0 → −5,47 → −3,53 → −2,19 → −1,31 → −0,77 → opacity 1. **MOVE** adalah bukti ter clearest: baris ALPHA yang bertahan meluncur dari **−183px ke 0** melalui −125,4, −81,0, −49,9, −30,0, −17,6, −10,0, −5,47, −2,81, −1,30, −0,51, −0,15, −0,02 — FLIP ease-out yang teksuranya konsisten. Keempat record uji dihapus (204 masing-masing), hanya row milik HIRO yang tersisa. Catatan harness: sampling dengan panggilan `js()` berulang dari Python **terlalu lambat dan sepenuhnya melewatkan** transisi 220ms — pasang perekam rAF di halaman **sebelum** memicu perubahan, lalu baca array-nya
- [x] **Filter jadi auto-apply, toggle Unsigned dihapus, tombol Excel** (2026-10-04) — Empat permintaan HIRO. **(1) Filter SEKARANG LANGSUNG BERLAKU — tidak ada tombol Apply dan tidak ada draft sama sekali.** Setiap kontrol di `LogbookFilterPopover.vue` menulis langsung lewat satu helper `set(key, value)` yang emit `update:modelValue`, dan preset quick-range emit from/to bersamaan. Ini **menghapus satu kelas bug** yang sebelumnya ada di komponen: draft terpisah bisa meleset dari nilai yang sudah diterapkan — persis seperti kasus popover yang pernah menampilkan tanggal lama dan switch ON padahal chip sudah hilang. Sekarang satu state, satu sumber kebenaran, tidak ada yang perlu direkonsiliasi, dan hack "apply on close" ikut hilang. Popover **sengaja tetap terbuka** setelah perubahan supaya beberapa filter bisa digabung tanpa dibuka lagi. **(2) "UNSIGNED ONLY" DIHAPUS** — alasan HIRO masuk akal dan saya tulis sebagai komentar kode: kedua tanda tangan **wajib** di form entry, jadi baris yang salah satunya kosong hanyalah baris yang sedang diisi, dan itu memang tidak seharusnya bisa dicari di dalam register. `unsignedOnly` dihapus dari objek filter, predicate, chip, dan `removeChip`. **(3) Tombol Export Excel → "Excel"** dan diberi `variant="soft"` agar sama dengan Print, sehingga dua aksi "keluarkan data" terbaca sebagai sepasang; Print tetap paling kanan. Urutan toolbar sekarang **Filter | Add Row | Excel | Print**. **TERVERIFIKASI dengan pointer event sungguhan**: klik "This month" langsung memunculkan chip "Date: 2026-10-01 → 2026-10-04" tanpa ada tombol Apply yang ditekan; pilih Section "a" langsung memunculkan chip "Section: a" dan placeholder-nya hilang, juga tanpa ada yang perlu diklik; tidak ada teks "Apply" maupun "Unsigned" di halaman; Clear all mengembalikan chip ke kosong dan tabel ke 1 baris. Filter sengaja saya tinggalkan dalam keadaan kosong agar HIRO membuka halaman dalam keadaan bersih
- [x] **Fix flicker pada filter Section / PIC Name** (2026-10-04) — HIRO melaporkan flicker saat memilih Section / PIC Name. Dua perubahan di `LogbookFilterPopover.vue`: (1) **`value-key=""` dihapus** dari kedua USelect — value-key kosong tidak bermakna untuk array string biasa dan memaksa select membangun ulang daftar opsi tiap update; (2) badan popover diberi **`min-h-[24rem]`** supaya daftar USelect yang terbuka tidak mengubah ukuran panel — panel yang ukurannya ikut isi kontenlah yang membuat seluruh popover melompat. **TERVERIFIKASI dengan mengukur bounding box popover di beberapa frame** saat memilih: `807,145,332,413` di **setiap** frame — sebelum daftar terbuka, saat terbuka, dan tepat setelah memilih. Diuji fungsional dengan 2 record uji (FINANCE/Zeta PIC, HR/Yuri PIC, karena 1 baris tidak cukup untuk menguji dropdown): Section=HR → 1 baris + chip; ditambah PIC=Yuri PIC → tetap 1 baris + chip kedua; kedua record uji dihapus, hanya row milik HIRO yang tersisa. ⚠️ **Pelajaran harness — dan di sini saya sempat salah:** `element.click()` sintetis pada opsi select Reka UI **TIDAK** memilih apa pun — opsi ter-highlight tapi tidak berubah, yang persis seperti kontrol rusak. Yang benar adalah `cdp('Input.dispatchMouseEvent', mousePressed lalu mouseReleased)`. **Sebelum menyalahkan aplikasi, uji ulang dengan pointer sequence sungguhan.** Saya sempat melaporkan ini sebagai "bug kegunaan nyata" dan harus mengoreksi diri sendiri
- [x] **Filter CCTV Log Book** (2026-10-04) — HIRO menyetujui rancangan yang dia ajukan, dan eksekusinya selesai. Komponen baru `web/app/components/LogbookFilterPopover.vue` + state filter di cctvacc.vue. **DESIGN: satu tombol "Filter" dengan badge jumlah filter aktif**, berada di antara Search dan Add Row, membuka popover — **sengaja BUKAN** tiga dropdown inline, karena toolbar sudah membawa Search, Add Row, Export, Print, dan kontrol inline tambahan akan memperkecil tabel di layar 1280px. Isi popover: **Quick range** (All time / Today / This week / This month, minggu mulai Senin), **From/To**, **Section**, **PIC Name** (searchable), switch **Unsigned only**, Clear all / Apply, dan baris informsional **"X of Y rows match"** yang berubah live. **Opsi dropdown diturunkan dari baris yang dimuat** lewat computed `sectionOptions` / `picOptions`, **tidak pernah hard-coded**, jadi daftarnya tidak mungkin basi. **Filtering berjalan di browser atas baris yang sudah dimuat**, bukan server-side: instan, dan tabel, penghitung baris, serta Excel export semuanya membaca **computed `visibleRows` yang sama** sehingga tidak mungkin berbeda pendapat. Konsekuensinya: `loadRows` pageSize dinaikkan **200 → 1000** karena filter bekerja pada himpunan itu; logbook yang lebih besar akan terpotong diam-diam dan filter tampak "kehilangan" baris — sudah ada komentar kode untuk menaikkan lagi. **Filter aktif ditampilkan sebagai chip yang bisa dihapus satu per satu** di bawah toolbar ("Filtered by [Date: x → y] [Section: ISD] … Clear all") memakai utilitas `anim-fade-up`, supaya tampilan saat ini tidak pernah ambigu dan tak ada yang salah print/export karena lupa filter masih aktif. **Tiga bug UX yang saya temukan dari screenshot, bukan dari menguji jalur bahagia:** (1) empty state tetap bilang "No rows yet / Use Add Row…" meski yang terjadi adalah filter menyaring semua — kini **sadar filter** ("No rows match your filter / Adjust or clear the filters to see the rest"); (2) penghitung kiri di footer mencetak total mentah DB sehingga terbaca "1 row / Showing 0 of 1" dan terlihat rusak — kini mengikuti filter; (3) setelah **Clear all**, chip hilang tapi popover masih menampilkan tanggal lama dan switch ON — tiga bagian layar berbeda pendapat; `LogbookFilterPopover` kini memantau `modelValue` dengan `{deep:true}` dan menyinkronkan draft **tanpa syarat** (aman, karena mengedit draft tidak menyentuh modelValue sehingga editan setengah jadi tidak pernah tertimpa). **TERVERIFIKASI**: Unsigned only → 0 baris + chip + badge + empty state benar; preset This month → chip "Date: 2026-10-01 → 2026-10-04"; klik X pada chip menghapus hanya filter itu; Clear all menyetel ulang chip, draft, badge, dan highlight preset sekaligus; Export diblokir "Nothing to export." saat filter menyembunyikan semua baris, dan mengekspor hasil terfilter bila tidak
- [x] **Lebarkan kolom header Excel: EMPLOYEE NO tidak lagi wrap** (2026-10-04) — HIRO melaporkan header "Employee No" wrap ke atas-bawah sehingga tinggi header bertambah. **Penyebabnya ketemu lewat PENGUKURAN, bukan tebakan:** canvas `measureText` dengan Calibri 11 bold, dinormalisasi terhadap lebar "0" (1 satuan lebar kolom Excel ≈ lebar digit), menunjukkan "EMPLOYEE NO" butuh **11,84 satuan** sementara hanya tersedia **12** — margin nol, jadi setiap perbedaan metrik font di Excel sungguhan membuatnya wrap. Itulah intinya: lebar yang terlihat lapang untuk huruf campuran akan **tipis sekali** untuk header UPPERCASE bold. `WIDTHS` baru = `[7,12,11,17,20,36,13,18,18,20,13]` (total 185, sebelumnya 180): EMPLOYEE NO **12 → 17**, dan kelebihannya diambil dari PURPOSE / DETAILS yang kelewat lebar (40 → 36, padahal hanya butuh 15,98). NO 6 → 7, SECTION 10 → 11, kedua kolom SIGN 12 → 13. **Aturan untuk ke depan: ukuran lebar kolom Excel terhadap label UPPERCASE yang diukur dengan font export itu sendiri — jangan pakai penglihatan atau hitungan karakter.** Terverifikasi lewat round-trip ExcelJS pada file hasil generate: tinggi header tetap **26**, widths terbaca `7,12,11,17,20,36,13,18,18,20,13`, 1 baris data, **2 gambar masih tertanam**. Catatan: canvas pengukuran memakai substitute Calibri yang ada di browser, jadi angkanya pendekatan — berguna untuk menangkap kasus nyaris batas seperti 11,84 vs 12, bukan untuk layout piksel-perfect
- [x] **Filter DITUNDA** (2026-10-04) — HIRO meminta menunggu sampai ia puas dengan hasil Excel export dulu. **Belum ada kode filter yang ditulis.** Rencana yang sudah disepakati nanti: tombol "Filter" di sebelah search box (bukan menumpuk dropdown di toolbar yang sudah penuh), isi popover = **Date range** (dengan preset cepat Today / This week / This month / All time), **Section** dropdown, dan **PIC Name** dropdown ber-search; kandidat tambahan **"Unsigned only"** untuk mencari baris yang salah satu tanda tangannya belum ada. Filter aktif ditampilkan sebagai **chip** di bawah toolbar supaya user tahu sedang melihat data tersaring (mencegah salah print/export), dan export otomatis mengikuti filter karena memakai baris yang tampil
- [x] **Fix: cell tanda tangan di Excel export tidak lagi berisi teks base64** (2026-10-04) — HIRO melihat `data:image/png;base64,iVBORw...` muncul di dalam cell PIC Sign / ISD Sign. **Penyebab:** `useExcelExport.ts` menulis nilai **semua** kolom apa adanya ke dalam cell — termasuk kolom tanda tangan, yang nilai tersimpannya memang berupa data-URL — lalu **terpisah** menaruh gambar hasil decode di atasnya. Jadi export berisi keduanya. **Fix: satu guard di pemetaan nilai**, `if (c.sign) return ''`. Gambar adalah isinya; cell tidak membawa teks. **TERBUKTI dengan round-trip, bukan dikira-kira:** export dilakukan dua kali, lalu **kedua file di-parse balik dengan ExcelJS di Node** (script pemeriksa harus berada **di dalam `web/`** agar `require('exceljs')` resolve — script di scratch tidak melihat `web/node_modules`). **SEBELUM:** `leaked data-URL: 2` di row2 col7 dan col11, keduanya diawali `data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA`. **SESUDAH:** `empty: 2, leaked data-URL: 0`, dengan `IMAGES: 2` sehingga gambarnya tetap utuh. Ukuran file turun **18.939 → 13.971 byte** — itulah base64 yang hilang. Header terkonfirmasi: NO | DATE | SECTION | EMPLOYEE NO | PIC NAME | PURPOSE / DETAILS | PIC SIGN | START TIME | END TIME | PIC BY ISD | ISD SIGN. Pola yang berguna untuk dipakai lagi: mengintercept `URL.createObjectURL` di halaman menangkap blob workbook tanpa menunggu unduhan ke filesystem, dan browser harness **tidak bisa** meng-import bare specifier seperti `'exceljs'` — hanya path yang sudah di-resolve Vite — jadi round-trip lewat Node adalah cara paling andal untuk menguji file hasil generate
- [x] **Export Excel (tombol di samping kiri Print)** (2026-10-04) — **Pilihan library: EXCELJS, bukan SheetJS.** SheetJS tidak bisa meng-embed gambar, dan logbook CCTV itu **register bertanda tangan** — spreadsheet dengan dua kolom kosong bernama "PIC Sign"/"ISD Sign" praktis tidak berguna sebagai bukti audit. ExcelJS 4.4.0 menulis PNG asli ke dalam sheet, sehingga file hasil export ikut membawa tanda tangan. Dibuat `web/app/composables/useExcelExport.ts` dengan `exportLogbookToExcel({rows, columns, sheetTitle, fileName})`. ExcelJS di-load lewat **dynamic import di dalam fungsi** karena ~1MB dan aplikasi diserve dari server kantor — memaksa setiap pengunjungunduhnya hanya ada yang klik Export itu boros. Bentuk row sengaja longgar (`values: Record<string, any>`) supaya bisa dipakai entitas metadata apa pun, bukan hanya CCTV. Styling: header row di-freeze, fill gelap #233D4D sesuai sidebar aplikasi, header uppercase, tinggi baris 46 untuk memberi ruang gambar, dan kolom **NO tetap ikut diekspor** meski disembunyikan di layar ("register tanpa nomor urut bukan register"). Export mencakup **persis baris yang sedang tampil**, sehingga layar dan file tidak mungkin berbeda. **Posisi tombol: Export Excel tepat di KIRI Print**, dengan Print tetap paling kanan supaya posisi yang sudah dihafal tidak berubah. Urutannya Add Row → Export Excel → Print. **TERVERIFIKASI dengan mengintercept `URL.createObjectURL` lalu memeriksa blob-nya**, bukan sekadar menganggap unduhan berhasil: 18.939 byte, MIME spreadsheet yang benar, ZIP magic `PK\x03\x04`, memuat `xl/worksheets/sheet1.xml` **dan** `xl/media/` — bukti gambar tanda tangan benar-benar tertanam. ⚠️ **BELUM diverifikasi: workbook-nya belum dibuka di Excel** untuk memeriksa lebar kolom dan penempatan gambar secara visual — itu masih perlu dilihat manusia
- [x] **SignaturePad bisa MULTIPLE STROKE** (2026-10-04) — Gejala: user menandatangani, lalu klik lagi, **goresan pertama diam-diam terhapus**. Penyebab: `start()` di `web/app/components/SignaturePad.vue` memanggil `c.clearRect(...)` dan set `hasInk = false` pada **setiap pointerdown** — itu memang disengaja, komentar aslinya alasan supaya signature lama tidak muncul di bawah yang baru saat mengedit baris bertanda tangan. Alasan itu salah untuk cara orang menandatangani (nama dulu, lalu tanggal, lalu aksen). **Fix: kedua baris itu dihapus**, goresan sekarang menumpuk. Menghapus total kini jadi tugas **tombol Clear** yang eksplisit. `hasInk` **sengaja tidak** diset di `start()` — ia tetap berubah di `move()` pada piksel pertama, jadi placeholder "Sign here" dan tombol Clear hanya muncul setelah ada tinta sungguhan, dan klik tanpa gerakan tidak mem-export PNG yang tidak berubah. `end()` emit di setiap pointerup, jadi tiap goresan memperbarui data-URL tersimpan. Efek samping yang bagus: mengedit baris yang sudah bertanda tangan sekarang bisa **menambah tanggal di bawah nama yang sudah ada**. **TERVERIFIKASI dengan menghitung piksel alpha**: tiga goresan memberi **239 → 450 → 584** (naik monoton, sebelumnya reset tiap kali); Clear mengembalikan ke **0** dan placeholder muncul lagi; pad dua goresan tersimpan **510 piksel tinta dengan sebaran 36 baris** di PNG yang tersimpan — membuktikan gambar multi-goresan benar-benar tersimpan, bukan sekadar terlihat benar di layar. ⚠️ Catatan Membersihkan: DB punya baris (id 69, PIC "123", section "a") yang **bukan dibuat saya** — itu hasil testing HIRO di browser — jadi sengaja **tidak** saya hapus. Selalu listing baris sebelum menghapus, jangan bulk-clear
- [x] **Semua field CCTV wajib + tanda bintang + footnote + popup peringatan English** (2026-10-04) — Keputusan HIRO: **semua field wajib** (hanya kolom NO tersembunyi yang opsional karena di-generate server). Field wajib: Date, Section, Employee No, PIC Name, Purpose / Details, Start Time, End Time, PIC by ISD, PIC Sign, ISD Sign. Implementasi: array `REQUIRED` (berupa {key,label} urutan form) + ref `showErrors` + fungsi `fieldInvalid(key)`. **Footnote "* Mandatory — all fields must be completed."** ditaruh di **kiri** action bar (baris itu berubah dari `justify-end` ke `justify-between`). **Tiga hal yang perlu diingat:** (1) `required` pada `<UFormField>` dipakai **murni untuk tanda bintang merah** — ia merender `after:content-['*'] after:text-error` pada elemen label, dan **tidak** menempelkan atribut `required` ke input (terverifikasi `input.required === false`); (2) validasi bawaan UForm **dimatikan** dengan `:validate-on="[]"` di `<UForm>`, **dengan sengaja** — kalau dibiarkan, UForm akan memotong alur `@submit` dan menampilkan error per-fieldnya sendiri, dan pemotongan itulah yang membuat Edit dulu diam-diam tidak melakukan apa-apa; (3) `:color="'error'"` pada UInput **TIDAK bekerja** di Nuxt UI v4 — tidak ada atribut `data-color` sama sekali, dan input memakai `border-0` karena **wrapper** yang menggambar border. Yang berhasil adalah `:ui="{ base: 'ring-2 ring-error' }"`. TimePicker dan SignaturePad sama-sama mendapat prop `invalid` dengan efek `ring-2 ring-error`. Pesan peringatan berbahasa Inggris dan menyesuaikan jumlah: *"Date is required. Please complete the highlighted field."* vs *"These fields are required: A, B, C. Please complete all highlighted fields."* Dua pesan lama berbahasa Indonesia ("Purpose / Details wajib diisi.", "PIC Name wajib diisi.") sudah dihapus. **TERVERIFIKASI end-to-end**: dikosongkan 8 field → peringatan menyebut tepat 8 itu → diisi satu per satu → merah hilang bertahap → form lengkap tersimpan dengan kedua tanda tangan, nomor auto, dan waktu terkomposisi; record uji dihapus
- [x] **Marker TimePicker ikut pindah ring** (2026-10-04) — HIRO melaporkan "ring luar masih ada 01, ganti jadi 13". **Ternyata "01" itu BUKAN label ring luar** — label luar di spoke itu memang sudah "13" (terverifikasi: `12,13,14,15,16,17,18,7,8,9,10,11`). Itu **marker pilihan**, yang SELALU digambar di radius luar dan menampilkan nilai terpilih apa adanya. Karena nilai 01 (01:50 pagi) hanya ada di ring **DALAM**, marker-nya melorot ke ring luar dan menutupi label "13" — sehingga terbaca seolah ring luar masih menulis 01. **Fix: marker dan tangan sekarang mengikuti `selRing`** lewat `markerR = computed(() => (hourSelected && selRing === 'inner' ? R_INNER : R))`, dengan ujung tangan `Math.max(markerR - 14, 30)`, dan jari-jari marker 14 -> 13. Terverifikasi: nilai 01 -> jarak marker dari pusat **52** (ring dalam, outer labels terbaca jelas 12,13,14…); tekan spoke luar 2 -> jam **14**, jarak marker **78** (ring luar)
- [x] **Jam TimePicker dibatasi ke jam operasional 07:00-18:00** (2026-10-04) — Aturan bisnis: rekaman CCTV hanya boleh dicari antara jam 07:00 dan 18:00. Dial ditulis ulang sebagai **dua tabel nilai literal** (bukan wajah 12-jam + tebakan AM/PM): `OUTER = [12,13,14,15,16,17,18,7,8,9,10,11]` — dua belas jam kerja, dengan **12 tetap di posisi atas** supaya tata letak jam yang familier tetap terjaga — dan `INNER = [0,1,2,3,4,5,6,19,20,21,22,23]` untuk sisa hari di spoke yang sama. Gabungan keduanya masih mencakup **24 jam penuh**, jadi pencarian di luar jam kerja seperti 03:00 tetap bisa dicatat — hanya bukan tempat pertama yang dikenai jari. **Konsekuensi yang perlu diingat: pemilihan jam sekaranglookup langsung** (`hour = pendingInner ? INNER[i] : OUTER[i]`) dan seluruh logika tebakan AM/PM **dihapus**, sehingga label yang tercetak dan nilai yang tersimpan tidak mungkin lagi berbeda. **Dua bug yang saya buat dan temukan lewat pengukuran, bukan dikira-kira**: (a) patch saya **mengganti** uji per-item `selectedIndex === i - 1` alih-alih menambahkannya, sehingga **seluruh 12 label luar ikut ter-highlight**; (b) `selRing` default-nya 'outer', jadi membuka picker di 01:45 (jam yang hanya ada di ring **DALAM**) tetap menyalakan ring luar. `loadFromModel()` kini menyetel `selRing` dari `OUTER.includes(Number(hour))`. **TERVERIFIKASI dengan menekan spoke sungguhan**: spoke luar 2 -> **14**, spoke dalam 10 -> **22**, tekan menit tetap menutup popover, Start Time terbaca **22:15**
- [x] **Popover TimePicker tidak lagi melewati border bawah modal** (2026-10-04) — Penyebabnya ruang memang kurang: trigger Start Time di y~258, dasar modal 536, jadi tersedia ~278px sementara popover 276px **ditambah** offset 8px. Fix: ritme vertikal dipadatkan (`w-64 pb-2`, kotak jam `pt-3 pb-1`), `sideOffset: 6` + `avoidCollisions` + `collisionPadding: 8`, dan dial dikecilkan ke **`size-[190px]`**. ⚠️ **`size-48` TIDAK ter-generate** oleh build Tailwind project ini — SVG diam-diam jatuh ke lebar parent 256px, jadi untuk diameter dial harus pakai nilai arbitrer seperti `size-[190px]`. **TERVERIFIKASI dengan menyapu tinggi viewport 900/800/720/640/569**: dasar popover selalu **13–14px DI ATAS** dasar modal di semua tinggi
- [x] **`web/restart-dev.ps1` — NUXT_API_PROXY_TARGET itu WAJIB** (2026-10-04) — `web/nuxt.config.ts` hanya memasang proxy Vite dev untuk `/api` **jika env var `NUXT_API_PROXY_TARGET` di-set** (`proxy: process.env.NUXT_API_PROXY_TARGET ? {...} : undefined`). Kalau `npm run dev` dijalankan tanpa itu, SPA tetap normal dimuat tetapi **setiap request /api jatuh ke SPA dan mengembalikan index.html**. Gejalanya menipu: tombol **Sign In tidak melakukan apa-apa tanpa pesan error**, dan `fetch('/api/auth/login')` mengembalikan **status 200 dengan `<!DOCTYPE html>`**, bukan 401 — terlihat seperti handler login rusak, bukan proxy hilang. `web/restart-dev.ps1` sekarang set `NUXT_API_PROXY_TARGET=http://localhost:5099` sekaligus membersihkan `NUXT_APP_BASE_URL`; **pakai script itu** untuk restart dev server, jangan `npm run dev` polos
- [x] **Dial analog disederhanakan: tanpa OK/Cancel/hint, auto-close, ukuran konstan** (2026-10-03) — Permintaan HIRO: hapus teks "Pick the hour/minute", hapus tombol **Cancel** dan **OK**, **popover menutup otomatis** saat menit diklik, dan **ukuran tidak berubah-ubah**. Alur sekarang: buka -> tekan jam -> dial pindah ke menit -> tekan menit -> **popover tutup sendiri**. Setiap pilihan langsung di-`commit()` (emit `update:modelValue`) karena tidak ada tombol OK untuk menundanya; `cancel()` dihapus beserta konsep draft. **Cara ukuran dijaga konstan**: popover dipatok `w-64`, dial `size-52`, dan **ring dalam 24-jam tetap digambar di kedua langkah** — hanya `fill`-nya yang jadi `transparent` saat langkah menit (diverifikasi: fill inner = `rgba(0, 0, 0, 0)`). Sebelumnya ring dalam hanya ada di langkah jam, sehingga langkah menit memakai label lebih kecil pada radius lebih kecil dan seluruh dialog melompat tinggi. Terukur: **256x276 di kedua langkah, dial 208x208, identik**. Interaksi terverifikasi: tekan "14" ring dalam -> langkah menit -> tekan menit 15 -> popover tertutup otomatis, Start Time = **14:15**. Tombol Clear signature dikecilkan 28x28 -> **24x24** (`size-6`, ikon `size-4` -> `size-3.5`, `right-1.5 top-1.5` -> `right-1 top-1`), tetap icon-only + `aria-label`, tetap `bg-elevated` + tint error (bagian itu yang membuatnya kelihatan)
- [x] **TimePicker -> DIAL ANALOG + tombol Clear jadi icon-only** (2026-10-03) — HIRO mengirim gambar referensi time picker analog. **PENTING — Tailwind v4 TIDAK menghasilkan utilitas `fill-*` untuk nama warna semantik Nuxt UI.** Menulis `class="fill-elevated"` / `fill-primary` / `fill-muted` / `fill-default"` pada elemen SVG **diam-diam tidak melakukan apa-apa** dan bentuknya jatuh ke fill default SVG yang **hitam** — Akibatnya dial render sebagai piringan hitam dengan label tak terlihat. Bukti: pemindaian semua stylesheet untuk `.fill-(primary|elevated|muted|default)` = **0 aturan**, sementara `getComputedStyle(circle).fill` = `rgb(0, 0, 0)`. **Fix: bind fill lewat `:style` ke CSS variable Nuxt UI** — `--ui-bg-elevated`, `--ui-text-highlighted`, `--ui-text-dimmed`, `--ui-text-muted`, `--ui-color-primary-500`, `--ui-text-inverted`. Variabel ini ditulis ulang Nuxt UI setiap kali tema/color mode berubah, jadi dial tetap mengikuti accent pilihan user dan mode terang/gelap. **TERVERIFIKASI lewat UserMenu sungguhan**: dark+lime -> face oklch(0.279…), primary rgb(0,193,106); light+**violet** -> face oklch(0.968…), primary oklch(0.606 0.25 292.717), hand stroke sama; lalu dikembalikan ke dark+green. **Desain dial meniru gambar persis**: dua kotak (jam/menit) di atas, **ring LUAR** = wajah 12-jam (12 di atas, lalu 1–11 searah jarum), **ring DALAM** = ekuivalen 24-jam pada spoke yang sama (00 di atas 12, lalu 13–23), tangan dari titik pusat ke posisi terpilih, marker terisi di spoke terpilih, Cancel/OK di kaki. **Ring ganda adalah inti desainnya**: user yang berpikir 24-jam membaca angka 14 di ring dalam sementara jarinya di angka "2" ring luar. **Disambiguasi inner/outer**: pointer handler mengukur jarak dari pusat; hit di dalam `(R_INNER+R)/2` dianggap ring 24-jam dan membalik AM/PM, sedangkan ring luar mempertahankan half yang sedang aktif. Dead zone 14px di sekitar titik pusat. Menit memakai 12 spoke yang sama dengan inkremental 5 menit. Draft disalin dari model saat popover dibuka sehingga Cancel membuang seluruh sesi. **Interaksi terverifikasi**: tekan "14" ring dalam -> hint berubah jadi "Pick the minute", tekan menit 30, OK -> Start Time jadi **14:30**. Catatan: `colorMode.preference` app ini `'dark'`, jadi set cookie `nuxt-color-mode` manual **tidak berpengaruh** — hanya toggle UserMenu yang jalan. **Tombol Clear signature jadi ICON-ONLY** (28x28, `aria-label="Clear signature"`), teks "Clear" dihapus atas permintaan HIRO
- [x] **Polish TimePicker (step 5 menit, close on minute) + tombol Clear signature + hapus subtitle PIC by ISD** (2026-10-03) — (1) **Subtitle "From your account." pada PIC by ISD DIHAPUS** (HIRO). (2) **`minuteStep` default TimePicker 15 -> 5** (HIRO minta "pilihan menit akumulasi 5 menit"); file default-nya 15, bukan 5 — sudah dikonfirmasi dengan membaca file, bukan diasumsikan. **Klik MENIT langsung menutup popover** tanpa harus klik di luar (sesuai permintaan HIRO), sementara klik **JAM tidak** menutup — memang masih harus pilih menit. `commit()` diubah dari `(h, m)` jadi `({h?, m?}, close = false)`. Ditambah: **auto-scroll kolom ke nilai terpilih saat popover dibuka** (`watch(open)` + `data-selected` + scrollTop centering) — tanpa itu roda jam selalu terbuka di 00:00 dan memilih 13:45 berarti menggulir 13 baris setiap kali; serta **header "Selected HH:MM AM/PM"** di atas kolom. Diverifikasi: klik menit 25 -> picker tertutup, Start Time jadi `00:25`; buka lagi -> klik jam 14 -> picker **tetap terbuka**, header jadi `14:25 PM`. (3) **Tombol Clear signature diperjelas** — sebelumnya cuma ikon eraser 14px berwarna `bg-white/90` di atas canvas **putih**, jadi nyaris tak terlihat (warna sama dengan latar!). Sekarang **pill berlabel** `border-error/40 bg-white/95 px-2 py-1 text-[11px] text-error` + ikon + teks "Clear", hover `bg-error hover:text-white`, focus ring, tetap hanya muncul saat ada tinta (`v-if="modelValue"`). Diuji dengan menggambar via PointerEvent sintetis: ada tinta -> tombol muncul & bisa diklik -> diklik -> canvas bersih (alpha 0) -> tombol hilang -> placeholder "Sign here" kembali. Catatan: tombol ini **overlay di sudut kanan atas canvas** (61x26 di pad 124x64) jadi menutup sedikit area tanda tangan — perilaku standar untuk signature pad, tapi kalau HIRO keberatan, pindahkan ke bawah canvas
- [x] **Revisi layout form CCTV + auto-fill + TimePicker + Department->Section** (2026-10-03) — Permintaan HIRO: susunan posisi bagus & profesional, Date otomatis today, Department->**Section** (header tabel **dan DB**), Start/End jadi time picker ("keren", tanpa ketik tanggal, ambil tanggal today otomatis), PIC by ISD otomatis dari user login, PIC Sign & ISD Sign bersebelahan kiri-kanan, dan **pas di modal tanpa scrollbar**. **Layout 4 baris di grid 3 kolom**: (1) Date | Section | Employee No, (2) PIC Name | Start Time | End Time, (3) Purpose / Details full width, (4) PIC by ISD | PIC Sign | ISD Sign. **Tanda tangan bersebelahan** karena masing-masing dapat **satu kolom masing-masing** — sempat dikasih `col-span-2` pada ISD Sign dan itu justru mendorongnya ke baris sendiri. **TimePicker** = komponen baru `web/app/components/TimePicker.vue`: tombol + popover dengan dua roda (jam/menit, step 5 menit), dibangun tangan karena dropdown native `<input type="time">` tidak bisa distyle senada. v-model menyimpan **waktu saja** ("HH:mm"); **tanggal disusun saat save** oleh `composeDateTime()` menjadi `dd/MM/yy HH:mm` (bentuk kertas), jadi mengedit satu field tidak diam-diam menulis ulang field lain. `openEdit` harus **memuris waktu** dari nilai tersimpan sebelum memberikannya ke TimePicker, kalau tidak picker menampilkan seluruh "03/10/26 09:00". **Auto-fill** (semua tetap bisa diedit — auto-filled bukan berarti terkunci): `openCreate` mengisi `tanggal`=today, Start/End=jam sekarang, dan `pic_isd`=`fullName || username` dari `useAuth()`. Helper: `todayIso()` (yyyy-mm-dd **waktu lokal**), `todayDisplay()` (dd/MM/yy), `currentUserName()`. ⚠️ `todayIso()` sengaja pakai komponen tanggal lokal, BUKAN `toISOString().slice(0,10)` yang lama — yang itu memakai UTC dan bisa bergeser sehari. **Department -> Section**: label di `SeedService.cs` **dan** di DB hidup diubah lewat `PUT /api/fields/{id}` (label runtime, jadi **tanpa migrasi**; `name` tetap `departemen`, jumlah field tetap 12). **Tanpa scrollbar**: modal `sm:max-w-4xl` + `body: 'p-4 sm:p-5'`; `UTextarea` butuh `class="w-full"` **selain** `col-span`, kalau tidak dia sizing ke isinya dan mengabaikan span. **TERVERIFIKASI end-to-end**: add -> `tanggal=2026-10-04`, `pic_mulai='04/10/26 13:45'` (tanggal hari ini + waktu pilihan), `pic_isd='Administrator'`, `nomor=1` auto; nol node scrollable di dalam modal; kedua canvas tanda tangan y sama dan x berbeda (benar bersebelahan). Record uji sudah dihapus (total kembali 0)
- [x] **Form Add/Edit CCTV -> UModal + 3 bug nyata ditemukan & diperbaiki** (2026-10-02) — Permintaan HIRO: form jadi modal (bukan panel di bawah tabel), transisi smooth/professional, dan add+edit diuji end-to-end. **BUG 1 — jangan taruh tombol submit di `#footer` slot UModal di codebase ini**: slot itu menelan `@click` (inilah alasan asli formnya tadinya inline panel). Tombol Save/Cancel tetap **di dalam `<UForm>`** pada `#body`. Field area dibungkus `max-h-[52vh] overflow-y-auto` supaya action bar selalu terjangkau tanpa scroll seluruh dialog. **BUG 2 — Edit TIDAK BERFUNGSI sama sekali** (padahal Create jalan, jadiHealthy-looked): (a) `openEdit` menaruh tanggal mentah dari API (`2026-10-03T00:00:00`) ke `<input type="date">` yang hanya menerima `yyyy-mm-dd` dan diam-diam render KOSONG; Date `required` → validasi UForm memblokir submit → modal diam saja. Fix: `form.tanggal = String(form.tanggal ?? '').slice(0, 10)` (`openCreate` sudah melakukan ini manual). (b) `save()` mengirim `nomor: null` **juga saat edit**, padahal komentarnya sendiri bilang jangan — `UpdateAsync` itu merge-only, jadi key yang **di-omit** tidak disentuh, tapi `null` eksplisit **menimpa** nilainya dan kena required+unique → setiap edit gagal "Validation failed". Fix: `if (editing.value) { for (const k of HIDDEN_FIELDS) delete values[k] } else { ... = null }`. **BUG 3 — error save tidak terlihat**: halaman ini pakai banner `toast` ref buatan sendiri (BUKAN `useToast` milik Nuxt UI) yang dirender di dalam dashboard panel, sedangkan `#__nuxt` punya `isolate` → stacking context sendiri, sehingga **z-index anak-anaknya tidak akan pernah bisa naik di atas UModal** yang di-teleport ke sibling body yang lebih akhir. Menaikkan ke z-[60] tidakowler berhasil (terbukti: `elementFromPoint` tetap kena backdrop). Yang berhasil: bungkus dengan `<Teleport to="body">` + `fixed left-1/2 top-20 z-[60]` (top-20 gunaa lewat navbar sticky 64px). **TERVERIFIKASI end-to-end**: create → 10 baris, tersimpan dengan nomor auto "10"; edit → id 65 dan nomor "10" **terjaga**, tanggal terjaga, tabel ter-update; toast error kini terbaca di atas modal yang terbuka. Sekalian dibenersih satu teks Indonesia nyisa di `SignaturePad.vue` ("Tanda tangan di sini" -> "Sign here"). Ditambah `api/run-tests.ps1` yang membaca password admin dari `appsettings.json` (`Seed.AdminUsername`/`Seed.AdminPassword`) ke `$env:INFRA_ADMIN_PASSWORD` (tidak pernah dicetak) lalu menjalankan kedua suite → **15/15 PASS**. ⚠️ **PERINGATAN: `test-logbook.ps1` MENGHAPUS semua baris yang sudah ada sebagai bagian dari cleanup-nya** — suite ini menghapus 9 record dummy, jadi **logbook sekarang KOSONG** (DB total 0)
- [x] **Judul sheet: "RECORDABLE MEDIA LOG BOOK" -> "CCTV Access Request Log"** (2026-10-02) — Dipilih HIRO dari empat opsi. Alasan yang dicatat di kode: sheet ini membawa **DUA tanda tangan** (PIC Sign pemohon + ISD Sign), jadi isinya catatan **permintaan akses yang telah disetujui**, bukan sekadar log akses pasif — kata "request" itu yang membawa makna. Sekalian konsisten dengan nama menu sidebar ("CCTV Access"). Hanya ada 2 kemunculan, keduanya di `web/app/pages/logbook/cctvacc.vue` (h1 + komentar doc); slug entity `cctv_log_book` dan semua nama field tidak berubah, jadi **tanpa migrasi** dan `test-logbook.ps1` tetap berlaku. **Ditulis dalam title case ("CCTV Access Request Log"), bukan ALL CAPS** — versi all-caps oleh HIRO dinilai terlalu "berteriak", dan di app ini tabel memang sudah memakai uppercase untuk header kolom. ⚠️ **Peringatan yang sudah disampaikan ke HIRO:** "Recordable Media LOG BOOK" itu istilah standar Retention Management/compliance — kalau ISD punya template form cetak resmi, nama itu bisa punya bobot administratif. **Cek ke atasan formulir dulu** sebelum menganggap rename ini final
- [x] **Nav: grup "Log Book" collapsible, default open** (2026-10-02) — Sidebar jadi: Dashboard / **Log Book** (`type: 'trigger'` + `defaultOpen: true`) berisi **CCTV Access** → `/logbook/cctvacc` dan **Handover** → `/logbook/handover` / User Management. Route `/logbook/cctv` **di-rename** jadi `/logbook/cctvacc` (file `pages/logbook/cctv.vue` → `cctvacc.vue` via `git mv`), judul halaman jadi "CCTV Access". Halaman placeholder baru `pages/logbook/handover.vue` memakai shell standar (UDashboardPanel + PageHeader — itu yang membawa tombol collapse sidebar) dan **sengaja belum tersambung API**: belum ada entity `handover_log_book` di DB; kalau dibangun, ikuti pola `cctvacc.vue` (slug entity di SeedService + storage lewat generic engine + layout form kertas yang ditulis manual). **Responsif untuk label panjang**: nav dirender dengan `v-for` atas grup-grupnya (sebelumnya hardcode `links[0]` dan `links[1]`, yang akan diam-diam membuang grup ketiga), vendor sudah pasang `truncate` di `linkLabel`/`childLinkLabel`, dan slot `item-label` menambahkan atribut `title` native supaya label terpotong tetap terbaca utuh saat di-hover (vendor tidak mendukung `title` per item). `:ui="{ link: 'min-w-0' }"` yang benar-benar membuat truncasi aktif di dalam flex row. **Terverifikasi dengan label panjang buatan** ("User Management & Access Roles") → tampil "User Managemen…" dengan ellipsis, sidebar tetap 208px (tidak melebar), `text-overflow: ellipsis` aktif, `title` berisi teks penuh; lalu label dikembalikan. Collapse grup terverifikasi: `aria-expanded` true → klik → false
- [x] **Struktur halaman final: 3 halaman saja** (2026-10-02) — Dashboard (`/`), CCTV Log Book (`/logbook/cctv`), User Management (`/users`). `app/pages/entities.vue` (Desainer Entitas) **dihapus dari disk**, link nav + cek `isAdmin` di layout ikut dibuang. Nav jadi daftar tetap 3 item; layout **tidak lagi memanggil `/entities`** sama sekali (`route`, `token`, state `entities`, `isAdmin` semuanya dead code lalu dihapus). `app/pages/data/[slug].vue` masih ada di disk tapi tidak lagi terjangkau dari nav
- [x] **Semua string UI jadi bahasa Inggris** (2026-10-02) — label nav, title/subtitle, tombol, header tabel, toast, pesan error login, branding sidebar ("Aplikasi Internal" → "Internal App"), item UserMenu (Warna aksen → Accent Color, Warna netral → Neutral Color, Tampilan → Appearance, Terang/Gelap → Light/Dark, Keluar → Sign Out), format tanggal `id-ID` → `en-GB`. Verifikasi browser: nol kata Indonesia tersisa di ketiga halaman
- [x] **Pembersihan Master Data + Transaksi** (2026-10-01) — HIRO menyatakan tidak butuh. Dihapus dari sidebar nav, command palette, dan kartu dashboard. Di DB: Customer (4 record), Product (3), Sales Order (4), Supplier214159 (0) dihapus via `api/purge-demo-entities.ps1` (delete record dulu, lalu entity — field ikut cascade). **Tersisa 1 entity saja: `cctv_log_book` (12 field, 0 record)**. Review Entitas (`/entities`) sengaja DIPERTAHANKAN untuk membuat entity baru
- [x] **Tombol collapse di navbar tiap halaman** (2026-10-02) — mengikuti template resmi `nuxt-ui-templates/dashboard` persis: `UDashboardSidebarCollapse` di slot `#leading` `UDashboardNavbar` tiap halaman, **bukan** di sidebar footer. Dibuat komponen bersama `web/app/components/PageHeader.vue` (prop `title`/`subtitle` + slot `#actions`) dipakai ketiga halaman, supaya tidak ada halaman tanpa cara expand. Vendor memetakan slot DEFAULT navbar ke slot "center" (terpusat), jadi judul harus lewat `#trailing`. Terverifikasi di browser di 3 halaman + lintas halaman (collapse di Dashboard → pindah ke logbook → expand dari sana): 240px → 64px → 240px
- [x] **Akar masalah `ResizeObserver loop`** ditelusuri sampai selesai — hanya muncul saat **HMR update**, bukan interaksi pengguna: 3× reload, 4× resize window, Ctrl+K, collapse, semuanya nol error. Artefak dev server, tidak ada di build produksi. **Artinya `resizable` bisa dikembalikan tanpa konsekuensi** — HIRO menyetujui penghapusannya, ternyata tidak terkait. Masih nonaktif di `layouts/default.vue`; boleh diaktifkan kalau drag-lebar diinginkan

### ⬜ Belum / belum terverifikasi
- [ ] **Hasil cetak PDF logbook** — `@page A4 landscape` sudah ada, tapi belum pernah dilihat output-nya. HIRO perlu cek sendiri: isi 1 baris → klik Cetak
- [ ] Toast `users.vue` & fix NUXT_E2005 — terkonfirmasi via grep + build, **belum diverifikasi visual**
- [ ] **`web.config` belum pernah diuji ke IIS sungguhan** — rewrite fallback ke `index.html` masih asumsi. **Ini risiko deploy tertinggi.**
- [ ] Belum ada test project otomatis (test selama ini ad-hoc PowerShell)
- [x] **Spring sidebar + centering semua icon** (2026-10-02) — **Spring sidebarnow CSS murni, tanpa state JS sama sekali**. Animasi di-key dari atribut milik vendor sendiri: `#dashboard-sidebar-app-v2[data-collapsed='false'] { animation: infra-sidebar-close }` dan `[data-collapsed='true'] { animation: infra-sidebar-open }`. Karena selector yang cocok berubah tiap flip, animasi restart dengan sendirinya. `transform-origin: left center` + `transition: width 320ms` (tanpa transisi width, transform-nya terlihat seperti glitch). Terverifikasi `data-collapsed` dan `animationName` berganti selaras dua arah. **JANGAN pasang ulang composable JS untuk ini** — `useSidebarCollapsed.ts` yang sudah dihapus gagal tiga cara: (a) template `:ref` pada **COMPONENT** memberi **component instance**, bukan DOM node, jadi `el.getAttribute()` melempar "el.getAttribute is not a function" dan **halaman jatuh 500**; (b) guard observer pakai flag `bound` di useState membuatnya mengawasi node yang sudah **detached** setelah re-mount, jadi state diam-diam beku; (c) `useState` dipanggil di **module scope** (di luar Nuxt instance) tidak pernah bind ke app. **Centering semua icon, berbasis ukur**: ketiganya persis di tengah saat collapsed (offset 0) — brand 0, avatar footer 0, nav icon 0. Masing-masing butuh `-ms-1` berbeda karena duduk di bawah padding berbeda: BrandMenu punya `px-4` header (16px) **plus** `px-1`-nya sendiri (4px), jadi mark 32px berada di x=20..52 dengan center x=36 vs center sidebar x=32. State expanded sengaja tetap **left-aligned**, jadi geseran hanya saat collapsed
- [x] **Fix ghost text — ROOT CAUSE SEBENARNYA (2026-10-02)** — Ghosthing **BUKAN** karena `scaleX` dan **BUKAN** spesifik halaman. Penyebab sebenarnya: saya pernah menambahkan `transition: width 320ms` ke spring sidebar, sehingga **lebar sidebar beranimasi** dan browser **me-layout ulang + me-raster ulang setiap label nav di SETIAP frame**. Terbukti langsung dengan sampling frame saat satu transisi collapse: lebar baris nav melangkah **129 → 165 → 174px** dalam tiga frame. Layout ulang per frame itulah ghost-nya. Terjadi di semua halaman; cuma paling kentok di item yang sedang dilihat mata (label User Management di bawah sidebar). **Fix: `transition: width` dihapus sama sekali** — lebar sekarang **lompat dalam satu langkah** (208 → 64, nav 175 → 31, konstan di semua frame) sehingga label di-layout sekali saja, dan bounce cukup transform murni pada piksel yang sudah di-raster. Terverifikasi: `nav[0]` dan `nav[last]` keduanya 31px collapsed / 175px expanded — **semua menu item kini transisi identik**. **Pelajaran: jangan pernah menganimasikan `width` pada container yang memuat teks; biarkan snap, animasikan transform saja**
- [x] ~~Fix ghost text saat transisi sidebar (percobaan 1)~~ (2026-10-02) — HIRO melaporkan "ghost text" pada label **User Management** saat collapse/expand. **Penyebab: keyframe spring sidebar memakai `scaleX`** untuk kesan squash-and-stretch; scaling memaksa browser **me-raster ulang teks pada ukuran berbeda** di tengah animasi, sehingga frame teks lama sempat masih terlihat. **Fix: spring sekarang TRANSLATE-ONLY** (`translateX`, 4 keyframe stop per arah, overshoot 8px/3px/-1px) — `translateX` memindahkan piksel yang sudah di-raster dan tidak pernah memicu raster ulang, jadi gerakannya tetap terbaca sebagai spring tapi teks tetap tajam. Terverifikasi dengan sampling frame saat collapse: computed transform kini `matrix(1, 0, 0, 1, x, 0)` **tanpa komponen scale**, meluruh 8.37 → 0.64 → 3.58 → 0.51 → −1.02 → 0.03. **Aturan: jangan pernah menaruh scale (atau transform apa pun yang memicu raster ulang) pada container yang memuat teks** kalau ghosting teks tidak diterima — kalau butuh kesan squash, taruh di layer dekoratif
- [x] **Polish tombol collapse: bounce + tooltip + centering avatar** (2026-10-02) — (1) **Bounce**: dua keyframe `infra-collapse-closed`/`infra-collapse-open` (260ms, `cubic-bezier(0.34,1.56,0.64,1)`, overshoot translate+scale) di `<style>` non-scoped `PageHeader.vue`, digerakkan `bounceKey` yang di-bind ke `:key` sehingga tombol **remount tiap klik** dan animasi mulai ulang (bukan sekali main). Diterapkan lewat `:ui="{ base: 'sidebar-collapse-btn hidden lg:flex' }"` — **base bawaan vendor adalah `hidden lg:flex`, jadi meng-override `ui.base` PASTI MENGGANTINYA** dan kelas itu wajib ditulis ulang atau tombol kehilangan visibility/layout. (2) **Tooltip**: state collapsed dibaca dari `aria-label` tombol sendiri lewat `MutationObserver` (vendor sudah meng>string "Collapse sidebar"/"Expand sidebar" dan tidak exposes prop reaktif); teks diberikan lewat atribut `title` native. Sengaja **tidak** memakai `useDashboard()`/`useDashboardState` — `useDashboard` itu util internal yang bukan auto-import. Karena `:key` me-remount tombol, observer **harus** di-disconnect di `onBeforeUnmount`, kalau tidak ia mengawasi node yang sudah lepas. (3) **Avatar di footer sidebar bergeser — diperbaiki berdasar UKURAN**: di sidebar collapsed 64px, center avatar ada di x=36 sedangkan center sidebar x=32 → **4px meleset**, karena footer punya `px-4` (16px) dan `p-1.5` tombol tidak sepenuhnya diserap `:square`. Fixпорядок Geser `-ms-1` (4px) pada wrapper `UserMenu`: **terukur offBy 36 → 0**. Koreksi yang "lebih besar" justru membuat BURUK (tercatat di komentar kode): `-mx-4` + `p-0` + `justify-center` mendorong avatar ke x=14, karena avatar tidak berada dalam konteks flex yang menengahkan di dalam `UButton`, jadi `justify-center` tidak bekerja dan negative margin cuma menyeret ke kiri. State expanded sengaja tetap left-aligned (center avatar x=40 di sidebar 208px), jadi geseran hanya saat collapsed
- [x] **Root cause sidebar "tetap lebar" — cookie, bukan HMR** (2026-10-02) — HIRO melaporkan sidebar masih 254px meski sudah hard refresh. Penyebab: `useResizable` default-nya `storage: "cookie", persistent: true`, dan kunci cookie-nya `${storageKey}-sidebar-${id}` → **`dashboard-sidebar-app`**. Cookie itu menyimpan `{size, collapsed}`, dan `size` dibaca **dari cookie, bukan dari `defaultSize`**, kalau cookie sudah ada. Jadi **`:default-size` cuma berlaku di kunjungan pertama** — mengubah prop itu tidak berefek sama sekali di browser yang sudah pernah, dan hard refresh tidak akan menghapusnya._checking localStorage justru mislead (tidak ada key dashboard di sana), makanya sempat terlihat seperti artefak HMR. **Fix: bump prop `id` sidebar (`app` → `app-v2`)** supaya cookie lama tidak pernah dibaca lagi. Terverifikasi: cookie baru `dashboard-sidebar-app-v2` berisi `size: 13`, sidebar render **208px**. **Aturan: setiap kali lebar default sidebar mau diubah, bump `id` lagi (v3, v4, …), atau hapus cookie `dashboard-sidebar-app` di devtools.** Lebar collapsed tidak dikontrol prop ini — dari `min-w-16` (64px) di theme sidebar root
- [x] **Kolom NO disembunyikan + auto-number di backend + animasi page load** (2026-10-02) — (1) Kolom **NO dihapus dari tabel DAN form** (sekarang 10 kolom data + Actions). Field `nomor` **tetap IsRequired + IsUnique di DB**, jadi backend wajib mengisinya — kalau tidak, setiap create 400 `NO wajib isi`. (2) Auto-number: `LogbookNumberService` mengimplementasikan interface baru level-namespace `ISequentialNumberProvider` (`Task<(bool handled, string value)>` — **bukan out param, async method tidak boleh punya out param** → CS1988); didaftarkan di `Program.cs`; `DynamicRecordService` menerimanya sebagai argumen ctor opsional. **GOTCHA mahal (3× gagal):** `MaterializeAsync` **langsung melempar exception** kalau ada field required yang tidak dikirim (branch `if (!has)`), jadi perbaikan yang jalan **setelah** materialize **tidak pernah tereksekusi**. Nomor harus disuntikkan ke `Dictionary` input **sebelum** memanggil MaterializeAsync agar mengalir lewat jalur validasi normal (termasuk cek unique). Create → generate nomor berikutnya; **Update harus MEMBACA NILAI TERSIMPAN dari `rec.Values` dan mempertahankannya** — kalau di-regenerate, baris akan bernomor ulang tiap save dan logbook cetak rusak. `SaveRecordRequest.Values` bersifat `init`-only, jadi buat salinan `new Dictionary<string, object?>(req.Values)`. **Terverifikasi**: probe API — create tanpa nomor → dapat `11`; edit tanpa nomor → tetap `11`; probe dihapus, NO kembali 1–10. Probe UI — baris "UI Create Probe" dibuat dari form sungguhan tanpa kolom NO, muncul (9 → 10), lalu dihapus. `test-logbook.ps1` **15/15 PASS**
- [x] **Animasi page load** (2026-10-02) — keyframes `infra-fade-up` / `infra-fade-in` / `infra-slide-left` dan utility `.anim-fade-up`, `.anim-fade-in`, `.anim-slide-left`, `.anim-stagger > *` di `main.css` (340ms, `cubic-bezier(0.22,1,0.36,1)`, stagger 60px hingga 6 anak). Dipakai: sidebar `anim-slide-left`, body logbook + grid dashboard `anim-stagger`, WelcomeBanner + kartu Modules `anim-fade-up`, body users `anim-fade-up`. Blok `@media (prefers-reduced-motion: reduce)` **membatalkan sepenuhnya** (opacity/transform dipaksa normal), bukan cuma memperpendek — partly-fade masih bisa memicu gangguan vestibular
- [x] **Format nomor urut logbook: 1, 2, 3, ...** (2026-10-02) — Ganti dari `1/2026/001` menjadi **integer polos**. `LogbookNumberService` kini parse `int.TryParse(raw.Trim(), ...)` lalu return `max.ToString()`. Konsekuensi: (a) **tidak ada komponen tahun lagi**, jadi penomoran **kontinyu seumur hidup logbook** dan tidak reset tiap Januari; (b) parameter `forDate` sengaja tidak dipakai (`_ = forDate;`) tapi tetap ada di signature agar controller & kontrak API tidak berubah — query `?date=` masih jalan, hanya tidak memengaruhi nomor; (c) nilai lama berformat `1/2026/001` **dilewati parser**, bukan ikut_requires angka, jadi tidak perlu migrasi. `test-logbook.ps1` diupdate: 4 assertion meng-hard-code format lama. Test "Nomor reset utk tahun" jadi "Nomor tidak dipengaruhi tanggal" dan **harus membandingkan dengan `$n2`, bukan `$n1`** — di antara kedua panggilan ada baris yang dibuat sehingga counter memang maju satu (membandingkan ke `$n1` itu bug saya sendiri dan gagal di run pertama). Suite kini **15/15 PASS**
- [x] **9 record dummy logbook** (2026-10-02) — NO 1–9, nama PIC varied (Budi Santoso … Joko Susilo), Department berselang-seling ISD/CAP, PIC by ISD varied, purpose berbeda-beda, 1 dari 3 baris tanpa PIC Sign agar kolom kosong terlihat saat print. Semua NO diambil dari endpoint `next-no` asli, bukan diketik. Angka **tidak lagi reset per tahun**
- [x] **Rename & reorder kolom CCTV, sinkron dengan form** (2026-10-02, lanjutan) — "Requestor Sign" → **PIC Sign** (header tabel + label form). **"PIC by ISD" dipindahkan ke tepat sebelum "ISD Sign"** supaya namaofficer ISD bersebelahan dengan tanda tangannya. Urutan final: NO, Date, Department, Employee No, PIC Name, Purpose / Details, PIC Sign, Start Time, End Time, PIC by ISD, ISD Sign, Actions. **Urutan field di form Add/Edit ikut disesuaikan agar sama persis dengan tabel** — sebelumnya order form dan tabel sudah berbeda. Hanya label + urutan tampilan yang berubah; **key field (`tanda_pemohon`, `pic_isd`) dan database tidak tersentuh**, jadi baris yang sudah ada dan jumlah 12 field tetap aman
- [x] **Redesign tabel CCTV + rename kolom** (2026-10-02) — (1) Rename: "Start Search" → **Start Time**, "End Search" → **End Time**, "Name Requestor" → **PIC Name** (header tabel, label form, **dan pesan validasi required di `save()`** yang masih berbunyi "Name Requestor wajib diisi" — mudah terlewat). (2) Tabel di-restyle: container `rounded-xl` + `overflow-hidden`, header sticky (`thead.print:sticky-head sticky top-0 z-10`) dengan label uppercase + tracking-wide + `text-muted`, `scope="col"`, zebra striping (`:class="i % 2 ? 'bg-default/20' : ''"` pada `v-for="(r, i) in rows"`), hover `bg-primary/5`, sel `border-b border-default/60` (garis bawah saja di layar), `align-middle` (bukan align-top), `tabular-nums` + `whitespace-nowrap` di kolom NO dan Date agar angka lurus, state kosong/loading pakai ikon, footer "N rows" + "Showing X of Y". Font `text-xs` → `text-sm`. (3) **Dampaknya ke cetak PDF — penting**: grid di layar cuma garis bawah, dan tabel berada di dalam container `max-h-[70vh] overflow-auto print:scroll-area` dengan header sticky; CSS print HARUS menetralkan keduanya atau hasil cetak terpotong setinggi satu viewport dengan header berulang tiap halaman. Ditambahkan `.print\:scroll-area { max-height:none; overflow:visible }`, `.print\:sticky-head { position:static }`, dan `th, td { border: 1px solid #000 }` supaya sheet cetak tetap berkotak penuh. (4) `min-w` 1600 → 1500 → **1180px**. Terukur: di 1920×1080 area scroll 1662px dan tabel pas **tanpa scroll horizontal sama sekali** (12 kolom terlihat); di 1280×900 area hanya 1006px sehingga ~174px masih overflow dan scroll horizontal jadi fallback yang disengaja, bukan memampatkan kolom tanda tangan sampai tak terbaca
- [x] **Pemangkasan UI CCTV Log Book** (2026-10-02) — (1) Sheet header kehilangan **"Name of System"** dan **"Dept"**; yang tersisa hanya judul + baris aksi (search / Add Row / Print), dan barisnya ganti `items-end` → `items-center` karena blok metadata sudah hilang. (2) Kolom **"Time Request" dihapus dari tabel** dan input **"Time Request" dihapus dari form** Add/Edit — jadi tidak ada data yang diisi tapi tidak pernah ditampilkan. **Field `waktu_diminta` di DB sengaja TIDAK dihapus**: `cctv_log_book` tetap **12 field** (tepatnya yang di-assert `test-logbook.ps1`), dan kini dicatat di const `HIDDEN_FIELDS = ['waktu_diminta']` di `cctv.vue` — `resetForm` membersihkannya, `openEdit` memuatnya dari row, `save()` mengirimkannya. `UpdateAsync` di backend bersifat merge-only (hanya menyentuh field yang ada di payload) jadi menghilangkannya pun sudah aman, tapi round-trip eksplisit menjaga niat terlihat dan mencegah backend berubah.drop data diam-diam. (3) Rename kolom: "PIC start search date/time" → **Start Search**, "PIC end search date/time" → **End Search** (header tabel + label form). Lebar yang dilepas diberikan ke Name Requestor 12→13% dan Purpose / Details 19→22%. Tabel kini 11 kolom data + Actions. **Terverifikasi di browser** (header, label form, nol teks "Time Request"/"Name of System"/"Dept") dan `test-logbook.ps1` tetap **14/14 PASS** dengan entity masih 12 field
- [x] **Test E2E dijalankan ulang setelah refactor kredensial** (2026-10-02) — `test-logbook.ps1` **14/14 PASS**, exit 0. Jadi perubahan ke env var **tidak merusak apa pun**. `test-e2e.ps1` **tidak bisa lulus lagi, dan itu BUKAN regresi**: suite itu menguji generic CRUD engine memakai demo schema (`customer` / `product` / `sales_order`) yang sudah **dihapus dari DB pada 2026-10-01**. Sebelumnya gagal menyesatkan di langkah 3 dengan HTTP 404 di `/api/records/` karena `$entCustomer` null — terlihat seperti API rusak. Sekarang ada pengecekan **PRECONDITION** yang mendeteksi fixture hilang dan `exit 0` dengan pesan jelas menyebut entity mana yang kurang + menunjuk `test-logbook.ps1` sebagai coverage engine yang sebenarnya
- [ ] Gotcha PowerShell: runner dengan `$ErrorActionPreference='Stop'` akan **BERHENTI** saat child script gagal seperti yang diharapkan — bungkus probe/negative-test dengan `Continue`, lalu kembalikan ke `Stop`
- `recordCount` di EntityDto belum ter-refresh setelah create
- ~~Route `/data/[slug]`~~ — sudah dihapus 2026-10-02; hanya tinggal 3 halaman
- [ ] Belum ada: export Excel/CSV, bulk delete + checkbox (HIRO belum memilih)

## 6b. Loop metadata terbukti penuh (2026-09-30)
Dibuat **nol kode** dari browser:
1. Entitas **"Warehouse"** dibuat dari `/entities` (slug `warehouse` auto-generated)
2. Field **"Kota"** ditambahkan dengan tipe **Lookup** → target Customer, display field "Nama"
3. `/data/warehouse` **otomatis** terbentuk: tabel dengan kolom "Kota", form berisi input teks + dropdown lookup berisi 3 customer nyata
4. Record tersimpan, lookup ter-resolve ke "PT Offline Test"

Read-only field di Entity Builder: field tipe Lookup tidak menampilkan "Panjang Maksimum"/"Nilai Default" (tidak relevan), dan pemilih target tidak menawarkan entity itu sendiri (mencegah self-reference).

## 7. Gotcha (penting, sudah ternoda)

1. **Nuxt 4 pakai `app/` sebagai srcDir** — assets di `web/app/assets`, bukan `web/assets`.
2. **Swashbuckle HARUS 6.9.0** — v7+ menarik Microsoft.OpenApi 2.x yang menghapus namespace `.Models`.
3. **Nuxt UI 4 `UModal` `#footer` menelan `@click` submit** (terverifikasi: `onclick=null` di node). Gunakan panel inline + `UForm @submit`.
4. **`NUXT_APP_BASE_URL` bocor lintas shell** — setelah `nuxt generate`, variabelnya persist dan `nuxt dev` berikutnya jalan dengan baseURL production → router menolak semua path. Selalu `unset` sebelum `nuxt dev`; `build-release.cmd` sudah meng-unset otomatis.
5. **Jangan `nuxt generate` lalu `nuxt dev` singkat** — cache `.nuxt` basi → "No match found for location". `rm -rf .nuxt` + restart.
6. **Banyak dev server menumpuk & merusak cache** — kill proses node dulu (PowerShell `Stop-Process`, tool ProcessManager sering timeout).
7. **Browser harness:** pakai selector `name`/`id` (bukan `type`) — `input[type=text]` bisa kena field yang salah. **Selalu `scrollIntoView` tombol sebelum klik** — tombol di y=943 pada viewport 569px diam-diam menelan klik dan tampak seperti handler rusak.
8. **Tool `write_file` kadang merusak file panjang** & memblokir overwrite (`stale_write_blocked`) → workaround: `rm` dulu lalu tulis, lalu read-back untuk verifikasi.
9. **PowerShell:** `$pid` readonly; bash merusak `$_` → selalu pakai file `.ps1` + `-File`, jangan `-Command`.
10. **Browser daemon mengunci file port** — `PermissionError` di `bu-default.port` → kill chrome/msedge lalu hapus file `.port`.
11. **Notifikasi watch-pattern bisa replay log lama**, dan HMR bisa memunculkan `ReferenceError` sesaat. **Selalu konfirmasi ke file sumber + kode yang benar-benar disajikan dev server sebelum mengejar error.**
12. **Exception handler switch** butuh `object body =` + cast eksplisit di satu branch (CS8506, bentuk anonymous type berbeda-beda).
13. **URL Rewrite** di IIS wajib untuk SPA fallback (route `/data/customer` saat refresh harus jatuh ke `index.html`).
14. **`export function` ilegal di dalam `<script setup>`** — memicu `<script setup> cannot contain ES module exports`, halaman mati 500 + "Failed to fetch dynamically imported module". Taruh helper bersama di `composables/`.
15. **Tidak ada route middleware di app ini** — jangan tulis `definePageMeta({ middleware: 'auth' })`, itu melempar "Unknown route middleware: 'auth'". Auth ditegakkan API (JWT); `data/[slug]` dan `users.vue` juga tanpa middleware.
16. **`dotnet run` TIDAK hot-reload** — edit backend tidak terlihat sampai proses pemegang port 5099 di-kill dan direstart. Gejalanya: test gagal seolah-olah logikanya salah padahal kodenya sudah benar.
17. **Query nomor urut wajib filter `!Record.IsDeleted`** — kalau tidak, row yang sudah soft-delete ikut mengonsumsi nomor dan meninggalkan celah permanen di logbook cetak.
18. **Canvas butuh pointer sequence penuh** — `press → move → release` lewat `cdp('Input.dispatchMouseEvent')`; `click_at_xy` tunggal tidak menghasilkan tinta.
19. **Notifikasi watch-pattern tidak selalu basi.** Dari 5 notifikasi berturut-turut pada sesi tema, 4 replay log lama, tapi **1 benar** (`ReferenceError: appConfig is not defined`) karena HMR menangkap kondisi tengah saat file sedang diedit. Bedanya: pesan yang menyebut **nama variabel** bisa ditelusuri ke file; pesan generik browser (`ResizeObserver loop`) tidak menunjuk apa pun. Jangan generalisasi "semua watch notification itu basi" hanya karena beberapa pertama begitu.
20. **`rm -rf .nuxt` butuh persetujuan eksplisit** dan bisa timeout tanpa jawaban. Kalau terblokir, restart saja — Vite rebuild sendiri dari `nuxt.config` yang berubah dan hasilnya tetap bersih.
21. **`useDashboard` BUKAN auto-import dan BUKAN API publik** — util internal Nuxt UI di `node_modules/@nuxt/ui/dist/runtime/utils/dashboard`. Kalau dipakai, harus `import { useDashboard } from '@nuxt/ui/runtime/utils/dashboard'`. Dua jebakan sekaligus: (a) tanpa import → `ReferenceError: useDashboard is not defined`; (b) bahkan dengan import, komponen custom di dalam **slot** sidebar tidak_andalkan context-nya sehingga `toggleSidebar` jatuh ke fallback dan tombol jadi **no-op** (gejalanya diam-diam: `sidebarCollapsed` tidak pernah berubah, `aria-label` tetap). **Solusi: pakai komponen vendor `UDashboardSidebarCollapse` apa adanya, override hanya `ui.base`.** Jangan tulis ulang logika dashboard.
22. **Posisi `absolute` di dalam slot menunjuk ke container terdekat, bukan sidebar.** Untuk menaruh elemen di tepi/ tengah sidebar, beri `:ui="{ root: 'relative' }"` pada `UDashboardSidebar` lalu pakai `right-0 top-1/2 -translate-x-1/2 -translate-y-1/2`. Tanpa itu, `top-1/2` mendarat di tengah header bar.
23. **Slot content di-evaluasi dalam scope komponen yang mendefinisikan slot**, bukan yang merender slot. Jadi `inject()` di komponen anak slot akan mencari context milik layout — bukan sidebar.
24. **Diagnosa HMR**: `ResizeObserver loop` / `__vnode` null / `Cannot set properties of null` / `Cannot read properties of null (reading 'flags'|'parentNode'|'type')` — semua itu **hanya muncul saat HMR update**, karena Vite menukar komponen live sementara node-nya dilepas. **Tidak terjadi dari interaksi pengguna dan tidak ada di build produksi.** Cara memastikan: `rm` file-nya (bukan diedit), restart dev server, lalu tunggu — kalau log bersih, error sebelumnya murni artefak HMR.
25. **Posisi collapse button di template resmi adalah `#leading` slot `UDashboardNavbar` TIAP HALAMAN**, bukan di sidebar footer. Dua percobaan gagal sebelum ketemu: (a) pill `absolute` di border kanan sidebar — bisa diklik tapi cuma karena `ui.base` di-override, tidak sesuai template; (b) baris penuh di sidebar footer — **rusak**, footer punya `px-4` dan area kontennya cuma 32px saat collapsed (`min-w-16` = 64px), jadi UserMenu + gap + tombol 32px = 70px meluber keluar sidebar dan berada DI BAWAH `div` konten utama. Bukti: `document.elementFromPoint()` di tengah tombol mengembalikan `div` konten, bukan tombol — klik expand swallowed, `aria-label` tidak berubah. Jangan ulangi. (c) Slot DEFAULT `UDashboardNavbar` dipetakan vendor ke slot "center" (terpusat, `hidden lg:flex`) — judul yang ditaruh di slot default diam-diam terpusat. Pakai `#trailing`.
26. **Halaman tanpa navbar = sidebar terkunci.** Kalau sebuah halaman tidak punya `UDashboardNavbar`, tombol collapse tidak ada di sana; user yang collapse di halaman lain lalu pindah ke halaman itu tidak bisa expand lagi. Karena itu dipakai komponen bersama `PageHeader.vue`, bukan menulis navbar manual per halaman.
27. **`write_file`/patch bisa merusak indentasi kalau `old_string` dipakai untuk penghapusan baris kosong** — satu patch saya sempat menggabungkan dua baris jadi satu (`...>            <div class=...`). Selalu baca ulang surroundings setelah patch struktural.
28. **Notifikasi watch-pattern dari proses yang sudah mati** muncul terus-menerus (puluhan kali) dan semuanya basi. Yang membedakan pesan nyata: nama **variabel** (`ReferenceError: appConfig is not defined`) bisa ditelusuri ke file; pesan internal framework (`__vnode`, `flags`, `parentNode`) tidak menunjuk apa pun. Verifikasi dengan `process(action='log')` pada **proses yang sedang hidup**, bukan yang ada di notifikasi.

## 7b. Akun dev (DB `InternalApp_Dev`, 2026-10-02)
| id | username | Nama | Role |
|---|---|---|---|
| 1 | `admin` | Administrator | Admin |
| 2 | `operator01` | Operator Satu | Staff |
| 3 | `staf04` | Staf Empat | Staff |

Password `operator01` **di-reset** atas permintaan HIRO dan disimpan di Hermes vault (handle `vault_935e089374bf`, identifier `operator01`); `admin` = `vault_38735d9c5d78`. **Nilai password sengaja tidak dicatat di sini** — hanya lokasi penyimpanannya.

Reset dilakukan lewat `PUT /api/users/2` memakai JWT admin yang sudah aktif di sesi browser, jadi tidak ada password admin yang perlu diketik. Password lama dipastikan mati (401), yang baru pasti hidup (200).

> Catatan keamanan: role berasal dari JWT, jadi user tanpa role sama sekali **tidak bisa login** — `/api/auth/me` balas 401.

## 7c. Git & secret hygiene (2026-10-02)
Repo: remote `origin` = `https://github.com/prehiro/INFRA-CAP-BT.git`, branch `main`, commit awal `6336674` (57 file). `gh` CLI **tidak terpasang** di mesin ini dan tidak ada global `credential.helper`, tapi `git push` HTTPS tetap berhasil (kredensial tersimpan).

**Aturan yang wajib dijaga saat commit berikutnya:**

| File | Status | Alasan |
|---|---|---|
| `api/appsettings.json` | **git-ignore, jangan pernah di-commit** | berisi password `sa` SQL Server asli (192.168.4.3) + password admin aktif |
| `api/appsettings.example.json` | aman di-commit | template dengan placeholder |
| `api/appsettings.Production.json` | aman di-commit | hanya berisi placeholder `GANTI_*` |
| `api/*.ps1` (4 script test/utility) | aman | password admin dibaca dari env var |

**Menjalankan script E2E** (sekarang wajib set env var dulu):
```powershell
$env:INFRA_ADMIN_PASSWORD = '<password>'
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\HIRO\Projects\InternalApp\api\test-logbook.ps1'   # 14/14
$env:INFRA_ADMIN_USER = 'admin'   # opsional, default admin
```

> `test-e2e.ps1` sekarang keluar sendiri dengan pesan SKIPPED karena demo schema sudah dihapus — itu perilaku yang benar, bukan kegagalan.
Kalau env var belum di-set, script berhenti dengan pesan yang jelas — bukan diam-diam gagal login.

Sebelum commit: `git status` dan pastikan `api/appsettings.json` tidak muncul di staged.

## 7d. Menjalankan API dev (2026-10-02)
`dotnet run` **memakai profil launchSettings** dan bind ke `http://localhost:5001`, bukan 5099. Untuk port yang benar:
```bash
cd /c/Users/HIRO/Projects/InternalApp/api
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --no-launch-profile --urls "http://localhost:5099"
```
> Before build: **kill `Api.exe` dulu** (`Stop-Process`), kalau tidak MSB3021/MSB3027 file-lock.
> `dotnet run` tidak hot-reload — perubahan backend baru terlihat setelah proses holding port 5099 di-restart.

### JANGAN pakai `--no-launch-profile` tanpa `ASPNETCORE_ENVIRONMENT=Development` (2026-10-05)

Ini adalah root cause dari "API tidak bisa konek ke SQL Server" yang sempat dikira Durante 2 sesi. Gejalanya: `Win32Exception 258 - The wait operation timed out` (lama-lama `1225 - connection refused`) saat `MigrateAsync`, padahal SQL Server sehat.

Penyebabnya BUKAN SQL Server, BUKAN password, BUKAN firewall. `appsettings.Production.json` berisi connection string sendiri yang menimpa `appsettings.json`:

```
Server=localhost,1433;Database=InternalApp;User Id=<akun dedicated>;Password=***
```

Dan di mesin dev **tidak ada yang listen di localhost:1433** — SQL Server DEV ada di 192.168.4.3, jadi connectTimeout=30 habis lalu timeout. `Program.cs:13` membaca `GetConnectionString("Default")`, jadi file config yang menang menang.

`--no-launch-profile` membuat `ASPNETCORE_ENVIRONMENT` **tidak di-set sama sekali**, yang berarti ASP.NET Core jatuh ke `Production` (default) dan memuat `appsettings.Production.json`. Profil `http` di `Properties/launchSettings.json` justru menyetel `ASPNETCORE_ENVIRONMENT=Development`.

Gejalanya menipu karena errornya seolah-olah jaringan: timeout, refused, "server not found or not accessible" — semuanya konsisten dengan "SQL mati", padahal yang terjadi adalah konfigurasi menunjuk ke localhost.

Cara start yang benar (sudah dipakai 2026-10-05, API jalan healthy di 5099):

```bash
cd /c/Users/HIRO/Projects/InternalApp/api
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5099 ./bin/Debug/net10.0/Api.exe
```

Menjalankan binary hasil build langsung (bukan `dotnet run`)yakni menghilangkan launchSettings sepenuhnya, jadi `ASPNETCORE_ENVIRONMENT=Development` yang kita set eksplisit itu yang benar-benar berlaku — tidak ada yang diam-diam menimpa.

Cek cepat kalau API Connectivity Timeout dan tidak yakin: `curl -s -m 10 http://localhost:5099/api/entities` → **401** = sehat (belum auth). 000 = proses mati.

> Di produzsi kantor file `appsettings.Production.json` memang itu yang BENAR (SQL lokal di 10.89.6.237), jadi file ini jangan dihapus — yang bahaya cuma membacanya di mesin dev.

## 7e. Notifikasi CCTV Access = toast yang sama dengan User Management (2026-10-05)

HIRO minta notifikasi "row added/deleted" di `/logbook/cctvacc` dibuat sama persis dengan yang di `/users`.

Sebelumnya cctvacc bikin notifikasi sendiri: `const toast = ref<{msg,kind}|null>(null)` + blok `<Teleport to="body">` dengan `top-20 z-[60]`, class success/error buatan sendiri, dan `setTimeout` 4 detik.

Sekarang pakai toast bawaan Nuxt UI yang sama dengan users.vue:

```ts
const toast = useToast()
function notify(msg: string, kind: 'success' | 'error' = 'success') {
  toast.add({ title: msg, color: kind })
}
```

Blok `<Teleport>` **dihapus**, bukan dinonaktifkan — grep membuktikan 0 sisa `toast.value` / `toast.msg` / `toast.kind` dan tidak ada elemen `<Teleport>` di halaman ini.

Kenapa `<Teleport>` itu perlu duluan: notifikasi yang render inline terkurung di stacking context `isolate` milik `#__nuxt`, jadi tidak bisa pernah di atas `UModal` (modal di-teleport ke sibling yang lebih akhir) — itu sebabnya "Validation failed" tidak terlihat di belakang modal. `<UToaster>` milik `UApp` sudah me-teleport ke body sendiri, jadi masalah yang sama tetap hilang.

> Fakta penting: `UApp` di `web/app/app.vue` sudah menyediakan `<UToaster>` untuk seluruh app. Itu sebabnya `users.vue` tidak menulis `<UToaster>` sama sekali, dan `grep UToaster web/app` kosong. Jangan tambahkan UToaster kedua di halaman manapun.

Tampilan sekarang: **kanan bawah**, ada tombol X, ada progress strip berwarna (hijau sukses / merah error).

Teks juga disamakan dengan users.vue (tanpa titik di akhir):

| Sebelum | Sesudah |
|---|---|
| `Row added.` | `Record created` |
| `Row updated.` | `Record updated` |
| `Row deleted.` | `Record deleted` |
| `Gagal menyimpan.` | `Could not save the record.` |
| `Gagal menghapus.` | `Could not delete the record.` |
| `Gagal memuat data.` | `Could not load the data.` |
| `Entity CCTV Log Book belum ada di database.` | `CCTV Log Book entity not found in the database.` |

Sudah diverifikasi end-to-end di browser 1920x1080: toast validasi error, record asli dibuat (tanda tangan digambar di kedua canvas, 341/339 piksel tinta) → "Record created", lalu dihapus lewat dialog konfirmasi → "Record deleted". Baris probe sudah dihapus, tabel kembali ke 10 baris milik HIRO, User Management tetap 3 user.

> Catatan harness: tombol aksi di baris tabel **tidak punya aria-label**, jadi cari lewat class ikon (`i-lucide:trash-2`, `i-lucide:pencil`) di dalam baris target — bukan lewat nama aksesibel. Toast hilang dalam ~5 detik, jadi pemicu dan pembacaan harus dalam satu panggilan js() yang sama.

## 7f. Role field di User Management = kartu radio (2026-10-05)

HIRO minta pilihan role pakai tampilan radio button. Komponen baru `web/app/components/RolePicker.vue` menggantikan `<USelectMenu multiple>` di `web/app/pages/users.vue`.

Tiap kartu berisi: indikator bulat (berisi saat dipilih), chip ikon, nama role, dan **deskripsi role yang selalu terlihat** — sebelumnya deskripsi hanya muncul di dalam popover yang tertutup.

Sengaja TIDAK memakai radio sungguhan: `AppUser.UserRoles` sebuah collection, `CreateUserRequest` menerima `List<int> RoleIds`, dan `[Authorize(Roles=...)]` membaca semua claim — jadi satu user memang bisa memegang beberapa role. HIRO sudah ditawari pilihan dan memilih mempertahankan multi-select. Karena itu komponen memakai `role="checkbox"` + `aria-checked`, bukan `<input type=radio>`.

State terpilih: border primary + `bg-primary/8` + chip ikon bernuansa. Helpertext: "Select at least one. A user can hold more than one."

`roleItems` yang sekarang tidak terpakai sudah **dihapus** dari `users.vue` — dan kali ini dibuktikan di runtime, bukan cuma grep: membuat user lewat form sungguhan → "User created" → server mengembalikan roles `['Admin','Staff']`; lalu diedit untuk menurunkan Admin → `['Staff']`; lalu dihapus (server kembali ke admin/operator01/staf04).

### Jebakan penting: ikon HARUS pakai `<UIcon>`

`<span class="iconify i-lucide:shield-check">` berukuran benar tapi **tidak terlihat sama sekali** (`mask-image: none`). Nuxt UI me-resolve SVG dan menyuntikkan CSS `mask-image` saat RENDER, dan hanya komponen `<UIcon>` yang memicu itu — nama class saja tidak melakukan apa pun.

Ini terlihat persis seperti kegagalan bundling offline, padahal BUKAN: ikonnya memang ada di `.nuxt/nuxt-icon-client-bundle.mjs` (68 ikon ter-bundle), dan menghapus `.nuxt` lalu restart dev server tidak menolong karena tidak pernah ada aturan CSS yang dibuat untuk span mentah.

> Selalu pakai `<UIcon :name="..." />`. Jangan pernah menulis span `iconify` manual.

Dua jebakan harness terkait:

- Span **terlepas** yang di-append ke `body` bukan tes ikon yang sah (CSS disuntik saat render, jadi selalu terbaca MISSING). Teslah elemen asli di dalam modal.
- CSS mask yang dihasilkan ada di tag `<style>` yang di-inject runtime, bukan di `.nuxt/ui.css`, jadi grep file build tidak membuktikan apa pun.

## 7g. Dialog Add/Edit User tidak lagi scroll (2026-10-05)

HIRO: "i dont want scrollbar in its modal form". Dialog Add user lama tingginya **854px** di viewport 1080p, jadi body UModal scroll dan baris Cancel/Create harus di-scroll dulu untuk dijangkau.

Diperbaiki dengan **redesign, bukan menyembunyikan scrollbar**:

1. `web/app/pages/users.vue` — susunan field satu kolom `space-y-4` jadi `grid grid-cols-2 gap-x-4 gap-y-3.5`:

   | Username | Full Name |
   |---|---|
   | Password | Email |
   | Role | (col-span-2) |
   | Status | (col-span-2) |

   Modal dilebarkan `sm:max-w-lg` → `sm:max-w-xl`, padding body `p-5` → `p-4 sm:p-5`, dan dua help text dipersingkat.

2. `web/app/components/RolePicker.vue` — kartu role dari dua baris (p-3, indikator size-5, nama di atas deskripsi yang wrap) jadi **satu baris ringkas**: `items-center px-2.5 py-1.5 rounded-lg gap-2.5`, indikator size-4, ikon size-3.5, nama role, lalu deskripsi satu baris ter-truncate dengan `title` untuk teks penuh. Deskripsi role adalah kalimat panjang Bahasa Indonesia, dan membiarkannya wrap adalah penyumbang tinggi terbesar.

Hasil: dialog **854px → 582px**.

Sudah diukur across beberapa tinggi viewport (ini yang penting — satu cek di 1080p akan berhasil padahal laptop 1366x768 masih scroll):

| Viewport | Scroll? |
|---|---|
| 1080 / 900 / 800 / 768 / 720 | tidak |
| 640 | 5px lebih |
| 569 | 70px lebih |

Jadi di jendela sangat pendek masih scroll, tapi laptop 1366x768 (viewport efektif ~640–720) sudah bersih.

Create diverifikasi ulang **setelah** field diurutkan ulang: isi berdasarkan posisi DOM (sekarang Username, FullName, Password, Email), pilih Admin+Staff → "User created" → server `['Admin','Staff']`; user buangan dihapus, server kembali ke admin/operator01/staf04.

> Pelajaran: "tidak mau scrollbar" itu soal pengguna bisa mencapai tombolnya — turunkan tinggi konten, jangan sekadar menyembunyikan batangnya. Dan selalu sweep beberapa tinggi viewport, jangan hanya mengukur di layar sendiri.

## 7h. Deskripsi role dibuat Bahasa Inggris (2026-10-05)

HIRO: "pls use english for role description". Isi deskripsi role adalah **data di database**, bukan teks di frontend.

Isi database sekarang:

| Role | Description |
|---|---|
| Admin | Full access: manage entities, fields, users, and all data |
| Manager | Manage master and transaction data, cannot manage users |
| Staff | Enter and view transactions, read-only for master data |

Table-nya adalah **`Roles`**, bukan `AppRoles` — query dengan `AppRoles` gagal dengan "Invalid object name 'AppRoles'". Table lengkap di `InternalApp_Dev`: `dbo.Roles`, `dbo.Users`, `dbo.UserRoles`, `dbo.Entities`, `dbo.Fields`, `dbo.Records`, `dbo.RecordValues`, `dbo.__EFMigrationsHistory`.

### Dua edit wajib — jebakan yang sama seperti rename Department→Section

`EnsureRoleAsync(name, desc)` di `api/Services/SeedService.cs` hanya mengisi `Description` kalau row-nya **belum ada**:

```csharp
if (r is null)
{
    r = new AppRole { Name = name, Description = desc };
```

Jadi **mengedit seed saja tidak mengubah apa pun** di database yang sudah ada. Row live di-update dengan SQL langsung ke `192.168.4.3/InternalApp_Dev` (@d/@n parameterized, masing-masing role 1 row).

Tidak ada endpoint API untuk mengubah role — `GET /api/users/roles` satu-satunya route roles dan read-only, jadi script DB langsung adalah satu-satunya opsi tanpa menambah migration.

> Konsekuensi yang perlu diingat: database **baru** otomatis dapat teks Inggris dari seed, tapi database lain yang sudah ada — terutama `InternalApp` di server kantor 10.89.6.237 — masih menyimpan baris Bahasa Indonesia sampai UPDATE yang sama dijalankan di sana. Seed tidak akan memperbaikinya.

Sudah diverifikasi: `GET /api/users/roles` mengembalikan teks Inggris, kartu RolePicker di render `"Admin | Full access: manage entities, fields, users, and all data"`, regex `/kelola|Akses penuh|Input dan lihat|transaksi/i` terhadap `innerText` modal return false, dan dialog tetap 582px tanpa scrollbar.

## 7i. Animasi modal + page load di User Management (2026-10-05)

HIRO minta animasi modal Add/Edit User sama seperti modal cctvacc, plus animasi saat page load. `web/app/pages/users.vue` sekarang mengikuti motion CCTV.

### Fakta struktural penting: harus NON-SCOPED

Style block users.vue tadinya **hanya `<style scoped>`**. Aturan scoped compiled menjadi `.users-record-modal[data-v-xxx]`, yang **tidak akan pernah match** elemen content dialog — karena `UModal` di-teleport ke `<body>` dan node hasil teleport tidak membawa scope id milik komponen ini.

Jadi CSS animasi harus dipindah ke blok `<style>` **non-scoped** tersendiri yang ditambahkan setelah blok scoped — persis seperti yang sudah dilakukan cctvacc.vue (di sana ada dua `<style>` biasa). Setelah dipindah, file punya 1 blok scoped + 1 blok non-scoped.

> Verifikasi dengan regex yang **hapus komentar HTML dulu**, karena komentar penjelas menyebut teks literal `<style>`. Pencarian `<style[^>]*>` yang naif melaporkan 3 hit untuk 2 blok asli.

### Animasi

Selector tiga komponen `.users-record-modal[data-slot='content'][data-state='open']` = specificity (0,3,0), mengalahkan utility vendor `data-[state=open]:animate-[scale-in...]` (0,2,0) **tanpa** mematikan transisi vendor (yang harus dimatikan juga akan menghilangkan timing focus trap).

DIVERIFIKASI dengan rAF recorder, bukan asumsi:

- `centreX` tetap tepat **960 di setiap frame** sementara `ty` berjalan 14 → 13.92 → 13.65 → 13.13 → 11.23 → 9.93 → 8.58 → 7.3 → 6.16 → 5.17 → 4.31 → 3.58 → 2.95 → 2.42 → 1.96 → 0 (monotonik, **tanpa overshoot**)
- opacity 0 → 1 selesai **sebelum** slide selesai — itulah pemisahan dua animasi dengan durasi berbeda
- enam field grid stagger: `0/0/0/0/0/0` → `0.01/0/0/0/0/0` → `0.48/0.13/0/0/0/0` → `0.85/0.6/0.22/0.02/0/0` → `1/1/0.94/0.78/0.48/0.17`
- state akhir tetap `translate: -50% -50%` dengan `transform: matrix(1,0,0,1,0,0)`, dialog 576×582, tanpa scrollbar

### Page load

Target `.users-page-block` (ditambahkan ke kartu header **dan** kartu tabel), dengan `:nth-of-type(2)` delay 90ms; delay terverifikasi 0s dan 0.09s.

Sengaja **tidak** dikaitkan dengan ref `loading`, jadi saat create/edit/delete memuat ulang, animasi tidak diputar ulang dan halaman tidak berkedut di bawah kursor. `prefers-reduced-motion` membatalkan semuanya.

Form diverifikasi ulang setelah perubahan: buat zzanim(Manager) → "User created", server `['Manager']`, lalu dihapus (kembali ke admin/operator01/staf04).

> Catatan: memory lama menyebut panel users adalah `<UCard v-if="modalOpen">` inline dengan animasi `modal-bounce-in`. Itu **sudah usang** — panelnya sekarang UModal asli di slot `#body`, jadi CSS bounce lama tidak lagi berlaku.

## 7j. Deskripsi role penuh, pesan password English, Full Name auto-capital (2026-10-05)

Tiga permintaan sekaligus.

### 1. Deskripsi role tampil penuh

`web/app/components/RolePicker.vue`: deskripsi dari satu baris ter-truncate + tooltip `title` menjadi **wrap penuh** di seluruh lebar kartu.

Karena deskripsi jadi 2–3 baris, kartu harus berubah dari `items-center` ke `items-start`, dan nama + deskripsi sekarang berada dalam **satu kolom `flex-1`** — di baris yang di-center, nama akan mengambang ke tengah vertikal deskripsinya sendiri dan terbaca tidak rapi. Indikator dan `UIcon` dapat `mt-0.5` agar rata ke baris pertama.

Dialog tumbuh 582px → **650px**, tapi sweep viewport tetap **tanpa scroll** di 1080/900/800/768/720, karena form dua kolom menyerap tinggi tambahan. Verifikasi: probe `scrollWidth > clientWidth` di semua anak kartu return **false**.

### 2. "Password minimal 6 karakter" → English

Teks yang HIRO lihat itu **server-side**, bukan help text (help sudah English). `api/Controllers/UsersController.cs`:

| Sebelum | Sesudah |
|---|---|
| `Password minimal 6 karakter` | `Password must be at least 6 characters` |
| `Username wajib diisi` | `Username is required` |
| `Username sudah dipakai` | `Username is already taken` |

`Password minimal` muncul **dua kali** (jalur create dan jalur update) — keduanya diubah. Help text client "Min. 6 characters." → "At least 6 characters." `dotnet build` 0 error, API di-restart. Ketiga respons 400 diverifikasi English lewat fetch langsung.

### 3. Full Name auto-capital — proteksi username dipertahankan

HIRO sebelumnya pernah bilang: "jangan terapkan plugin ini pada halaman login dan user management. **bahaya!**", dan `web/app/plugins/autocapitalize.client.ts` punya `EXCLUDED_PATHS = ['/login', '/users']` sebagai blok **seluruh halaman**.

Blok itu **persempit, bukan dihapus**: `EXCLUDED_PATHS` jadi `['/login']` saja, dan UInput username memakai `data-no-capitalize` (escape hatch yang sudah didokumentasikan plugin ini). Bahaya yang HIRO maksudkan nyata dan masih berlaku — capitalize username mengubah data, "budi" tersimpan jadi "Budi" dan orang itu terkunci dari akunnya.

Verifikasi memakai **keystroke asli** (set `.value` sintetis tidak memicu plugin): mengetik "budi" membuat username tetap `b` lowercase sementara Full Name menjadi `B`. Lalu dibuat user nyata dan dibaca balik dari server: username `zzlower`, fullName `Budi Santoso`.

Trade yang didokumentasikan di komentar plugin: opt-out per-field tidak otomatis menutupi field baru yang ditambahkan ke /users nanti — itu justru argumen asli kenapa blok se-page dulu dipakai.

> Jebakan harness: dispatch **keyDown-with-text sekaligus** `type='char'` untuk satu ketikan menyisipkan karakter **dua kali** ("b" → "bb"), dan Ctrl+A tidak membersihkan UInput terfokus lewat jalur ini. Pakai `type='char'` saja pada field yang sudah fokus dan kosong.

## 7k. Rename user tidak langsung update di Dashboard (2026-10-05)

HIRO: "when i rename the user, name in dashboard page not automaticaly change, i must manualy refresh the browser."

**Ini bukan normal — itu bug**, dan sudah diperbaiki.

### Root cause

`useUser()` (`web/app/composables/useApi.ts:23`) adalah `useState<User|null>('auth-user')` — sebuah ref **global** yang ditulis **hanya sekali**, oleh `login()` atau oleh `restore()` setelah full page load.

Dashboard `WelcomeBanner`, sidebar `UserMenu`, dan avatar initials semuanya derive dari situ:

```ts
const displayName = computed(() => user.value?.fullName || user.value?.username || '')
```

Jadi PUT berhasil mengubah **database**, tapi state cache tetap menyajikan nama lama. Tidak ada yang fetch ulang; hanya browser refresh yang menjalankan `restore()` → `apiMe()`.

### Fix

`refreshUser()` baru di `web/app/composables/useAuth.ts` — memanggil `apiMe()`, assign ke `user.value`, return boolean, return false kalau tidak ada token, dan **sengaja tidak** menghapus token atau logout saat gagal (network error sesaat tidak boleh mengeluarkan sesi yang masih valid).

`users.vue` sekarang mendestructure `refreshUser` dan memanggilnya setelah `await load()` **hanya** bila `editing.value.id === me.value.id` — mengedit akun orang lain tidak boleh mengubah siapa yang sedang login, dan create tidak relevan.

Karena banner/menu/initials semuanya `computed` dari `user`, **satu assignment itu** memperbarui semua permukaan sekaligus; tidak ada perubahan per-komponen.

### Verifikasi

Rename admin → "Rename Probe", save, lalu **klik link Dashboard di sidebar** (bukan `goto_url`, karena itu bisa memicu reload) → `h2` = "Rename Probe", sidebar = "RP | Rename Probe | Admin". Lalu dikembalikan ke "ADMINISTRATOR" dan dicek ulang → "ADMINISTRATOR" / "AD".

> Jebakan harness: mencocokkan baris user dengan `/@admin/` lebih dulu akan cocok ke **avatar sidebar**, bukan baris tabel — lalu `.querySelectorAll` pada undefined melempar. Batasi pencarian baris ke string sel tabel yang khas, misalnya email `admin@internal`.

## 7l. Glow accent di kanan atas modal Add/Edit User (2026-10-05)

HIRO: "add smooth gradient in the top right and this color will follow user selected theme accent color".

Diaplication sebagai **pseudo-element `::after`** pada `.users-record-modal[data-slot='content']`, di dalam blok `<style>` **non-scoped** (wajib, alasan sama seperti animasi reveal: `UModal` di-teleport ke body).

Resepnya disalin dari glow `WelcomeBanner` yang sudah diukur terhadap template referensi: **bukan CSS gradient**, melainkan lingkaran 380px berwarna `var(--ui-primary)` dengan `blur(90px)` dan `opacity: 0.28`, diposisikan `top: -140px; right: -120px`, `border-radius: 9999px`.

Karena warnanya `var(--ui-primary)`, glow **otomatis mengikuti accent** — ganti tema, warnanya ikut tanpa kode tambahan. Mode terang menurunkan opacity ke 0.18, karena 0.28 terbaca sebagai tint lembut di navy gelap tapi jadi noda berat di permukaan hampir putih.

Tiga keputusan yang sifatnya menentukan:

1. **`::after`, bukan elemen nyata** — slot `#content` milik `UModal` adalah hal yang membuat `@submit` form tidak pernah sampai ke `save()` di file ini juga, dan elemen nyata akan di-reparent vendor tiap perubahan state.
2. **`pointer-events: none`** — orb-nya lingkaran besar di atas header; tanpanya ia akan menelan klik tombol close. Diverifikasi dengan `elementFromPoint` bahwa hit test tetap sampai ke tombol close.
3. **`header: 'relative z-10'` dan `body: 'relative z-10'`** ditambahkan ke prop `:ui`, agar konten tergambar **di atas** glow yang z-0.

Elemen `content` sudah `overflow: hidden`, dan itu persis clipping yang dibutuhkan supaya glow terbaca sebagai cahaya yang merembes dari sudut, bukan blob melayang.

### Verifikasi

Membaca computed style `::after`:

| Accent | Warna glow |
|---|---|
| green | `rgb(0, 220, 130)` |
| violet | `oklch(0.702 0.183 293.541)` |
| green (dikembalikan) | `rgb(0, 220, 130)` |

Ganti accent lewat submenu **Accent Color** di sidebar, screenshot mengonfirmasi glow jadi violet, lalu dikembalikan ke green (cookie `infra-cap.theme` kembali `{"primary":"green","neutral":"slate"}`).

Tinggi dialog tetap **650px**, tanpa scroll di 1080/900/800/768/720. Form diverifikasi ulang dengan glow aktif: buat `zzglow`(Staff) → "User created", baris muncul, lalu dihapus; server kembali ke admin/operator01/staf04.

> Catatan harness:
> - Komentar HTML **tidak boleh** berada di antara atribut sebuah komponen di template Vue — pindahkan ke atas tag.
> - Submenu sidebar punya tiga anak: Accent Color / Neutral Color / Appearance; nama warna ada di span `[data-slot=itemLabel]`, bisa dipilih lewat teks seperti 'violet' atau 'green'.
> - Override viewport CDP **hilang diam-diam** setelah siklus login/navigasi. Selalu pasang ulang `setDeviceMetricsOverride` sebelum mengukur tinggi/lebar, atau akan mengukur jendela 1264×569 dan salah menyimpulkan ada regresi layout.

## 8. Permintaan terbuka ke HIRO

> **SQL Server kantor pakai Windows Auth atau SQL Auth?**
> Kalau Windows Auth → connection string `Integrated Security=True`, app pool harus jalan sebagai domain service account, dan DB user login-nya perlu `Enable Windows Authentication`. Jawaban ini menentukan langkah setup IIS.

## CCTV Access — glow accent di modal New/Edit Record (2026-10-05)

HIRO: *"cctv acces modal add glowing like add users modal"*.

`web/app/pages/logbook/cctvacc.vue`, pseudo-element `::after` pada
`.cctv-record-modal[data-slot='content']` — sama persis resepnya dengan glow di dialog
Add/Edit User (`users.vue`), bukan CSS gradient melainkan **lingkaran** berwarna
`var(--ui-primary)` dengan blur besar.

| | Users dialog | CCTV dialog |
|---|---|---|
| ukuran | 250px | **300px** |
| blur | 60px | **70px** |
| opacity (dark) | 0.14 | **0.13** |
| opacity (light) | 0.09 | **0.09** |
| posisi | top -110 / right -90 | top -130 / right -110 |

Nilai CCTV sedikit **diperbesar** hanya karena dialognya jauh lebih lebar
(`sm:max-w-4xl`, terukur 896×500) — orb ukuran users ikut menghilang di sudutnya.
Bobot visualnya sama, bukan lebih kuat.

Tiga keputusan yang menentukan:

1. **`::after`, bukan elemen nyata** — slot `#content` milik `UModal` adalah hal yang
   membuat `@submit` form tidak pernah sampai ke `save()` di file ini juga, dan elemen
   content sudah `overflow: hidden` yang persis memberi clipping supaya glow terbaca
   sebagai cahaya yang merembes dari sudut.
2. **`pointer-events: none`** — orb-nya besar di atas header; tanpanya ia menelan klik
   tombol close.
3. **`header: 'relative z-10'` + `body: 'relative z-10'`** ditambahkan ke prop `:ui`,
   supaya judul dan field tergambar di atas glow yang z-0.

CSS-nya di blok `<style>` **non-scoped** yang sudah ada, karena `UModal` di-teleport ke
`<body>` — aturan scoped tidak akan pernah sampai ke elemen content.

### Verifikasi

| Accent | Warna glow |
|---|---|
| green | `rgb(0, 220, 130)` |
| violet | `oklch(0.702 0.183 293.541)` |
| green (dikembalikan) | `rgb(0, 220, 130)` |

- Screenshot crop 2x sudut kanan atas (lewat `Page.captureScreenshot` + `clip`) menunjukkan
  bloom hijau maupun violet dengan jelas.
- `elementFromPoint` di tengah tombol close tetap mendarat di dalam tombol → orb tidak
  menutupi klik.
- Jalur submit form diuji ulang setelah perubahan `:ui`: **Save Row** pada form kosong
  memunculkan peringatan "These fields are required: Section, Employee No, PIC Name,
  Purpose / Details, PIC Sign, ISD Sign." → rantai `@submit` → `save()` utuh.
- Modal ditutup, tabel tidak berubah. Server 11 record, UI "Showing 11 of 11" — cocok,
  tidak ada baris probe.

> Catatan harness:
> - Teks aksesibel tombol user di sidebar adalah `ADAdministratorAdmin` (inisial + nama +
>   role digabung), bukan `Administrator`.
> - `.click()` sintetis pada item submenu **tidak** membukanya; pakai `cdp
>   Input.dispatchMouseEvent` pada rect hasil `getBoundingClientRect()`.

## Handover Log Book — module baru (2026-10-05)

HIRO: *"now let's make handover page. in this page records all IT parts hand over to user.
table header start with Taken Date, Part Name, Brand, QTY, Employee No, Name, Section,
Signature, Remarks. pls make effect and transision or animation same like cctv page"*

Menggantikan placeholder `handover.vue` yang dibuat 2026-10-02.

### Backend

Entity baru **`handover_log_book`** (SortOrder 20, DisplayField `nomor`, 10 field), di-seed
oleh `SeedService.SeedHandoverLogBookAsync`:

| Field | Label | Tipe | Wajib |
|---|---|---|---|
| `nomor` | NO | Text | ya (auto, unik) |
| `tanggal_ambil` | Taken Date | Date | ya |
| `nama_barang` | Part Name | Text | ya |
| `merek` | Brand | Text | — |
| `qty` | QTY | Number | ya (default 1) |
| `no_pegawai` | Employee No | Text | — |
| `nama` | Name | Text | ya |
| `departemen` | Section | Text | — |
| `tanda` | Signature | Text (data-URL PNG) | — |
| `catatan` | Remarks | TextArea | — |

**`LogbookNumberService` harus digeneralisasi** — ini bagian yang tidak obvious:
`TryNextAsync` membandingkan `entitySlug != CCTV_SLUG` dan mengembalikan `(false, "")`.
Untuk handover itu berarti field `nomor` (required + unique) kosong di setiap create →
validasi gagal. Diganti dengan `HashSet` `NumberedSlugs` + `NextNumberAsync(slug, …)`
yang menerima slug sebagai parameter (counter-nya memang sudah membaca field `nomor` milik
entity itu sendiri, jadi tiap logbook bernomor sendiri mulai 1). `NextCctvNumberAsync`
kembali jadi satu baris mendelegasikan, sehingga `CctvLogbookController` dan assertion
`next-no` di `test-logbook.ps1` tidak tersentuh.

Catatan operasional: `Api.exe` harus **dihentikan dulu** sebelum `dotnet build` —
MSB3027, apphost tidak bisa menimpa `Api.exe` yang sedang terkunci.

### Frontend

`web/app/pages/logbook/handover.vue` meniru `cctvacc.vue` dengan sengaja: panel shell dengan
`:ui` per-instance, toolbar Search | Filter | Excel | Add Record, `LogbookFilterPopover` +
chip yang bisa dilepas (animasi tinggi 0fr→1fr), grid `table-fixed` + `colgroup` (tepat 10
`<col>` untuk 10 kolom), transisi baris FLIP, keyframes reveal/dismiss modal, glow accent
`::after` (300px / blur 70 / 0.13), dialog konfirmasi hapus dengan motion stagger, dan blok
print A4 landscape.

Semua CSS spesifik halaman berprefiks `handover-`. Yang shared (`.chips-slot`, `.row-*`,
`.logbook-scroll`, print) **sengaja diduplikasi** dengan nilai identik, bukan diekstrak, agar
satu halaman tidak bisa dirusak oleh edit di halaman lain.

**Satu bug nyata yang ditemukan dari screenshot:** popover filter shared meng-hard-code
dropdown kedua sebagai "PIC Name" / "All requesters" — salah total di lembar yang tidak punya
kolom PIC. Diperbaiki dengan dua prop **opsional** `personLabel` / `personPlaceholder` yang
**default-nya string CCTV yang sekarang**, jadi markup CCTV tidak berubah sama sekali.
Diverifikasi live: Handover membaca Name / All recipients, CCTV tetap PIC Name.

Halaman ini juga memakai `tanggal_ambil` untuk filter tanggal dan `nama` untuk filter
persona (CCTV memakai `tanggal` / `nama_pemohon`), dan search haystack mengecualikan
`tanda` supaya base64 signature ~1800 karakter tidak pernah mencocoki kata pencarian.

### Verifikasi (di browser, bukan hanya build)

- **Create** — tanggal/nama/QTY ter-prefill, signature digambar di canvas via
  `cdp Input.dispatchMouseEvent`, Save Row → "Record created", `nomor` dari server = **"1"**,
  signature tersimpan sebagai data-URL 1846 karakter.
- **Edit** — QTY 1→3, signature bertahan, `nomor` **tidak** terhapus (membuktikan
 Kolom field tersembunyi bekerja).
- **Search** — "latitude" → 1 of 1; "zzzznotfound" → 0 of 1 + "No rows match your filter";
  dikosongkan → 1 of 1.
- **Filter** — preset "This month" → chip `Taken Date: 2026-10-01 → 2026-10-05`, slot `is-open`.
- **Delete** — dialog membaca Part Name / Name / Employee No / Taken Date / QTY = nilai asli
  baris; Delete record → 0 of 0, server mengonfirmasi total 0.

Baris probe **sudah dihapus**, jadi `handover_log_book` kosong dan siap untuk data asli HIRO.
Entity id 9 di DB dev.

## Handover — canvas signature diperbesar (2026-10-05)

HIRO: *"canvas signature too small i think, could you redesign the modal form so the canvas
can have biger space"*.

### Dua sebab, dan hanya memperbaiki satu akan terlihat seperti tidak terjadi apa-apa

1. **Layout** — pad cuma menempati **satu kolom** dari grid 3 kolom form.
2. **Wrapper `inline-block` di `SignaturePad.vue`** — ini yang sebenarnya. `inline-block`
   berarti elemennya shrink-to-fit, dan **canvas tidak punya lebar intrinsik**, jadi
   "fit the content" resolve ke lebar Sekitar placeholder "Sign here": **terukur 124px**
   di dalam kolom yang sebenarnya ~250px. Jadi kolomnya masih ada ruang, tapi pad tidak
   bisa memakainya.

### Perbaikan

Prop **opsional** `fullWidth` di `SignaturePad.vue` (default `false`, jadi dialog CCTV yang
sengaja menaruh dua pad berdampingan **tidak tersentuh**). Saat aktif, wrapper jadi
`block w-full` **dan** root ikut dapat `w-full` — root itu block biasa, tanpa itu wrapper
tidak punya ruang tambahan untuk tumbuh.

Field Signature di form handover sekarang `sm:col-span-2 lg:col-span-3` dengan
`:height="140"`. Form juga diurut ulang: **Name** dapat satu baris penuh (nilai free-text
terpanjang, sebelumnya tergelit di sebelah Brand yang mostly kosong) dan Section pindah ke
samping Employee No.

Nilai **140**, bukan lebih tinggi, itu disengaja: dialog terukur 642px dan tidak boleh mulai
scroll di laptop biasa — pelajaran yang sama dengan perbaikan dialog Add User.

### Terukur

| | Sebelum | Sesudah |
|---|---|---|
| Canvas | 124 × 64 | **856 × 140** (~15× luas) |
| Dialog | 505px | 642px |

Sweep tinggi viewport (bukan cuma 1080p): 1080/900/800/768/720 dialog tetap 642px dengan
`scrollHeight === clientHeight` (tanpa scroll); 640/569 juga overflow 0 dengan dialog
mengecil ke 576/505.

D drew di pad itu: 954 piksel tinta di canvas 856×140, tombol Clear muncul, signature
tergambar penuh selebar dialog di screenshot.

**CCTV diukur ulang sesudahnya** — dua pad tetap **124 × 64**, dialog 503px, overflow 0.
Benar-benar tidak berubah. Data tidak tersentuh: handover 0 baris, cctv 11.

### Jebakan Vue yang|biaya satu 500 untuk seluruh halaman

Menulis **dua binding `:class` pada elemen yang sama** ditolak outright oleh
`@vitejs/plugin-vue` dengan "Duplicate attribute", dan itu menjatuhkan **seluruh halaman**
sebagai 500 "Failed to fetch dynamically imported module" — bukan error lokal. Semua
conditional class digabung jadi satu ekspresi array.

Diagnosisnya lambat karena satu alasan spesifik: browser menampilkan `500 Internal Server
Error` **tanpa overlay**, dan `curl` ke `/_nuxt/components/SignaturePad.vue` mengembalikan
404 sementara `/_nuxt/pages/logbook/handover.vue` mengembalikan 200 — jadi pengecekan URL
langsung mengarah ke file yang salah. Error ini juga baru muncul **setelah** `.nuxt`
dihapus dan dev server direstart; sebelumnya dev server menyajikan module graph basi dan
errornya tertutup. Kalau halaman 500 tanpa overlay, screenshot halaman **segera** — jangan
berasumsi errornya ada di file yang sedang kamu edit.

## CCTV — redesign form + canvas signature diperbesar (2026-10-05)

HIRO: *"i want the cctv signature canvas also make it bigger, redesign the modal form make it
professional look"*.

### Dua canvas

Penyebabnya **sama persis** dengan yang ditemukan di Handover: tiap pad duduk di **satu kolom**
grid 3-kolom, **dan** wrapper `SignaturePad` itu `inline-block` sehingga canvas shrink-wrap ke
lebar placeholder "Sign here"-nya sendiri. Keduanya 124×64.

Diperbaiki dengan prop `full-width` yang sama plus **grid 2-kolom bersarang**
(`mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2`), sehingga dua pad tetap **berdampingan** —
dan itu disengaja: dua tanda tangan itu justru inti lembar ini (pemohon dan petugas ISD
menyetujui permintaan yang sama). Memisahkannya ke baris berbeda akan menyiratkan keduanya
tidak berhubungan.

**Terukur 124×64 → 422×130**, sekitar 7× luas. Dialog 503px → 694px, overflow 0 di
1080/900/800/768 dan mengecil ke 656 di 720. Tinggi 130, bukan lebih, karena dialog ini tidak
boleh mulai scroll di laptop biasa.

Lebar dialog **sengaja tidak dilebarkan** — 896px (`sm:max-w-4xl`) sudah cukup untuk sepuluh
field; memperlebar hanya menambah panjang baris yang tidak dibutuhkan sekaligus membuat 768px
lebih repot.

### Tiga seksi

| Seksi | Isi |
|---|---|
| **Request** | Date, Section, Employee No; lalu PIC Name (2 kolom) + Purpose (1 kolom) |
| **Access window** | Start Time, End Time — **dua** kolom, karena kolom kosong ketiga akan terbaca sebagai field yang hilang |
| **Authorisation** | PIC by ISD (lebar penuh), lalu dua canvas signature |

Judul seksi kecil (11px), uppercase, redup, masing-masing dengan `UIcon` kecil
(file-text / clock / pen-line) — menandai kelompoknya tanpa bersaing dengan label field di
bawahnya. Purpose dipendekkan dari `rows=2` lebar penuh menjadi `rows=1` satu kolom; itu yang
membayar tambahan tinggi canvas.

### Animasi stagger hampir rusak diam-diam — ini bagian menariknya

`.cctv-record-grid > *` dulu langsung menymatch field. Setelah redesign dibungkus tiga
`<section>`, `> *` hanya match **tiga section itu**. Tidak ada error, animasi tetap terlihat —
tapi setiap field di dalam satu section tiba sebagai satu slab, persis hal yang harus dicegah
oleh stagger. Diperbaiki dengan menymatch section **dan** `.cctv-record-grid > section > div > *`,
satu delay per SEKSI (0/70/140ms), dan pasangan signature paling akhir (210ms). Blok
`prefers-reduced-motion` dan `print` diperbarui dengan selector yang sama, kalau tidak keduanya
berhenti menutupi field.

### Verifikasi end to end (baris probe)

Isi sepuluh field, gambar di **kedua** pad, Save Row → baris muncul dengan **dua** image
signature (data-URL 4658 karakter masing-masing, jadi keduanya benar-benar tersimpan dan bukan
saling menimpa). Dialog hapus membaca Employee No / PIC / Purpose / Date / Section. Dihapus →
server kembali ke **11 baris CCTV, 0 handover, nol baris probe**.

> Jebakan harness yang memakan tiga percobaan: sesi login terus mati **dan** form diam-diam
> mengosongkan diri sendiri ("Username dan password wajib diisi") karena HMR reload dari edit
> CSS saya sendiri terjadi di antara fill dan submit. Mengisi **dan** submit dalam **satu**
> `browser_exec` call adalah pola yang bisa diandalkan; mengisi di satu call lalu submit di
> call berikutnya tidak.

## CCTV — urutan field form according (2026-10-05)

HIRO: *"change the position of date make it same line on section access windows, in order ->
Date, Start Time, End Time. Then in the request section in order -> Section, Employee No,
PIC Name, below that PIC by ISD, Purpose. then in Signature section make Signature title"*.

### Struktur hasil (dibaca ulang dari DOM, bukan diasumsikan)

| Seksi | Field |
|---|---|
| **Request** | Section, Employee No, PIC Name, PIC by ISD, Purpose |
| **Access window** | Date, Start Time, End Time |
| **Signature** | PIC Sign, ISD Sign |

Dua konsekuensi layout yang perlu diingat:

1. **Access window** berubah dari grid 2 kolom ke `sm:col-cols-3`, karena sekarang ada
   **tiga** nilai persis di baris itu — kolom kosong keempat akan terbaca sebagai field
   yang hilang.
2. **PIC by ISD pindah keluar** dari seksi Authorisation ke dalam Request. Itulah sebabnya
   seksi ketiga sekarang diberi judul **"Signature"**, bukan "Authorisation": isinya hanya
   dua pad, jadi judul yang lebih luas mengklaim lebih dari yang_isinya.

`sm:col-span-2` pada **Purpose** berpindah ke kolom lain di baris yang sama (sebelumnya
PIC Name yang 1 kolom; sekarang PIC by ISD 1 kolom dan Purpose 2 kolom), karena Purpose
adalah nilai free-text terpanjang sedangkan PIC by ISD hanya nama yang ter-prefill dari
sesi. `mt-3` pada grid pad dihapus karena `mb-2` pada judul seksi sudah memberi jarak itu —
`mt-3` itu hanya ada untuk menyingkiri baris yang sekarang tidak lagi memisahkan keduanya.

### Tingginya justru turun

Dialog **694px → 626px**ZBANpad tetap 130px, karena tiga field jadi satu baris
(Date + Start + End) dan Purpose pindah seeksip PIC by ISD — satu baris utuh hilang.
Sweep 1080/900/800/768/720 semua membaca 626px overflow 0; di 640 menyusut ke 576px dengan
pad 415×130, tetap overflow 0.

### Verifikasi

Diuji ulang end to end pada baris probe **sesudah** pengurutan: diisi mengikuti posisi
BARU, gambar di kedua pad (904 piksel tinta masing-masing), Save Row → baris muncul dengan
dua image signature 4658 karakter, dihapus → server kembali ke **11 baris CCTV, nol ZZ
Probe**.

CSS stagger tidak perlu diubah: selector-nya menyasar `> section > div > *`, dan
pengurutan ini hanya mengubah **field mana** di tiap seksi, bukan strukturnya.

## CCTV — baris kedua Request ditukar (2026-10-05)

HIRO: *"swap position of PIC by ISD and Purpose, keep purpose use 2 grid"*.

Purpose sekarang **pertama** dengan `sm:col-span-2`, PIC by ISD di kolom ketiga.
Grid tetap 3 + 3, baris tetap sama, tinggi tetap sama — hanya yang mana yang di kiri.

Diverifikasi dengan **membaca posisi label dari DOM**, bukan Apenas melihat screenshot:

```
Section      @x=532
Employee No  @x=821
PIC Name     @x=1111
Purpose      @x=532   <- kiri, 2 kolom
PIC by ISD   @x=1111  <- kanan, 1 kolom
```

Dialog tetap 626px overflow 0, dua pad tetap 422×130 berdampingan.

> Kebiasaan yang perlu dijaga: ketika permintaan pengurutan datang, **periksa juga blok
> komentar** di sekelilingnya, bukan hanya markup-nya. Komentar seksi di file ini masih
> menyebut urutan lama "PIC by ISD dan Purpose di bawah … 3 + (1 + 2)", dan komentar basi
> seperti itulah yang membuat orang berikutnya "memperbaiki" order-nya kembali.

## CCTV — icon plate penuh di title dialog (2026-10-05)

HIRO: *"add cctv icon in the form title, position in left title and subtitle"* →
*"make it full in the wrapper, dont separate the subtitle"*.

### Butuh TIGA putaran pengukuran, dan tidak satu pun melempar error

Gejalanya selalu cuma **angka piksel yang salah**, jadi tiap perbaikan harus diverifikasi dengan
`getBoundingClientRect`, bukan dengan dilihat saja:

1. **`size-9`/`size-10` diam-diam kalah.** `height` tetap dan `align-self: stretch` menyetel
   property yang **sama**; utility menang, jadi plate tetap bujur sangkar di header dua baris.
2. Ganti `w-10` + **`grid`** + `self-stretch` gagal **ke arah sebaliknya**, terukur 40×29:
   `display: grid`membuat alignment context sendiri, jadi `align-self` tidak pernah sampai ke
   cross axis baris dan elemen runtuh ke tinggi konten satu barisnya.
3. **Penyebab sebenarnya adalah tinggi header yang dikunci vendor**, bukan alignment plate-nya:
   header `UModal` membawa `min-h-(--ui-header-height)` — kunci 4rem yang sama yang membuat
   `PageHeader` jadi title-only di project ini — dan dengan `p-4 sm:px-6` bawaan, 32px padding
  -nya menyisakan content box hanya 32px untuk blok teks setinggi 41px. `min-h-0` membatalkan
   lantai itu sehingga **konten** yang menentukan tinggi, dan `items-stretch` pada header —
   bukan `items-center` — yang membuat semua item mengambil tinggi penuh baris.

**Hasil akhir terukur: plate 40×33 مقابل text block 33px** — persis sama, dan berlaku untuk
New Record (ikon video) maupun Edit Row (ikon square-pen).

### Kombinasi yang benar

```
header : relative z-10 flex items-stretch gap-3.5 p-4 min-h-0 sm:px-6
plate  : flex w-10 shrink-0 items-center justify-center self-stretch rounded-xl
         bg-primary/10 text-primary ring-1 ring-inset ring-primary/20 dark:bg-primary/15
```

Lebar tetap, **tanpa kelas tinggi sama sekali**, dan harus anak **flex** bukan anak grid.

### Kenapa #header diganti seluruhnya, bukan #title

`Modal.vue` me-render `<slot name="title">` **di dalam** `<DialogTitle>`-nya sendiri, yang
bersAUDARA dengan `<DialogDescription>`. Plate di slot itu hanya bisa setinggi baris title dan
tidak pernah bisa menj，dua baris — permintaan ini secara struktural mustahil dari #title.
Mengambil #header berarti mengambil alih tombol close juga (slot itu membungkus wrapper, slot
#actions, dan `<DialogClose>` sekaligus, Modal.vue baris 100–135), jadi tombol close dibangun
ulang sebagai `<button>` asli yang memanggil callback `close` milik DialogRoot.

**Kedua jalur penutupan diverifikasi masih jalan setelah pengambilalihan:** klik close menutup
dialog, ESC juga menutup, tabel tetap 11 of 11 di keduanya, dan `aria-label="Close"` terjaga.

Title dan subtitle kini **satu blok** (`min-w-0 flex-1`, title `leading-tight`, subtitle
`mt-0.5 text-sm leading-snug`) sehingga pasangan itu terbaca sebagai satu label dua baris,
bukan keterangan yang melayang di bawah judul. Tinggi dialog justru **turun**, 638 → **619px**,
overflow 0 di 1080/900/800/768/720.

> Pengulangan pelajaran project ini: kalau permintaannya soal **TASTE** bukan perilaku, tampilkan
> hasilnya dan tanya dulu sebelum commit. Versi pertama ikon ini saya commit lalu ditolak
> HIRO ("make it full in the wrapper, dont separate the subtitle") — bertanya memakan satu
> round, jauh lebih murah daripada mengirim header yang salah tampilan lalu revert.

## Handover — form disamakan dengan CCTV (2026-10-05)

HIRO: *"on new handover form adapt style from cctv form, then make remarks in the right of
name. make it use 2 colum grid. also make the section title like form cctv access"*.

Dua logbook yang terlihat berbeda terbaca sebagai dua aplikasi berbeda — jadi ini **salin
sengaja**, bukan desain paralel.

### 1. Header

Slot `#header` diganti seluruhnya, sama persis dengan CCTV: plate accent setinggi penuh
**40×29 terhadap text block 29px** (fills=true, terukur), ikon **package** untuk New Handover
dan **square-pen** untuk Edit Row, title+subtitle jadi satu blok `min-w-0 flex-1`, tombol close
dibangun ulang memanggil callback `close` milik DialogRoot.

Semua tiga fakta yang dipelajari di halaman CCTV dipakai ulang, **bukan** ditemukan ulang:

- slot `#title` **mustahil** menjangkau dua baris — `Modal.vue` me-render-nya **di dalam**
  `<DialogTitle>`, yang bersaudara dengan `<DialogDescription>`;
- `min-h-0` membatalkan lantai `min-h-(--ui-header-height)` milik vendor;
- `items-stretch` (bukan `items-center`) yang membuat item mengisi tinggi baris;
- plate butuh **lebar tetap, tanpa kelas tinggi sama sekali**, dan harus anak **flex**.

### 2. Tiga seksi

| Seksi | Isi (baris 1 | baris 2) |
|---|---|
| **Part** | Taken Date \| QTY | Part Name \| Brand |
| **Recipient** | **Name \| Remarks** | Employee No \| Section |
| **Signature** | Recipient Sign | — |

Permintaan spesifik HIRO — *Remarks di kanan Name* — terukur benar: **Name@x=204,
Remarks@x=631**, satu baris, Remarks di kanan. **Dua kolom** justru yang membuat pasangan itu
mengisi baris penuh; di tiga kolom akan ada celah.

Label field diubah dari "Signature" jadi "Recipient Sign" karena **seksi**nya sudah bernama
Signature — labelnya akan mengulang judulnya sendiri.

### 3. CSS stagger wajib ditulis ulang

`.handover-record-grid > *` dulu langsung match field. Sekarang anak langsungnya adalah tiga
`<section>`, jadi `> *` saja akan men-*stagger* tiga section itu dan **tidak ada field di
dalamnya** — kegagalan parsial yang senyap, karena animasinya tetap terlihat dan tidak ada error
yang muncul. Sekarang menyasar section **dan** `> section > div > *`, satu delay per section
(0/70/140ms). Blok `prefers-reduced-motion` dan `print` diperbarui dengan selector yang sama,
kalau tidak keduanya berhenti menutupi field.

Pad terukur 415×130, dialog 687px dengan overflow 0 di 1080/900/800/768, menyusut ke 656 di 720
dan 576 di 640.

### Verifikasi end to end (baris probe)

Create (807 piksel tinta, Save Row, nomor ter-assign) → Edit (judul berubah ke "Edit Row" dengan
ikon pena, QTY 1→5, signature 4446 karakter **bertahan** — itu lagi perilaku
omit-field-tersembunyi) → dialog hapus membaca Part Name / Name / Employee No / Taken Date / QTY
= nilai asli baris → dihapus → server kembali **0 baris handover, 11 baris CCTV, nol probe**.

## Handover — urutan kolom diperbaiki, Signature di tengah (2026-10-05)

HIRO: *"i want this position / Taken Date | Part Name / Brand | QTY / (baris kosong) /
Employee No | Name / Section | Remarks / make the signature section in the center of form"*.

Urutan sekarang dibaca **mendatar per baris**, bukan menurun per kolom — diverifikasi dengan
membaca label yang benar-benar ter-render per seksi:

| Seksi | Isi |
|---|---|
| **Part** | Taken Date \| Part Name · Brand \| QTY |
| **Recipient** | Employee No \| Name · Section \| Remarks |
| **Signature** | Recipient Sign |

Pasangan identitas (Employee No + Name) sengaja memimpin seksi Recipient — nomor dan orang yang
memiliki akun itu memang satu paket. Baris kedua brought Section dan Remarks.

> Catatan: ini **menggantikan** layout yang di-commit beberapa jam sebelumnya (Name | Remarks di
> baris 1, Employee No | Section di baris 2). Permintaan "pasangan kolom" dan permintaan
> "urutan baris" adalah dua hal berbeda, dan yang terakhir inilah yang berlaku.

### Centering butuh DUA mekanisme berbeda

Mencampur keduanya akan menghasilkan seksi yang setback centering-nya:

- **Judul seksi** di-center dengan `justify-center` pada flex row-nya sendiri. `items-center`
  tetap ada, karena sumbu itu soal menyejajarkan ikonnya.
- **Pad** di-center dengan `mx-auto` di dalam wrapper `max-w-2xl`, sehingga center-nya
  **terhadap dialog**. Merapitkannya ke kolom grid lalu `justify-center` adalah cara yang
  tampak wajar dan **diam-diam tidak melakukan apa-apa** di sini: pad adalah anak tunggal
  seksinya, jadi tidak ada kolom kedua untuk disejajarkan.

`max-w-2xl`, bukan lebar penuh, itu bagian yang disengaja — kotak signature yang diregangkan ke
856px bukan kotak signature, itu garis dengan gelayangan di atasnya. Pad terukur **672×130**,
lebar yang memang ditandatangani orang di kertas.

### Jebakan harness yang menghasilkan false negative

Pemeriksaan centering pertama saya membandingkan pad terhadap **border box** body modal dan
melaporkan leftGap 105 vs rightGap 120, yaitu "tidak|center". Padahal **sudah** center — padding
horizontal 20px milik body ikut terhitung di border box-nya, jadi perbandingannya dilakukan
terhadap kotak yang 40px lebih lebar dari konten sebenarnya. Diukur terhadap **content box
seksi lain** hasilnya leftGap 85 = rightGap 85 dan headingOffset 0 — itulah kebenarannya.

> **Aturan:** saat centering apa pun di dalam container berpadding, ukur terhadap **saudara seksi
> yang sama**, jangan pernah terhadap border box container-nya.

Sweep: dialog 687px dengan overflow 0 di 1080/900/768, menyusut ke 656 di 720.

Verifikasi end to end (baris probe): create (873 piksel tinta, Save Row, sepuluh field
tersimpan) → dihapus → server kembali **0 baris handover, 11 baris CCTV, nol probe**.

## Handover — Name tidak lagi ter-prefill, placeholder baru (2026-10-05)

HIRO: *"employee no placeholder change to 939432. do not fill name to Administrator put
placeholder Recepient"*.

Prefill itu ada karena form grid rata-rata dulu menganggap "menyerahkan part ke diri sendiri"
adali kasus umum — dan itu justru **bukan** apa yang yang terjadi pada handover. Petugas IT adalah orang yang
**menyerahkan**, bukan yang **menerima**, jadi prefill-nya mengarah ke pihak yang salah di baris
itu, dan membuat field terlihat terjawab sebelum labelnya sempat dibaca.

### Yang diubah

- `form.nama = currentUserName()` dihapus dari `openCreate`.
- Dua simbol jadi mati dan **dihapus**, tidak ditinggalkan: `currentUserName()` dan
  `const { user: me } = useAuth()`. Halaman ini sekarang tidak lagi memakai `useAuth` sama
  sekali — perlu dicek, karena `useAuth` tidak pernah ada di daftar import (itu auto-import),
  jadi sisa referensinya mudah terlewat.
- Placeholder Employee No `940900` → **`939432`**.
- Placeholder Name `"Recipient's full name"` → **`Recipient`**.

### Verifikasi

Dibaca ulang semua input yang ter-render: `nama` EMPTY dengan `ph=Recipient`, `no_pegawai`
EMPTY dengan `ph=939432`, `qty` masih ter-prefill `1`, `tanggal_ambil` masih hari ini.

Validasi wajib dicek ulang **karena prefill lama menutupinya** — menekan Save Row pada form
kosong kini melaporkan *"These fields are required: Part Name, Name, Signature"*, yang benar dan
membuktikan Name sekarang benar-benar wajib diketik. Sebelumnya error Name tidak akan pernah
terjevak di jalur create karena field-nya tidak pernah kosong.

Dialog tetap 687px, data tidak tersentuh (0 handover, 11 CCTV).

## Handover — label "Recipient Sign" dihapus (2026-10-05)

HIRO: *"Recipient Sign title to center"* → lalu langsung *"hapus saja Recipient Sign title"*.

Yang perlu dicatat adalah **kenapa** dua permintaan itu saling bertentangan dan hanya yang
kedua yang berlaku: judul seksi tepat di atas pad sudah berbunyi **"SIGNATURE"** dengan gaya
uppercase redup yang sama dengan Part dan Recipient, jadi label field satu baris di bawahnya
mengulang hal yang sama dua kali. Satu pad lebar di bawah judul Signature hanya punya satu
makna yang mungkin — label itu murni duplikasi, dan HIRO melihatnya begitu setelah label itu
dijadikan center di sebelah pad. Trik `ui.labelWrapper: 'w-full justify-center'` yang
ditambahkan sesaat sebelumnya ikut hilang bersamanya.

### Yang TIDAK BOLEH ikut terhapus

- **`name="tanda"`** — inilah yang mengikat field ke `form.tanda` di state `UForm`. Menghapus
  label tidak boleh berarti menghapus name.
- **`required`** — tetap menggambar asterisk merah dan tetap menggerakkan validasi halaman ini.

### Verifikasi

Seksi Signature kini me-render **nol label** sementara judulnya tetap berbunyi "Signature";
pad tetap center di lebar 672px dengan **leftGap 85 = rightGap 85**; dan menekan Save Row pada
form kosong **tetap** melaporkan *"These fields are required: Part Name, Name, Signature"* —
jadi field-nya masih wajib walau tidak lagi|SWEATKAN dirinya sendiri. Dialog 687px overflow 0,
data tidak tersentuh (0 handover, 11 CCTV).

> Pelajaran yang layak digeneralisasi: di project ini HIRO sudah **dua kali** meminta
> sesuatu di-center lalu meminta menghapusnya, dan dua-duanya permintaan kedua yang lebih baik.
> Tandranya: hal yang diminta di-center itu sudah berada persis di bawah judul yang
> mengatakan hal yang sama. Saat diminta menengahkan sebuah judul, cek dulu apakah judul itu
> memang perlu ada.

## Tema default: accent green + neutral zinc, dan restore yang benar-benar jalan (2026-10-06)

HIRO: *"make accent color green, and neutral color zinc as default theme for all user"*.

Sesi sebelumnya meninggalkan pekerjaan ini **setengah jadi dan belum di-commit**. Dua bug nyata
ketahuan waktu verifikasi — keduanya lolos dari pemeriksaan statis dan hanya muncul saat dijalankan.

### Bug 1 — `setPrimary` diam-diam ikut mengubah netral

`persist()` membaca `appConfig.ui.colors`, lalu menulis ulang **kedua** kunci ke cookie. Di luar
component setup `appConfig` bukan instance yang hidup, jadi `neutral` yang ditulis adalah nilai
default modul — **`stone`**, bukan `zinc`.

Terukur: `setPrimary('violet')` menghasilkan cookie `{"primary":"violet","neutral":"stone"}`.
Artinya memilih satu warna merusak pilihan warna lainnya.

Fix: helper `currentChoice()` yang membaca kedua kunci dari `appConfig`, memvalidasi tiap nilai
terhadap `VALID_PRIMARY`/`VALID_NEUTRAL`, lalu jatuh ke `DEFAULT_*` bila tidak valid. Tiap setter
persist dari situ. Sekarang satu sumber jawaban untuk "warna apa yang sedang aktif".

### Bug 2 — `restore()` dipanggil dari tempat yang salah, DIEMPAT KALI

`restore()` dicoba dari `onMounted` UserMenu → `onMounted` app.vue → plugin `onNuxtReady` →
plugin yang dipanggil langsung. **Keempatnya mengabaikan cookie.** Bukti yang makeshift: cookie
di-set ke violet/slate — warna yang **bukan** default lama, jadi tidak bisa dijelaskan oleh
migrasi — lalu di-reload, `--ui-primary` tetap hijau.

Penyebabnya `useCookie`: ref-nya di-resolve terhadap payload Nuxt instance yang sedang aktif, dan
panggilan dari `onMounted` komponen berjalan dengan effect scope komponen itu, bukan scope app.
Jadi ref yang dikembalikan bukan ref yang dibaca seluruh app. Yang justru tetap bekerja adalah
`setPrimary`/`setNeutral`, karena click handler di komponen ter-mount adalah konteks di mana ref
bersama itu memang di-scope.

Akibatnya `useCookie` **dibuang** sama sekali. Cookie dibaca dan ditulis langsung lewat
`document.cookie`. Kolornya tetap tinggal di `appConfig` — itu yang dibaca komponen Nuxt UI, dan
Nuxt UI memperlakukannya reaktif, jadi assignment memperbarui UI yang sedang berjalan.

Fix: plugin baru `web/app/plugins/theme.client.ts`, `restore()` dipanggil **langsung dari body
plugin** (bukan di dalam `onNuxtReady` — bentuk itu juga sudah dicoba dan gagal). `.client`
supaya tidak jalan saat SSR. Call site di `app.vue` dan `UserMenu.vue` dihapus, sisanya
komentar penjelas.

### Dua sumber kebenaran untuk satu setting

`app.config.ts` (`primary: 'green', neutral: 'zinc'`) adalah runtime truth yang dibaca
komponen; `DEFAULT_PRIMARY`/`DEFAULT_NEUTRAL` di composable adalah truth lapisan persistensi.
Keduanya diekspor dan saling-rujuk di komentar. Pernah melenceng: default lama green/slate
bertahan setelah file satu berubah.

### Migrasi cookie lama

Tanpa migrasi, permintaan ini hanya setengah jadi — dan kegagalan-nya tak terlihat di mesin yang
sedang menguji: setiap browser yang **pernah** membuka app (yaitu semua browser dengan user
sungguhan) tetap menyimpan slate-nya dan tidak pernah melihat default baru. Orang yang menguji di
profil baru akan melihat zinc dan melaporkan sukses.

`LEGACY_DEFAULTS = { primary: 'green', neutral: 'slate' }` diperlakukan sebagai **"tidak ada
pilihan"**, di-default-kan, lalu ditulis ulang. Trade-off ini eksplisit dan kecil: orang yang
memang pernah memilih tepat green + slate tidak bisa dibedakan dari yang tidak pernah memilih,
dan cookie-nya diperlakukan sebagai belum memilih. Alternatifnya — membiarkan cookie basi —
berarti perubahan default tidak terlihat sama sekali ke seluruh user base yang sudah ada.

### Verifikasi (semua lewat UI sungguhan + CDP, bukan baca cookie)

| Kasus | Hasil |
| --- | --- |
| Tanpa cookie | primary `#00DC82` (green), bg `oklch(0.21 0.006 285.885)` = zinc |
| Pilih **violet** | primary `oklch(70.2% 0.183 293.541)`, cookie `{primary:violet, neutral:zinc}` — **netral tidak ikut berubah** |
| Pilih **slate** | bg `oklch(0.208 0.042 265.755)`, cookie `{...neutral:slate}` — netral benar-benar berubah, ada bukti numerik |
| Reload setelah violet+slate | primary & bg **identik** → plugin restore bekerja |
| Cookie lama green+slate | ditulis ulang jadi `{primary:green, neutral:zinc}`, bg jadi zinc |
| Cookie rusak `%7Bbroken` | `#__nuxt` children = 1, path `/`, app tidak brick |
| Sweep `/logbook/cctvacc` (11 baris), `/logbook/handover` (1), `/users` (3) | semua render, primary ikut tema |

Submenu accent/neutral hanya terbuka setelah diklik dengan pointer sungguhan lewat
`Input.dispatchMouseEvent` — `element.click()` sintetis pada opsi select Reka UI tidak memilih.

Placeholder baru di handover ikut terverifikasi di dalam dialog Add Record:
`Keyboard / Mouse / Monitor / etc...` dan `Dell / Lenovo / HP / etc...`. Submit kosong tetap
memunculkan error → validasi form tidak ikut mati.

### Yang TIDAK saya ubah

Tidak ada theme override global. Permintaan ini memang tentang **default global**, jadi
`app.config.ts` yang tepat sasaran — bukan `dashboardPanel` atau slot lain yang pernah
membocorkan scrollbar ke halaman yang tidak benar.

## CCTV — chip preview tanda tangan diperbesar (2026-10-06)

HIRO: *"the sign preview in the table a bit small, can you improve"*.

Yang menghambat BUKAN kolomnya, tapi chip-nya — dan itu yang harus dibuktikan dulu sebelum
mengubah apa pun. Terukur di viewport kantor 1920: kolom PIC SIGN dan ISD SIGN **164px**,
`px-3` menyisakan 140px content box, sementara chip-nya hanya **108px**. Jadi ~32px dari tiap
kolom **tidak terpakai sama sekali**, dan tinta terkunci di 98×32.

PNG yang tersimpan rasio **3.2:1** (422×130), jadi yang membatasi adalah **lebar**, bukan
tinggi. Chip 132px + `h-10` menghasilkan tinta **122×40** — sekitar 27% lebih besar linear —
sambil **meninggalkan `<colgroup>` apa adanya**. Mengalokasikan ulang persentase kolom akan
memindahkan semua kolom lain dan berisiko merusak properti "dua kolom tanda tangan selalu sama
lebar" yang justru alasan `<table-fixed>` + `<colgroup>` ada di file ini.

### `max-w-full` itu WAJIB, bukan sekadar pengaman

Tabel membawa `min-w-[1180px]`, jadi di viewport **1280 atau 1440** kolom tanda tangan hanya
**117px** (93px content box), sementara di layar kantor 164px. **Terukur tanpa `max-w-full`:
chip 132px melebihi selnya sendiri sebesar 15px di kedua lebar itu** dan mendorong konten ke
kolom Start Time. Dengan `max-w-full`, chip menyusut mengikuti kolomnya.

Kekhawatiran yang wajar — "chip jadi mengikuti kolom, jadi baris yang berisi tanda tangan
selalu beda lebar dari baris yang berisi `—`" — **tidak berlaku di sini**. Ketidaksamaan itu
dari layout **auto**, di mana `w-[n%]` pada `<th>` cuma **hint** dan browser
membagikan sisa ruang tidak rata per baris. Under table-fixed + `<colgroup>`,
lebar kolom deterministik dan identik di semua baris, jadi `min(132px, sel)`
menghasilkan angka yang sama untuk semua baris.

### Cara mengukur tanpa menebak

Chip **LAMA dan BARU diukur berdampingan di sel yang sama** — node klon disisipkan ke DOM
yang sama lalu kedua-nya di-`getBoundingClientRect()` dalam satu panggilan. Ini menghapus
dua sumber kesalahan sekaligus: membandingkan angka antar sesi, dan menghitung dari CSS apa
yang seharusnya terjadi.

| viewport | kolom | chip LAMA / tinta | chip BARU / tinta |
| --- | --- | --- | --- |
| 1280 | 117px | 108px / 98×32 | 93px / 83×40 |
| 1920 | 164px | 108px / 98×32 | 132px / 122×40 |

### Trade-off yang harus dinyatakan

Di 1280 chip sekarang **mengecil** (tinta 83px, bukan 98px lama) karena chip yang lebih lebar
dari kolomnya akan mendorong sel tetangga. Di 1600/1920 — layar yang HIRO pakai — tinta jadi
**98×40 dan 122×40**, jelas lebih terbaca. Jadi permintaan "perbesar" terpenuhi di layar
kantor, dan di layar sempit luar biasa justru dikecilkan demi tidak merusak layout.

Sweep 1280/1440/1600/1920: tidak ada clipping atau pergeseran di lebar mana pun, jarak ke
kolom berikutnya tetap 12–20px, dan kedua kolom tanda tangan **tetap persis sama** di semua
lebar. Screenshot 1920 mengonfirmasi tinta terbaca, seragam, dan berada di dalam selnya.

> Catatan harness: perintah `Stop-Process` untuk restart dev server **diblokir tanpa
> persetujuan** dan tidak dicoba ulang. HMR ternyata sudah menyajikan kode baru — dibuktikan
> dengan membaca `className` yang tersaji, bukan dengan menganggapnya benar.

> Pelajaran yang lebih luas: **ukur dulu, baru ubah bagian yang terbukti menjadi pembatas.**
> Dugaan awal saya adalah kolomnya yang sempit; pengukuran membuktikan chip-nya yang kecil,
> dan kolomnya justru punya 32px yang tidak terpakai. Dan untuk data yang tidak bisa dibaca
> dengan yakin, mengatakannya adalah hasilnya — bukan mengisinya dengan tebakan.

## Signature PNG auto-crop kiri-kanan (2026-10-06)

Permintaan HIRO: *"make auto crop right and left the signature image result when there is
empty space"*. Commit `a4a34b0`, satu file: `web/app/components/SignaturePad.vue`.

### Masalahnya

Pad signature di dialog CCTV **422x130**, tapi tangan orang hampir nunca mengisi lebar
itu. PNG yang tersimpan jadi membawa margin kosong yang lebar, dan preview di tabel
mengecilkan **seluruh** gambar itu ke dalam chip setinggi 40px dengan `object-contain` —
jadi yang kecil adalah tintanya, bukan kotaknya.

### Yang dikerjakan

`exportDataUrl()` di `SignaturePad.vue` memotong margin transparan **kiri dan kanan saja**
(kedua sisi yang diminta), menyisakan bantalan **3 CSS px**, lalu mengembalikan PNG dari
canvas baru. Tinggi **sengaja** tidak disentuh: pad-nya pendek dan hampir penuh.

Tiga kondisi fallback, supaya tanda tangan tidak pernah hilang oleh crop:

| kondisi | hasil |
| --- | --- |
| ada tinta | dipotong + bantalan 3px |
| tidak ada tinta sama sekali | canvas penuh (seperti sebelumnya) |
| tinta < 16px (mis. satu titik nyasar) | canvas penuh |

Batasannya dihitung dalam **device pixel** karena backing store di-scale
`devicePixelRatio`; satu-satunya konversi yang perlu adalah `3 x dpr`.

### Efek samping yang WAJIB diperbaiki di perubahan yang sama

`paint()` menggambar ulang signature tersimpan dengan
`drawImage(img, 0, 0, width, height)` — meregangkan ke seluruh pad. Itu benar **hanya
selama** gambar tersimpan memang sebesar pad. Begitu export jadi lebih sempit dari pad,
mengedit baris akan **meregangkan** tinta melintasi lebar penuh: tanda tangan yang digambar
pada 55% lebar pad akan kembali pada 100%, lebih lebar dari apa pun yang bisa digambar user.
Terbukti di browser: record ter-crop 227px kembali **tergambar 422px**.

Fix: `paint()` sekarang menggambar pada **ukuran natural**
(`drawImage(img, 0, 0, img.naturalWidth, img.naturalHeight)`) dan tetap di kiri-atas,
supaya geometri yang ditandatangani user tetap sama saat diedit.

### Terverifikasi di browser (1920, bukan ditebak)

| yang diukur | hasil |
| --- | --- |
| goresan di 55% lebar pad -> PNG tersimpan | **113x130** (sebelumnya 422x130) |
| piksel tinta sebelum vs sesudah crop | **435 vs 435** — tidak ada yang terpotong |
| ink di dialog Edit untuk record ter-crop | **221px** dari PNG 227px (bukan 422) |
| tinta preview baris legacy (belum ter-crop) | 33x38 |
| tinta preview baris ter-crop | **68x40** |

Record lama **tidak tersentuh** — crop hanya berlaku saat export, jadi 13 record yang
sekarang ada tetap 422x130 dan tetap tampil apa adanya.

### Jebakan yang hampir menipu saya

Screenshot pertama **terlihat seperti crop justru memperkecil** tampilan: goresan uji saya
tinggi amplitudonya rendah, jadi memang melebar dan pendek. Angka yang mengalahkannya —
`vision` bilang "rows below look bigger", padahal tinta baris legacy hanya 33x38 vs 68x40
untuk yang ter-crop. Goresan uji kedua yang setinggi penuh mengonfirmasi arahnya benar.
Pelajarannya sama seperti di atas: **measurement menang atas screenshot**, dan untuk menilai
ukuran tinta, `naturalWidth x getBoundingClientRect()` lebih jujur daripada mata.

> Probe row yang dibuat untuk pengukuran (Crop Probe, Crop Realistic, Crop Edit,
> Full Height) **sudah dihapus semua**; tabel kembali ke 13 baris.

### BUG yang saya perkenalkan sendiri: tinta 2x besar + meleset di dialog Edit

HIRO melapor: *"in edit row modal, the signature is moved to the right"*. Dua defect nyata,
**keduanya konsekuensi dari crop di atas** — sudah diverifikasi ulang di browser, bukan
menebak.

**1. Tinta tergambar DUA KALI LEBIH BESAR di layar HiDPI.** `paint()` menggambar
`img.naturalWidth` (device pixel) ke dalam context yang **sudah** di-`scale(dpr, dpr)` oleh
`resize()`. Jadi di dpr=2 tinta keluar 2×. Di dpr=1 tidak terlihat sama sekali — itulah
alasannya lolos dari pengujian saya sebelumnya, dan **layar 1920 laptop HIRO yang
memunculkannya**. Terukur: PNG 201px tapi tinta gambar **392 device px**, mengisi setengah pad.

**2. Posisi dianggap "meleset ke kanan".** Crop membuang posisi asli tinta di dalam pad, jadi
`x = 0` hanya hasil "nempel di tepi kiri dengan ekor kosong panjang" — itu terbaca meleset,
bukan sekadar rata-kiri. `vision` bahkan melapor "left-anchored", jadi laporan mata dan
visi sama-sama salah arah; yang benar adalah **ekornya yang hilang penyeimbangnya**.

Fix di `paint()`: bagi dengan `dpr`, lalu **center horizontal**. Posisi vertikal tidak
disentuh karena export tidak memotong tinggi — jadi `y` masih informasi yang benar.

| kondisi | tintanya | jarak kiri | jarak kanan |
| --- | --- | --- | --- |
| dpr=2, SEBELUM | 392 device px (2×) | 5 | 447 |
| dpr=2, SESUDAH | **196 device px (1:1 dari PNG 201)** | **324** | **324** |
| dpr=1, SESUDAH | **196px** | **113** | **113** |

Kiri == kanan di kedua DPR = benar-benar center, dan ukuran tintanya cocok 1:1 dengan PNG
yang tersimpan, jadi tidak lagi dobel dan tidak gepeng.

> Pelajaran: **efek samping dari sebuah optimasi bisa tersembunyi justru di konfigurasi
> tempat saya menguji.** dpr=1 adalah kondisi yang justru MENUTUPI bug — yang hanya muncul
> di dpr>1 akan lolos semua test di sini. Untuk canvas HiDPI, DPR bukan detail, tapi
> **variabel uji yang wajib diaktifkan ulang setelah `Page.navigate`** (override kembali ke 1
> kalau tidak dipasang ulang di panggilan berikutnya).

## Handover — crop + preview yang sama dengan CCTV (2026-10-06)

HIRO: *"implement crop dan preview adjustment juga ke form handover"*. Commit `b64c7b8`,
satu file: `web/app/pages/logbook/handover.vue`.

### Yang sebenarnya perlu diubah — dan apa yang TIDAK

`SignaturePad.vue` itu **komponen bersama**, jadi crop saat export dan perbaikan
centering di `paint()` **sudah otomatis aktif di handover** begitu dua commit sebelumnya
tidak merusak apa pun. Yang belum ikut cuma **chip preview di tabel** — masih `w-[108px]`
+ `h-8` (tinta 98x32) sementara CCTV sudah `w-[132px] max-w-full` + `h-10` (122x40).

Jadi diff-nya hanya baris class chip itu. Menyalin seluruh mekanisme crop ke handover
justru akan jadi duplikasi yang harus diperbaiki dua kali di setiap perbaikan berikutnya.

`max-w-full` itu **wajib**, bukan defensif: tabel handover `min-w-[1240px]`, jadi di 1280
sel signature lebih sempit dari chip 132px dan akan **membanjiri** kolom Section + Remarks.
`<colgroup>` sengaja tidak disentuh — handover cuma punya SATUR kolom signature (11%), jadi
sifat "dua kolom signature sama lebar" milik CCTV tidak berlaku di sini.

### Pad handover lebih besar dari CCTV, jadi crop lebih menguntungkan

Pad signature handover **672x130**, bukan 422x130 seperti CCTV — karena form-nya `max-w-2xl`
dan di-center. Artinya margin kosong yang dibuang crop **jauh lebih lebar** di halaman ini.

| yang diukur | hasil |
| --- | --- |
| pad handover | 672x130 |
| goresan di 15–60% lebar pad → PNG tersimpan | **310x130** |
| chip 132px di dalam sel | muat di sel **135px** (overflow -3px) |
| tinta preview record ter-crop | 95x40 |
| dpr=2, ink di dialog Edit | **304 device px**, kiri **520** == kanan **520** |

### Jebakan: "0 rows" bukan berarti data hilang

Setelah delete probe lewat UI, tabel menampilkan **0 rows** padahal row asli HIRO
(Keyboard/Rama) bersama probe masih ada di server. Saya cek langsung ke API: `total: 2`,
record 140 (Chip Probe) dan 134 (Keyboard). Setelah **full reload** barisnya kembali 2 —
jadi itu state transien saat refetch, bukan data hilang.

Probe lalu saya hapus **lewat API** (`DELETE /api/records/9/140`) dengan `KEEP = {134}`,
dan script itu mencetak before/after-nya, jadi sekarang hanya ada **134** — row asli
HIRO utuh. **Jangan pernah percaya angka "0 baris" di UI sebagai bukti penghapusan**;
baca server.

`vision` juga salah di sini: melaporkan dua chip "tidak sama besar", padahal keduanya
**132x50 persis** — hanya border yang lebih terlihat pada baris yang tintaunya besar.

## Garis preview terputus-putus (2026-10-06)

HIRO: *"kenapa preview signature pada tabel tidak smooth line nya?"*.

### Penyebabnya BUKAN crop — tapi rasio pengecilan

Terukur di PNG asli yang tersimpan: `scaleX == scaleY == 3.25` untuk signature yang
setinggi penuh. Jadi goresan `lineWidth: 1.6` CSS px itu tiba di preview sebagai **~0.5
device px** — lebih tipis dari SATU piksel. Di bawah satu piksel, resampler tidak bisa
meng-blend garis, dia **membuang** pikselnya, dan kurva jadi putus-putus. Ink piksel di
preview melonjak dari 121 ke 101 di beberapa baris: bukti piksel hilang, bukan piksel
tersebar.

### Dua perbaikan, satu sebab yang sama

| perubahan | angka |
| --- | --- |
| `lineWidth` 1.6 -> **2.4** CSS px | setelah downscale ~0.7–1.8 device px, selalu >= 1 px |
| crop **semua sisi**, bukan hanya kiri-kanan | tinggi yang terpakai hanya **42%–89%**, jadi ini yang menurunkan downscale dari 3.25x ke 1.35x–2.41x |

Tinggi sengaja ikut dipotong: permintaan awal hanya menyebut kiri-kanan, tapi pengukuran
bilang **margin vertikal adalah buangan yang lebih besar**. `paint()` kini center di
kedua sumbu, karena crop tinggi membuang posisi vertikal persis seperti crop lebar
membuang posisi horizontal.

### Metrik verifikasi: JUMLAH KOMPONEN TERHUBUNG di preview

Ini tes langsung dari "apakah ini satu garis", bukan soalTF screenshot:

| baris | PNG tersimpan | preview | downscale | komponen |
| --- | --- | --- | --- | --- |
| **baru (Smooth Probe)** | 270x115 | 94x40 | 2.88x | **1** (satu garis utuh) |
| lama (Asdad) | 201x130 | 62x40 | 3.25x | 5 |
| lama (Riswanto) | 81x130 | 25x40 | 3.25x | 2 |
| lama (Riswanto) | 415x130 | 122x40 | 3.25x | 3 |

Round-trip juga diukur: PNG 270x115 kembali di dialog Edit sebagai tinta **264x110**,
`gapL == gapR == 79` dan `gapT == gapB == 10` — center di kedua sumbu, ukuran 1:1.

Record lama **tidak tersentuh**: crop dan lebar goresan sama-sama berlaku saat export, jadi
14 signature yang sudah tersimpan tetap memakai goresan tipis lama. Kalau HIRO mau
semua baris ikut mulus, itu butuh data migration, bukan perubahan kode.

### Dua jebakan yang menghabiskan waktu (dan wajib dicatat)

1. **JWT kedaluwarsa membuat save 401 SENYAP.** `api.log`: `SecurityTokenExpiredException
   IDX10223 ... ValidTo 10/5 23:16 UTC, Current 10/6 10:16 UTC`. Gejalanya menyesatkan:
   dialog **tertutup**, tidak ada toast error, tidak ada record baru — dan `window.__errs`
   berisi `[]`. Saya sempat hampir menyimpulkan ada bug di kode crop. Akar masalahnya
   **bukan kode sama sekali**. Jadi saat sebuah aksi UI "tidak terjadi", **baca log server
   sebelum menuduh kode**: `grep "Request starting.*POST" api.log` showed **nol** POST.

2. **Override CDP ke-1920 hilang tanpa jejak.** Viewport jatuh sendiri ke 1264x569, dialog
   jadi ter-scroll, dan koordinat klik saya meleset — jadi klik "Save" mendarat di
   tempat lain. Gejalanya identik dengan "tombol tidak berfungsi": dialog tertutup,
   tidak ada record. **Selalu pasang ulang `Emulation.setDeviceMetricsOverride` di awal
   call yang butuh koordinat**, jangan andalkan override dari call sebelumnya.

## Halaman Log Audit (2026-10-06)

HIRO meminta halaman baru berisi **semua aktivitas user** — "design plagus mungkin, professional
dan informatif. pastikan animasi dan transisi nya juga ada". reseller  belum ada audit sama sekali
(`grep -ril "audit\|activity"` kosong), jadi ini semuanya dari nol.

**Why bukan middleware:** kalau cukup sekadar mencatat request, middleware lebih mudah. Tapi
middleware akan mencatat **percobaan** yang gagal dan request yang lalu di-rollback. Log audit
harus berisi kejadian yang benar-benar terjadi. Jadi tiap endpoint mutating memanggil
`_audit.LogAsync(...)` **setelah** work-nya sukses.

Tiap baris login gagal tetap dicatat di kedua cabang (user tidak ada / password salah) —
log yang hanya berisi keberhasilan tidak bisa menjawab "ada yang mencoba masuk?".

**Isi kolom hanya nama field, tidak pernah nilainya.** `AuditDetails` menyimpan
`["nama_barang","qty"]` atau flag `passwordChanged:true`, bukan isi field. Free text, email,
dan data signature tidak ikut ter-copy ke tabel append-only yang dibaca lebih bebas
daripada logbook-nya.

`AuditController` **read-only by construction** — tidak ada endpoint write/update/delete,
dan tidak ada affordance hapus di UI.

### 500 saat `GET /api/audit`

```
g.GroupBy(a => a.Username).Select(g => new AuditActor(g.Key, g.Count()))
```

EF memaksa **client evaluation** (`g.AsQueryable().Count()`) → "could not be translated".
Perbaikan: project ke **anonymous type**, mapping ke `AuditActor` di memory. Blok agregat
`GroupBy(_ => 1)` juga diganti `CountAsync()` terpisah.

Pelajaran: `new T(...)` dari class DTO di dalam `.Select()` EF = client evaluation = 500.
Anonymous type dulu, mapping sesudahnya.

### Animasi baris harus dibuktikan, bukan diasumsikan

Claim "ada animasi fade-in" tidak terbukti hanya dari ada `<TransitionGroup>` di source.
Yang diukur: `Page.addScriptToEvaluateOnNewDocument` + rAF sampler, lalu baca `opacity`
per frame. Hasil: **16 nilai opacity berbeda, 0 → 1**, delay tetap `0.018s` (fixed, bukan
fungsi `loading`). Animasi nyata.

`window.__var` **tidak bertahan across  navigasi** — recorder harus dipasang via
`Page.addScriptToEvaluateOnNewDocument`, bukan `js()` sebelum `goto_url`.

### Kolom yang terpotong padahal tabel 45% kosong

Screenshot review menemukan TARGET fixed 150px → `"Handover Log Bo..."` ter-ellipsis,
sementara DETAIL memegang 991px yang sebagian besar kosong. **Kolom terpotong di sebelah
kolom kosong terbaca sebagai bug.** Rebalance: kolom wasi diberi ruang, IP masuk sebagai
kolom Source yang informatif (di dev selalu `::1`, di server kantor baru berarti).

Verifikasi harus hitung elemen: `<colgroup>` 6 `<col>`, 6 `<th>`, 6 `<td>`, colspan 6.
Kolom hasil di 1920: 150/170/188/210/116/764.

### Warna: satu makna = satu warna

Kartu "Deletions" with a stray dot  amber sementara baris yang dihitungnya merah. Dua hue untuk
satu kelas kejadian terbaca sebagai salah set-up . Sekarang keduanya `text-error`.

Baris gagal kehilangan action-nya (icon diganti cross generik, jadi tidak jelas action apa
yang ditolak). Sekarang icon + nama action tetap, state merah dibawa tag kecil "REJECTED" —
**warna tidak boleh jadi satu-satunya pembawa makna.**

### Posisi menu

`mt-auto` menempel di **group terakhir** sidebar. Group Log Audit terpisah mengambil slot
itu dan mendorong User Management ke atas. Log Audit harus di **group yang sama, sebelum**
User Management. Terukur: Log Audit y=943, User Management y=975, profil y=1024.

### Search tidak pernah menulis nilainya (bug nyata, bukan UX)

Input search terikat dengan `:value="search"` (satu arah) dan dibaca lewat `onSearchInput()`,
tapi handler itu **tidak pernah menulis `search.value`** — hanya menjadwalkan reload. Jadi
mengetik tidak mengubah apa pun: term tidak pernah masuk query.

`search` juga ikut ada di `watch([search, ...])`, yang justru **membatalkan debounce-nya**:
watcher tetap jalan tiap ketukan, jadi timer 280ms hanya menjadwalkan request kedua yang
identik. Satu jalur memiliki term (handler), watcher hanya memiliki select.

**Akibat tidak terlihat: mengubah ref bukan hal yang sama dengan meminta reload.** Setelah
`search` dikeluarkan dari watcher, `clearFilters()` jadi tidak memicu apa pun — tombolnya
mengosongkan input tapi tabel tetap menampilkan hasil 0 baris lama dengan
"Showing 0 of 0 events". Diperbaiki dengan `load()` eksplisit di dalam `clearFilters()`.

Verifikasi: request di-patch di halaman sebelum mengetik. Ketik 8 karakter
`operator` → **tepat 1 request**, bukan 8. Sesi bersih: `zzzznotathing` → 0 baris,
`handover` → 3, keduanya HTTP 200.

**HMR melayani kode basi setelah patch.** Right setelah patch, `zzzznotathing` mengembalikan
15 baris di browser padahal API-nya 0. Full reload → 0. Selalu uji state baru di sesi bersih;
HMR bukan bukti.

### Tabel fit in screen, nol page scrollbar

`max-h-[62vh]` membuat halaman scroll **dua kali**: panel body (1050 > 1016, jadi 34px
keluar) plus tabelnya sendiri. Diubah jadi rantai flex height:

`panel body:flex min-h-0 flex-col` → `content wrapper:flex min-h-0 flex-1 flex-col` →
`register card:flex min-h-0 flex-1 flex-col` → `table wrapper:flex-1 min-h-0`,
dengan filter bar dan pager `shrink-0`.

`min-h-0` wajib di setiap link: item flex default `min-height:auto` dan **menolak menyusut**
di bawah isinya, bukan melepaskan ruang.

Diverifikasi di 1920×1080, 1600×900, 1366×768, 1280×720 dengan tabel PENUH (16 baris):
`html scrollHeight == clientHeight` di semua resolusi, dan elemen yang scroll hanya
`.audit-table`. Panel body tidak overflow sama sekali.

Scrollbar panel body disembunyikan (`scrollbar-width:none` + `::-webkit-scrollbar{width:0}`)
dengan class `.audit-panel-body` via `:ui` **per-instance** — `overflow-y:auto` tetap
supaya viewport pendek tetap degrade jadi scroll yang berfungsi, bukan pager terpotong.

### Lebar kolom: vision menemukan kebalikan dari masalah lama

Setelah kolom Date ditambah, screenshot review menemukan kebalikannya: SOURCE 112px
untuk isi `::1` atau `—` (jarak kosong besar), sementara TARGET 200px membuat
`"Handover Log Book / #183"` **wrap dua baris**. Digeser: SOURCE 84px, TARGET 236px.
Hasil: cell TARGET seragam 57px (satu baris), nol ellipsis.

HIRO: *"ketika tombol delete record ditekan ada seperti popup muncul sepersekian detik
sebelum modal tertutup"* — di **CCTV Access dan Handover**.

### Bukan popup liar — itu toast yang memang dipanggil, tapi terlalu cepat

`notify('Record deleted')` dipanggil **di dalam** `confirmDelete`, sementara `showDelete`
baru di-`false` **dua baris berikutnya**. Terukur dengan MutationObserver:

| t (ms) | dialog | toast |
| --- | --- | --- |
| 16246 | **closed** (masuk animasi keluar) | none |
| 16263 | closed | **muncul** |

Jadi toast datang **17ms setelah** modal mulai menutup — masih di atas layar. Mata
membacanya sebagai popup nyasar.

### Fix: tunggu `after:leave`

`UModal` (Nuxt UI 4) meng-emit `after:leave` — sudah dicek langsung di
`node_modules/@nuxt/ui/dist/runtime/components/Modal.vue`, bukan dari dokumentasi:

```js
const emits = defineEmits(["leave", "after:leave", "enter", "after:enter", ...])
```

Toast sukses sekarang ditahan di `pendingDeleteToast` dan dilepas dari
`@after:leave`. `loadRows()` tetap jalan **langsung** — jadi tabel di belakang modal
sudah benar saat modal menutup; hanya toast yang ditunda. Toast **error** tetap inline,
karena saat delete gagal modal memang harus tetap terbuka.

Hasil terukur di Handover (probe baris buang, dihapus **berdasarkan identitas**):

| t (ms) | keadaan |
| --- | --- |
| 28833 | dialog `closed` |
| 29082 | dialog **hilang dari DOM** |
| 29091 | **toast muncul** — 258ms setelah modal hilang |

Sebelum: toast 17ms *sebelum* modal selesai menutup. Sesudah: 258ms *setelah* modal
hilang. Commit `6fdd62d`, kedua halaman memakai mekanisme yang sama.

### Pelajaran terpenting dari kejadian yang sama

Beberapa kali dalam sesi ini **koordinat klik meleset** (override CDP hilang, atau baris
bergeser setelah `loadRows()`), dan klik delete yang dimaksud mendarat di record lain.
`purge_ho_probe.py` sempat saya tulis dengan `KEEP = {"134"}` lalu saya nyatakan aman —
padahal targetnya dipilih dari `querySelectorAll('button')[1]`, **bukan dari ID**, jadi
"KEEP" itu tidak melindungi apa pun saat koordinat bergeser.

Aturan yang sekarang berlaku: **untuk operasi destruktif, pilih target berdasarkan ID dari
server, bukan berdasarkan indeks/posisi di DOM, dan print ulang sisa baris setelahnya.**
`audit_rows.py` dibuat persis untuk ini — ia mencetak `(id, nama)` dari API, jadi
"data hilang" langsung terlihat, bukan baru ketahuan dari angka baris di UI.

---

## Dashboard — panel System Metrics dirancang ulang (2026-10-08)

### Kenapa ini perbaikan, bukan sekadar poles tampilan

Panel lamanya rusak secara teknis, dan kerusakannya tidak pernah melempar error:

1. **Garis data lari keluar canvas.** `x = pad + (i/(n-1)) * width` memakai lebar penuh, bukan
   `width - 2*pad`, jadi titik terakhir jatuh di `x = width + pad` — di luar bitmap. Akibatnya
   garis selalu tampak menembus sumbu-Y vertikal.
2. **Bitmap tidak diskalakan DPI.** `canvas.width = clientWidth` tanpa `devicePixelRatio`,
   jadi setiap garis buram di layar HiDPI.
3. **`useFetch` dipanggil di dalam `setInterval`.** `useFetch` itu composable setup; dipanggil
   berulang dari timer ia membuat request baru tiap 8 detik, dan `setInterval` tetap menembak
   walau request sebelumnya belum kembali (request menumpuk saat API lambat).
4. **Sumbu tanpa satu pun angka** — digambar sebagai huruf L kosong, jadi tinggi garis tidak
   bisa dibaca.
5. **`ram` memakai `% Committed Bytes In Use`** — itu rasio terhadap commit limit, bukan
   pemakaian RAM fisik. Di panel berlabel "Memory" angkanya salah arti.
6. **`PerformanceCounter` mati di Windows non-Inggris.** Nama counter ("Processor", "Memory")
   hanya ada di Windows en-US. Di Windows Server kantor, kalau lokalisasinya bukan en-US,
   konstruktornya melempar exception dan `/api/metrics` menjadi 500.

### Yang berubah

**API — `api/Services/SystemMetricsService.cs` (baru), `MetricsController` jadi tipis.**
Sampler sekarang memakai `GetSystemTimes` + `GlobalMemoryStatusEx` (kernel32 langsung, tanpa
paket NuGet) sehingga bebas locale, dan CPU dihitung sebagai **delta antar dua sampel** — karena
itu service-nya **singleton**: baseline harus diingat antar-request. Implementasi lama
menyiasatinya dengan `Thread.Sleep(100)` **di dalam handler HTTP**; itu hilang. Paket
`System.Diagnostics.PerformanceCounter` **dihapus** dari `Api.csproj` setelah grep seluruh repo
memastikan tidak ada pemakai lain.

Kontrak JSON lama (`cpu`, `ram`, `disk`) **tidak berubah**, hanya ditambah field aditif:
`cores`, `host`, `os`, `uptimeSeconds`, `ramUsedBytes`/`ramTotalBytes`,
`diskUsedBytes`/`diskTotalBytes`, `diskDrive`. `ram` sekarang diturunkan dari dua angka yang
sama yang dicetak panel (used/total), sehingga persen dan "12,6 / 63,9 GB" mustahil bertentangan.

**Web — `MetricSparkline.vue` (baru) + `MetricsChart.vue` (ditulis ulang), `index.vue` berhenti
membungkusnya dengan UCard ganda.** Satu kolom per metrik (CPU | Memory | Disk); setiap kolom:
ikon + label, angka besar, sparkline canvas 70px, baris detail (`16 logical cores` /
`12.6 / 63.9 GB` / `377.6 / 464.8 GB (C:)`) dan `puncak x%`. Header: judul + pill `Live` dengan
titik berdenyut (`--ui-primary`) + jam pembaruan. Footer: `host | OS | up 3d 2h` dan
`Riwayat 60 sampel (5 menit)`.

Tetap **tanpa library chart**: 3 canvas × 60 titik, nol tambahan bundle — server produksi offline.

Keputusan yang disengaja, masing-masing punya alasan:

- **Skala Y mengikuti jendela tapi selalu berlabel.** Skala 0-100 tetap membuat host idle jadi
  garis mati; skala otomatis tanpa label membuat bentuk yang sama bisa berarti 5% atau 95%.
  Yang dipakai: puncak jendela × 1.25 → dibulatkan ke langkah enak (5/10/20/25/50/75/100), dan
  **label hanya di ujung** (0 dan max). Garis tengah sengaja tidak diberi label: dengan plafon
  25 garis tengahnya 12,5, dan label "13" di panel persentase terbaca seperti bug.
- **Kurva Fritsch-Butland (monotone cubic).** Catmull-Rom biasa *overshoot* — ia mengarang puncak
  yang tidak pernah terjadi dan bisa turun di bawah nol pada garis datar. Versi monotone tidak bisa.
- **Transisi antar sampel** 420ms dengan easing keluar kubik, dilewati total saat
  `prefers-reduced-motion`. Sampel baru masuk dari kanan; titik lama tidak bergeser.
- **Readout hover**: `pointermove` menggambar garis putus-putus vertikal, titik, dan bubble
  `20.54.19  5.6%`.
- **Polling = rantai `setTimeout`, bukan `setInterval`**, ditambah: tab tersembunyi
  (`document.hidden`) tidak dipoll sama sekali, dan setelah 3 kegagalan berturut intervalnya
  jadi 20 detik alih-alih menembak API yang mati tiap 5 detik. Kembali ke tab langsung poll.
- **Nilai ≥ 90% berubah ke `--ui-error`** — angka yang berubah warna, bukan badge tambahan,
  supaya tidak menambah keramaian.
- Nama OS dinormalkan di klien: .NET melaporkan `Microsoft Windows 10.0.26200` karena Windows 11
  masih mengaku 10.0. Build ≥ 22000 ditampilkan `Windows 11 (build 26200)`; tanpa itu panel
  terbaca seolah salah mengenali OS.

### Verifikasi (runtime, bukan hanya build)

- `/api/metrics` sebelum: `{"cpu":3,"ram":17,"disk":81.2}`. Sesudah: CPU `3` → `4.9` pada dua
  panggilan berjarak 6 detik (**bukti delta bekerja**), RAM 17% = 11.68/68.63 GB (konsisten
  dengan persennya), disk 81.2% = 405.3/499.05 GB.
- `dotnet build`: **0 error, 0 warning**. Log API: **26 × `GET /api/metrics - 200`**, nol error.
- Browser di **1920×1080 (resolusi kantor)** dan **1280×900**: `document.documentElement.scrollWidth == window.innerWidth` → tidak ada overflow horizontal; kolom 509px dan 295px.
- Warna angka dibaca dari `getComputedStyle`, bukan diasumsikan: `rgb(0,193,106)`,
  `rgb(56,189,248)`, `rgb(245,158,11)`.
- Hover dibuktikan dengan `Input.dispatchMouseEvent` di atas canvas: piksel tinta 9.534 → 10.937
  dan bubble `20.54.19 5.6%` tergambar.
- **Mode terang ikut dicek** (kelas `dark` dilepas): grid, isian area, dan label tetap terbaca.
- **Keadaan API mati diuji sungguhan, bukan disimulasikan** (proses API di-stop): pill jadi merah
  `API offline`, footer menampilkan `Metrik tidak tersedia (API tidak merespons).`, nilai terakhir
  **dibiarkan di layar** (tidak di-nol-kan supaya tidak terbaca seperti host baru reboot), dan dev
  server hanya mencatat **4** ECONNREFUSED selama jeda itu — bukti backoff bekerja, bukan menembak
  tiap 5 detik. Setelah API dihidupkan lagi panel kembali `Live` **tanpa reload halaman**.

### Catatan keamanan (belum diubah, disengaja)

`/api/metrics` masih **anonim**. Siapa pun yang bisa menjangkau API dapat membaca nama host,
versi OS, jumlah core, dan kapasitas disk tanpa login. Klien web sudah mengirim header
`Authorization` lewat `authHeaders()` di `useApi.ts`, jadi menambahkan `[Authorize]` ke
`MetricsController` tidak akan merusak panel. Belum diubah karena di luar permintaan — tunggu
keputusan HIRO.

### Lanjutan (2026-10-08, sore) — bahasa, Quick Actions, banner

Tiga permintaan HIRO sekaligus, semuanya selesai dan diverifikasi di browser:

1. **Panel System Metrics jadi bahasa Inggris** (`Beban host, sampel tiap 5 detik` → `Live host
   load, sampled every 5s`; `Diperbarui` → `Updated`; `puncak` → `peak`; `Riwayat 60 sampel (5
   menit)` → `60 samples (5 min)`; `Menunggu data…` → `Waiting for data…`; pesan error →
   `Metrics unavailable (API not responding).`). Jam memakai locale `en-GB` supaya tampil
   `21:11:26`, bukan `21.11.26` seperti format id-ID sebelumnya.
2. **Section Quick Actions dihapus** beserta seluruh markup/`NuxtLink`-nya (bukan disembunyikan).
   Halaman Dashboard sekarang berakhir setelah tiga kartu modul.
3. **WelcomeBanner di-enhance agar cocok di dark dan light mode.** Yang ditambahkan: dua wash
   gradien diagonal (`dark:` lebih kuat), grid aksen 44px yang di-mask supaya hanya muncul di
   area glow kanan, orb kedua yang lebih redup di kiri-bawah sebagai counter-glow, hairline
   aksen di tepi atas (memberi definisi di light mode, tempat bayangan nyaris tak terlihat), dan
   `ring-1 ring-inset ring-primary/10` pada kartu. Semua tetap memakai token aksen, jadi ikut
   berubah saat user ganti warna.

**BUG NYATA yang ketahuan saat verifikasi dan layak diingat:** rencana pertama saya membuat dua
set keyframes untuk bintang dan menukarnya per mode lewat `:global(html:not(.dark)) .infra-star`.
Vue mengompilasi selector itu menjadi **`html:not(.dark) { animation-name: infra-twinkle-light }`**
— bagian `.infra-star` **dibuang**, sehingga aturannya menempel ke elemen `<html>`, bukan ke
bintangnya. Akibatnya: rule light mode itu tidak pernah menyentuh satu bintang pun (computed
`animationName` bintang tetap nama versi dasar), **dan** karena selector yang sama saya pakai juga
di blok `prefers-reduced-motion`, di sana ia menjadi `html:not(.dark) { opacity: 0.55 !important }`
— yaitu **meredupkan SELURUH halaman** untuk pengguna yang mengaktifkan reduce-motion. Terdeteksi
bukan dari membaca kode, tapi dengan **membaca kembali `cssRules` dari stylesheet yang hidup** dan
mencetak aturan yang mengandung `infra-twinkle`. Perbaikan: buang `:global` dari file ini
sepenuhnya, pakai satu rentang opacity (0.14 → 0.85) yang enak di kedua mode, dan untuk apa pun
yang mode-dependent gunakan varian `dark:` Tailwind. Pelajaran umum: **`:global(...)` dengan
descendant di scoped CSS Vue tidak bisa dipercaya — verifikasi lewat cssRules, jangan dari tebakan.**

Verifikasi akhir (viewport 1920×1080): `cssRules` hanya menyisakan satu aturan
`@keyframes infra-twinkle-…`, `getComputedStyle(html).animationName === 'none'` dan
`opacity === '1'` (bukti bug peredupan itu hilang), 3 canvas hidup, teks Inggris ada, dan
`Quick Actions` tidak ada lagi di DOM. Dark dan light keduanya difoto dan diperiksa.

### Banner: revisi setelah "wah jelek" + twinkle dipercepat (2026-10-08)

HIRO melihat hasil enhance pertama dan langsung menolak: *"wah jelek bannernya"*. Setelah saya
zoom sendiri screenshotnya, penyebabnya jelas dan **bukan soal selera yang samar** — tiga hal
memang salah secara visual:

1. **Grid 44px** terbaca sebagai **artefak render / grid debug**, bukan tekstur.
2. **Orb kedua di kiri-bawah** menimbulkan bercak hijau pucat di bawah nama — seperti noda dan
   pencahayaan yang tidak rata.
3. **Dua wash gradien ditumpuk** di atas dua orb blur menghasilkan tampilan **blotchy** dan
   nuansa olive kotor, dengan tepi bidang terang yang terlihat tidak beraturan.

Yang dibuang: grid, orb kedua, wash kedua, hairline atas, dan `ring-inset`. Yang tersisa: **satu**
gradien horizontal (`from-primary/10 via-primary/[0.03]`, dark `from-primary/20 via-primary/[0.06]`)
+ orb glow dari tepi kanan yang opasitasnya per mode (`opacity-50` terang, `90` gelap) + bintang.
Satu gradien tidak bisa menghasilkan tumpang tindih berlumpur seperti versi dua-wash. Selanjutnya
twinkle dipercepat atas permintaan HIRO: durasi **1.60–4.00s → 0.64–1.58s** dan delay
**0–5.00s → 0.00–2.46s**; terukur dari `getComputedStyle` tiap bintang (46 elemen). Spread 5 detik
ternyata membuat sebagian bidang diam beberapa detik sehingga terasa beku.

### Ikon login INFRA-BTCAP dianimasikan (2026-10-08)

Glyph `streamline-cyber:network` di login page sekarang **berputar 18s linear infinite** dengan
opacity bernapas 0.7–1.0 dalam 3.5s, dan ditambah **dua cincin yang bergerak keluar lalu masuk
kembali** (scale 1 → 1.6 → 1, durasi 2.8s dan 3.4s dengan delay −1.7s sehingga fasenya bergeser
perlahan). Permintaan HIRO eksplisit: *"lingkaran tengah tetap seperti sekarang"* — jadi badge,
ukuran, border, dan animasi `pulse` box-shadow-nya **tidak disentuh**; cincin adalah child
`absolute` terpisah.

DUA JEBAKAN YANG DIHINDARI DI SINI:
- Rotasi dan sway ditaruh di **dua elemen berbeda** (wrapper vs ikon). Kalau keduanya di satu
  elemen, keduanya butuh `transform` dan yang belakangan menang per-property — salah satu
  animasi diam-diam tidak jalan.
- **Tidak ada scaling pada glyph.** Glyph di-raster ulang pada ukuran antara saat diskalakan dan
  terbaca soft saat bergerak; itu alasan yang sama kenapa app ini tidak pernah men-scale teks.
  Karena itu animasi badge sendiri juga tidak di-scale (HIRO memang minta badge tetap), dan
  cincin dibuat sebagai elemen terpisah.

Bukti runtime: `animationName` wrapper `logo-spin` 18s infinite, transform berubah
`matrix(-0.81248, 0.582989, …)` → `matrix(-0.995126, 0.0986087, …)` = ~144° → ~174° dalam 1.5s,
tepat 360°/18s. Cincin terukur `scale 1.251 → 1.480` (keluar) sementara cincin kedua
`1.461 → 1.426` (masuk) pada saat yang sama — jadi benar-benar keluar-masuk dan fasenya berbeda.
Badge tetap `transform: none`, `animationName: pulse`, kotak **44×44**. Visual di-zoom dari
screenshot: cincin terbaca sebagai halo, tidak memotong wordmark INFRA-BTCAP. `prefers-reduced-motion`
sudah mencakup `.logo-icon`, anaknya, dan `.logo-ring`.

### Brand mark sidebar ikut dianimasikan (2026-10-08)

HIRO: *"implement juga untuk icon infra-btcappada sidebar"*. Jadi `BrandMenu.vue` sekarang memakai
tiga gerakan yang sama: glyph berputar 18s, opacity bernapas 3.5s, dan dua cincin keluar-masuk
(2.8s dan 3.4s delay −1.7s). Badge-nya sendiri **tidak diubah** — tetap `size-8 rounded-lg
bg-primary` dengan glyph putih.

SATU PERBEDAAN YANG DISENGAJA dari versi login: **warna cincin memakai token, bukan hex literal.**
Login page itu permukaan gelap tetap dengan `#00dc82` hardcoded, sedangkan sidebar mengikuti theme
picker — jadi cincinnya `color-mix(in oklab, var(--ui-primary) 75%, transparent)`. Kalau saya
menyalin `rgba(0,220,130,…)` dari login, cincin itu akan jadi satu-satunya bagian mark yang tidak
ikut berubah saat accent diganti ke violet. `border-radius` cincin disamakan dengan badge (8px,
`rounded-lg`), kalau tidak sudutnya jadi dobel.

Terukur di runtime: badge `transform: none` dan kotak **32×32** di (20,16) — tidak berubah. Ikon
`brand-spin` 18s, transform berubah `matrix(-0.385149, -0.922854, …)` → `matrix(0.0235172,
-0.999723, …)` yaitu **−112.7° → −88.65° = +24° dalam 1.2s**, tepat 360°/18s. Cincin 1 turun
`1.5665 → 1.1194` (masuk) sementara cincin 2 naik `1.0941 → 1.5996` (keluar) pada saat yang sama —
kedua arah gerak terbukti, fasenya berlawanan. Kotak cincin di ujung luar 50×50 di (11,7): masih
di dalam header 64px, dan tepi kanannya x=61 sementara teks `INFRA-BTCAP` mulai di x=60 — jadi
hanya bersinggungan 1px pada opacity ~0.05 (praktis tidak terlihat). Di-zoom dari screenshot:
tidak ada clipping, glyph tetap center.

---

## PC Ledger — modul baru (2026-10-08)

HIRO: *"sekarang buat 1 page PC Ledger. saya mau membuat system pengganti excel manual ini. ada
fitur exportnya dan hasil exportnya mengikuti format excel ini
`D:\WORK\PANASONIC\Web\INFRA-CAP\reff\PC_Ledger.xlsx`. design yang bagus dan professional"*.

### Referensi dibaca dulu, bukan ditebak

File referensi itu `IT FORM SG031 Department PC Ledger Form v7`, **satu sheet bernama `Ledger`**,
16 kolom, 26 baris data. Strukturnya di-dump pakai ExcelJS (script di scratch, bukan di repo):
judul di baris 1 (bold 16pt, center, melintasi kolom A–D), catatan "Requests to IT Reps:-" di
**kolom C** baris 3–6 (baris 6 berisi dua baris teks dengan `\n`), "Department:" di **C9**,
header di **baris 10** (bold 12pt, center, fill `theme1`), data mulai **baris 11**, dan **semua
kolom C–P punya fill `FFFFFFCC`** — kuning muda, itulah arti "please fill in yellow columns".
Kolom A adalah spacer selebar 2.44. Lebar kolom asli disalin apa adanya ke export.

### Entity dibuat lewat API, bukan migration

`POST /api/entities` → **`pc_ledger` (id 10), 16 field**, kind `Transaction`. Field order
mengikuti sheet: `nomor` (required+unique, diisi server), staff_name, email, gid, japan_hostname,
computer_model, computer_sn, tanggal (Date), chassis, manufacturer, os_name, os_arch, lokasi,
remark2, remark3, departemen. Script pembuatnya disimpan di `api/create-pc-ledger-entity.py`.

**`departemen` adalah satu-satunya penambahan**, dan alasannya: workbook punya SATU sel
"Department:" di atas tabel, yang tidak mungkin mewakili nilai per baris. Jadi Department hidup
di record (bisa difilter di layar) dan ditulis kembali ke sel itu saat export; kalau baris yang
diexport punya lebih dari satu department, selnya diisi daftar gabungan.

### JEBAKAN YANG KETEMU SAAT RUNTIME (dua-duanya nyata, bukan teori)

1. **`nomor` tidak diisi otomatis.** `POST /api/records` pertama gagal 26 kali dengan
   `{"nomor":"No wajib diisi"}`. `LogbookNumberService.TryNextAsync` hanya melayani slug yang
   terdaftar di `NumberedSlugs` — cctv dan handover saja. Jadi entity baru **tidak** otomatis
   dapat penomoran: tanpa menambah `PC_LEDGER_SLUG` ke set itu, SEMUA create dari UI juga akan
   gagal 400. Pelajaran: menambah entity ber-`nomor` baru selalu butuh satu baris di service ini.
2. **`<SelectItem>` menolak `value: ''`.** Filter department/chassis saya isi opsi
   `{ label: 'All departments', value: '' }` dan **seluruh halaman jadi 500**:
   *"A <SelectItem /> must have a value prop that is not an empty string. This is because the
   Select value can be set to an empty string to clear the selection and show the placeholder."*
   Fix: sentinel `const ALL = '__all__'` untuk nilai "tanpa filter", dipakai di v-model, di items,
   dan di pengecekan filter. Awalan string kosong sebagai penanda "semua" adalah pola yang salah
   di Reka UI.

### Halaman

`web/app/pages/logbook/pcledger.vue`, route **`/logbook/pcledger`**, masuk grup **Log Book** di
sidebar (ikon `i-lucide-monitor`) — juga didaftarkan di command palette (dua tempat, karena
palette sengaja eksplisit).

Isinya: header sheet (judul + "IT FORM SG031 · Department PC Ledger Form v7 · N records"),
toolbar **Search | All departments | All chassis | Excel | Add Record**, tabel 15 kolom with
`table-fixed` + `<colgroup>`, header sticky, **kolom No dan Staff Name di-pin** (`position: sticky`
kiri) supaya identitas baris tetap terlihat saat 15 kolom di-scroll horizontal, klik header untuk
sort (No dan Date dibandingkan numerik/tanggal, bukan string — kalau tidak, 10 akan mendahului 2),
kolom mono untuk email/GID/hostname/S-N, footer "N records | Showing X of N", modal Add/Edit 3 seksi
(Identity / Hardware / System and placement), dan dialog delete yang mengulang identitas baris
(No + Staff Name + Hostname + Location).

**Tidak ada auto-capitalise di halaman ini.** Nilai identitas (`.\\capuser`, `JAPAN\\29384_DTS05`,
`pidbt.dts05@sg.panasonic.com`, `E0B5536/7`) dikopi apa adanya dari spreadsheet, dan app ini punya
aturan berdiri bahwa permukaan identitas/kredensial tidak pernah "dirapikan" otomatis.

### Import data lama

26 baris referensi diimpor lewat `api/seed-pc-ledger-rows.cjs` (ExcelJS → `POST /api/records/10`):
**created=26 failed=0, server total = 26**. Nilai disalin **verbatim**, termasuk yang berantakan dan
memang sudah dijalani departemen (`#NA` pada email, `E0B5536/7`, spasi ganda di `Surface GO  4`,
spasi di ujung `PC Display EVR `). Merapikannya di sini akan membuat data aplikasi berbeda dari
workbook yang masih dipakai departemen — itu satu hal yang tidak boleh dilakukan sebuah import.
Kolom Date di sumber isinya campur: angka serial Excel dan teks `OK` (seseorang mengetik status ke
kolom tanggal). Serial dikonversi; yang bukan angka **dibuang**, tidak dipaksa masuk field Date.

### Verifikasi export (dibandingkan langsung dengan file referensi)

Klik Excel di browser → file benar-benar terunduh (`C:\Users\HIRO\Downloads\PC_Ledger_2026-10-08.xlsx`,
10.027 byte), lalu dibandingkan field-per-field dengan `PC_Ledger.xlsx` memakai ExcelJS:

| yang dibandingkan | hasil |
| --- | --- |
| nama sheet | `Ledger` = `Ledger` |
| judul baris 1 + bold + size | identik (`true`, 16) |
| 4 baris catatan di kolom C | keempatnya identik |
| label "Department:" | identik |
| 15 label header baris 10 | identik, urutannya sama |
| 16 lebar kolom A–P | identik |
| fill kolom B–P pada baris data | `["none", FFFFFFCC × 14]` — sama persis |
| jumlah baris data | 26 = 26 |
| baris pertama & terakhir (spot check) | nilainya identik |

Render halaman juga sudah dikonfirmasi di browser: 26 baris, 15 kolom + Actions, footer
"26 records | Showing 26 of 26", dan "PC Ledger" muncul di sidebar.

### Pindah menu + pilih kolom (2026-10-08)

HIRO: *"menu pc ledger pindahkan keluar menu log book, letakkan diatas menu log book. pada table
bisa buat fitur hide column? jadi user bisa fleksibel mau tampilkan kolom yang diinginkan"*.

**Menu**: PC Ledger keluar dari grup Log Book dan dapat grup sendiri **di atas** Log Book —
urutannya sekarang `Dashboard | PC Ledger | Log Book (CCTV Access, Handover) | Log Audit | User
Management`. Alasan yang dicatat di kode: ini register aset, bukan log kejadian, dan di dalam grup
ia menghilang setiap kali Log Book di-collapse. Grup terpisah juga **tidak menggeser** aturan
`mt-auto` di layout — grup TERAKHIR tetap Log Audit + User Management, jadi keduanya tetap menempel
di atas tombol profile. Command palette ikut disesuaikan urutannya.

**Hide column**: tombol `Columns (15/15)` di toolbar membuka `UPopover` berisi `UCheckbox` per
kolom (lihat dulu `Popover.vue` di node_modules untuk memastikan nama slot-nya `#content`, bukan
menebak). Pilihan disimpan **per browser** di `localStorage` kunci `infra-cap.pcledger.columns`,
alasannya sama dengan cookie tema: preferensi cara membaca tabel bukan state departemen. Aturan:
minimal satu kolom harus tetap tampil (tabel kosong tanpa penjelasan terbaca seperti halaman rusak),
kunci asing dari build lama dibuang saat load, dan nilai localStorage yang rusak tidak boleh
membuat halaman mati — semua di dalam `try/catch` dengan fallback "tampilkan semua".

**Konsekuensi yang HARUS ikut diperbaiki — pin kolom sebelumnya hard-coded.** Pin dulu memakai
`th:nth-child(1)` (left 0) dan `:nth-child(2)` (left 5rem). Begitu No bisa disembunyikan, Staff Name
masuk ke slot pertama dan pin kedua masih di `5rem` → kolom kedua akan didorong 5rem ke dalam tabel
dan meninggalkan lubang kosong. Sekarang pin memakai kelas (`.pl-pin-first` / `.pl-pin-second`) dan
offset pin kedua **dihitung** dari lebar kolom pertama yang sedang tampil (`pinStyle()`), bukan
konstanta. Ditambah satu detail yang gampang terlewat: sel yang di-pin **harus opak**, karena hover
semi-transparan membuat kolom yang lewat di bawahnya tembus terlihat — itu tampilan sticky-column
yang rusak — jadi warna hover-nya di-`color-mix` ke permukaan opak, bukan ditumpuk di atasnya.

Verifikasi di browser (klik pointer sungguhan via CDP, karena `element.click()` sintetis tidak
dipercaya untuk komponen Reka UI): urutan sidebar terbaca `Dashboard, PC Ledger, Log Book, CCTV
Access, Handover, Log Audit, User Management`; tombol `Columns (15/15)` membuka popover berisi 15
checkbox; mematikan "Email Address" → header tinggal 14 kolom, label jadi `Columns (14/15)`, dan
`localStorage` berisi daftar kunci tanpa `email`; **reload halaman → kolom itu tetap tersembunyi
(14 kolom, label 14/15)** = persistensi terbukti; tombol "Show all" mengembalikan 15/15 dan
seluruh header. Pin terukur benar: kolom pertama `position: sticky, left: 0px`, kolom kedua
`position: sticky, left: 80px` (= 5rem, lebar kolom No), kolom ketiga `static`. Ditinggalkan dalam
keadaan semua kolom tampil.


### Font diseragamkan + animasi filter (2026-10-08)

HIRO: *"font nya samakan dong jangn beda-beda. lalu pastikan animasi filter nya buat smooth"*.

**FONT** — grep seluruh web/app menemukan hanya DUA tempat yang memakai typeface berbeda dari
Public Sans:
- `pcledger.vue`: kelas `.pl-mono` (ui-monospace / Cascadia Mono / Consolas) pada kolom Email, GID,
  JAPAN Hostname, dan S/N, plus satu baris di dialog delete. Register jadi terbaca seperti dua
  dokumen berbeda yang disambung.
- `TimePicker.vue`: kelas Tailwind `font-mono` di 6 tempat (stepper jam pada modal CCTV).
  **SENGAJA TIDAK saya ubah** — itu di halaman lain, dan mono di stepper jam bisa jadi memang
  disengaja supaya terbaca seperti jam digital. Tunggu keputusan HIRO dulu.

Yang dikerjakan: `.pl-mono` dihapus bersama flag `mono?: boolean` di tipe `Column` dan keempat
pemakaiannya (dead code dibuang, bukan dibiarkan), dan perataan digitnya dipindah ke
`font-variant-numeric: tabular-nums` di `.pl-table td`. Hasilnya satu typeface, tapi kode tetap
sejajar per kolom. Terukur: `fontFamily` sel = `"Public Sans", ui-sans-serif, system-ui, sans-serif`
dengan `fontVariantNumeric: tabular-nums`, dan aturan CSS yang menyebut Cascadia sudah tidak ada di
stylesheet (`monoRulePresent: false`).

**ANIMASI FILTER** — `tbody` sekarang `<TransitionGroup name="pl-rows">`. Dipilih TransitionGroup dan
bukan animasi CSS berbasis kelas, karena hanya dia yang tahu baris mana yang BENAR-BENAR ditambah dan
dihapus oleh sebuah perubahan filter; animasi berbasis kelas akan jalan ulang di 26 baris pada setiap
ketikan, termasuk 20 baris yang tidak berubah. Fade in 220ms / fade out 130ms, stagger `nth-child`
0–126ms (cap 10 langkah) mengikuti konvensi `.anim-stagger` di main.css, dan `prefers-reduced-motion`
mematikannya total.

**KEPUTUSAN YANG BUKAN KOSMETIK:** animasinya *opacity-only, tanpa translate*. Memberi `transform`
pada `<tr>` membuat ancestor yang ter-transform jadi containing block bagi descendant-nya, dan itu
mematikan `position: sticky` pada dua kolom yang di-pin selama animasi berjalan — jadi gerak
naik-turun sengaja tidak dipakai.

Terukur saat filter dijalankan: pada t=37ms ada **21 baris** dalam keadaan transisi (16 keluar +
5 masuk) dengan opacity minimum masih 1; pada t=95ms opacity minimum **0.902** — barisnya benar-benar
memudar di tengah jalan, bukan lompat; pada t=506ms transisi selesai dan DOM tinggal **5 baris**
(footer `Showing 5 of 26`). Kolom yang di-pin tetap `position: sticky, left: 0px` sepanjang animasi.


### Footer disamakan + transisi baris (2026-10-08)

HIRO: *"status footer samakan stylenya denga cctv access. animasi dan transisi hasil filter masih
kurang halus, terasa tiba-biba, coba enhance lagi"*.

**FOOTER** sekarang memakai kelas yang sama persis dengan register CCTV
(`flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs
text-muted`), dan **hitungan kiri mengikuti FILTER** (`{{ visibleRows.length }} records`) supaya
tidak mungkin berbeda dengan `Showing X of Y` di sebelahnya. CCTV pernah mencetak total mentah di
sana dan terbaca `1 row / Showing 0 of 1` yang tampak seperti bug. Kata bendanya tetap
"record/records" karena itu kosakata halaman ini; yang disamakan stylenya, bukan teksnya.

**TRANSISI** diambil dari cctvacc.vue, bukan dikarang ulang: `TransitionGroup` dengan
`enter-active-class` / `enter-from-class` / `move-class` / `move-active-class` dan nilai 220ms pada
`cubic-bezier(0.22, 1, 0.36, 1)` yang sama. Tambahan di PC Ledger: kelas **leave** (fade 260ms +
padding sel mengempis 240ms) karena CCTV sengaja menghapus baris keluar seketika, dan dengan 26
baris di layar itulah potongan keras yang HIRO rasakan.

Terukur, dengan sampel tiap 35-40ms:
- **FLIP move bekerja**: klik header "Staff Name" membuat **25 baris** transisi/ter-transform
  (seluruh daftar berubah urutan) dan `pinnedLeft` tetap 232 = `scrollerLeft` di SEMUA sampel —
  jadi transform pada `<tr>` **tidak** merusak pin kolom sticky. Ini yang saya khawatirkan dan
  ternyata aman.
- **Leave** memudar bertahap: opacity 1 → 0.980 → 0.853 → 0.530 → 0.252, bukan potong satu frame.
- **Padding sel mengempis**: tinggi baris keluar 43px → 27px, jadi 16px tingginya sudah kembali ke
  baris bawah sambil baris itu masih dihapus.

**KOREKSI ATAS KLAIM SAYA SENDIRI:** saya sempat menambahkan `max-height: 0` pada isi sel supaya
baris keluar mengempis total sampai 0, dan **itu tidak bekerja sama sekali** — diukur, barisnya
tetap 27px saat dihapus. Aturannya sudah saya HAPUS, bukan ditinggal seolah berfungsi.

**SISA MASALAH YANG JUJUR:** lompatan ~477px masih ada satu kali saat 18 baris dihapus serentak
(filter 26 → 8): baris di bawahnya naik dalam satu langkah besar. Ini persis peringatan yang sudah
tertulis di komentar cctvacc.vue. Perbaikan sesungguhnya berarti mengeluarkan baris yang keluar dari
alur tabel (`position: absolute` + spacer), dan itu menukar lompatan dengan tabel yang tidak bisa
mempertahankan lebar kolomnya sendiri. Belum saya kerjakan — dilaporkan ke HIRO lengkap dengan
angkanya, bukan ditutupi.


### Bounce saat baris mendarat (2026-10-08)

HIRO: *"can add bounce effect when column collide after filter?"*.

Kurva FLIP move diganti dari monoton `cubic-bezier(0.22, 1, 0.36, 1)` 220ms menjadi
**`cubic-bezier(0.34, 1.56, 0.64, 1)` 340ms** (ease-out-back klasik) — baris melaju sedikit melewati
garis akhirnya lalu mantul kembali ke tempatnya. Kurva yang sama juga dipakai untuk transform pada
baris yang BARU masuk, jadi baris yang datang mendarat dengan bounce yang sama; opacity-nya tetap di
kurva monoton karena kurva overshoot akan mendorong opacity melewati 1 di tengah jalan (di-clamp,
jadi tidak berbahaya, tapi hanya memperpendek fade tanpa manfaat).

340ms, bukan 220ms seperti register CCTV: pada tinggi baris 43px, overshoot dengan durasi lebih
pendek tidak terasa sebagai bounce, hanya terasa seperti mendarat agak telat.

**Aman meski aturan app ini menuntut kurva monoton** — aturan itu ada karena kurva non-monoton bisa
meninggalkan elemen di luar nilai akhirnya. Transisi CSS SELALU berakhir tepat di targetnya, dan
target di sini `transform: none`, jadi barisnya mendarat persis di garisnya; overshoot hanya ada di
tengah lintasan. Aturan itu juga ditulis untuk TEKS yang di-scale (glyph jadi soft di ukuran antara)
— ini memindahkan baris, bukan men-scale apa pun.

Terukur (klik header Staff Name, sampel tiap 30ms, top baris no. 5): `394 → 359 → 300 → 257 → 241 →
220 → 209 → **207** → 210 → 212 → 218 → 222 → **224 → 224 → ...`. Jadi: melaju 170px, **melewati
garis akhir sejauh 17px** (207 vs final 224), lalu mantul dan berhenti **tepat** di 224 dan tidak
bergerak lagi (`settledLast4: true`, 2 pembalikan arah).

Satu koreksi komentar: blok komentar transisi masih menulis "NO LEAVE ANIMATION, deliberately" dari
revisi sebelumnya, padahal animasi leave sudah ditambahkan. Sudah diperbaiki supaya tidak jadi
komentar yang membohongi pembaca berikutnya.


### Bounce antar kolom saat check/uncheck visible column (2026-10-08)

HIRO: *"maksud saya ketika ceklis visible column di check dan uncheck ada efek bounce antar column"*.

Jadi yang dimaksud bukan bounce baris setelah filter, tapi bounce **antar kolom** saat sebuah kolom
disembunyikan/ditampilkan. Dua mekanisme saya bangun dan UKUR di tabel hidup, dan **tidak satu pun
menghasilkan bounce** — jadi yang saya commit adalah bagian fungsionalnya saja, dengan komentar yang
menyebutkan kegagalannya, bukan animasi yang berpura-pura jalan:

1. **`transition: width` pada `<col>/th/td`.** Terbukti beranimasi di tabel ini (satu sel terukur
   256px → 32px lewat 14 nilai antara) dan ia memang menggeser kolom tetangga dengan halus, karena
   kolom yang menutup menyempit bertahap. Tapi ia **tidak bisa bounce**: kurvanya harus melewati
   lebar nol, dan lebar negatif tidak valid, jadi penutupannya selalu mendarat rata. Lebih buruk
   lagi, mekanisme ini juga **menyerap pergerakan** yang dibutuhkan FLIP: posisi sel berubah 0 pada
   saat FLIP mengukurnya, sehingga transformnya tidak pernah dipasang.
2. **FLIP pada sel** (`onBeforeUpdate` snapshot, `onUpdated` lepas ke kurva overshoot). Terukur:
   hook-nya **tidak pernah memasang transform** (0 sampel berisi transform inline sementara kolomnya
   berpindah 1240 → 1048 dalam satu langkah). Kode ini sudah **DIHAPUS**, tidak dibiarkan. Dugaan
   penyebabnya urutan update — snapshot dan pengukuran butuh `await nextTick()` plus layout paksa di
   antaranya — tapi itu baru dugaan, bukan perbaikan terverifikasi, jadi tidak saya tulis ke file.
   `tableEl` dan `ref="tableEl"` yang jadi tidak terpakai ikut dihapus, dan fade opacity pada sel
   yang disembunyikan juga (kolom sudah nol-lebar dan ter-clip, jadi fade-nya tak terlihat).

**Yang JALAN dan terverifikasi:** setiap kolom sekarang selalu dirender (bukan dihapus), kolom
tersembunyi dikuncupkan oleh `.pl-col-hidden` (lebar 0, padding 0, ter-clip instant), `visibleIndex()`
menjaga agar pasangan kolom yang di-pin tetap kolom VISIBLE pertama dan kedua (terukur: "1" di
left 0px dan ".\capuser" di left 80px setelah GID disembunyikan), dan kolom tersembunyi benar-benar
0px (1 header + 26 sel dapat kelas `pl-col-hidden`). Preferensinya tetap tersimpan per browser.

**STATUS JUJUR: efek bounce antar kolom BELUM ADA.** Langkah berikutnya yang paling masuk akal:
instrumentasi hook-nya lebih dulu (cetak kapan `onBeforeUpdate`/`onUpdated` benar-benar jalan dan
berapa dx yang terukur) sebelum menulis mekanisme ketiga — bukan menebak lagi.


### Bounce antar kolom — SELESAI, lewat instrumentasi dulu (2026-10-08)

HIRO: *"instrumentasi dulu — log kapan kedua hook benar-benar jalan dan berapa dx yang terukur —
baru tulis mekanismenya"*. Urutan itu saya ikuti, dan hasilnya menyelesaikan masalahnya.

**TEMUAN INSTRUMENTASI (yang mengubah segalanya):** logger sementara dipasang di `onBeforeUpdate`
dan `onUpdated`, plus satu `watch(visibleKeys)`. Setelah toggle kolom, log hanya berisi SATU entri:
`{"e":"watch:visibleKeys","count":14}`. **Kedua hook update TIDAK PERNAH JALAN** di komponen ini.
Jadi FLIP pertama bukan salah tulis — ia tidak pernah dipanggil sama sekali, dan itu sebabnya ia
diam-diam tidak melakukan apa pun. Menebak urutan update/`nextTick` (dugaan saya sebelumnya) arahnya
salah; masalahnya triggernya memang tidak pernah menyala.

**MEKANISME YANG DITULIS DARI FAKTA ITU:** FLIP digerakkan `watch(visibleKeys)` dengan
`flush: pre` (default). Dua alasan kenapa ini justru lebih tepat daripada hook:
- watch berjalan di fase pre-flush, jadi DOM masih memegang layout LAMA saat callback mulai — persis
  pengukuran "before" yang dibutuhkan FLIP;
- `await nextTick()` setelahnya membuat layout baru terbaca, lalu sel digeser balik dengan
  `translateX(dx)` dan dilepas ke kurva overshoot yang sama dengan baris
  (`cubic-bezier(0.34, 1.56, 0.64, 1)`, 340ms). Inline style selalu dibersihkan di timeout supaya
  FLIP tidak pernah meninggalkan transform yang salah.

**TERUKUR (klik GID untuk menampilkan kembali, sampel tiap 20ms, kolom JAPAN HOSTNAME):**
`1048 → 1124 → 1155 → 1181 → 1203 → 1220 → 1244 → 1252 → 1256 → **1259** → 1255 → 1252 → 1249 →
1246 → 1243 → **1240 → 1240 → ...` Jadi kolomnya bergerak 192px, **melewati garis akhir sejauh 19px**
(1259 vs final 1240), lalu mantul dan berhenti tepat di 1240 (1 pembalikan arah, 16 nilai berbeda).
17 frame terukur membawa transform aktif (penanda `T`), dan setelah selesai **nol** inline
transform/transition tersisa di baris itu.

Instrumentasi sementara sudah dihapus dari file; `tableEl` + `ref="tableEl"` sekarang benar-benar
dipakai oleh `measureLefts()`, dan `onBeforeUpdate`/`onUpdated` tidak lagi direferensikan.

### Bug: cuma row 1 yang bounce (2026-10-08)

HIRO: *"bro kenapa cuma row 1 yang bounce, saya mau semua row"*. Betul, dan itu bug saya:
`measureLefts()` mengukur lewat `tableEl.value?.querySelector('tbody tr')` — **baris pertama saja** —
jadi hanya sel di baris 1 yang dapat transform; 25 baris lainnya melompat.

Perbaikan: snapshot sekarang mengambil **semua** baris (`tbody tr` → array left per baris) **plus**
baris header (`thead tr` → left tiap `th`), lalu transform diterapkan ke setiap elemen yang memang
harus bergerak. Header ikut diukur bukan karena pelengkap: saat kolom disembunyikan, sel header juga
bergeser, dan kalau hanya body yang dianimasikan maka header sudah duduk di layout baru sementara
body masih berjalan — salah posisi selama 340ms penuh.

TERUKUR setelah perbaikan (klik GID, sampel tiap 20ms, kolom JAPAN HOSTNAME), dan keempat seri ini
IDENTIK:

| target | seri |
| --- | --- |
| header | 1240 → 1200 → 1164 → 1107 → 1085 → 1068 → 1054 → 1044 → 1032 → **1029** → 1030 → 1033 → 1039 → 1042 → 1045 → 1047 → 1048 |
| row 1 | idem |
| row 5 | idem |
| baris terakhir | idem |

Masing-masing: `distinct: 17, reversals: 1, overshoot: 19px`, berhenti tepat di 1048, dan tidak ada
inline transform/transition yang tersisa setelah selesai.

CATATAN PENGUKURAN yang sempat membingungkan: penghitung "elemen yang punya style.transform"
melaporkan 0 walaupun gerakannya jelas ada. Sebabnya FLIP memang mengosongkan `el.style.transform`
di frame berikutnya dan membiarkan **transition** yang menganimasikan dari nilai sebelumnya — jadi
`style.transform` sengaja kosong selama animasi berjalan, dan pengukuran yang benar adalah posisi
(`getBoundingClientRect().left`), bukan ada/tidaknya inline transform.

### Warna kolom No + Staff Name berbeda (2026-10-08)

HIRO: *"pada tabel no dan staff name warna nya kenapa berbeda?"* Ini bug saya, bukan maksud desain.

**Penyebab (terukur):** sel kolom pin dicat `var(--ui-bg-elevated)` = `oklch(0.274 0.006 286.033)`,
sedangkan sel kolom lain `rgba(0,0,0,0)` alias transparan sehingga memperlihatkan permukaan di
belakang tabel = `oklch(0.21 0.006 285.885)` alias `var(--ui-bg)`. Jadi dua kolom pertama duduk satu
tone lebih terang dari 14 kolom lainnya.

Kolum pin memang WAJIB opaque — kalau tidak, kolom yang lewat di bawahnya akan tembus saat scroll
(itulah kenapa `.pl-pin` punya background sejak awal). Yang salah adalah tokennya: tabel ini duduk di
`--ui-bg`, bukan `--ui-bg-elevated`. Saya ambil token yang lebih terang tanpa memeriksa permukaan di
bawah tabelnya.

**Perbaikan:** `.pl-pin` → `var(--ui-bg)`, dan hover pin `color-mix(... var(--ui-bg))` supaya tetap
opaque, sehingga mix-nya setara dengan hover sel biasa yang translucent di atas `--ui-bg`.
Satu penyesuaian penting: `.pl-table th.pl-pin` **di-set ulang ke `--ui-bg-elevated`** — kalau
dibiarkan mewarisi warna body, dua sel header pertama akan jadi gelap dan memotong pita header yang
memang sengaja satu tone di atas body.

**TERUKUR setelah perbaikan:** `bodyPin1` dan `bodyPin2` = `oklch(0.21 0.006 285.885)` = persis
`tokenBg`; `bodyNormal` tetap transparan; `headPin1` dan `headPin3` keduanya
`oklch(0.274 0.006 286.033)` (pita header tetap seragam). Semua berbasis token, jadi light mode ikut
benar tanpa hardcode.

### Tiga penyesuaian tampilan tabel PC Ledger (2026-10-08)

HIRO: *"hilangkan line antara kolom staff name dan email address. lalu buat header tabel text color
sesuai warna accent, sama seperti NO. warna antar row buat selang seling biar memudahkan view user."*

**1. Garis antara Staff Name dan Email Address — DIHAPUS.** Itu adalah `box-shadow: 1px 0 0
var(--ui-border)` pada `.pl-pin-second`, yaitu garis di tepi kanan kolom pin kedua. Rule-nya dihapus
dan kelasnya ikut dikeluarkan dari `pinClass()` supaya tidak meninggalkan CSS/kelas mati. Konsekuensi
yang saya terima sadar: saat register di-scroll ke samping, tidak ada lagi batas visual di ujung
pasangan kolom pin — yang menyembunyikan kolom yang lewat hanya latar opaque-nya. Terukur:
`boxShadow` sel Staff Name = `none`, dan tidak ada garis di screenshot.

**2. Warna teks header = accent, sama seperti NO.** `.pl-table th` dari `var(--ui-text-muted)` →
`var(--ui-primary)`. Terukur: SEMUA `th` (termasuk yang di-pin) = `rgb(0, 220, 130)` = `#00DC82`,
sama persis dengan token accent. Konsekuensinya `.pl-table th.is-sorted` (yang tugasnya cuma
mewarnai kolom terurut dengan accent) jadi tidak punya efek apa-apa lagi → rule itu dan binding kelas
`is-sorted` di markup keduanya dihapus, bukan dibiarkan terlihat bermakna. Penanda kolom terurut
sekarang murni panah ↑/↓ yang memang sudah ada.

**3. Warna baris selang seling.** Ditambahkan `.pl-table tbody tr:nth-child(even) td { background:
color-mix(in oklab, var(--ui-text) 7%, var(--ui-bg)) }` — lift netral (bukan tint warna accent), jadi
di dark mode jadi band lebih terang dan di light mode jadi band lebih abu, mengikuti tema tanpa
warna kedua yang harus dirawat.

Dua hal urutan yang penting dan sengaja didokumentasikan di file:
- Rule zebra dideklarasikan **SEBELUM** rule hover. Keduanya (0,2,0), jadi yang menang adalah urutan
  sumber — kalau terbalik, baris genap tidak akan menyala saat di-hover.
- Rule ini mengenai **semua** `td` termasuk yang di-pin, karena `.pl-pin` hanya (0,1,0). Jadi bandnya
  menerus melewati pasangan kolom pin, tidak berhenti di kolom kedua — kegagalan yang persis sama
  dengan kasus "No dan Staff Name warnanya berbeda" sebelumnya. Rule hover pin tetap (0,3,0) sehingga
  masih menang atas band.

Kekuatan band sempat 5% dan saya naikkan ke 7%: pada 5% terukur L 0.2455 vs dasar 0.21 (ada, tapi
terlalu tipis untuk membantu memindai baris, padahal itu tujuan bandnya). Pada 7% terukur L 0.2597 baik
untuk sel pin maupun sel biasa, dan terlihat jelas di screenshot. Hover pada baris genap tetap
bekerja: sel biasa jadi `oklab(... / 0.06)` translucent, sel pin jadi campuran opaque
`oklab(0.244569 ...)`.

### Samakan style tabel PC Ledger dengan tabel CCTV Access (2026-10-08)

HIRO: *"samakan style tabel dengan style table cctv access"*. Sebelum mengubah apa pun saya UKUR
tabel CCTV yang sedang berjalan (dark + light) dan menjadikannya patokan angka, bukan tebakan.

Angka patokan CCTV (dark) vs PC Ledger: `card` CCTV = `oklch(0.274)` alias `--ui-bg-elevated` radius
12px, PC Ledger `oklch(0.21)` radius 8px; `table-fontSize` 14px vs 13px; `th` 12px / padding 10px 12px /
tracking 0.3px vs 11px / 8px 12px / 0.44px; `td` padding 10px 12px dan border 60% vs 8px 12px border
100%; zebra = `--ui-bg` 20% di atas permukaan elevated (efektif L 0.261) vs campuran teks 7%;
`actionTd` padding 8px 4px align center vs 8px 12px align right; scrollbar punya gaya 12px sendiri vs
default browser.

Yang disamakan (semua terverifikasi dengan alat ukur yang sama):
- kartu tabel → `bg-elevated` + `rounded-xl` (12px)
- `overflow-y: scroll` (track selalu ada, tidak berkedip) + gaya scrollbar CCTV (track 12px, thumb
  `--ui-text-dimmed` pill dengan border 2px transparan). Disalin ke blok scoped halaman ini, BUKAN
  memakai `.logbook-scroll` milik cctvacc, supaya dua halaman tidak saling terikat.
- font tabel 13px → 14px
- `th`: 12px, padding 10px 12px, tracking 0.025em (0.3px)
- `td`: padding 10px 12px, border `border-default/60`
- zebra: formula CCTV (`--ui-bg` 20% di atas elevated) — terukur `oklab(0.2612)`, sama dengan nilai
  efektif CCTV
- hover: `primary` 5% (dari 6%)
- Actions: rata tengah, padding 8px 4px, dan pembungkus `inline-flex gap-1` DIHAPUS supaya tombolnya
  seperti di CCTV
- header band: campuran opaque `color-mix(bg 60%, elevated)` — terukur `oklab(0.2356)`, PERSIS warna
  yang dihasilkan header CCTV (`bg-default/60 backdrop-blur-sm` di atas elevated). Opaque dan bukan
  translucent karena di sini dua kolom pertama di-pin: header tembus pandang akan memperlihatkan kolom
  yang lewat di bawahnya.

Konsekuensi yang harus ikut berubah dan sudah dilakukan: karena kartu pindah ke permukaan elevated,
`.pl-pin` WAJIB ikut pindah ke `var(--ui-bg-elevated)` — kalau tidak, kolom No dan Staff Name kembali
berbeda warna (bug yang sama seperti sebelumnya). Terukur sekarang: sel pin baris ganjil =
`oklch(0.274)` = permukaan kartu, dan baris genap = `oklab(0.2612)` sama dengan sel biasa, jadi band
zebra tetap menerus melewati kolom pin.

TIGA PERBEDAAN YANG SENGAJA DIBIARKAN (dan alasannya):
1. **Warna teks header = accent**, bukan `text-muted` seperti CCTV. Ini permintaan HIRO satu pesan
   sebelumnya ("buat header tabel text color sesuai warna accent, sama seperti NO"). Kalau yang
   diinginkan benar-benar identik dengan CCTV, cukup bilang — satu baris untuk dikembalikan.
2. **Model lebar kolom**: PC Ledger `table-layout: fixed` + `min-width: max-content` dengan lebar rem,
   CCTV `min-w-[1180px]` dengan persentase. Tidak diubah karena fitur hide-column dan animasi bounce
   bergantung pada kolom yang benar-benar bergeser.
3. **Loading/empty state**: PC Ledger pakai div `p-12` biasa, CCTV pakai `<tbody>` khusus dengan
   `py-10`/`py-12` plus tombol Clear filters. Ini bukan chrome tabel, jadi tidak disentuh.

Catatan jujur soal zebra: formula CCTV itu di LIGHT mode hampir tidak terlihat (`--ui-bg` = putih di
atas permukaan elevated yang juga hampir putih; terukur 0.9736 vs 0.967) — itu sifat band CCTV sendiri,
bukan efek port ini, dan sudah didokumentasikan di komentar CSS-nya.

### Footer PC Ledger disamakan dengan CCTV (2026-10-08)

HIRO: *"footer pc ladger berbeda, coba samakan"*. Class string footernya SUDAH sama sejak sebelumnya,
jadi bedanya ada di WADAHNYA — dan itu terbukti begitu diukur, bukan diperkirakan.

**Penyebab terukur:** footer PC Ledger berada di dalam slot `#footer` UCard, dan UCard membungkus
slot itu dengan `<div class="p-4 sm:px-6">` (padding 16px 24px). Akibatnya:
- bar footer jadi **69px** tinggi, bukan 37px (ada pita transparan 16px di atas dan bawahnya);
- lebarnya **1616px** sementara kartunya 1664px — `bg-default/30` tidak bisa mengecat keluar dari
  wrapper, jadi pita abu-abu itu tidak menyentuh tepi kartu (24px menganga di kiri-kanan);
- UCard root memakai `ring` + `divide-y divide-default`, jadi ada garis pemisah ekstra di atas area
  footer yang tidak ada di CCTV.

Di CCTV, bar footer adalah **anak langsung** kartu (`div.overflow-hidden.rounded-xl.border.bg-elevated`):
37px, lebar penuh 1662px.

**Perbaikan:** `<UCard>` diganti `<div class="anim-fade-up overflow-hidden rounded-xl border
border-default bg-elevated">` — persis class string kartu CCTV — dan footernya dikeluarkan dari slot
`#footer` menjadi SIBLING dari area scroll di dalam kartu itu (dengan `v-if="!loading && rows.length"`
supaya tetap hilang saat loading/kosong). Tidak ada lagi wrapper berpadding, tidak ada `divide-y`, dan
`border-t` barnya mendarat di tepi kartu.

**TERUKUR setelah perbaikan (dibandingkan angka CCTV):** tinggi bar 37px (=CCTV 37), lebar 1662px
(=CCTV 1662), padding `10px 16px`, bg `oklab(0.21 ... / 0.3)`, font 12px, warna `oklch(0.705 0.015
286.067)`, border-top `1px solid oklch(0.274 0.006 286.033)` — semuanya identik; parent = `div`
kartu dengan class string yang sama, tinggi kartu 795px (=CCTV 795 = scroll 756 + bar 37 + border 2),
`barIsDirectChildOfCard: true`, `widthMatchesCard: true`, 26 baris tetap utuh.

### Transisi baris saat filter chassis diperbaiki (2026-10-08)

HIRO: *"pada saat filter by chasis transisi row kurang smooth, tolong perbaiki"*. Diperbaiki, dan
yang menemukan penyebabnya adalah instrumentasi, bukan dugaan.

**GEJALA TERUKUR SEBELUM PERBAIKAN (filter chassis Desktop, 26 → 16 baris):** baris yang tersisa
berpindah **-387px dalam SATU frame**. Nol frame membawa transform. Tinggi tbody turun dalam 4
langkah dengan lompatan terbesar -250px dalam satu frame. Jadi memang lompat, bukan meluncur.

**TIGA MEKANISME DIUJI, DUA DIHAPUS:**
1. **Transisi leave (fade + kolaps padding)** — sudah ada, dan justru inilah penyebabnya: selama
   baris yang keluar masih memegang ruangnya, posisi baris yang tersisa tidak berubah saat "move"
   diukur, jadi tidak ada transform yang pernah dipasang; saat leave selesai, sisa tingginya
   dilepas sekaligus (terukur ~250px dalam satu frame). Varian kedua (kolaps penuh termasuk
   `font-size: 0`) juga gagal: teksnya kolaps instan dan tombol Actions memegang tinggi baris di
   ~25px. **Kedua rule `.row-leave-*` DIHAPUS.**
2. **`move-class` milik Vue TransitionGroup** — dengan leave ada: 0 frame transform. Setelah leave
   dibuang: jalan di satu run, tidak di run lain. Tidak dapat diandalkan. **`move-class` /
   `move-active-class` DIHAPUS** beserta rule `.row-move` / `.row-move-active`.
3. **FLIP sendiri (dipakai)** — pola yang sama dengan animasi kolom, dan disempurnakan dua kali
   setelah diukur.

**DUA BUG SAYA SENDIRI YANG KETAHUAN LEWAT INSTRUMENTASI:**
- Watcher `flush: 'pre'` menerima **nilai BARU** sebagai argumen pertama, jadi "before" yang saya
  simpan justru urutan baru → kedua list identik, dy = 0, tidak ada yang bergerak. Perlu `oldV`.
- Pin dengan delta tetap diterapkan saat layout lama masih terpasang → baris didorong satu delta
  penuh dari layout basi dan sempat "pop" (terukur: baris di 918 melompat ke 1004 satu frame, baru
  meluncur ke 826). Penyebab lain: pada jalur filter, patch DOM Vue mendarat **lebih lambat** dari
  hook mana pun — dengan logger sementara, setelah filter row count masih 26 satu tick kemudian,
  satu frame kemudian, dan bahkan dari hook `flush: 'post'`.

**PERBAIKAN AKHIR:** pin **self-correcting per frame pakai `offsetTop`** (posisi layout, tidak
terpengaruh transform). `old offset - offset sekarang` = transform yang menahan baris di posisi
visual lamanya, benar baik patch sudah mendarat atau belum. Pin diterapkan tiap frame sampai layout
benar-benar bergerak dan bertahan satu frame lagi, baru dilepas ke kurva overshoot yang sama dengan
kolom (340ms). `data-id` ditambahkan ke setiap `<tr>` supaya baris bisa diidentifikasi lintas update;
baris baru/terhapus dilewati (yang baru milik animasi enter). Offset lama diambil di watcher pre-flush.

**TERUKUR SETELAH PERBAIKAN:**
- FILTER Desktop (26 → 16): 13 frame membawa transform; baris 200: `918 → 900 → 884 → 870 → 858 →
  849 → 841 → 835 → 830 → 827 → 825 → 824` = **11 frame gerak beruntun, step terbesar hanya -18px**,
  mulai dari posisi LAMA (tanpa pop) dan berhenti mulus. Sebelumnya: 1 frame, -86px.
- FILTER Notebook (16 → 2): 15 frame transform.
- SORT (25 baris bertukar posisi): 22 frame transform, 22 frame gerak per baris — jalur sort tetap
  jalan dengan mekanisme yang sama (sebelumnya pun jalan, sekarang lewat kode yang sama, deterministik).
- Tidak ada error di konsol; 26 baris utuh; tidak ada transform tertinggal setelah animasi.

### Scrollbar tabel berkedip saat filter chassis (2026-10-08)

HIRO: *"ketika saya apply filter chasis scrollbar pada tabel berkedip-kedip/flickering"*.

**PENYEBAB TERUKUR.** Konfigurasi `.pl-scroll` sudah benar sejak awal (`overflow-y: scroll`,
`overflow-x: auto`, `scrollbar-gutter: stable`, `max-height: 70vh`) dan kedua scrollbar memang selalu
ada (terukur: `clientWidth` 1650 = 1662 - 12 untuk bar vertikal, `clientHeight` 744 = 756 - 12 untuk
bar horizontal). Yang salah adalah **transform pada baris**: baris yang di-pin FLIP tetap dihitung
sebagai *scrollable overflow* milik container, jadi saat baris ditahan di posisi bawahnya, tinggi
area scroll ikut membengkak padahal konten aslinya baru saja MENJADI LEBIH PENDEK. Terukur saat filter
chassis: `scrollHeight` berjalan **813 → 795 → 779 → 765 → 753 → 744 → 736 → 730 → 727** dalam 10
frame sementara `clientHeight` 727 — artinya garis batas overflow bergerak terus di bawah scrollbar,
dan thumb-nya di-repaint tiap frame. Itu kedipannya.

**PERBAIKAN.** Tinggi area scroll **dibekukan selama animasi**: sebelum perubahan diambil
`scrollHeight` container (di watcher pre-flush), lalu tabel diberi `min-height` sebesar nilai itu
selama baris meluncur, dan dilepas setelah baris mendarat (`ROW_ANIM_MS + 120`). Karena baris yang
di-pin tidak pernah melebihi tinggi lama, `scrollHeight` jadi konstan sepanjang animasi; thumb
berubah tepat sekali setelah gerakan selesai, bukan berkedip.

**TERUKUR SETELAH PERBAIKAN (filter Desktop, 26 → 16):** `vOverflowToggles: 0` (sebelumnya 1),
`scrollHeightChanges: 0:1157, 61:1157px (min-height dipasang)` — tinggi **konstan 1157** sepanjang
animasi, `clientHeight` tidak berubah (744), `scrollWidth`/`clientWidth` tidak berubah.

**SISA YANG JUJUR BELUM BERES:** pada filter paling ekstrem (Notebook, 16 → 2) container-nya sendiri
menyusut 727 → 125 px karena kartu register menjadi pendek — itu perubahan layout struktural
(tinggi tabel mengikuti jumlah baris), bukan efek transform, dan pada jalur ini `min-height` beku
tidak terpasang (0 frame), jadi status overflow masih beberapa kali berubah selama transisi.
Perlu ditelusuri kenapa `movers` kosong di jalur itu.

### Tinggi area tabel dibuat TETAP (2026-10-08, lanjutan flicker scrollbar)

HIRO: *"filter chasis masih menyebabkan flicker scrollbar. mungkin bisa buat tinggi table nya tetap
seperti original, walau pun record banyak/dikit tidak berubah"*. Instruksinya tepat, dan itu memang
sumber kedua flicker yang belum tertutup.

**PERUBAHAN 1 — `.pl-scroll` dari `max-height: 70vh` menjadi `height: 70vh` (tinggi TETAP).**
Dengan `max-height`, kotak tabel tumbuh/menyusut mengikuti jumlah baris, jadi setiap filter
menggerakkan track dan thumb-nya sendiri — dan pada filter ekstrem (16 baris → 2) kotaknya
menciut 727px → 125px di tengah transisi. Tinggi tetap menghapus seluruh kelas masalah itu, dan
footer di bawahnya juga tidak bisa bergeser lagi. Konsekuensi yang diterima sadar: register yang
tersaring menyisakan area kosong tinggi di bawah baris terakhir.

**PERUBAHAN 2 — pembekuan tinggi scroll diterapkan untuk SETIAP perubahan daftar baris.** Sebelumnya
pembekuan dilakukan setelah pengecekan `movers`, jadi filter yang baris tersisanya tidak berpindah
(Notebook, 16 → 2) keluar lebih awal dan tingginya tidak pernah dibekukan — itulah celah yang masih
berkedip. Sekarang pembekuan dipasang lebih dulu, dan kalau tidak ada yang perlu dianimasikan
tingginya dikembalikan lewat timeout pendek (60ms).

**TERUKUR SETELAH PERUBAHAN (1920×1080):**

| filter | baris | tinggi container | clientHeight | vOverflow toggles |
| --- | --- | --- | --- | --- |
| awal | 26 | 756 | 744 | — |
| Desktop | 16 | **756** | 744 | **0** |
| Notebook | 2 | **756** | 744 | 2 |
| Reset | 26 | **756** | 744 | 1 |

`containerHeightChanges` = `0:756` di ketiga kasus → tinggi kotak tabel **tidak berubah sama sekali**
dari 26 baris ke 2 baris, dan `clientHeight` tetap 744 (track scrollbar tingginya konstan). Pada
filter Desktop: **0 toggle overflow** dan `scrollHeight` beku di 1157 sepanjang animasi.

**SISA 2 TOGGLE PADA FILTER NOTEBOOK, penyebabnya teridentifikasi bukan lagi soal tinggi:** DOM
sempat memuat **18 baris** selama 2 frame (terukur `rowCount 16 → 18 → 2`) padahal filter hanya
menyisakan 2 — baris lama dan baris baru sempat hidup bersamaan, kemungkinan dari animasi enter
TransitionGroup. Selama 2 frame itu konten melebihi container sehingga thumb muncul sekejap
(≈33ms) lalu hilang. Pembekuan tinggi tidak bisa menutupnya karena pembekuan hanya menaikkan tinggi
minimum, tidak bisa membatasi konten yang benar-benar lebih banyak. Menghilangkannya berarti melepas
animasi enter/TransitionGroup — belum saya lakukan tanpa persetujuan.

### Transisi baris dibuat lebih halus saat filter chassis (2026-10-08)

HIRO: *"ok sekarang saya mau transisi row nya lebih smooth ketika filter chasis"*.

Dua penghambat kehalusan ditemukan dari hasil ukur sebelumnya, dan keduanya diperbaiki:

**1. Ada stall ~1 frame di awal setiap glide.** Pin lama mensyaratkan layout sudah bergerak **dan**
masih bergerak pada frame berikutnya sebelum melepas animasi — jadi baris berdiri diam 16ms lebih
dulu, lalu mulai bergerak. Itu terbaca sebagai "hitch" tepat saat filter ditekan. Sekarang release
dilepas pada **frame pertama** layout terlihat bergerak (`if (movedNow || ++frames > 8)`), plafon 8
frame tetap dipertahankan sebagai jaring pengaman.

**2. Durasi rata 340ms membuat perjalanan jauh terasa terburu-buru.** Sekarang durasi mengikuti jarak
lewat `rowAnimMs(dy) = clamp(340 + (|dy| - 60) / 3, 340, 560)` ms, sehingga baris yang menempuh
ratusan piksel dapat waktu lebih banyak, dan baris yang cuma bergeser sedikit tetap gesit. Kurvanya
juga dilembutkan dari `cubic-bezier(0.34, 1.56, 0.64, 1)` (bounce kolom) menjadi
`cubic-bezier(0.22, 1.06, 0.36, 1)` — pada baris 43px, overshoot yang kuat terbaca sebagai goyangan
di ujung glide, bukan pendaratan. Timeout pembersihan ikut memakai plafon `ROW_ANIM_MAX_MS`.

**TERUKUR SETELAH PERBAIKAN:**
- FILTER DESKTOP (26 → 16), baris 197 menempuh 349px: `789 → 850 → 906 → 954 → 994 → 1027 → 1054 →
  1074 → 1091 → 1103 → 1113 → 1120 → 1125 → 1129 → 1132 → 1135 → 1136 → 1137 → 1138 → 1139 → 1138`
  = **20 frame gerak beruntun** (sebelumnya 11), deselerasi rata tanpa stall, mendarat dengan
  overshoot 1px (sebelumnya 2px). Baris 196 dan 201: 18 frame.
- SORT: 20-22 frame gerak per baris (sebelumnya 22 dengan kurva lebih keras).
- **Tidak ada regresi scrollbar**: `overflowToggles: 0` pada filter maupun sort, `clientHeight`
  tetap 744 (tidak berubah).

### Baris memanjang sesaat saat filter chassis (2026-10-08)

HIRO: *"ketika saya filter chasis desktop, row melebar sebentar lalu kembali normal, ini jelek
tolong perbaiki"*. Penyebabnya adalah perbaikan flicker saya sendiri di langkah sebelumnya.

**PENYEBAB.** Pembekuan tinggi scroll saya pasang sebagai `min-height` pada `<table>`. Pada tabel,
kelebihan tinggi **dibagikan ke baris-barisnya**, jadi selama animasi setiap baris ikut memanjang
(mencari tinggi ekstra) dan kembali normal begitu `min-height`-nya dilepas — persis "melebar sebentar
lalu kembali normal" yang dilihat HIRO.

**PERBAIKAN.** Pembekuan dipindah ke sebuah **wrapper `<div ref="tableWrapEl">`** yang membungkus
tabel di dalam `.pl-scroll`. Wrapper block menerima tinggi yang sama tanpa menyentuh baris sama
sekali. `tableEl` tetap dipakai untuk query baris; hanya target `min-height` yang berubah.

**TERUKUR SETELAH PERBAIKAN (filter Desktop, 26 → 16):**
- `rowHeightMin` = `0:43` dan `rowHeightMax` = `0:43` → **setiap baris tetap 43px sepanjang transisi**,
  tidak ada satu frame pun yang memanjang.
- `rowWidthMax` = `0:4304` (lebar baris tidak berubah), `scrollHeight` = `0:1157` konstan,
  `clientHeight` = `0:744` konstan, `overflowToggles: 0` → pembekuan tetap bekerja, tidak ada regresi
  flicker scrollbar.
- Wrapper menerima `min-height: 1157px` di frame 54 (`wrapperMinHeight`), tabel tidak lagi.

### Filter PC Ledger: Department dihapus, Location ditambahkan (2026-10-08)

HIRO: *"delete filter all departement. tambah filter by location. posisi allchasis di kiri, location
di kanan"*.

**DEPARTMENT DIHAPUS** karena memang tidak bisa memfilter apa pun: kolom `departemen` di data hanya
berisi SATU nilai ("Capacitor") pada seluruh 26 baris, jadi pilihannya cuma "All departments" +
"Capacitor". Yang dihapus: USelect-nya, state `filterDept`, baris penyaringan di `visibleRows`, dan
acuannya di nama file Excel. **`computed departments` SENGAJA DIBIARKAN** karena masih dipakai
`blankForm()` untuk mengisi default field Department di form tambah record — sudah saya beri komentar
supaya tidak dikira sisa.

**LOCATION DITAMBAHKAN** memakai field `lokasi` (label "Location", 8 nilai nyata: Office, PC Display
EVR, Pc Machine Final inspection #018/#019/#020/#035, Rack PC Ghatering - Server Room, X-DTS Project).
State `filterLocation`, computed `locations` (dibangun sama persis seperti `chassisTypes`), baris
penyaringan di `visibleRows`, dan USelect-nya. Ikut masuk ke acuan nama file Excel.

**URUTAN TOOLBAR** sekarang: Search | **All chassis** | **All locations** | Columns | Excel | Add
Record — chassis di kiri, location di kanan sesuai permintaan.

**TERUKUR DI BROWSER (1920×1080):**
- Posisi terukur dari koordinat X: `All chassis` di 1076, `All locations` di 1244 → chassis kiri,
  location kanan.
- `hasDepartments: false` → kontrol Department benar-benar hilang dari DOM/halaman.
- Opsi chassis: All chassis / Desktop / Notebook / Tablet. Pilih Desktop → 16 baris.
- Opsi location: All locations + 8 lokasi nyata. Dengan chassis=Desktop lalu lokasi=Office → 2 baris,
  footer "Showing 2 of 26" — kedua filter bergabung dengan benar.

### Lebar kolom PC Ledger dibuat responsif (2026-10-08)

HIRO: *"buat lebar kolomnya responsive? tujuanya agar lebih ramping dan tidak terlalu melebar"*.

**PENYEBAB LEBAR.** Setiap kolom punya lebar rem eksplisit yang totalnya 269rem = **4304px** di dalam
viewport 1650px, dan `.pl-table` memakai `min-width: max-content` sehingga tabel dipatok selebar
4304px. Hasilnya register 2,6 layar dan tiap kolom selebar nilai terpanjangnya.

**PERUBAHAN:**
1. `COLUMNS[].w` sekarang **persentase**, bukan rem — totalnya 95% + 5% untuk kolom Actions.
   Bagiannya dipilih manual, bukan hasil skala mekanis nilai rem lama (konversi proporsional akan
   memberi kolom No 1,8% alias 30px dan kolom Date 4%).
2. Colgroup: `c.w + '%'` (kolom tersembunyi `0%`), kolom Actions `5%`.
3. `.pl-table` `min-width: max-content` → **`min-width: 1200px`** sebagai lantai untuk layar sempit
   (model yang sama dengan tabel CCTV: full width + min-width + kolom persentase).
4. **Offset kolom pin sekarang DIUKUR, bukan hard-code** (`pinOffset` + `measurePinOffset()`), karena
   lebar kolom pertama kini bergantung viewport dan berubah saat kolom disembunyikan. Pengukuran
   melewati sel selebar 0 px supaya kolom yang disembunyikan tidak dianggap kolom pertama.
   Dipanggil saat mount, saat resize, saat `rows` berubah (register dimuat asinkron), dan setelah
   animasi show/hide kolom selesai.
5. `white-space: nowrap` pada `th` **dihapus**: dengan kolom persentase, judul panjang
   ("Computer Manufacturer", "JAPAN Hostname") tidak lagi muat dan akan menimpa kolom sebelah;
   sekarang membungkus seperti header register CCTV.

**TERUKUR SETELAH PERUBAHAN (1920×1080):** `tableWidth` 1650 = `scrollWidth` 1650 = `clientWidth`
1650, **`hOverflow: false`** (sebelumnya 4304 vs 1650). Lebar kolom: No 50, Staff 132, Email 165,
GID 83, Hostname 132, Model 116, S/N 116, Date 66, Chassis 83, Manufacturer 99, OS Name 116,
OS Arch 83, Location 149, Remark2 116, Remark3 66, Actions 83 — jumlah 1650. Pin tepat:
`staffStickyLeft: 50px` = lebar kolom No (50), dan posisi `th`/`td` kolom Staff Name sama-sama 283
(= tepi scroller 233 + 50). Tinggi baris header 57px (judul membungkus, tidak menimpa). 26 baris utuh.

**KESALAHAN SAYA YANG SEMPAT MEMBUAT HALAMAN 500:** `watch(rows, ...)` saya pasang sebelum `rows`
dideklarasikan, sehingga Vue menerima sumber undefined dan halaman gagal render ("Cannot read
properties of undefined"). Watcher itu sudah dipindah ke setelah `rows` dideklarasikan; tercatat juga
di komentarnya supaya tidak terulang.

### PC Ledger: kilatan abu-abu di header + tinggi header (2026-10-08)

HIRO: *"ada bug css pada tampilan header dimana ketika filter kolom di tick/untick terlihat ada
warna abu-abu terang. ohya table header juga terlalu besar height nya tolong sesuaikan"*.

**1. KILATAN ABU-ABU TERANG.** Sebabnya: warna pita header hanya dipasang pada elemen `th`.
Saat kolom di-tick/untick, sel header bergerak horizontal (FLIP kolom), dan selama beberapa frame
muncul CELAH antar sel - yang tembus adalah permukaan kartu (`--ui-bg-elevated`, abu-abu lebih
terang), bukan warna header. Perbaikan: `.pl-table thead` dibuat `position: sticky; top: 0;
z-index: 10` **dan** diberi warna pita yang sama dengan `th`. Thead membentang seluruh pita, ikut
lengket, melukis di bawah sel, jadi celah apa pun terisi warna yang SAMA. Ini juga struktur yang
dipakai register CCTV (`sticky top-0` pada thead-nya).
Terukur: `getComputedStyle(thead).backgroundColor` === `getComputedStyle(th).backgroundColor` ===
oklab(0.2356 ...); screenshot pita header yang diambil di tengah animasi toggle memperlihatkan pita
yang rata (kolom GID yang runtuh tidak lagi membocorkan warna terang).

**2. TINGGI HEADER.** Penyebabnya label panjang membungkus dua baris: 2 x 18px + 20px padding + 1px
border = **57px**. Perbaikan:
- `Column.short` (label pendek KHUSUS header tabel). `label` tetap utuh dan masih dipakai ekspor
  Excel serta popover pilihan kolom, jadi header Excel tetap sama dengan form IT FORM SG031.
  Singkatan: Email, JAPAN Host, Model, S/N, Chassis, Vendor, O/S Name, O/S Arch.
- Padding vertikal `th` 10px -> 8px (horizontal tetap 12px supaya tepi kiri teks sejajar dengan sel).
- Bagian kolom sedikit digeser agar semua label muat satu baris (chassis 6%, os_arch 6%; email 9%,
  staff_name 7%).
Hasil: semua sel header **35px**, satu baris, tanpa pembungkusan (sebelumnya 57px).

**DIVERIFIKASI:** lebar tabel tetap 1650 = scrollWidth = clientWidth; 26 baris; header tetap lengket
saat kontainer di-scroll (`theadTopVsScrollport: 0` pada scrollTop 300).

### Tinggi header PC Ledger dibuat tetap 35px di semua lebar viewport (2026-10-10)

HIRO: *"tolong adjust height nya header table ini"* + path DOM
`#dashboard-panel-... > div.anim-fade-up...bg-elevated > div.pl-scroll > div > table > thead`.

**SEBABNYA TINGGINYA TIDAK TETAP.** Diukur dulu (instrumentasi sebelum menulis mekanisme):
`thead` = **35px di 1920** tetapi **53px di 1600 / 1440 / 1280** — label header membungkus jadi dua
baris begitu kolom persentase menyempit (2 x 18px line-height + 16px padding + 1px border = 53px).
Jadi tinggi header berubah mengikuti lebar jendela, dan di jendela yang tidak penuh (atau browser
dengan zoom di atas 100%) headernya memang tampak besar. Perbaikan 57 -> 35 di sesi sebelumnya hanya
terasa di 1920 saja.

**PERBAIKAN.** Header dipatok SELALU satu baris:
- `th` : `white-space: nowrap; overflow: hidden` (tidak lagi membungkus).
- Rule baru `.pl-th-label` memegang ellipsis-nya sendiri (`overflow: hidden; text-overflow: ellipsis`),
  dan label itu dibungkus `<span class="pl-th-label">`. Penting: label berada di dalam flex row bersama
  ikon sortir, jadi kalau ellipsis dipasang di `th`, yang terpotong adalah ikon sortirnya, bukan teks.
  `min-w-0` pada span pembungkus agar label boleh menyusut.
- `<span ... :title="c.label">` pada label header: nama penuh tetap muncul saat hover, dan tetap ada di
  popover pilihan kolom serta ekspor Excel (keduanya memakai `label`, bukan `short`).

**TERUKUR SESUDAH (instrumentasi, 4 lebar):** `thead` **35px** di 1920 / 1600 / 1440 / 1280, satu-satunya
tinggi sel yang terukur di tiap lebar; **nol label terpotong** di keempat lebar itu (ellipsis hanya jaring
pengaman untuk lebar yang lebih sempit lagi); ikon sortir tetap tampil ("NO ↑" terlihat di screenshot);
26 baris; di 1440/1280 tabel mencapai `min-width: 1200px` sehingga muncul scroll horizontal yang memang
disengaja, bukan kolom yang dipaksa sempit.

### PC Ledger: default kolom + rename Remark (2026-10-10)

HIRO: *"set default filter table column adalah no,staff name,email,gid,japanhost,model,SN,chasis,
location,remark2,remark3. setelah itu rename remark2 jadi Remark1, dan Remark3 jadi Remark2"*.

**1. DEFAULT KOLOM = 11 dari 15.** Tambah `DEFAULT_VISIBLE` (nomor, staff_name, email, gid,
japan_hostname, computer_model, computer_sn, chassis, lokasi, remark2, remark3) dan `visibleKeys`
diinisialisasi dari situ, bukan lagi semua kolom. Yang tetap tersembunyi: Date, Vendor (manufacturer),
O/S Name, O/S Arch - tinggal satu klik di picker kolom.

**PENTING - KUNCI STORAGE DINAJIKKAN:** pilihan kolom disimpan per-browser di localStorage
(`infra-cap.pcledger.columns`). Setiap browser yang pernah membuka halaman ini sudah punya pilihan
15 kolom tersimpan, yang akan MENIMPA default baru tanpa terlihat - HIRO akan melihat "tidak ada
perubahan". Karena itu kuncinya jadi `infra-cap.pcledger.columns.v2`; nilai lama tidak pernah dibaca lagi.

**2. RENAME Remark.** Label (yang tampil) berubah, KEY tetap `remark2`/`remark3` karena itu nama field
di database; mengubah key = rename field di EAV store, perubahan yang jauh lebih besar.
- `COLUMNS`: remark2 -> label **Remark1**, remark3 -> label **Remark2** (header tabel + ekspor Excel).
- Peta label form (`os_arch`/`lokasi`/`remark2`/`remark3`/`departemen`) di file yang sama ikut diubah,
  supaya form Add/Edit tidak menunjukkan nama lama.
- Label field di DB ikut diubah lewat `PUT /api/fields/{id}`: remark2 (id 66) 'Remark2' -> 'Remark1',
  remark3 (id 67) 'Remark3' -> 'Remark2', dan dibaca ulang untuk konfirmasi. Halaman designer
  (/entities) jadi tidak lagi menampilkan kata lama.
- `api/create-pc-ledger-entity.py` juga diedit agar seed ulang di masa depan memakai nama baru.
Catatan: entity id pc_ledger sekarang **10** (bukan 7 - 7 adalah cctv_log_book; id bergeser setelah
entity demo dihapus dan dibuat ulang).

**TERUKUR SESUDAH (1920x1080):** label tombol picker "Columns (11/15)"; header tabel =
NO, STAFF NAME, EMAIL, GID, JAPAN HOST, MODEL, S/N, **REMARK1**, **REMARK2**, LOCATION (+ACTIONS),
4 kolom lain runtuh ke 0px; tinggi header tetap 35px satu baris; lebar tabel tetap 1650 = scrollWidth =
clientWidth (kolom yang tersisa menyerap bagian kolom tersembunyi: NO 64px, STAFF 150px, dst - jumlah
tetap 1650 sehingga tidak ada celah); 26 baris utuh.

### Modal PC Ledger disamakan background-nya dengan modal lain (2026-10-10)

HIRO: *"style background modal new pc record samakan dengan yang lain, begitu juga modal delete"*.

**SEBABNYA.** Modal New PC record dan Delete record tidak punya lapisan background yang dipakai
modal CCTV Access dan Users. Di dua halaman itu: modal form memakai kelas modal + glow aksen di sudut
kanan atas (::after pada elemen content), dan modal delete memakai wash merah (div terpisah) + band
footer di permukaan elevated. Di PC Ledger keduanya polos: `:ui="{ content: 'max-w-4xl' }"` dan
`'max-w-md'` tanpa kelas/lapisan apa pun.

**YANG DIUBAH (resepnya disalin apa adanya dari users.vue, yang memang jadi referensi):**
1. Modal form: `content: 'max-w-4xl pl-record-modal'`; wrapper dalamnya diberi `relative z-10` supaya
   seluruh dialog berada DI ATAS glow (satu kelas, karena modal ini memakai slot #content dengan
   header/body/footer buatan sendiri - beda dengan CCTV/Users yang memakai slot header/body vendor).
2. Blok `<style>` NON-SCOPED baru di kaki file dengan `.pl-record-modal[data-slot='content']::after`:
   lingkaran `bg-primary` 250x250 di top -110px / right -90px, `blur(60px)`, opacity 0.14,
   `pointer-events: none`, `z-index: 0`; plus override mode terang `:root:not(.dark) ... { opacity: 0.09 }`.
   Non-scoped itu wajib: UModal meneleportasi konten ke <body>, jadi rule scoped (yang dikompilasi
   jadi .pl-record-modal[data-v-xxx]) tidak akan pernah cocok.
3. Modal delete: struktur disamakan dengan modal delete Users/CCTV - wrapper `overflow-hidden rounded-xl`,
   wash merah `<div aria-hidden class="... -right-16 -top-24 size-48 rounded-full bg-error/20 blur-3xl">`,
   konten di atas wash, kotak identitas jadi `bg-elevated/60 ring-1 ring-inset ring-default` (dulu
   `border border-default bg-elevated`), dan footer jadi band `bg-elevated/40 px-5 py-4` (dulu satu div
   tanpa latar).

**TERUKUR SESUDAH:** modal form - elemen berkelas `pl-record-modal` (896x656), ::after ada dengan
w=250px h=250px bg=rgb(0,220,130) filter=blur(60px) opacity=0.14 top=-110px right=-90px z=0
pointer-events=none, wrapper `relative z-10` ✓. Modal delete - wash ada (oklab(0.704 0.177 0.072/0.2),
blur(64px), 192x192, aria-hidden="true"), kotak identitas `bg-elevated/60`, band footer `bg-elevated/40`;
teks judul di atas wash (terlihat jelas di screenshot).

**SENGAJA TIDAK DIUBAH:** animasi buka/tutup. Modal CCTV dan Users memakai keyframes reveal/dismiss
yang dinamai per modal (`users-record-modal[data-state='open']` dst); modal PC Ledger tidak punya
animasi itu sebelumnya, dan HIRO meminta bagian BACKGROUND-nya saja. Kalau animasinya juga mau
disamakan, itu tambahan terpisah.

### Field Department dihapus dari page PC Ledger (2026-10-10)

HIRO: *"hapus kolom departement dari page pc ledger, form add juga. departement tidak dipakai di page
pc ledger"*.

**YANG DIHAPUS (dan kenapa daftarnya sepanjang ini).** `departemen` bukan kolom tabel - ia muncul di
empat tempat lain, dan semuanya dibersihkan sekaligus supaya tidak meninggalkan dead code:
1. `FORM_SECTIONS` bagian Identity : `['staff_name','email','gid','departemen']` -> tanpa departemen.
2. Peta `LABELS`: entri `departemen: 'Department'` dihapus (form tidak lagi merender field itu).
3. `computed departments` dihapus. Ia dulu dipertahankan HANYA untuk mengisi field Department di
   `blankForm()` setelah filter Department dihapus; sekarang tidak ada lagi yang memakainya.
4. `blankForm()` : baris `form.departemen = departments.value[0] ?? 'Capacitor'` dihapus.
5. Placeholder input `:placeholder="key === 'departemen' ? 'Capacitor' : ''"` dihapus - tidak ada lagi
   field yang bisa bernilai 'departemen'.
6. Komentar kepala file diperbarui (dulu menulis department "filters on screen" - sudah tidak benar).

**YANG SENGAJA TIDAK DIHAPUS.** Ekspor Excel masih membaca `departemen` (baris ~770): workbook
IT FORM SG031 punya satu sel "Department:" di atas tabel, dan ekspor mengisinya dari nilai record.
Field di database + nilai 'Capacitor' pada record lama karena itu DIPERTAHANKAN - menghapusnya akan
membuat sel Department di file ekspor kosong dan sheet tidak lagi sama dengan form aslinya. Kode
pembacanya diberi komentar eksplisit supaya tidak dikira dead code di kemudian hari.

**DIUJI DULU SEBELUM MENGHAPUS (risiko data hilang).** `payload()` mengirim nilai dari form saja, jadi
kalau field dihapus dari form, apakah edit akan MENGHAPUS departemen pada record lama? Diuji ke API:
PUT parsial pada record 184 dengan hanya `{"values":{"staff_name":...}}` -> `departemen` tetap
'Capacitor' dan seluruh field lain utuh. API-nya **merge**, bukan replace. Jadi aman.

**TERUKUR SESUDAH (1920x1080):** tabel 12 sel header (11 kolom + Actions) - NO, STAFF NAME, EMAIL, GID,
JAPAN HOST, MODEL, S/N, CHASSIS, LOCATION, REMARK1, REMARK2, ACTIONS; 26 baris; tidak ada teks
"Department" di mana pun di halaman; form "New PC record" tinggal **14 field** (dulu 15) dan tidak
mengandung Department; filter chassis ("All chassis") tetap ada; halaman tetap render tanpa error.

**KESALAHAN SAYA SENDIRI DI TENGAH JALAN (dan sudah diperbaiki sebelum verifikasi):** satu patch
penghapusan `computed departments` salah menyerap baris berikutnya sehingga `const chassisTypes`
berubah nama menjadi `locations` - artinya ada dua `const locations` dan `chassisTypes` hilang
(error kompilasi, filter chassis rusak). Langsung diperbaiki; grep membuktikan `chassisTypes` sekarang
dideklarasikan sekali dan masih dipakai oleh `:items` filter chassis.

### Perbaikan: kolom Staff Name bergeser ke kiri saat kolom di-tick (2026-10-10)

HIRO: *"ada bug lagi, setiap kali filter kolom di tick, kolom staff name bergeser ke kiri. perbaiki"*.

**AKAR MASALAH (diukur per-frame, bukan ditebak).** Kolom Staff Name adalah kolom pinned kedua, jadi
posisinya = lebar kolom No yang **DIUKUR** (`pinOffset`), bukan nilai tetap. Karena kolom memakai
persentase, lebar kolom No ikut berubah setiap kali set kolom berubah: dari rekaman, kolom No
**64px -> 61px pada frame 7** saat Date di-tick. Tapi `measurePinOffset()` baru dipanggil di akhir
animasi (timeout `COL_ANIM_MS + 100` = frame 33), jadi selama ~430ms `left` kolom Staff Name masih 64px
sementara lebar kolom No sudah 61px - sel pinned **tertahan** di posisi lama oleh sticky, lalu
**melompat ke kiri 297 -> 294 dalam satu frame** begitu animasi selesai. Itulah pergeseran yang terlihat.
Besar lompatannya mengikuti seberapa banyak set kolom berubah (makin banyak kolom di-tick, makin besar).

**PERBAIKAN.** `measurePinOffset()` dipanggil di dalam watcher `visibleKeys`, tepat setelah layout
berubah dan SEBELUM snapshot `after` diambil (plus satu `await nextTick()` lagi karena binding `:style`
baru masuk DOM pada tick berikutnya). Dengan begitu offset pin sudah benar saat layout berubah, dan
kolom pinned ikut menjadi "mover" biasa di FLIP sehingga meluncur bersama kolom lain, bukan tertinggal
lalu mengoreksi diri di akhir.

**TERUKUR SESUDAH (trello per-frame, 1920x1080):**
- Tick ON (12 -> 13 kolom): `staffThCss` 64px -> **61px pada frame 8** (dulu frame 33); posisi sel
  header & body meluncur 297 -> 296 -> 295 -> 294 (frame 12-16) bersamaan; jarak terhadap tepi kanan
  kolom No (`gap`) 3.2px -> 0 tepat di frame 29; transform tersisa 0.
- Tick OFF (13 -> 12 kolom): `staffThCss` 61px -> **64px pada frame 7**; `transform: translateX(-3.17px)`
  terpasang frame 7 lalu dilepas frame 8; posisi 294 -> 295 -> 296 -> 297 -> 298 (overshoot) -> 297;
  gap kembali 0 di frame 14.
- Keadaan akhir kedua arah: `noW` 61px = `staffThLeft` = `staffTdLeft` = "61px", gapTh = gapTd = **0**,
  `leftoverTransforms` = 0, header dan body sejajar, picker membaca "Columns (12/15)".

### Sel Department dihapus dari ekspor Excel PC Ledger (2026-10-10)

HIRO: *"bro saya tidak butuh sel departement pada saat export excel, hapus saja code yang mengandung
departement"*.

**YANG DIHAPUS.** Blok penulis sel di ekspor: label `Department:` di (9,3) dan nilainya di (9,4),
termasuk pembacaan `departemen` dari record (`deptValues`, `deptCell`). Setelah ini **tidak ada satu pun
kode di halaman ini yang membaca field tersebut** - ia tadinya satu-satunya pembaca yang tersisa.

**ROW 9 TETAP ADA, hanya kosong.** Alamat baris di sheet ini absolut (notes di baris 3-6, header baris 10,
data dari baris 11), jadi menghapus barisnya akan memaksa penomoran ulang seluruh ekspor. Barisnya
dibiarkan sebagai spacer, dan komentarnya diperbarui supaya tidak lagi menyebut sel itu berisi apa pun.

**DUA KOMENTAR USANG DIPERBAIKI** (keduanya menyebut isi baris 9 yang sudah tidak ditulis lagi):
peta tata letak sheet di atas fungsi ekspor, dan komentar kepala file.

**DIUJI END-TO-END, bukan sekadar dibaca.** Browser diberi `Browser.setDownloadBehavior` ke folder
scratch, tombol Excel diklik, lalu file yang benar-benar terunduh diperiksa:
`PC_Ledger_2026-10-09.xlsx` (9.993 bytes, arsip xlsx valid, 143 string) - **sel C9 dan D9 KOSONG**,
baris yang ada: 1, 3, 4, 5, 6, 10, 11, ... 32 (baris 2, 7, 8, 9 memang kosong sesuai tata letak asli).

**SATU TEMUAN YANG PERLU KEPUTUSAN.** Masih ada satu string di workbook yang mengandung kata
"Department": judul form `IT FORM SG031 Department PC Ledger Form v7` (baris 1 sheet, dan teks yang sama
juga jadi subtitle di halaman). Itu **nama formnya**, bukan selnya - tidak saya ubah karena judul itu
milik form aslinya. Kalau mau diganti juga, bilang saja.

**CATATAN FIELD DI DATABASE.** Field `departemen` + nilai 'Capacitor' pada 26 record masih ada di entity
(deleting data bukan sesuatu yang pantas dilakukan diam-diam oleh perubahan UI). Sekarang tidak ada kode
yang memakainya. Menghapus field-nya adalah langkah terpisah yang butuh persetujuan eksplisit.

### Scrollbar horizontal yang berkedip saat kolom di-untick (2026-10-10)

HIRO: *"setiap kali filter kolom di untick, ada scrollbar muncul sebentar dibagian bawah tabel, saya
tidak mau itu terjadi"*.

**AKAR MASALAH (diukur per-frame).** Sel yang sedang memakai `transform` FLIP tetap dihitung sebagai
*scrollable overflow* milik scroll container - aturan yang sama yang dulu membuat scrollbar vertikal
berkedip saat filter chassis, kali ini di sumbu lain. Rekaman pada satu untick:

| frame | scrollWidth | clientHeight | scrollbar horizontal |
|---|---|---|---|
| 0 | 1650 | 756 | tidak |
| 8 (405 sel ber-transform) | **1657** | **744** | **muncul (track 12px)** |
| 17 | 1650 | 756 | tidak |

Jadi overflow transien 7px memunculkan scrollbar 12px selama ~9 frame, dan scrollbar itu memakan 12px
tinggi area tabel (clientHeight 756 -> 744) - dua gejala sekaligus.

**PERBAIKAN.** Selama animasi, scroll container diberi `overflow-x: hidden` supaya overflow transien
tidak bisa memunculkan scrollbar, lalu dikembalikan ke `auto` pada cleanup (timeout yang sama dengan
pelepasan transform), memakai `.pl-scroll` yang ditemukan dari `tableWrapEl.closest()`.
Container **dibiarkan apa adanya kalau scrollbar-nya memang sedang tampil** (layar sempit): menyembunyikan
lalu memunculkan kembali bar itu akan menyentak konten setinggi barnya sendiri - bug yang sama di sumbu
yang tidak diminta. Di lebar normal tidak ada bar horizontal sejak awal, jadi peralihannya tidak terlihat
dan tata letak akhirnya tidak berubah sama sekali.

**TERUKUR SESUDAH (rekaman per-frame, 1920x1080):** `overflow-x` auto -> **hidden** (frame 9) -> auto
(frame 36); **tinggi track scrollbar tetap 0 sepanjang 70 frame**; **`clientHeight` tetap 756** (tidak ada
sentakan); `scrollWidth` sempat 1651 pada frame 20 tapi karena overflow-x hidden tidak ada bar yang
muncul, dan setelah dikembalikan tetap 1650; header sticky tetap bekerja
(`theadTopVsScrollport: 0` saat scrollTop 300).

### Menu Device Ledger + halaman Factory PC (2026-10-10)

HIRO: *"sekarang ubah struktur menu sidebar, saya mau seperti ini: Device Ledger |- PC Ledger -> link url
tetap /pcledger |- Factory PC -> link url /factorypc. Pada halaman factory PC design/style/animasi
identik dengan halaman /pcledger, namun kolom tabel nya No, PIC, Email, Chasis, Model, SN, OS, Status,
Remarks, Actions"*.

**ENTITY BARU.** `factory_pc` (id 11) dibuat lewat API dengan 9 field: nomor (No, required+unique),
pic, email, chassis, model, sn, os, status, remarks. 0 record.

**HALAMAN BARU.** `web/app/pages/logbook/factorypc.vue` adalah SALINAN `pcledger.vue` yang diadaptasi -
bukan implementasi kedua - supaya design/style/animasi identik secara konstruksi: chrome tabel, hide
kolom + FLIP-nya, pasangan kolom pinned dengan offset terukur, tinggi header tetap 35px, penanganan
scrollbar, modal record dengan glow aksen, modal delete dengan wash merah. Yang diadaptasi: ENTITY_SLUG,
COLUMNS (9 kolom: No 4%, PIC 12%, Email 16%, Chassis 10%, Model 12%, SN 12%, OS 10%, Status 8%,
Remarks 11% = 95% + Actions 5%), DEFAULT_VISIBLE (9 kolom), FORM_SECTIONS (Ownership / Hardware /
Status and notes), LABELS, filter kedua (Location -> Status, karena entity baru tidak punya `lokasi`),
judul halaman, dan identitas baris di modal delete (PIC/Chassis).
**Excel export SENGAJA DIHAPUS** dari halaman ini (tombol + fungsinya) - tidak ada workbook yang harus
direproduksi, dan export itu khas IT FORM SG031 (judul, 4 baris catatan, lebar kolom). Kalau nanti mau
export untuk Factory PC, itu pekerjaan terpisah.

**BUG YANG SAYA TEMUKAN SENDIRI:** kunci localStorage pilihan kolom masih milik PC Ledger
(`infra-cap.pcledger.columns.v2`). Karena dua halaman ini punya daftar kolom berbeda, kunci bersama akan
membuat tiap halaman membuang pilihan halaman lain saat dimuat dan diam-diam kembali ke default. Sudah
diganti jadi `infra-cap.factorypc.columns`.

**MENU.** Grup "Device Ledger" (trigger, defaultOpen, ikon hard-drive) berisi PC Ledger (/logbook/pcledger)
dan Factory PC (/logbook/factorypc); entri flat untuk command palette juga ditambah.
CATATAN URL: HIRO menulis "/pcledger" dan "/factorypc"; URL PC Ledger yang berjalan adalah
`/logbook/pcledger` dan dia minta yang itu "tetap", jadi Factory PC diletakkan bersebelahan di
`/logbook/factorypc`. Kalau yang dimaksud adalah URL root `/pcledger` + `/factorypc`, itu memindahkan
kedua route dan perlu satu langkah terpisah.

**BELUM SELESAI DI TURN INI (jujur):**
1. `api/Services/LogbookNumberService.cs` sudah ditambah `FACTORY_PC_SLUG = "factory_pc"` dan dimasukkan
   ke set slug bernomor, TAPI API yang berjalan belum direstart. Kill proses `dotnet.exe` tadi BUTUH
   PERSETUJUAN dan promptnya tidak dijawab, jadi API masih memakai kode lama: membuat record Factory PC
   dari UI akan gagal validasi (field `nomor` required) sampai API direstart.
2. Verifikasi visual halaman Factory PC belum selesai karena daemon browser harness wedged
   (PermissionError pada bu-default.port); yang sudah terbukti: file bersih (0 referensi pc_ledger/
   staff_name/gid/Department/exportExcel) dan entity factory_pc ada di API dengan 9 field.

### Perbaikan: `</script>` hilang di factorypc.vue (2026-10-10)

Skrip adaptasi yang membuang blok Excel export ikut memakan tag `</script>` (rentangnya berakhir tepat
sebelum `<template>`, dan `</script>` ada di antaranya). Akibatnya Vite menolak file itu:
`[plugin:vite:vue] Element is missing end tag` dan halaman /logbook/factorypc menampilkan 500
"Failed to fetch dynamically imported module".

Terdeteksi DARI VERIFIKASI BROWSER, bukan dari pembacaan kode - pelajaran: menghapus blok dengan regex
"dari penanda X sampai sebelum Y" harus memeriksa pasangan tag yang berakhir di antaranya. Diperbaiki
dengan menyisipkan `</script>` sebelum `<template>`; `curl /logbook/factorypc` kembali 200.

### Factory PC: 10 record dummy, deskripsi, URL root, restart API (2026-10-10)

**RESTART API (HIRO: "eksekusi resart API") - dan pelajaran soal prosesnya.** Ternyata SERVER yang
memegang port 5099 bukan `dotnet.exe` melainkan **`Api.exe`** (PID 2776, anak dari run-host `dotnet.exe`),
jadi `taskkill /F /IM dotnet.exe /T` tidak menyentuhnya sama sekali - itu sebabnya percobaan pertama
"berhasil" tapi API masih memakai kode lama dan validasi `nomor` masih menolak. Selain itu bentuk
`taskkill //F //IM ...` DITOLAK MSYS sebagai "Invalid argument/option - '//F'", dan `cmd //c "..."` yang
dipakai lewat pipe hanya memunculkan banner cmd tanpa menjalankan perintahnya. Bentuk yang bekerja di
bash: `taskkill /F /PID <pid> /T` (slash tunggal). Setelah PID 2776 dimatikan, port bebas, API dijalankan
ulang dengan kode baru.

**10 RECORD DUMMY FACTORY PC - berhasil, 10/10.** `nomor` terisi otomatis oleh LogbookNumberService
(terlihat '10','9','8','7' pada urutan terbaru), yang sekaligus MEMBUKTIKAN restart + `FACTORY_PC_SLUG`
bekerja. PIC: Ahmad Fauzi, Budi Santoso, Citra Lestari, Dedi Kurniawan, Eka Wulandari, Fajar Nugroho,
Gita Permata, Hendra Wijaya, Indah Sari, Joko Prasetyo. Chassis Desktop/Notebook/Tablet, status
In Use/Standby/Repair/Retired, masing-masing dengan Remarks realistis. Total di API: 10.

**URL PINDAH KE ROOT.** `web/app/pages/logbook/pcledger.vue` -> `web/app/pages/pcledger.vue` dan
`.../logbook/factorypc.vue` -> `web/app/pages/factorypc.vue` (git mv), link sidebar + command palette
diubah ke `/pcledger` dan `/factorypc` (2+2 kemunculan). grep memastikan tidak ada sisa referensi
`/logbook/pcledger` atau `/logbook/factorypc` di web/. `/logbook/` sekarang hanya berisi cctvacc + handover.
`curl /factorypc` = 200.

**DESKRIPSI HALAMAN (rekomendasi HIRO).** Dipilih: **"Standalone PCs - not connected to the corporate
network or domain."** Alasannya: kalimat itu menyebutkan apa yang MEMBEDAKAN halaman ini dari PC Ledger
(yang domain-joined), bukan mengulang isi kolom. Alternatif yang saya pertimbangkan dan tidak dipakai:
a) "PCs outside the corporate domain - recorded separately for IT asset visibility." (lebih panjang,
   menekankan tujuan pencatatan) b) "Non-domain factory computers." (paling ringkas, tapi tidak
   menjelaskan kenapa dicatat terpisah). Deskripsi lama (warisan PC Ledger: "List of PC, Laptop, Tablet
   &middot; CAPACITOR Only") sudah diganti, jumlah record di belakangnya tetap ditampilkan.

### Halaman Data Import dengan tab (2026-10-10)

HIRO: *"buat halaman untuk data import, admin bisa import data file excel untuk halaman pc ledger.
posisi halaman ada diatas menu log audit. pada halaman tersebut saya mau ada navigation tab seperti di
web https://dashboard-template.nuxt.dev/settings (#dashboard-panel-settings > div.shrink-0...
min-h-[49px] > nav). Tab pertama Import PC Ledger, kedua GID List. pastikan style dan transisi sama
seperti page lain"*.

**HALAMAN BARU `/data-import`** (`web/app/pages/data-import.vue`). Tab bar-nya memakai komponen vendor
yang PERSIS dipakai template yang dia tunjuk - pola yang sama juga sudah dipakai halaman register di app
ini: `UDashboardPanel` + `UDashboardNavbar title="Data Import"` lalu `UDashboardToolbar` yang memuat
`UNavigationMenu` horizontal dengan prop `highlight`. Jadi tinggi bar (min-h 49px), border bawah, padding
dan indikator aktifnya adalah milik vendor, bukan tiruan. Tab diletakkan di QUERY STRING
(`/data-import?tab=import` / `?tab=gid`) dengan `exact: true` supaya menu bisa menandai satu tab aktif
tanpa child route. Transisi panel memakai `anim-fade-up` - kelas animasi standar app ini (main.css),
lengkap dengan guard prefers-reduced-motion yang sama seperti halaman lain.

**TAB 1 - Import PC Ledger.** File dibaca DULU di browser dengan ExcelJS, tidak ada yang ditulis sebelum
tombol Import ditekan:
- Baris header tidak di-hardcode. Skor tiap baris 1-15 berdasarkan berapa banyak label kolom yang
  dikenali; baris dengan skor terbanyak jadi header (pada export asli terdeteksi **Row 10**).
- Pemetaan label -> field `pc_ledger` (14 kolom; `No` sengaja TIDAK dipetakan karena API yang memberi
  nomor - field `nomor` required+unique, jadi mengirim nomor sendiri bisa bentrok).
- Ringkasan: nama sheet, baris header, jumlah kolom dikenali, jumlah baris siap import, baris kosong
  yang dilewati; plus badge kolom, preview 5 baris, lalu tombol "Import N record(s)" + ringkasan hasil
  (berhasil/gagal + pesan error pertama).

**TAB 2 - GID List.** Diturunkan dari record PC Ledger itu sendiri (tanpa entity baru): GID, pemiliknya,
dan jumlah PC per GID, dengan jumlah total di footer.

**MENU.** `Data Import` (ikon file-up) di grup yang sama, **DI ATAS Log Audit**, sesuai permintaan;
command palette ikut ditambah.

**TERVERIFIKASI DI BROWSER (1920x1080):** sidebar = Dashboard | Device Ledger (PC Ledger, Factory PC) |
Log Book (CCTV Access, Handover) | **Data Import** | Log Audit | User Management, dan menunya menyala
saat halaman dibuka. Tab bar menampilkan "Import PC Ledger" | "GID List"; klik GID List memindahkan URL
ke `?tab=gid`, mengganti isi panel, dan underline hijau pindah ke tab itu. GID List memuat data nyata:
8 GID, 26 PC tercakup (`.\capuser` 5, 7008269 Sepriadi 1, 7008318 Edy Purnomo 1, 7008386 Roestan 1,
7008512 Ratna Dewi 1, 29384_BALFI 4, 29384_DTS05 5, E0B5536/7 8). Tab Import diuji dengan file xlsx
nyata (hasil export halaman PC Ledger): "Sheet Ledger | Header row found Row 10 | Columns recognised 14
| Rows to import 26" + preview 5 baris + toast "File read - 26 row(s) ready to import".

**BELUM DIUJI, disebutkan terus terang:** langkah TULIS (menekan "Import 26 record(s)") sengaja tidak
saya jalankan supaya tidak menumpuk 26 baris duplikat di register dev. Panggilan per barisnya adalah
`apiCreateRecord` yang sama dengan tombol Add Record di halaman register (sudah terbukti jalan), tapi
klaim bahwa seluruh alur import 26 baris lolos belum saya buktikan.

### Data Import: header, import PC Ledger (layout tetap), import GID list (2026-10-10)

HIRO minta tiga hal di halaman Data Import, plus menjelaskan tujuan tab GID List.

**1. HEADER DISAMAKAN (ada tombol collapse).** Sebelumnya halaman ini memakai `UDashboardNavbar`
langsung; halaman lain memakai komponen lokal `PageHeader`, yang di komentarnya sendiri disebut
"carries the sidebar collapse control in the navbar's #leading slot, on every page". Sekarang halaman
ini memakai `PageHeader` juga - jadi tombol collapse, gaya, dan animasinya sama secara konstruksi.
Terverifikasi: tombol collapse terdeteksi di header (1), termasuk saat sidebar sedang runtuh.

**2. IMPORT PC LEDGER = LAYOUT TETAP.** Aturan deteksi header otomatis DIBUANG, diganti layout tetap
sesuai instruksi: data dibaca dari **row 11**, kolom **B..P (15 kolom)** sesuai urutan register
(No, Staff Name, Email Address, GID, JAPAN Hostname, Computer Model, Computer S/N, Date, Computer
Chassis, Computer Manufacturer, Computer O/S Name, Computer O/S Architecture, Location, Remark1,
Remark2). Kolom B (No) DIBACA tapi tidak dikirim: API yang memberi nomor (`nomor` required+unique).
- **Masalah nyata yang ketahuan dari uji:** percobaan pertama hanya 23 dari 26 baris masuk. Sebabnya
  kolom Date di file referensi bukan selalu tanggal - sebagian sel berisi angka serial Excel, dan
  beberapa berisi teks yang bukan tanggal sama sekali (di file referensi ada sel berisi "20"). API
  menolak field Date yang tidak bisa diparse, sehingga seluruh baris gagal. Ditambahkan
  `normaliseDate()`: Date asli, serial Excel 1900, dan string ISO semuanya diterima; yang benar-benar
  tidak bisa dibaca DIBUANG dan jumlahnya dilaporkan di ringkasan ("Unreadable dates dropped: 3"),
  bukan menggagalkan barisnya secara senyap.
- **UJI GANTI-ISI (HIRO mengizinkan hapus data lama):** 26 record lama dihapus lewat API, lalu file
  `D:\WORK\PANASONIC\Web\INFRA-CAPeff\PC_Ledger.xlsx` diimpor lewat halaman: ringkasan
  "Row 11, columns B-P | 26 of 26 row(s) | 3 unreadable dates dropped", hasil **"26 imported, 0 failed"**,
  API `pc_ledger` total 26, nomor otomatis 1..26. Data verbatim (mis. "Surface GO  4" dengan dua spasi,
  email "#NA", gid "E0B5536/7") tetap apa adanya.

**3. IMPORT GID LIST.** File `gid_list.xlsx` (header di baris 1) hanya dibaca kolom **A, C, E, H** =
Global ID, Alphabet Name, E-mail Address, Employee No. Hasil uji: **32 added, 0 failed**
(32 baris, semua punya Global ID). GID yang sudah ada DILEWATI, jadi mengimpor ekspor baru hanya
menambah yang belum ada. Datanya disimpan di entity baru **`gid_list`** (id 12, 4 field: gid
required+unique, name, email, employee_no) - bukan di cache browser - karena ini akan jadi sumber
saran di form PC Ledger, dan supaya semua admin melihat daftar yang sama. Tab ini juga menampilkan
daftar tersimpan (Global ID / Name / E-mail / Employee No) dengan pencarian dan tombol Refresh.

### Smart suggestion + smart fill di form PC Ledger (2026-10-10)

HIRO: *"data [gid list] ini sebagai sumber data pada form add new pc record pada halaman /pcledger.
saya mau fungsi smart suggestion / smart fill pada saat user ngetik di text box staff name, email
address, gid. design fungsi yang saya mau bagus sebaik mungkin"*.

**KOMPONEN BARU `SmartFillField.vue`** dipakai untuk tiga field itu (Staff Name, Email Address, GID).
Tiga keputusan desain yang membuatnya terasa benar:

1. **Dropdown di-TELEPORT ke <body> dan berposisi `fixed`.** Form ini hidup di dalam body dialog yang
   `overflow-y-auto`; dropdown absolute di dalamnya akan TERPOTONG di tepi body - saran hilang justru
   saat field-nya ada di bagian bawah form. Teleport keluar dari kliping itu, `fixed` + rect input
   menjaga posisinya menempel di field. Konsekuensinya daftar ditutup saat scroll, karena input bisa
   bergeser di bawah panel fixed.
2. **Pencocokan DIRANKING, bukan sekadar difilter.** Prefix pada field yang sedang diketik > prefix pada
   tiga kolom lain > substring. Terukur: mengetik "li" di Staff Name menaruh **LI LI OH** di paling atas,
   lalu KHENG HUA **LIM**, KHWAN HOON LIEW - bukan urutan acak.
3. **CROSS-FIELD BOOST.** Kalau form sudah berisi nama/email/GID yang cocok dengan kandidat, kandidat itu
   dinaikkan. Jadi mengetik email setelah memilih nama akan menyarankan orang yang sama.
4. **PICKING FILLS, TYPING NEVER DOES.** Tidak ada yang ditulis ke field lain saat mengetik; hanya saat
   memilih. Setelah memilih, field menampilkan jejak kecil "Filled from GID list (70D8456)" yang hilang
   begitu diketik ulang - jadi jelas nilai itu datang dari mana tanpa perlu toast yang berisik.
   Keyboard: panah atas/bawah, Enter memilih, Esc menutup, Tab menerima. Kandidat di-dedupe per GID,
   maksimal 8 baris, teks yang cocok di-highlight.

**BUG YANG KETAHUAN DARI VERIFIKASI BROWSER (bukan dari baca kode):** dropdown tidak pernah muncul saat
mengetik. Sebabnya `show()` dipanggil sinkron tepat setelah `emit('update:modelValue')`, sementara
`matches` dihitung dari PROP milik parent yang baru kembali satu tick kemudian - jadi saat show()
berjalan daftarnya masih kosong dan fungsi itu keluar lebih awal. Diperbaiki dengan `nextTick(show)`
plus watcher nilai agar daftar ikut segar saat field lain mengisi field ini.

**TERVERIFIKASI END-TO-END (1920x1080):** membuka Add Record, mengetik "li" di Staff Name ->
dropdown muncul (fixed, z=60, header "GID list - picking fills name, email and GID"), urutan kandidat
LI LI OH lalu KHENG HUA LIM... -> Enter -> **Staff Name = LI LI OH, Email = lili.oh@test.com,
GID = 70D8456** ketiganya terisi dari satu pilihan + hint "Filled from GID list (70D8456)" -> tombol
Add record ditekan -> API mencatat record baru nomor 27 dengan ketiga nilai itu -> baris uji DIHAPUS
lagi, total kembali 26.

### Revisi form PC Ledger + tab Data Import (2026-10-10)

**1. GARIS AKSEN TAB TIDAK MENGIKUTI TAB AKTIF — diperbaiki.** Diukur dulu: SEBELUM perbaikan, KEDUA
item menu berwarna primary baik sebelum maupun sesudah berpindah tab, sebab vendor menandai item dengan
mencocokkan `to` ke route dan hanya membandingkan PATH, sementara kedua tab berbagi path dan hanya beda
query. Perbaikan: `active` diset eksplisit dari query. Terukur sesudah: "Import PC Ledger=AKTIF | GID
List=nonaktif", lalu setelah diklik menjadi "Import PC Ledger=nonaktif | GID List=AKTIF" + URL ?tab=gid.

**2. SARAN TIDAK BISA DIKLIK — akarnya `pointer-events: none`.** Portal dialog Reka membungkus isi portal
dengan elemen ber-pointer-events:none dan hanya mengaktifkannya pada elemen content, sehingga node yang
di-teleport ke <body> mewarisi none. Panel saran tergambar benar dan keyboard jalan (Enter dikirim ke input
yang ada DI DALAM dialog) tapi semua klik mouse ditelan diam-diam. Bukti: `elementFromPoint` di atas baris
saran mengembalikan INPUT di belakangnya (hit-testing melewati elemen ber-pointer-events:none). Perbaikan:
`pointerEvents: 'auto'` pada panel; sesudahnya elemen di titik itu adalah elemen DI DALAM list.

**3. DROPDOWN TIDAK BISA DI-SCROLL — diperbaiki.** Listener scroll memakai fase capture sehingga scroll
DI DALAM list dianggap scroll halaman dan menutup dropdown sebelum bisa digulir. Sekarang event yang
targetnya di dalam list diabaikan, plus `overscroll-contain`. Terukur: 8 baris, panel 288px vs scrollHeight
432px, setelah wheel `scrollTop=146` dan panel TETAP terbuka.

**4. DEFAULTS record baru:** tanggal = HARI INI (waktu lokal — picker mem-parse lokal, `new Date('yyyy-mm-dd')`
UTC dan bisa meleset sehari di WIB), O/S Name = "Microsoft Windows 11 Pro", O/S Arch = "64-bit"; semua tetap
bisa diubah. Terlihat di layar: Date "10 Oct 2026" + kedua field O/S terisi.

**5. DATE PICKER mengikuti logbook.** `<input type="date">` diganti komponen `DatePicker` milik app ini
(yang dipakai logbook), jadi kalender, warna aksen, dan mode gelap/terang sama. Terukur: `input[type=date]`
di dalam modal = 0.

**6. CHASSIS dropdown Desktop / Laptop / Tablet.** Nilai lama yang bukan salah satu dari ketiganya tetap
dimasukkan ke daftar, supaya baris lama berisi "Notebook" tidak jadi kosong. Terukur: kontrol "Pick a
chassis" ada di form.

**7. SARAN MODEL** dari model yang sudah pernah diinput (record register itu sendiri, paling sering dipakai
dulu, dengan keterangan "used N times").

**REFACTOR:** `SmartFillField.vue` diganti `SuggestInput.vue` yang generik, lalu SmartFillField DIHAPUS
(tidak dipakai lagi).

**BELUM TERVERIFIKASI DI BROWSER (disebut terus terang):** (a) rantai klik-mouse -> mengisi field sesudah
perbaikan pointer-events — penyebabnya sudah terbukti hilang di level hit-testing, tapi saya belum sempat
melihat sendiri satu klik mengisi field (probe terakhir menutup dialog sebelum nilai terbaca); Enter sudah
terbukti mengisi. (b) daftar opsi dropdown chassis belum dibuka. (c) saran model belum dipicu.

**KONDISI DATA:** saat verifikasi, register PC Ledger sempat terbaca 53 baris (impor 26 terduplikasi +
sisa baris uji). Dibersihkan kembali ke 26 baris (nomor 1..26) lewat API.

### Tiga perbaikan lanjutan (2026-10-10, setelah laporan HIRO)

**1. FLICKER SAAT KLIK TAB di halaman Data Import — diperbaiki.** Instrumentasi `document.getAnimations()`
per frame saat tab diklik menemukan `infra-fade-up` berjalan di `DIV.anim-fade-up.mx-auto`: kedua panel tab
membawa kelas animasi masuk, dan karena keduanya ditukar oleh `v-if`, tiap ganti tab me-mount panel baru
dan MEMUTAR ULANG animasi 340ms (fade + scale) di seluruh area halaman — itu yang terlihat sebagai
"membesar-mengecil". Elemen lain (spinner brand, hover menu) memang animasi tetap, bukan penyebabnya.
Perbaikan: animasi hanya untuk paint pertama (`entrance` ref, dilepas setelah 420ms); ganti tab bukan
animasi masuk. Terukur sesudah: `infra-fade-up` TIDAK ada lagi (`[]`) setelah tab diklik.

**2. KLIK SARAN MENUTUP MODAL — diperbaiki.** Sebelumnya panel saran di-teleport ke `<body>`, sehingga bagi
Reka node itu ORANG LUAR dialog; klik di atasnya dibaca sebagai klik di luar dialog dan modal ditutup.
Terukur: klik benar-benar mengenai list (`elementFromPoint` => node di dalam list) tetapi dialog tetap
tertutup. Perbaikan: panel di-teleport ke elemen `[role=dialog]` milik input (di dalam dismissable layer),
plus posisi dihitung dua tahap: panel diletakkan di 0,0, dibaca posisi nyatanya, lalu di-offset selisihnya
(persis baik dialog punya transform/tidak), dan bila ruang di bawah tidak cukup panel dibalik ke atas field
atau tingginya dibatasi. Terukur sesudah: `panel di dalam dialog: true`, klik baris saran mengisi Staff
Name/Email/GID (KHENG HUA LIM / khlim@test.com / 70D8447, trace "Filled from GID list (70D8447)") dan
dialog TETAP terbuka.

**3. Z-INDEX IKON ↵ vs header.** Header panel kini `sticky top-0 z-10` dengan latar `bg-elevated`, jadi
penanda ↵ pada baris tidak pernah tampil di atas header saat list digulir. Terukur: header z-index = 10.

### Flickering teks sidebar + header (2026-10-10, lanjutan)

**INSTRUMENTASI (3 metode, sesuai kronologi HIRO: refresh lalu klik tab GID List).**
(1) `document.getAnimations()` per frame, (2) `animationstart`/`animationend` di document, (3) MutationObserver
pada atribut `data-collapsed` sidebar dan pada `header h1`. Hasil: saat tab DIKLIK tidak ada apa pun yang
berubah pada sidebar maupun judul header (tidak ada animasi, tidak ada mutasi atribut, geometri konstan:
sidebar 208px, judul 114x28). Jadi klik tab di lingkungan ini bersih.

**TAPI penyebabnya ketemu di jalur LOAD, dan itu struktural.** Animasi spring sidebar dipicu LANGSUNG oleh
atribut vendor `data-collapsed` di CSS (`#dashboard-sidebar-app-v2[data-collapsed='false']`). Selector itu
juga cocok pada render PERTAMA, jadi setiap load memutar ulang translate 360ms di SELURUH batang sidebar —
semua teks menu bergerak serentak, persis keluhan "flickering teks pada seluruh menu sidebar". Hal yang
sama terjadi pada tombol collapse di header: kelas animasinya sudah ada sejak render pertama sehingga
tombol bounce (scale 0.88 -> 1.06 -> 1) di SETIAP load. Di mode dev keduanya bahkan baru jalan ~3.5 detik
setelah dokumen dimuat (terukur: animationstart pada t=3471ms), sehingga tampak seperti dipicu oleh klik
tab yang kebetulan dilakukan saat itu.

**PERBAIKAN.**
- Sidebar: animasi TIDAK lagi di-key ke atribut vendor. Layout menonton `data-collapsed` lewat
  MutationObserver (di `document.body`, `subtree: true`, agar tetap hidup setelah re-mount - versi lama
  pernah menonton elemennya sendiri lalu diam-diam mengawasi node yang sudah terlepas) dan menulis
  `data-spring-dir="close"|"open"` HANYA ketika nilainya benar-benar berubah. CSS-nya sekarang
  `[data-spring-dir=...]`, jadi CSS tidak bisa lagi memulai animasi sendiri: load bersih, dan penulisan
  atribut yang berulang/nir-perubahan juga tidak memicu apa-apa. Karena flip selalu bergantian
  close/open, nama animasinya berubah dan spring tetap restart saat dipakai.
- Tombol collapse di header: `armed` hanya di-set setelah flip PERTAMA yang nyata; nilai pertama yang
  terlihat (kondisi mount) hanya dicatat. Bounce jadi milik aksi collapse/expand saja.

**VERIFIKASI.** Saat load: `infra-sidebar-*` dan `infra-collapse-*` TIDAK ada, `data-spring-dir` = null.
Saat tombol collapse diklik: `infra-collapse-open` pada tombol DAN `infra-sidebar-open` pada sidebar
berjalan (`data-spring-dir` = open), jadi fitur aslinya tidak hilang.

**CATATAN JUSTRU:** saat load animasi `infra-fade-up` pada konten tercatat DUA kali (bukan bagian dari
sidebar/header). Belum saya ubah karena keluhan menyangkut sidebar dan header.

**BELUM BISA DIUJI DI SINI:** bila flicker itu ternyata berasal dari geometri scrollbar (bar yang
muncul/hilang mencuri ~15px lebar sehingga konten bergeser), lingkungan headless di mesin ini memakai
scrollbar OVERLAY sehingga lebar tidak pernah tersita - kelas bug ini tidak bisa direproduksi lokal.

### Teks chrome "bergetar" setiap refresh — akarnya FONT SWAP (2026-10-10)

**Kronologi yang benar (dari HIRO):** bukan klik tab, tetapi SETIAP REFRESH, dan terjadi di SEMUA halaman.

**BUKTI PENGUKURAN.** Perekam dipasang lewat `Page.addScriptToEvaluateOnNewDocument` sehingga ikut selamat
melewati refresh dan mulai dari awal dokumen, lalu merekam rect sidebar, label sidebar, judul header, dan
panel setiap frame. Hasil sebelum perbaikan: pada t=3668ms **lebar judul berubah 115.19px -> 113.70px
sementara left/top-nya TIDAK berubah** (270 / 17.5). Perubahan lebar tanpa pergeseran posisi adalah tanda
khas teks dirender ulang dengan font berbeda — bukan animasi, bukan scrollbar. `font-display: swap` pada
@font-face membuat paint pertama memakai font fallback, lalu Public Sans datang dan menggantinya.

**MENGAPA TERJADI DI SEMUA HALAMAN DAN SETIAP REFRESH.** @font-face Public Sans hanya dideklarasikan di
CSS aplikasi (`@import "@fontsource/public-sans/*.css"`), dan CSS itu di-inject oleh JavaScript. Pada
render pertama browser belum mengenal face-nya, jadi seluruh teks chrome (label sidebar + judul header)
digambar dengan fallback lebih dulu, kemudian dirender ulang begitu face dikenal dan berkas font tiba.
Karena itu letaknya di shell (semua halaman), bukan di satu page.

**PERBAIKAN.**
- Berkas font disalin ke `web/public/fonts/` sebagai aset statis (4 bobot latin: 400/500/600/700 woff2)
  supaya URL-nya stabil dan bisa di-preload.
- Deklarasi @font-face DIPINDAH dari CSS ke `nuxt.config.ts` -> `app.head.style`, jadi browser mengenal
  face-nya saat parsing HTML, sebelum JavaScript aplikasi jalan.
- Keempat berkas di-preload (`rel=preload as=font type=font/woff2 crossorigin`) sehingga font siap sebelum
  paint pertama. `app.head.link` dan URL di CSS memakai prefix `NUXT_APP_BASE_URL` karena produksi dilayani
  di bawah /INFRA-CAP.
- `@import "@fontsource/public-sans/*.css"` dihapus dari main.css agar tidak ada deklarasi ganda, dengan
  komentar yang menjelaskan mengapa deklarasinya sekarang ada di head.

**VERIFIKASI SESUDAH.** Judul header: **0 perubahan** — pengukuran pertamanya (t=6041ms, saat shell pertama
muncul) sudah [270, 17.5, **113.7**], yaitu lebar Public Sans, nilai yang sebelumnya hanya tercapai SETELAH
swap. Label sidebar dan sidebar root juga 0 perubahan. Berkas font tersaji 200 dengan 14632 byte asli.

**CATATAN TERKAIT (sudah dikerjakan sebelumnya di sesi ini).** Spring sidebar dan bounce tombol collapse
di header tidak lagi dipicu oleh atribut vendor `data-collapsed` (dulu ikut main di setiap load, seluruh
teks sidebar bergerak 360ms) — sekarang hanya saat aksi collapse/expand nyata.
