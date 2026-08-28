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
}
