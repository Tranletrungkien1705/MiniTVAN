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

    // Lưu hoa hồng đơn hàng với các giá trị mặc định (tránh lặp danh sách tham số dài trong test).
    private static Task<(bool ok, string msg, int id)> SaveComm(ITvanService svc, string orderNo, int? id = null)
        => svc.SaveLicOrderCommissionAsync(id, orderNo, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, null, null);

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

    // ===== Gửi lại email cho NHIỀU hóa đơn cùng lúc (theo Invoice_InvoiceController.ReSendEmail của TVAN gốc) =====

    [Fact]
    public async Task ReSendEmails_OnAccepted_LogsAllAndStamps()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.RegisterNntAsync(nntId);
            var (_, _, inv1) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 10_000_000 });
            var (_, _, inv2) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 20_000_000 });
            await svc.TransmitAsync(inv1);
            await svc.TransmitAsync(inv2);
            await svc.SendInvoiceEmailAsync(inv1, "a@congty.vn", "kế toán");
            await svc.SendInvoiceEmailAsync(inv2, "b@congty.vn", "kế toán");

            var (ok, msg, count) = await svc.ReSendEmailsAsync(new List<int> { inv1, inv2 }, "kế toán");
            Assert.True(ok);
            Assert.Equal(2, count);
            Assert.Contains("2 hóa đơn", msg);
            Assert.Equal(2, (await svc.EmailLogsAsync(inv1)).Count);
            Assert.Equal(2, (await svc.EmailLogsAsync(inv2)).Count);
            Assert.NotNull((await svc.GetInvoiceAsync(inv1))!.SendEmailDTimeUTC);
        }
    }

    [Fact]
    public async Task ReSendEmails_EmptyList_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.ReSendEmailsAsync(new List<int>(), "x");
            Assert.False(ok);
            Assert.Contains("ít nhất một", msg);
        }
    }

    [Fact]
    public async Task ReSendEmails_OnDraft_BlockedAllOrNothing()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.RegisterNntAsync(nntId);
            var (_, _, inv1) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 10_000_000 });
            var (_, _, inv2) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 20_000_000 });
            await svc.TransmitAsync(inv1);
            await svc.SendInvoiceEmailAsync(inv1, "a@congty.vn", "kế toán");
            // inv2 vẫn Draft → không gửi HĐ nào.
            var (ok, msg, count) = await svc.ReSendEmailsAsync(new List<int> { inv1, inv2 }, "x");
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Contains("chấp nhận", msg);
            Assert.Single(await svc.EmailLogsAsync(inv1));   // chỉ log cũ, không thêm
        }
    }

    [Fact]
    public async Task ReSendEmails_NoRecipient_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // Accepted nhưng chưa có EmailSend
            var (ok, msg, _) = await svc.ReSendEmailsAsync(new List<int> { invId }, "x");
            Assert.False(ok);
            Assert.Contains("email người nhận", msg);
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

    // Danh mục loại hóa đơn (theo Mst_InvoiceType của TVAN gốc).
    [Fact]
    public async Task InvoiceType_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveInvoiceTypeAsync(null, "GTGT", "Hóa đơn giá trị gia tăng", "Hóa đơn GTGT", InvoiceNoRule.TT78, true, "kế toán");
            Assert.True(ok);
            var list = await svc.InvoiceTypesAsync(null);
            Assert.Single(list);
            Assert.Equal("Hóa đơn giá trị gia tăng", list[0].InvoiceTypeName);
            Assert.Equal(InvoiceNoRule.TT78, list[0].TTType);
            Assert.True(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task InvoiceType_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveInvoiceTypeAsync(null, "GTGT", "Hóa đơn GTGT", null, InvoiceNoRule.TT78, true, null);
            // Lưu lại cùng mã loại = cập nhật (không tạo mới).
            var (ok, _, id2) = await svc.SaveInvoiceTypeAsync(null, "GTGT", "Hóa đơn GTGT (sửa)", "Ghi chú", InvoiceNoRule.TT68, false, null);
            Assert.True(ok);
            Assert.Equal(id, id2);
            var list = await svc.InvoiceTypesAsync(null);
            Assert.Single(list);
            Assert.Equal("Hóa đơn GTGT (sửa)", list[0].InvoiceTypeName);
            Assert.Equal(InvoiceNoRule.TT68, list[0].TTType);
            Assert.False(list[0].FlagActive);
        }
    }

    [Fact]
    public async Task InvoiceType_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveInvoiceTypeAsync(null, "GTGT", "  ", null, InvoiceNoRule.TT78, true, null);
            Assert.False(ok);
            Assert.Contains("tên loại hóa đơn", msg);
        }
    }

    [Fact]
    public async Task InvoiceType_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveInvoiceTypeAsync(null, "BANHANG", "Hóa đơn bán hàng", null, InvoiceNoRule.TT78, true, null);
            var (ok, _) = await svc.DeleteInvoiceTypeAsync(id);
            Assert.True(ok);
            Assert.Empty(await svc.InvoiceTypesAsync(null));
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

    // Tạo NNT kèm phòng ban gốc (theo Mst_NNT_CreateNNTAndDepartment của TVAN gốc).
    private static NntProfile NntProf(string mst, string name) => new(
        mst, name, null, null, null, null, "Hà Nội", null, null, null, "Nguyễn Văn A", null,
        "Giám đốc", null, null, null, "Nguyễn Văn A", "0901234567", "a@cty.vn", null, null, null,
        null, null, null, null, null, null, null, null, true, "kế toán");

    [Fact]
    public async Task Nnt_CreateWithDepartment_CreatesBoth()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, nntId, deptId) = await svc.CreateNntAndDepartmentAsync(NntProf("0107654321", "Cty Sao Mai"), "HO", "Trụ sở chính");
            Assert.True(ok);
            Assert.True(nntId > 0);
            Assert.True(deptId > 0);
            var nnt = await svc.GetNntAsync(nntId);
            Assert.Equal("0107654321", nnt!.Mst);
            var dept = await svc.GetDepartmentAsync(deptId);
            Assert.Equal("HO", dept!.DepartmentCode);
            Assert.Null(dept.DepartmentCodeParent);
            Assert.Equal("0107654321", dept.MST);
            Assert.Equal("HO", dept.DepartmentBUCode);
            Assert.Equal(1, dept.DepartmentLevel);
        }
    }

    [Fact]
    public async Task Nnt_CreateWithDepartment_MissingDeptCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _, _) = await svc.CreateNntAndDepartmentAsync(NntProf("0107654321", "Cty Sao Mai"), "  ", "Trụ sở chính");
            Assert.False(ok);
            Assert.Contains("mã phòng ban", msg);
            Assert.Empty(await svc.NntsAsync());
        }
    }

    [Fact]
    public async Task Nnt_CreateWithDepartment_InvalidProfile_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            // Thiếu tên NNT → SaveNntAsync chặn, không tạo phòng ban.
            var (ok, msg, _, _) = await svc.CreateNntAndDepartmentAsync(NntProf("0107654321", "  "), "HO", "Trụ sở chính");
            Assert.False(ok);
            Assert.Contains("tên", msg);
            Assert.Empty(await svc.DepartmentsAsync(null, null));
        }
    }

    [Fact]
    public async Task Nnt_CreateWithDepartment_DuplicateDeptCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateNntAndDepartmentAsync(NntProf("0107654321", "Cty Sao Mai"), "HO", "Trụ sở chính");
            var (ok, msg, _, _) = await svc.CreateNntAndDepartmentAsync(NntProf("0107654322", "Cty Sao Mai 2"), "HO", "Trụ sở chính 2");
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
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
    public async Task SysUser_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveSysUserAsync(null, "ketoan01", "Nguyễn Văn A", "secret", "0901234567", "a@x.vn", "0101243150", "KT", "Kế toán", false, false, false, true, "quản trị");
            Assert.True(ok);
            var u = await svc.GetSysUserAsync(id);
            Assert.NotNull(u);
            Assert.Equal("ketoan01", u!.UserCode);
            Assert.Equal("Nguyễn Văn A", u.UserName);
            Assert.True(u.FlagActive);
            // Mật khẩu KHÔNG lưu thô.
            Assert.NotEqual("secret", u.UserPasswordHash);
            Assert.False(string.IsNullOrEmpty(u.UserPasswordHash));
        }
    }

    [Fact]
    public async Task SysUser_Save_DuplicateCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSysUserAsync(null, "ketoan01", "A", "p1", null, null, null, null, null, false, false, false, true, null);
            var (ok, msg, _) = await svc.SaveSysUserAsync(null, "ketoan01", "B", "p2", null, null, null, null, null, false, false, false, true, null);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task SysUser_Save_MissingName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveSysUserAsync(null, "ketoan01", "  ", "p1", null, null, null, null, null, false, false, false, true, null);
            Assert.False(ok);
            Assert.Contains("Cần tên người dùng", msg);
        }
    }

    [Fact]
    public async Task SysUser_Save_MissingPasswordOnCreate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveSysUserAsync(null, "ketoan01", "A", null, null, null, null, null, null, false, false, false, true, null);
            Assert.False(ok);
            Assert.Contains("Cần mật khẩu", msg);
        }
    }

    [Fact]
    public async Task SysUser_Save_Update_KeepsPasswordWhenBlank()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSysUserAsync(null, "ketoan01", "A", "secret", null, null, null, null, null, false, false, false, true, null);
            var hash1 = (await svc.GetSysUserAsync(id))!.UserPasswordHash;
            // Cập nhật tên, mật khẩu để trống → giữ nguyên hash.
            var (ok, _, _) = await svc.SaveSysUserAsync(id, "ketoan01", "B", null, null, null, null, null, null, false, false, false, true, null);
            Assert.True(ok);
            var u = await svc.GetSysUserAsync(id);
            Assert.Equal("B", u!.UserName);
            Assert.Equal(hash1, u.UserPasswordHash);
        }
    }

    [Fact]
    public async Task SysUser_Delete_RemovesGroupMembership()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, uid) = await svc.SaveSysUserAsync(null, "ketoan01", "A", "p1", null, null, null, null, null, false, false, false, true, null);
            var (_, _, gid) = await svc.SaveSysGroupAsync(null, "KETOAN", "Nhóm kế toán", true, null);
            await svc.SaveSysGroupMembersAsync(gid, new() { "ketoan01" }, null);
            var (ok, _) = await svc.DeleteSysUserAsync(uid);
            Assert.True(ok);
            Assert.Empty(await svc.SysUsersAsync(null));
            Assert.Empty(await db.SysUserInGroups.ToListAsync());
        }
    }

    [Fact]
    public async Task ChangePassword_Valid_UpdatesHashAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSysUserAsync(null, "ketoan01", "A", "OldPass1", null, null, null, null, null, false, false, false, true, null);
            var hash1 = (await svc.GetSysUserAsync(id))!.UserPasswordHash;
            var (ok, msg) = await svc.ChangePasswordAsync("ketoan01", "OldPass1", "NewPass2", "ketoan01");
            Assert.True(ok);
            Assert.Contains("Đã đổi mật khẩu", msg);
            var u = await svc.GetSysUserAsync(id);
            Assert.NotEqual(hash1, u!.UserPasswordHash);
            var logs = await svc.PasswordChangeLogsAsync("ketoan01");
            Assert.Single(logs);
            Assert.Equal(PasswordChangeResult.Success, logs[0].Result);
        }
    }

    [Fact]
    public async Task ChangePassword_WrongOldPassword_BlockedAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSysUserAsync(null, "ketoan01", "A", "OldPass1", null, null, null, null, null, false, false, false, true, null);
            var (ok, msg) = await svc.ChangePasswordAsync("ketoan01", "WrongPass1", "NewPass2", null);
            Assert.False(ok);
            Assert.Contains("Mật khẩu cũ không đúng", msg);
            var logs = await svc.PasswordChangeLogsAsync("ketoan01");
            Assert.Single(logs);
            Assert.Equal(PasswordChangeResult.Failed, logs[0].Result);
        }
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSysUserAsync(null, "ketoan01", "A", "OldPass1", null, null, null, null, null, false, false, false, true, null);
            // Thiếu chữ HOA và quá ngắn → không đạt chính sách.
            var (ok, msg) = await svc.ChangePasswordAsync("ketoan01", "OldPass1", "abc", null);
            Assert.False(ok);
            Assert.Contains("Mật khẩu mới phải có", msg);
        }
    }

    [Fact]
    public async Task ChangePassword_UnknownUser_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.ChangePasswordAsync("khongton", "OldPass1", "NewPass2", null);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
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

    // Nhận kết quả xử lý thông báo sai sót từ CQT (theo Invoice_Invoice_Process301 của TVAN gốc).
    // Đưa HĐ về trạng thái đã gửi thông báo sai sót (FlagSuaDoi = Sent) để nhận kết quả 301.
    private static async Task<int> SetupSent300(AppDbContext db, ITvanService svc)
    {
        var invId = await SetupAccepted(db, svc);
        await svc.SendTct300Async(invId, ReplaceOrAdjustFlag.Adjust, "1", "04/SS", DateTime.Today, "Sai MST người mua", "kế toán");
        return invId;
    }

    [Fact]
    public async Task ReceiveTct301_Sent_SetsAllowedAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupSent300(db, svc);
            var (ok, msg) = await svc.ReceiveTct301Async(invId, null, "kế toán");
            Assert.True(ok);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(SuaDoiFlag.Allowed, inv!.FlagSuaDoi);
            var logs = await svc.Tct301LogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal(SuaDoiFlag.Allowed, logs[0].KetQua);
            Assert.Equal(inv.TCTSuaDoiRefNo, logs[0].TCTRefNo);
        }
    }

    [Fact]
    public async Task ReceiveTct301_NotSent_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupAccepted(db, svc);   // đã Accepted nhưng CHƯA gửi thông báo 300
            var (ok, msg) = await svc.ReceiveTct301Async(invId, null, null);
            Assert.False(ok);
            Assert.Contains("300", msg);
        }
    }

    [Fact]
    public async Task ReceiveTct301_ExplicitRefNo_Stored()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var invId = await SetupSent300(db, svc);
            var (ok, _) = await svc.ReceiveTct301Async(invId, "V-CUSTOM-301", "kế toán");
            Assert.True(ok);
            var logs = await svc.Tct301LogsAsync(invId);
            Assert.Single(logs);
            Assert.Equal("V-CUSTOM-301", logs[0].TCTRefNo);
        }
    }

    // Dòng hàng hóa/dịch vụ của hóa đơn (theo Invoice_InvoiceDtl của TVAN gốc).
    private static async Task SeedDtlCatalog(AppDbContext db)
    {
        db.VatRates.Add(new VatRate { VATRateCode = "VAT10", VATRate = "10%", FlagActive = true });
        db.InvoiceDtlTypes.Add(new InvoiceDtlType { InvoiceDtlTypeCode = "GOODS", Desc = "Hàng hóa", FlagActive = true });
        await db.SaveChangesAsync();
    }

    private static InvoiceDtlLine Line(string name, decimal qty, decimal price, decimal vat = 10, string type = "GOODS", string vatCode = "VAT10") =>
        new(null, type, null, name, null, name, vatCode, vat, null, "CAI", "Cái", price, qty, 0, null, null, null, null, null, null);

    [Fact]
    public async Task InvoiceLines_Save_ComputesTotalsFromLines()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedDtlCatalog(db);
            var (_, invId) = await Setup(svc);
            var lines = new List<InvoiceDtlLine> { Line("Thép tấm", 100, 200_000), Line("Bu lông", 500, 10_000) };
            var (ok, _, count) = await svc.SaveInvoiceWithLinesAsync(invId, lines, "kế toán");
            Assert.True(ok);
            Assert.Equal(2, count);
            var inv = await svc.GetInvoiceAsync(invId);
            Assert.Equal(25_000_000, inv!.Amount);
            Assert.Equal(2_500_000, inv.VatAmount);
            Assert.Equal(2, (await svc.InvoiceDtlsAsync(invId)).Count);
        }
    }

    [Fact]
    public async Task InvoiceLines_Save_ReplacesExistingLines()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedDtlCatalog(db);
            var (_, invId) = await Setup(svc);
            await svc.SaveInvoiceWithLinesAsync(invId, new List<InvoiceDtlLine> { Line("A", 1, 1_000_000) }, "kế toán");
            await svc.SaveInvoiceWithLinesAsync(invId, new List<InvoiceDtlLine> { Line("B", 2, 1_000_000), Line("C", 3, 1_000_000) }, "kế toán");
            var ls = await svc.InvoiceDtlsAsync(invId);
            Assert.Equal(2, ls.Count);
            Assert.Equal(5_000_000, (await svc.GetInvoiceAsync(invId))!.Amount);
        }
    }

    [Fact]
    public async Task InvoiceLines_UnknownDtlType_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedDtlCatalog(db);
            var (_, invId) = await Setup(svc);
            var (ok, msg, _) = await svc.SaveInvoiceWithLinesAsync(invId, new List<InvoiceDtlLine> { Line("A", 1, 1_000_000, type: "UNKNOWN") }, "kế toán");
            Assert.False(ok);
            Assert.Contains("Loại dòng", msg);
        }
    }

    [Fact]
    public async Task InvoiceLines_UnknownVatRate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedDtlCatalog(db);
            var (_, invId) = await Setup(svc);
            var (ok, msg, _) = await svc.SaveInvoiceWithLinesAsync(invId, new List<InvoiceDtlLine> { Line("A", 1, 1_000_000, vatCode: "VAT99") }, "kế toán");
            Assert.False(ok);
            Assert.Contains("thuế suất", msg);
        }
    }

    [Fact]
    public async Task InvoiceLines_NonDraft_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedDtlCatalog(db);
            var (_, invId) = await Setup(svc);
            await svc.TransmitAsync(invId);   // HĐ chuyển sang Accepted
            var (ok, msg, _) = await svc.SaveInvoiceWithLinesAsync(invId, new List<InvoiceDtlLine> { Line("A", 1, 1_000_000) }, "kế toán");
            Assert.False(ok);
            Assert.Contains("nháp", msg);
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

    // Helper tạo mẫu hóa đơn đã phát hành (dùng cho các test sửa lỗi hàng loạt theo mẫu).
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
    public async Task SpecType1_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveSpecType1Async(null, "DIENTU", "Điện tử", "Hàng điện tử", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetSpecType1Async(id);
            Assert.Equal("DIENTU", e!.SpecType1Code);
            Assert.Equal("Điện tử", e.SpecType1Name);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task SpecType1_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSpecType1Async(null, "DIENTU", "Điện tử", null, true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveSpecType1Async(null, "DIENTU", "Điện tử gia dụng", "cập nhật", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.SpecType1sAsync(null));
            Assert.Equal("Điện tử gia dụng", (await svc.GetSpecType1Async(id))!.SpecType1Name);
        }
    }

    [Fact]
    public async Task SpecType1_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveSpecType1Async(null, "  ", "Điện tử", null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã loại sản phẩm", msg1);
            var (ok2, msg2, _) = await svc.SaveSpecType1Async(null, "DIENTU", "  ", null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("tên loại sản phẩm", msg2);
        }
    }

    [Fact]
    public async Task SpecType1_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecType1Async(null, "DIENTU", "Điện tử", null, true, "kế toán");
            await svc.SaveSpecType1Async(null, "THUCPHAM", "Thực phẩm", null, true, "kế toán");
            var all = await svc.SpecType1sAsync(null);
            Assert.Equal(2, all.Count);
            var filtered = await svc.SpecType1sAsync("THUCPHAM");
            Assert.Single(filtered);
            Assert.Equal("THUCPHAM", filtered[0].SpecType1Code);
        }
    }

    [Fact]
    public async Task SpecType1_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSpecType1Async(null, "GIAYDEP", "Giày dép", null, true, "kế toán");
            var (ok, _) = await svc.DeleteSpecType1Async(id);
            Assert.True(ok);
            Assert.Null(await svc.GetSpecType1Async(id));
        }
    }

    [Fact]
    public async Task SpecType2_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveSpecType2Async(null, "DT01", "Điện thoại", "Điện thoại di động", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetSpecType2Async(id);
            Assert.Equal("DT01", e!.SpecType2Code);
            Assert.Equal("Điện thoại", e.SpecType2Name);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task SpecType2_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSpecType2Async(null, "DT01", "Điện thoại", null, true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveSpecType2Async(null, "DT01", "Điện thoại thông minh", "cập nhật", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.SpecType2sAsync(null));
            Assert.Equal("Điện thoại thông minh", (await svc.GetSpecType2Async(id))!.SpecType2Name);
        }
    }

    [Fact]
    public async Task SpecType2_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveSpecType2Async(null, "  ", "Điện thoại", null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã nhóm sản phẩm", msg1);
            var (ok2, msg2, _) = await svc.SaveSpecType2Async(null, "DT01", "  ", null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("tên nhóm sản phẩm", msg2);
        }
    }

    [Fact]
    public async Task SpecType2_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecType2Async(null, "DT01", "Điện thoại", null, true, "kế toán");
            await svc.SaveSpecType2Async(null, "LT01", "Laptop", null, true, "kế toán");
            var all = await svc.SpecType2sAsync(null);
            Assert.Equal(2, all.Count);
            var filtered = await svc.SpecType2sAsync("LT01");
            Assert.Single(filtered);
            Assert.Equal("LT01", filtered[0].SpecType2Code);
        }
    }

    [Fact]
    public async Task SpecType2_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSpecType2Async(null, "GD01", "Giày thể thao", null, true, "kế toán");
            var (ok, _) = await svc.DeleteSpecType2Async(id);
            Assert.True(ok);
            Assert.Null(await svc.GetSpecType2Async(id));
        }
    }

    [Fact]
    public async Task SpecCustomField_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveSpecCustomFieldAsync(null, "CF1", "Màu sắc", DBPhysicalType.Text, "Màu hàng hóa", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetSpecCustomFieldAsync(id);
            Assert.Equal("CF1", e!.SpecCustomFieldCode);
            Assert.Equal("Màu sắc", e.SpecCustomFieldName);
            Assert.Equal(DBPhysicalType.Text, e.DBPhysicalType);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task SpecCustomField_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSpecCustomFieldAsync(null, "CF1", "Màu sắc", DBPhysicalType.Text, null, true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveSpecCustomFieldAsync(null, "CF1", "Màu sắc (mới)", DBPhysicalType.Number, "cập nhật", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.SpecCustomFieldsAsync(null));
            var e = await svc.GetSpecCustomFieldAsync(id);
            Assert.Equal("Màu sắc (mới)", e!.SpecCustomFieldName);
            Assert.Equal(DBPhysicalType.Number, e.DBPhysicalType);
        }
    }

    [Fact]
    public async Task SpecCustomField_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveSpecCustomFieldAsync(null, "  ", "Màu sắc", DBPhysicalType.Text, null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã trường tùy chỉnh", msg1);
            var (ok2, msg2, _) = await svc.SaveSpecCustomFieldAsync(null, "CF1", "  ", DBPhysicalType.Text, null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("tên trường tùy chỉnh", msg2);
        }
    }

    [Fact]
    public async Task SpecCustomField_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecCustomFieldAsync(null, "CF1", "Màu sắc", DBPhysicalType.Text, null, true, "kế toán");
            await svc.SaveSpecCustomFieldAsync(null, "CF2", "Bảo hành", DBPhysicalType.Number, null, true, "kế toán");
            var all = await svc.SpecCustomFieldsAsync(null);
            Assert.Equal(2, all.Count);
            var filtered = await svc.SpecCustomFieldsAsync("Bảo hành");
            Assert.Single(filtered);
        }
    }

    [Fact]
    public async Task SpecCustomField_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSpecCustomFieldAsync(null, "CF1", "Màu sắc", DBPhysicalType.Text, null, true, "kế toán");
            var (ok, _) = await svc.DeleteSpecCustomFieldAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetSpecCustomFieldAsync(id));
        }
    }

    [Fact]
    public async Task Spec_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecType1Async(null, "DIENTU", "Điện tử", null, true, "kế toán");
            await svc.SaveSpecType2Async(null, "DT01", "Điện thoại", null, true, "kế toán");
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            var (ok, _, id) = await svc.SaveSpecAsync(null, "SP-A54", "Điện thoại Galaxy A54", "Mô tả", null, "DIENTU", "DT01", "Đen", true, false, "CHIEC", "CHIEC", "Hàng điện tử", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetSpecAsync(id);
            Assert.Equal("SP-A54", e!.SpecCode);
            Assert.Equal("Điện thoại Galaxy A54", e.SpecName);
            Assert.Equal("DIENTU", e.SpecType1);
            Assert.Equal("DT01", e.SpecType2);
            Assert.True(e.FlagHasSerial);
            Assert.False(e.FlagHasLOT);
            Assert.Equal("CHIEC", e.DefaultUnitCode);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task Spec_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54 (bản mới)", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.SpecsAsync(null, null, null, null));
            Assert.Equal("Galaxy A54 (bản mới)", (await svc.GetSpecAsync(id))!.SpecName);
        }
    }

    [Fact]
    public async Task Spec_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveSpecAsync(null, "  ", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã sản phẩm", msg1);
            var (ok2, msg2, _) = await svc.SaveSpecAsync(null, "SP-A54", "  ", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("tên sản phẩm", msg2);
        }
    }

    [Fact]
    public async Task Spec_Save_InvalidReferences_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            // Loại sản phẩm chưa tồn tại → chặn.
            var (ok1, msg1, _) = await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, "DIENTU", null, null, false, false, null, null, null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("Loại sản phẩm", msg1);
            // Đơn vị tính chưa tồn tại → chặn.
            var (ok2, msg2, _) = await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, "CHIEC", null, null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("Đơn vị tính", msg2);
            // Loại sản phẩm đã ngừng dùng → chặn.
            await svc.SaveSpecType1Async(null, "DIENTU", "Điện tử", null, false, "kế toán");
            var (ok3, msg3, _) = await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, "DIENTU", null, null, false, false, null, null, null, true, "kế toán");
            Assert.False(ok3);
            Assert.Contains("ngừng dùng", msg3);
        }
    }

    [Fact]
    public async Task Spec_List_FilteredByKeywordAndType()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecType1Async(null, "DIENTU", "Điện tử", null, true, "kế toán");
            await svc.SaveSpecType1Async(null, "GIAYDEP", "Giày dép", null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, "DIENTU", null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-IP15", "iPhone 15", null, null, "DIENTU", null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-GD01", "Giày chạy bộ", null, null, "GIAYDEP", null, null, false, false, null, null, null, true, "kế toán");
            Assert.Equal(3, (await svc.SpecsAsync(null, null, null, null)).Count);
            Assert.Single(await svc.SpecsAsync("IP15", null, null, null));
            Assert.Equal(2, (await svc.SpecsAsync(null, "DIENTU", null, null)).Count);
        }
    }

    [Fact]
    public async Task Spec_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (ok, _) = await svc.DeleteSpecAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetSpecAsync(id));
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
    public async Task TypeCode_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveTypeCodeAsync(null, "100", "Tờ khai đăng ký", "TCT", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetTypeCodeAsync(id);
            Assert.Equal("100", e!.TypeCodeValue);
            Assert.Equal("Tờ khai đăng ký", e.TypeDesc);
            Assert.Equal("TCT", e.TypeGroup);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task TypeCode_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveTypeCodeAsync(null, "100", "Tờ khai", "TCT", true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveTypeCodeAsync(null, "100", "Tờ khai đăng ký", "TCT", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.TypeCodesAsync(null));
            Assert.Equal("Tờ khai đăng ký", (await svc.GetTypeCodeAsync(id))!.TypeDesc);
        }
    }

    [Fact]
    public async Task TypeCode_Save_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveTypeCodeAsync(null, "  ", "Tờ khai", "TCT", true, "kế toán");
            Assert.False(ok);
            Assert.Contains("mã loại", msg);
        }
    }

    [Fact]
    public async Task TypeCode_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveTypeCodeAsync(null, "100", "Tờ khai đăng ký", "TCT", true, "kế toán");
            await svc.SaveTypeCodeAsync(null, "200", "Hóa đơn điện tử", "TCT", true, "kế toán");
            var all = await svc.TypeCodesAsync(null);
            Assert.Equal(2, all.Count);
            var filtered = await svc.TypeCodesAsync("200");
            Assert.Single(filtered);
            Assert.Equal("200", filtered[0].TypeCodeValue);
        }
    }

    [Fact]
    public async Task TypeCode_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveTypeCodeAsync(null, "300", "Thông báo sai sót", "TCT", true, "kế toán");
            var (ok, _) = await svc.DeleteTypeCodeAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetTypeCodeAsync(id));
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

    [Fact]
    public async Task TctTransaction_Create_RequiresMessageCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateTctTransactionLogAsync("", "0101243150", TctMessageAction.Send, null, "200", TctMessageStatus.Success, TctMessageResult.Accept, null, null, null, null, 0, null, null, null, "kế toán");
            Assert.False(ok);
            Assert.Contains("mã thông điệp", msg);
        }
    }

    [Fact]
    public async Task TctTransaction_Create_DuplicateBlocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, _, _) = await svc.CreateTctTransactionLogAsync("200001", "0101243150", TctMessageAction.Send, DateTime.UtcNow, "200", TctMessageStatus.Success, TctMessageResult.Accept, null, null, null, null, 1, "Gửi HĐ", null, null, "kế toán");
            Assert.True(ok1);
            var (ok2, msg2, _) = await svc.CreateTctTransactionLogAsync("200001", "0101243150", TctMessageAction.Send, DateTime.UtcNow, "200", TctMessageStatus.Success, TctMessageResult.Accept, null, null, null, null, 1, null, null, null, "kế toán");
            Assert.False(ok2);
            Assert.Contains("đã tồn tại", msg2);
        }
    }

    [Fact]
    public async Task TctTransaction_List_FiltersByActionAndStatus()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateTctTransactionLogAsync("200001", "0101243150", TctMessageAction.Send, DateTime.UtcNow.AddDays(-1), "200", TctMessageStatus.Success, TctMessageResult.Accept, null, null, null, null, 1, null, null, null, "kế toán");
            await svc.CreateTctTransactionLogAsync("202002", "0101243150", TctMessageAction.Receive, DateTime.UtcNow, "202", TctMessageStatus.Success, TctMessageResult.Accept, null, null, null, null, 1, null, null, null, "kế toán");
            await svc.CreateTctTransactionLogAsync("300003", "0101243150", TctMessageAction.Send, DateTime.UtcNow, "300", TctMessageStatus.Error, TctMessageResult.Reject, null, null, null, null, 1, null, null, null, "kế toán");

            Assert.Equal(3, (await svc.TctTransactionLogsAsync(null, null, null, null, null, null, null, null)).Count);
            Assert.Equal(2, (await svc.TctTransactionLogsAsync(null, TctMessageAction.Send, null, null, null, null, null, null)).Count);
            Assert.Single(await svc.TctTransactionLogsAsync(null, null, null, TctMessageStatus.Error, null, null, null, null));
            Assert.Single(await svc.TctTransactionLogsAsync(null, null, null, null, TctMessageResult.Reject, null, null, null));
            Assert.Single(await svc.TctTransactionLogsAsync("202", null, null, null, null, null, null, null));
        }
    }

    [Fact]
    public async Task TctTransaction_Get_ReturnsDetail()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateTctTransactionLogAsync("200001", "0101243150", TctMessageAction.Send, DateTime.UtcNow, "200", TctMessageStatus.Success, TctMessageResult.Accept, "V123", "Cục Thuế TP Hà Nội", DateTime.Today, "8012345678", 2, "Gửi 2 HĐ", "HOA_DON", "2026-06-12/msg.xml", "kế toán");
            var e = await svc.GetTctTransactionLogAsync(id);
            Assert.NotNull(e);
            Assert.Equal("200001", e!.MessageCode);
            Assert.Equal(TctMessageAction.Send, e.MessageAction);
            Assert.Equal(TctMessageResult.Accept, e.MessageResult);
            Assert.Equal(2, e.InvoiceQty);
            Assert.Equal("V123", e.MessageRefCode);
        }
    }

    // Hóa đơn đầu vào (theo Invoice_InvoiceInput của TVAN gốc).
    private static InvoiceInputHeader Hdr(string mst, string code, string? seller = "NCC A", decimal rate = 10) =>
        new(mst, code, null, "01GTKT", "1C26TAA", SourceInvoiceCode.Root, InvoiceAdjType.Normal, PaymentMethod.Transfer, null,
            DateTime.Today, seller, "0107654321", "Hà Nội", null, null, null, null,
            "Cty Mua", mst, "Hà Nội", null, null, null, "00001234", null, null, null, null, null, "VND", 1, null);

    [Fact]
    public async Task InvoiceInput_Save_ComputesTotalsFromLines()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var lines = new List<InvoiceInputLine>
            {
                new("Thép tấm", "KG", 100, 200_000, 10, null),
                new("Bu lông", "CAI", 500, 10_000, 10, null)
            };
            var (ok, _, id) = await svc.SaveInvoiceInputAsync(null, Hdr("0101243150", "0026082012345001"), lines, "kế toán");
            Assert.True(ok);
            var e = await svc.GetInvoiceInputAsync(id);
            Assert.NotNull(e);
            Assert.Equal(2, e!.Details.Count);
            Assert.Equal(25_000_000, e.TotalValInvoice);
            Assert.Equal(2_500_000, e.TotalValVAT);
            Assert.Equal(27_500_000, e.TotalValPmt);
        }
    }

    [Fact]
    public async Task InvoiceInput_Save_SameCode_Upserts()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id1) = await svc.SaveInvoiceInputAsync(null, Hdr("0101243150", "0026082012345001"), new(), "kế toán");
            // Lưu lại cùng (MST + số tra cứu) = cập nhật, không tạo bản ghi mới.
            var (ok, _, id2) = await svc.SaveInvoiceInputAsync(null, Hdr("0101243150", "0026082012345001", seller: "NCC B"), new(), "kế toán");
            Assert.True(ok);
            Assert.Equal(id1, id2);
            Assert.Single(await svc.InvoiceInputsAsync("0101243150", null, null));
            Assert.Equal("NCC B", (await svc.GetInvoiceInputAsync(id1))!.SellerName);
        }
    }

    [Fact]
    public async Task InvoiceInput_Save_MissingCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveInvoiceInputAsync(null, Hdr("0101243150", ""), new(), "kế toán");
            Assert.False(ok);
            Assert.Contains("số tra cứu", msg);
        }
    }

    [Fact]
    public async Task InvoiceInput_Delete_MarksDeleted()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveInvoiceInputAsync(null, Hdr("0101243150", "0026082012345001"), new(), "kế toán");
            var (ok, _) = await svc.DeleteInvoiceInputAsync(id, "Nhập trùng", "kế toán");
            Assert.True(ok);
            var e = await svc.GetInvoiceInputAsync(id);
            Assert.Equal(InputInvoiceStatus.Deleted, e!.Status);
            Assert.Equal("Nhập trùng", e.DeleteReason);
            // Xóa lần hai bị chặn.
            var (ok2, msg2) = await svc.DeleteInvoiceInputAsync(id, null, null);
            Assert.False(ok2);
            Assert.Contains("đã bị xóa", msg2);
        }
    }

    [Fact]
    public async Task InvoiceInput_List_FiltersByMstAndStatus()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveInvoiceInputAsync(null, Hdr("0101243150", "0026082012345001"), new(), "kế toán");
            var (_, _, id2) = await svc.SaveInvoiceInputAsync(null, Hdr("0101243150", "0026082012345002"), new(), "kế toán");
            await svc.SaveInvoiceInputAsync(null, Hdr("0312345678", "0026082012345003"), new(), "kế toán");
            await svc.DeleteInvoiceInputAsync(id2, "xóa", "kế toán");

            Assert.Equal(3, (await svc.InvoiceInputsAsync(null, null, null)).Count);
            Assert.Equal(2, (await svc.InvoiceInputsAsync("0101243150", null, null)).Count);
            Assert.Single(await svc.InvoiceInputsAsync(null, null, InputInvoiceStatus.Deleted));
            Assert.Single(await svc.InvoiceInputsAsync(null, "0026082012345003", null));
        }
    }

    [Fact]
    public async Task SpecUnit_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            await svc.SaveUnitAsync(null, "HOP", "Hộp", null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (ok, _, id) = await svc.SaveSpecUnitAsync(null, "SP-A54", "HOP", "CHIEC", "Hộp 10 chiếc", 10, 20, 12, 6, 1440, 0.5m, "Đóng gói", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetSpecUnitAsync(id);
            Assert.Equal("SP-A54", e!.SpecCode);
            Assert.Equal("HOP", e.UnitCode);
            Assert.Equal("CHIEC", e.StandardUnitCode);
            Assert.Equal(10, e.Qty);
            Assert.Equal(0.5m, e.Weight);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task SpecUnit_Save_SameKey_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            await svc.SaveUnitAsync(null, "HOP", "Hộp", null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (_, _, id) = await svc.SaveSpecUnitAsync(null, "SP-A54", "HOP", "CHIEC", null, 10, null, null, null, null, null, null, true, "kế toán");
            // Lưu lại cùng cặp (sản phẩm, đơn vị) = cập nhật (không tạo bản ghi mới).
            var (ok, _, id2) = await svc.SaveSpecUnitAsync(null, "SP-A54", "HOP", "CHIEC", null, 24, null, null, null, null, null, null, true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.SpecUnitsAsync(null, null, null));
            Assert.Equal(24, (await svc.GetSpecUnitAsync(id))!.Qty);
        }
    }

    [Fact]
    public async Task SpecUnit_Save_MissingKey_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveSpecUnitAsync(null, "  ", "HOP", "CHIEC", null, 1, null, null, null, null, null, null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã sản phẩm", msg1);
            var (ok2, msg2, _) = await svc.SaveSpecUnitAsync(null, "SP-A54", "  ", "CHIEC", null, 1, null, null, null, null, null, null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("mã đơn vị tính", msg2);
            var (ok3, msg3, _) = await svc.SaveSpecUnitAsync(null, "SP-A54", "HOP", "  ", null, 1, null, null, null, null, null, null, true, "kế toán");
            Assert.False(ok3);
            Assert.Contains("mã đơn vị chuẩn", msg3);
        }
    }

    [Fact]
    public async Task SpecUnit_Save_InvalidReferences_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            // Sản phẩm chưa tồn tại → chặn.
            var (ok1, msg1, _) = await svc.SaveSpecUnitAsync(null, "SP-A54", "CHIEC", "CHIEC", null, 1, null, null, null, null, null, null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("Sản phẩm", msg1);
            // Đơn vị tính chưa tồn tại → chặn.
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (ok2, msg2, _) = await svc.SaveSpecUnitAsync(null, "SP-A54", "HOP", "CHIEC", null, 1, null, null, null, null, null, null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("Đơn vị tính", msg2);
            // Đơn vị chuẩn đã ngừng dùng → chặn.
            await svc.SaveUnitAsync(null, "HOP", "Hộp", null, false, "kế toán");
            var (ok3, msg3, _) = await svc.SaveSpecUnitAsync(null, "SP-A54", "CHIEC", "HOP", null, 1, null, null, null, null, null, null, true, "kế toán");
            Assert.False(ok3);
            Assert.Contains("ngừng dùng", msg3);
        }
    }

    [Fact]
    public async Task SpecUnit_List_FilteredBySpecAndUnit()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            await svc.SaveUnitAsync(null, "HOP", "Hộp", null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-IP15", "iPhone 15", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveSpecUnitAsync(null, "SP-A54", "HOP", "CHIEC", null, 10, null, null, null, null, null, null, true, "kế toán");
            await svc.SaveSpecUnitAsync(null, "SP-A54", "CHIEC", "CHIEC", null, 1, null, null, null, null, null, null, true, "kế toán");
            await svc.SaveSpecUnitAsync(null, "SP-IP15", "HOP", "CHIEC", null, 5, null, null, null, null, null, null, true, "kế toán");
            Assert.Equal(3, (await svc.SpecUnitsAsync(null, null, null)).Count);
            Assert.Equal(2, (await svc.SpecUnitsAsync(null, "SP-A54", null)).Count);
            Assert.Equal(2, (await svc.SpecUnitsAsync(null, null, "HOP")).Count);
        }
    }

    [Fact]
    public async Task SpecUnit_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (_, _, id) = await svc.SaveSpecUnitAsync(null, "SP-A54", "CHIEC", "CHIEC", null, 1, null, null, null, null, null, null, true, "kế toán");
            var (ok, _) = await svc.DeleteSpecUnitAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetSpecUnitAsync(id));
        }
    }

    [Fact]
    public async Task SpecPrice_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            await svc.SaveCurrencyExAsync(null, "VND", "đồng", "VND", 1, 1, null, true, "kế toán");
            await svc.SaveVatRateAsync(null, "VAT10", "10%", null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (ok, _, id) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 6_500_000, 8_200_000, "VND", 200_000, "VAT10", DateTime.Today, DateTime.Today.AddDays(180), "Giá lẻ", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetSpecPriceAsync(id);
            Assert.Equal("SP-A54", e!.SpecCode);
            Assert.Equal(8_200_000, e.SellPrice);
            Assert.Equal("VAT10", e.VATRateCode);
        }
    }

    [Fact]
    public async Task SpecPrice_Save_SameKey_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            await svc.SaveCurrencyExAsync(null, "VND", "đồng", "VND", 1, 1, null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (_, _, id) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 6_500_000, 8_200_000, "VND", 0, null, null, null, null, true, "kế toán");
            // Lưu lại cùng cặp (sản phẩm, đơn vị) = cập nhật (không tạo bản ghi mới).
            var (ok, _, id2) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 6_500_000, 9_000_000, "VND", 0, null, null, null, null, true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.SpecPricesAsync(null, null, null));
            Assert.Equal(9_000_000, (await svc.GetSpecPriceAsync(id))!.SellPrice);
        }
    }

    [Fact]
    public async Task SpecPrice_Save_MissingKey_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveSpecPriceAsync(null, "  ", "CHIEC", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã sản phẩm", msg1);
            var (ok2, msg2, _) = await svc.SaveSpecPriceAsync(null, "SP-A54", "  ", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("mã đơn vị tính", msg2);
            var (ok3, msg3, _) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 1, 1, "  ", 0, null, null, null, null, true, "kế toán");
            Assert.False(ok3);
            Assert.Contains("loại tiền", msg3);
        }
    }

    [Fact]
    public async Task SpecPrice_Save_InvalidReferences_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            // Sản phẩm chưa tồn tại → chặn.
            var (ok1, msg1, _) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("không tồn tại", msg1);
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            // Đơn vị tính chưa tồn tại → chặn.
            var (ok2, msg2, _) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("Đơn vị tính", msg2);
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            // Loại tiền chưa tồn tại → chặn.
            var (ok3, msg3, _) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            Assert.False(ok3);
            Assert.Contains("Loại tiền", msg3);
            await svc.SaveCurrencyExAsync(null, "VND", "đồng", "VND", 1, 1, null, true, "kế toán");
            // Thuế suất khai báo nhưng chưa tồn tại → chặn.
            var (ok4, msg4, _) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 1, 1, "VND", 0, "VAT10", null, null, null, true, "kế toán");
            Assert.False(ok4);
            Assert.Contains("Thuế suất", msg4);
        }
    }

    [Fact]
    public async Task SpecPrice_List_FilteredBySpecAndUnit()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            await svc.SaveUnitAsync(null, "HOP", "Hộp", null, true, "kế toán");
            await svc.SaveCurrencyExAsync(null, "VND", "đồng", "VND", 1, 1, null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-IP15", "iPhone 15", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            await svc.SaveSpecPriceAsync(null, "SP-A54", "HOP", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            await svc.SaveSpecPriceAsync(null, "SP-IP15", "CHIEC", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            Assert.Equal(3, (await svc.SpecPricesAsync(null, null, null)).Count);
            Assert.Equal(2, (await svc.SpecPricesAsync(null, "SP-A54", null)).Count);
            Assert.Equal(2, (await svc.SpecPricesAsync(null, null, "CHIEC")).Count);
        }
    }

    [Fact]
    public async Task SpecPrice_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveUnitAsync(null, "CHIEC", "Chiếc", null, true, "kế toán");
            await svc.SaveCurrencyExAsync(null, "VND", "đồng", "VND", 1, 1, null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (_, _, id) = await svc.SaveSpecPriceAsync(null, "SP-A54", "CHIEC", 1, 1, "VND", 0, null, null, null, null, true, "kế toán");
            var (ok, _) = await svc.DeleteSpecPriceAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetSpecPriceAsync(id));
        }
    }

    // ===== Mã sản phẩm / serial (theo Prd_ProductID của TVAN gốc) =====

    [Fact]
    public async Task ProductId_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (ok, _, id) = await svc.SaveProductIdAsync(null, "SN-0001", "SP-A54", DateTime.Today.AddDays(-60), "LOT-01", DateTime.Today.AddDays(-30), "SEC-1",
                DateTime.Today.AddDays(-30), DateTime.Today.AddMonths(12), 12, null, null, null, null, null, null, "Nguyễn Văn A",
                ProductIdStatus.Sold, "Đen", null, null, null, null, "kế toán");
            Assert.True(ok);
            var e = await svc.GetProductIdAsync(id);
            Assert.Equal("SN-0001", e!.ProductID);
            Assert.Equal("SP-A54", e.SpecCode);
            Assert.Equal("LOT-01", e.LOTNo);
            Assert.Equal(12, e.WarrantyDuration);
            Assert.Equal(ProductIdStatus.Sold, e.ProductIDStatus);
            Assert.Equal("Đen", e.CustomField1);
        }
    }

    [Fact]
    public async Task ProductId_Save_SameKey_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (_, _, id) = await svc.SaveProductIdAsync(null, "SN-0001", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            // Lưu lại cùng id = cập nhật (không tạo bản ghi mới).
            var (ok, _, id2) = await svc.SaveProductIdAsync(id, "SN-0001", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, "Trần Thị B", ProductIdStatus.Sold, null, null, null, null, null, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.ProductIdsAsync(null, null, null));
            Assert.Equal(ProductIdStatus.Sold, (await svc.GetProductIdAsync(id))!.ProductIDStatus);
        }
    }

    [Fact]
    public async Task ProductId_Save_DuplicateKey_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveProductIdAsync(null, "SN-0001", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            // Tạo mới trùng cặp (serial, sản phẩm) → chặn.
            var (ok, msg, _) = await svc.SaveProductIdAsync(null, "SN-0001", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task ProductId_Save_MissingKey_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveProductIdAsync(null, "  ", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            Assert.False(ok1);
            Assert.Contains("serial", msg1);
            var (ok2, msg2, _) = await svc.SaveProductIdAsync(null, "SN-0001", "  ", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            Assert.False(ok2);
            Assert.Contains("SpecCode", msg2);
        }
    }

    [Fact]
    public async Task ProductId_Save_UnknownSpec_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            // Sản phẩm chưa tồn tại → chặn.
            var (ok, msg, _) = await svc.SaveProductIdAsync(null, "SN-0001", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            Assert.False(ok);
            Assert.Contains("không tồn tại", msg);
        }
    }

    [Fact]
    public async Task ProductId_List_FilteredBySpecAndStatus()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveSpecAsync(null, "SP-IP15", "iPhone 15", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            await svc.SaveProductIdAsync(null, "SN-0001", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.Sold, null, null, null, null, null, "kế toán");
            await svc.SaveProductIdAsync(null, "SN-0002", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            await svc.SaveProductIdAsync(null, "SN-0003", "SP-IP15", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            Assert.Equal(3, (await svc.ProductIdsAsync(null, null, null)).Count);
            Assert.Equal(2, (await svc.ProductIdsAsync(null, "SP-A54", null)).Count);
            Assert.Equal(2, (await svc.ProductIdsAsync(null, null, ProductIdStatus.New)).Count);
        }
    }

    [Fact]
    public async Task ProductId_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveSpecAsync(null, "SP-A54", "Galaxy A54", null, null, null, null, null, false, false, null, null, null, true, "kế toán");
            var (_, _, id) = await svc.SaveProductIdAsync(null, "SN-0001", "SP-A54", null, null, null, null, null, null, null, null, null, null, null, null, null, null, ProductIdStatus.New, null, null, null, null, null, "kế toán");
            var (ok, _) = await svc.DeleteProductIdAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetProductIdAsync(id));
        }
    }

    // Báo cáo tình hình sử dụng hóa đơn (BC26/AC) — theo Rpt_InvoiceInvoice_ResultUsed của TVAN gốc.
    private static async Task<int> SetupTemplate(AppDbContext db, ITvanService svc, int startNo, int endNo, DateTime effStart)
    {
        var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
        await svc.RegisterNntAsync(nntId);
        var tpl = new InvoiceTemplate
        {
            NntId = nntId, TInvoiceCode = "TINV-1C26TAA", TInvoiceName = "Hóa đơn GTGT 1C26TAA",
            FormNo = "1C26TAA", Sign = "K26TAA", TTType = InvoiceNoRule.TT78,
            EffDateStart = effStart, StartInvoiceNo = startNo, EndInvoiceNo = endNo,
            TInvoiceStatus = TemplateStatus.Issued, FlagActive = true
        };
        db.InvoiceTemplates.Add(tpl); await db.SaveChangesAsync();
        return nntId;
    }

    [Fact]
    public async Task InvoiceUsage_CountsUsedDeletedCancelled()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var nntId = await SetupTemplate(db, svc, 1, 1000, DateTime.Today.AddDays(-30));
            // 2 HĐ đã phát hành (Accepted) + 1 HĐ đã xóa (Deleted) trong kỳ.
            var (_, _, i1) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", No = "00000001", BuyerName = "A", Amount = 1_000_000 });
            await svc.TransmitAsync(i1);
            var (_, _, i2) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", No = "00000002", BuyerName = "B", Amount = 2_000_000 });
            await svc.TransmitAsync(i2);
            var (_, _, i3) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", No = "00000003", BuyerName = "C", Amount = 3_000_000 });
            await svc.TransmitAsync(i3);
            await svc.DeleteInvoiceAsync(i3, "lập sai", "kế toán");

            var rows = await svc.InvoiceUsageReportAsync(null, null, null, DateTime.Today.Year, null);
            var r = Assert.Single(rows);
            Assert.Equal("1C26TAA", r.FormNo);
            Assert.Equal(2, r.QtyUsed);
            Assert.Equal(1, r.QtyDeleted);
            Assert.Equal(0, r.QtyCancelled);
        }
    }

    [Fact]
    public async Task InvoiceUsage_IssuedInPeriod_CountsRange()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            // Mẫu có hiệu lực trong năm nay → phát hành trong kỳ = số lượng dải số (500).
            await SetupTemplate(db, svc, 1, 500, DateTime.Today.AddDays(-10));
            var rows = await svc.InvoiceUsageReportAsync(null, null, null, DateTime.Today.Year, null);
            var r = Assert.Single(rows);
            Assert.Equal(500, r.QtyIssued);
            Assert.Equal(500, r.QtyClosing);   // chưa dùng gì → tồn cuối = phát hành
        }
    }

    [Fact]
    public async Task InvoiceUsage_FilterByMst_NoMatch_Empty()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SetupTemplate(db, svc, 1, 100, DateTime.Today.AddDays(-5));
            var rows = await svc.InvoiceUsageReportAsync("9999999999", null, null, DateTime.Today.Year, null);
            Assert.Empty(rows);
        }
    }

    [Fact]
    public async Task InvoiceUsage_EmptyYear_NoIssued()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SetupTemplate(db, svc, 1, 100, DateTime.Today.AddDays(-5));
            // Năm không có hiệu lực mẫu → không phát hành trong kỳ.
            var rows = await svc.InvoiceUsageReportAsync(null, null, null, DateTime.Today.Year - 5, null);
            var r = Assert.Single(rows);
            Assert.Equal(0, r.QtyIssued);
        }
    }

    // ===== Phân quyền nhóm người dùng theo đối tượng (theo Sys_Access của TVAN gốc) =====

    private static async Task<(int groupId, string groupCode)> SetupGroup(AppDbContext db, string code = "KETOAN")
    {
        var g = new SysGroup { GroupCode = code, GroupName = "Nhóm " + code, FlagActive = true };
        db.SysGroups.Add(g);
        await db.SaveChangesAsync();
        return (g.Id, g.GroupCode);
    }

    private static async Task SetupObject(AppDbContext db, string code, bool active = true)
    {
        db.SysObjects.Add(new SysObject { ObjectCode = code, ObjectName = "Chức năng " + code, ObjectType = SysObjectType.Func, FlagActive = active });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SysAccess_Save_ReplacesAllForGroup()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (gid, gcode) = await SetupGroup(db);
            await SetupObject(db, "INV_ISSUE");
            await SetupObject(db, "INV_CANCEL");

            var (ok1, _) = await svc.SaveSysAccessAsync(gid, new List<string> { "INV_ISSUE" }, "admin");
            Assert.True(ok1);
            Assert.Single(await svc.SysAccessesAsync(gcode));

            // Lưu lại = thay thế toàn bộ danh sách quyền của nhóm.
            var (ok2, _) = await svc.SaveSysAccessAsync(gid, new List<string> { "INV_ISSUE", "INV_CANCEL" }, "admin");
            Assert.True(ok2);
            var list = await svc.SysAccessesAsync(gcode);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, a => a.ObjectCode == "INV_CANCEL");
        }
    }

    [Fact]
    public async Task SysAccess_Save_UnknownObject_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (gid, _) = await SetupGroup(db);
            var (ok, msg) = await svc.SaveSysAccessAsync(gid, new List<string> { "NOPE" }, "admin");
            Assert.False(ok);
            Assert.Contains("không tồn tại", msg);
        }
    }

    [Fact]
    public async Task SysAccess_Save_InactiveObject_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (gid, _) = await SetupGroup(db);
            await SetupObject(db, "INV_ISSUE", active: false);
            var (ok, msg) = await svc.SaveSysAccessAsync(gid, new List<string> { "INV_ISSUE" }, "admin");
            Assert.False(ok);
            Assert.Contains("ngừng dùng", msg);
        }
    }

    [Fact]
    public async Task SysAccess_Deny_AllowsViaGroupMembership()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (gid, gcode) = await SetupGroup(db);
            await SetupObject(db, "INV_ISSUE");
            db.SysUsers.Add(new SysUser { UserCode = "ketoan01", UserName = "KT", FlagActive = true });
            db.SysUserInGroups.Add(new SysUserInGroup { SysGroupId = gid, GroupCode = gcode, UserCode = "ketoan01" });
            await db.SaveChangesAsync();
            await svc.SaveSysAccessAsync(gid, new List<string> { "INV_ISSUE" }, "admin");

            var (allowed, _) = await svc.SysAccessDenyAsync("ketoan01", "INV_ISSUE");
            Assert.True(allowed);
            // Đối tượng không được cấp → từ chối.
            await SetupObject(db, "INV_CANCEL");
            var (denied, _) = await svc.SysAccessDenyAsync("ketoan01", "INV_CANCEL");
            Assert.False(denied);
        }
    }

    [Fact]
    public async Task SysAccess_Deny_SysAdminAlwaysAllowed()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SetupObject(db, "INV_ISSUE");
            db.SysUsers.Add(new SysUser { UserCode = "admin", UserName = "QT", FlagSysAdmin = true, FlagActive = true });
            await db.SaveChangesAsync();
            var (allowed, _) = await svc.SysAccessDenyAsync("admin", "INV_ISSUE");
            Assert.True(allowed);
        }
    }

    // ===== Lịch sử đăng ký dịch vụ (theo Hist_RegisterServices của TVAN gốc) =====

    [Fact]
    public async Task HistRegister_Create_SetsSentTct_WithMessageAndRefNo()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, id) = await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "Mới", "1,2", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            Assert.True(ok);
            Assert.Contains("thành công", msg);
            var e = await svc.GetHistRegisterServiceAsync(id);
            Assert.NotNull(e);
            Assert.Equal(RegServiceStatus.SentTCT, e!.TThai);
            Assert.StartsWith("K", e.MTDiep);
            Assert.StartsWith("V", e.TCTRefNo);
            Assert.Equal("100", e.MLTDiep);
        }
    }

    [Fact]
    public async Task HistRegister_Create_MissingMst_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateHistRegisterServiceAsync("  ", DateTime.Today, "Mới", "1", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            Assert.False(ok);
            Assert.Contains("MST", msg);
        }
    }

    [Fact]
    public async Task HistRegister_Create_MissingHtdk_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "  ", "1", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            Assert.False(ok);
            Assert.Contains("hình thức đăng ký", msg);
        }
    }

    [Fact]
    public async Task HistRegister_List_FilterByMstAndStatus()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "Mới", "1,2", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            await svc.CreateHistRegisterServiceAsync("0312345678", DateTime.Today, "Mới", "1", "K", false, false, true, RegSendMethod.BTH, "100", null, null, "kế toán");

            Assert.Equal(2, (await svc.HistRegisterServicesAsync(null, null, null, null, null, null, null)).Count);
            Assert.Single(await svc.HistRegisterServicesAsync("0101243150", null, null, null, null, null, null));
            Assert.Single(await svc.HistRegisterServicesAsync(null, null, "K", null, null, null, null));
            Assert.Equal(2, (await svc.HistRegisterServicesAsync(null, null, null, RegServiceStatus.SentTCT, null, null, null)).Count);
        }
    }

    [Fact]
    public async Task HistRegister_List_FilterByDateRange()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today.AddDays(-10), "Mới", "1", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "Mới", "1", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");

            Assert.Single(await svc.HistRegisterServicesAsync(null, null, null, null, null, DateTime.Today.AddDays(-1), null));
            Assert.Equal(2, (await svc.HistRegisterServicesAsync(null, null, null, null, null, DateTime.Today.AddDays(-20), null)).Count);
        }
    }

    [Fact]
    public async Task HistRegister_Receive102_SetsReceiveStatus()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "Mới", "1,2", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            var (ok, msg) = await svc.ReceiveHistRegisterServiceResultAsync(id, "102", true, null, "CQT đã tiếp nhận tờ khai.", "kế toán");
            Assert.True(ok);
            Assert.Contains("chấp nhận", msg);
            var e = await svc.GetHistRegisterServiceAsync(id);
            Assert.Equal(RegServiceStatus.Receive, e!.TThai);
            Assert.Equal("ACCEPT", e.TCTTiepNhan);
            Assert.Equal("102", e.MLTDiep);
        }
    }

    [Fact]
    public async Task HistRegister_Receive103_Accept_UpdatesNntMccqt()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var (_, _, id) = await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "Mới", "1,2", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            var (ok, _) = await svc.ReceiveHistRegisterServiceResultAsync(id, "103", true, "A1B2C", "CQT chấp nhận tờ khai.", "kế toán");
            Assert.True(ok);
            var e = await svc.GetHistRegisterServiceAsync(id);
            Assert.Equal(RegServiceStatus.Accept, e!.TThai);
            Assert.Equal("ACCEPT", e.TCTChapNhan);
            Assert.Equal("A1B2C", e.MCCQT);
            Assert.Equal("A1B2C", (await svc.GetNntAsync(nntId))!.MCCQT);
        }
    }

    [Fact]
    public async Task HistRegister_Receive103_Reject_SetsRejectStatus()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "Mới", "1", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            var (ok, msg) = await svc.ReceiveHistRegisterServiceResultAsync(id, "103", false, null, "Tờ khai không hợp lệ.", "kế toán");
            Assert.True(ok);
            Assert.Contains("từ chối", msg);
            var e = await svc.GetHistRegisterServiceAsync(id);
            Assert.Equal(RegServiceStatus.Reject, e!.TThai);
            Assert.Equal("REJECT", e.TCTChapNhan);
        }
    }

    [Fact]
    public async Task HistRegister_Receive_NotSentTct_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "Mới", "1", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            // Nhận lần 1 (102) → chuyển sang Receive, không còn SENTTCT.
            await svc.ReceiveHistRegisterServiceResultAsync(id, "102", true, null, null, "kế toán");
            var (ok, msg) = await svc.ReceiveHistRegisterServiceResultAsync(id, "103", true, null, null, "kế toán");
            Assert.False(ok);
            Assert.Contains("SENTTCT", msg);
        }
    }

    [Fact]
    public async Task HistRegister_Receive_InvalidMltDiep_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateHistRegisterServiceAsync("0101243150", DateTime.Today, "Mới", "1", "C", true, false, false, RegSendMethod.Full, "100", null, null, "kế toán");
            var (ok, msg) = await svc.ReceiveHistRegisterServiceResultAsync(id, "999", true, null, null, "kế toán");
            Assert.False(ok);
            Assert.Contains("102", msg);
        }
    }

    // Sửa lỗi hàng loạt hóa đơn theo mẫu (theo luồng Invoice_Invoice_Fix của TVAN gốc).
    private static async Task<(int nntId, int tplId, int inv1, int inv2)> SetupFix(AppDbContext db, ITvanService svc)
    {
        var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
        await svc.RegisterNntAsync(nntId);
        var (_, _, inv1) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 10_000_000 });
        var (_, _, inv2) = await svc.CreateInvoiceAsync(new Invoice { NntId = nntId, Symbol = "1C26TAA", BuyerName = "Cty Mua", Amount = 20_000_000 });
        await svc.TransmitAsync(inv1);   // inv1 → Accepted (ISSUED)
        await svc.TransmitAsync(inv2);   // inv2 → Accepted (ISSUED)
        var tplId = await AddTemplate(db, nntId);
        return (nntId, tplId, inv1, inv2);
    }

    [Fact]
    public async Task BulkFix_ByTemplate_ReSignsAllAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, inv1, inv2) = await SetupFix(db, svc);
            var (ok, msg, count) = await svc.BulkFixByTemplateAsync("TINV-1C26TAA", "sai thuế suất", "kế toán trưởng");
            Assert.True(ok);
            Assert.Equal(2, count);
            Assert.Equal(HotfixFlag.Hotfixed, (await svc.GetInvoiceAsync(inv1))!.FlagHotfix);
            Assert.Equal(HotfixFlag.Hotfixed, (await svc.GetInvoiceAsync(inv2))!.FlagHotfix);
            var logs = await svc.BulkFixLogsAsync(null);
            Assert.Single(logs);
            Assert.Equal(2, logs[0].FixedCount);
            Assert.Equal("kế toán trưởng", logs[0].By);
        }
    }

    [Fact]
    public async Task BulkFix_EmptyCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, count) = await svc.BulkFixByTemplateAsync("  ", null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Contains("mã mẫu", msg);
        }
    }

    [Fact]
    public async Task BulkFix_UnknownTemplate_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, count) = await svc.BulkFixByTemplateAsync("TINV-KHONG-CO", null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Contains("Không tìm thấy mẫu", msg);
        }
    }

    [Fact]
    public async Task BulkFix_NoCandidates_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            await svc.RegisterNntAsync(nntId);
            await AddTemplate(db, nntId);   // mẫu có nhưng chưa có HĐ nào đã phát hành
            var (ok, msg, count) = await svc.BulkFixByTemplateAsync("TINV-1C26TAA", null, null);
            Assert.False(ok);
            Assert.Equal(0, count);
            Assert.Contains("Không có hóa đơn", msg);
        }
    }

    [Fact]
    public async Task BulkFix_AlreadyHotfixed_NotCandidate()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, inv1, inv2) = await SetupFix(db, svc);
            await svc.ReSignAsync(inv1, "PD94bWwgdmVyc2lvbj0iMS4wIj8+", null, null);   // inv1 đã ký lại
            var candidates = await svc.BulkFixCandidatesAsync("TINV-1C26TAA");
            Assert.Single(candidates);
            Assert.Equal(inv2, candidates[0].Id);
        }
    }

    // Danh mục phương thức thanh toán (theo Mst_PaymentMethods của TVAN gốc).
    [Fact]
    public async Task PaymentMethod_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.PaymentMethods.AddRange(
                new PaymentMethodMaster { PaymentMethodCode = "TM", PaymentMethodName = "Tiền mặt", FlagActive = true },
                new PaymentMethodMaster { PaymentMethodCode = "CK", PaymentMethodName = "Chuyển khoản", FlagActive = true });
            await db.SaveChangesAsync();
            Assert.Equal(2, (await svc.PaymentMethodsAsync(null)).Count);
            Assert.Single(await svc.PaymentMethodsAsync("Chuyển"));
        }
    }

    [Fact]
    public async Task PaymentMethod_Check_Existing_Active_Ok()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.PaymentMethods.Add(new PaymentMethodMaster { PaymentMethodCode = "TM", PaymentMethodName = "Tiền mặt", FlagActive = true });
            await db.SaveChangesAsync();
            var (ok, msg) = await svc.CheckPaymentMethodAsync("TM", true, true);
            Assert.True(ok);
            Assert.Contains("hợp lệ", msg);
        }
    }

    [Fact]
    public async Task PaymentMethod_Check_Missing_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg) = await svc.CheckPaymentMethodAsync("XX", true, true);
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task PaymentMethod_Check_Inactive_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            db.PaymentMethods.Add(new PaymentMethodMaster { PaymentMethodCode = "TM", PaymentMethodName = "Tiền mặt", FlagActive = false });
            await db.SaveChangesAsync();
            var (ok, msg) = await svc.CheckPaymentMethodAsync("TM", true, true);
            Assert.False(ok);
            Assert.Contains("ngừng dùng", msg);
        }
    }

    // Nhập hóa đơn từ Excel (theo luồng Invoice_ImportExcel của TVAN gốc).
    [Fact]
    public async Task InvoiceImport_Create_ComputesSummary()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var rows = new List<InvoiceImportRow>
            {
                new() { Idx = 1, InvoiceNo = "00000010", InvoiceStatus = "ISSUED", FlagResult = ImportFlagResult.Success },
                new() { Idx = 2, InvoiceNo = "00000011", InvoiceStatus = "ISSUED", FlagResult = ImportFlagResult.Success },
                new() { Idx = 3, InvoiceNo = "", InvoiceStatus = "", FlagResult = ImportFlagResult.Fail },
                new() { Idx = 4, InvoiceNo = "", InvoiceStatus = "", FlagResult = ImportFlagResult.Skip }
            };
            var (ok, msg, id) = await svc.CreateInvoiceImportBatchAsync("IMP-1", "a.xlsx", ImportType.LuuVaCapSo, rows, null, "kt");
            Assert.True(ok);
            var b = await svc.GetInvoiceImportBatchAsync(id);
            Assert.NotNull(b);
            Assert.Equal(4, b!.TotalRows);
            Assert.Equal(4, b.TotalInvoices);
            Assert.Equal(1, b.Skipped);
            Assert.Equal(2, b.Succeeded);
            Assert.Equal(1, b.Failed);
        }
    }

    [Fact]
    public async Task InvoiceImport_PhatHanh_RequiresIssued()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var rows = new List<InvoiceImportRow>
            {
                new() { Idx = 1, InvoiceStatus = "ISSUED", FlagResult = ImportFlagResult.Success },
                new() { Idx = 2, InvoiceStatus = "PENDING", FlagResult = ImportFlagResult.Success }
            };
            var (ok, _, id) = await svc.CreateInvoiceImportBatchAsync("IMP-2", "b.xlsx", ImportType.PhatHanh, rows, null, null);
            Assert.True(ok);
            var b = await svc.GetInvoiceImportBatchAsync(id);
            Assert.Equal(1, b!.Succeeded);   // chỉ dòng có InvoiceStatus = ISSUED mới tính thành công
            Assert.Equal(1, b.Failed);
        }
    }

    [Fact]
    public async Task InvoiceImport_DuplicateBatchNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateInvoiceImportBatchAsync("IMP-3", "c.xlsx", ImportType.Luu, new List<InvoiceImportRow>(), null, null);
            var (ok, msg, _) = await svc.CreateInvoiceImportBatchAsync("IMP-3", "d.xlsx", ImportType.Luu, new List<InvoiceImportRow>(), null, null);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task InvoiceImport_MissingBatchNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateInvoiceImportBatchAsync("", "e.xlsx", ImportType.Luu, new List<InvoiceImportRow>(), null, null);
            Assert.False(ok);
            Assert.Contains("số lô nhập", msg);
        }
    }

    [Fact]
    public async Task InvoiceImport_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateInvoiceImportBatchAsync("IMP-A", "thang6.xlsx", ImportType.Luu, new List<InvoiceImportRow>(), null, null);
            await svc.CreateInvoiceImportBatchAsync("IMP-B", "thang7.xlsx", ImportType.Luu, new List<InvoiceImportRow>(), null, null);
            Assert.Equal(2, (await svc.InvoiceImportBatchesAsync(null)).Count);
            Assert.Single(await svc.InvoiceImportBatchesAsync("thang6"));
        }
    }

    // ===== Đơn hàng license + hoa hồng đại lý (theo Inos_LicOrder / RptSv_InosLicOrder_Commission của TVAN gốc) =====

    [Fact]
    public async Task LicOrder_Save_CreatesPendingOrder()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var lines = new List<LicOrderLine> { new("PKG-BASIC", "Gói cơ bản", LicOrderType.RegisterLic, 12_000_000, 1) };
            var (ok, _, id) = await svc.SaveLicOrderAsync(null, "ORD-1", "ORG001", "Cty A", "0101243150", "DL001", "SALE10", 12_000_000, 10_800_000, "PAY-1", "Chưa thanh toán", LicOrderStatus.Pending, null, lines, "admin");
            Assert.True(ok);
            var o = await svc.GetLicOrderAsync(id);
            Assert.Equal(LicOrderStatus.Pending, o!.Status);
            Assert.Single(o.Details);
            Assert.Equal(1_200_000, o.DiscountVal);
        }
    }

    [Fact]
    public async Task LicOrder_DuplicateOrderNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveLicOrderAsync(null, "ORD-1", "ORG001", null, null, null, null, 0, 0, null, null, LicOrderStatus.Pending, null, new(), null);
            var (ok, msg, _) = await svc.SaveLicOrderAsync(null, "ORD-1", "ORG002", null, null, null, null, 0, 0, null, null, LicOrderStatus.Pending, null, new(), null);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task LicOrder_MissingOrderNo_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.SaveLicOrderAsync(null, "", "ORG001", null, null, null, null, 0, 0, null, null, LicOrderStatus.Pending, null, new(), null);
            Assert.False(ok);
            Assert.Contains("số đơn hàng", msg);
        }
    }

    [Fact]
    public async Task LicOrder_Approve_SetsApproved()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveLicOrderAsync(null, "ORD-1", "ORG001", null, null, null, null, 0, 0, null, null, LicOrderStatus.Pending, null, new(), null);
            var (ok, _) = await svc.ApproveLicOrderAsync(id, "admin");
            Assert.True(ok);
            var o = await svc.GetLicOrderAsync(id);
            Assert.Equal(LicOrderStatus.Approved, o!.Status);
            Assert.NotNull(o.ApproveDTime);
        }
    }

    [Fact]
    public async Task LicOrder_ApproveCancelled_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveLicOrderAsync(null, "ORD-1", "ORG001", null, null, null, null, 0, 0, null, null, LicOrderStatus.Pending, null, new(), null);
            await svc.CancelLicOrderAsync(id, "admin");
            var (ok, msg) = await svc.ApproveLicOrderAsync(id, "admin");
            Assert.False(ok);
            Assert.Contains("đã hủy", msg);
        }
    }

    [Fact]
    public async Task LicOrder_ConfirmPayment_SetsProcessing()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveLicOrderAsync(null, "ORD-1", "ORG001", null, null, null, null, 0, 0, null, null, LicOrderStatus.NotPaid, null, new(), null);
            var (ok, _) = await svc.ConfirmLicOrderPaymentAsync(id, "admin");
            Assert.True(ok);
            var o = await svc.GetLicOrderAsync(id);
            Assert.Equal(LicOrderStatus.Processing, o!.Status);
            Assert.Equal("Đã thanh toán", o.PaymentStatusDesc);
        }
    }

    [Fact]
    public async Task LicOrderCommission_Save_CreatesPending()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveLicOrderCommissionAsync(null, "ORD-1", "0101243150", "DL001", "A", "B", "C", "D", "E", 1_000_000, 0, 500_000, 300_000, 200_000, null, "admin");
            Assert.True(ok);
            var c = await svc.GetLicOrderCommissionAsync(id);
            Assert.Equal(CommissionStatus.Pending, c!.CommissionStatus);
            Assert.Equal(2_000_000, c.TotalCommission);
        }
    }

    [Fact]
    public async Task LicOrderCommission_DuplicateOrder_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveLicOrderCommissionAsync(null, "ORD-1", null, null, null, null, null, null, null, 0, 0, 0, 0, 0, null, null);
            var (ok, msg, _) = await svc.SaveLicOrderCommissionAsync(null, "ORD-1", null, null, null, null, null, null, null, 0, 0, 0, 0, 0, null, null);
            Assert.False(ok);
            Assert.Contains("đã có bản ghi hoa hồng", msg);
        }
    }

    [Fact]
    public async Task LicOrderCommission_Approve_SetsApprove()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveLicOrderCommissionAsync(null, "ORD-1", null, null, null, null, null, null, null, 0, 0, 0, 0, 0, null, null);
            var (ok, _, count) = await svc.ApproveLicOrderCommissionsAsync(new List<int> { id }, "admin");
            Assert.True(ok);
            Assert.Equal(1, count);
            var c = await svc.GetLicOrderCommissionAsync(id);
            Assert.Equal(CommissionStatus.Approve, c!.CommissionStatus);
            Assert.NotNull(c.ApprDTimeUTC);
        }
    }

    [Fact]
    public async Task LicOrderCommission_ApproveNonPending_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveLicOrderCommissionAsync(null, "ORD-1", null, null, null, null, null, null, null, 0, 0, 0, 0, 0, null, null);
            await svc.ApproveLicOrderCommissionsAsync(new List<int> { id }, "admin");
            var (ok, msg, _) = await svc.ApproveLicOrderCommissionsAsync(new List<int> { id }, "admin");
            Assert.False(ok);
            Assert.Contains("không ở trạng thái chờ", msg);
        }
    }

    [Fact]
    public async Task LicOrder_List_FilteredByStatus()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveLicOrderAsync(null, "ORD-1", "ORG001", null, null, null, null, 0, 0, null, null, LicOrderStatus.Pending, null, new(), null);
            var (_, _, id2) = await svc.SaveLicOrderAsync(null, "ORD-2", "ORG002", null, null, null, null, 0, 0, null, null, LicOrderStatus.Pending, null, new(), null);
            await svc.ApproveLicOrderAsync(id2, "admin");
            Assert.Equal(2, (await svc.LicOrdersAsync(null, null, null, null)).Count);
            Assert.Single(await svc.LicOrdersAsync(null, LicOrderStatus.Approved, null, null));
        }
    }

    // Danh mục Loại giấy tờ (theo Mst_GovIDType của TVAN gốc).
    [Fact]
    public async Task GovIdType_Save_Creates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.SaveGovIdTypeAsync(null, "CCCD", "Căn cước công dân", "Giấy tờ tùy thân", true, "kế toán");
            Assert.True(ok);
            var e = await svc.GetGovIdTypeAsync(id);
            Assert.Equal("CCCD", e!.GovIDType);
            Assert.Equal("Căn cước công dân", e.GovIDTypeName);
            Assert.True(e.FlagActive);
        }
    }

    [Fact]
    public async Task GovIdType_Save_SameCode_Updates()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveGovIdTypeAsync(null, "CCCD", "Căn cước", null, true, "kế toán");
            // Lưu lại cùng mã = cập nhật (không tạo bản ghi mới, không báo trùng).
            var (ok, _, id2) = await svc.SaveGovIdTypeAsync(null, "CCCD", "Căn cước công dân", "cập nhật", true, "kế toán");
            Assert.True(ok);
            Assert.Equal(id, id2);
            Assert.Single(await svc.GovIdTypesAsync(null));
            Assert.Equal("Căn cước công dân", (await svc.GetGovIdTypeAsync(id))!.GovIDTypeName);
        }
    }

    [Fact]
    public async Task GovIdType_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveGovIdTypeAsync(null, "  ", "Căn cước", null, true, "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã loại giấy tờ", msg1);
            var (ok2, msg2, _) = await svc.SaveGovIdTypeAsync(null, "CCCD", "  ", null, true, "kế toán");
            Assert.False(ok2);
            Assert.Contains("tên loại giấy tờ", msg2);
        }
    }

    [Fact]
    public async Task GovIdType_List_FilteredByKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveGovIdTypeAsync(null, "CCCD", "Căn cước công dân", null, true, "kế toán");
            await svc.SaveGovIdTypeAsync(null, "HOPCHIEU", "Hộ chiếu", null, true, "kế toán");
            var all = await svc.GovIdTypesAsync(null);
            Assert.Equal(2, all.Count);
            var filtered = await svc.GovIdTypesAsync("HOPCHIEU");
            Assert.Single(filtered);
            Assert.Equal("HOPCHIEU", filtered[0].GovIDType);
        }
    }

    [Fact]
    public async Task GovIdType_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveGovIdTypeAsync(null, "CMND", "Chứng minh nhân dân", null, true, "kế toán");
            var (ok, _) = await svc.DeleteGovIdTypeAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetGovIdTypeAsync(id));
        }
    }

    // ===== Mẫu thông điệp trao đổi với cơ quan thuế (theo Mst_MessageTemplate của TVAN gốc) =====

    [Fact]
    public async Task TctMessageTemplate_Save_CreatesWithFields()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var fields = new List<TctMessageTemplateField>
            {
                new("MST", "TEXT", "Mã số thuế"),
                new("TNNT", "TEXT", "Tên NNT")
            };
            var (ok, msg, id) = await svc.SaveTctMessageTemplateAsync(null, "MSG100", "Tờ khai đăng ký HĐĐT", TctMessageTypeCode.Register100, "<TKhai/>", "01DKHDDT.xml", "Templates/01DKHDDT.xml", true, fields, "kế toán");
            Assert.True(ok);
            Assert.Contains("tạo", msg);
            var e = await svc.GetTctMessageTemplateAsync(id);
            Assert.NotNull(e);
            Assert.Equal(TctMessageTypeCode.Register100, e!.MessageTypeCode);
            Assert.Equal(2, e.Details.Count);
        }
    }

    [Fact]
    public async Task TctMessageTemplate_Save_Update_ReplacesFields()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveTctMessageTemplateAsync(null, "MSG300", "Thông báo sai sót", TctMessageTypeCode.Error300, null, null, null, true,
                new List<TctMessageTemplateField> { new("MST", "TEXT", null) }, "kế toán");
            var (ok, msg, _) = await svc.SaveTctMessageTemplateAsync(id, "MSG300", "Thông báo HĐĐT sai sót", TctMessageTypeCode.Error300, "<TDiep/>", null, null, true,
                new List<TctMessageTemplateField> { new("MST", "TEXT", null), new("MCCQT", "TEXT", null) }, "kế toán");
            Assert.True(ok);
            Assert.Contains("cập nhật", msg);
            var e = await svc.GetTctMessageTemplateAsync(id);
            Assert.Equal("Thông báo HĐĐT sai sót", e!.MessageTplName);
            Assert.Equal(2, e.Details.Count);
        }
    }

    [Fact]
    public async Task TctMessageTemplate_Save_DuplicateCode_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveTctMessageTemplateAsync(null, "MSG100", "A", TctMessageTypeCode.Register100, null, null, null, true, new(), "kế toán");
            var (ok, msg, _) = await svc.SaveTctMessageTemplateAsync(null, "MSG100", "B", TctMessageTypeCode.Register100, null, null, null, true, new(), "kế toán");
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task TctMessageTemplate_Save_MissingCodeOrName_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok1, msg1, _) = await svc.SaveTctMessageTemplateAsync(null, "  ", "A", TctMessageTypeCode.Register100, null, null, null, true, new(), "kế toán");
            Assert.False(ok1);
            Assert.Contains("mã mẫu", msg1);
            var (ok2, msg2, _) = await svc.SaveTctMessageTemplateAsync(null, "MSG100", "  ", TctMessageTypeCode.Register100, null, null, null, true, new(), "kế toán");
            Assert.False(ok2);
            Assert.Contains("tên mẫu", msg2);
        }
    }

    [Fact]
    public async Task TctMessageTemplate_List_FilteredByTypeAndKeyword()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.SaveTctMessageTemplateAsync(null, "MSG100", "Tờ khai đăng ký", TctMessageTypeCode.Register100, null, null, null, true, new(), "kế toán");
            await svc.SaveTctMessageTemplateAsync(null, "MSG300", "Thông báo sai sót", TctMessageTypeCode.Error300, null, null, null, true, new(), "kế toán");
            Assert.Equal(2, (await svc.TctMessageTemplatesAsync(null, null)).Count);
            Assert.Single(await svc.TctMessageTemplatesAsync(TctMessageTypeCode.Error300, null));
            Assert.Single(await svc.TctMessageTemplatesAsync(null, "MSG100"));
        }
    }

    [Fact]
    public async Task TctMessageTemplate_Delete_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.SaveTctMessageTemplateAsync(null, "MSG100", "A", TctMessageTypeCode.Register100, null, null, null, true, new(), "kế toán");
            var (ok, _) = await svc.DeleteTctMessageTemplateAsync(id);
            Assert.True(ok);
            Assert.Null(await svc.GetTctMessageTemplateAsync(id));
        }
    }

    // Tạo NNT đầy đủ (có số chứng thư số) để test sinh XML đăng ký thay đổi CTS.
    private static async Task<int> SetupNntWithCa(ITvanService svc, string caNumber = "VNPT-CA-0101243150")
    {
        var p = new NntProfile("0101243150", "Cty Bán", null, null, null, null, "Hà Nội", null, null, null,
            "Nguyễn Văn A", null, "Giám đốc", null, null, null, "Nguyễn Văn A", "0900000000", "kt@cty.vn", null,
            caNumber, "VNPT-CA", null, null, null, null, null, null, null, null, true, "kế toán");
        var (_, _, id) = await svc.SaveNntAsync(null, p);
        return id;
    }

    [Fact]
    public async Task GenNntUpdateXml_WithCa_ReturnsXmlAndLogs()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var nntId = await SetupNntWithCa(svc);
            var (ok, msg, xmlBase64, logId) = await svc.GenNntUpdateXmlAsync(nntId, "kế toán");
            Assert.True(ok);
            Assert.True(logId > 0);
            var xml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(xmlBase64));
            Assert.Contains("<maDKy>217</maDKy>", xml);
            Assert.Contains("<mauDKy>02-DK_T-VAN</mauDKy>", xml);
            Assert.Contains("<serial>VNPT-CA-0101243150</serial>", xml);
            Assert.Single(await svc.NntXmlLogsAsync(nntId));
        }
    }

    [Fact]
    public async Task GenNntUpdateXml_NoCa_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, nntId) = await svc.CreateNntAsync(new Nnt { Mst = "0101243150", Name = "Cty Bán" });
            var (ok, msg, _, _) = await svc.GenNntUpdateXmlAsync(nntId, "kế toán");
            Assert.False(ok);
            Assert.Contains("chứng thư số", msg);
            Assert.Empty(await svc.NntXmlLogsAsync(nntId));
        }
    }

    [Fact]
    public async Task GenNntUpdateXml_UnknownNnt_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _, _) = await svc.GenNntUpdateXmlAsync(9999, "kế toán");
            Assert.False(ok);
            Assert.Contains("Không tìm thấy", msg);
        }
    }

    [Fact]
    public async Task NntXmlLogs_FilteredByNnt()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var nntId = await SetupNntWithCa(svc);
            await svc.GenNntUpdateXmlAsync(nntId, "kế toán");
            await svc.GenNntUpdateXmlAsync(nntId, "kế toán");
            Assert.Equal(2, (await svc.NntXmlLogsAsync(nntId)).Count);
            Assert.Equal(2, (await svc.NntXmlLogsAsync(null)).Count);
            Assert.Empty(await svc.NntXmlLogsAsync(9999));
        }
    }
}
