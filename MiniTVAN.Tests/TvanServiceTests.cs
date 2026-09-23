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

    // Duyệt NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_ApprovedMulti của TVAN gốc).
    [Fact]
    public async Task BulkApprove_MultipleDrafts_AllApprovedAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.RegisterNntAsync(nntId);
            var (_, _, inv1) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 10_000_000 });
            var (_, _, inv2) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 20_000_000 });
            var (ok, msg, count) = await svc.BulkApproveAsync(new List<int> { inv1, inv2 }, "duyệt lô", "kế toán trưởng");
            Assert.True(ok);
            Assert.Equal(2, count);
            Assert.Equal(InvoiceStatus.Approved, (await svc.GetInvoiceAsync(inv1))!.Status);
            Assert.Equal(InvoiceStatus.Approved, (await svc.GetInvoiceAsync(inv2))!.Status);
            Assert.Single(await svc.ApproveLogsAsync(inv1));
            Assert.Single(await svc.ApproveLogsAsync(inv2));
            var logs = await svc.BulkApproveLogsAsync();
            Assert.Single(logs);
            Assert.Equal(2, logs[0].ApprovedCount);
            Assert.Equal("kế toán trưởng", logs[0].By);
        }
    }

    [Fact]
    public async Task BulkApprove_EmptyList_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, count) = await svc.BulkApproveAsync(new List<int>(), null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Contains("ít nhất một", msg);
        }
    }

    [Fact]
    public async Task BulkApprove_OneNotDraft_NoneApproved()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.RegisterNntAsync(nntId);
            var (_, _, inv1) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 10_000_000 });
            var (_, _, inv2) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 20_000_000 });
            await svc.TransmitAsync(inv2);   // inv2 → Accepted, không còn ở trạng thái chờ
            var (ok, msg, count) = await svc.BulkApproveAsync(new List<int> { inv1, inv2 }, null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Equal(InvoiceStatus.Draft, (await svc.GetInvoiceAsync(inv1))!.Status);   // all-or-nothing
            Assert.Empty(await svc.ApproveLogsAsync(inv1));
            Assert.Empty(await svc.BulkApproveLogsAsync());
        }
    }

    [Fact]
    public async Task BulkApprove_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, inv1) = await Setup(svc);
            var (ok, msg, count) = await svc.BulkApproveAsync(new List<int> { inv1, 9999 }, null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Contains("không tồn tại", msg);
        }
    }

    // Xóa NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_DeleteMulti của TVAN gốc).
    [Fact]
    public async Task BulkDelete_MultipleIssued_AllDeletedAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.RegisterNntAsync(nntId);
            var (_, _, inv1) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 10_000_000 });
            var (_, _, inv2) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 20_000_000 });
            await svc.TransmitAsync(inv1);   // inv1 → Accepted (ISSUED)
            await svc.TransmitAsync(inv2);   // inv2 → Accepted (ISSUED)
            var (ok, msg, count) = await svc.BulkDeleteAsync(new List<int> { inv1, inv2 }, "xóa lô lập sai", "kế toán trưởng");
            Assert.True(ok);
            Assert.Equal(2, count);
            Assert.Equal(InvoiceStatus.Deleted, (await svc.GetInvoiceAsync(inv1))!.Status);
            Assert.Equal(InvoiceStatus.Deleted, (await svc.GetInvoiceAsync(inv2))!.Status);
            var logs = await svc.BulkDeleteLogsAsync();
            Assert.Single(logs);
            Assert.Equal(2, logs[0].DeletedCount);
            Assert.Equal("kế toán trưởng", logs[0].By);
        }
    }

    [Fact]
    public async Task BulkDelete_EmptyList_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, count) = await svc.BulkDeleteAsync(new List<int>(), null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Contains("ít nhất một", msg);
        }
    }

    [Fact]
    public async Task BulkDelete_OneNotIssued_NoneDeleted()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.RegisterNntAsync(nntId);
            var (_, _, inv1) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 10_000_000 });
            var (_, _, inv2) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 20_000_000 });
            await svc.TransmitAsync(inv1);   // inv1 → Accepted (ISSUED)
            // inv2 vẫn ở trạng thái Draft → không hợp lệ
            var (ok, msg, count) = await svc.BulkDeleteAsync(new List<int> { inv1, inv2 }, null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Equal(InvoiceStatus.Accepted, (await svc.GetInvoiceAsync(inv1))!.Status);   // all-or-nothing
            Assert.Empty(await svc.BulkDeleteLogsAsync());
        }
    }

    [Fact]
    public async Task BulkDelete_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, inv1) = await Setup(svc);
            var (ok, msg, count) = await svc.BulkDeleteAsync(new List<int> { inv1, 9999 }, null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Contains("không tồn tại", msg);
        }
    }

    // Cấp phát số hóa đơn (theo Invoice_Invoice_AllocatedInv của TVAN gốc).
    // Tạo HĐ nháp CHƯA có số (CreateInvoiceAsync tự cấp số nên tạo trực tiếp qua DbContext).
    private static async Task<(int nntId, int invId)> SetupNoNo(AppDbContext db, ITvanService svc)
    {
        var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
        await svc.RegisterNntAsync(nntId);
        var inv = new Invoice { NntId = nntId, Symbol = "1C26TAA", No = "", BuyerName = "Cty Mua", Amount = 10_000_000, Status = InvoiceStatus.Draft };
        db.Invoices.Add(inv); await db.SaveChangesAsync();
        return (nntId, inv.Id);
    }

    private static async Task<int> AddTemplate(AppDbContext db, int nntId, InvoiceNoRule rule = InvoiceNoRule.TT78, string? lastNo = null, int qtyUsed = 0)
    {
        var tpl = new InvoiceTemplate
        {
            NntId = nntId, TInvoiceCode = "TINV-1C26TAA", TInvoiceName = "Mẫu 1C26TAA",
            FormNo = "1C26TAA", Sign = "K26TAA", TTType = rule,
            EffDateStart = DateTime.Today.AddDays(-30), StartInvoiceNo = 1, EndInvoiceNo = 1000,
            LastInvoiceNo = lastNo, QtyUsed = qtyUsed, TInvoiceStatus = TemplateStatus.Issued, FlagActive = true
        };
        db.InvoiceTemplates.Add(tpl); await db.SaveChangesAsync();
        return tpl.Id;
    }

    [Fact]
    public async Task AllocateNo_OnDraft_AssignsNextNoAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);
            await AddTemplate(db, nntId, InvoiceNoRule.TT78, lastNo: "00000003", qtyUsed: 3);
            var (ok, msg, no) = await svc.AllocateInvoiceNoAsync(invId, DateTime.Today, "kế toán");
            Assert.True(ok);
            Assert.Equal("00000004", no);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal("00000004", inv!.No);
            Assert.Equal("1C26TAA", inv.Symbol);
            Assert.NotNull(inv.InvoiceNoDTimeUTC);
            Assert.Equal("kế toán", inv.InvoiceNoBy);
            var logs = await svc.AllocLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal("00000004", logs[0].InvoiceNo);
        }
    }

    [Fact]
    public async Task AllocateNo_AlreadyHasNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);
            await AddTemplate(db, nntId);
            await svc.AllocateInvoiceNoAsync(invId, DateTime.Today, null);
            var (ok, msg, _) = await svc.AllocateInvoiceNoAsync(invId, DateTime.Today, null);
            Assert.False(ok);
            Assert.Contains("đã có số", msg);
        }
    }

    [Fact]
    public async Task AllocateNo_NoTemplate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await SetupNoNo(db, svc);   // không có mẫu
            var (ok, msg, _) = await svc.AllocateInvoiceNoAsync(invId, DateTime.Today, null);
            Assert.False(ok);
            Assert.Contains("mẫu hóa đơn", msg);
        }
    }

    [Fact]
    public async Task AllocateNo_FutureDate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);
            await AddTemplate(db, nntId);
            var (ok, msg, _) = await svc.AllocateInvoiceNoAsync(invId, DateTime.Today.AddDays(1), null);
            Assert.False(ok);
            Assert.Contains("tương lai", msg);
        }
    }

    [Fact]
    public async Task AllocateNo_DateBeforeLastAllocated_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);
            var tplId = await AddTemplate(db, nntId);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            tpl.LastInvoiceDateUTC = DateTime.Today;
            await db.SaveChangesAsync();
            var (ok, msg, _) = await svc.AllocateInvoiceNoAsync(invId, DateTime.Today.AddDays(-1), null);
            Assert.False(ok);
            Assert.Contains("trước ngày cấp số gần nhất", msg);
        }
    }

    [Fact]
    public async Task AllocateNo_TT68_UsesStartPlusQtyUsed()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);
            await AddTemplate(db, nntId, InvoiceNoRule.TT68, qtyUsed: 5);
            var (ok, _, no) = await svc.AllocateInvoiceNoAsync(invId, DateTime.Today, null);
            Assert.True(ok);
            Assert.Equal("00000006", no);   // StartInvoiceNo(1) + QtyUsed(5)
        }
    }

    // Phát hành / ngừng mẫu hóa đơn (theo Invoice_TempInvoice_Issued / Invoice_TempInvoice_InActive của TVAN gốc).
    private static async Task<int> AddDraftTemplate(AppDbContext db, int nntId, int startNo = 1, int endNo = 500)
    {
        var tpl = new InvoiceTemplate
        {
            NntId = nntId, TInvoiceCode = "TINV-1C26TAB", TInvoiceName = "Mẫu 1C26TAB",
            FormNo = "1C26TAB", Sign = "K26TAB", TTType = InvoiceNoRule.TT78,
            EffDateStart = DateTime.Today, StartInvoiceNo = startNo, EndInvoiceNo = endNo,
            QtyUsed = 0, TInvoiceStatus = TemplateStatus.Draft, FlagActive = true
        };
        db.InvoiceTemplates.Add(tpl); await db.SaveChangesAsync();
        return tpl.Id;
    }

    [Fact]
    public async Task IssueTemplate_Draft_BecomesIssued()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId);
            var (ok, msg) = await svc.IssueTemplateAsync(tplId, DateTime.Today, "Phát hành theo thông báo TCT");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal(TemplateStatus.Issued, tpl.TInvoiceStatus);
            Assert.Equal(DateTime.Today, tpl.EffDateStart.Date);
            Assert.Null(tpl.EffDateEnd);
        }
    }

    [Fact]
    public async Task IssueTemplate_AlreadyIssued_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);   // đã Issued
            var (ok, msg) = await svc.IssueTemplateAsync(tplId, DateTime.Today, null);
            Assert.False(ok);
            Assert.Contains("trạng thái chờ", msg);
        }
    }

    [Fact]
    public async Task IssueTemplate_EffDateBeforeToday_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId);
            var (ok, msg) = await svc.IssueTemplateAsync(tplId, DateTime.Today.AddDays(-1), null);
            Assert.False(ok);
            Assert.Contains("trước ngày hiện tại", msg);
        }
    }

    [Fact]
    public async Task IssueTemplate_InvalidRange_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId, startNo: 0, endNo: 0);
            var (ok, msg) = await svc.IssueTemplateAsync(tplId, DateTime.Today, null);
            Assert.False(ok);
            Assert.Contains("dải số", msg);
        }
    }

    [Fact]
    public async Task InactivateTemplate_Issued_BecomesInactive()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);   // đã Issued
            var (ok, msg) = await svc.InactivateTemplateAsync(tplId, "Hết hiệu lực");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal(TemplateStatus.Inactive, tpl.TInvoiceStatus);
            Assert.False(tpl.FlagActive);
            Assert.NotNull(tpl.EffDateEnd);
        }
    }

    [Fact]
    public async Task InactivateTemplate_Draft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId);
            var (ok, msg) = await svc.InactivateTemplateAsync(tplId, null);
            Assert.False(ok);
            Assert.Contains("đang sử dụng", msg);
        }
    }

    // Hủy mẫu hóa đơn (theo Invoice_TempInvoice_Cancel của TVAN gốc).
    [Fact]
    public async Task CancelTemplate_Issued_BecomesCancel()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId, qtyUsed: 20);   // đã Issued, dải 1..1000
            var (ok, msg) = await svc.CancelTemplateAsync(tplId, "Hủy theo đề nghị NNT", "kế toán");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal(TemplateStatus.Cancel, tpl.TInvoiceStatus);
            Assert.False(tpl.FlagActive);
            Assert.Equal(980, tpl.QtyCancel);   // EndInvoiceNo(1000) - QtyUsed(20)
            Assert.NotNull(tpl.CancelDTimeUTC);
            Assert.Equal("kế toán", tpl.CancelBy);
        }
    }

    [Fact]
    public async Task CancelTemplate_Draft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId);
            var (ok, msg) = await svc.CancelTemplateAsync(tplId, null, null);
            Assert.False(ok);
            Assert.Contains("đang sử dụng", msg);
        }
    }

    [Fact]
    public async Task CancelTemplate_AlreadyCancelled_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);
            var (first, _) = await svc.CancelTemplateAsync(tplId, null, null);
            Assert.True(first);
            var (ok, msg) = await svc.CancelTemplateAsync(tplId, null, null);
            Assert.False(ok);
            Assert.Contains("đang sử dụng", msg);
        }
    }

    // Nhận kết quả phản hồi từ CQT (theo Invoice_Invoice_TCTReceive của TVAN gốc).
    // Đưa HĐ về trạng thái Sent (chờ phản hồi) để mô phỏng luồng bất đồng bộ.
    private static async Task<int> SetupSent(AppDbContext db, ITvanService svc)
    {
        var (_, invId) = await Setup(svc);
        var inv = await db.Invoices.FirstAsync(i => i.Id == invId);
        inv.Status = InvoiceStatus.Sent;
        inv.SentAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return invId;
    }

    [Fact]
    public async Task TctReceive_202_AcceptsWithCodeAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupSent(db, svc);
            var (ok, msg) = await svc.ReceiveTctResultAsync(invId, TctMessageType.Success202, "0026082512345678", null, null);
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Accepted, inv!.Status);
            Assert.Equal("0026082512345678", inv.TctCode);
            Assert.Equal(TctAcceptStatus.Accept, inv.TctChapNhan);
            Assert.Equal("202", inv.MltDiep);
            Assert.NotNull(inv.TctReceiveDTimeUTC);
            var logs = await svc.TctReceiveLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(TctMessageType.Success202, logs[0].MltDiep);
            Assert.Equal("0026082512345678", logs[0].MaCQT);
        }
    }

    [Fact]
    public async Task TctReceive_204_RejectsWithReasonAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupSent(db, svc);
            var (ok, msg) = await svc.ReceiveTctResultAsync(invId, TctMessageType.Fail204, null, "1001", "Sai định dạng MST người mua");
            Assert.False(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Rejected, inv!.Status);
            Assert.Equal(TctAcceptStatus.Reject, inv.TctChapNhan);
            Assert.Equal("1001", inv.TctMaLoi);
            Assert.Equal("Sai định dạng MST người mua", inv.RejectReason);
            var logs = await svc.TctReceiveLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(TctMessageType.Fail204, logs[0].MltDiep);
            Assert.Equal("1001", logs[0].MaLoi);
        }
    }

    [Fact]
    public async Task TctReceive_202_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupSent(db, svc);
            var (ok, msg) = await svc.ReceiveTctResultAsync(invId, TctMessageType.Success202, "  ", null, null);
            Assert.False(ok);
            Assert.Contains("thiếu mã xác thực", msg);
            Assert.Empty(await svc.TctReceiveLogsAsync(invId));
        }
    }

    [Fact]
    public async Task TctReceive_NotSent_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, chưa truyền
            var (ok, msg) = await svc.ReceiveTctResultAsync(invId, TctMessageType.Success202, "0026082512345678", null, null);
            Assert.False(ok);
            Assert.Contains("chờ phản hồi", msg);
        }
    }

    [Fact]
    public async Task TctReceive_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.ReceiveTctResultAsync(9999, TctMessageType.Success202, "x", null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // Cập nhật nội dung hóa đơn sau khi đã cấp số (theo Invoice_Invoice_UpdAfterAllocated của TVAN gốc).
    // Tạo HĐ nháp ĐÃ có số (Draft + No) để mô phỏng trạng thái sau khi cấp số.
    private static async Task<(int nntId, int invId)> SetupAllocated(AppDbContext db, ITvanService svc, string no = "00000002", DateTime? date = null)
    {
        var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
        await svc.RegisterNntAsync(nntId);
        var inv = new Invoice
        {
            NntId = nntId, Symbol = "1C26TAA", No = no, BuyerName = "Cty Mua", Amount = 10_000_000,
            VatRate = 10, IssuedDate = date ?? DateTime.Today, Status = InvoiceStatus.Draft
        };
        db.Invoices.Add(inv); await db.SaveChangesAsync();
        return (nntId, inv.Id);
    }

    [Fact]
    public async Task UpdateAfterAllocated_UpdatesContentAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await SetupAllocated(db, svc);
            var (ok, msg) = await svc.UpdateAfterAllocatedAsync(invId, "Cty Mua Mới", "8012345678", "Hà Nội",
                PaymentMethod.Transfer, 20_000_000, 8, DateTime.Today, "sửa sai", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal("Cty Mua Mới", inv!.BuyerName);
            Assert.Equal("8012345678", inv.BuyerMst);
            Assert.Equal(PaymentMethod.Transfer, inv.PaymentMethod);
            Assert.Equal(20_000_000, inv.Amount);
            Assert.Equal(8, inv.VatRate);
            var logs = await svc.UpdateLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal("Cty Mua Mới", logs[0].BuyerName);
            Assert.Equal(PaymentMethod.Transfer, logs[0].PaymentMethod);
        }
    }

    [Fact]
    public async Task UpdateAfterAllocated_NoNumber_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);   // Draft, chưa có số
            var (ok, msg) = await svc.UpdateAfterAllocatedAsync(invId, "Cty Mua", null, null,
                PaymentMethod.Cash, 10_000_000, 10, DateTime.Today, null, null);
            Assert.False(ok);
            Assert.Contains("chưa được cấp số", msg);
        }
    }

    [Fact]
    public async Task UpdateAfterAllocated_NotDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft nhưng CreateInvoiceAsync tự cấp số → Draft + No
            await svc.TransmitAsync(invId);      // → Accepted
            var (ok, msg) = await svc.UpdateAfterAllocatedAsync(invId, "Cty Mua", null, null,
                PaymentMethod.Cash, 10_000_000, 10, DateTime.Today, null, null);
            Assert.False(ok);
            Assert.Contains("trạng thái chờ", msg);
        }
    }

    [Fact]
    public async Task UpdateAfterAllocated_FutureDate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await SetupAllocated(db, svc);
            var (ok, msg) = await svc.UpdateAfterAllocatedAsync(invId, "Cty Mua", null, null,
                PaymentMethod.Cash, 10_000_000, 10, DateTime.Today.AddDays(1), null, null);
            Assert.False(ok);
            Assert.Contains("ngày tương lai", msg);
        }
    }

    [Fact]
    public async Task UpdateAfterAllocated_DateBeforePrevious_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupAllocated(db, svc, no: "00000002", date: DateTime.Today);
            // HĐ liền trước (số 00000001) có ngày hôm qua → ngày mới không được trước hôm qua.
            db.Invoices.Add(new Invoice { NntId = nntId, Symbol = "1C26TAA", No = "00000001", BuyerName = "X", Amount = 1_000_000, IssuedDate = DateTime.Today.AddDays(-1), Status = InvoiceStatus.Draft });
            await db.SaveChangesAsync();
            var (ok, msg) = await svc.UpdateAfterAllocatedAsync(invId, "Cty Mua", null, null,
                PaymentMethod.Cash, 10_000_000, 10, DateTime.Today.AddDays(-3), null, null);
            Assert.False(ok);
            Assert.Contains("liền trước", msg);
        }
    }

    // Tăng số hóa đơn cuối (EndInvoiceNo) của mẫu hóa đơn
    // (theo Invoice_TempInvoice_IncreaseEndInvoiceNo của TVAN gốc).
    [Fact]
    public async Task IncreaseEndNo_OnIssued_ExtendsRangeAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId, InvoiceNoRule.TT78, lastNo: "00000003", qtyUsed: 3);
            var (ok, msg) = await svc.IncreaseTemplateEndNoAsync(tplId, 2000, "mở rộng dải số", "kế toán");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal(2000, tpl.EndInvoiceNo);
            var logs = await svc.TemplateRangeLogsAsync(tplId);
            Assert.Single(logs);
            Assert.Equal(TemplateRangeAction.IncreaseEndNo, logs[0].Action);
            Assert.Equal(1000, logs[0].OldEndInvoiceNo);
            Assert.Equal(2000, logs[0].NewEndInvoiceNo);
            Assert.Equal("kế toán", logs[0].By);
        }
    }

    [Fact]
    public async Task IncreaseEndNo_NotGreater_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);   // EndInvoiceNo = 1000
            var (ok, msg) = await svc.IncreaseTemplateEndNoAsync(tplId, 1000, null, null);
            Assert.False(ok);
            Assert.Contains("phải lớn hơn", msg);
            Assert.Empty(await svc.TemplateRangeLogsAsync(tplId));
        }
    }

    [Fact]
    public async Task IncreaseEndNo_RangeSmallerThanUsed_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            // StartInvoiceNo=1, EndInvoiceNo=1000, QtyUsed=1000 → dải mới 1..1001 vẫn < QtyUsed? Không.
            // Dùng mẫu dải nhỏ: Start=1, End=10, QtyUsed=10 → tăng lên 11 vẫn hợp lệ; cần dải mới < QtyUsed.
            var tpl = new InvoiceTemplate
            {
                NntId = nntId, TInvoiceCode = "TINV-SMALL", TInvoiceName = "Mẫu nhỏ",
                FormNo = "1C26TAC", Sign = "K26TAC", TTType = InvoiceNoRule.TT78,
                EffDateStart = DateTime.Today.AddDays(-30), StartInvoiceNo = 1, EndInvoiceNo = 10,
                QtyUsed = 10, TInvoiceStatus = TemplateStatus.Issued, FlagActive = true
            };
            db.InvoiceTemplates.Add(tpl); await db.SaveChangesAsync();
            // Số cuối mới 11 > 10 (hợp lệ về tăng) nhưng 11 - 1 = 10 >= QtyUsed(10) → hợp lệ.
            // Để chạm ràng buộc, đặt QtyUsed lớn hơn dải mới: QtyUsed=15.
            tpl.QtyUsed = 15; await db.SaveChangesAsync();
            var (ok, msg) = await svc.IncreaseTemplateEndNoAsync(tpl.Id, 11, null, null);
            Assert.False(ok);
            Assert.Contains("nhỏ hơn số hóa đơn đã dùng", msg);
        }
    }

    [Fact]
    public async Task IncreaseEndNo_LastNoExceedsNewEnd_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId, InvoiceNoRule.TT78, lastNo: "00000900", qtyUsed: 900);
            // Số cuối mới 800 > EndInvoiceNo(1000)? Không → phải chọn số > 1000 nhưng < LastInvoiceNo(900)?
            // LastInvoiceNo=900 <= EndInvoiceNo=1000. Đặt LastInvoiceNo vượt số cuối mới: tăng lên 1001 nhưng Last=1500.
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            tpl.LastInvoiceNo = "00001500"; await db.SaveChangesAsync();
            var (ok, msg) = await svc.IncreaseTemplateEndNoAsync(tplId, 1001, null, null);
            Assert.False(ok);
            Assert.Contains("vượt quá số cuối mới", msg);
        }
    }

    [Fact]
    public async Task IncreaseEndNo_NotIssued_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId);   // Draft
            var (ok, msg) = await svc.IncreaseTemplateEndNoAsync(tplId, 2000, null, null);
            Assert.False(ok);
            Assert.Contains("đang sử dụng", msg);
        }
    }

    [Fact]
    public async Task IncreaseEndNo_UnknownTemplate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.IncreaseTemplateEndNoAsync(9999, 2000, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // Cập nhật lại CẢ dải số (số bắt đầu + số kết thúc) của mẫu hóa đơn đang chờ
    // (theo Invoice_TempInvoice_UpdQtyInvoiceNo của TVAN gốc).
    [Fact]
    public async Task UpdateQtyNo_OnDraft_UpdatesRangeAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId, startNo: 1, endNo: 500);
            var (ok, msg) = await svc.UpdateTemplateQtyNoAsync(tplId, 1, 800, "điều chỉnh dải số", "kế toán");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal(1, tpl.StartInvoiceNo);
            Assert.Equal(800, tpl.EndInvoiceNo);
            var logs = await svc.TemplateRangeLogsAsync(tplId);
            Assert.Single(logs);
            Assert.Equal(TemplateRangeAction.UpdateQtyNo, logs[0].Action);
            Assert.Equal(1, logs[0].OldStartInvoiceNo);
            Assert.Equal(1, logs[0].NewStartInvoiceNo);
            Assert.Equal(500, logs[0].OldEndInvoiceNo);
            Assert.Equal(800, logs[0].NewEndInvoiceNo);
            Assert.Equal("kế toán", logs[0].By);
        }
    }

    [Fact]
    public async Task UpdateQtyNo_InvalidRange_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId, startNo: 1, endNo: 500);
            // Số kết thúc nhỏ hơn số bắt đầu → không hợp lệ.
            var (ok, msg) = await svc.UpdateTemplateQtyNoAsync(tplId, 100, 50, null, null);
            Assert.False(ok);
            Assert.Contains("không hợp lệ", msg);
            Assert.Empty(await svc.TemplateRangeLogsAsync(tplId));
        }
    }

    [Fact]
    public async Task UpdateQtyNo_RangeSmallerThanUsed_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId, startNo: 1, endNo: 500);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            tpl.QtyUsed = 600; await db.SaveChangesAsync();
            var (ok, msg) = await svc.UpdateTemplateQtyNoAsync(tplId, 1, 500, null, null);
            Assert.False(ok);
            Assert.Contains("nhỏ hơn số hóa đơn đã dùng", msg);
        }
    }

    [Fact]
    public async Task UpdateQtyNo_LastNoExceedsNewEnd_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId, startNo: 1, endNo: 500);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            tpl.LastInvoiceNo = "00000600"; await db.SaveChangesAsync();
            var (ok, msg) = await svc.UpdateTemplateQtyNoAsync(tplId, 1, 500, null, null);
            Assert.False(ok);
            Assert.Contains("vượt quá số kết thúc mới", msg);
        }
    }

    [Fact]
    public async Task UpdateQtyNo_NotDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);   // Issued
            var (ok, msg) = await svc.UpdateTemplateQtyNoAsync(tplId, 1, 800, null, null);
            Assert.False(ok);
            Assert.Contains("trạng thái chờ", msg);
        }
    }

    [Fact]
    public async Task UpdateQtyNo_UnknownTemplate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.UpdateTemplateQtyNoAsync(9999, 1, 800, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // ===== Phát hành hóa đơn (theo Invoice_Invoice_Issued của TVAN gốc): APPROVED → ISSUED =====

    [Fact]
    public async Task Issue_OnApproved_SetsIssuedAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft (PENDING)
            await svc.ApproveAsync(invId, "a.xml", "a.pdf", null, "kế toán trưởng");
            var (ok, msg) = await svc.IssueAsync(invId, "khachhang@congty.vn", "phát hành gửi khách", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Accepted, inv!.Status);
            Assert.NotNull(inv.IssuedDTimeUTC);
            Assert.Equal("kế toán", inv.IssuedBy);
            Assert.Equal("khachhang@congty.vn", inv.EmailSend);
            Assert.NotNull(inv.SendEmailDTimeUTC);
            var logs = await svc.IssueLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(IssueAction.Issue, logs[0].Action);
            Assert.Equal("khachhang@congty.vn", logs[0].EmailSend);
            Assert.Equal("kế toán", logs[0].By);
        }
    }

    [Fact]
    public async Task Issue_OnDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, chưa duyệt
            var (ok, msg) = await svc.IssueAsync(invId, null, null, null);
            Assert.False(ok);
            Assert.Contains("đã duyệt", msg);
            Assert.Empty(await svc.IssueLogsAsync(invId));
        }
    }

    [Fact]
    public async Task Issue_InvalidEmail_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.ApproveAsync(invId, null, null, null, "kế toán");
            var (ok, msg) = await svc.IssueAsync(invId, "khong-phai-email", null, null);
            Assert.False(ok);
            Assert.Contains("Email", msg);
            Assert.Equal(InvoiceStatus.Approved, (await svc.GetInvoiceAsync(invId))!.Status);
        }
    }

    [Fact]
    public async Task Issue_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.IssueAsync(9999, null, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // Hủy hóa đơn (theo Invoice_Invoice_Cancel của TVAN gốc): PENDING/APPROVED → CANCELED.
    [Fact]
    public async Task CancelInvoice_OnDraftWithNo_SetsCanceledAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // Draft, đã có số (CreateInvoiceAsync tự cấp số)
            var (ok, msg) = await svc.CancelInvoiceAsync(invId, "lập sai", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Cancelled, inv!.Status);
            Assert.NotNull(inv.CancelDTimeUTC);
            Assert.Equal("kế toán", inv.CancelBy);
            Assert.Equal("lập sai", inv.Remark);
            var logs = await svc.CancelLogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(CancelAction.Cancel, logs[0].Action);
            Assert.Equal("kế toán", logs[0].By);
        }
    }

    [Fact]
    public async Task CancelInvoice_OnApproved_SetsCanceled()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.ApproveAsync(invId, null, null, null, "kế toán trưởng");
            var (ok, _) = await svc.CancelInvoiceAsync(invId, null, "kế toán");
            Assert.True(ok);
            Assert.Equal(InvoiceStatus.Cancelled, (await svc.GetInvoiceAsync(invId))!.Status);
        }
    }

    [Fact]
    public async Task CancelInvoice_OnAccepted_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted
            var (ok, msg) = await svc.CancelInvoiceAsync(invId, null, null);
            Assert.False(ok);
            Assert.Contains("chờ", msg);
            Assert.Empty(await svc.CancelLogsAsync(invId));
        }
    }

    [Fact]
    public async Task CancelInvoice_DraftWithoutNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.RegisterNntAsync(nntId);
            var inv = new Invoice { NntId = nntId, Symbol = "1C26TAA", No = "", BuyerName = "Cty Mua", Amount = 5_000_000 };
            db.Invoices.Add(inv); await db.SaveChangesAsync();
            var (ok, msg) = await svc.CancelInvoiceAsync(inv.Id, null, null);
            Assert.False(ok);
            Assert.Contains("cấp số", msg);
        }
    }

    [Fact]
    public async Task CancelInvoice_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.CancelInvoiceAsync(9999, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // Cấp số hóa đơn khởi tạo từ MÁY TÍNH TIỀN + sinh mã CQT máy tính tiền
    // (theo Invoice_Invoice_AllocatedInvoiceTypeM / Invoice_Invoice_GenMCCQTMTTTypeM của TVAN gốc).
    private static async Task<(int nntId, int invId)> SetupTypeM(AppDbContext db, ITvanService svc, string? mccqt = "A1B2C")
    {
        var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán", MCCQT = mccqt });
        await svc.RegisterNntAsync(nntId);
        var inv = new Invoice { NntId = nntId, Symbol = "1C2MAA", No = "", BuyerName = "Khách lẻ", Amount = 2_000_000, Status = InvoiceStatus.Draft };
        db.Invoices.Add(inv); await db.SaveChangesAsync();
        return (nntId, inv.Id);
    }

    private static async Task<int> AddTypeMTemplate(AppDbContext db, int nntId, string formNo = "1C2MAA", string? lastNo = null, int qtyUsed = 0)
    {
        var tpl = new InvoiceTemplate
        {
            NntId = nntId, TInvoiceCode = "TINV-" + formNo, TInvoiceName = "Mẫu " + formNo,
            FormNo = formNo, Sign = "2", TTType = InvoiceNoRule.TT78,
            EffDateStart = DateTime.Today.AddDays(-30), StartInvoiceNo = 1, EndInvoiceNo = 1000,
            LastInvoiceNo = lastNo, QtyUsed = qtyUsed, TInvoiceStatus = TemplateStatus.Issued, FlagActive = true
        };
        db.InvoiceTemplates.Add(tpl); await db.SaveChangesAsync();
        return tpl.Id;
    }

    [Fact]
    public async Task AllocateNoTypeM_OnDraft_AssignsNoAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupTypeM(db, svc);
            await AddTypeMTemplate(db, nntId, lastNo: "00000003", qtyUsed: 3);
            var (ok, msg, no) = await svc.AllocateInvoiceNoTypeMAsync(invId, DateTime.Today, "kế toán");
            Assert.True(ok);
            Assert.Equal("00000004", no);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal("00000004", inv!.No);
            Assert.Equal("1C2MAA", inv.Symbol);
            Assert.Null(inv.TctCode);   // HĐ MTT không sinh mã tra cứu thông thường
            Assert.Single(await svc.AllocLogsAsync(invId));
        }
    }

    [Fact]
    public async Task AllocateNoTypeM_NonTypeMTemplate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupTypeM(db, svc);
            await AddTemplate(db, nntId);   // mẫu 1C26TAA (không phải MTT)
            var (ok, msg, _) = await svc.AllocateInvoiceNoTypeMAsync(invId, DateTime.Today, null);
            Assert.False(ok);
            Assert.Contains("máy tính tiền", msg);
        }
    }

    [Fact]
    public async Task AllocateNoTypeM_MissingMccqt_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupTypeM(db, svc, mccqt: null);
            await AddTypeMTemplate(db, nntId);
            var (ok, msg, _) = await svc.AllocateInvoiceNoTypeMAsync(invId, DateTime.Today, null);
            Assert.False(ok);
            Assert.Contains("MCCQT", msg);
        }
    }

    [Fact]
    public async Task GenMccqtMtt_OnDraftTypeM_Generates23CharCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupTypeM(db, svc);
            await AddTypeMTemplate(db, nntId);
            await svc.AllocateInvoiceNoTypeMAsync(invId, DateTime.Today, null);
            var (ok, msg, code) = await svc.GenMccqtMttAsync(invId);
            Assert.True(ok);
            Assert.NotNull(code);
            Assert.Equal(23, code!.Length);
            Assert.StartsWith("M2-", code);
            Assert.Contains("A1B2C", code);
            Assert.Equal(code, (await svc.GetInvoiceAsync(invId))!.MCCQTMTT);
        }
    }

    [Fact]
    public async Task GenMccqtMtt_NonTypeM_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // HĐ 1C26TAA (không phải MTT)
            var (ok, msg, _) = await svc.GenMccqtMttAsync(invId);
            Assert.False(ok);
            Assert.Contains("máy tính tiền", msg);
        }
    }

    // Tạo biên bản đính kèm hóa đơn (theo luồng TaoBienBan của TVAN gốc).
    [Fact]
    public async Task CreateRecord_OnInvoice_SavesFileAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            var (ok, msg, logId) = await svc.CreateRecordAsync(invId, RecordType.Huy, "MauBienBanHuyHoaDon.docx", "UEsDBBQ=", "Lập sai", "kế toán");
            Assert.True(ok);
            Assert.True(logId > 0);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal("MauBienBanHuyHoaDon.docx", inv!.AttachedDelFileName);
            Assert.Equal("UEsDBBQ=", inv.AttachedDelFileSpec);
            Assert.Equal("Lập sai", inv.DeleteReason);
            Assert.False(string.IsNullOrWhiteSpace(inv.AttachedDelFilePath));
            Assert.Single(await svc.RecordLogsAsync(invId));
        }
    }

    [Fact]
    public async Task CreateRecord_MissingFileName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            var (ok, msg, _) = await svc.CreateRecordAsync(invId, RecordType.DieuChinh, "  ", null, null, null);
            Assert.False(ok);
            Assert.Contains("tên file", msg);
        }
    }

    [Fact]
    public async Task CreateRecord_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateRecordAsync(9999, RecordType.ThayThe, "bb.docx", null, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // Cấp số + Duyệt + Phát hành trong MỘT bước (theo Invoice_Invoice_AllocatedAndApprovedAndIssued của TVAN gốc).
    [Fact]
    public async Task AllocateApproveIssue_OnDraft_AssignsApprovesIssuesAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);
            await AddTemplate(db, nntId, InvoiceNoRule.TT78, lastNo: "00000003", qtyUsed: 3);
            var (ok, msg, no) = await svc.AllocateApproveIssueAsync(invId, DateTime.Today, "2026-06-12/HD0004.xml", "2026-06-12/HD0004.pdf", "khach@congty.vn", "phát hành gửi khách", "kế toán");
            Assert.True(ok);
            Assert.Equal("00000004", no);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(InvoiceStatus.Accepted, inv!.Status);
            Assert.Equal("00000004", inv.No);
            Assert.Equal("2026-06-12/HD0004.xml", inv.InvoiceFilePath);
            Assert.Equal("2026-06-12/HD0004.pdf", inv.InvoicePDFFilePath);
            Assert.Equal("khach@congty.vn", inv.EmailSend);
            Assert.NotNull(inv.InvoiceNoDTimeUTC);
            Assert.NotNull(inv.ApprDTimeUTC);
            Assert.NotNull(inv.IssuedDTimeUTC);
            Assert.Equal("kế toán", inv.IssuedBy);
            Assert.Single(await svc.AllocLogsAsync(invId));
            Assert.Single(await svc.ApproveLogsAsync(invId));
            Assert.Single(await svc.IssueLogsAsync(invId));
        }
    }

    [Fact]
    public async Task AllocateApproveIssue_AlreadyHasNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);
            await AddTemplate(db, nntId);
            await svc.AllocateInvoiceNoAsync(invId, DateTime.Today, null);   // đã có số
            var (ok, msg, _) = await svc.AllocateApproveIssueAsync(invId, DateTime.Today, null, null, null, null, null);
            Assert.False(ok);
            Assert.Contains("đã có số", msg);
        }
    }

    [Fact]
    public async Task AllocateApproveIssue_NoTemplate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await SetupNoNo(db, svc);   // không có mẫu
            var (ok, msg, _) = await svc.AllocateApproveIssueAsync(invId, DateTime.Today, null, null, null, null, null);
            Assert.False(ok);
            Assert.Contains("mẫu hóa đơn", msg);
        }
    }

    [Fact]
    public async Task AllocateApproveIssue_InvalidEmail_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, invId) = await SetupNoNo(db, svc);
            await AddTemplate(db, nntId);
            var (ok, msg, _) = await svc.AllocateApproveIssueAsync(invId, DateTime.Today, null, null, "khong-phai-email", null, null);
            Assert.False(ok);
            Assert.Contains("Email", msg);
            Assert.Equal(InvoiceStatus.Draft, (await svc.GetInvoiceAsync(invId))!.Status);
        }
    }

    [Fact]
    public async Task AllocateApproveIssue_UnknownInvoice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.AllocateApproveIssueAsync(9999, DateTime.Today, null, null, null, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // Gửi mẫu hóa đơn tới CQT (theo Invoice_TempInvoice_SentTCT của TVAN gốc).
    private static async Task<int> AddDraftTemplate(AppDbContext db, int nntId)
    {
        var tpl = new InvoiceTemplate
        {
            NntId = nntId, TInvoiceCode = "TINV-1C26TAC", TInvoiceName = "Mẫu 1C26TAC",
            FormNo = "1C26TAC", Sign = "K26TAC", TTType = InvoiceNoRule.TT78,
            EffDateStart = DateTime.Today, StartInvoiceNo = 1, EndInvoiceNo = 500,
            QtyUsed = 0, TInvoiceStatus = TemplateStatus.Draft, FlagActive = true
        };
        db.InvoiceTemplates.Add(tpl); await db.SaveChangesAsync();
        return tpl.Id;
    }

    [Fact]
    public async Task SendTemplateToTct_OnDraft_SetsSentTctAndRefNo()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var tplId = await AddDraftTemplate(db, nntId);
            var (ok, msg) = await svc.SendTemplateToTctAsync(tplId, "Gửi đăng ký", "kế toán");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal(TemplateStatus.SentTct, tpl.TInvoiceStatus);
            Assert.False(string.IsNullOrWhiteSpace(tpl.TCTRefNo));
            Assert.NotNull(tpl.SentTCTDTime);
            Assert.Equal("kế toán", tpl.SentTCTBy);
            Assert.Single(await svc.TemplateTctLogsAsync(tplId));
        }
    }

    [Fact]
    public async Task SendTemplateToTct_OnIssued_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var tplId = await AddTemplate(db, nntId);   // Issued
            var (ok, msg) = await svc.SendTemplateToTctAsync(tplId, null, null);
            Assert.False(ok);
            Assert.Contains("chờ", msg);
        }
    }

    [Fact]
    public async Task ReceiveTemplateTct_Accept_SetsIssued()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var tplId = await AddDraftTemplate(db, nntId);
            await svc.SendTemplateToTctAsync(tplId, null, "kế toán");
            var (ok, _) = await svc.ReceiveTemplateTctResultAsync(tplId, TctAcceptStatus.Accept, "Mẫu hợp lệ", "kế toán");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal(TemplateStatus.Issued, tpl.TInvoiceStatus);
            Assert.Equal(TctAcceptStatus.Accept, tpl.TCTChapNhan);
            Assert.NotNull(tpl.TCTChapNhanDTime);
            Assert.Equal(2, (await svc.TemplateTctLogsAsync(tplId)).Count);
        }
    }

    [Fact]
    public async Task ReceiveTemplateTct_Reject_BackToDraft()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var tplId = await AddDraftTemplate(db, nntId);
            await svc.SendTemplateToTctAsync(tplId, null, null);
            var (ok, _) = await svc.ReceiveTemplateTctResultAsync(tplId, TctAcceptStatus.Reject, "Sai mẫu số", null);
            Assert.False(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal(TemplateStatus.Draft, tpl.TInvoiceStatus);
            Assert.Equal(TctAcceptStatus.Reject, tpl.TCTChapNhan);
        }
    }

    [Fact]
    public async Task ReceiveTemplateTct_NotSent_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var tplId = await AddDraftTemplate(db, nntId);   // Draft, chưa gửi CQT
            var (ok, msg) = await svc.ReceiveTemplateTctResultAsync(tplId, TctAcceptStatus.Accept, null, null);
            Assert.False(ok);
            Assert.Contains("SENTTCT", msg);
        }
    }

    // ===== Trường tùy chỉnh hóa đơn (theo Invoice_CustomField / Invoice_DtlCustomField của TVAN gốc) =====
    [Fact]
    public async Task CustomField_Save_CreatesThenUpdates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveInvoiceCustomFieldAsync("InvCF1", "Số hợp đồng", DBPhysicalType.Text, true, "kế toán");
            Assert.True(ok);
            var f = await db.InvoiceCustomFields.FirstAsync(x => x.Id == id);
            Assert.Equal("Số hợp đồng", f.InvoiceCustomFieldName);
            Assert.True(f.FlagActive);

            // Lưu lại cùng mã → cập nhật, không tạo mới.
            var (ok2, _, id2) = await svc.SaveInvoiceCustomFieldAsync("InvCF1", "Số HĐ", DBPhysicalType.Text, false, "kế toán");
            Assert.True(ok2);
            Assert.Equal(id, id2);
            Assert.Equal(1, await db.InvoiceCustomFields.CountAsync());
            Assert.Equal("Số HĐ", (await db.InvoiceCustomFields.FirstAsync(x => x.Id == id)).InvoiceCustomFieldName);
            Assert.False((await db.InvoiceCustomFields.FirstAsync(x => x.Id == id)).FlagActive);
        }
    }

    [Fact]
    public async Task CustomField_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveInvoiceCustomFieldAsync("InvCF1", "  ", DBPhysicalType.Text, true, null);
            Assert.False(ok);
            Assert.Contains("tên trường", msg);
            Assert.Equal(0, await db.InvoiceCustomFields.CountAsync());
        }
    }

    [Fact]
    public async Task CustomField_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveInvoiceCustomFieldAsync("InvCF1", "Số hợp đồng", DBPhysicalType.Text, true, null);
            var (ok, _) = await svc.DeleteInvoiceCustomFieldAsync("InvCF1");
            Assert.True(ok);
            Assert.Equal(0, await db.InvoiceCustomFields.CountAsync());
            var (ok2, msg2) = await svc.DeleteInvoiceCustomFieldAsync("InvCF1");
            Assert.False(ok2);
            Assert.Contains("Không tìm thấy", msg2);
        }
    }

    [Fact]
    public async Task DtlCustomField_SaveAndList()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, _) = await svc.SaveInvoiceDtlCustomFieldAsync("InvDCF1", "Mã kho", DBPhysicalType.Text, true, "kế toán");
            Assert.True(ok);
            var ls = await svc.InvoiceDtlCustomFieldsAsync();
            Assert.Single(ls);
            Assert.Equal("Mã kho", ls[0].InvoiceDtlCustomFieldName);
        }
    }

    // ===== Cập nhật thông tin liên hệ của NNT trên mẫu hóa đơn
    // (theo Invoice_TempInvoice_SupportUpdEmailAndAddress của TVAN gốc) =====

    [Fact]
    public async Task UpdateTemplateContact_SavesAllFields()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);
            var (ok, msg) = await svc.UpdateTemplateContactAsync(tplId, "Cty Bán", "Hà Nội", "024 1234", "kt@cty.vn", "https://cty.vn", true, "kế toán");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal("Cty Bán", tpl.NNTName);
            Assert.Equal("Hà Nội", tpl.NNTAddress);
            Assert.Equal("024 1234", tpl.NNTPhone);
            Assert.Equal("kt@cty.vn", tpl.NNTEmail);
            Assert.Equal("https://cty.vn", tpl.NNTWebsite);
            Assert.True(tpl.FlagStyleComma);
            Assert.Equal("kế toán", tpl.ContactUpdatedBy);
            Assert.NotNull(tpl.ContactUpdatedAt);
        }
    }

    [Fact]
    public async Task UpdateTemplateContact_EmptyName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);
            var (ok, msg) = await svc.UpdateTemplateContactAsync(tplId, "  ", "Hà Nội", null, null, null, false, null);
            Assert.False(ok);
            Assert.Contains("Tên đơn vị", msg);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Null(tpl.NNTName);
        }
    }

    [Fact]
    public async Task UpdateTemplateContact_UnknownTemplate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.UpdateTemplateContactAsync(9999, "Cty Bán", null, null, null, null, false, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // ===== Cập nhật số tài khoản & tên ngân hàng của NNT trên mẫu hóa đơn
    // (theo Invoice_TempInvoice_SupportUpdAccNoAndBankName của TVAN gốc) =====

    [Fact]
    public async Task UpdateTemplateBank_SavesAccNoAndBankName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);
            var (ok, msg) = await svc.UpdateTemplateBankAsync(tplId, "1234567890", "Vietcombank - CN Hà Nội", "kế toán");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Equal("1234567890", tpl.NNTAccNo);
            Assert.Equal("Vietcombank - CN Hà Nội", tpl.NNTBankName);
            Assert.Equal("kế toán", tpl.ContactUpdatedBy);
            Assert.NotNull(tpl.ContactUpdatedAt);
        }
    }

    [Fact]
    public async Task UpdateTemplateBank_EmptyAccNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);
            var (ok, msg) = await svc.UpdateTemplateBankAsync(tplId, "  ", "Vietcombank", null);
            Assert.False(ok);
            Assert.Contains("Số tài khoản", msg);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Null(tpl.NNTAccNo);
        }
    }

    [Fact]
    public async Task UpdateTemplateBank_EmptyBankName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);
            var (ok, msg) = await svc.UpdateTemplateBankAsync(tplId, "1234567890", "  ", null);
            Assert.False(ok);
            Assert.Contains("Tên ngân hàng", msg);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            Assert.Null(tpl.NNTBankName);
        }
    }

    [Fact]
    public async Task UpdateTemplateBank_UnknownTemplate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.UpdateTemplateBankAsync(9999, "1234567890", "Vietcombank", null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    // ===== Nhóm mẫu hóa đơn (theo Invoice_TempGroup của TVAN gốc) =====

    [Fact]
    public async Task TempGroup_Save_CreatesWithFields()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var fields = new List<(string, string)> { ("Temp_NameSale", "TEXT"), ("Temp_MSTSale", "TEXT") };
            var (ok, msg, id) = await svc.SaveTempGroupAsync(null, "MAU1VAT", "0101243150", VATType.OneVat, "Mẫu 1VAT", "<div/>", "/Images/x.png", SpecPrdType.Spec, true, fields, "kế toán");
            Assert.True(ok);
            var g = await svc.GetTempGroupAsync(id);
            Assert.NotNull(g);
            Assert.Equal("MAU1VAT", g!.InvoiceTGroupCode);
            Assert.Equal(VATType.OneVat, g.VATType);
            Assert.Equal(SpecPrdType.Spec, g.SpecPrdType);
            Assert.Equal(2, g.Fields.Count);
        }
    }

    [Fact]
    public async Task TempGroup_Save_UpdatesAndReplacesFields()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (_, _, id) = await svc.SaveTempGroupAsync(null, "MAU1VAT", "0101243150", VATType.OneVat, "Mẫu 1VAT", null, null, SpecPrdType.Spec, true, new List<(string, string)> { ("A", "TEXT") }, null);
            var (ok, _, id2) = await svc.SaveTempGroupAsync(id, "MAU1VAT", "0101243150", VATType.NVat, "Mẫu NVAT", null, null, SpecPrdType.ProductId, false, new List<(string, string)> { ("B", "NUMBER"), ("C", "TEXT") }, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Equal(1, await db.InvoiceTempGroups.CountAsync());
            var g = await svc.GetTempGroupAsync(id);
            Assert.Equal("Mẫu NVAT", g!.InvoiceTGroupName);
            Assert.Equal(VATType.NVat, g.VATType);
            Assert.False(g.FlagActive);
            Assert.Equal(2, g.Fields.Count);
        }
    }

    [Fact]
    public async Task TempGroup_Save_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (ok, msg, _) = await svc.SaveTempGroupAsync(null, "  ", "0101243150", VATType.OneVat, "Mẫu", null, null, SpecPrdType.Spec, true, new List<(string, string)>(), null);
            Assert.False(ok);
            Assert.Contains("mã nhóm mẫu", msg);
            Assert.Equal(0, await db.InvoiceTempGroups.CountAsync());
        }
    }

    [Fact]
    public async Task TempGroup_Save_UnknownMst_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (ok, msg, _) = await svc.SaveTempGroupAsync(null, "MAU1VAT", "9999999999", VATType.OneVat, "Mẫu", null, null, SpecPrdType.Spec, true, new List<(string, string)>(), null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy NNT", msg);
        }
    }

    [Fact]
    public async Task TempGroup_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (_, _, id) = await svc.SaveTempGroupAsync(null, "MAU1VAT", "0101243150", VATType.OneVat, "Mẫu", null, null, SpecPrdType.Spec, true, new List<(string, string)> { ("A", "TEXT") }, null);
            var (ok, _) = await svc.DeleteTempGroupAsync(id);
            Assert.True(ok);
            Assert.Equal(0, await db.InvoiceTempGroups.CountAsync());
            Assert.Equal(0, await db.InvoiceTempGroupFields.CountAsync());   // cascade
            var (ok2, msg2) = await svc.DeleteTempGroupAsync(id);
            Assert.False(ok2);
            Assert.Contains("Không tìm thấy", msg2);
        }
    }

    // Bảng tổng hợp hóa đơn (BTH) — theo Invoice_Invoice_BTHGet / Invoice_Invoice_BTHGetX của TVAN gốc.
    [Fact]
    public async Task Bth_IssuedRoot_ShowsMoi()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted (ISSUED), IssuedDate = hôm nay
            var rows = await svc.BthRowsAsync(PeriodType.Month, DateTime.Today.ToString("yyyy-MM"));
            Assert.Single(rows);
            Assert.Equal(TThai.Moi, rows[0].TThai);
            Assert.Equal(11_000_000, rows[0].Total);   // 10tr + 10% VAT
        }
    }

    [Fact]
    public async Task Bth_DeletedRoot_ShowsHuy()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            await svc.DeleteInvoiceAsync(invId, "lập sai", "kế toán");   // ISSUED → DELETED
            var rows = await svc.BthRowsAsync(PeriodType.Month, DateTime.Today.ToString("yyyy-MM"));
            Assert.Single(rows);
            Assert.Equal(TThai.Huy, rows[0].TThai);
        }
    }

    [Fact]
    public async Task Bth_AdjustAndReplace_ShowStatusAndRefInvoice()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var (_, _, adjId) = await svc.AdjustAsync(invId, InvoiceAdjType.Decrease, 5_000_000, 10, "giảm giá");
            var (_, _, repId) = await svc.ReplaceAsync(invId, 20_000_000, 10, "sai số tiền");
            var rows = await svc.BthRowsAsync(PeriodType.Month, DateTime.Today.ToString("yyyy-MM"));
            var adj = rows.Single(r => r.TThai == TThai.DieuChinh);
            var rep = rows.Single(r => r.TThai == TThai.ThayThe);
            // HĐ điều chỉnh/thay thế trỏ tới hóa đơn gốc (ký hiệu/mẫu số/số HĐ gốc).
            Assert.Equal("1C26TAA", adj.RefFormNo);
            Assert.False(string.IsNullOrWhiteSpace(adj.RefInvoiceNo));
            Assert.Equal("1C26TAA", rep.RefFormNo);
            Assert.False(string.IsNullOrWhiteSpace(rep.RefInvoiceNo));
        }
    }

    [Fact]
    public async Task Bth_EmptyPeriod_ReturnsEmpty()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);
            var rows = await svc.BthRowsAsync(PeriodType.Month, "2000-01");   // kỳ không có HĐ
            Assert.Empty(rows);
        }
    }

    // Danh mục khách hàng / người mua (theo Mst_CustomerNNT của TVAN gốc).
    [Fact]
    public async Task Customer_Save_CreatesAndLists()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var (ok, _, id) = await svc.SaveCustomerNntAsync(null, "0101243150", "KH001", "Cty Mua A", "8012345678", "Doanh nghiệp", "Hà Nội", "a@x.vn", "0912", null, null, null, null, null, null, null, null, null, null, null, null, true, "kế toán");
            Assert.True(ok);
            var list = await svc.CustomerNntsAsync("0101243150");
            Assert.Single(list);
            Assert.Equal("KH001", list[0].CustomerNNTCode);
            Assert.Equal("Cty Mua A", list[0].CustomerNNTName);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task Customer_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var (_, _, id) = await svc.SaveCustomerNntAsync(null, "0101243150", "KH001", "Cty Mua A", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, null);
            // Lưu lại cùng mã khách hàng = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveCustomerNntAsync(null, "0101243150", "KH001", "Cty Mua B", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.CustomerNntsAsync("0101243150");
            Assert.Single(list);
            Assert.Equal("Cty Mua B", list[0].CustomerNNTName);
        }
    }

    [Fact]
    public async Task Customer_Save_DuplicateCustomerMst_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.SaveCustomerNntAsync(null, "0101243150", "KH001", "Cty Mua A", "8012345678", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, null);
            var (ok, msg, _) = await svc.SaveCustomerNntAsync(null, "0101243150", "KH002", "Cty Mua B", "8012345678", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, null);
            Assert.False(ok);
            Assert.Contains("MST khách hàng", msg);
        }
    }

    [Fact]
    public async Task Customer_Save_UnknownNnt_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveCustomerNntAsync(null, "9999999999", "KH001", "Cty Mua A", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, null);
            Assert.False(ok);
            Assert.Contains("NNT", msg);
        }
    }

    [Fact]
    public async Task Customer_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var (_, _, id) = await svc.SaveCustomerNntAsync(null, "0101243150", "KH001", "Cty Mua A", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, null);
            var (ok, _) = await svc.DeleteCustomerNntAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.CustomerNntsAsync("0101243150"));
        }
    }

    [Fact]
    public async Task NntType_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveNntTypeAsync(null, "DN", "Doanh nghiệp", true, "kế toán");
            Assert.True(ok);
            var list = await svc.NntTypesAsync(null);
            Assert.Single(list);
            Assert.Equal("Doanh nghiệp", list[0].NNTTypeName);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task NntType_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveNntTypeAsync(null, "DN", "Doanh nghiệp", true, null);
            // Lưu lại cùng mã loại = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveNntTypeAsync(null, "DN", "Doanh nghiệp lớn", false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.NntTypesAsync(null);
            Assert.Single(list);
            Assert.Equal("Doanh nghiệp lớn", list[0].NNTTypeName);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task NntType_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveNntTypeAsync(null, "DN", "  ", true, null);
            Assert.False(ok);
            Assert.Contains("tên", msg);
        }
    }

    [Fact]
    public async Task NntType_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveNntTypeAsync(null, "HKD", "Hộ kinh doanh", true, null);
            var (ok, _) = await svc.DeleteNntTypeAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.NntTypesAsync(null));
        }
    }

    // Danh mục thuế suất VAT (theo Mst_VATRate của TVAN gốc).
    [Fact]
    public async Task VatRate_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveVatRateAsync(null, "VAT10", "10%", "Thuế suất GTGT 10%", true, "kế toán");
            Assert.True(ok);
            var list = await svc.VatRatesAsync(null);
            Assert.Single(list);
            Assert.Equal("10%", list[0].VATRate);
            Assert.Equal("Thuế suất GTGT 10%", list[0].VATDesc);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task VatRate_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveVatRateAsync(null, "VAT8", "8%", null, true, null);
            // Lưu lại cùng mã thuế suất = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveVatRateAsync(null, "VAT8", "8%", "Giảm thuế GTGT", false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.VatRatesAsync(null);
            Assert.Single(list);
            Assert.Equal("Giảm thuế GTGT", list[0].VATDesc);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task VatRate_Save_MissingRate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveVatRateAsync(null, "VAT10", "  ", null, true, null);
            Assert.False(ok);
            Assert.Contains("thuế suất", msg);
        }
    }

    [Fact]
    public async Task VatRate_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveVatRateAsync(null, "KCT", "KCT", "Không chịu thuế", true, null);
            var (ok, _) = await svc.DeleteVatRateAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.VatRatesAsync(null));
        }
    }

    // Danh mục loại khách hàng / người mua (theo Mst_CustomerNNTType của TVAN gốc).
    [Fact]
    public async Task CustomerNntType_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveCustomerNntTypeAsync(null, "DN", "Doanh nghiệp", "Khách hàng doanh nghiệp", true, "kế toán");
            Assert.True(ok);
            var list = await svc.CustomerNntTypesAsync(null);
            Assert.Single(list);
            Assert.Equal("Doanh nghiệp", list[0].CustomerNNTTypeName);
            Assert.Equal("Khách hàng doanh nghiệp", list[0].Remark);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task CustomerNntType_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveCustomerNntTypeAsync(null, "DN", "Doanh nghiệp", null, true, null);
            // Lưu lại cùng mã loại = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveCustomerNntTypeAsync(null, "DN", "Doanh nghiệp lớn", null, false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.CustomerNntTypesAsync(null);
            Assert.Single(list);
            Assert.Equal("Doanh nghiệp lớn", list[0].CustomerNNTTypeName);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task CustomerNntType_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveCustomerNntTypeAsync(null, "DN", "  ", null, true, null);
            Assert.False(ok);
            Assert.Contains("tên", msg);
        }
    }

    [Fact]
    public async Task CustomerNntType_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveCustomerNntTypeAsync(null, "CN", "Cá nhân", null, true, null);
            var (ok, _) = await svc.DeleteCustomerNntTypeAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.CustomerNntTypesAsync(null));
        }
    }

    // Danh mục Tỉnh/Thành phố (theo Mst_Province của TVAN gốc).
    [Fact]
    public async Task Province_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, "kế toán");
            Assert.True(ok);
            var list = await svc.ProvincesAsync(null);
            Assert.Single(list);
            Assert.Equal("Hà Nội", list[0].ProvinceName);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task Province_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            // Lưu lại cùng mã tỉnh = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveProvinceAsync(null, "01", "Thủ đô Hà Nội", false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.ProvincesAsync(null);
            Assert.Single(list);
            Assert.Equal("Thủ đô Hà Nội", list[0].ProvinceName);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task Province_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveProvinceAsync(null, "01", "  ", true, null);
            Assert.False(ok);
            Assert.Contains("tên", msg);
        }
    }

    [Fact]
    public async Task Province_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveProvinceAsync(null, "79", "TP Hồ Chí Minh", true, null);
            var (ok, _) = await svc.DeleteProvinceAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.ProvincesAsync(null));
        }
    }

    [Fact]
    public async Task District_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            var (ok, _, id) = await svc.SaveDistrictAsync(null, "01", "0101", "Quận Ba Đình", true, "kế toán");
            Assert.True(ok);
            var list = await svc.DistrictsAsync(null, null);
            Assert.Single(list);
            Assert.Equal("Quận Ba Đình", list[0].DistrictName);
            Assert.Equal("01", list[0].ProvinceCode);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task District_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            var (_, _, id) = await svc.SaveDistrictAsync(null, "01", "0101", "Quận Ba Đình", true, null);
            // Lưu lại cùng mã quận trong cùng tỉnh = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveDistrictAsync(null, "01", "0101", "Q. Ba Đình", false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.DistrictsAsync(null, null);
            Assert.Single(list);
            Assert.Equal("Q. Ba Đình", list[0].DistrictName);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task District_Save_MissingProvince_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            // Chưa có tỉnh/thành nào → chặn (theo Mst_Province_CheckDB của TVAN gốc).
            var (ok, msg, _) = await svc.SaveDistrictAsync(null, "01", "0101", "Quận Ba Đình", true, null);
            Assert.False(ok);
            Assert.Contains("Tỉnh/thành phố không tồn tại", msg);
        }
    }

    [Fact]
    public async Task District_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            var (ok, msg, _) = await svc.SaveDistrictAsync(null, "01", "0101", "  ", true, null);
            Assert.False(ok);
            Assert.Contains("tên", msg);
        }
    }

    [Fact]
    public async Task District_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            var (_, _, id) = await svc.SaveDistrictAsync(null, "01", "0101", "Quận Ba Đình", true, null);
            var (ok, _) = await svc.DeleteDistrictAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.DistrictsAsync(null, null));
        }
    }

    // Danh mục Quốc gia (theo Mst_Country của TVAN gốc).
    [Fact]
    public async Task Country_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveCountryAsync(null, "VN", "Việt Nam", true, "kế toán");
            Assert.True(ok);
            var list = await svc.CountriesAsync(null);
            Assert.Single(list);
            Assert.Equal("Việt Nam", list[0].CountryName);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task Country_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveCountryAsync(null, "VN", "Việt Nam", true, null);
            // Lưu lại cùng mã quốc gia = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveCountryAsync(null, "VN", "Cộng hòa XHCN Việt Nam", false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.CountriesAsync(null);
            Assert.Single(list);
            Assert.Equal("Cộng hòa XHCN Việt Nam", list[0].CountryName);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task Country_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveCountryAsync(null, "VN", "  ", true, null);
            Assert.False(ok);
            Assert.Contains("tên", msg);
        }
    }

    [Fact]
    public async Task Country_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveCountryAsync(null, "JP", "Nhật Bản", true, null);
            var (ok, _) = await svc.DeleteCountryAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.CountriesAsync(null));
        }
    }

    // Danh mục Đại lý (theo Mst_Dealer của TVAN gốc).
    [Fact]
    public async Task Dealer_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            var (ok, _, id) = await svc.SaveDealerAsync(null, "DL001", "Đại lý Hà Nội", "01", "Số 1 Lê Lợi", "Nguyễn Văn A", "001090012345", "dl@x.vn", "024 3933 1122", true, "kế toán");
            Assert.True(ok);
            var list = await svc.DealersAsync(null, null);
            Assert.Single(list);
            Assert.Equal("Đại lý Hà Nội", list[0].DLName);
            Assert.Equal("01", list[0].ProvinceCode);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task Dealer_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            var (_, _, id) = await svc.SaveDealerAsync(null, "DL001", "Đại lý Hà Nội", "01", null, null, null, null, null, true, null);
            // Lưu lại cùng mã đại lý = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveDealerAsync(null, "DL001", "Đại lý Hà Nội (mới)", "01", null, null, null, null, null, false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.DealersAsync(null, null);
            Assert.Single(list);
            Assert.Equal("Đại lý Hà Nội (mới)", list[0].DLName);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task Dealer_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            var (ok, msg, _) = await svc.SaveDealerAsync(null, "DL001", "  ", "01", null, null, null, null, null, true, null);
            Assert.False(ok);
            Assert.Contains("tên", msg);
        }
    }

    [Fact]
    public async Task Dealer_Save_UnknownProvince_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveDealerAsync(null, "DL001", "Đại lý X", "99", null, null, null, null, null, true, null);
            Assert.False(ok);
            Assert.Contains("Tỉnh", msg);
        }
    }

    [Fact]
    public async Task Dealer_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveProvinceAsync(null, "01", "Hà Nội", true, null);
            var (_, _, id) = await svc.SaveDealerAsync(null, "DL001", "Đại lý Hà Nội", "01", null, null, null, null, null, true, null);
            var (ok, _) = await svc.DeleteDealerAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.DealersAsync(null, null));
        }
    }

    // Danh mục Phòng ban (theo Mst_Department của TVAN gốc).
    [Fact]
    public async Task Department_Save_ComputesBuCodeAndLevel()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.SaveDepartmentAsync(null, "HO", null, "0101243150", "Hội sở", true, null);
            await svc.SaveDepartmentAsync(null, "KT", "HO", "0101243150", "Phòng Kế toán", true, null);
            await svc.SaveDepartmentAsync(null, "KT1", "KT", "0101243150", "Bộ phận KT1", true, null);

            var list = await svc.DepartmentsAsync("0101243150", null);
            var ho = list.First(d => d.DepartmentCode == "HO");
            var kt = list.First(d => d.DepartmentCode == "KT");
            var kt1 = list.First(d => d.DepartmentCode == "KT1");
            Assert.Equal("HO", ho.DepartmentBUCode);
            Assert.Equal(1, ho.DepartmentLevel);
            Assert.Equal("HO.KT", kt.DepartmentBUCode);
            Assert.Equal(2, kt.DepartmentLevel);
            Assert.Equal("HO.KT.KT1", kt1.DepartmentBUCode);
            Assert.Equal(3, kt1.DepartmentLevel);
        }
    }

    [Fact]
    public async Task Department_Save_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var (ok, msg, _) = await svc.SaveDepartmentAsync(null, "  ", null, "0101243150", "Phòng X", true, null);
            Assert.False(ok);
            Assert.Contains("mã phòng ban", msg);
        }
    }

    [Fact]
    public async Task Department_Save_UnknownMst_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveDepartmentAsync(null, "KT", null, "9999999999", "Phòng Kế toán", true, null);
            Assert.False(ok);
            Assert.Contains("MST", msg);
        }
    }

    [Fact]
    public async Task Department_Save_UnknownParent_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var (ok, msg, _) = await svc.SaveDepartmentAsync(null, "KT", "XX", "0101243150", "Phòng Kế toán", true, null);
            Assert.False(ok);
            Assert.Contains("cha", msg);
        }
    }

    [Fact]
    public async Task Department_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var (_, _, id) = await svc.SaveDepartmentAsync(null, "KT", null, "0101243150", "Phòng Kế toán", true, null);
            var (ok, _) = await svc.DeleteDepartmentAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.DepartmentsAsync(null, null));
        }
    }

    [Fact]
    public async Task OrgCks_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveOrgCksAsync(null, "1234567890", "VNPT-CA", "CN=Cty", null, null, "/keys/a.p12", null, true, "kế toán");
            Assert.True(ok);
            var list = await svc.OrgCksesAsync(null);
            Assert.Single(list);
            Assert.Equal("1234567890", list[0].CANumber);
            Assert.Equal(id, list[0].Id);
        }
    }

    [Fact]
    public async Task OrgCks_Save_SameNumber_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveOrgCksAsync(null, "1234567890", "VNPT-CA", "CN=Cty", null, null, null, null, true, null);
            // Lưu lại cùng số chứng thư = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveOrgCksAsync(null, "1234567890", "VIETTEL-CA", "CN=Cty mới", null, null, null, null, false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.OrgCksesAsync(null);
            Assert.Single(list);
            Assert.Equal("VIETTEL-CA", list[0].CAOrg);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task OrgCks_Save_MissingNumber_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveOrgCksAsync(null, "  ", "VNPT-CA", null, null, null, null, null, true, null);
            Assert.False(ok);
            Assert.Contains("chứng thư", msg);
        }
    }

    [Fact]
    public async Task OrgCks_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveOrgCksAsync(null, "1234567890", "VNPT-CA", null, null, null, null, null, true, null);
            var (ok, _) = await svc.DeleteOrgCksAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.OrgCksesAsync(null));
        }
    }

    [Fact]
    public async Task NotifyType_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveNotifyTypeAsync(null, "NOTIFY_ISSUED", "Thông báo phát hành hóa đơn", true, true, "quản trị");
            Assert.True(ok);
            var list = await svc.NotifyTypesAsync(null);
            Assert.Single(list);
            Assert.Equal("NOTIFY_ISSUED", list[0].NotifyTypeCode);
            Assert.Equal(id, list[0].Id);
            Assert.True(list[0].DefaultActive);
        }
    }

    [Fact]
    public async Task NotifyType_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveNotifyTypeAsync(null, "NOTIFY_ISSUED", "Mô tả cũ", true, true, null);
            // Lưu lại cùng mã loại = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveNotifyTypeAsync(null, "NOTIFY_ISSUED", "Mô tả mới", false, false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.NotifyTypesAsync(null);
            Assert.Single(list);
            Assert.Equal("Mô tả mới", list[0].NotifyDesc);
            Assert.False(list[0].DefaultActive);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task NotifyType_Save_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveNotifyTypeAsync(null, "  ", "Mô tả", true, true, null);
            Assert.False(ok);
            Assert.Contains("loại thông báo", msg);
        }
    }

    [Fact]
    public async Task NotifyType_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveNotifyTypeAsync(null, "NOTIFY_TCT", "Thông báo CQT", true, true, null);
            var (ok, _) = await svc.DeleteNotifyTypeAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.NotifyTypesAsync(null));
        }
    }

    [Fact]
    public async Task Notify_Create_Valid()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.CreateNotifyAsync("TB2026-001", "Bảo trì hệ thống", DateTime.Today, DateTime.Today.AddDays(7), true, "quản trị");
            Assert.True(ok);
            var list = await svc.NotifiesAsync(null);
            Assert.Single(list);
            Assert.Equal("TB2026-001", list[0].NotifyNo);
            Assert.Equal(id, list[0].Id);
            Assert.True(list[0].FlagSendEmail);
        }
    }

    [Fact]
    public async Task Notify_Create_DuplicateNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNotifyAsync("TB2026-001", "A", DateTime.Today, DateTime.Today.AddDays(1), false, null);
            var (ok, msg, _) = await svc.CreateNotifyAsync("TB2026-001", "B", DateTime.Today, DateTime.Today.AddDays(1), false, null);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task Notify_Create_StartAfterEnd_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateNotifyAsync("TB2026-002", "X", DateTime.Today.AddDays(5), DateTime.Today.AddDays(1), false, null);
            Assert.False(ok);
            Assert.Contains("sau hiệu lực kết thúc", msg);
        }
    }

    [Fact]
    public async Task Notify_Create_StartBeforeToday_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateNotifyAsync("TB2026-003", "X", DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), false, null);
            Assert.False(ok);
            Assert.Contains("trước ngày hiện tại", msg);
        }
    }

    [Fact]
    public async Task Notify_AddRecipient_And_MarkRead()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateNotifyAsync("TB2026-004", "Bảo trì", DateTime.Today, DateTime.Today.AddDays(3), false, null);
            var (ok, _, _) = await svc.AddNotifyDtlAsync(id, "ketoan01", false, null);
            Assert.True(ok);
            // Gửi trùng cho cùng người dùng bị chặn.
            var (dup, dupMsg, _) = await svc.AddNotifyDtlAsync(id, "ketoan01", false, null);
            Assert.False(dup);
            Assert.Contains("đã nhận", dupMsg);
            // Đánh dấu đã đọc.
            var (readOk, _) = await svc.MarkNotifyReadAsync(id, "ketoan01");
            Assert.True(readOk);
            var n = await svc.GetNotifyAsync(id);
            Assert.True(n!.Details.Single(d => d.UserCode == "ketoan01").FlagRead);
        }
    }

    [Fact]
    public async Task Notify_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateNotifyAsync("TB2026-005", "X", DateTime.Today, DateTime.Today.AddDays(1), false, null);
            var (ok, _) = await svc.DeleteNotifyAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.NotifiesAsync(null));
        }
    }

    [Fact]
    public async Task NotifyRecipient_Create_AutoRegistersAllTypes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveNotifyTypeAsync(null, "NOTIFY_ISSUED", "Phát hành", true, true, null);
            await svc.SaveNotifyTypeAsync(null, "NOTIFY_TCT", "CQT", false, true, null);
            var (ok, _, id) = await svc.CreateNotifyRecipientAsync("ketoan01", "Nguyễn Văn A", "quản trị");
            Assert.True(ok);
            var r = await svc.GetNotifyRecipientAsync(id);
            Assert.NotNull(r);
            Assert.Equal(2, r!.Types.Count);
            // Cờ mặc định lấy từ NotifyType.DefaultActive.
            Assert.True(r.Types.Single(t => t.NotifyType == "NOTIFY_ISSUED").FlagNotify);
            Assert.False(r.Types.Single(t => t.NotifyType == "NOTIFY_TCT").FlagNotify);
        }
    }

    [Fact]
    public async Task NotifyRecipient_Create_DuplicateCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNotifyRecipientAsync("ketoan01", "A", null);
            var (ok, msg, _) = await svc.CreateNotifyRecipientAsync("ketoan01", "B", null);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task NotifyRecipient_Create_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateNotifyRecipientAsync("  ", "A", null);
            Assert.False(ok);
            Assert.Contains("Cần mã người dùng", msg);
        }
    }

    [Fact]
    public async Task NotifyRecipient_SaveTypes_Replaces()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveNotifyTypeAsync(null, "NOTIFY_ISSUED", "Phát hành", true, true, null);
            await svc.SaveNotifyTypeAsync(null, "NOTIFY_TCT", "CQT", false, true, null);
            var (_, _, id) = await svc.CreateNotifyRecipientAsync("ketoan01", "A", null);
            // Đổi: tắt NOTIFY_ISSUED, bật NOTIFY_TCT.
            var (ok, _) = await svc.SaveNotifyRecipientTypesAsync(id, new() { ("NOTIFY_ISSUED", false), ("NOTIFY_TCT", true) }, "quản trị");
            Assert.True(ok);
            var r = await svc.GetNotifyRecipientAsync(id);
            Assert.False(r!.Types.Single(t => t.NotifyType == "NOTIFY_ISSUED").FlagNotify);
            Assert.True(r.Types.Single(t => t.NotifyType == "NOTIFY_TCT").FlagNotify);
        }
    }

    [Fact]
    public async Task NotifyRecipient_Delete_RemovesTypes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveNotifyTypeAsync(null, "NOTIFY_ISSUED", "Phát hành", true, true, null);
            var (_, _, id) = await svc.CreateNotifyRecipientAsync("ketoan01", "A", null);
            var (ok, _) = await svc.DeleteNotifyRecipientAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.NotifyRecipientsAsync(null));
            Assert.Empty(await db.NotifyRecipientTypes.ToListAsync());
        }
    }

    [Fact]
    public async Task SysGroup_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveSysGroupAsync(null, "KETOAN", "Nhóm kế toán", true, "quản trị");
            Assert.True(ok);
            var g = await svc.GetSysGroupAsync(id);
            Assert.NotNull(g);
            Assert.Equal("KETOAN", g!.GroupCode);
            Assert.Equal("Nhóm kế toán", g.GroupName);
            Assert.True(g.FlagActive);
        }
    }

    [Fact]
    public async Task SysGroup_Save_DuplicateCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSysGroupAsync(null, "KETOAN", "A", true, null);
            var (ok, msg, _) = await svc.SaveSysGroupAsync(null, "KETOAN", "B", true, null);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task SysGroup_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveSysGroupAsync(null, "KETOAN", "  ", true, null);
            Assert.False(ok);
            Assert.Contains("Cần tên nhóm", msg);
        }
    }

    [Fact]
    public async Task SysGroup_SaveMembers_Replaces()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSysGroupAsync(null, "KETOAN", "Nhóm kế toán", true, null);
            var (ok, _) = await svc.SaveSysGroupMembersAsync(id, new() { "ketoan01", "ketoan02" }, "quản trị");
            Assert.True(ok);
            var g = await svc.GetSysGroupAsync(id);
            Assert.Equal(2, g!.Members.Count);
            // Lưu lại thay thế toàn bộ danh sách.
            await svc.SaveSysGroupMembersAsync(id, new() { "ketoan03" }, "quản trị");
            g = await svc.GetSysGroupAsync(id);
            Assert.Single(g!.Members);
            Assert.Equal("ketoan03", g.Members[0].UserCode);
        }
    }

    [Fact]
    public async Task SysGroup_Delete_RemovesMembers()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSysGroupAsync(null, "KETOAN", "Nhóm kế toán", true, null);
            await svc.SaveSysGroupMembersAsync(id, new() { "ketoan01" }, null);
            var (ok, _) = await svc.DeleteSysGroupAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.SysGroupsAsync(null));
            Assert.Empty(await db.SysUserInGroups.ToListAsync());
        }
    }

    [Fact]
    public async Task TvanInteg_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveTvanIntegAsync(null, "0101243150", "0101243150", "0101243150", "quản trị");
            Assert.True(ok);
            var t = await svc.GetTvanIntegAsync(id);
            Assert.NotNull(t);
            Assert.Equal("0101243150", t!.OrgCode);
            Assert.Equal("0101243150", t.MsttctnIn);
            Assert.True(t.FlagActive);
        }
    }

    [Fact]
    public async Task TvanInteg_Save_DuplicateOrg_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveTvanIntegAsync(null, "0101243150", "0101243150", null, null);
            var (ok, msg, _) = await svc.SaveTvanIntegAsync(null, "0101243150", "0101243150", null, null);
            Assert.False(ok);
            Assert.Contains("đã có cấu hình", msg);
        }
    }

    [Fact]
    public async Task TvanInteg_Save_MissingIn_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveTvanIntegAsync(null, "0101243150", "  ", null, null);
            Assert.False(ok);
            Assert.Contains("hóa đơn đầu vào", msg);
        }
    }

    [Fact]
    public async Task TvanInteg_Save_UpdatesSameOrg()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveTvanIntegAsync(null, "0101243150", "0101243150", null, null);
            var (ok, _, _) = await svc.SaveTvanIntegAsync(id, "0101243150", "0200000000", "0300000000", "quản trị");
            Assert.True(ok);
            var t = await svc.GetTvanIntegAsync(id);
            Assert.Equal("0200000000", t!.MsttctnIn);
            Assert.Equal("0300000000", t.MsttctnOut);
        }
    }

    [Fact]
    public async Task TvanInteg_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveTvanIntegAsync(null, "0101243150", "0101243150", null, null);
            var (ok, _) = await svc.DeleteTvanIntegAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.TvanIntegsAsync(null));
        }
    }

    [Fact]
    public async Task SysModule_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.SysSolutions.Add(new SysSolution { SolutionCode = "TVAN", SolutionName = "TVAN", FlagActive = true });
            await db.SaveChangesAsync();
            var (ok, _, id) = await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "TVAN", "Gói cơ bản", "dùng thử", 1000, 5000, true, "quản trị");
            Assert.True(ok);
            var m = await svc.GetSysModuleAsync(id);
            Assert.NotNull(m);
            Assert.Equal("TVAN_BASIC", m!.ModuleCode);
            Assert.Equal("TVAN", m.SolutionCode);
            Assert.Equal(1000, m.QtyInvoice);
            Assert.True(m.FlagActive);
        }
    }

    [Fact]
    public async Task SysModule_Save_DuplicateCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.SysSolutions.Add(new SysSolution { SolutionCode = "TVAN", SolutionName = "TVAN", FlagActive = true });
            await db.SaveChangesAsync();
            await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "TVAN", "A", null, 0, 0, true, null);
            var (ok, msg, _) = await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "TVAN", "B", null, 0, 0, true, null);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task SysModule_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.SysSolutions.Add(new SysSolution { SolutionCode = "TVAN", SolutionName = "TVAN", FlagActive = true });
            await db.SaveChangesAsync();
            var (ok, msg, _) = await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "TVAN", "  ", null, 0, 0, true, null);
            Assert.False(ok);
            Assert.Contains("Cần tên gói", msg);
        }
    }

    [Fact]
    public async Task SysModule_Save_UnknownSolution_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "KHONGCO", "Gói cơ bản", null, 0, 0, true, null);
            Assert.False(ok);
            Assert.Contains("không tồn tại", msg);
        }
    }

    [Fact]
    public async Task SysModule_SetActive_Toggles()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.SysSolutions.Add(new SysSolution { SolutionCode = "TVAN", SolutionName = "TVAN", FlagActive = true });
            await db.SaveChangesAsync();
            var (_, _, id) = await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "TVAN", "Gói cơ bản", null, 0, 0, true, null);
            var (ok, _) = await svc.SetSysModuleActiveAsync(id, false, "quản trị");
            Assert.True(ok);
            Assert.False((await svc.GetSysModuleAsync(id))!.FlagActive);
            await svc.SetSysModuleActiveAsync(id, true, "quản trị");
            Assert.True((await svc.GetSysModuleAsync(id))!.FlagActive);
        }
    }

    [Fact]
    public async Task SysModule_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.SysSolutions.Add(new SysSolution { SolutionCode = "TVAN", SolutionName = "TVAN", FlagActive = true });
            await db.SaveChangesAsync();
            var (_, _, id) = await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "TVAN", "Gói cơ bản", null, 0, 0, true, null);
            var (ok, _) = await svc.DeleteSysModuleAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.SysModulesAsync(null));
        }
    }

    [Fact]
    public async Task SysObject_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveSysObjectAsync(null, "INV_ISSUE", "Phát hành hóa đơn", "INVOICE", SysObjectType.Func, true, "quản trị");
            Assert.True(ok);
            var o = (await svc.SysObjectsAsync(null)).FirstOrDefault(x => x.Id == id);
            Assert.NotNull(o);
            Assert.Equal("INV_ISSUE", o!.ObjectCode);
            Assert.Equal(SysObjectType.Func, o.ObjectType);
            Assert.True(o.FlagActive);
        }
    }

    [Fact]
    public async Task SysObject_Save_DuplicateCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSysObjectAsync(null, "INV_ISSUE", "A", null, SysObjectType.Func, true, null);
            var (ok, msg, _) = await svc.SaveSysObjectAsync(null, "INV_ISSUE", "B", null, SysObjectType.Func, true, null);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task SysObject_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveSysObjectAsync(null, "INV_ISSUE", "  ", null, SysObjectType.Func, true, null);
            Assert.False(ok);
            Assert.Contains("Cần tên đối tượng", msg);
        }
    }

    [Fact]
    public async Task SysObjectInModules_Save_Replaces()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.SysSolutions.Add(new SysSolution { SolutionCode = "TVAN", SolutionName = "TVAN", FlagActive = true });
            await db.SaveChangesAsync();
            var (_, _, mid) = await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "TVAN", "Gói cơ bản", null, 0, 0, true, null);
            var (ok, _) = await svc.SaveSysObjectInModulesAsync(mid, new() { "INV_ISSUE", "INV_CANCEL" }, "quản trị");
            Assert.True(ok);
            Assert.Equal(2, (await svc.SysObjectInModulesAsync("TVAN_BASIC")).Count);
            // Lưu lại thay thế toàn bộ danh sách.
            await svc.SaveSysObjectInModulesAsync(mid, new() { "MENU_INVOICE" }, "quản trị");
            var maps = await svc.SysObjectInModulesAsync("TVAN_BASIC");
            Assert.Single(maps);
            Assert.Equal("MENU_INVOICE", maps[0].ObjectCode);
        }
    }

    [Fact]
    public async Task SysObjectInModules_Save_UnknownModule_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.SaveSysObjectInModulesAsync(9999, new() { "INV_ISSUE" }, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy gói Module", msg);
        }
    }

    [Fact]
    public async Task SysObject_Delete_RemovesMappings()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.SysSolutions.Add(new SysSolution { SolutionCode = "TVAN", SolutionName = "TVAN", FlagActive = true });
            await db.SaveChangesAsync();
            var (_, _, mid) = await svc.SaveSysModuleAsync(null, "TVAN_BASIC", "TVAN", "Gói cơ bản", null, 0, 0, true, null);
            var (_, _, oid) = await svc.SaveSysObjectAsync(null, "INV_ISSUE", "Phát hành", null, SysObjectType.Func, true, null);
            await svc.SaveSysObjectInModulesAsync(mid, new() { "INV_ISSUE" }, null);
            var (ok, _) = await svc.DeleteSysObjectAsync(oid);
            Assert.True(ok);
            Assert.Empty(await svc.SysObjectsAsync(null));
            Assert.Empty(await svc.SysObjectInModulesAsync(null));
        }
    }

    [Fact]
    public async Task ColumnConfig_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveColumnConfigAsync(null, "Invoice_Invoice", "InvoiceNo", "00000000", "Số hóa đơn", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetColumnConfigAsync(id);
            Assert.NotNull(e);
            Assert.Equal("Invoice_Invoice", e!.TableName);
            Assert.Equal("InvoiceNo", e.ColumnName);
            Assert.Equal("00000000", e.ColumnFormat);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task ColumnConfig_Save_SameKey_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveColumnConfigAsync(null, "Invoice_Invoice", "InvoiceNo", "00000000", "Số hóa đơn", true, "kế toán");
            // Lưu lại cùng (bảng, cột) = cập nhật, không tạo mới.
            var (ok, _, id2) = await svc.SaveColumnConfigAsync(null, "Invoice_Invoice", "InvoiceNo", "N0", "Số HĐ", false, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            var all = await svc.ColumnConfigsAsync(null, null);
            Assert.Single(all);
            Assert.Equal("N0", all[0].ColumnFormat);
            Assert.False(all[0].FlagActive);
        }
    }

    [Fact]
    public async Task ColumnConfig_Save_MissingTableOrColumn_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveColumnConfigAsync(null, "  ", "InvoiceNo", null, null, true, null);
            Assert.False(ok1);
            Assert.Contains("tên bảng", msg1);
            var (ok2, msg2, _) = await svc.SaveColumnConfigAsync(null, "Invoice_Invoice", "  ", null, null, true, null);
            Assert.False(ok2);
            Assert.Contains("tên cột", msg2);
        }
    }

    [Fact]
    public async Task ColumnConfig_Filter_ByTableAndKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveColumnConfigAsync(null, "Invoice_Invoice", "InvoiceNo", null, "Số hóa đơn", true, null);
            await svc.SaveColumnConfigAsync(null, "Invoice_Invoice", "InvoiceDateUTC", null, "Ngày hóa đơn", true, null);
            await svc.SaveColumnConfigAsync(null, "Mst_NNT", "MST", null, "Mã số thuế", true, null);
            Assert.Equal(2, (await svc.ColumnConfigsAsync("Invoice_Invoice", null)).Count);
            Assert.Single(await svc.ColumnConfigsAsync(null, "Ngày"));
        }
    }

    [Fact]
    public async Task ColumnConfig_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveColumnConfigAsync(null, "Invoice_Invoice", "InvoiceNo", null, null, true, null);
            var (ok, _) = await svc.DeleteColumnConfigAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.ColumnConfigsAsync(null, null));
        }
    }

    // Danh mục cơ quan thuế (theo Mst_GovTaxID của TVAN gốc):
    // tạo mới/cập nhật theo mã, tự tính mã đơn vị nghiệp vụ/cấp từ cây, xóa.
    [Fact]
    public async Task TaxOffice_Save_CreatesAndRecomputesBu()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (okRoot, _, _) = await svc.SaveTaxOfficeAsync(null, "0100231226", null, null, null, "Tổng cục Thuế", "0", null, null, null, true, "quản trị");
            Assert.True(okRoot);
            var (okChild, _, _) = await svc.SaveTaxOfficeAsync(null, "0101", "0100231226", null, null, "Cục Thuế TP Hà Nội", "1", null, null, null, true, "quản trị");
            Assert.True(okChild);

            var offices = await svc.TaxOfficesAsync();
            var root = offices.First(t => t.GovTaxID == "0100231226");
            var child = offices.First(t => t.GovTaxID == "0101");
            Assert.Equal("0100231226", root.GovTaxIDBUCode);
            Assert.Equal(0, root.GovTaxIDLevel);
            Assert.Equal("0100231226.0101", child.GovTaxIDBUCode);
            Assert.Equal(1, child.GovTaxIDLevel);
        }
    }

    [Fact]
    public async Task TaxOffice_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveTaxOfficeAsync(null, "  ", null, null, null, "Cục Thuế", null, null, null, null, true, null);
            Assert.False(ok1);
            Assert.Contains("mã cơ quan thuế", msg1);
            var (ok2, msg2, _) = await svc.SaveTaxOfficeAsync(null, "0101", null, null, null, "  ", null, null, null, null, true, null);
            Assert.False(ok2);
            Assert.Contains("tên cơ quan thuế", msg2);
        }
    }

    [Fact]
    public async Task TaxOffice_Save_SameCode_UpdatesExisting()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveTaxOfficeAsync(null, "0101", null, null, null, "Cục Thuế TP Hà Nội", null, null, null, null, true, null);
            // Lưu lại cùng mã = cập nhật (theo khóa nghiệp vụ OrgId + mã CQT).
            var (ok, _, id2) = await svc.SaveTaxOfficeAsync(null, "0101", null, null, null, "Cục Thuế TP Hà Nội (đổi tên)", null, null, null, null, true, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var offices = await svc.TaxOfficesAsync();
            Assert.Single(offices);
            Assert.Equal("Cục Thuế TP Hà Nội (đổi tên)", offices[0].GovTaxName);
        }
    }

    [Fact]
    public async Task TaxOffice_Save_ParentNotFound_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveTaxOfficeAsync(null, "0101", "9999", null, null, "Cục Thuế TP Hà Nội", null, null, null, null, true, null);
            Assert.False(ok);
            Assert.Contains("cấp trên không tồn tại", msg);
        }
    }

    [Fact]
    public async Task TaxOffice_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveTaxOfficeAsync(null, "0101", null, null, null, "Cục Thuế TP Hà Nội", null, null, null, null, true, null);
            var (ok, _) = await svc.DeleteTaxOfficeAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.TaxOfficesAsync());
        }
    }

    [Fact]
    public async Task DynamicComma_Default_IsComma()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var c = await svc.GetDynamicCommaAsync();
            Assert.Equal(DynamicCommaStyle.Comma, c.FlagStyle);
        }
    }

    [Fact]
    public async Task SetDynamicComma_UpdatesStyle()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.SetDynamicCommaAsync(DynamicCommaStyle.Dot, "kế toán");
            Assert.True(ok);
            Assert.Contains("dấu chấm", msg);
            var c = await svc.GetDynamicCommaAsync();
            Assert.Equal(DynamicCommaStyle.Dot, c.FlagStyle);
            Assert.Equal("kế toán", c.UpdatedBy);
        }
    }

    [Fact]
    public async Task SetDynamicComma_ReusesSingleRecord()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SetDynamicCommaAsync(DynamicCommaStyle.Dot, "a");
            await svc.SetDynamicCommaAsync(DynamicCommaStyle.Comma, "b");
            // Mỗi tổ chức chỉ có một bản ghi cấu hình dấu phân cách.
            Assert.Single(await db.DynamicCommas.ToListAsync());
            Assert.Equal(DynamicCommaStyle.Comma, (await svc.GetDynamicCommaAsync()).FlagStyle);
        }
    }

    // Cột hiển thị danh sách hóa đơn theo tổ chức (theo Mst_SortColumnInvoice của TVAN gốc):
    // tạo mới/cập nhật theo mã cột, sắp theo thứ tự hiển thị, xóa.
    [Fact]
    public async Task SortColumn_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveSortColumnInvoiceAsync(null, "InvoiceNo", 1, "Số hóa đơn", SortColumnType.Text, true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetSortColumnInvoiceAsync(id);
            Assert.NotNull(e);
            Assert.Equal("InvoiceNo", e!.ColumnCode);
            Assert.Equal(1, e.Idx);
            Assert.Equal(SortColumnType.Text, e.ColumnType);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task SortColumn_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSortColumnInvoiceAsync(null, "InvoiceNo", 1, "Số hóa đơn", SortColumnType.Text, true, "kế toán");
            // Lưu lại cùng mã cột = cập nhật, không tạo mới.
            var (ok, _, id2) = await svc.SaveSortColumnInvoiceAsync(null, "InvoiceNo", 5, "Số HĐ", SortColumnType.Number, false, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            var all = await svc.SortColumnInvoicesAsync(null);
            Assert.Single(all);
            Assert.Equal(5, all[0].Idx);
            Assert.Equal(SortColumnType.Number, all[0].ColumnType);
            Assert.False(all[0].FlagActive);
        }
    }

    [Fact]
    public async Task SortColumn_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveSortColumnInvoiceAsync(null, "  ", 1, "Số hóa đơn", SortColumnType.Text, true, null);
            Assert.False(ok1);
            Assert.Contains("mã cột", msg1);
            var (ok2, msg2, _) = await svc.SaveSortColumnInvoiceAsync(null, "InvoiceNo", 1, "  ", SortColumnType.Text, true, null);
            Assert.False(ok2);
            Assert.Contains("tên hiển thị", msg2);
        }
    }

    [Fact]
    public async Task SortColumn_List_OrderedByIdxAndFiltered()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSortColumnInvoiceAsync(null, "TotalValPmt", 4, "Tổng tiền thanh toán", SortColumnType.Number, true, null);
            await svc.SaveSortColumnInvoiceAsync(null, "InvoiceNo", 1, "Số hóa đơn", SortColumnType.Text, true, null);
            await svc.SaveSortColumnInvoiceAsync(null, "InvoiceDateUTC", 2, "Ngày hóa đơn", SortColumnType.Date, true, null);
            var all = await svc.SortColumnInvoicesAsync(null);
            Assert.Equal(new[] { "InvoiceNo", "InvoiceDateUTC", "TotalValPmt" }, all.Select(c => c.ColumnCode).ToArray());
            Assert.Single(await svc.SortColumnInvoicesAsync("Ngày"));
        }
    }

    [Fact]
    public async Task SortColumn_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSortColumnInvoiceAsync(null, "InvoiceNo", 1, "Số hóa đơn", SortColumnType.Text, true, null);
            var (ok, _) = await svc.DeleteSortColumnInvoiceAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.SortColumnInvoicesAsync(null));
        }
    }

    // ===== Tạo mới / cập nhật / xóa mẫu hóa đơn (theo Invoice_TempInvoice_Save của TVAN gốc) =====

    [Fact]
    public async Task SaveTemplate_Create_NewDraftWithEmptyRange()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (ok, msg, id) = await svc.SaveTemplateAsync(null, "TINV-1C26TAE", nntId, "Hóa đơn GTGT 1C26TAE", "1C26TAE", "K26TAE", InvoiceNoRule.TT78, "tạo mẫu", "kế toán");
            Assert.True(ok);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == id);
            Assert.Equal("TINV-1C26TAE", tpl.TInvoiceCode);
            Assert.Equal("1C26TAE", tpl.FormNo);
            Assert.Equal("K26TAE", tpl.Sign);
            Assert.Equal(InvoiceNoRule.TT78, tpl.TTType);
            Assert.Equal(TemplateStatus.Draft, tpl.TInvoiceStatus);
            Assert.True(tpl.FlagActive);
            Assert.Equal(0, tpl.StartInvoiceNo);   // dải số rỗng chờ cấp phát
            Assert.Equal(0, tpl.EndInvoiceNo);
        }
    }

    [Fact]
    public async Task SaveTemplate_SameCode_UpdatesDraft()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (_, _, id) = await svc.SaveTemplateAsync(null, "TINV-1C26TAE", nntId, "Mẫu A", "1C26TAE", "K26TAE", InvoiceNoRule.TT78, null, null);
            // Lưu lại cùng mã = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveTemplateAsync(null, "TINV-1C26TAE", nntId, "Mẫu B", "1C26TAF", "K26TAF", InvoiceNoRule.TT68, null, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Equal(1, await db.InvoiceTemplates.CountAsync());
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == id);
            Assert.Equal("Mẫu B", tpl.TInvoiceName);
            Assert.Equal("1C26TAF", tpl.FormNo);
            Assert.Equal(InvoiceNoRule.TT68, tpl.TTType);
        }
    }

    [Fact]
    public async Task SaveTemplate_UpdateNonDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);   // đã Issued
            var (ok, msg, _) = await svc.SaveTemplateAsync(tplId, "TINV-1C26TAA", nntId, "Mẫu sửa", "1C26TAA", "K26TAA", InvoiceNoRule.TT78, null, null);
            Assert.False(ok);
            Assert.Contains("trạng thái chờ", msg);
        }
    }

    [Fact]
    public async Task SaveTemplate_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (ok, msg, _) = await svc.SaveTemplateAsync(null, "  ", nntId, "Mẫu", "1C26TAE", "K26TAE", InvoiceNoRule.TT78, null, null);
            Assert.False(ok);
            Assert.Contains("Mã mẫu", msg);
            Assert.Equal(0, await db.InvoiceTemplates.CountAsync());
        }
    }

    [Fact]
    public async Task SaveTemplate_MissingFormNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (ok, msg, _) = await svc.SaveTemplateAsync(null, "TINV-1C26TAE", nntId, "Mẫu", "  ", "K26TAE", InvoiceNoRule.TT78, null, null);
            Assert.False(ok);
            Assert.Contains("Mẫu số", msg);
        }
    }

    [Fact]
    public async Task SaveTemplate_UnknownNnt_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveTemplateAsync(null, "TINV-1C26TAE", 9999, "Mẫu", "1C26TAE", "K26TAE", InvoiceNoRule.TT78, null, null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy người nộp thuế", msg);
        }
    }

    [Fact]
    public async Task DeleteTemplate_DraftUnused_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var (_, _, id) = await svc.SaveTemplateAsync(null, "TINV-1C26TAE", nntId, "Mẫu", "1C26TAE", "K26TAE", InvoiceNoRule.TT78, null, null);
            var (ok, msg) = await svc.DeleteTemplateAsync(id);
            Assert.True(ok);
            Assert.Equal(0, await db.InvoiceTemplates.CountAsync());
            var (ok2, msg2) = await svc.DeleteTemplateAsync(id);
            Assert.False(ok2);
            Assert.Contains("Không tìm thấy", msg2);
        }
    }

    [Fact]
    public async Task DeleteTemplate_NonDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddTemplate(db, nntId);   // đã Issued
            var (ok, msg) = await svc.DeleteTemplateAsync(tplId);
            Assert.False(ok);
            Assert.Contains("trạng thái chờ", msg);
        }
    }

    [Fact]
    public async Task DeleteTemplate_UsedRange_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (nntId, _) = await Setup(svc);
            var tplId = await AddDraftTemplate(db, nntId);
            var tpl = await db.InvoiceTemplates.FirstAsync(t => t.Id == tplId);
            tpl.QtyUsed = 5; await db.SaveChangesAsync();
            var (ok, msg) = await svc.DeleteTemplateAsync(tplId);
            Assert.False(ok);
            Assert.Contains("đã dùng số", msg);
        }
    }

    // Gửi thông báo hóa đơn sai sót tới CQT (theo Invoice_Invoice_SentTCT_300 của TVAN gốc).
    // Đưa HĐ về trạng thái Accepted (đã được CQT chấp nhận, có mã tra cứu) để gửi thông báo 300.
    private static async Task<int> SetupAccepted(AppDbContext db, ITvanService svc)
    {
        var (_, invId) = await Setup(svc);
        var inv = await db.Invoices.FirstAsync(i => i.Id == invId);
        inv.Status = InvoiceStatus.Accepted;
        inv.TctCode = "0026082512345678";
        inv.SentAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return invId;
    }

    [Fact]
    public async Task SendTct300_Accepted_SetsRefAndFlagAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupAccepted(db, svc);
            var (ok, msg, tctRefNo) = await svc.SendTct300Async(invId, ReplaceOrAdjustFlag.Adjust, "1", "04/SS", DateTime.Today, "Sai MST người mua", "kế toán");
            Assert.True(ok);
            Assert.False(string.IsNullOrWhiteSpace(tctRefNo));
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(tctRefNo, inv!.TCTSuaDoiRefNo);
            Assert.Equal(ReplaceOrAdjustFlag.Adjust, inv.FlagReplaceOrAdjust);
            Assert.Equal(SuaDoiFlag.Sent, inv.FlagSuaDoi);
            var logs = await svc.Tct300LogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(tctRefNo, logs[0].TCTRefNo);
            Assert.Equal(ReplaceOrAdjustFlag.Adjust, logs[0].FlagReplaceOrAdjust);
            Assert.Equal("Sai MST người mua", logs[0].LyDo);
        }
    }

    [Fact]
    public async Task SendTct300_NotAccepted_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);   // HĐ đang Draft
            var (ok, msg, _) = await svc.SendTct300Async(invId, ReplaceOrAdjustFlag.Replace, null, null, null, "Sai sót", null);
            Assert.False(ok);
            Assert.Contains("chấp nhận", msg);
        }
    }

    [Fact]
    public async Task SendTct300_MissingReason_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupAccepted(db, svc);
            var (ok, msg, _) = await svc.SendTct300Async(invId, ReplaceOrAdjustFlag.Adjust, null, null, null, "  ", null);
            Assert.False(ok);
            Assert.Contains("lý do", msg);
        }
    }

    [Fact]
    public async Task SendTct300_ReplaceFlag_Stored()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupAccepted(db, svc);
            var (ok, _, _) = await svc.SendTct300Async(invId, ReplaceOrAdjustFlag.Replace, "1", "04/SS", DateTime.Today, "Thay thế HĐ", "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(ReplaceOrAdjustFlag.Replace, inv!.FlagReplaceOrAdjust);
        }
    }
}

// Đọc tiền bằng chữ (theo luồng DocTien của TVAN gốc) + danh mục tiền tệ (Mst_CurrencyEx).
public class DocTienTests
{
    private static (AppDbContext db, ITvanService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new TvanService(db), conn);
    }

    [Fact]
    public void DocSo_Zero_ReturnsKhongDong()
    {
        var text = DocTienService.DocSo("0", "VND", "đồng");
        Assert.Equal("Không đồng./.", text);
    }

    [Fact]
    public void DocSo_Simple_ReturnsWords()
    {
        var text = DocTienService.DocSo("1500000", "VND", "đồng");
        Assert.Equal("Một triệu năm trăm nghìn đồng./.", text);
    }

    [Fact]
    public void DocSo_WithDecimals_ReadsDigits()
    {
        var text = DocTienService.DocSo("1234.56", "VND", "đồng");
        Assert.Contains("phẩy năm sáu", text);
        Assert.EndsWith("đồng./.", text);
    }

    [Fact]
    public async Task DocTien_DefaultCurrency_LogsAndReturnsText()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, text, id) = await svc.DocTienAsync(2_000_000, null, "kế toán");
            Assert.True(ok);
            Assert.Equal("Hai triệu đồng./.", text);
            Assert.True(id > 0);
            var logs = await svc.DocTienLogsAsync();
            Assert.Single(logs);
            Assert.Equal("VND", logs[0].CurrencyCode);
        }
    }

    [Fact]
    public async Task DocTien_UsdCurrency_UsesCatalogName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveCurrencyExAsync(null, "USD", "đô la Mỹ", "VND", 25400, 25600, null, true, "kế toán");
            var (ok, _, text, _) = await svc.DocTienAsync(100, "USD", "kế toán");
            Assert.True(ok);
            Assert.Contains("đô la Mỹ", text);
        }
    }

    [Fact]
    public async Task SaveCurrencyEx_SameCode_UpsertsNotDuplicate()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveCurrencyExAsync(null, "VND", "đồng", null, 1, 1, null, true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveCurrencyExAsync(null, "VND", "đồng Việt Nam", null, 1, 1, null, true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.CurrencyExesAsync(null));
        }
    }

    [Fact]
    public async Task SaveCurrencyEx_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveCurrencyExAsync(null, "VND", "  ", null, 1, 1, null, true, "kế toán");
            Assert.False(ok);
            Assert.Contains("tên tiền tệ", msg);
        }
    }

    [Fact]
    public async Task SaveCurrencyEx_UpdateSameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveCurrencyExAsync(null, "VND", "đồng", null, 1, 1, null, true, "kế toán");
            var (ok, _, id2) = await svc.SaveCurrencyExAsync(null, "VND", "Việt Nam đồng", null, 1, 1, "cập nhật", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            var e = await svc.GetCurrencyExAsync(id);
            Assert.Equal("Việt Nam đồng", e!.CurrencyName);
        }
    }

    [Fact]
    public async Task DeleteCurrencyEx_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveCurrencyExAsync(null, "JPY", "yên Nhật", null, 1, 1, null, true, "kế toán");
            var (ok, _) = await svc.DeleteCurrencyExAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetCurrencyExAsync(id));
        }
    }

    [Fact]
    public async Task Unit_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveUnitAsync(null, "CAI", "Cái", "Đơn vị tính hàng hóa", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetUnitAsync(id);
            Assert.Equal("CAI", e!.UnitCode);
            Assert.Equal("Cái", e.UnitName);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task Unit_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveUnitAsync(null, "CAI", "Cái", null, true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveUnitAsync(null, "CAI", "Cái (bộ)", "cập nhật", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.UnitsAsync(null));
            Assert.Equal("Cái (bộ)", (await svc.GetUnitAsync(id))!.UnitName);
        }
    }

    [Fact]
    public async Task Unit_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveUnitAsync(null, "  ", "Cái", null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã đơn vị tính", msg1);
            var (ok2, msg2, _) = await svc.SaveUnitAsync(null, "CAI", "  ", null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("tên đơn vị tính", msg2);
        }
    }

    [Fact]
    public async Task Unit_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CAI", "Cái", null, true, "kế toán");
            await svc.SaveUnitAsync(null, "KG", "Kilôgam", null, true, "kế toán");
            var all = await svc.UnitsAsync(null);
            Assert.Equal(2, all.Count);
            var filtered = await svc.UnitsAsync("KG");
            Assert.Single(filtered);
            Assert.Equal("KG", filtered[0].UnitCode);
        }
    }

    [Fact]
    public async Task Unit_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveUnitAsync(null, "HOP", "Hộp", null, true, "kế toán");
            var (ok, _) = await svc.DeleteUnitAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetUnitAsync(id));
        }
    }

    [Fact]
    public async Task InvoiceDtlType_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveInvoiceDtlTypeAsync(null, "GOODS", "Hàng hóa / dịch vụ", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetInvoiceDtlTypeAsync(id);
            Assert.Equal("GOODS", e!.InvoiceDtlTypeCode);
            Assert.Equal("Hàng hóa / dịch vụ", e.Desc);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task InvoiceDtlType_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveInvoiceDtlTypeAsync(null, "GOODS", "Hàng hóa", true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveInvoiceDtlTypeAsync(null, "GOODS", "Hàng hóa / dịch vụ", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.InvoiceDtlTypesAsync(null));
            Assert.Equal("Hàng hóa / dịch vụ", (await svc.GetInvoiceDtlTypeAsync(id))!.Desc);
        }
    }

    [Fact]
    public async Task InvoiceDtlType_Save_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveInvoiceDtlTypeAsync(null, "  ", "Hàng hóa", true, "kế toán");
            Assert.False(ok);
            Assert.Contains("mã loại dòng", msg);
        }
    }

    [Fact]
    public async Task InvoiceDtlType_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveInvoiceDtlTypeAsync(null, "GOODS", "Hàng hóa / dịch vụ", true, "kế toán");
            await svc.SaveInvoiceDtlTypeAsync(null, "NOTES", "Dòng ghi chú", true, "kế toán");
            var all = await svc.InvoiceDtlTypesAsync(null);
            Assert.Equal(2, all.Count);
            var filtered = await svc.InvoiceDtlTypesAsync("NOTES");
            Assert.Single(filtered);
            Assert.Equal("NOTES", filtered[0].InvoiceDtlTypeCode);
        }
    }

    [Fact]
    public async Task InvoiceDtlType_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveInvoiceDtlTypeAsync(null, "FEES", "Phí / lệ phí", true, "kế toán");
            var (ok, _) = await svc.DeleteInvoiceDtlTypeAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetInvoiceDtlTypeAsync(id));
        }
    }

    [Fact]
    public async Task Brand_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveBrandAsync(null, "SAMSUNG", "Samsung", "Thương hiệu điện tử", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetBrandAsync(id);
            Assert.Equal("SAMSUNG", e!.BrandCode);
            Assert.Equal("Samsung", e.BrandName);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task Brand_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveBrandAsync(null, "SONY", "Sony", null, true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveBrandAsync(null, "SONY", "Sony Việt Nam", "cập nhật", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.BrandsAsync(null));
            Assert.Equal("Sony Việt Nam", (await svc.GetBrandAsync(id))!.BrandName);
        }
    }

    [Fact]
    public async Task Brand_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveBrandAsync(null, "  ", "Samsung", null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã thương hiệu", msg1);
            var (ok2, msg2, _) = await svc.SaveBrandAsync(null, "SAMSUNG", "  ", null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("tên thương hiệu", msg2);
        }
    }

    [Fact]
    public async Task Brand_Delete_BlockedWhenModelExists()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, brandId) = await svc.SaveBrandAsync(null, "APPLE", "Apple", null, true, "kế toán");
            await svc.SaveProductModelAsync(null, "IP15", "iPhone 15", null, "APPLE", null, true, "kế toán");
            var (ok, msg) = await svc.DeleteBrandAsync(brandId);
            Assert.False(ok);
            Assert.Contains("model sản phẩm", msg);
        }
    }

    [Fact]
    public async Task ProductModel_Save_RequiresActiveBrand()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            // Thương hiệu chưa tồn tại → chặn.
            var (ok1, msg1, _) = await svc.SaveProductModelAsync(null, "A54", "Galaxy A54", null, "SAMSUNG", null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("không tồn tại", msg1);

            // Thương hiệu đã ngừng dùng → chặn.
            await svc.SaveBrandAsync(null, "SAMSUNG", "Samsung", null, false, "kế toán");
            var (ok2, msg2, _) = await svc.SaveProductModelAsync(null, "A54", "Galaxy A54", null, "SAMSUNG", null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("ngừng dùng", msg2);
        }
    }

    [Fact]
    public async Task ProductModel_Save_CreatesAndListsByBrand()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveBrandAsync(null, "SAMSUNG", "Samsung", null, true, "kế toán");
            await svc.SaveBrandAsync(null, "SONY", "Sony", null, true, "kế toán");
            var (ok, _, id) = await svc.SaveProductModelAsync(null, "A54", "Galaxy A54", "SS-A54", "SAMSUNG", "Điện thoại", true, "kế toán");
            Assert.True(ok);
            await svc.SaveProductModelAsync(null, "WH1000", "WH-1000XM5", null, "SONY", null, true, "kế toán");

            var e = await svc.GetProductModelAsync(id);
            Assert.Equal("A54", e!.ModelCode);
            Assert.Equal("SAMSUNG", e.BrandCode);
            Assert.Equal("SS-A54", e.OrgModelCode);

            Assert.Equal(2, (await svc.ProductModelsAsync(null, null)).Count);
            var samsung = await svc.ProductModelsAsync("SAMSUNG", null);
            Assert.Single(samsung);
            Assert.Equal("A54", samsung[0].ModelCode);
        }
    }

    [Fact]
    public async Task ProductModel_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveBrandAsync(null, "SONY", "Sony", null, true, "kế toán");
            var (_, _, id) = await svc.SaveProductModelAsync(null, "WH1000", "WH-1000XM5", null, "SONY", null, true, "kế toán");
            var (ok, _) = await svc.DeleteProductModelAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetProductModelAsync(id));
        }
    }
}
