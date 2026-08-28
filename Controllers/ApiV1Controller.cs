using Microsoft.AspNetCore.Mvc;
using MiniTVAN.Data;
using MiniTVAN.Models;
using MiniTVAN.Services;

namespace MiniTVAN.Controllers;

/// <summary>
/// API JSON cho SPA React. DTO phẳng. Dashboard cache Redis 30s theo tenant (X-Cache).
/// T-VAN: đăng ký NNT với TCT → phát hành HĐ (Draft→Sent→Accepted/Rejected/Cancelled) → nhận mã CQT + nhật ký thông điệp.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ApiV1Controller(ITvanService svc, ICache cache, ITenantContext tenant) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var key = $"tvan:dash:{tenant.OrgId}";
        var hit = await cache.GetAsync<DashDto>(key);
        if (hit != null) { Response.Headers["X-Cache"] = "HIT"; return Ok(hit); }
        var d = await svc.DashboardAsync();
        var dto = new DashDto(d.Nnts, d.Registered, d.Invoices, d.Sent, d.Accepted, d.Rejected, d.AcceptedValue);
        await cache.SetAsync(key, dto, TimeSpan.FromSeconds(30));
        Response.Headers["X-Cache"] = "MISS";
        return Ok(dto);
    }

    [HttpGet("nnts")]
    public async Task<IActionResult> Nnts()
        => Ok((await svc.NntsAsync()).Select(n => new { n.Id, n.Mst, n.Name, n.Address, n.Email, regStatus = (int)n.RegStatus, regStatusText = Ui.Reg(n.RegStatus).text, regStatusCss = Ui.Reg(n.RegStatus).css, n.RegisteredAt }));

    [HttpPost("nnts")]
    public async Task<IActionResult> CreateNnt([FromBody] NntReq r)
    {
        var (ok, msg, id) = await svc.CreateNntAsync(new Nnt { Mst = r.Mst ?? "", Name = r.Name, Address = r.Address, Email = r.Email });
        return ok ? Ok(new { id }) : BadRequest(new { error = msg });
    }

    [HttpPost("nnts/{id:int}/register")]
    public async Task<IActionResult> Register(int id)
    {
        var (ok, msg) = await svc.RegisterNntAsync(id);
        return ok ? Ok(new { ok, msg }) : BadRequest(new { ok, error = msg });
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices([FromQuery] InvoiceStatus? status, [FromQuery] int? nntId)
        => Ok((await svc.InvoicesAsync(status, nntId)).Select(ToDto));

    [HttpGet("invoices/{id:int}")]
    public async Task<IActionResult> Invoice(int id)
    {
        var i = await svc.GetInvoiceAsync(id);
        if (i == null) return NotFound(new { error = "Không tìm thấy hóa đơn." });
        var msgs = await svc.MessagesAsync(id);
        return Ok(new
        {
            invoice = ToDto(i),
            messages = msgs.Select(m => new { type = m.Type.ToString(), dir = m.Dir == MsgDir.Out ? "Gửi TCT" : "TCT phản hồi", m.Code, m.Text, m.CreatedAt })
        });
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] InvoiceReq r)
    {
        var (ok, msg, id) = await svc.CreateInvoiceAsync(new Invoice
        {
            NntId = r.NntId, Symbol = r.Symbol ?? "", BuyerName = r.BuyerName, BuyerMst = r.BuyerMst, BuyerAddress = r.BuyerAddress,
            Amount = r.Amount, VatRate = r.VatRate <= 0 ? 10 : r.VatRate, IssuedDate = r.IssuedDate == default ? DateTime.Today : r.IssuedDate
        });
        return ok ? Ok(new { id }) : BadRequest(new { error = msg });
    }

    [HttpPost("invoices/{id:int}/transmit")]
    public async Task<IActionResult> Transmit(int id)
    {
        var (ok, msg) = await svc.TransmitAsync(id);
        return ok ? Ok(new { ok, msg }) : BadRequest(new { ok, error = msg });
    }

    [HttpPost("invoices/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var (ok, msg) = await svc.CancelAsync(id);
        return ok ? Ok(new { ok, msg }) : BadRequest(new { ok, error = msg });
    }

    // Tra cứu công khai theo mã CQT.
    [HttpGet("lookup/{code}")]
    public async Task<IActionResult> Lookup(string code)
    {
        var i = await svc.LookupByCodeAsync(code);
        if (i == null) return NotFound(new { error = "Không tìm thấy hóa đơn với mã CQT này." });
        return Ok(new { i.Symbol, i.No, seller = i.Nnt?.Name, sellerMst = i.Nnt?.Mst, i.BuyerName, i.BuyerMst, i.Amount, vat = i.VatAmount, total = i.Total, i.TctCode, status = Ui.Inv(i.Status).text, i.IssuedDate });
    }

    private static object ToDto(Invoice i) => new
    {
        i.Id, i.Symbol, i.No, nnt = i.Nnt?.Name, nntMst = i.Nnt?.Mst, i.BuyerName, i.BuyerMst, i.Amount, i.VatRate, vat = i.VatAmount, total = i.Total,
        status = (int)i.Status, statusText = Ui.Inv(i.Status).text, statusCss = Ui.Inv(i.Status).css, i.TctCode, i.RejectReason, i.IssuedDate, i.SentAt
    };
}

public record DashDto(int Nnts, int Registered, int Invoices, int Sent, int Accepted, int Rejected, decimal AcceptedValue);

public class NntReq { public string? Mst { get; set; } public string Name { get; set; } = ""; public string? Address { get; set; } public string? Email { get; set; } }
public class InvoiceReq
{
    public int NntId { get; set; } public string? Symbol { get; set; } public string BuyerName { get; set; } = ""; public string? BuyerMst { get; set; } public string? BuyerAddress { get; set; }
    public decimal Amount { get; set; } public decimal VatRate { get; set; } public DateTime IssuedDate { get; set; }
}
