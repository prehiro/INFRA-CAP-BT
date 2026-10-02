using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
    public DbSet<DynamicEntity> Entities => Set<DynamicEntity>();
    public DbSet<DynamicField> Fields => Set<DynamicField>();
    public DbSet<Record> Records => Set<Record>();
    public DbSet<RecordValue> RecordValues => Set<RecordValue>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.FullName).HasMaxLength(200);
        });

        b.Entity<AppRole>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        b.Entity<AppUserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DynamicEntity>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.DisplayField).HasMaxLength(100);
        });

        b.Entity<DynamicField>(e =>
        {
            e.HasIndex(x => new { x.EntityId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.DefaultValue).HasMaxLength(400);
            e.Property(x => x.OptionsJson).HasMaxLength(2000);
            e.Property(x => x.LookupDisplayField).HasMaxLength(100);
            e.HasOne(x => x.Entity).WithMany(en => en.Fields).HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Record>(e =>
        {
            e.HasIndex(x => new { x.EntityId, x.IsDeleted });
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            e.HasOne(x => x.Entity).WithMany(en => en.Records).HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecordValue>(e =>
        {
            e.HasIndex(x => new { x.RecordId, x.FieldId }).IsUnique();
            e.HasOne(x => x.Record).WithMany(r => r.Values).HasForeignKey(x => x.RecordId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Field).WithMany().HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Restrict);

            // Money/quantity precision must be explicit. SQL Server default decimal(18,2)
            // would silently truncate and rounds monetary values to 2 dp — unacceptable
            // for an inventory/transaction app.
            e.Property(x => x.NumberValue).HasPrecision(18, 4);
        });
    }
}
