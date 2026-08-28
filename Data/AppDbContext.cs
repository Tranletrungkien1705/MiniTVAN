using Microsoft.EntityFrameworkCore;
using MiniTVAN.Models;

namespace MiniTVAN.Data;

public class AppDbContext : DbContext
{
    private readonly Guid _orgId;
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options) => _orgId = tenant.OrgId;

    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<Nnt> Nnts => Set<Nnt>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<TranMessage> Messages => Set<TranMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        if (Database.IsNpgsql()) b.HasDefaultSchema("minitvan");
        b.Entity<Org>().HasIndex(x => x.ApiKey).IsUnique();
        b.Entity<Nnt>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Mst }).IsUnique();
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Invoice>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.VatRate).HasPrecision(9, 2);
            e.Ignore(x => x.VatAmount);
            e.Ignore(x => x.Total);
            e.HasIndex(x => x.TctCode).IsUnique();          // mã tra cứu GLOBAL (xuyên tenant)
            e.HasIndex(x => new { x.OrgId, x.NntId });
            e.HasOne(x => x.Nnt).WithMany().HasForeignKey(x => x.NntId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TranMessage>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
    }

    public override int SaveChanges() { StampOrg(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) { StampOrg(); return base.SaveChangesAsync(ct); }
    private void StampOrg()
    {
        foreach (var e in ChangeTracker.Entries<IOrgOwned>())
            if (e.State == EntityState.Added && e.Entity.OrgId == Guid.Empty) e.Entity.OrgId = _orgId;
    }
}
