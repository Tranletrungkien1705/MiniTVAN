using Microsoft.EntityFrameworkCore;
using MiniTVAN.Data;
using MiniTVAN.Models;

namespace MiniTVAN.Services;

public record TvanDash(int Nnts, int Registered, int Invoices, int Sent, int Accepted, int Rejected, decimal AcceptedValue, List<Invoice> Recent);

public interface ITvanService
{
    Task<List<Nnt>> NntsAsync();
    Task<Nnt?> GetNntAsync(int id);
    Task<(bool ok, string msg, int id)> CreateNntAsync(Nnt n);
    Task<(bool ok, string msg)> RegisterNntAsync(int id);
    Task<List<Invoice>> InvoicesAsync(InvoiceStatus? status, int? nntId);
    Task<Invoice?> GetInvoiceAsync(int id);
    Task<(bool ok, string msg, int id)> CreateInvoiceAsync(Invoice inv);
    Task<(bool ok, string msg, string? tctCode, string status, int id)> ExternalIssueAsync(string sellerMst, string? sellerName, string buyerName, string? buyerMst, string? buyerAddress, decimal amount, decimal vatRate, string? docRef);
    Task<(bool ok, string msg)> TransmitAsync(int invoiceId);
    Task<(bool ok, string msg)> CancelAsync(int invoiceId);
    Task<List<TranMessage>> MessagesAsync(int invoiceId);
    Task<Invoice?> LookupByCodeAsync(string tctCode);
    Task<TvanDash> DashboardAsync();
}

public class TvanService(AppDbContext db) : ITvanService
{
    private static readonly Random _rng = new();

    public Task<List<Nnt>> NntsAsync() => db.Nnts.OrderBy(n => n.Name).ToListAsync();
    public Task<Nnt?> GetNntAsync(int id) => db.Nnts.FirstOrDefaultAsync(n => n.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateNntAsync(Nnt n)
    {
        if (string.IsNullOrWhiteSpace(n.Name)) return (false, "Cần tên NNT.", 0);
        if (string.IsNullOrWhiteSpace(n.Mst)) return (false, "Cần mã số thuế.", 0);
        n.Mst = n.Mst.Trim();
        if (await db.Nnts.AnyAsync(x => x.Mst == n.Mst)) return (false, "MST đã tồn tại.", 0);
        db.Nnts.Add(n); await db.SaveChangesAsync();
        return (true, "Đã thêm NNT.", n.Id);
    }

    // Đăng ký NNT với cơ quan thuế (thông điệp 100/102). TCT giả lập: MST 10 hoặc 13 số → duyệt.
    public async Task<(bool ok, string msg)> RegisterNntAsync(int id)
    {
        var n = await db.Nnts.FirstOrDefaultAsync(x => x.Id == id);
        if (n == null) return (false, "Không tìm thấy NNT.");
        if (n.RegStatus == RegStatus.Registered) return (false, "NNT đã đăng ký.");
        n.RegStatus = RegStatus.Pending;
        db.Messages.Add(new TranMessage { NntId = n.Id, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "102", Text = $"Đăng ký sử dụng HĐĐT — MST {n.Mst}" });

        var digits = new string(n.Mst.Where(char.IsDigit).ToArray());
        if (digits.Length is 10 or 13 or 14)
        {
            n.RegStatus = RegStatus.Registered; n.RegisteredAt = DateTime.UtcNow;
            db.Messages.Add(new TranMessage { NntId = n.Id, Type = MsgType.RegisterNnt, Dir = MsgDir.In, Code = "202", Text = "TCT chấp nhận đăng ký" });
            await db.SaveChangesAsync();
            return (true, "Đăng ký thành công — NNT đã được cơ quan thuế chấp nhận.");
        }
        n.RegStatus = RegStatus.Rejected;
        db.Messages.Add(new TranMessage { NntId = n.Id, Type = MsgType.RegisterNnt, Dir = MsgDir.In, Code = "204", Text = "TCT từ chối — MST không hợp lệ (cần 10/13 số)" });
        await db.SaveChangesAsync();
        return (false, "TCT từ chối đăng ký: MST không hợp lệ.");
    }

    public Task<List<Invoice>> InvoicesAsync(InvoiceStatus? status, int? nntId)
    {
        var q = db.Invoices.Include(i => i.Nnt).AsQueryable();
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        if (nntId.HasValue) q = q.Where(i => i.NntId == nntId.Value);
        return q.OrderByDescending(i => i.Id).ToListAsync();
    }

    public Task<Invoice?> GetInvoiceAsync(int id) => db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateInvoiceAsync(Invoice inv)
    {
        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.Id == inv.NntId);
        if (nnt == null) return (false, "Chọn người bán (NNT).", 0);
        if (inv.Amount <= 0) return (false, "Tiền hàng phải > 0.", 0);
        if (inv.VatRate < 0) return (false, "Thuế suất VAT không được âm.", 0);
        if (string.IsNullOrWhiteSpace(inv.No)) inv.No = (await db.Invoices.CountAsync(i => i.NntId == inv.NntId) + 1).ToString("D8");
        if (string.IsNullOrWhiteSpace(inv.Symbol)) inv.Symbol = "1C" + DateTime.Today.ToString("yy") + "TAA";
        inv.Status = InvoiceStatus.Draft;
        db.Invoices.Add(inv); await db.SaveChangesAsync();
        return (true, "Đã tạo hóa đơn nháp.", inv.Id);
    }

    // API cho hệ ngoài (MiniService, DMS...) đẩy hóa đơn: tự tạo/đăng ký NNT bên bán → tạo HĐ → truyền TCT.
    public async Task<(bool ok, string msg, string? tctCode, string status, int id)> ExternalIssueAsync(
        string sellerMst, string? sellerName, string buyerName, string? buyerMst, string? buyerAddress, decimal amount, decimal vatRate, string? docRef)
    {
        sellerMst = (sellerMst ?? "").Trim();
        if (sellerMst.Length == 0) return (false, "Thiếu MST người bán.", null, "", 0);
        if (amount <= 0) return (false, "Tiền hàng phải > 0.", null, "", 0);
        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == sellerMst);
        if (nnt == null)
        {
            nnt = new Nnt { Mst = sellerMst, Name = sellerName ?? sellerMst };
            db.Nnts.Add(nnt); await db.SaveChangesAsync();
        }
        if (nnt.RegStatus != RegStatus.Registered)
        {
            var (rok, rmsg) = await RegisterNntAsync(nnt.Id);
            if (!rok) return (false, "Đăng ký NNT thất bại: " + rmsg, null, "", 0);
        }
        var no = (await db.Invoices.CountAsync(i => i.NntId == nnt.Id) + 1).ToString("D8");
        var inv = new Invoice
        {
            NntId = nnt.Id, Symbol = "1C" + DateTime.Today.ToString("yy") + "TAA", No = no,
            BuyerName = buyerName ?? "", BuyerMst = buyerMst, BuyerAddress = buyerAddress,
            Amount = amount, VatRate = vatRate <= 0 ? 10 : vatRate, IssuedDate = DateTime.Today, Status = InvoiceStatus.Draft
        };
        db.Invoices.Add(inv); await db.SaveChangesAsync();
        var (tok, tmsg) = await TransmitAsync(inv.Id);
        var fresh = await db.Invoices.FirstOrDefaultAsync(i => i.Id == inv.Id);
        return (tok, tmsg, fresh?.TctCode, (fresh?.Status ?? InvoiceStatus.Draft).ToString(), inv.Id);
    }

    // Truyền hóa đơn tới TCT — mô phỏng round-trip: gửi (Out) → TCT kiểm tra → phản hồi (In 202/204).
    public async Task<(bool ok, string msg)> TransmitAsync(int invoiceId)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status is InvoiceStatus.Accepted or InvoiceStatus.Cancelled) return (false, "Hóa đơn đã ở trạng thái cuối, không truyền lại.");
        if (inv.Nnt == null || inv.Nnt.RegStatus != RegStatus.Registered)
            return (false, "Người bán chưa đăng ký với cơ quan thuế — không thể truyền.");

        inv.Status = InvoiceStatus.Sent; inv.SentAt = DateTime.UtcNow; inv.RejectReason = null;
        db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300", Text = $"Gửi HĐ {inv.Symbol}-{inv.No}, tổng {inv.Total:N0}đ" });

        // TCT giả lập kiểm tra hợp lệ
        string? reject = null;
        if (string.IsNullOrWhiteSpace(inv.BuyerName)) reject = "Thiếu tên người mua";
        else if (inv.Total <= 0) reject = "Tổng tiền không hợp lệ";
        else if (!string.IsNullOrWhiteSpace(inv.BuyerMst) && new string(inv.BuyerMst.Where(char.IsDigit).ToArray()).Length is not (10 or 13 or 14))
            reject = "MST người mua sai định dạng";

        if (reject == null)
        {
            inv.Status = InvoiceStatus.Accepted;
            inv.TctCode = await GenCodeAsync();
            db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "202", Text = $"TCT cấp mã: {inv.TctCode}" });
            await db.SaveChangesAsync();
            return (true, $"Cơ quan thuế đã tiếp nhận. Mã tra cứu: {inv.TctCode}");
        }
        inv.Status = InvoiceStatus.Rejected; inv.RejectReason = reject;
        db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "204", Text = $"TCT từ chối: {reject}" });
        await db.SaveChangesAsync();
        return (false, $"TCT từ chối: {reject}");
    }

    public async Task<(bool ok, string msg)> CancelAsync(int invoiceId)
    {
        var inv = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy.");
        if (inv.Status != InvoiceStatus.Accepted) return (false, "Chỉ hủy được hóa đơn đã được chấp nhận.");
        inv.Status = InvoiceStatus.Cancelled;
        db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.CancelInvoice, Dir = MsgDir.Out, Code = "300", Text = "Gửi thông điệp hủy HĐ" });
        db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.CancelInvoice, Dir = MsgDir.In, Code = "202", Text = "TCT xác nhận hủy" });
        await db.SaveChangesAsync();
        return (true, "Đã hủy hóa đơn (thông báo tới cơ quan thuế).");
    }

    public Task<List<TranMessage>> MessagesAsync(int invoiceId) =>
        db.Messages.Where(m => m.InvoiceId == invoiceId).OrderBy(m => m.Id).ToListAsync();

    public Task<Invoice?> LookupByCodeAsync(string tctCode) =>
        db.Invoices.IgnoreQueryFilters().Include(i => i.Nnt)
          .FirstOrDefaultAsync(i => i.TctCode == tctCode && i.Status == InvoiceStatus.Accepted);

    private async Task<string> GenCodeAsync()
    {
        for (int i = 0; i < 12; i++)
        {
            var code = "00" + DateTime.Now.ToString("yyMMdd") + _rng.Next(100000000, 999999999).ToString();
            if (!await db.Invoices.IgnoreQueryFilters().AnyAsync(x => x.TctCode == code)) return code;
        }
        return "00" + Guid.NewGuid().ToString("N")[..16];
    }

    public async Task<TvanDash> DashboardAsync()
    {
        var invs = await db.Invoices.ToListAsync();
        return new TvanDash(
            await db.Nnts.CountAsync(),
            await db.Nnts.CountAsync(n => n.RegStatus == RegStatus.Registered),
            invs.Count,
            invs.Count(i => i.Status == InvoiceStatus.Sent),
            invs.Count(i => i.Status == InvoiceStatus.Accepted),
            invs.Count(i => i.Status == InvoiceStatus.Rejected),
            invs.Where(i => i.Status == InvoiceStatus.Accepted).Sum(i => i.Total),
            await db.Invoices.Include(i => i.Nnt).OrderByDescending(i => i.Id).Take(8).ToListAsync());
    }
}
