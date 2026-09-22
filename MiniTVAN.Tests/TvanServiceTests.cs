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
}
