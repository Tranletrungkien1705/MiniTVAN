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
    public async Task<IActionResult> Index() => View(await svc.NntsAsync());

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
