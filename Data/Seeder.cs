using Microsoft.EntityFrameworkCore;
using MiniTVAN.Models;
namespace MiniTVAN.Data;

public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await MigratePostgresAsync(db);
        if (!await db.Orgs.AnyAsync(o => o.Id == TenantContext.DefaultOrgId))
        { db.Orgs.Add(new Org { Id = TenantContext.DefaultOrgId, Name = "Demo TVAN", ApiKey = TenantContext.DefaultApiKey }); await db.SaveChangesAsync(); }

        if (!await db.Nnts.AnyAsync())
        {
            var seller = new Nnt { Mst = "0101243150", Name = "Công ty CP Ô tô Đông Đô", Address = "Hà Nội", Email = "kt@dongdo.vn", RegStatus = RegStatus.Registered, RegisteredAt = DateTime.UtcNow.AddDays(-10) };
            var seller2 = new Nnt { Mst = "0312345678", Name = "Công ty TNHH Miền Nam", Address = "TP.HCM", RegStatus = RegStatus.None };
            db.Nnts.AddRange(seller, seller2); await db.SaveChangesAsync();

            var inv1 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000001", BuyerName = "Nguyễn Văn A", BuyerMst = "8012345678", BuyerAddress = "Hà Nội", Amount = 500_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-5), Status = InvoiceStatus.Accepted, TctCode = "0026082512345678", SentAt = DateTime.UtcNow.AddDays(-5) };
            var inv2 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000002", BuyerName = "Trần Thị B", BuyerAddress = "Hải Phòng", Amount = 30_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-1), Status = InvoiceStatus.Draft };
            db.Invoices.AddRange(inv1, inv2); await db.SaveChangesAsync();
            db.Messages.AddRange(
                new TranMessage { InvoiceId = inv1.Id, NntId = seller.Id, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300", Text = "Gửi HĐ 1C26TAA-00000001", CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new TranMessage { InvoiceId = inv1.Id, NntId = seller.Id, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "202", Text = "TCT cấp mã: 0026082512345678", CreatedAt = DateTime.UtcNow.AddDays(-5) });
            await db.SaveChangesAsync();
        }
    }

    private static async Task MigratePostgresAsync(AppDbContext db)
    {
        if (!db.Database.IsNpgsql()) return;
        var def = TenantContext.DefaultOrgId;
        var tables = new[] { "Nnts", "Invoices", "Messages" };
        var sql = new List<string> {
            "CREATE TABLE IF NOT EXISTS minitvan.\"Orgs\" (\"Id\" uuid PRIMARY KEY, \"Name\" text NOT NULL DEFAULT '', \"ApiKey\" text NOT NULL DEFAULT '', \"CreatedAt\" timestamp NOT NULL DEFAULT now())",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Orgs_ApiKey\" ON minitvan.\"Orgs\" (\"ApiKey\")" };
        foreach (var t in tables) sql.Add($"ALTER TABLE minitvan.\"{t}\" ADD COLUMN IF NOT EXISTS \"OrgId\" uuid NOT NULL DEFAULT '{def}'");
        foreach (var s in sql) try { await db.Database.ExecuteSqlRawAsync(s); } catch { }
    }
}
