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
    public DbSet<InvoiceLicense> Licenses => Set<InvoiceLicense>();
    public DbSet<LicenseHist> LicenseHists => Set<LicenseHist>();
    public DbSet<GuiTongHop> GuiTongHops => Set<GuiTongHop>();
    public DbSet<GuiTongHopDtl> GuiTongHopDtls => Set<GuiTongHopDtl>();
    public DbSet<TaxOffice> TaxOffices => Set<TaxOffice>();
    public DbSet<NntLookupLog> NntLookupLogs => Set<NntLookupLog>();
    public DbSet<InvoiceEmailLog> InvoiceEmailLogs => Set<InvoiceEmailLog>();
    public DbSet<ConversionPrintLog> ConversionPrintLogs => Set<ConversionPrintLog>();
    public DbSet<ReSignLog> ReSignLogs => Set<ReSignLog>();
    public DbSet<ApproveLog> ApproveLogs => Set<ApproveLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();
    public DbSet<InvoiceNoAllocLog> InvoiceNoAllocLogs => Set<InvoiceNoAllocLog>();

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
            e.HasOne(x => x.RefInvoice).WithMany().HasForeignKey(x => x.RefInvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TranMessage>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceLicense>(e =>
        {
            e.Ignore(x => x.Remaining);
            e.HasIndex(x => new { x.OrgId, x.NntId }).IsUnique();   // mỗi NNT một hạn mức
            e.HasOne(x => x.Nnt).WithMany().HasForeignKey(x => x.NntId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<LicenseHist>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.NntId });
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<GuiTongHop>(e =>
        {
            e.Ignore(x => x.TotalAmount);
            e.Ignore(x => x.TotalVat);
            e.Ignore(x => x.TotalPayment);
            e.HasIndex(x => new { x.OrgId, x.NntId });
            e.HasOne(x => x.Nnt).WithMany().HasForeignKey(x => x.NntId);
            e.HasMany(x => x.Details).WithOne().HasForeignKey(x => x.GuiTongHopId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<GuiTongHopDtl>(e =>
        {
            e.Property(x => x.TTCThue).HasPrecision(18, 2);
            e.Property(x => x.TgTThue).HasPrecision(18, 2);
            e.Property(x => x.TgTTToan).HasPrecision(18, 2);
            e.Property(x => x.SLuong).HasPrecision(18, 2);
            e.Property(x => x.TSuat).HasPrecision(9, 2);
            e.HasIndex(x => new { x.OrgId, x.GuiTongHopId });
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TaxOffice>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.GovTaxID }).IsUnique();   // mỗi CQT một mã
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<NntLookupLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Mst });
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceEmailLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<ConversionPrintLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<ReSignLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<ApproveLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<SystemSetting>(e =>
        {
            e.HasIndex(x => x.OrgId).IsUnique();   // mỗi tổ chức một cấu hình
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceTemplate>(e =>
        {
            e.Ignore(x => x.QtyRemain);
            e.HasIndex(x => new { x.OrgId, x.TInvoiceCode }).IsUnique();   // mỗi mẫu một mã
            e.HasOne(x => x.Nnt).WithMany().HasForeignKey(x => x.NntId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceNoAllocLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
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
