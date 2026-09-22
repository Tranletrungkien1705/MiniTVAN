using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniTVAN.Data;
using MiniTVAN.Models;
using MiniTVAN.Services;
using Xunit;

namespace MiniTVAN.Tests;

/// <summary>Test T-VAN: đăng ký NNT, truyền HĐ (NNT chưa ĐK bị chặn), TCT chấp nhận cấp mã / từ chối, hủy, tra cứu.</summary>
public class TvanServiceTests
{
    private static (AppDbContext db, ITvanService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new TvanService(db), conn);
    }

    private static async Task<(int nntId, int invId)> Setup(ITvanService svc, bool register = true, string buyer = "Cty Mua", decimal amount = 10_000_000)
    {
        var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
        if (register) await svc.RegisterNntAsync(nntId);
        var (_, _, invId) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = buyer, Amount = amount });
        return (nntId, invId);
    }

    [Fact]
    public async Task Register_SetsRegistered()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "X" });
            await svc.RegisterNntAsync(nntId);
            Assert.Equal(RegStatus.Registered, (await svc.GetNntAsync(nntId))!.RegStatus);
        }
    }

    [Fact]
    public async Task Transmit_UnregisteredNnt_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc, register: false);
            var (ok, msg) = await svc.TransmitAsync(invId);
            Assert.False(ok);
            Assert.Contains("chưa đăng ký", msg);
        }
    }

    [Fact]
    public async Task Transmit_Valid_Accepted_WithTctCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            var (ok, _) = await svc.TransmitAsync(invId);
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Accepted, inv!.Status);
            Assert.False(string.IsNullOrEmpty(inv.TctCode));
        }
    }

    [Fact]
    public async Task Transmit_MissingBuyer_Rejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc, buyer: "");
            var (ok, _) = await svc.TransmitAsync(invId);
            Assert.False(ok);
            Assert.Equal(InvoiceStatus.Rejected, (await svc.GetInvoiceAsync(invId))!.Status);
        }
    }

    [Fact]
    public async Task Lookup_ByTctCode_AfterAccepted()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var inv = await svc.GetInvoiceAsync(invId);
            var found = await svc.LookupByCodeAsync(inv!.TctCode!);
            Assert.NotNull(found);
            Assert.Equal(invId, found!.Id);
        }
    }

    [Fact]
    public async Task Cancel_OnlyAccepted()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            var (bad, _) = await svc.CancelAsync(invId);   // chưa Accepted
            Assert.False(bad);
            await svc.TransmitAsync(invId);
            var (ok, _) = await svc.CancelAsync(invId);
            Assert.True(ok);
            Assert.Equal(InvoiceStatus.Cancelled, (await svc.GetInvoiceAsync(invId))!.Status);
        }
    }

    [Fact]
    public async Task Invoice_VatAndTotal_Computed()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc, amount: 10_000_000);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(1_000_000, inv!.VatAmount);   // 10%
            Assert.Equal(11_000_000, inv.Total);
        }
    }

    [Fact]
    public async Task Adjust_OnAccepted_CreatesAdjustInvoice()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (ok, _, newId) = await svc.AdjustAsync(invId, InvoiceAdjType.Decrease, 5_000_000, 10, "Giảm giá");
            Assert.True(ok);
            var adj = await svc.GetInvoiceAsync(newId);
            Assert.Equal(SourceInvoiceCode.Adjust, adj!.SourceCode);
            Assert.Equal(InvoiceAdjType.Decrease, adj.AdjType);
            Assert.Equal(invId, adj.RefInvoiceId);
            Assert.Equal(InvoiceStatus.Accepted, adj.Status);
        }
    }

    [Fact]
    public async Task Adjust_OnDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, chưa truyền
            var (ok, msg, _) = await svc.AdjustAsync(invId, InvoiceAdjType.Increase, 1_000_000, 10, "x");
            Assert.False(ok);
            Assert.Contains("chấp nhận", msg);
        }
    }

    [Fact]
    public async Task Adjust_AlreadyAdjusted_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (_, _, adjId) = await svc.AdjustAsync(invId, InvoiceAdjType.Decrease, 5_000_000, 10, "lần 1");
            var (ok, msg, _) = await svc.AdjustAsync(adjId, InvoiceAdjType.Decrease, 1_000_000, 10, "lần 2");
            Assert.False(ok);
            Assert.Contains("điều chỉnh tiếp", msg);
        }
    }

    [Fact]
    public async Task Replace_OnAccepted_CancelsRoot()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (ok, _, newId) = await svc.ReplaceAsync(invId, 20_000_000, 10, "Sai số tiền");
            Assert.True(ok);
            var rep = await svc.GetInvoiceAsync(newId);
            Assert.Equal(SourceInvoiceCode.Replace, rep!.SourceCode);
            Assert.Equal(invId, rep.RefInvoiceId);
            Assert.Equal(InvoiceStatus.Accepted, rep.Status);
            Assert.Equal(InvoiceStatus.Cancelled, (await svc.GetInvoiceAsync(invId))!.Status);   // gốc bị hủy
        }
    }

    [Fact]
    public async Task Replace_OnReplacedRoot_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (_, _, repId) = await svc.ReplaceAsync(invId, 20_000_000, 10, "lần 1");
            var (ok, msg, _) = await svc.ReplaceAsync(repId, 1_000_000, 10, "lần 2");
            Assert.False(ok);
            Assert.Contains("hóa đơn gốc", msg);
        }
    }

    [Fact]
    public async Task License_Increase_FirstTime_CreatesWithQty()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (ok, _, _) = await svc.IncreaseLicenseAsync(nntId, 1000, "cấp ban đầu");
            Assert.True(ok);
            var lic = await svc.GetLicenseAsync(nntId);
            Assert.NotNull(lic);
            Assert.Equal(1000, lic!.TotalQty);
            Assert.Equal(1000, lic.Remaining);
            var hists = await svc.LicenseHistsAsync(nntId);
            Assert.Single(hists);
            Assert.Equal(LicenseHistType.Create, hists[0].Type);
            Assert.Equal(1000, hists[0].TotalQtyAfter);
        }
    }

    [Fact]
    public async Task License_Increase_SecondTime_Accumulates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            await svc.IncreaseLicenseAsync(nntId, 1000, "lần 1");
            var (ok, _, _) = await svc.IncreaseLicenseAsync(nntId, 500, "lần 2");
            Assert.True(ok);
            var lic = await svc.GetLicenseAsync(nntId);
            Assert.Equal(1500, lic!.TotalQty);
            var hists = await svc.LicenseHistsAsync(nntId);
            Assert.Equal(2, hists.Count);
            Assert.Equal(LicenseHistType.Increase, hists[0].Type);   // mới nhất trước
            Assert.Equal(1500, hists[0].TotalQtyAfter);
        }
    }

    [Fact]
    public async Task License_Increase_NonPositiveQty_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (ok, msg, _) = await svc.IncreaseLicenseAsync(nntId, 0, "x");
            Assert.False(ok);
            Assert.Contains("phải > 0", msg);
            Assert.Null(await svc.GetLicenseAsync(nntId));
        }
    }

    [Fact]
    public async Task License_Increase_UnknownNnt_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.IncreaseLicenseAsync(9999, 100, "x");
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task GuiTongHop_Create_GathersAcceptedInvoicesInPeriod()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted, IssuedDate = hôm nay
            var (ok, _, id) = await svc.CreateGuiTongHopAsync(nntId, PeriodType.Month, DateTime.Today.ToString("yyyy-MM"), 0, "tháng này");
            Assert.True(ok);
            var g = await svc.GetGuiTongHopAsync(id);
            Assert.NotNull(g);
            Assert.Single(g!.Details);
            Assert.True(g.LDau);
            Assert.Equal(GthStatus.Draft, g.Status);
            Assert.Equal(11_000_000, g.TotalPayment);   // 10tr + 10% VAT
        }
    }

    [Fact]
    public async Task GuiTongHop_Create_NoAcceptedInPeriod_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);   // HĐ còn Draft, chưa truyền
            var (ok, msg, _) = await svc.CreateGuiTongHopAsync(nntId, PeriodType.Month, DateTime.Today.ToString("yyyy-MM"), 0, null);
            Assert.False(ok);
            Assert.Contains("Không có hóa đơn", msg);
        }
    }

    [Fact]
    public async Task GuiTongHop_Send_Accepted_WithReplyCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (_, _, id) = await svc.CreateGuiTongHopAsync(nntId, PeriodType.Month, DateTime.Today.ToString("yyyy-MM"), 0, null);
            var (ok, _) = await svc.SendGuiTongHopAsync(id);
            Assert.True(ok);
            var g = await svc.GetGuiTongHopAsync(id);
            Assert.Equal(GthStatus.Accepted, g!.Status);
            Assert.Equal("202", g.MessageReplyCode);
        }
    }

    [Fact]
    public async Task GuiTongHop_Send_AlreadyAccepted_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (_, _, id) = await svc.CreateGuiTongHopAsync(nntId, PeriodType.Month, DateTime.Today.ToString("yyyy-MM"), 0, null);
            await svc.SendGuiTongHopAsync(id);
            var (ok, msg) = await svc.SendGuiTongHopAsync(id);
            Assert.False(ok);
            Assert.Contains("đã được CQT chấp nhận", msg);
        }
    }

    [Fact]
    public async Task LookupNnt_ValidMst_ReturnsInfoAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);   // NNT MST 0101243150
            db.TaxOffices.Add(new TaxOffice { GovTaxID = "0101", GovTaxName = "Cục Thuế TP Hà Nội" });
            await db.SaveChangesAsync();
            var (ok, msg, log) = await svc.LookupNntByMstAsync("0101243150");
            Assert.True(ok);
            Assert.NotNull(log);
            Assert.Equal(LookupResult.Success, log!.Result);
            Assert.Equal("Cty Bán", log.FullName);
            Assert.Equal("0101", log.GovTaxID);
            Assert.Equal("Cục Thuế TP Hà Nội", log.GovTaxName);
            var logs = await svc.NntLookupLogsAsync("0101243150");
            Assert.Single(logs);
        }
    }

    [Fact]
    public async Task LookupNnt_InvalidMst_NotFoundAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, log) = await svc.LookupNntByMstAsync("123");
            Assert.False(ok);
            Assert.NotNull(log);
            Assert.Equal(LookupResult.NotFound, log!.Result);
            Assert.Contains("Không tìm thấy", msg);
            Assert.Single(await svc.NntLookupLogsAsync("123"));
        }
    }

    [Fact]
    public async Task LookupNnt_EmptyMst_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, log) = await svc.LookupNntByMstAsync("  ");
            Assert.False(ok);
            Assert.Null(log);
            Assert.Contains("Cần nhập", msg);
        }
    }

    [Fact]
    public async Task SendEmail_OnAccepted_LogsAndStampsInvoice()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted
            var (ok, msg, logId) = await svc.SendInvoiceEmailAsync(invId, "khachhang@congty.vn", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal("khachhang@congty.vn", inv!.EmailSend);
            Assert.NotNull(inv.SendEmailDTimeUTC);
            Assert.Equal("kế toán", inv.SendEmailBy);
            var logs = await svc.EmailLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(logId, logs[0].Id);
            Assert.Equal(EmailSendResult.Success, logs[0].Result);
        }
    }

    [Fact]
    public async Task SendEmail_OnDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, chưa truyền
            var (ok, msg, _) = await svc.SendInvoiceEmailAsync(invId, "a@b.vn", "x");
            Assert.False(ok);
            Assert.Contains("chấp nhận", msg);
            Assert.Empty(await svc.EmailLogsAsync(invId));
        }
    }

    [Fact]
    public async Task SendEmail_InvalidEmail_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (ok, msg, _) = await svc.SendInvoiceEmailAsync(invId, "khong-phai-email", "x");
            Assert.False(ok);
            Assert.Contains("không hợp lệ", msg);
        }
    }

    [Fact]
    public async Task SendEmail_NoRecipient_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (ok, msg, _) = await svc.SendInvoiceEmailAsync(invId, "  ", "x");
            Assert.False(ok);
            Assert.Contains("Cần email", msg);
        }
    }
}
