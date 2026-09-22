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
    Task<(bool ok, string msg, int id)> AdjustAsync(int invoiceId, InvoiceAdjType adjType, decimal amount, decimal vatRate, string? reason);
    Task<(bool ok, string msg, int id)> ReplaceAsync(int invoiceId, decimal amount, decimal vatRate, string? reason);
    Task<List<TranMessage>> MessagesAsync(int invoiceId);
    Task<Invoice?> LookupByCodeAsync(string tctCode);
    Task<TvanDash> DashboardAsync();
    Task<List<InvoiceLicense>> LicensesAsync();
    Task<InvoiceLicense?> GetLicenseAsync(int nntId);
    Task<(bool ok, string msg, int id)> IncreaseLicenseAsync(int nntId, int qty, string? note);
    Task<List<LicenseHist>> LicenseHistsAsync(int? nntId);
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

    // Điều chỉnh hóa đơn đã phát hành (INVOICEADJ). HĐ gốc phải Accepted; HĐ đã điều chỉnh không được điều chỉnh tiếp.
    // Tăng/giảm: HĐ điều chỉnh mang chênh lệch tiền; TCT cấp mã mới, HĐ gốc giữ nguyên.
    public async Task<(bool ok, string msg, int id)> AdjustAsync(int invoiceId, InvoiceAdjType adjType, decimal amount, decimal vatRate, string? reason)
    {
        var root = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (root == null) return (false, "Không tìm thấy hóa đơn gốc.", 0);
        if (root.Status != InvoiceStatus.Accepted) return (false, "Chỉ điều chỉnh được hóa đơn đã được CQT chấp nhận.", 0);
        if (root.SourceCode == SourceInvoiceCode.Adjust) return (false, "Hóa đơn đã điều chỉnh không được điều chỉnh tiếp.", 0);
        if (adjType == InvoiceAdjType.Normal) return (false, "Chọn loại điều chỉnh: Tăng hoặc Giảm.", 0);
        if (amount <= 0) return (false, "Số tiền điều chỉnh phải > 0.", 0);
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Cần lý do điều chỉnh.", 0);

        var no = (await db.Invoices.CountAsync(i => i.NntId == root.NntId) + 1).ToString("D8");
        var adj = new Invoice
        {
            NntId = root.NntId, Symbol = root.Symbol, No = no,
            BuyerName = root.BuyerName, BuyerMst = root.BuyerMst, BuyerAddress = root.BuyerAddress,
            Amount = amount, VatRate = vatRate <= 0 ? root.VatRate : vatRate, IssuedDate = DateTime.Today,
            Status = InvoiceStatus.Draft,
            SourceCode = SourceInvoiceCode.Adjust, AdjType = adjType,
            RefInvoiceId = root.Id, RefTctCode = root.TctCode, AdjReason = reason.Trim()
        };
        db.Invoices.Add(adj); await db.SaveChangesAsync();

        var (tok, tmsg) = await TransmitAsync(adj.Id);
        var fresh = await db.Invoices.FirstOrDefaultAsync(i => i.Id == adj.Id);
        return (tok, tok ? $"Đã điều chỉnh {(adjType == InvoiceAdjType.Increase ? "tăng" : "giảm")} HĐ {root.Symbol}-{root.No}. {tmsg}" : tmsg, adj.Id);
    }

    // Thay thế hóa đơn đã phát hành (INVOICEREPLACE). HĐ gốc phải Accepted; HĐ gốc bị hủy (DELETED) khi thay thế.
    public async Task<(bool ok, string msg, int id)> ReplaceAsync(int invoiceId, decimal amount, decimal vatRate, string? reason)
    {
        var root = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (root == null) return (false, "Không tìm thấy hóa đơn gốc.", 0);
        if (root.Status != InvoiceStatus.Accepted) return (false, "Chỉ thay thế được hóa đơn đã được CQT chấp nhận.", 0);
        if (root.SourceCode != SourceInvoiceCode.Root) return (false, "Chỉ thay thế được hóa đơn gốc.", 0);
        if (amount <= 0) return (false, "Tiền hàng phải > 0.", 0);
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Cần lý do thay thế.", 0);

        var no = (await db.Invoices.CountAsync(i => i.NntId == root.NntId) + 1).ToString("D8");
        var rep = new Invoice
        {
            NntId = root.NntId, Symbol = root.Symbol, No = no,
            BuyerName = root.BuyerName, BuyerMst = root.BuyerMst, BuyerAddress = root.BuyerAddress,
            Amount = amount, VatRate = vatRate <= 0 ? root.VatRate : vatRate, IssuedDate = DateTime.Today,
            Status = InvoiceStatus.Draft,
            SourceCode = SourceInvoiceCode.Replace, AdjType = InvoiceAdjType.Normal,
            RefInvoiceId = root.Id, RefTctCode = root.TctCode, AdjReason = reason.Trim()
        };
        db.Invoices.Add(rep); await db.SaveChangesAsync();

        var (tok, tmsg) = await TransmitAsync(rep.Id);
        if (!tok) return (false, tmsg, rep.Id);

        // HĐ gốc bị thay thế → hủy (DELETED) và ghi nhật ký
        root.Status = InvoiceStatus.Cancelled;
        db.Messages.Add(new TranMessage { InvoiceId = root.Id, NntId = root.NntId, Type = MsgType.ReplaceInvoice, Dir = MsgDir.Out, Code = "300", Text = $"HĐ gốc bị thay thế bởi {rep.Symbol}-{rep.No}" });
        db.Messages.Add(new TranMessage { InvoiceId = root.Id, NntId = root.NntId, Type = MsgType.ReplaceInvoice, Dir = MsgDir.In, Code = "202", Text = "TCT xác nhận thay thế" });
        await db.SaveChangesAsync();
        return (true, $"Đã thay thế HĐ {root.Symbol}-{root.No} bằng {rep.Symbol}-{rep.No}. {tmsg}", rep.Id);
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

    // Hạn mức hóa đơn (theo bảng Invoice_license của TVAN gốc): mỗi NNT có một hạn mức tổng.
    public Task<List<InvoiceLicense>> LicensesAsync() =>
        db.Licenses.Include(l => l.Nnt).OrderBy(l => l.Nnt!.Name).ToListAsync();

    public Task<InvoiceLicense?> GetLicenseAsync(int nntId) =>
        db.Licenses.Include(l => l.Nnt).FirstOrDefaultAsync(l => l.NntId == nntId);

    // Cấp/điều chỉnh hạn mức (theo Invoice_license_IncreaseQty của TVAN gốc):
    // nếu NNT chưa có hạn mức thì tạo mới (TotalQty=0) rồi cộng thêm Qty; luôn ghi lịch sử vào Invoice_licenseCreHist.
    public async Task<(bool ok, string msg, int id)> IncreaseLicenseAsync(int nntId, int qty, string? note)
    {
        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.Id == nntId);
        if (nnt == null) return (false, "Không tìm thấy NNT.", 0);
        if (qty <= 0) return (false, "Số lượng hạn mức phải > 0.", 0);

        var lic = await db.Licenses.FirstOrDefaultAsync(l => l.NntId == nntId);
        var type = LicenseHistType.Increase;
        if (lic == null)
        {
            lic = new InvoiceLicense { NntId = nntId, TotalQty = 0 };
            db.Licenses.Add(lic);
            type = LicenseHistType.Create;
        }
        lic.TotalQty += qty;
        lic.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        db.LicenseHists.Add(new LicenseHist { NntId = nntId, Type = type, Qty = qty, TotalQtyAfter = lic.TotalQty, Note = note });
        await db.SaveChangesAsync();
        return (true, $"Đã {(type == LicenseHistType.Create ? "cấp" : "tăng")} hạn mức {qty:N0} cho {nnt.Name}. Hạn mức tổng: {lic.TotalQty:N0}.", lic.Id);
    }

    public Task<List<LicenseHist>> LicenseHistsAsync(int? nntId)
    {
        var q = db.LicenseHists.AsQueryable();
        if (nntId.HasValue) q = q.Where(h => h.NntId == nntId.Value);
        return q.OrderByDescending(h => h.Id).ToListAsync();
    }
}
