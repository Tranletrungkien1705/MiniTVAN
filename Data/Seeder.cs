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
            // HĐ điều chỉnh giảm cho HĐ gốc inv1 (minh họa xử lý sai sót)
            var inv3 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000003", BuyerName = "Nguyễn Văn A", BuyerMst = "8012345678", BuyerAddress = "Hà Nội", Amount = 5_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-2), Status = InvoiceStatus.Accepted, TctCode = "0026082612345679", SentAt = DateTime.UtcNow.AddDays(-2), SourceCode = SourceInvoiceCode.Adjust, AdjType = InvoiceAdjType.Decrease, RefInvoiceId = inv1.Id, RefTctCode = inv1.TctCode, AdjReason = "Giảm giá theo phụ lục hợp đồng" };
            db.Invoices.Add(inv3); await db.SaveChangesAsync();
            // Gửi email hóa đơn cho người mua (theo Invoice_Invoice_Support_SendMail của TVAN gốc):
            // inv1 đã phát hành → đã gửi email cho người mua.
            inv1.EmailSend = "nguyenvana@congty.vn";
            inv1.SendEmailDTimeUTC = DateTime.UtcNow.AddDays(-5);
            inv1.SendEmailBy = "kế toán";
            db.InvoiceEmailLogs.Add(new InvoiceEmailLog
            {
                InvoiceId = inv1.Id, ToEmail = "nguyenvana@congty.vn",
                Subject = $"Hóa đơn điện tử {inv1.Symbol}-{inv1.No} — {seller.Name}",
                Result = EmailSendResult.Success, SentBy = "kế toán",
                Message = "Đã gửi email hóa đơn tới nguyenvana@congty.vn.", CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            await db.SaveChangesAsync();
            // Xóa hóa đơn đã phát hành (theo Invoice_Invoice_Deleted của TVAN gốc):
            // inv4 đã phát hành nhưng bị xóa (DELETED) kèm lý do & người xóa.
            var inv4 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000004", BuyerName = "Lê Văn C", BuyerAddress = "Đà Nẵng", Amount = 8_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-3), Status = InvoiceStatus.Deleted, TctCode = "0026082712345680", SentAt = DateTime.UtcNow.AddDays(-3), DeleteDTimeUTC = DateTime.UtcNow.AddDays(-2), DeleteBy = "kế toán", Remark = "Lập sai thông tin người mua" };
            db.Invoices.Add(inv4); await db.SaveChangesAsync();
            // In chuyển đổi hóa đơn (theo Invoice_Invoice.FlagChange của TVAN gốc):
            // inv1 đã được in ở dạng chuyển đổi cho người mua.
            inv1.FlagChange = ConversionPrintFlag.Printed;
            db.ConversionPrintLogs.Add(new ConversionPrintLog
            {
                InvoiceId = inv1.Id, Action = ConversionPrintAction.Print, By = "kế toán",
                Note = "In bản chuyển đổi giao khách", CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            await db.SaveChangesAsync();
            db.Messages.AddRange(
                new TranMessage { InvoiceId = inv1.Id, NntId = seller.Id, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300", Text = "Gửi HĐ 1C26TAA-00000001", CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new TranMessage { InvoiceId = inv1.Id, NntId = seller.Id, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "202", Text = "TCT cấp mã: 0026082512345678", CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new TranMessage { InvoiceId = inv3.Id, NntId = seller.Id, Type = MsgType.AdjustInvoice, Dir = MsgDir.Out, Code = "300", Text = "Gửi HĐ điều chỉnh giảm cho 1C26TAA-00000001", CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new TranMessage { InvoiceId = inv3.Id, NntId = seller.Id, Type = MsgType.AdjustInvoice, Dir = MsgDir.In, Code = "202", Text = "TCT cấp mã: 0026082612345679", CreatedAt = DateTime.UtcNow.AddDays(-2) });
            await db.SaveChangesAsync();

            // Hạn mức hóa đơn (theo Invoice_license của TVAN gốc): seller đã được cấp 1000 HĐ, đã phát hành 3.
            var lic = new InvoiceLicense { NntId = seller.Id, TotalQty = 1000, TotalQtyIssued = 3, TotalQtyUsed = 2, UpdatedAt = DateTime.UtcNow.AddDays(-2) };
            db.Licenses.Add(lic); await db.SaveChangesAsync();
            db.LicenseHists.AddRange(
                new LicenseHist { NntId = seller.Id, Type = LicenseHistType.Create, Qty = 1000, TotalQtyAfter = 1000, Note = "Cấp hạn mức ban đầu", CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new LicenseHist { NntId = seller.Id, Type = LicenseHistType.Increase, Qty = 0, TotalQtyAfter = 1000, Note = "Khởi tạo demo", CreatedAt = DateTime.UtcNow.AddDays(-2) });
            await db.SaveChangesAsync();

            // Bảng tổng hợp dữ liệu HĐĐT gửi CQT (theo Mst_GuiTongHop của TVAN gốc):
            // gom các HĐ đã được CQT chấp nhận của seller trong kỳ tháng hiện tại.
            var gth = new GuiTongHop
            {
                NntId = seller.Id, LKDLieu = PeriodType.Month, KDLieu = DateTime.Today.ToString("yyyy-MM"),
                BSLThu = 0, LDau = true, TNNT = seller.Name, MST = seller.Mst, NLap = DateTime.Today,
                SBTHDLieu = $"{seller.Mst}-{DateTime.Today:yyyy-MM}-0", Status = GthStatus.Draft
            };
            int stt = 1;
            foreach (var i in new[] { inv1, inv3 })
            {
                gth.Details.Add(new GuiTongHopDtl
                {
                    STT = stt++, InvoiceCode = i.TctCode ?? "", KHMSHDon = "01GTKT", KHHDon = i.Symbol, SHDon = i.No,
                    NLap = i.IssuedDate, TNMua = i.BuyerName, MSTNMua = i.BuyerMst, THHDVu = "Hàng hóa, dịch vụ",
                    DVTinh = "Lần", SLuong = 1, TTCThue = i.Amount, TSuat = i.VatRate, TgTThue = i.VatAmount, TgTTToan = i.Total
                });
            }
            db.GuiTongHops.Add(gth); await db.SaveChangesAsync();
        }

        // Danh mục cơ quan thuế (theo Mst_GovTaxID của TVAN gốc) + nhật ký tra cứu MST demo.
        if (!await db.TaxOffices.AnyAsync())
        {
            db.TaxOffices.AddRange(
                new TaxOffice { GovTaxID = "0101", GovTaxName = "Cục Thuế TP Hà Nội", Address = "Hà Nội", ContactEmail = "hanoi@gdt.gov.vn", ContactPhone = "024 3825 0000" },
                new TaxOffice { GovTaxID = "0301", GovTaxName = "Cục Thuế TP Hồ Chí Minh", Address = "TP.HCM", ContactEmail = "hcm@gdt.gov.vn", ContactPhone = "028 3829 0000" });
            await db.SaveChangesAsync();
        }
        if (!await db.NntLookupLogs.AnyAsync())
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            db.NntLookupLogs.Add(new NntLookupLog
            {
                Mst = "0101243150", Result = LookupResult.Success,
                FullName = seller?.Name ?? "Công ty CP Ô tô Đông Đô", Address = seller?.Address,
                GovTaxID = "0101", GovTaxName = "Cục Thuế TP Hà Nội",
                Message = "Tra cứu thành công từ cơ quan thuế.", CreatedAt = DateTime.UtcNow.AddDays(-3)
            });
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
