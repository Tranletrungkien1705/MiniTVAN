using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniTVAN.Data;
using MiniTVAN.Models;
using MiniTVAN.Services;

namespace MiniTVAN.Controllers;

public class HomeController : Controller
{
    // SPA React ở "/". Trang tra cứu công khai /Lookup (Razor) giữ nguyên.
    public IActionResult Index() => Redirect("/index.html");
}

public class LegacyController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.Dash = await svc.DashboardAsync(); return View("~/Views/Home/Index.cshtml"); }
}

public class NntController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword, string? mst, string? dlCode, RegStatus? regStatus)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Mst = mst;
        ViewBag.DlCode = dlCode;
        ViewBag.RegStatus = regStatus;
        ViewBag.Dealers = await svc.DealersAsync(null, null);
        ViewBag.Provinces = await svc.ProvincesAsync(null);
        ViewBag.Districts = await svc.DistrictsAsync(null, null);
        ViewBag.TaxOffices = await svc.TaxOfficesAsync();
        return View(await svc.NntsAsync(keyword, mst, dlCode, regStatus));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string mst, string name, string? address, string? email)
    {
        var (ok, msg, _) = await svc.CreateNntAsync(new Nnt { Mst = mst ?? "", Name = name ?? "", Address = address, Email = email });
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(int id)
    {
        var (ok, msg) = await svc.RegisterNntAsync(id);
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Index));
    }

    // Lưu hồ sơ NNT đầy đủ (theo Mst_NNT_Create/Update của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string mst, string name, string? mstParent, string? provinceCode, string? districtCode, string? dlCode,
        string? address, string? mobile, string? phone, string? fax, string? presentBy, string? businessRegNo, string? nntPosition,
        string? presentIdNo, string? presentIdType, string? govTaxID, string? contactName, string? contactPhone, string? contactEmail,
        string? website, string? caNumber, string? caOrg, DateTime? caEffStart, DateTime? caEffEnd, string? accNo, string? accHolder,
        string? bankName, string? bizType, string? bizFieldCode, string? bizSizeCode, bool active, string? by)
    {
        var p = new NntProfile(mst, name, mstParent, provinceCode, districtCode, dlCode, address, mobile, phone, fax, presentBy,
            businessRegNo, nntPosition, presentIdNo, presentIdType, govTaxID, contactName, contactPhone, contactEmail, website,
            caNumber, caOrg, caEffStart, caEffEnd, accNo, accHolder, bankName, bizType, bizFieldCode, bizSizeCode, active, by);
        var (ok, msg, _) = await svc.SaveNntAsync(id, p);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Xóa NNT theo id (theo Mst_NNT_Delete của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteNntAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Cập nhật trạng thái đăng ký NNT (theo Mst_NNT_UpdateRegisterStatusX của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRegisterStatus(int id, RegStatus status, string? remark, string? by)
    {
        var (ok, msg) = await svc.UpdateNntRegisterStatusAsync(id, status, remark, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class InvoiceController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(InvoiceStatus? status, int? nntId)
    {
        ViewBag.Status = status; ViewBag.NntId = nntId; ViewBag.Nnts = await svc.NntsAsync();
        return View(await svc.InvoicesAsync(status, nntId));
    }

    public async Task<IActionResult> Create() { ViewBag.Nnts = await svc.NntsAsync(); return View(); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int nntId, string? symbol, string? no, string buyerName, string? buyerMst, string? buyerAddress, decimal amount, decimal vatRate, DateTime issuedDate)
    {
        var (ok, msg, id) = await svc.CreateInvoiceAsync(new Invoice
        {
            NntId = nntId, Symbol = symbol ?? "", No = no ?? "", BuyerName = buyerName ?? "", BuyerMst = buyerMst, BuyerAddress = buyerAddress,
            Amount = amount, VatRate = vatRate <= 0 ? 10 : vatRate, IssuedDate = issuedDate == default ? DateTime.Today : issuedDate
        });
        TempData[ok ? "Success" : "Error"] = msg;
        if (!ok) { ViewBag.Nnts = await svc.NntsAsync(); return RedirectToAction(nameof(Create)); }
        return RedirectToAction(nameof(Detail), new { id });
    }

    public async Task<IActionResult> Detail(int id)
    {
        var inv = await svc.GetInvoiceAsync(id);
        if (inv == null) return NotFound();
        ViewBag.Messages = await svc.MessagesAsync(id);
        ViewBag.EmailLogs = await svc.EmailLogsAsync(id);
        ViewBag.ConvLogs = await svc.ConversionPrintLogsAsync(id);
        ViewBag.ReSignLogs = await svc.ReSignLogsAsync(id);
        ViewBag.ApproveLogs = await svc.ApproveLogsAsync(id);
        ViewBag.IssueLogs = await svc.IssueLogsAsync(id);
        ViewBag.TctLogs = await svc.TctReceiveLogsAsync(id);
        ViewBag.Tct300Logs = await svc.Tct300LogsAsync(id);
        ViewBag.UpdateLogs = await svc.UpdateLogsAsync(id);
        ViewBag.CancelLogs = await svc.CancelLogsAsync(id);
        ViewBag.RecordLogs = await svc.RecordLogsAsync(id);
        return View(inv);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transmit(int id)
    {
        var (ok, msg) = await svc.TransmitAsync(id);
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var (ok, msg) = await svc.CancelAsync(id);
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int id, InvoiceAdjType adjType, decimal amount, decimal vatRate, string? reason)
    {
        var (ok, msg, newId) = await svc.AdjustAsync(id, adjType, amount, vatRate, reason);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id = newId }) : RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Replace(int id, decimal amount, decimal vatRate, string? reason)
    {
        var (ok, msg, newId) = await svc.ReplaceAsync(id, amount, vatRate, reason);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id = newId }) : RedirectToAction(nameof(Detail), new { id });
    }

    // Chuyển hóa đơn về trạng thái chờ (PENDING) — giữ nguyên số (theo Invoice_Invoice_Support_InvoiceToPending của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToPending(int id, string? reason)
    {
        var (ok, msg) = await svc.ResetToPendingAsync(id, reason);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Khôi phục hóa đơn đã hủy (DELETED) về trạng thái đã phát hành (ISSUED) để làm thông báo sai sót
    // (theo Invoice_Invoice_Support_BackInvoiceStatus của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, string? reason)
    {
        var (ok, msg) = await svc.RestoreInvoiceAsync(id, reason);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Xóa hóa đơn điều chỉnh/thay thế chưa phát hành (theo Invoice_Invoice_Support_DeleteInvoiceRefNo của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdjustReplace(int id, string? reason)
    {
        var (ok, msg) = await svc.DeleteAdjustReplaceAsync(id, reason);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Detail), new { id });
    }

    // Xóa hóa đơn đã phát hành (theo Invoice_Invoice_Deleted của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? remark, string? by)
    {
        var (ok, msg) = await svc.DeleteInvoiceAsync(id, remark, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Gửi/gửi lại email hóa đơn đã phát hành cho người mua (theo Invoice_Invoice_Support_SendMail của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(int id, string? toEmail, string? sentBy)
    {
        var (ok, msg, _) = await svc.SendInvoiceEmailAsync(id, toEmail, sentBy);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // In chuyển đổi hóa đơn (theo Invoice_Invoice.FlagChange của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConversionPrint(int id, string? note, string? by)
    {
        var (ok, msg) = await svc.MarkConversionPrintedAsync(id, note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Bỏ cờ in chuyển đổi (theo Invoice_Invoice_Support_BackFlagChange của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetConversionPrint(int id, string? note, string? by)
    {
        var (ok, msg) = await svc.ResetConversionPrintAsync(id, note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Ký lại hóa đơn đã phát hành (theo Invoice_Invoice_ReSign của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReSign(int id, string? fileSpec, string? note, string? by)
    {
        var (ok, msg) = await svc.ReSignAsync(id, fileSpec, note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc): PENDING → APPROVED.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? filePath, string? pdfFilePath, string? note, string? by)
    {
        var (ok, msg) = await svc.ApproveAsync(id, filePath, pdfFilePath, note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Bỏ duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc): APPROVED → PENDING.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unapprove(int id, string? note, string? by)
    {
        var (ok, msg) = await svc.UnapproveAsync(id, note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Duyệt NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_ApprovedMulti của TVAN gốc):
    // duyệt hàng loạt danh sách HĐ đang chờ (PENDING) đã có số → APPROVED.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApprove(int[] ids, string? note, string? by)
    {
        var (ok, msg, _) = await svc.BulkApproveAsync((ids ?? Array.Empty<int>()).ToList(), note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Phát hành hóa đơn đã duyệt (theo Invoice_Invoice_Issued của TVAN gốc): APPROVED → ISSUED.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int id, string? emailSend, string? note, string? by)
    {
        var (ok, msg) = await svc.IssueAsync(id, emailSend, note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Cấp phát số hóa đơn (theo Invoice_Invoice_AllocatedInv của TVAN gốc): PENDING + chưa có số → cấp số kế tiếp từ mẫu.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AllocateNo(int id, DateTime invoiceDate, string? by)
    {
        var (ok, msg, _) = await svc.AllocateInvoiceNoAsync(id, invoiceDate, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Cấp số + Duyệt + Phát hành trong MỘT bước (theo Invoice_Invoice_AllocatedAndApprovedAndIssued của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AllocateApproveIssue(int id, DateTime invoiceDate, string? filePath, string? pdfFilePath, string? emailSend, string? note, string? by)
    {
        var (ok, msg, _) = await svc.AllocateApproveIssueAsync(id, invoiceDate, filePath, pdfFilePath, emailSend, note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Cấp số hóa đơn khởi tạo từ MÁY TÍNH TIỀN (theo Invoice_Invoice_AllocatedInvoiceTypeM của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AllocateNoTypeM(int id, DateTime invoiceDate, string? by)
    {
        var (ok, msg, _) = await svc.AllocateInvoiceNoTypeMAsync(id, invoiceDate, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Sinh mã CQT trên hóa đơn khởi tạo từ máy tính tiền (theo Invoice_Invoice_GenMCCQTMTTTypeM của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenMccqtMtt(int id)
    {
        var (ok, msg, _) = await svc.GenMccqtMttAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Nhận kết quả phản hồi từ CQT (theo Invoice_Invoice_TCTReceive của TVAN gốc): 202 → Accepted, 204 → Rejected.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TctReceive(int id, TctMessageType mltDiep, string? maCQT, string? maLoi, string? lyDo)
    {
        var (ok, msg) = await svc.ReceiveTctResultAsync(id, mltDiep, maCQT, maLoi, lyDo);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Gửi thông báo hóa đơn sai sót tới CQT (theo Invoice_Invoice_SentTCT_300 của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTct300(int id, ReplaceOrAdjustFlag flagReplaceOrAdjust, string? loaiTb, string? soTb, DateTime? ngayTb, string? lyDo, string? by)
    {
        var (ok, msg, _) = await svc.SendTct300Async(id, flagReplaceOrAdjust, loaiTb, soTb, ngayTb, lyDo, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Cập nhật nội dung hóa đơn sau khi đã cấp số (theo Invoice_Invoice_UpdAfterAllocated của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAfterAllocated(int id, string? buyerName, string? buyerMst, string? buyerAddress, PaymentMethod paymentMethod, decimal amount, decimal vatRate, DateTime invoiceDate, string? note, string? by)
    {
        var (ok, msg) = await svc.UpdateAfterAllocatedAsync(id, buyerName, buyerMst, buyerAddress, paymentMethod, amount, vatRate, invoiceDate, note, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Hủy hóa đơn đang chờ/đã duyệt (theo Invoice_Invoice_Cancel của TVAN gốc): PENDING/APPROVED → CANCELED.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelInvoice(int id, string? remark, string? by)
    {
        var (ok, msg) = await svc.CancelInvoiceAsync(id, remark, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Tạo biên bản đính kèm hóa đơn (theo luồng TaoBienBan của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRecord(int id, RecordType type, string fileName, string? fileSpec, string? reason, string? by)
    {
        var (ok, msg, _) = await svc.CreateRecordAsync(id, type, fileName, fileSpec, reason, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }
}

public class LicenseController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? nntId)
    {
        ViewBag.Nnts = await svc.NntsAsync();
        ViewBag.NntId = nntId;
        ViewBag.Hists = await svc.LicenseHistsAsync(nntId);
        return View(await svc.LicensesAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Increase(int nntId, int qty, string? note)
    {
        var (ok, msg, _) = await svc.IncreaseLicenseAsync(nntId, qty, note);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class GuiTongHopController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? nntId)
    {
        ViewBag.Nnts = await svc.NntsAsync();
        ViewBag.NntId = nntId;
        return View(await svc.GuiTongHopsAsync(nntId));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var g = await svc.GetGuiTongHopAsync(id);
        if (g == null) return NotFound();
        return View(g);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int nntId, PeriodType lkdlieu, string kdlieu, int bslthu, string? note)
    {
        var (ok, msg, id) = await svc.CreateGuiTongHopAsync(nntId, lkdlieu, kdlieu, bslthu, note);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int id)
    {
        var (ok, msg) = await svc.SendGuiTongHopAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }
}

// Bảng tổng hợp hóa đơn (BTH) — theo Invoice_Invoice_BTHGet / Invoice_Invoice_BTHGetX của TVAN gốc:
// liệt kê các hóa đơn đã phát hành (ISSUED) hoặc đã hủy (DELETED) trong một kỳ (ngày/tháng/quý/năm)
// kèm trạng thái TThai (Mới/Huỷ/Điều chỉnh/Thay thế) và thông tin hóa đơn gốc bị điều chỉnh/thay thế.
// Dùng để đối chiếu trước khi lập bảng tổng hợp gửi CQT.
public class BthController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(PeriodType lkdlieu = PeriodType.Month, string? kdlieu = null)
    {
        kdlieu = string.IsNullOrWhiteSpace(kdlieu) ? DateTime.Today.ToString("yyyy-MM") : kdlieu.Trim();
        ViewBag.LKDLieu = lkdlieu;
        ViewBag.KDLieu = kdlieu;
        return View(await svc.BthRowsAsync(lkdlieu, kdlieu));
    }
}

public class LookupController(ITvanService svc) : Controller
{
    [Route("Lookup/{code?}")]
    public async Task<IActionResult> Index(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return View("Search");
        ViewBag.Code = code;
        var inv = await svc.LookupByCodeAsync(code);
        if (inv == null) { ViewBag.NotFound = true; return View("Search"); }
        return View(inv);
    }
}

// Tra cứu thông tin người nộp thuế theo MST từ cơ quan thuế (theo TCT_TraTTinMaSoThue của TVAN gốc).
public class TaxLookupController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? mst)
    {
        ViewBag.Mst = mst;
        ViewBag.Offices = await svc.TaxOfficesAsync();
        ViewBag.Logs = await svc.NntLookupLogsAsync(mst);
        if (!string.IsNullOrWhiteSpace(mst))
        {
            var (ok, msg, log) = await svc.LookupNntByMstAsync(mst);
            ViewBag.Ok = ok; ViewBag.Msg = msg; ViewBag.Result = log;
        }
        return View();
    }
}

// Danh mục cơ quan thuế (theo Mst_GovTaxID của TVAN gốc):
// cây CQT phân cấp, tự tính mã đơn vị nghiệp vụ/mẫu/cấp từ cây, gắn với địa giới hành chính.
public class TaxOfficeController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Provinces = await svc.ProvincesAsync(null);
        ViewBag.Districts = await svc.DistrictsAsync(null, null);
        var all = await svc.TaxOfficesAsync();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            all = all.Where(t => t.GovTaxID.Contains(k) || t.GovTaxName.Contains(k)).ToList();
        }
        return View(all);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string? codeParent, string? provinceCode, string? districtCode, string name, string? level, string? address, string? contactEmail, string? contactPhone, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveTaxOfficeAsync(id, code, codeParent, provinceCode, districtCode, name, level, address, contactEmail, contactPhone, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteTaxOfficeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Nhật ký gửi email hóa đơn cho người mua (theo Invoice_Invoice_Support_SendMail của TVAN gốc).
public class EmailLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.EmailLogsAsync(invoiceId));
    }
}

// Nhật ký in chuyển đổi hóa đơn (theo Invoice_Invoice_Support_BackFlagChange của TVAN gốc).
public class ConversionPrintLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.ConversionPrintLogsAsync(invoiceId));
    }
}

// Nhật ký ký lại hóa đơn (theo Invoice_Invoice_ReSign của TVAN gốc).
public class ReSignLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.ReSignLogsAsync(invoiceId));
    }
}

// Nhật ký duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc).
public class ApproveLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.ApproveLogsAsync(invoiceId));
    }
}

// Nhật ký duyệt NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_ApprovedMulti của TVAN gốc).
public class BulkApproveLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index() => View(await svc.BulkApproveLogsAsync());
}

// Nhật ký phát hành hóa đơn (theo Invoice_Invoice_Issued của TVAN gốc).
public class IssueLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.IssueLogsAsync(invoiceId));
    }
}

// Nhật ký nhận kết quả phản hồi từ CQT (theo Invoice_Invoice_TCTReceive của TVAN gốc).
public class TctReceiveLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.TctReceiveLogsAsync(invoiceId));
    }
}

// Nhật ký gửi thông báo hóa đơn sai sót tới CQT (theo Invoice_Invoice_SentTCT_300 của TVAN gốc).
public class Tct300LogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.Tct300LogsAsync(invoiceId));
    }
}

// Nhật ký hủy hóa đơn (theo Invoice_Invoice_Cancel của TVAN gốc).
public class CancelLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.CancelLogsAsync(invoiceId));
    }
}

// Nhật ký tạo biên bản đính kèm hóa đơn (theo luồng TaoBienBan của TVAN gốc).
public class RecordLogController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? invoiceId)
    {
        ViewBag.InvoiceId = invoiceId;
        return View(await svc.RecordLogsAsync(invoiceId));
    }
}

// Danh mục mẫu hóa đơn + nhật ký cấp phát số (theo Invoice_TempInvoice / Invoice_Invoice_AllocatedInv của TVAN gốc).
public class InvoiceTemplateController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(int? nntId)
    {
        ViewBag.Nnts = await svc.NntsAsync();
        ViewBag.NntId = nntId;
        ViewBag.Logs = await svc.AllocLogsAsync(null);
        ViewBag.RangeLogs = await svc.TemplateRangeLogsAsync(null);
        ViewBag.TctLogs = await svc.TemplateTctLogsAsync(null);
        return View(await svc.TemplatesAsync(nntId));
    }

    // Phát hành mẫu hóa đơn (theo Invoice_TempInvoice_Issued của TVAN gốc): PENDING → ISSUED.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int id, DateTime effDateStart, string? remark)
    {
        var (ok, msg) = await svc.IssueTemplateAsync(id, effDateStart, remark);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Ngừng hoạt động mẫu hóa đơn (theo Invoice_TempInvoice_InActive của TVAN gốc): ISSUED → INACTIVE.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivate(int id, string? remark)
    {
        var (ok, msg) = await svc.InactivateTemplateAsync(id, remark);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Hủy mẫu hóa đơn (theo Invoice_TempInvoice_Cancel của TVAN gốc): ISSUED → CANCEL.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? remark, string? by)
    {
        var (ok, msg) = await svc.CancelTemplateAsync(id, remark, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Tăng số hóa đơn cuối (EndInvoiceNo) của mẫu hóa đơn — mở rộng dải số được cấp phát
    // (theo Invoice_TempInvoice_IncreaseEndInvoiceNo của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> IncreaseEndNo(int id, int newEndInvoiceNo, string? remark, string? by)
    {
        var (ok, msg) = await svc.IncreaseTemplateEndNoAsync(id, newEndInvoiceNo, remark, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Cập nhật lại CẢ dải số (số bắt đầu + số kết thúc) của mẫu hóa đơn đang chờ
    // (theo Invoice_TempInvoice_UpdQtyInvoiceNo của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQtyNo(int id, int startInvoiceNo, int endInvoiceNo, string? remark, string? by)
    {
        var (ok, msg) = await svc.UpdateTemplateQtyNoAsync(id, startInvoiceNo, endInvoiceNo, remark, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Gửi mẫu hóa đơn tới CQT (theo Invoice_TempInvoice_SentTCT của TVAN gốc): PENDING → SENTTCT.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTct(int id, string? remark, string? by)
    {
        var (ok, msg) = await svc.SendTemplateToTctAsync(id, remark, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Nhận kết quả phát hành mẫu từ CQT (theo Invoice_TempInvoice_TCTIssued của TVAN gốc):
    // ACCEPT → ISSUED, REJECT → PENDING.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveTct(int id, TctAcceptStatus chapNhan, string? message, string? by)
    {
        var (ok, msg) = await svc.ReceiveTemplateTctResultAsync(id, chapNhan, message, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Cập nhật thông tin liên hệ của NNT in trên mẫu hóa đơn
    // (theo Invoice_TempInvoice_SupportUpdEmailAndAddress của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateContact(int id, string? nntName, string? nntAddress, string? nntPhone, string? nntEmail, string? nntWebsite, bool flagStyleComma, string? by)
    {
        var (ok, msg) = await svc.UpdateTemplateContactAsync(id, nntName, nntAddress, nntPhone, nntEmail, nntWebsite, flagStyleComma, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Cập nhật số tài khoản & tên ngân hàng của NNT in trên mẫu hóa đơn
    // (theo Invoice_TempInvoice_SupportUpdAccNoAndBankName của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBank(int id, string? nntAccNo, string? nntBankName, string? by)
    {
        var (ok, msg) = await svc.UpdateTemplateBankAsync(id, nntAccNo, nntBankName, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Tạo mới/cập nhật mẫu hóa đơn (theo Invoice_TempInvoice_Save của TVAN gốc):
    // lưu lần đầu = tạo mẫu mới ở trạng thái chờ (PENDING), lưu lại cùng mã = cập nhật.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string tInvoiceCode, int nntId, string? tInvoiceName, string formNo, string sign, InvoiceNoRule ttType, string? remark, string? by)
    {
        var (ok, msg, _) = await svc.SaveTemplateAsync(id, tInvoiceCode, nntId, tInvoiceName ?? "", formNo, sign, ttType, remark, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Xóa mẫu hóa đơn đang chờ chưa dùng số (theo Invoice_TempInvoice_Save với FlagIsDelete của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteTemplateAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Cấu hình hệ thống: bật/bỏ kiểm tra ký quá 60 ngày (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc).
public class SettingController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index() => View(await svc.GetSettingAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSign60Day(Sign60DayFlag flag, string? note)
    {
        var (ok, msg) = await svc.SetSign60DayAsync(flag, note);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Cấu hình dấu phân cách động (theo Mst_DynamicComma của TVAN gốc):
// mỗi tổ chức chọn kiểu dấu phân cách khi hiển thị số trên hóa đơn (dấu phẩy ',' hoặc dấu chấm '.').
public class DynamicCommaController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index() => View(await svc.GetDynamicCommaAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DynamicCommaStyle flagStyle, string? by)
    {
        var (ok, msg) = await svc.SetDynamicCommaAsync(flagStyle, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Trường tùy chỉnh hóa đơn (theo Invoice_CustomField / Invoice_DtlCustomField của TVAN gốc):
// mỗi tổ chức tự định nghĩa các trường tùy chỉnh trên hóa đơn và trên danh sách hàng hóa.
public class CustomFieldController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.DtlFields = await svc.InvoiceDtlCustomFieldsAsync();
        return View(await svc.InvoiceCustomFieldsAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string code, string name, DBPhysicalType type, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveInvoiceCustomFieldAsync(code, name, type, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDtl(string code, string name, DBPhysicalType type, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveInvoiceDtlCustomFieldAsync(code, name, type, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string code)
    {
        var (ok, msg) = await svc.DeleteInvoiceCustomFieldAsync(code);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDtl(string code)
    {
        var (ok, msg) = await svc.DeleteInvoiceDtlCustomFieldAsync(code);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Nhóm mẫu hóa đơn (theo Invoice_TempGroup của TVAN gốc): mỗi nhóm gắn với một NNT,
// định nghĩa thân mẫu hóa đơn (HTML) + loại thuế suất + loại hàng hóa/serial + danh sách trường động.
public class TempGroupController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? mst)
    {
        ViewBag.Nnts = await svc.NntsAsync();
        ViewBag.Mst = mst;
        return View(await svc.TempGroupsAsync(mst));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string mst, VATType vatType, string name, string? body, string? thumbnail, SpecPrdType specPrdType, bool active, string? fieldNames, string? fieldTypes, string? by)
    {
        // Trường động nhập theo 2 dòng song song (tên trường / kiểu trường), mỗi dòng một giá trị.
        var names = (fieldNames ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var types = (fieldTypes ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fields = new List<(string, string)>();
        for (int i = 0; i < names.Length; i++) fields.Add((names[i], i < types.Length ? types[i] : "TEXT"));

        var (ok, msg, _) = await svc.SaveTempGroupAsync(id, code, mst, vatType, name, body, thumbnail, specPrdType, active, fields, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteTempGroupAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Mẫu thông điệp/thông báo gửi CQT (theo Mst_MessageTemplate của TVAN gốc):
// mỗi tổ chức khai báo mẫu nội dung thông điệp theo loại thông điệp (100/204/300/301),
// kèm file mẫu (.rtmpl) lưu base64 + đường dẫn file đã lưu.
public class MessageTemplateController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(MessageTypeCode? type)
    {
        ViewBag.Type = type;
        return View(await svc.MessageTemplatesAsync(type));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string code, string name, MessageTypeCode type, string content, string? fileName, string? fileSpec, string? by)
    {
        var (ok, msg, _) = await svc.SaveMessageTemplateAsync(code, name, type, content, fileName, fileSpec, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string code)
    {
        var (ok, msg) = await svc.DeleteMessageTemplateAsync(code);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục khách hàng / người mua (theo Mst_CustomerNNT của TVAN gốc):
// mỗi NNT (bên bán) quản lý danh sách khách hàng của mình để chọn nhanh khi lập hóa đơn.
public class CustomerController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? mst)
    {
        ViewBag.Mst = mst;
        ViewBag.Nnts = await svc.NntsAsync();
        return View(await svc.CustomerNntsAsync(mst));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string mst, string code, string name, string? customerMst, string? type, string? address, string? email, string? phone, string? fax, string? contactName, string? contactPhone, string? contactEmail, DateTime? dob, string? provinceCode, string? districtCode, string? accNo, string? bankName, string? govIdType, string? govId, string? remark, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveCustomerNntAsync(id, mst, code, name, customerMst, type, address, email, phone, fax, contactName, contactPhone, contactEmail, dob, provinceCode, districtCode, accNo, bankName, govIdType, govId, remark, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index), new { mst });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? mst)
    {
        var (ok, msg) = await svc.DeleteCustomerNntAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index), new { mst });
    }
}

// Danh mục loại người nộp thuế (theo Mst_NNTType của TVAN gốc):
// phân loại NNT (Doanh nghiệp, Hộ kinh doanh, Cá nhân...) dùng khi đăng ký NNT.
public class NntTypeController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.NntTypesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string name, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveNntTypeAsync(id, code, name, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteNntTypeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục thuế suất VAT (theo Mst_VATRate của TVAN gốc):
// các mức thuế suất VAT (0%, 5%, 8%, 10%, KCT, KKKNT...) dùng khi lập hóa đơn.
public class VatRateController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.VatRatesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string rate, string? desc, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveVatRateAsync(id, code, rate, desc, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteVatRateAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục đơn vị tính (theo Mst_Unit của TVAN gốc):
// các đơn vị tính (cái, chiếc, hộp, kg, lần...) dùng cho dòng hàng hóa trên hóa đơn.
public class UnitController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.UnitsAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string name, string? remark, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveUnitAsync(id, code, name, remark, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteUnitAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục loại khách hàng / người mua (theo Mst_CustomerNNTType của TVAN gốc):
// phân loại khách hàng (Doanh nghiệp, Cá nhân, Tổ chức nước ngoài...) dùng khi khai báo danh mục khách hàng.
public class CustomerNntTypeController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.CustomerNntTypesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string name, string? remark, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveCustomerNntTypeAsync(id, code, name, remark, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteCustomerNntTypeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục Tỉnh/Thành phố (theo Mst_Province của TVAN gốc):
// danh mục địa giới hành chính cấp tỉnh dùng khi khai báo địa chỉ NNT/khách hàng.
public class ProvinceController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.ProvincesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string name, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveProvinceAsync(id, code, name, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteProvinceAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục Quận/Huyện (theo Mst_District của TVAN gốc):
// danh mục địa giới hành chính cấp quận/huyện thuộc một tỉnh/thành, dùng khi khai báo địa chỉ NNT/khách hàng.
public class DistrictController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? provinceCode, string? keyword)
    {
        ViewBag.ProvinceCode = provinceCode;
        ViewBag.Keyword = keyword;
        ViewBag.Provinces = await svc.ProvincesAsync(null);
        return View(await svc.DistrictsAsync(provinceCode, keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string provinceCode, string code, string name, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveDistrictAsync(id, provinceCode, code, name, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index), new { provinceCode });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? provinceCode)
    {
        var (ok, msg) = await svc.DeleteDistrictAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index), new { provinceCode });
    }
}

// Danh mục Quốc gia (theo Mst_Country của TVAN gốc):
// danh mục quốc tịch/quốc gia dùng khi khai báo thông tin NNT/khách hàng nước ngoài.
public class CountryController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.CountriesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string name, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveCountryAsync(id, code, name, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteCountryAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục Đại lý (theo Mst_Dealer của TVAN gốc):
// đại lý phân phối/giới thiệu khách hàng cho NNT, gắn với một tỉnh/thành.
public class DealerController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword, string? provinceCode)
    {
        ViewBag.Keyword = keyword;
        ViewBag.ProvinceCode = provinceCode;
        ViewBag.Provinces = await svc.ProvincesAsync(null);
        return View(await svc.DealersAsync(keyword, provinceCode));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string name, string provinceCode, string? address, string? presentBy, string? govIdNumber, string? email, string? phone, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveDealerAsync(id, code, name, provinceCode, address, presentBy, govIdNumber, email, phone, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteDealerAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục Phòng ban (theo Mst_Department của TVAN gốc):
// cây phòng ban của một NNT (MST), tự tính mã đơn vị nghiệp vụ/mẫu/cấp từ cây.
public class DepartmentController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? mst, string? keyword)
    {
        ViewBag.Mst = mst;
        ViewBag.Keyword = keyword;
        ViewBag.Nnts = await svc.NntsAsync();
        return View(await svc.DepartmentsAsync(mst, keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string? codeParent, string mst, string name, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveDepartmentAsync(id, code, codeParent, mst, name, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index), new { mst });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? mst)
    {
        var (ok, msg) = await svc.DeleteDepartmentAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index), new { mst });
    }
}

// Chứng thư số của tổ chức (theo Mst_OrgCKS của TVAN gốc):
// mỗi tổ chức khai báo các chứng thư số dùng để ký hóa đơn điện tử.
public class OrgCksController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.OrgCksesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string caNumber, string? caOrg, string? subject, DateTime? effStart, DateTime? effEnd, string? ctsPath, string? ctsPwd, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveOrgCksAsync(id, caNumber, caOrg, subject, effStart, effEnd, ctsPath, ctsPwd, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteOrgCksAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục loại thông báo (theo Mst_NotifyType của TVAN gốc):
// phân loại thông báo (phát hành HĐ, sai sót, CQT...) dùng khi gán loại cho thông báo gửi người dùng.
public class NotifyTypeController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.NotifyTypesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string? desc, bool defaultActive, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveNotifyTypeAsync(id, code, desc, defaultActive, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteNotifyTypeAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Thông báo hệ thống (theo Notify_Notify / Notify_NotifyDtl của TVAN gốc):
// tạo thông báo kèm khoảng hiệu lực, gửi tới người dùng và đánh dấu đã đọc.
public class NotifyController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.NotifiesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string notifyNo, string desc, DateTime effDateStart, DateTime effDateEnd, bool sendEmail, string? by)
    {
        var (ok, msg, _) = await svc.CreateNotifyAsync(notifyNo, desc, effDateStart, effDateEnd, sendEmail, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string? desc, bool sendEmail, string? by)
    {
        var (ok, msg) = await svc.UpdateNotifyAsync(id, desc, sendEmail, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteNotifyAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRecipient(int id, string userCode, bool flagRead, string? by)
    {
        var (ok, msg, _) = await svc.AddNotifyDtlAsync(id, userCode, flagRead, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, string userCode)
    {
        var (ok, msg) = await svc.MarkNotifyReadAsync(id, userCode);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Người nhận thông báo (theo Mst_ManageNotify / Map_UserInNotifyType của TVAN gốc):
// quản lý danh sách người dùng nhận thông báo và đăng ký nhận theo từng loại thông báo.
public class NotifyRecipientController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        ViewBag.NotifyTypes = await svc.NotifyTypesAsync(null);
        return View(await svc.NotifyRecipientsAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string userCode, string? userName, string? by)
    {
        var (ok, msg, _) = await svc.CreateNotifyRecipientAsync(userCode, userName, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string? userName, string? by)
    {
        var (ok, msg) = await svc.UpdateNotifyRecipientAsync(id, userName, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteNotifyRecipientAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTypes(int id, string? notifyType, string? by)
    {
        // Checkbox gửi lên danh sách loại thông báo được bật (name="notifyType").
        var checkedTypes = Request.Form["notifyType"].ToArray();
        var all = await svc.NotifyTypesAsync(null);
        var types = all.Select(t => (t.NotifyTypeCode, checkedTypes.Contains(t.NotifyTypeCode))).ToList();
        var (ok, msg) = await svc.SaveNotifyRecipientTypesAsync(id, types, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Nhóm người dùng (theo Sys_Group / Sys_UserInGroup của TVAN gốc):
// mỗi tổ chức khai báo các nhóm người dùng và phân gán người dùng vào nhóm.
public class SysGroupController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.SysGroupsAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string groupCode, string groupName, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveSysGroupAsync(id, groupCode, groupName, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteSysGroupAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMembers(int id, string? userCodes, string? by)
    {
        // Danh sách mã người dùng cách nhau bởi dấu phẩy / xuống dòng.
        var codes = (userCodes ?? "")
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        var (ok, msg) = await svc.SaveSysGroupMembersAsync(id, codes, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Cấu hình định dạng cột hiển thị theo bảng (theo Mst_ColumnConfig của TVAN gốc):
// mỗi tổ chức khai báo định dạng hiển thị + mô tả cho một cột của một bảng.
public class ColumnConfigController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? tableName, string? keyword)
    {
        ViewBag.TableName = tableName;
        ViewBag.Keyword = keyword;
        return View(await svc.ColumnConfigsAsync(tableName, keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string tableName, string columnName, string? columnFormat, string? columnDesc, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveColumnConfigAsync(id, tableName, columnName, columnFormat, columnDesc, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteColumnConfigAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

public class SortColumnInvoiceController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword)
    {
        ViewBag.Keyword = keyword;
        return View(await svc.SortColumnInvoicesAsync(keyword));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string columnCode, int idx, string columnName, SortColumnType columnType, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveSortColumnInvoiceAsync(id, columnCode, idx, columnName, columnType, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteSortColumnInvoiceAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }
}

// Danh mục tiền tệ / ngoại tệ (theo Mst_CurrencyEx của TVAN gốc) + đọc tiền bằng chữ
// (theo luồng DocTien của TVAN gốc — Invoice_InvoiceController.DocTien).
public class DocTienController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index(string? keyword, decimal? amount, string? currencyCode)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Amount = amount;
        ViewBag.CurrencyCode = currencyCode;
        ViewBag.Currencies = await svc.CurrencyExesAsync(null);
        ViewBag.Logs = await svc.DocTienLogsAsync();
        if (amount.HasValue)
        {
            var (ok, msg, text, _) = await svc.DocTienAsync(amount.Value, currencyCode, "kế toán");
            ViewBag.Ok = ok; ViewBag.Msg = msg; ViewBag.Text = text;
        }
        return View(await svc.CurrencyExesAsync(keyword));
    }

    // Lưu (tạo mới/cập nhật) tiền tệ theo mã (theo Mst_CurrencyEx của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, string code, string name, string? baseCode, decimal buyRate, decimal sellRate, string? remark, bool active, string? by)
    {
        var (ok, msg, _) = await svc.SaveCurrencyExAsync(id, code, name, baseCode, buyRate, sellRate, remark, active, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Xóa tiền tệ theo id (theo Mst_CurrencyEx của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, msg) = await svc.DeleteCurrencyExAsync(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index));
    }

    // Đọc số tiền thành chữ tiếng Việt (theo luồng DocTien của TVAN gốc).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Doc(decimal amount, string? currencyCode, string? by)
    {
        var (ok, msg, _, _) = await svc.DocTienAsync(amount, currencyCode, by);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Index), new { amount, currencyCode });
    }
}

public class OrgController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        Request.Cookies.TryGetValue(TenantContext.CookieName, out var curKey);
        ViewBag.CurrentKey = curKey ?? TenantContext.DefaultApiKey;
        return View(await db.Orgs.IgnoreQueryFilters().OrderBy(o => o.CreatedAt).ToListAsync());
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "Cần tên tổ chức."; return RedirectToAction(nameof(Index)); }
        var org = new Org { Name = name.Trim(), ApiKey = "tvan_" + Guid.NewGuid().ToString("N") };
        db.Orgs.Add(org); await db.SaveChangesAsync();
        SetCookies(org.ApiKey, org.Name);
        TempData["Success"] = $"Đã tạo & chuyển sang \"{org.Name}\"."; return RedirectToAction("Index", "Home");
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch(string apiKey)
    {
        var org = await db.Orgs.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.ApiKey == apiKey);
        if (org == null) { TempData["Error"] = "Không tìm thấy."; return RedirectToAction(nameof(Index)); }
        SetCookies(org.ApiKey, org.Name); return RedirectToAction("Index", "Home");
    }
    private void SetCookies(string k, string n)
    {
        var o = new CookieOptions { IsEssential = true, Expires = DateTimeOffset.UtcNow.AddDays(30) };
        Response.Cookies.Append(TenantContext.CookieName, k, o); Response.Cookies.Append("org_name", n, o);
    }
}
