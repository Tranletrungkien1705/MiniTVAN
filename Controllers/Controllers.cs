using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniTVAN.Data;
using MiniTVAN.Models;
using MiniTVAN.Services;

namespace MiniTVAN.Controllers;

public class HomeController(ITvanService svc) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.Dash = await svc.DashboardAsync(); return View(); }
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
