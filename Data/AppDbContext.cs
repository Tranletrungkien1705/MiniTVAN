using Microsoft.EntityFrameworkCore;
using MiniTVAN.Models;

namespace MiniTVAN.Data;

public class AppDbContext : DbContext
{
    private readonly Guid _orgId;
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options) => _orgId = tenant.OrgId;

    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<Nnt> Nnts => Set<Nnt>();
    public DbSet<CustomerNnt> CustomerNnts => Set<CustomerNnt>();
    public DbSet<NntType> NntTypes => Set<NntType>();
    public DbSet<CustomerNntType> CustomerNntTypes => Set<CustomerNntType>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Dealer> Dealers => Set<Dealer>();
    public DbSet<Department> Departments => Set<Department>();
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
    public DbSet<BulkApproveLog> BulkApproveLogs => Set<BulkApproveLog>();
    public DbSet<IssueLog> IssueLogs => Set<IssueLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<DynamicComma> DynamicCommas => Set<DynamicComma>();
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();
    public DbSet<InvoiceNoAllocLog> InvoiceNoAllocLogs => Set<InvoiceNoAllocLog>();
    public DbSet<TctReceiveLog> TctReceiveLogs => Set<TctReceiveLog>();
    public DbSet<InvoiceUpdateLog> InvoiceUpdateLogs => Set<InvoiceUpdateLog>();
    public DbSet<TemplateRangeLog> TemplateRangeLogs => Set<TemplateRangeLog>();
    public DbSet<CancelInvoiceLog> CancelInvoiceLogs => Set<CancelInvoiceLog>();
    public DbSet<InvoiceRecordLog> InvoiceRecordLogs => Set<InvoiceRecordLog>();
    public DbSet<BulkFixLog> BulkFixLogs => Set<BulkFixLog>();
    public DbSet<TemplateTctLog> TemplateTctLogs => Set<TemplateTctLog>();
    public DbSet<InvoiceCustomField> InvoiceCustomFields => Set<InvoiceCustomField>();
    public DbSet<InvoiceDtlCustomField> InvoiceDtlCustomFields => Set<InvoiceDtlCustomField>();
    public DbSet<InvoiceTempGroup> InvoiceTempGroups => Set<InvoiceTempGroup>();
    public DbSet<InvoiceTempGroupField> InvoiceTempGroupFields => Set<InvoiceTempGroupField>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<OrgCks> OrgCkses => Set<OrgCks>();
    public DbSet<NotifyType> NotifyTypes => Set<NotifyType>();
    public DbSet<Notify> Notifies => Set<Notify>();
    public DbSet<NotifyDtl> NotifyDtls => Set<NotifyDtl>();
    public DbSet<NotifyRecipient> NotifyRecipients => Set<NotifyRecipient>();
    public DbSet<NotifyRecipientType> NotifyRecipientTypes => Set<NotifyRecipientType>();
    public DbSet<ColumnConfig> ColumnConfigs => Set<ColumnConfig>();
    public DbSet<SortColumnInvoice> SortColumnInvoices => Set<SortColumnInvoice>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        if (Database.IsNpgsql()) b.HasDefaultSchema("minitvan");
        b.Entity<Org>().HasIndex(x => x.ApiKey).IsUnique();
        b.Entity<Nnt>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Mst }).IsUnique();
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<CustomerNnt>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.MST, x.CustomerNNTCode }).IsUnique();   // mỗi NNT một mã khách hàng
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<NntType>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.NNTType }).IsUnique();   // mỗi tổ chức một mã loại NNT
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<CustomerNntType>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.CustomerNNTType }).IsUnique();   // mỗi tổ chức một mã loại khách hàng
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Province>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.ProvinceCode }).IsUnique();   // mỗi tổ chức một mã tỉnh/thành
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<District>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.ProvinceCode, x.DistrictCode }).IsUnique();   // mỗi tổ chức một mã quận/huyện trong tỉnh
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Country>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.CountryCode }).IsUnique();   // mỗi tổ chức một mã quốc gia
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Dealer>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.DLCode }).IsUnique();   // mỗi tổ chức một mã đại lý
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Department>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.DepartmentCode }).IsUnique();   // mỗi tổ chức một mã phòng ban
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Invoice>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.VatRate).HasPrecision(9, 2);
            e.Ignore(x => x.VatAmount);
            e.Ignore(x => x.Total);
            e.HasIndex(x => x.TctCode).IsUnique();          // mã tra cứu GLOBAL (xuyên tenant)
            e.HasIndex(x => x.MCCQTMTT).IsUnique();         // mã CQT trên HĐ MTT GLOBAL (xuyên tenant)
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
        b.Entity<BulkApproveLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.CreatedAt });
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<IssueLog>(e =>
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
        b.Entity<DynamicComma>(e =>
        {
            e.HasIndex(x => x.OrgId).IsUnique();   // mỗi tổ chức một cấu hình dấu phân cách
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
        b.Entity<TctReceiveLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceUpdateLog>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.VatRate).HasPrecision(9, 2);
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TemplateRangeLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.TemplateId });
            e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<CancelInvoiceLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceRecordLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceId });
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<BulkFixLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.TemplateId });
            e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<TemplateTctLog>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.TemplateId });
            e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceCustomField>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceCustomFieldCode }).IsUnique();   // mỗi tổ chức một mã trường
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceDtlCustomField>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceDtlCustomFieldCode }).IsUnique();   // mỗi tổ chức một mã trường
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceTempGroup>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceTGroupCode }).IsUnique();   // mỗi tổ chức một mã nhóm mẫu
            e.HasMany(x => x.Fields).WithOne(f => f.Group).HasForeignKey(f => f.InvoiceTempGroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<InvoiceTempGroupField>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.InvoiceTempGroupId });
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<MessageTemplate>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.MessageTplCode }).IsUnique();   // mỗi tổ chức một mã mẫu thông điệp
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<OrgCks>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.CANumber }).IsUnique();   // mỗi tổ chức một số chứng thư
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<NotifyType>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.NotifyTypeCode }).IsUnique();   // mỗi tổ chức một mã loại thông báo
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<Notify>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.NotifyNo }).IsUnique();   // mỗi tổ chức một số thông báo
            e.HasMany(x => x.Details).WithOne(d => d.Notify).HasForeignKey(d => d.NotifyId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<NotifyDtl>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.NotifyId });
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<NotifyRecipient>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.UserCode }).IsUnique();   // mỗi tổ chức một mã người nhận
            e.HasMany(x => x.Types).WithOne(t => t.Recipient).HasForeignKey(t => t.NotifyRecipientId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<NotifyRecipientType>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.UserCode, x.NotifyType }).IsUnique();   // mỗi người nhận một loại thông báo
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<ColumnConfig>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.TableName, x.ColumnName }).IsUnique();   // mỗi tổ chức một cấu hình cho (bảng, cột)
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<SortColumnInvoice>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.ColumnCode }).IsUnique();   // mỗi tổ chức một mã cột
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
