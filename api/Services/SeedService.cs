using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class SeedService
{
    private readonly AppDbContext _db;
    public SeedService(AppDbContext db) => _db = db;

    public async Task SeedAsync(string adminUsername, string adminPassword)
    {
        var adminRole = await EnsureRoleAsync("Admin", "Akses penuh: kelola entity, field, user, dan semua data");
        await EnsureRoleAsync("Manager", "Kelola data master dan transaksi, tidak bisa kelola user");
        await EnsureRoleAsync("Staff", "Input dan lihat transaksi, read-only untuk master data");

        if (!await _db.Users.AnyAsync(u => u.Username == adminUsername))
        {
            _db.Users.Add(new AppUser
            {
                Username = adminUsername,
                Email = "admin@internal.local",
                FullName = "Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            });
            await _db.SaveChangesAsync();
        }

        var admin = await _db.Users.Include(u => u.UserRoles).FirstAsync(u => u.Username == adminUsername);
        if (!admin.UserRoles.Any(r => r.RoleId == adminRole.Id))
        {
            admin.UserRoles.Add(new AppUserRole { UserId = admin.Id, RoleId = adminRole.Id });
            await _db.SaveChangesAsync();
        }

        await SeedSampleSchemaAsync();
        await SeedCctvLogBookAsync();
    }

    /// <summary>
    /// The CCTV Log Book, mirroring the paper form "RECORDABLE MEDIA LOG BOOK" used by
    /// ISD. Seeded separately from the sample schema because the sample schema bails out
    /// as soon as ANY entity exists, while the logbook must appear even on a database
    /// that already holds Customer/Product/Sales Order.
    ///
    /// Signature fields (requestor_sign, pic_isd_sign, isd_sign) are Text fields holding
    /// a data-URL PNG drawn on the signature pad. They carry no MaxLength so the base64
    /// payload is not truncated by the generic validator.
    /// </summary>
    private async Task SeedCctvLogBookAsync()
    {
        if (await _db.Entities.AnyAsync(e => e.Slug == LogbookNumberService.CCTV_SLUG)) return;

        var cctv = new DynamicEntity
        {
            Name = "CCTV Log Book",
            Slug = LogbookNumberService.CCTV_SLUG,
            Kind = EntityKind.Transaction,
            Description = "Logbook akses rekaman CCTV (form RECORDABLE MEDIA LOG BOOK)",
            DisplayField = LogbookNumberService.NO_FIELD,
            IsSystem = false,
            IsActive = true,
            SortOrder = 10,
            Fields =
            {
                new DynamicField { Name = "nomor",              Label = "NO",                          Type = FieldType.Text,     IsRequired = true, IsUnique = true,  MaxLength = 30,  SortOrder = 0 },
                new DynamicField { Name = "tanggal",            Label = "Date",                        Type = FieldType.Date,     IsRequired = true, SortOrder = 1 },
                new DynamicField { Name = "departemen",         Label = "Department",                  Type = FieldType.Text,     MaxLength = 100, SortOrder = 2 },
                new DynamicField { Name = "no_pegawai",         Label = "Employee No",                 Type = FieldType.Text,     MaxLength = 50,  SortOrder = 3 },
                new DynamicField { Name = "nama_pemohon",       Label = "Name Requestor",              Type = FieldType.Text,     IsRequired = true, MaxLength = 200, SortOrder = 4 },
                new DynamicField { Name = "tujuan",             Label = "Purpose / Details",          Type = FieldType.TextArea, IsRequired = true, MaxLength = 1000, SortOrder = 5 },
                new DynamicField { Name = "waktu_diminta",      Label = "Time Request",               Type = FieldType.Text,     MaxLength = 50,  SortOrder = 6 },
                new DynamicField { Name = "tanda_pemohon",      Label = "Requestor Sign",             Type = FieldType.Text,     SortOrder = 7 },
                new DynamicField { Name = "pic_isd",            Label = "PIC by ISD",                 Type = FieldType.Text,     MaxLength = 200, SortOrder = 8 },
                new DynamicField { Name = "pic_mulai",          Label = "PIC start search date/time", Type = FieldType.Text,     MaxLength = 50,  SortOrder = 9 },
                new DynamicField { Name = "pic_selesai",        Label = "PIC end search date/time",   Type = FieldType.Text,     MaxLength = 50,  SortOrder = 10 },
                new DynamicField { Name = "tanda_isd",          Label = "ISD Sign",                   Type = FieldType.Text,     SortOrder = 11 },
            }
        };

        _db.Entities.Add(cctv);
        await _db.SaveChangesAsync();
    }

    private async Task<AppRole> EnsureRoleAsync(string name, string desc)
    {
        var r = await _db.Roles.FirstOrDefaultAsync(x => x.Name == name);
        if (r is null)
        {
            r = new AppRole { Name = name, Description = desc };
            _db.Roles.Add(r);
            await _db.SaveChangesAsync();
        }
        return r;
    }

    private async Task SeedSampleSchemaAsync()
    {
        if (await _db.Entities.AnyAsync()) return;

        var customer = new DynamicEntity
        {
            Name = "Customer", Slug = "customer", Kind = EntityKind.Master,
            Description = "Daftar pelanggan", DisplayField = "nama", SortOrder = 1,
            IsSystem = false, IsActive = true,
            Fields =
            {
                new DynamicField { Name = "kode",   Label = "Kode",       Type = FieldType.Text,   IsUnique = true,  IsRequired = true, MaxLength = 20, SortOrder = 0 },
                new DynamicField { Name = "nama",   Label = "Nama",       Type = FieldType.Text,   IsRequired = true, MaxLength = 200, SortOrder = 1 },
                new DynamicField { Name = "email",  Label = "Email",      Type = FieldType.Email,  MaxLength = 200, SortOrder = 2 },
                new DynamicField { Name = "telpon", Label = "Telpon",     Type = FieldType.Text,   MaxLength = 30, SortOrder = 3 },
                new DynamicField { Name = "alamat", Label = "Alamat",     Type = FieldType.TextArea, MaxLength = 500, SortOrder = 4 },
                new DynamicField { Name = "status", Label = "Aktif",      Type = FieldType.Boolean, SortOrder = 5, DefaultValue = "true" },
            }
        };

        var product = new DynamicEntity
        {
            Name = "Product", Slug = "product", Kind = EntityKind.Master,
            Description = "Daftar barang", DisplayField = "nama", SortOrder = 2,
            IsSystem = false, IsActive = true,
            Fields =
            {
                new DynamicField { Name = "sku",      Label = "SKU",        Type = FieldType.Text,   IsUnique = true, IsRequired = true, MaxLength = 30, SortOrder = 0 },
                new DynamicField { Name = "nama",     Label = "Nama Barang", Type = FieldType.Text,   IsRequired = true, MaxLength = 200, SortOrder = 1 },
                new DynamicField { Name = "kategori", Label = "Kategori",    Type = FieldType.Text,   MaxLength = 100, SortOrder = 2 },
                new DynamicField { Name = "harga",    Label = "Harga",       Type = FieldType.Decimal, IsRequired = true, SortOrder = 3 },
                new DynamicField { Name = "stok",     Label = "Stok",        Type = FieldType.Number, SortOrder = 4, DefaultValue = "0" },
            }
        };

        var salesOrder = new DynamicEntity
        {
            Name = "Sales Order", Slug = "sales_order", Kind = EntityKind.Transaction,
            Description = "Header transaksi penjualan", DisplayField = "nomor", SortOrder = 3,
            IsSystem = false, IsActive = true,
            Fields =
            {
                new DynamicField { Name = "nomor",         Label = "No. SO",     Type = FieldType.Text,    IsUnique = true, IsRequired = true, MaxLength = 30, SortOrder = 0 },
                new DynamicField { Name = "tanggal",       Label = "Tanggal",     Type = FieldType.Date,    IsRequired = true, SortOrder = 1 },
                new DynamicField { Name = "customer",      Label = "Customer",    Type = FieldType.Lookup,  IsRequired = true, LookupDisplayField = "nama", SortOrder = 2 },
                new DynamicField { Name = "product",       Label = "Produk",      Type = FieldType.Lookup,  IsRequired = true, LookupDisplayField = "nama", SortOrder = 3 },
                new DynamicField { Name = "qty",           Label = "Qty",         Type = FieldType.Number,  IsRequired = true, DefaultValue = "1", SortOrder = 4 },
                new DynamicField { Name = "harga_satuan",  Label = "Harga Satuan", Type = FieldType.Decimal, IsRequired = true, SortOrder = 5 },
                new DynamicField { Name = "total",         Label = "Total",       Type = FieldType.Decimal, SortOrder = 6 },
                new DynamicField { Name = "catatan",       Label = "Catatan",     Type = FieldType.TextArea, MaxLength = 500, SortOrder = 7 },
            }
        };

        _db.Entities.AddRange(customer, product, salesOrder);
        await _db.SaveChangesAsync();

        // Point the lookup fields at the freshly created master entities.
        foreach (var f in salesOrder.Fields.Where(f => f.Type == FieldType.Lookup))
        {
            var target = f.Name == "customer" ? customer : product;
            f.LookupEntityId = target.Id;
        }
        await _db.SaveChangesAsync();
    }
}
