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

    [Fact]
    public async Task ResetToPending_OnAccepted_ClearsTctCodeAndKeepsNo()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted, có TctCode
            var before = await svc.GetInvoiceAsync(invId);
            var no = before!.No;
            var (ok, msg) = await svc.ResetToPendingAsync(invId, "sửa sai sót");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Draft, inv!.Status);
            Assert.Null(inv.TctCode);
            Assert.Null(inv.SentAt);
            Assert.Equal(no, inv.No);   // giữ nguyên số
        }
    }

    [Fact]
    public async Task ResetToPending_OnRejected_ClearsRejectReason()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc, buyer: "");   // thiếu người mua → bị từ chối
            await svc.TransmitAsync(invId);
            Assert.Equal(InvoiceStatus.Rejected, (await svc.GetInvoiceAsync(invId))!.Status);
            var (ok, _) = await svc.ResetToPendingAsync(invId, null);
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Draft, inv!.Status);
            Assert.Null(inv.RejectReason);
        }
    }

    [Fact]
    public async Task ResetToPending_OnDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft
            var (ok, msg) = await svc.ResetToPendingAsync(invId, null);
            Assert.False(ok);
            Assert.Contains("trạng thái chờ", msg);
        }
    }

    [Fact]
    public async Task ResetToPending_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.ResetToPendingAsync(9999, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task Restore_OnCancelled_BackToAccepted_KeepsTctCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted, có TctCode
            var code = (await svc.GetInvoiceAsync(invId))!.TctCode;
            await svc.CancelAsync(invId);     // Cancelled (DELETED)
            var (ok, msg) = await svc.RestoreInvoiceAsync(invId, "làm thông báo sai sót");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Accepted, inv!.Status);
            Assert.Equal(code, inv.TctCode);   // giữ nguyên mã tra cứu
        }
    }

    [Fact]
    public async Task Restore_OnAccepted_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted, chưa hủy
            var (ok, msg) = await svc.RestoreInvoiceAsync(invId, null);
            Assert.False(ok);
            Assert.Contains("đã hủy", msg);
        }
    }

    [Fact]
    public async Task Restore_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.RestoreInvoiceAsync(9999, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task DeleteAdjustReplace_OnDraftAdjust_Deletes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // HĐ gốc Accepted
            // Tạo HĐ điều chỉnh nhưng KHÔNG truyền (giữ Draft) để xóa
            var root = await svc.GetInvoiceAsync(invId);
            var adj = new Invoice
            {
                NntId = root!.NntId, Symbol = root.Symbol, No = "00000099",
                BuyerName = root.BuyerName, Amount = 1_000_000, VatRate = 10,
                Status = InvoiceStatus.Draft, SourceCode = SourceInvoiceCode.Adjust,
                AdjType = InvoiceAdjType.Decrease, RefInvoiceId = root.Id, RefTctCode = root.TctCode
            };
            db.Invoices.Add(adj); await db.SaveChangesAsync();
            var (ok, msg) = await svc.DeleteAdjustReplaceAsync(adj.Id, "lập sai");
            Assert.True(ok);
            Assert.Contains("Đã xóa", msg);
            Assert.Null(await svc.GetInvoiceAsync(adj.Id));
        }
    }

    [Fact]
    public async Task DeleteAdjustReplace_OnRootInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // HĐ gốc Draft, SourceCode = Root
            var (ok, msg) = await svc.DeleteAdjustReplaceAsync(invId, null);
            Assert.False(ok);
            Assert.Contains("điều chỉnh hoặc thay thế", msg);
        }
    }

    [Fact]
    public async Task DeleteAdjustReplace_OnAcceptedAdjust_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (_, _, adjId) = await svc.AdjustAsync(invId, InvoiceAdjType.Decrease, 5_000_000, 10, "giảm giá");   // Accepted
            var (ok, msg) = await svc.DeleteAdjustReplaceAsync(adjId, null);
            Assert.False(ok);
            Assert.Contains("chưa phát hành", msg);
        }
    }

    [Fact]
    public async Task DeleteAdjustReplace_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.DeleteAdjustReplaceAsync(9999, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task ConversionPrint_OnAccepted_MarksPrintedAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted
            var (ok, msg) = await svc.MarkConversionPrintedAsync(invId, "in bản chuyển đổi", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(ConversionPrintFlag.Printed, inv!.FlagChange);
            var logs = await svc.ConversionPrintLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(ConversionPrintAction.Print, logs[0].Action);
            Assert.Equal("kế toán", logs[0].By);
        }
    }

    [Fact]
    public async Task ConversionPrint_OnDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, chưa truyền
            var (ok, msg) = await svc.MarkConversionPrintedAsync(invId, null, null);
            Assert.False(ok);
            Assert.Contains("chấp nhận", msg);
            Assert.Empty(await svc.ConversionPrintLogsAsync(invId));
        }
    }

    [Fact]
    public async Task ConversionPrint_AlreadyPrinted_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            await svc.MarkConversionPrintedAsync(invId, null, null);
            var (ok, msg) = await svc.MarkConversionPrintedAsync(invId, null, null);
            Assert.False(ok);
            Assert.Contains("đã được in chuyển đổi", msg);
        }
    }

    [Fact]
    public async Task ResetConversionPrint_AfterPrinted_BackToNotPrinted()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            await svc.MarkConversionPrintedAsync(invId, null, null);
            var (ok, msg) = await svc.ResetConversionPrintAsync(invId, "in nhầm", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(ConversionPrintFlag.NotPrinted, inv!.FlagChange);
            var logs = await svc.ConversionPrintLogsAsync(invId);
            Assert.Equal(2, logs.Count);
            Assert.Equal(ConversionPrintAction.Reset, logs[0].Action);   // mới nhất trước
        }
    }

    [Fact]
    public async Task ResetConversionPrint_WhenNotPrinted_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted nhưng chưa in chuyển đổi
            var (ok, msg) = await svc.ResetConversionPrintAsync(invId, null, null);
            Assert.False(ok);
            Assert.Contains("chưa in chuyển đổi", msg);
        }
    }

    [Fact]
    public async Task ConversionPrint_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.MarkConversionPrintedAsync(9999, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task Delete_OnAccepted_SetsDeletedWithTracking()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted (ISSUED)
            var (ok, msg) = await svc.DeleteInvoiceAsync(invId, "lập sai", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Deleted, inv!.Status);
            Assert.NotNull(inv.DeleteDTimeUTC);
            Assert.Equal("kế toán", inv.DeleteBy);
            Assert.Equal("lập sai", inv.Remark);
        }
    }

    [Fact]
    public async Task Delete_OnDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, chưa truyền
            var (ok, msg) = await svc.DeleteInvoiceAsync(invId, null, null);
            Assert.False(ok);
            Assert.Contains("đã phát hành", msg);
        }
    }

    [Fact]
    public async Task Delete_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.DeleteInvoiceAsync(9999, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task Sign60Day_Default_IsCheck()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var s = await svc.GetSettingAsync();
            Assert.Equal(Sign60DayFlag.Check, s.Sign60Day);
        }
    }

    [Fact]
    public async Task Transmit_SignedOver60Days_BlockedWhenCheck()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            var inv = await svc.GetInvoiceAsync(invId);
            inv!.SignedDate = DateTime.UtcNow.AddDays(-70);   // ký quá 60 ngày
            await db.SaveChangesAsync();
            var (ok, msg) = await svc.TransmitAsync(invId);
            Assert.False(ok);
            Assert.Contains("60 ngày", msg);
            Assert.Equal(InvoiceStatus.Rejected, (await svc.GetInvoiceAsync(invId))!.Status);
        }
    }

    [Fact]
    public async Task Transmit_SignedOver60Days_AllowedWhenUncheck()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            var inv = await svc.GetInvoiceAsync(invId);
            inv!.SignedDate = DateTime.UtcNow.AddDays(-70);
            await db.SaveChangesAsync();
            await svc.SetSign60DayAsync(Sign60DayFlag.Uncheck, "bỏ check");
            var (ok, _) = await svc.TransmitAsync(invId);
            Assert.True(ok);
            Assert.Equal(InvoiceStatus.Accepted, (await svc.GetInvoiceAsync(invId))!.Status);
        }
    }

    [Fact]
    public async Task Transmit_SignedWithin60Days_AllowedWhenCheck()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            var inv = await svc.GetInvoiceAsync(invId);
            inv!.SignedDate = DateTime.UtcNow.AddDays(-10);   // trong hạn
            await db.SaveChangesAsync();
            var (ok, _) = await svc.TransmitAsync(invId);
            Assert.True(ok);
            Assert.Equal(InvoiceStatus.Accepted, (await svc.GetInvoiceAsync(invId))!.Status);
        }
    }

    [Fact]
    public async Task SetSign60Day_UpdatesSetting()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.SetSign60DayAsync(Sign60DayFlag.Uncheck, "theo yêu cầu");
            Assert.True(ok);
            Assert.Contains("bỏ kiểm tra", msg);
            var s = await svc.GetSettingAsync();
            Assert.Equal(Sign60DayFlag.Uncheck, s.Sign60Day);
            Assert.Equal("theo yêu cầu", s.Note);
        }
    }

    [Fact]
    public async Task ReSign_OnAccepted_StampsHotfixAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted (ISSUED)
            var (ok, msg) = await svc.ReSignAsync(invId, "PD94bWwgdmVyc2lvbj0iMS4wIj8+", "lỗi chữ ký", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(HotfixFlag.Hotfixed, inv!.FlagHotfix);
            Assert.Equal("PD94bWwgdmVyc2lvbj0iMS4wIj8+", inv.InvoiceFileSpec);
            Assert.False(string.IsNullOrWhiteSpace(inv.InvoiceFilePath));
            Assert.NotNull(inv.ApprDTimeUTC);
            Assert.Equal("kế toán", inv.ApprBy);
            var logs = await svc.ReSignLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal("kế toán", logs[0].By);
        }
    }

    [Fact]
    public async Task ReSign_OnDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, chưa truyền
            var (ok, msg) = await svc.ReSignAsync(invId, "abc", null, null);
            Assert.False(ok);
            Assert.Contains("đã phát hành", msg);
            Assert.Empty(await svc.ReSignLogsAsync(invId));
        }
    }

    [Fact]
    public async Task ReSign_AlreadyHotfixed_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            await svc.ReSignAsync(invId, "abc", null, null);
            var (ok, msg) = await svc.ReSignAsync(invId, "def", null, null);
            Assert.False(ok);
            Assert.Contains("đã được ký lại", msg);
        }
    }

    [Fact]
    public async Task ReSign_EmptyFileSpec_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (ok, msg) = await svc.ReSignAsync(invId, "  ", null, null);
            Assert.False(ok);
            Assert.Contains("Cần nội dung", msg);
        }
    }

    [Fact]
    public async Task ReSign_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.ReSignAsync(9999, "abc", null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task Approve_OnDraft_SetsApprovedAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft (PENDING)
            var (ok, msg) = await svc.ApproveAsync(invId, "2026-06-12/HD0001.xml", "2026-06-12/HD0001.pdf", "duyệt phát hành", "kế toán trưởng");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Approved, inv!.Status);
            Assert.Equal("2026-06-12/HD0001.xml", inv.InvoiceFilePath);
            Assert.Equal("2026-06-12/HD0001.pdf", inv.InvoicePDFFilePath);
            Assert.NotNull(inv.ApprDTimeUTC);
            Assert.Equal("kế toán trưởng", inv.ApprBy);
            var logs = await svc.ApproveLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(ApproveAction.Approve, logs[0].Action);
            Assert.Equal("kế toán trưởng", logs[0].By);
        }
    }

    [Fact]
    public async Task Approve_OnAccepted_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted, không còn ở trạng thái chờ
            var (ok, msg) = await svc.ApproveAsync(invId, null, null, null, null);
            Assert.False(ok);
            Assert.Contains("trạng thái chờ", msg);
            Assert.Empty(await svc.ApproveLogsAsync(invId));
        }
    }

    [Fact]
    public async Task Approve_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.ApproveAsync(9999, null, null, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task Unapprove_AfterApproved_BackToDraftAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.ApproveAsync(invId, "a.xml", "a.pdf", null, "kế toán");
            var (ok, msg) = await svc.UnapproveAsync(invId, "cần sửa lại", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Draft, inv!.Status);
            Assert.Null(inv.InvoiceFilePath);
            Assert.Null(inv.InvoicePDFFilePath);
            Assert.Null(inv.ApprDTimeUTC);
            Assert.Null(inv.ApprBy);
            var logs = await svc.ApproveLogsAsync(invId);
            Assert.Equal(2, logs.Count);
            Assert.Equal(ApproveAction.Unapprove, logs[0].Action);   // mới nhất trước
        }
    }

    [Fact]
    public async Task Unapprove_OnDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, chưa duyệt
            var (ok, msg) = await svc.UnapproveAsync(invId, null, null);
            Assert.False(ok);
            Assert.Contains("đã duyệt", msg);
        }
    }
}
