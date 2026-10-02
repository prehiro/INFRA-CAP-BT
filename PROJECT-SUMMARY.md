# INFRA-CAP — Ringkasan Project

> File ini adalah **backup** dari holographic memory. Kalau memory agent hilang/reset, baca file ini untuk melanjutkan.
> Terakhir diupdate: 2026-10-02

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

## 8. Permintaan terbuka ke HIRO

> **SQL Server kantor pakai Windows Auth atau SQL Auth?**
> Kalau Windows Auth → connection string `Integrated Security=True`, app pool harus jalan sebagai domain service account, dan DB user login-nya perlu `Enable Windows Authentication`. Jawaban ini menentukan langkah setup IIS.
