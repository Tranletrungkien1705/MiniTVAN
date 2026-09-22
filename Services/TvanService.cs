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
    Task<(bool ok, string msg)> ResetToPendingAsync(int invoiceId, string? reason);
    Task<(bool ok, string msg)> RestoreInvoiceAsync(int invoiceId, string? reason);
    Task<(bool ok, string msg)> DeleteAdjustReplaceAsync(int invoiceId, string? reason);
    Task<(bool ok, string msg)> DeleteInvoiceAsync(int invoiceId, string? remark, string? by);
    Task<List<TranMessage>> MessagesAsync(int invoiceId);
    Task<Invoice?> LookupByCodeAsync(string tctCode);
    Task<TvanDash> DashboardAsync();
    Task<List<InvoiceLicense>> LicensesAsync();
    Task<InvoiceLicense?> GetLicenseAsync(int nntId);
    Task<(bool ok, string msg, int id)> IncreaseLicenseAsync(int nntId, int qty, string? note);
    Task<List<LicenseHist>> LicenseHistsAsync(int? nntId);
    Task<List<GuiTongHop>> GuiTongHopsAsync(int? nntId);
    Task<GuiTongHop?> GetGuiTongHopAsync(int id);
    Task<(bool ok, string msg, int id)> CreateGuiTongHopAsync(int nntId, PeriodType lkdlieu, string kdlieu, int bslthu, string? note);
    Task<(bool ok, string msg)> SendGuiTongHopAsync(int id);
    Task<List<TaxOffice>> TaxOfficesAsync();
    Task<List<NntLookupLog>> NntLookupLogsAsync(string? mst);
    Task<(bool ok, string msg, NntLookupLog? log)> LookupNntByMstAsync(string mst);
    Task<List<InvoiceEmailLog>> EmailLogsAsync(int? invoiceId);
    Task<(bool ok, string msg, int id)> SendInvoiceEmailAsync(int invoiceId, string? toEmail, string? sentBy);
    Task<(bool ok, string msg)> MarkConversionPrintedAsync(int invoiceId, string? note, string? by);
    Task<(bool ok, string msg)> ResetConversionPrintAsync(int invoiceId, string? note, string? by);
    Task<List<ConversionPrintLog>> ConversionPrintLogsAsync(int? invoiceId);
    Task<SystemSetting> GetSettingAsync();
    Task<(bool ok, string msg)> SetSign60DayAsync(Sign60DayFlag flag, string? note);
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

        // Kiểm tra ký quá 60 ngày (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc):
        // nếu cấu hình đang bật Check và HĐ có ngày ký cách hiện tại > 60 ngày thì chặn truyền.
        var setting = await GetSettingAsync();
        if (setting.Sign60Day == Sign60DayFlag.Check && inv.SignedDate.HasValue
            && (DateTime.UtcNow - inv.SignedDate.Value).Days > 60)
        {
            inv.Status = InvoiceStatus.Rejected;
            inv.RejectReason = "Hóa đơn ký quá 60 ngày";
            db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "204", Text = $"TCT từ chối: hóa đơn ký quá 60 ngày (ngày ký {inv.SignedDate:dd/MM/yyyy})" });
            await db.SaveChangesAsync();
            return (false, "Hóa đơn ký quá 60 ngày — bật 'Bỏ check ký >60 ngày' trong Cấu hình để truyền.");
        }

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

    // Chuyển hóa đơn về trạng thái chờ (PENDING) — giữ nguyên số hóa đơn (theo Invoice_Invoice_Support_InvoiceToPending của TVAN gốc).
    // Dùng để sửa sai sót: đưa HĐ đã phát hành/bị từ chối về nháp, xóa mã tra cứu CQT, thời điểm gửi và dấu vết gửi email.
    public async Task<(bool ok, string msg)> ResetToPendingAsync(int invoiceId, string? reason)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status == InvoiceStatus.Draft) return (false, "Hóa đơn đang ở trạng thái chờ, không cần chuyển.");
        if (inv.Status == InvoiceStatus.Sent) return (false, "Hóa đơn đang chờ phản hồi TCT, không thể chuyển về chờ.");

        inv.Status = InvoiceStatus.Draft;
        inv.TctCode = null;
        inv.RejectReason = null;
        inv.SentAt = null;
        inv.EmailSend = null;
        inv.SendEmailDTimeUTC = null;
        inv.SendEmailBy = null;
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Chuyển HĐ {inv.Symbol}-{inv.No} về trạng thái chờ (PENDING){(string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã chuyển HĐ {inv.Symbol}-{inv.No} về trạng thái chờ (giữ nguyên số).");
    }

    // Khôi phục hóa đơn đã hủy (DELETED) về trạng thái đã phát hành (ISSUED) để làm thông báo sai sót
    // (theo Invoice_Invoice_Support_BackInvoiceStatus của TVAN gốc: chỉ nhận HĐ ở trạng thái ISSUED/DELETED,
    // giữ nguyên mã xác thực CQT nếu đầu vào không truyền mã mới).
    public async Task<(bool ok, string msg)> RestoreInvoiceAsync(int invoiceId, string? reason)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Cancelled) return (false, "Chỉ khôi phục được hóa đơn đã hủy (DELETED).");
        if (string.IsNullOrWhiteSpace(inv.TctCode)) return (false, "Hóa đơn không có mã tra cứu CQT để khôi phục.");

        inv.Status = InvoiceStatus.Accepted;
        inv.RejectReason = null;
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Khôi phục HĐ {inv.Symbol}-{inv.No} từ DELETED về ISSUED (mã tra cứu {inv.TctCode}){(string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã khôi phục HĐ {inv.Symbol}-{inv.No} về trạng thái đã phát hành (giữ nguyên mã tra cứu).");
    }

    // Xóa hóa đơn điều chỉnh/thay thế CHƯA phát hành (theo Invoice_Invoice_Support_DeleteInvoiceRefNo của TVAN gốc).
    // Chỉ xóa được HĐ đang ở trạng thái chờ (Draft), có tham chiếu HĐ gốc (RefTctCode) và chưa được cấp mã tra cứu (TctCode).
    // Dùng để hủy bỏ một HĐ điều chỉnh/thay thế lập sai trước khi truyền tới CQT.
    public async Task<(bool ok, string msg)> DeleteAdjustReplaceAsync(int invoiceId, string? reason)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Draft) return (false, "Chỉ xóa được hóa đơn điều chỉnh/thay thế chưa phát hành (đang ở trạng thái chờ).");
        if (inv.SourceCode is not (SourceInvoiceCode.Adjust or SourceInvoiceCode.Replace))
            return (false, "Chỉ xóa được hóa đơn điều chỉnh hoặc thay thế.");
        if (string.IsNullOrWhiteSpace(inv.RefTctCode)) return (false, "Hóa đơn không có tham chiếu tới hóa đơn gốc.");
        if (!string.IsNullOrWhiteSpace(inv.TctCode)) return (false, "Hóa đơn đã được cấp mã tra cứu, không thể xóa.");

        var label = inv.SourceCode == SourceInvoiceCode.Adjust ? "điều chỉnh" : "thay thế";
        var symbol = inv.Symbol; var no = inv.No;
        db.Invoices.Remove(inv);
        db.Messages.Add(new TranMessage
        {
            NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Xóa hóa đơn {label} chưa phát hành {symbol}-{no} (tham chiếu {inv.RefTctCode}){(string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã xóa hóa đơn {label} chưa phát hành {symbol}-{no}.");
    }

    // Xóa hóa đơn đã phát hành (theo Invoice_Invoice_Deleted của TVAN gốc):
    // Chỉ xóa được HĐ đang ở trạng thái đã phát hành (Accepted/ISSUED); đưa về DELETED,
    // ghi lại thời điểm xóa (DeleteDTimeUTC), người xóa (DeleteBy) và lý do (Remark).
    public async Task<(bool ok, string msg)> DeleteInvoiceAsync(int invoiceId, string? remark, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Accepted) return (false, "Chỉ xóa được hóa đơn đã phát hành (ISSUED).");

        inv.Status = InvoiceStatus.Deleted;
        inv.DeleteDTimeUTC = DateTime.UtcNow;
        inv.DeleteBy = by;
        inv.Remark = remark;
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Xóa hóa đơn đã phát hành {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã xóa hóa đơn {inv.Symbol}-{inv.No} (DELETED).");
    }

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

    // Bảng tổng hợp dữ liệu HĐĐT gửi CQT (theo Mst_GuiTongHop của TVAN gốc):
    // NNT lập bảng tổng hợp theo kỳ, gom các HĐ đã được CQT chấp nhận trong kỳ đó.
    public Task<List<GuiTongHop>> GuiTongHopsAsync(int? nntId)
    {
        var q = db.GuiTongHops.Include(g => g.Nnt).Include(g => g.Details).AsQueryable();
        if (nntId.HasValue) q = q.Where(g => g.NntId == nntId.Value);
        return q.OrderByDescending(g => g.Id).ToListAsync();
    }

    public Task<GuiTongHop?> GetGuiTongHopAsync(int id) =>
        db.GuiTongHops.Include(g => g.Nnt).Include(g => g.Details).FirstOrDefaultAsync(g => g.Id == id);

    // Lập bảng tổng hợp: gom HĐ Accepted của NNT trong kỳ (ngày/tháng/quý/năm).
    // Ràng buộc theo TVAN gốc: BSLThu != 0 (bổ sung) thì LDau phải = 0; ngược lại LDau = 1.
    public async Task<(bool ok, string msg, int id)> CreateGuiTongHopAsync(int nntId, PeriodType lkdlieu, string kdlieu, int bslthu, string? note)
    {
        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.Id == nntId);
        if (nnt == null) return (false, "Không tìm thấy NNT.", 0);
        if (string.IsNullOrWhiteSpace(kdlieu)) return (false, "Cần kỳ dữ liệu (VD 2026-06).", 0);
        if (bslthu < 0) return (false, "Bổ sung lần thứ không được âm.", 0);

        var (from, to) = PeriodRange(lkdlieu, kdlieu.Trim());
        var invs = await db.Invoices.Include(i => i.Nnt)
            .Where(i => i.NntId == nntId && i.Status == InvoiceStatus.Accepted
                        && i.IssuedDate >= from && i.IssuedDate < to)
            .OrderBy(i => i.IssuedDate).ThenBy(i => i.Id).ToListAsync();
        if (invs.Count == 0) return (false, "Không có hóa đơn đã được CQT chấp nhận trong kỳ này.", 0);

        var gth = new GuiTongHop
        {
            NntId = nntId, LKDLieu = lkdlieu, KDLieu = kdlieu.Trim(), BSLThu = bslthu,
            LDau = bslthu == 0, TNNT = nnt.Name, MST = nnt.Mst, NLap = DateTime.Today,
            SBTHDLieu = $"{nnt.Mst}-{kdlieu.Trim()}-{bslthu}", Status = GthStatus.Draft
        };
        int stt = 1;
        foreach (var i in invs)
        {
            gth.Details.Add(new GuiTongHopDtl
            {
                STT = stt++, InvoiceCode = i.TctCode ?? "", KHMSHDon = "01GTKT", KHHDon = i.Symbol, SHDon = i.No,
                NLap = i.IssuedDate, TNMua = i.BuyerName, MSTNMua = i.BuyerMst, THHDVu = "Hàng hóa, dịch vụ",
                DVTinh = "Lần", SLuong = 1, TTCThue = i.Amount, TSuat = i.VatRate, TgTThue = i.VatAmount, TgTTToan = i.Total,
                GChu = note
            });
        }
        db.GuiTongHops.Add(gth); await db.SaveChangesAsync();
        return (true, $"Đã lập bảng tổng hợp {gth.SBTHDLieu} gồm {gth.Details.Count} hóa đơn.", gth.Id);
    }

    // Gửi bảng tổng hợp tới CQT — mô phỏng round-trip: gửi (Out) → CQT kiểm tra → phản hồi (In 202/204).
    public async Task<(bool ok, string msg)> SendGuiTongHopAsync(int id)
    {
        var gth = await db.GuiTongHops.Include(g => g.Details).FirstOrDefaultAsync(g => g.Id == id);
        if (gth == null) return (false, "Không tìm thấy bảng tổng hợp.");
        if (gth.Status is GthStatus.Accepted) return (false, "Bảng tổng hợp đã được CQT chấp nhận.");
        if (gth.Details.Count == 0) return (false, "Bảng tổng hợp không có dòng dữ liệu.");

        gth.Status = GthStatus.Sent; gth.SentAt = DateTime.UtcNow; gth.Remark = null;
        gth.MessageSentCode = "K" + DateTime.Now.ToString("yyMMddHHmmss");
        db.Messages.Add(new TranMessage { NntId = gth.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300", Text = $"Gửi bảng tổng hợp {gth.SBTHDLieu} ({gth.Details.Count} HĐ)" });

        // CQT giả lập kiểm tra: mọi dòng phải có mã tra cứu và tổng tiền > 0.
        string? reject = null;
        if (gth.Details.Any(d => string.IsNullOrWhiteSpace(d.InvoiceCode))) reject = "Có dòng thiếu mã tra cứu hóa đơn";
        else if (gth.Details.Any(d => d.TgTTToan <= 0)) reject = "Có dòng tổng tiền thanh toán không hợp lệ";

        if (reject == null)
        {
            gth.Status = GthStatus.Accepted;
            gth.MessageReplyCode = "202";
            db.Messages.Add(new TranMessage { NntId = gth.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "202", Text = $"CQT chấp nhận bảng tổng hợp {gth.SBTHDLieu}" });
            await db.SaveChangesAsync();
            return (true, $"Cơ quan thuế đã tiếp nhận bảng tổng hợp {gth.SBTHDLieu}.");
        }
        gth.Status = GthStatus.Rejected; gth.MessageReplyCode = "204"; gth.Remark = reject;
        db.Messages.Add(new TranMessage { NntId = gth.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "204", Text = $"CQT từ chối bảng tổng hợp: {reject}" });
        await db.SaveChangesAsync();
        return (false, $"CQT từ chối: {reject}");
    }

    // Danh mục cơ quan thuế (theo Mst_GovTaxID của TVAN gốc).
    public Task<List<TaxOffice>> TaxOfficesAsync() =>
        db.TaxOffices.OrderBy(t => t.GovTaxID).ToListAsync();

    public Task<List<NntLookupLog>> NntLookupLogsAsync(string? mst)
    {
        var q = db.NntLookupLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(mst)) q = q.Where(l => l.Mst == mst.Trim());
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Tra cứu thông tin NNT theo MST từ cơ quan thuế (theo TCT_TraTTinMaSoThue của TVAN gốc).
    // TCT giả lập: MST 10/13/14 số → trả về tên + địa chỉ + CQT quản lý; ngược lại báo không tìm thấy.
    // Mọi lần tra cứu đều ghi nhật ký (NntLookupLog) để đối soát.
    public async Task<(bool ok, string msg, NntLookupLog? log)> LookupNntByMstAsync(string mst)
    {
        mst = (mst ?? "").Trim();
        if (mst.Length == 0) return (false, "Cần nhập mã số thuế.", null);

        var log = new NntLookupLog { Mst = mst };
        var digits = new string(mst.Where(char.IsDigit).ToArray());
        if (digits.Length is not (10 or 13 or 14))
        {
            log.Result = LookupResult.NotFound;
            log.Message = "Không tìm thấy người nộp thuế với MST này (cần 10/13/14 số).";
            db.NntLookupLogs.Add(log); await db.SaveChangesAsync();
            return (false, log.Message, log);
        }

        // Ưu tiên NNT đã có trong hệ thống; nếu chưa có thì TCT trả về thông tin tối thiểu.
        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == mst);
        var office = await db.TaxOffices.FirstOrDefaultAsync(t => t.FlagActive);

        log.Result = LookupResult.Success;
        log.FullName = nnt?.Name ?? $"Người nộp thuế MST {mst}";
        log.Address = nnt?.Address;
        log.GovTaxID = office?.GovTaxID;
        log.GovTaxName = office?.GovTaxName;
        log.Message = "Tra cứu thành công từ cơ quan thuế.";
        db.NntLookupLogs.Add(log); await db.SaveChangesAsync();
        return (true, log.Message, log);
    }

    // Gửi/gửi lại email hóa đơn cho người mua (theo Invoice_Invoice_Support_SendMail của TVAN gốc).
    // Chỉ gửi được cho hóa đơn ĐÃ PHÁT HÀNH (Accepted); người nhận lấy từ tham số, nếu trống thì dùng EmailSend đã lưu.
    // Mọi lần gửi đều ghi nhật ký (InvoiceEmailLog) và cập nhật SendEmailDTimeUTC/SendEmailBy trên hóa đơn.
    public async Task<(bool ok, string msg, int id)> SendInvoiceEmailAsync(int invoiceId, string? toEmail, string? sentBy)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.", 0);
        if (inv.Status != InvoiceStatus.Accepted) return (false, "Chỉ gửi email được cho hóa đơn đã được CQT chấp nhận.", 0);

        var to = (toEmail ?? "").Trim();
        if (to.Length == 0) to = (inv.EmailSend ?? "").Trim();
        if (to.Length == 0) return (false, "Cần email người nhận.", 0);

        var recipients = to.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (recipients.Length == 0 || recipients.Any(r => !r.Contains('@'))) return (false, "Email người nhận không hợp lệ.", 0);

        var subject = $"Hóa đơn điện tử {inv.Symbol}-{inv.No} — {inv.Nnt?.Name}";
        var log = new InvoiceEmailLog
        {
            InvoiceId = inv.Id, ToEmail = string.Join(";", recipients), Subject = subject,
            Result = EmailSendResult.Success, SentBy = sentBy,
            Message = $"Đã gửi email hóa đơn tới {string.Join(";", recipients)}."
        };
        db.InvoiceEmailLogs.Add(log);

        inv.EmailSend = string.Join(";", recipients);
        inv.SendEmailDTimeUTC = DateTime.UtcNow;
        inv.SendEmailBy = sentBy;
        await db.SaveChangesAsync();
        return (true, log.Message!, log.Id);
    }

    public Task<List<InvoiceEmailLog>> EmailLogsAsync(int? invoiceId)
    {
        var q = db.InvoiceEmailLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // In chuyển đổi hóa đơn (theo Invoice_Invoice.FlagChange của TVAN gốc):
    // đánh dấu hóa đơn đã được in ở dạng chuyển đổi (FlagChange = Printed).
    // Chỉ thực hiện được với hóa đơn đã phát hành (Accepted).
    public async Task<(bool ok, string msg)> MarkConversionPrintedAsync(int invoiceId, string? note, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Accepted) return (false, "Chỉ in chuyển đổi được hóa đơn đã được CQT chấp nhận.");
        if (inv.FlagChange == ConversionPrintFlag.Printed) return (false, "Hóa đơn đã được in chuyển đổi.");

        inv.FlagChange = ConversionPrintFlag.Printed;
        db.ConversionPrintLogs.Add(new ConversionPrintLog
        {
            InvoiceId = inv.Id, Action = ConversionPrintAction.Print, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"In chuyển đổi HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã đánh dấu in chuyển đổi HĐ {inv.Symbol}-{inv.No}.");
    }

    // Bỏ cờ in chuyển đổi (theo Invoice_Invoice_Support_BackFlagChange của TVAN gốc):
    // đưa FlagChange về NotPrinted ('1' = chưa in chuyển đổi) để in lại hóa đơn thường.
    public async Task<(bool ok, string msg)> ResetConversionPrintAsync(int invoiceId, string? note, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.FlagChange == ConversionPrintFlag.NotPrinted) return (false, "Hóa đơn chưa in chuyển đổi, không cần bỏ cờ.");

        inv.FlagChange = ConversionPrintFlag.NotPrinted;
        db.ConversionPrintLogs.Add(new ConversionPrintLog
        {
            InvoiceId = inv.Id, Action = ConversionPrintAction.Reset, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Bỏ cờ in chuyển đổi HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã bỏ cờ in chuyển đổi HĐ {inv.Symbol}-{inv.No}.");
    }

    public Task<List<ConversionPrintLog>> ConversionPrintLogsAsync(int? invoiceId)
    {
        var q = db.ConversionPrintLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Cấu hình hệ thống (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc):
    // mỗi tổ chức có một bản ghi; mặc định bật kiểm tra ký >60 ngày (Check).
    public async Task<SystemSetting> GetSettingAsync()
    {
        var s = await db.SystemSettings.FirstOrDefaultAsync();
        if (s == null)
        {
            s = new SystemSetting { Sign60Day = Sign60DayFlag.Check };
            db.SystemSettings.Add(s); await db.SaveChangesAsync();
        }
        return s;
    }

    // Bật/bỏ kiểm tra ký quá 60 ngày (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc:
    // FlagChange = '0' (Inactive) → bỏ check; ngược lại → check).
    public async Task<(bool ok, string msg)> SetSign60DayAsync(Sign60DayFlag flag, string? note)
    {
        var s = await GetSettingAsync();
        s.Sign60Day = flag;
        s.Note = note;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, flag == Sign60DayFlag.Uncheck
            ? "Đã bỏ kiểm tra ký quá 60 ngày."
            : "Đã bật kiểm tra ký quá 60 ngày.");
    }

    // Khoảng thời gian [from, to) của kỳ dữ liệu theo loại kỳ (LKDLieu).
    private static (DateTime from, DateTime to) PeriodRange(PeriodType t, string kdlieu)
    {
        switch (t)
        {
            case PeriodType.Day:
                var d = DateTime.TryParse(kdlieu, out var dd) ? dd.Date : DateTime.Today;
                return (d, d.AddDays(1));
            case PeriodType.Quarter:
                var parts = kdlieu.Split('-', '/');
                int qy = parts.Length > 0 && int.TryParse(parts[0], out var y) ? y : DateTime.Today.Year;
                int qn = parts.Length > 1 && int.TryParse(parts[1], out var q) ? Math.Clamp(q, 1, 4) : 1;
                var qs = new DateTime(qy, (qn - 1) * 3 + 1, 1);
                return (qs, qs.AddMonths(3));
            case PeriodType.Year:
                int yy = int.TryParse(kdlieu, out var y2) ? y2 : DateTime.Today.Year;
                var ys = new DateTime(yy, 1, 1);
                return (ys, ys.AddYears(1));
            default: // Month
                var mp = kdlieu.Split('-', '/');
                int my = mp.Length > 0 && int.TryParse(mp[0], out var y3) ? y3 : DateTime.Today.Year;
                int mm = mp.Length > 1 && int.TryParse(mp[1], out var m3) ? Math.Clamp(m3, 1, 12) : 1;
                var ms = new DateTime(my, mm, 1);
                return (ms, ms.AddMonths(1));
        }
    }
}
