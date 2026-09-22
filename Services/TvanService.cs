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
    Task<List<InvoiceGthRow>> BthRowsAsync(PeriodType lkdlieu, string kdlieu);
    Task<List<TaxOffice>> TaxOfficesAsync();
    Task<List<NntLookupLog>> NntLookupLogsAsync(string? mst);
    Task<(bool ok, string msg, NntLookupLog? log)> LookupNntByMstAsync(string mst);
    Task<List<InvoiceEmailLog>> EmailLogsAsync(int? invoiceId);
    Task<(bool ok, string msg, int id)> SendInvoiceEmailAsync(int invoiceId, string? toEmail, string? sentBy);
    Task<(bool ok, string msg)> MarkConversionPrintedAsync(int invoiceId, string? note, string? by);
    Task<(bool ok, string msg)> ResetConversionPrintAsync(int invoiceId, string? note, string? by);
    Task<List<ConversionPrintLog>> ConversionPrintLogsAsync(int? invoiceId);
    Task<(bool ok, string msg)> ReSignAsync(int invoiceId, string? fileSpec, string? note, string? by);
    Task<List<ReSignLog>> ReSignLogsAsync(int? invoiceId);
    Task<(bool ok, string msg)> ApproveAsync(int invoiceId, string? filePath, string? pdfFilePath, string? note, string? by);
    Task<(bool ok, string msg)> UnapproveAsync(int invoiceId, string? note, string? by);
    Task<List<ApproveLog>> ApproveLogsAsync(int? invoiceId);
    Task<(bool ok, string msg, int approvedCount)> BulkApproveAsync(List<int> invoiceIds, string? note, string? by);
    Task<List<BulkApproveLog>> BulkApproveLogsAsync();
    Task<(bool ok, string msg)> IssueAsync(int invoiceId, string? emailSend, string? note, string? by);
    Task<List<IssueLog>> IssueLogsAsync(int? invoiceId);
    Task<SystemSetting> GetSettingAsync();
    Task<(bool ok, string msg)> SetSign60DayAsync(Sign60DayFlag flag, string? note);
    Task<List<InvoiceTemplate>> TemplatesAsync(int? nntId);
    Task<(bool ok, string msg)> IssueTemplateAsync(int templateId, DateTime effDateStart, string? remark);
    Task<(bool ok, string msg)> InactivateTemplateAsync(int templateId, string? remark);
    Task<(bool ok, string msg)> IncreaseTemplateEndNoAsync(int templateId, int newEndInvoiceNo, string? remark, string? by);
    Task<(bool ok, string msg)> UpdateTemplateQtyNoAsync(int templateId, int startInvoiceNo, int endInvoiceNo, string? remark, string? by);
    Task<List<TemplateRangeLog>> TemplateRangeLogsAsync(int? templateId);
    Task<(bool ok, string msg)> UpdateTemplateContactAsync(int templateId, string? nntName, string? nntAddress, string? nntPhone, string? nntEmail, string? nntWebsite, bool flagStyleComma, string? by);
    Task<(bool ok, string msg)> UpdateTemplateBankAsync(int templateId, string? nntAccNo, string? nntBankName, string? by);
    Task<(bool ok, string msg, string? invoiceNo)> AllocateInvoiceNoAsync(int invoiceId, DateTime invoiceDate, string? by);
    Task<(bool ok, string msg, string? invoiceNo)> AllocateApproveIssueAsync(int invoiceId, DateTime invoiceDate, string? filePath, string? pdfFilePath, string? emailSend, string? note, string? by);
    Task<List<InvoiceNoAllocLog>> AllocLogsAsync(int? invoiceId);
    Task<(bool ok, string msg, string? mccqtmtt)> AllocateInvoiceNoTypeMAsync(int invoiceId, DateTime invoiceDate, string? by);
    Task<(bool ok, string msg, string? mccqtmtt)> GenMccqtMttAsync(int invoiceId);
    Task<(bool ok, string msg)> ReceiveTctResultAsync(int invoiceId, TctMessageType mltDiep, string? maCQT, string? maLoi, string? lyDo);
    Task<List<TctReceiveLog>> TctReceiveLogsAsync(int? invoiceId);
    Task<(bool ok, string msg)> UpdateAfterAllocatedAsync(int invoiceId, string? buyerName, string? buyerMst, string? buyerAddress, PaymentMethod paymentMethod, decimal amount, decimal vatRate, DateTime invoiceDate, string? note, string? by);
    Task<List<InvoiceUpdateLog>> UpdateLogsAsync(int? invoiceId);
    Task<(bool ok, string msg)> CancelInvoiceAsync(int invoiceId, string? remark, string? by);
    Task<List<CancelInvoiceLog>> CancelLogsAsync(int? invoiceId);
    Task<(bool ok, string msg, int id)> CreateRecordAsync(int invoiceId, RecordType type, string fileName, string? fileSpec, string? reason, string? by);
    Task<List<InvoiceRecordLog>> RecordLogsAsync(int? invoiceId);
    Task<List<Invoice>> BulkFixCandidatesAsync(string tinvoiceCode);
    Task<(bool ok, string msg, int fixedCount)> BulkFixByTemplateAsync(string tinvoiceCode, string? reason, string? by);
    Task<List<BulkFixLog>> BulkFixLogsAsync(int? templateId);
    Task<(bool ok, string msg)> SendTemplateToTctAsync(int templateId, string? remark, string? by);
    Task<(bool ok, string msg)> ReceiveTemplateTctResultAsync(int templateId, TctAcceptStatus chapNhan, string? message, string? by);
    Task<List<TemplateTctLog>> TemplateTctLogsAsync(int? templateId);
    Task<List<InvoiceCustomField>> InvoiceCustomFieldsAsync();
    Task<List<InvoiceDtlCustomField>> InvoiceDtlCustomFieldsAsync();
    Task<(bool ok, string msg, int id)> SaveInvoiceCustomFieldAsync(string code, string name, DBPhysicalType type, bool active, string? by);
    Task<(bool ok, string msg, int id)> SaveInvoiceDtlCustomFieldAsync(string code, string name, DBPhysicalType type, bool active, string? by);
    Task<(bool ok, string msg)> DeleteInvoiceCustomFieldAsync(string code);
    Task<(bool ok, string msg)> DeleteInvoiceDtlCustomFieldAsync(string code);
    Task<List<InvoiceTempGroup>> TempGroupsAsync(string? mst);
    Task<InvoiceTempGroup?> GetTempGroupAsync(int id);
    Task<(bool ok, string msg, int id)> SaveTempGroupAsync(int? id, string code, string mst, VATType vatType, string name, string? body, string? thumbnail, SpecPrdType specPrdType, bool active, List<(string fieldName, string tcfType)> fields, string? by);
    Task<(bool ok, string msg)> DeleteTempGroupAsync(int id);
    Task<List<MessageTemplate>> MessageTemplatesAsync(MessageTypeCode? type);
    Task<(bool ok, string msg, int id)> SaveMessageTemplateAsync(string code, string name, MessageTypeCode type, string content, string? fileName, string? fileSpec, string? by);
    Task<(bool ok, string msg)> DeleteMessageTemplateAsync(string code);
    Task<List<CustomerNnt>> CustomerNntsAsync(string? mst);
    Task<CustomerNnt?> GetCustomerNntAsync(int id);
    Task<(bool ok, string msg, int id)> SaveCustomerNntAsync(int? id, string mst, string code, string name, string? customerMst, string? type, string? address, string? email, string? phone, string? fax, string? contactName, string? contactPhone, string? contactEmail, DateTime? dob, string? provinceCode, string? districtCode, string? accNo, string? bankName, string? govIdType, string? govId, string? remark, bool active, string? by);
    Task<(bool ok, string msg)> DeleteCustomerNntAsync(int id);
    Task<List<NntType>> NntTypesAsync(string? keyword);
    Task<NntType?> GetNntTypeAsync(int id);
    Task<(bool ok, string msg, int id)> SaveNntTypeAsync(int? id, string code, string name, bool active, string? by);
    Task<(bool ok, string msg)> DeleteNntTypeAsync(int id);
    Task<List<Province>> ProvincesAsync(string? keyword);
    Task<Province?> GetProvinceAsync(int id);
    Task<(bool ok, string msg, int id)> SaveProvinceAsync(int? id, string code, string name, bool active, string? by);
    Task<(bool ok, string msg)> DeleteProvinceAsync(int id);
    Task<List<District>> DistrictsAsync(string? provinceCode, string? keyword);
    Task<District?> GetDistrictAsync(int id);
    Task<(bool ok, string msg, int id)> SaveDistrictAsync(int? id, string provinceCode, string code, string name, bool active, string? by);
    Task<(bool ok, string msg)> DeleteDistrictAsync(int id);
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

    // Bảng tổng hợp hóa đơn (BTH) — theo Invoice_Invoice_BTHGet / Invoice_Invoice_BTHGetX của TVAN gốc:
    // liệt kê các hóa đơn đã phát hành (ISSUED) hoặc đã hủy (DELETED) trong một kỳ (ngày/tháng/quý),
    // kèm trạng thái TThai suy ra từ SourceInvoiceCode + InvoiceStatus và thông tin hóa đơn gốc
    // bị điều chỉnh/thay thế. Dùng để đối chiếu trước khi lập bảng tổng hợp gửi CQT.
    public async Task<List<InvoiceGthRow>> BthRowsAsync(PeriodType lkdlieu, string kdlieu)
    {
        kdlieu = (kdlieu ?? "").Trim();
        if (kdlieu.Length == 0) return new();
        var (from, to) = PeriodRange(lkdlieu, kdlieu);

        var invs = await db.Invoices.Include(i => i.Nnt)
            .Where(i => (i.Status == InvoiceStatus.Accepted || i.Status == InvoiceStatus.Deleted)
                        && i.IssuedDate >= from && i.IssuedDate < to)
            .OrderBy(i => i.IssuedDate).ThenBy(i => i.Id).ToListAsync();

        // Nạp hóa đơn gốc (bị điều chỉnh/thay thế) để hiển thị ký hiệu/mẫu số/số hóa đơn gốc.
        var refIds = invs.Where(i => i.RefInvoiceId.HasValue).Select(i => i.RefInvoiceId!.Value).Distinct().ToList();
        var refs = refIds.Count == 0
            ? new Dictionary<int, Invoice>()
            : await db.Invoices.Where(i => refIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

        var rows = new List<InvoiceGthRow>();
        foreach (var i in invs)
        {
            // TThai theo Invoice_Invoice_BTHGetX: DELETED luôn là Huỷ; ISSUED theo nguồn gốc hóa đơn.
            var tthai = i.Status == InvoiceStatus.Deleted
                ? TThai.Huy
                : i.SourceCode switch
                {
                    SourceInvoiceCode.Adjust => TThai.DieuChinh,
                    SourceInvoiceCode.Replace => TThai.ThayThe,
                    _ => TThai.Moi
                };
            refs.TryGetValue(i.RefInvoiceId ?? 0, out var refInv);
            rows.Add(new InvoiceGthRow
            {
                InvoiceCode = i.TctCode ?? "", Sign = i.Symbol, FormNo = i.Symbol, InvoiceNo = i.No,
                InvoiceDate = i.IssuedDate, BuyerName = i.BuyerName, BuyerMst = i.BuyerMst,
                Amount = i.Amount, VatRate = i.VatRate, VatAmount = i.VatAmount, Total = i.Total,
                TThai = tthai,
                RefSign = refInv?.Symbol, RefFormNo = refInv?.Symbol, RefInvoiceNo = refInv?.No
            });
        }
        return rows;
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

    // Ký lại hóa đơn (theo Invoice_Invoice_ReSign của TVAN gốc):
    // chỉ ký lại được hóa đơn đã phát hành (Accepted/ISSUED) và CHƯA ký lại (FlagHotfix is null).
    // Cập nhật nội dung hóa đơn đã ký (InvoiceFileSpec), đường dẫn file XML (InvoiceFilePath),
    // đánh dấu FlagHotfix = Hotfixed, ghi thời điểm & người duyệt (ApprDTimeUTC/ApprBy) và ghi nhật ký.
    public async Task<(bool ok, string msg)> ReSignAsync(int invoiceId, string? fileSpec, string? note, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Accepted) return (false, "Chỉ ký lại được hóa đơn đã phát hành (ISSUED).");
        if (inv.FlagHotfix == HotfixFlag.Hotfixed) return (false, "Hóa đơn đã được ký lại, không ký lại tiếp.");
        if (string.IsNullOrWhiteSpace(fileSpec)) return (false, "Cần nội dung hóa đơn đã ký (base64 XML).");

        var subFolder = DateTime.Now.ToString("yyyy-MM-dd");
        var fileName = $"{DateTime.Now:yyyyMMdd.HHmmss}.{inv.Id}.KyLaiHoaDon.xml";
        inv.InvoiceFileSpec = fileSpec.Trim();
        inv.InvoiceFilePath = $"{subFolder}/{fileName}";
        inv.FlagHotfix = HotfixFlag.Hotfixed;
        inv.ApprDTimeUTC = DateTime.UtcNow;
        inv.ApprBy = by;
        db.ReSignLogs.Add(new ReSignLog
        {
            InvoiceId = inv.Id, FilePath = inv.InvoiceFilePath, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Ký lại HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã ký lại HĐ {inv.Symbol}-{inv.No} (FlagHotfix=1).");
    }

    public Task<List<ReSignLog>> ReSignLogsAsync(int? invoiceId)
    {
        var q = db.ReSignLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc):
    // chỉ duyệt được hóa đơn đang ở trạng thái chờ (Draft/PENDING) và đã có số hóa đơn.
    // Đưa HĐ sang APPROVED, ghi đường dẫn file XML/PDF, thời điểm & người duyệt (ApprDTimeUTC/ApprBy) và ghi nhật ký.
    public async Task<(bool ok, string msg)> ApproveAsync(int invoiceId, string? filePath, string? pdfFilePath, string? note, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Draft) return (false, "Chỉ duyệt được hóa đơn đang ở trạng thái chờ (PENDING).");
        if (string.IsNullOrWhiteSpace(inv.No)) return (false, "Hóa đơn chưa có số, không thể duyệt.");

        inv.Status = InvoiceStatus.Approved;
        inv.InvoiceFilePath = filePath;
        inv.InvoicePDFFilePath = pdfFilePath;
        inv.ApprDTimeUTC = DateTime.UtcNow;
        inv.ApprBy = by;
        db.ApproveLogs.Add(new ApproveLog
        {
            InvoiceId = inv.Id, Action = ApproveAction.Approve,
            FilePath = filePath, PdfFilePath = pdfFilePath, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Duyệt HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã duyệt HĐ {inv.Symbol}-{inv.No} (APPROVED).");
    }

    // Bỏ duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc):
    // đưa HĐ đã duyệt (APPROVED) về lại trạng thái chờ (PENDING), xóa dấu vết duyệt và ghi nhật ký.
    public async Task<(bool ok, string msg)> UnapproveAsync(int invoiceId, string? note, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Approved) return (false, "Chỉ bỏ duyệt được hóa đơn đã duyệt (APPROVED).");

        inv.Status = InvoiceStatus.Draft;
        inv.InvoiceFilePath = null;
        inv.InvoicePDFFilePath = null;
        inv.ApprDTimeUTC = null;
        inv.ApprBy = null;
        db.ApproveLogs.Add(new ApproveLog
        {
            InvoiceId = inv.Id, Action = ApproveAction.Unapprove, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Bỏ duyệt HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã bỏ duyệt HĐ {inv.Symbol}-{inv.No} (về PENDING).");
    }

    public Task<List<ApproveLog>> ApproveLogsAsync(int? invoiceId)
    {
        var q = db.ApproveLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Duyệt NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_ApprovedMulti của TVAN gốc):
    // duyệt hàng loạt danh sách HĐ đang ở trạng thái chờ (Draft/PENDING) và đã có số hóa đơn.
    // Mỗi HĐ đưa sang APPROVED, ghi thời điểm & người duyệt (ApprDTimeUTC/ApprBy), ghi nhật ký duyệt
    // cho từng HĐ và 1 nhật ký duyệt hàng loạt để đối soát. Nếu có HĐ không hợp lệ thì KHÔNG duyệt HĐ nào.
    public async Task<(bool ok, string msg, int approvedCount)> BulkApproveAsync(List<int> invoiceIds, string? note, string? by)
    {
        if (invoiceIds == null || invoiceIds.Count == 0) return (false, "Cần chọn ít nhất một hóa đơn để duyệt.", 0);
        var ids = invoiceIds.Distinct().ToList();
        var invs = await db.Invoices.Include(i => i.Nnt).Where(i => ids.Contains(i.Id)).ToListAsync();
        if (invs.Count != ids.Count) return (false, "Có hóa đơn không tồn tại.", 0);

        // Kiểm tra toàn bộ trước khi ghi (all-or-nothing).
        foreach (var inv in invs)
        {
            if (inv.Status != InvoiceStatus.Draft)
                return (false, $"HĐ {inv.Symbol}-{inv.No} không ở trạng thái chờ (PENDING), không thể duyệt.", 0);
            if (string.IsNullOrWhiteSpace(inv.No))
                return (false, $"HĐ {inv.Symbol} chưa có số, không thể duyệt.", 0);
        }

        var now = DateTime.UtcNow;
        var nos = new List<string>();
        foreach (var inv in invs)
        {
            inv.Status = InvoiceStatus.Approved;
            inv.ApprDTimeUTC = now;
            inv.ApprBy = by;
            db.ApproveLogs.Add(new ApproveLog
            {
                InvoiceId = inv.Id, Action = ApproveAction.Approve, Note = note, By = by, CreatedAt = now,
            });
            db.Messages.Add(new TranMessage
            {
                InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
                Text = $"Duyệt HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}", CreatedAt = now
            });
            nos.Add($"{inv.Symbol}-{inv.No}");
        }
        db.BulkApproveLogs.Add(new BulkApproveLog
        {
            Action = BulkApproveAction.BulkApprove, ApprovedCount = invs.Count,
            InvoiceNos = string.Join(", ", nos), Note = note, By = by, CreatedAt = now
        });
        await db.SaveChangesAsync();
        return (true, $"Đã duyệt {invs.Count} hóa đơn (APPROVED).", invs.Count);
    }

    public Task<List<BulkApproveLog>> BulkApproveLogsAsync()
        => db.BulkApproveLogs.OrderByDescending(l => l.Id).Take(50).ToListAsync();

    // Phát hành hóa đơn (theo Invoice_Invoice_Issued của TVAN gốc):
    // chỉ phát hành được hóa đơn đã duyệt (APPROVED) và đã có số hóa đơn.
    // Đưa HĐ sang ISSUED (Accepted), ghi thời điểm & người phát hành (IssuedDTimeUTC/IssuedBy),
    // cập nhật email người nhận (EmailSend) + thời điểm/người gửi email (SendEmailDTimeUTC/SendEmailBy)
    // và ghi nhật ký (IssueLog) để đối soát.
    public async Task<(bool ok, string msg)> IssueAsync(int invoiceId, string? emailSend, string? note, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Approved) return (false, "Chỉ phát hành được hóa đơn đã duyệt (APPROVED).");
        if (string.IsNullOrWhiteSpace(inv.No)) return (false, "Hóa đơn chưa có số, không thể phát hành.");

        var to = (emailSend ?? "").Trim();
        if (to.Length > 0 && to.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(r => !r.Contains('@')))
            return (false, "Email người nhận không hợp lệ.");

        inv.Status = InvoiceStatus.Accepted;
        inv.IssuedDTimeUTC = DateTime.UtcNow;
        inv.IssuedBy = by;
        if (to.Length > 0)
        {
            inv.EmailSend = to;
            inv.SendEmailDTimeUTC = DateTime.UtcNow;
            inv.SendEmailBy = by;
        }
        db.IssueLogs.Add(new IssueLog
        {
            InvoiceId = inv.Id, Action = IssueAction.Issue,
            EmailSend = to.Length > 0 ? to : null, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Phát hành HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã phát hành HĐ {inv.Symbol}-{inv.No} (ISSUED).");
    }

    public Task<List<IssueLog>> IssueLogsAsync(int? invoiceId)
    {
        var q = db.IssueLogs.Include(l => l.Invoice).AsQueryable();
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

    // Mẫu hóa đơn (theo bảng Invoice_TempInvoice của TVAN gốc): danh sách mẫu theo NNT.
    public Task<List<InvoiceTemplate>> TemplatesAsync(int? nntId)
    {
        var q = db.InvoiceTemplates.Include(t => t.Nnt).AsQueryable();
        if (nntId.HasValue) q = q.Where(t => t.NntId == nntId.Value);
        return q.OrderBy(t => t.Nnt!.Name).ThenBy(t => t.TInvoiceCode).ToListAsync();
    }

    // Phát hành mẫu hóa đơn (theo Invoice_TempInvoice_Issued của TVAN gốc):
    // chỉ phát hành được mẫu đang ở trạng thái chờ (Draft/PENDING) và đang hoạt động (FlagActive).
    // Ràng buộc: dải số phải hợp lệ (StartInvoiceNo/EndInvoiceNo != 0) và ngày bắt đầu sử dụng
    // không được trước ngày hiện tại. Đưa mẫu sang ISSUED, ghi ngày bắt đầu sử dụng + ghi chú.
    public async Task<(bool ok, string msg)> IssueTemplateAsync(int templateId, DateTime effDateStart, string? remark)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (tpl.TInvoiceStatus != TemplateStatus.Draft) return (false, "Chỉ phát hành được mẫu đang ở trạng thái chờ (PENDING).");
        if (!tpl.FlagActive) return (false, "Mẫu đã ngừng hoạt động, không thể phát hành.");
        if (tpl.StartInvoiceNo == 0 || tpl.EndInvoiceNo == 0) return (false, "Mẫu chưa có dải số hợp lệ (số bắt đầu/kết thúc phải khác 0).");

        var date = effDateStart == default ? DateTime.Today : effDateStart.Date;
        if (date < DateTime.Today) return (false, "Ngày bắt đầu sử dụng không được trước ngày hiện tại.");

        tpl.TInvoiceStatus = TemplateStatus.Issued;
        tpl.EffDateStart = date;
        tpl.EffDateEnd = null;
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Phát hành mẫu hóa đơn {tpl.FormNo} ({tpl.TInvoiceCode}) từ {date:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã phát hành mẫu {tpl.FormNo} ({tpl.TInvoiceCode}).");
    }

    // Ngừng hoạt động mẫu hóa đơn (theo Invoice_TempInvoice_InActive của TVAN gốc):
    // chỉ ngừng được mẫu đang sử dụng (Issued) và đang hoạt động (FlagActive).
    // Đưa mẫu sang INACTIVE, ghi ngày kết thúc sử dụng = hiện tại và tắt cờ hoạt động.
    public async Task<(bool ok, string msg)> InactivateTemplateAsync(int templateId, string? remark)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (tpl.TInvoiceStatus != TemplateStatus.Issued) return (false, "Chỉ ngừng được mẫu đang sử dụng (ISSUED).");
        if (!tpl.FlagActive) return (false, "Mẫu đã ngừng hoạt động.");

        tpl.TInvoiceStatus = TemplateStatus.Inactive;
        tpl.EffDateEnd = DateTime.Today;
        tpl.FlagActive = false;
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Ngừng hoạt động mẫu hóa đơn {tpl.FormNo} ({tpl.TInvoiceCode}){(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã ngừng hoạt động mẫu {tpl.FormNo} ({tpl.TInvoiceCode}).");
    }

    // Tăng số hóa đơn cuối (EndInvoiceNo) của mẫu hóa đơn — mở rộng dải số được cấp phát
    // (theo Invoice_TempInvoice_IncreaseEndInvoiceNo / Invoice_TempInvoice_IncreaseQtyInvoiceNo của TVAN gốc).
    // Ràng buộc theo TVAN gốc:
    //  - Mẫu phải đang sử dụng (ISSUED) và đang hoạt động (FlagActive);
    //  - Số cuối mới phải LỚN HƠN số cuối hiện tại (chỉ tăng, không giảm);
    //  - (EndInvoiceNo - StartInvoiceNo) >= QtyUsed (dải số không được nhỏ hơn số đã dùng);
    //  - LastInvoiceNo (số cuối đã cấp) không được vượt quá số cuối mới.
    // Mọi lần tăng ghi nhật ký (TemplateRangeLog) để đối soát.
    public async Task<(bool ok, string msg)> IncreaseTemplateEndNoAsync(int templateId, int newEndInvoiceNo, string? remark, string? by)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (tpl.TInvoiceStatus != TemplateStatus.Issued) return (false, "Chỉ tăng dải số được cho mẫu đang sử dụng (ISSUED).");
        if (!tpl.FlagActive) return (false, "Mẫu đã ngừng hoạt động, không thể tăng dải số.");
        if (newEndInvoiceNo <= tpl.EndInvoiceNo)
            return (false, $"Số hóa đơn cuối mới ({newEndInvoiceNo}) phải lớn hơn số cuối hiện tại ({tpl.EndInvoiceNo}).");
        if (newEndInvoiceNo - tpl.StartInvoiceNo < tpl.QtyUsed)
            return (false, $"Dải số mới ({tpl.StartInvoiceNo}..{newEndInvoiceNo}) nhỏ hơn số hóa đơn đã dùng ({tpl.QtyUsed}).");
        if (int.TryParse(tpl.LastInvoiceNo, out var lastNo) && lastNo > newEndInvoiceNo)
            return (false, $"Số hóa đơn cuối đã cấp ({tpl.LastInvoiceNo}) vượt quá số cuối mới ({newEndInvoiceNo}).");

        var oldEnd = tpl.EndInvoiceNo;
        tpl.EndInvoiceNo = newEndInvoiceNo;
        db.TemplateRangeLogs.Add(new TemplateRangeLog
        {
            TemplateId = tpl.Id, Action = TemplateRangeAction.IncreaseEndNo,
            OldEndInvoiceNo = oldEnd, NewEndInvoiceNo = newEndInvoiceNo, Remark = remark, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Tăng dải số mẫu {tpl.FormNo} ({tpl.TInvoiceCode}): {oldEnd} → {newEndInvoiceNo}{(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã tăng số hóa đơn cuối mẫu {tpl.FormNo} từ {oldEnd} lên {newEndInvoiceNo}.");
    }

    // Cập nhật lại CẢ dải số (số bắt đầu + số kết thúc) của mẫu hóa đơn đang chờ
    // (theo Invoice_TempInvoice_UpdQtyInvoiceNo của TVAN gốc).
    // Ràng buộc theo TVAN gốc:
    //  - Mẫu phải đang ở trạng thái chờ (PENDING) và đang hoạt động (FlagActive);
    //  - Số bắt đầu / số kết thúc phải > 0 và số kết thúc >= số bắt đầu;
    //  - (EndInvoiceNo - StartInvoiceNo) >= QtyUsed (dải số không được nhỏ hơn số đã dùng);
    //  - LastInvoiceNo (số cuối đã cấp) không được vượt quá số kết thúc mới.
    // Mọi lần cập nhật ghi nhật ký (TemplateRangeLog) để đối soát.
    public async Task<(bool ok, string msg)> UpdateTemplateQtyNoAsync(int templateId, int startInvoiceNo, int endInvoiceNo, string? remark, string? by)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (tpl.TInvoiceStatus != TemplateStatus.Draft) return (false, "Chỉ cập nhật dải số được cho mẫu đang ở trạng thái chờ (PENDING).");
        if (!tpl.FlagActive) return (false, "Mẫu đã ngừng hoạt động, không thể cập nhật dải số.");
        if (startInvoiceNo <= 0 || endInvoiceNo <= 0 || endInvoiceNo - startInvoiceNo < 0)
            return (false, "Dải số không hợp lệ: số bắt đầu và số kết thúc phải > 0 và số kết thúc không nhỏ hơn số bắt đầu.");
        if (endInvoiceNo - startInvoiceNo < tpl.QtyUsed)
            return (false, $"Dải số mới ({startInvoiceNo}..{endInvoiceNo}) nhỏ hơn số hóa đơn đã dùng ({tpl.QtyUsed}).");
        if (int.TryParse(tpl.LastInvoiceNo, out var lastNo) && lastNo > endInvoiceNo)
            return (false, $"Số hóa đơn cuối đã cấp ({tpl.LastInvoiceNo}) vượt quá số kết thúc mới ({endInvoiceNo}).");

        var oldStart = tpl.StartInvoiceNo;
        var oldEnd = tpl.EndInvoiceNo;
        tpl.StartInvoiceNo = startInvoiceNo;
        tpl.EndInvoiceNo = endInvoiceNo;
        db.TemplateRangeLogs.Add(new TemplateRangeLog
        {
            TemplateId = tpl.Id, Action = TemplateRangeAction.UpdateQtyNo,
            OldStartInvoiceNo = oldStart, NewStartInvoiceNo = startInvoiceNo,
            OldEndInvoiceNo = oldEnd, NewEndInvoiceNo = endInvoiceNo, Remark = remark, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Cập nhật dải số mẫu {tpl.FormNo} ({tpl.TInvoiceCode}): {oldStart}..{oldEnd} → {startInvoiceNo}..{endInvoiceNo}{(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật dải số mẫu {tpl.FormNo} thành {startInvoiceNo}..{endInvoiceNo}.");
    }

    public Task<List<TemplateRangeLog>> TemplateRangeLogsAsync(int? templateId)
    {
        var q = db.TemplateRangeLogs.Include(l => l.Template).AsQueryable();
        if (templateId.HasValue) q = q.Where(l => l.TemplateId == templateId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Cập nhật thông tin liên hệ của NNT in trên mẫu hóa đơn
    // (theo Invoice_TempInvoice_SupportUpdEmailAndAddress của TVAN gốc):
    // sửa tên đơn vị, địa chỉ, điện thoại, email, website và cờ dấu phân cách động (FlagStyleComma)
    // hiển thị trên hóa đơn phát hành. Ràng buộc theo TVAN gốc: mẫu phải tồn tại; nếu cập nhật
    // tên đơn vị thì tên không được rỗng. Ghi lại thời điểm & người cập nhật để đối soát.
    public async Task<(bool ok, string msg)> UpdateTemplateContactAsync(int templateId, string? nntName, string? nntAddress, string? nntPhone, string? nntEmail, string? nntWebsite, bool flagStyleComma, string? by)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (string.IsNullOrWhiteSpace(nntName)) return (false, "Tên đơn vị không được để trống.");

        tpl.NNTName = nntName.Trim();
        tpl.NNTAddress = string.IsNullOrWhiteSpace(nntAddress) ? null : nntAddress.Trim();
        tpl.NNTPhone = string.IsNullOrWhiteSpace(nntPhone) ? null : nntPhone.Trim();
        tpl.NNTEmail = string.IsNullOrWhiteSpace(nntEmail) ? null : nntEmail.Trim();
        tpl.NNTWebsite = string.IsNullOrWhiteSpace(nntWebsite) ? null : nntWebsite.Trim();
        tpl.FlagStyleComma = flagStyleComma;
        tpl.ContactUpdatedAt = DateTime.UtcNow;
        tpl.ContactUpdatedBy = by;
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Cập nhật thông tin liên hệ mẫu {tpl.FormNo} ({tpl.TInvoiceCode}): {tpl.NNTName}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật thông tin liên hệ mẫu {tpl.FormNo} ({tpl.TInvoiceCode}).");
    }

    // Cập nhật số tài khoản & tên ngân hàng của NNT in trên mẫu hóa đơn
    // (theo Invoice_TempInvoice_SupportUpdAccNoAndBankName của TVAN gốc):
    // sửa số tài khoản (NNTAccNo) và tên ngân hàng (NNTBankName) hiển thị trên hóa đơn phát hành.
    // Ràng buộc theo TVAN gốc: mẫu phải tồn tại; nếu cập nhật số tài khoản thì không được rỗng
    // (InvalidNNTAccNo); nếu cập nhật tên ngân hàng thì không được rỗng (InvalidNNTBankName).
    // Ghi lại thời điểm & người cập nhật để đối soát.
    public async Task<(bool ok, string msg)> UpdateTemplateBankAsync(int templateId, string? nntAccNo, string? nntBankName, string? by)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (string.IsNullOrWhiteSpace(nntAccNo)) return (false, "Số tài khoản không được để trống.");
        if (string.IsNullOrWhiteSpace(nntBankName)) return (false, "Tên ngân hàng không được để trống.");

        tpl.NNTAccNo = nntAccNo.Trim();
        tpl.NNTBankName = nntBankName.Trim();
        tpl.ContactUpdatedAt = DateTime.UtcNow;
        tpl.ContactUpdatedBy = by;
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Cập nhật số tài khoản/ngân hàng mẫu {tpl.FormNo} ({tpl.TInvoiceCode}): {tpl.NNTAccNo} — {tpl.NNTBankName}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật số tài khoản/ngân hàng mẫu {tpl.FormNo} ({tpl.TInvoiceCode}).");
    }

    // Cấp phát số hóa đơn (theo Invoice_Invoice_AllocatedInv của TVAN gốc):
    // HĐ phải đang ở trạng thái chờ (Draft/PENDING) và CHƯA có số; ngày hóa đơn không được trước
    // ngày cấp số gần nhất của mẫu, không trước ngày bắt đầu sử dụng mẫu và không được là ngày tương lai.
    // Số được cấp = LastInvoiceNo + 1 (TT78) hoặc StartInvoiceNo + QtyUsed (TT68); cập nhật mẫu + ghi nhật ký.
    public async Task<(bool ok, string msg, string? invoiceNo)> AllocateInvoiceNoAsync(int invoiceId, DateTime invoiceDate, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.", null);
        if (inv.Status != InvoiceStatus.Draft) return (false, "Chỉ cấp số được cho hóa đơn đang ở trạng thái chờ (PENDING).", null);
        if (!string.IsNullOrWhiteSpace(inv.No)) return (false, "Hóa đơn đã có số, không thể cấp số lại.", null);

        var date = invoiceDate == default ? DateTime.Today : invoiceDate.Date;
        if (date > DateTime.Today) return (false, "Ngày hóa đơn không được là ngày tương lai.", null);

        var tpl = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.NntId == inv.NntId && t.FlagActive);
        if (tpl == null) return (false, "NNT chưa có mẫu hóa đơn đang hoạt động để cấp số.", null);
        if (date < tpl.EffDateStart.Date) return (false, $"Ngày hóa đơn phải sau ngày bắt đầu sử dụng mẫu ({tpl.EffDateStart:dd/MM/yyyy}).", null);
        if (tpl.LastInvoiceDateUTC.HasValue && date < tpl.LastInvoiceDateUTC.Value.Date)
            return (false, $"Ngày hóa đơn không được trước ngày cấp số gần nhất ({tpl.LastInvoiceDateUTC.Value:dd/MM/yyyy}).", null);
        if (tpl.QtyUsed >= tpl.EndInvoiceNo - tpl.StartInvoiceNo + 1)
            return (false, "Mẫu hóa đơn đã dùng hết dải số được cấp.", null);

        // Tính số kế tiếp theo loại thông tư (TT78: nối tiếp LastInvoiceNo; TT68: StartInvoiceNo + QtyUsed).
        int nextNo;
        if (tpl.TTType == InvoiceNoRule.TT78)
        {
            nextNo = (int.TryParse(tpl.LastInvoiceNo, out var last) ? last : tpl.StartInvoiceNo - 1) + 1;
        }
        else
        {
            nextNo = tpl.StartInvoiceNo + tpl.QtyUsed;
        }
        var invoiceNo = nextNo.ToString("D8");

        inv.No = invoiceNo;
        inv.Symbol = string.IsNullOrWhiteSpace(tpl.FormNo) ? inv.Symbol : tpl.FormNo;
        inv.IssuedDate = date;
        inv.InvoiceNoDTimeUTC = DateTime.UtcNow;
        inv.InvoiceNoBy = by;

        tpl.LastInvoiceNo = invoiceNo;
        tpl.LastInvoiceDateUTC = date;
        tpl.QtyUsed += 1;

        db.InvoiceNoAllocLogs.Add(new InvoiceNoAllocLog
        {
            InvoiceId = inv.Id, TemplateId = tpl.Id, FormNo = tpl.FormNo, Sign = tpl.Sign,
            InvoiceNo = invoiceNo, InvoiceDate = date, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Cấp số hóa đơn {tpl.FormNo}-{invoiceNo} cho HĐ ngày {date:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(by) ? "" : " bởi " + by.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã cấp số hóa đơn {tpl.FormNo}-{invoiceNo}.", invoiceNo);
    }

    // Cấp số + Duyệt + Phát hành trong MỘT bước (theo Invoice_Invoice_AllocatedAndApprovedAndIssued của TVAN gốc):
    // gộp 3 thao tác tuần tự trên cùng một hóa đơn đang chờ (PENDING) chưa có số:
    //   1) Cấp số kế tiếp từ mẫu (Invoice_Invoice_AllocatedInvX);
    //   2) Duyệt hóa đơn (Invoice_Invoice_ApprovedX): PENDING → APPROVED, ghi file XML/PDF + người duyệt;
    //   3) Phát hành hóa đơn (Invoice_Invoice_IssuedX): APPROVED → ISSUED, ghi email người nhận + người phát hành.
    // Mọi bước ghi nhật ký riêng (InvoiceNoAllocLog/ApproveLog/IssueLog) để đối soát.
    public async Task<(bool ok, string msg, string? invoiceNo)> AllocateApproveIssueAsync(
        int invoiceId, DateTime invoiceDate, string? filePath, string? pdfFilePath, string? emailSend, string? note, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.", null);
        if (inv.Status != InvoiceStatus.Draft) return (false, "Chỉ cấp số + duyệt + phát hành được hóa đơn đang ở trạng thái chờ (PENDING).", null);
        if (!string.IsNullOrWhiteSpace(inv.No)) return (false, "Hóa đơn đã có số, không thể cấp số lại.", null);

        var date = invoiceDate == default ? DateTime.Today : invoiceDate.Date;
        if (date > DateTime.Today) return (false, "Ngày hóa đơn không được là ngày tương lai.", null);

        var tpl = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.NntId == inv.NntId && t.FlagActive);
        if (tpl == null) return (false, "NNT chưa có mẫu hóa đơn đang hoạt động để cấp số.", null);
        if (date < tpl.EffDateStart.Date) return (false, $"Ngày hóa đơn phải sau ngày bắt đầu sử dụng mẫu ({tpl.EffDateStart:dd/MM/yyyy}).", null);
        if (tpl.LastInvoiceDateUTC.HasValue && date < tpl.LastInvoiceDateUTC.Value.Date)
            return (false, $"Ngày hóa đơn không được trước ngày cấp số gần nhất ({tpl.LastInvoiceDateUTC.Value:dd/MM/yyyy}).", null);
        if (tpl.QtyUsed >= tpl.EndInvoiceNo - tpl.StartInvoiceNo + 1)
            return (false, "Mẫu hóa đơn đã dùng hết dải số được cấp.", null);

        var to = (emailSend ?? "").Trim();
        if (to.Length > 0 && to.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(r => !r.Contains('@')))
            return (false, "Email người nhận không hợp lệ.", null);

        // 1) Cấp số kế tiếp từ mẫu (theo Invoice_Invoice_AllocatedInvX).
        int nextNo;
        if (tpl.TTType == InvoiceNoRule.TT78)
            nextNo = (int.TryParse(tpl.LastInvoiceNo, out var last) ? last : tpl.StartInvoiceNo - 1) + 1;
        else
            nextNo = tpl.StartInvoiceNo + tpl.QtyUsed;
        var invoiceNo = nextNo.ToString("D8");

        inv.No = invoiceNo;
        inv.Symbol = string.IsNullOrWhiteSpace(tpl.FormNo) ? inv.Symbol : tpl.FormNo;
        inv.IssuedDate = date;
        inv.InvoiceNoDTimeUTC = DateTime.UtcNow;
        inv.InvoiceNoBy = by;

        tpl.LastInvoiceNo = invoiceNo;
        tpl.LastInvoiceDateUTC = date;
        tpl.QtyUsed += 1;

        db.InvoiceNoAllocLogs.Add(new InvoiceNoAllocLog
        {
            InvoiceId = inv.Id, TemplateId = tpl.Id, FormNo = tpl.FormNo, Sign = tpl.Sign,
            InvoiceNo = invoiceNo, InvoiceDate = date, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Cấp số hóa đơn {tpl.FormNo}-{invoiceNo} cho HĐ ngày {date:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(by) ? "" : " bởi " + by.Trim())}"
        });

        // 2) Duyệt hóa đơn (theo Invoice_Invoice_ApprovedX): PENDING → APPROVED.
        inv.Status = InvoiceStatus.Approved;
        inv.InvoiceFilePath = filePath;
        inv.InvoicePDFFilePath = pdfFilePath;
        inv.ApprDTimeUTC = DateTime.UtcNow;
        inv.ApprBy = by;
        db.ApproveLogs.Add(new ApproveLog
        {
            InvoiceId = inv.Id, Action = ApproveAction.Approve,
            FilePath = filePath, PdfFilePath = pdfFilePath, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Duyệt HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });

        // 3) Phát hành hóa đơn (theo Invoice_Invoice_IssuedX): APPROVED → ISSUED.
        inv.Status = InvoiceStatus.Accepted;
        inv.IssuedDTimeUTC = DateTime.UtcNow;
        inv.IssuedBy = by;
        if (to.Length > 0)
        {
            inv.EmailSend = to;
            inv.SendEmailDTimeUTC = DateTime.UtcNow;
            inv.SendEmailBy = by;
        }
        db.IssueLogs.Add(new IssueLog
        {
            InvoiceId = inv.Id, Action = IssueAction.Issue,
            EmailSend = to.Length > 0 ? to : null, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Phát hành HĐ {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });

        await db.SaveChangesAsync();
        return (true, $"Đã cấp số {tpl.FormNo}-{invoiceNo}, duyệt và phát hành HĐ {inv.Symbol}-{inv.No} (ISSUED).", invoiceNo);
    }

    public Task<List<InvoiceNoAllocLog>> AllocLogsAsync(int? invoiceId)
    {
        var q = db.InvoiceNoAllocLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Cấp số hóa đơn khởi tạo từ MÁY TÍNH TIỀN (theo Invoice_Invoice_AllocatedInvoiceTypeM của TVAN gốc):
    // chỉ áp dụng cho hóa đơn loại MTT (FormNo có ký tự thứ 4 = 'M'). Ràng buộc theo TVAN gốc:
    //  - HĐ phải đang ở trạng thái chờ (Draft/PENDING) và CHƯA có số;
    //  - Mẫu số phải là loại MTT (ký tự thứ 4 = 'M');
    //  - NNT phải có Mã CQT cấp cho máy tính tiền (MCCQT) dài đúng 5 ký tự;
    //  - ngày hóa đơn không được là ngày tương lai, không trước ngày hiệu lực mẫu và không trước ngày cấp số gần nhất.
    // Khác với cấp số thường: KHÔNG sinh mã tra cứu CQT (TctCode) mà sinh "Mã của CQT trên hóa đơn MTT" (MCCQTMTT).
    public async Task<(bool ok, string msg, string? mccqtmtt)> AllocateInvoiceNoTypeMAsync(int invoiceId, DateTime invoiceDate, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.", null);
        if (inv.Status != InvoiceStatus.Draft) return (false, "Chỉ cấp số được cho hóa đơn đang ở trạng thái chờ (PENDING).", null);
        if (!string.IsNullOrWhiteSpace(inv.No)) return (false, "Hóa đơn đã có số, không thể cấp số lại.", null);

        var tpl = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.NntId == inv.NntId && t.FlagActive);
        if (tpl == null) return (false, "NNT chưa có mẫu hóa đơn đang hoạt động để cấp số.", null);
        if (!IsTypeM(tpl.FormNo)) return (false, "Mẫu hóa đơn không phải loại khởi tạo từ máy tính tiền (ký tự thứ 4 của mẫu số phải là 'M').", null);

        var mccqt = (inv.Nnt?.MCCQT ?? "").Trim();
        if (mccqt.Length != 5) return (false, "NNT chưa được CQT cấp mã máy tính tiền (MCCQT phải gồm 5 ký tự).", null);

        var date = invoiceDate == default ? DateTime.Today : invoiceDate.Date;
        if (date > DateTime.Today) return (false, "Ngày hóa đơn không được là ngày tương lai.", null);
        if (date < tpl.EffDateStart.Date) return (false, $"Ngày hóa đơn phải sau ngày bắt đầu sử dụng mẫu ({tpl.EffDateStart:dd/MM/yyyy}).", null);
        if (tpl.LastInvoiceDateUTC.HasValue && date < tpl.LastInvoiceDateUTC.Value.Date)
            return (false, $"Ngày hóa đơn không được trước ngày cấp số gần nhất ({tpl.LastInvoiceDateUTC.Value:dd/MM/yyyy}).", null);
        if (tpl.QtyUsed >= tpl.EndInvoiceNo - tpl.StartInvoiceNo + 1)
            return (false, "Mẫu hóa đơn đã dùng hết dải số được cấp.", null);

        int nextNo;
        if (tpl.TTType == InvoiceNoRule.TT78)
            nextNo = (int.TryParse(tpl.LastInvoiceNo, out var last) ? last : tpl.StartInvoiceNo - 1) + 1;
        else
            nextNo = tpl.StartInvoiceNo + tpl.QtyUsed;
        var invoiceNo = nextNo.ToString("D8");

        inv.No = invoiceNo;
        inv.Symbol = string.IsNullOrWhiteSpace(tpl.FormNo) ? inv.Symbol : tpl.FormNo;
        inv.IssuedDate = date;
        inv.InvoiceNoDTimeUTC = DateTime.UtcNow;
        inv.InvoiceNoBy = by;

        tpl.LastInvoiceNo = invoiceNo;
        tpl.LastInvoiceDateUTC = date;
        tpl.QtyUsed += 1;

        db.InvoiceNoAllocLogs.Add(new InvoiceNoAllocLog
        {
            InvoiceId = inv.Id, TemplateId = tpl.Id, FormNo = tpl.FormNo, Sign = tpl.Sign,
            InvoiceNo = invoiceNo, InvoiceDate = date, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Cấp số HĐ máy tính tiền {tpl.FormNo}-{invoiceNo} cho HĐ ngày {date:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(by) ? "" : " bởi " + by.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã cấp số hóa đơn máy tính tiền {tpl.FormNo}-{invoiceNo}.", invoiceNo);
    }

    // Sinh "Mã của CQT trên hóa đơn khởi tạo từ máy tính tiền" (MCCQTMTT)
    // (theo Invoice_Invoice_GenMCCQTMTTTypeM của TVAN gốc). Định dạng: M<C2>-<yy>-<MCCQT>-<MMdd><seq7>.
    // Chỉ áp dụng cho hóa đơn loại MTT đang ở trạng thái chờ (Draft/PENDING) và đã có số.
    public async Task<(bool ok, string msg, string? mccqtmtt)> GenMccqtMttAsync(int invoiceId)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.", null);
        if (inv.Status != InvoiceStatus.Draft) return (false, "Chỉ sinh mã CQT máy tính tiền cho hóa đơn đang ở trạng thái chờ (PENDING).", null);
        if (!IsTypeM(inv.Symbol)) return (false, "Hóa đơn không phải loại khởi tạo từ máy tính tiền (ký tự thứ 4 của mẫu số phải là 'M').", null);

        var mccqt = (inv.Nnt?.MCCQT ?? "").Trim();
        if (mccqt.Length != 5) return (false, "NNT chưa được CQT cấp mã máy tính tiền (MCCQT phải gồm 5 ký tự).", null);

        // C2 = ký hiệu hóa đơn (Sign) của mẫu (theo Invoice_Invoice_GenMCCQTMTTTypeMX của TVAN gốc).
        var tpl = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.NntId == inv.NntId && t.FormNo == inv.Symbol);
        var sign = (tpl?.Sign ?? "").Trim();
        if (sign.Length == 0) sign = inv.Symbol.Length >= 2 ? inv.Symbol.Substring(1, 1) : "1";
        var year = DateTime.Now.ToString("yy");
        var seq = await NextMccqtSeqAsync();
        var seqPart = DateTime.Now.ToString("MMdd") + (seq % 10_000_000).ToString("D7");
        var code = $"M{sign}-{year}-{mccqt}-{seqPart}";
        if (code.Length != 23) return (false, "Không sinh được mã CQT máy tính tiền hợp lệ.", null);

        inv.MCCQTMTT = code;
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Sinh mã CQT máy tính tiền cho HĐ {inv.Symbol}-{inv.No}: {code}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã sinh mã CQT máy tính tiền: {code}", code);
    }

    // Ký tự thứ 4 (C2) của Mẫu số = 'M' → hóa đơn khởi tạo từ máy tính tiền (theo Thông tư 32/2025/TT-BTC).
    private static bool IsTypeM(string? formNo) =>
        !string.IsNullOrWhiteSpace(formNo) && formNo.Length >= 4 && char.ToUpperInvariant(formNo[3]) == 'M';

    // Số thứ tự tăng dần cho mã CQT máy tính tiền (theo Seq_Common_Raw("Seq_InvoiceMCCQTMTT") của TVAN gốc).
    private async Task<long> NextMccqtSeqAsync() =>
        await db.Invoices.IgnoreQueryFilters().CountAsync(i => i.MCCQTMTT != null) + 1;

    // Nhận kết quả phản hồi từ CQT cho hóa đơn đã gửi (theo Invoice_Invoice_TCTReceive của TVAN gốc):
    // CQT trả về mã loại thông điệp 202 (phát hành thành công, có mã CQT) hoặc 204 (phát hành thất bại).
    // Chỉ nhận kết quả cho hóa đơn đang chờ phản hồi (Sent). 202 → Accepted + ghi mã xác thực CQT;
    // 204 → Rejected + ghi mã lỗi/lý do. Mọi lần nhận ghi nhật ký (TctReceiveLog) để đối soát.
    public async Task<(bool ok, string msg)> ReceiveTctResultAsync(int invoiceId, TctMessageType mltDiep, string? maCQT, string? maLoi, string? lyDo)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Sent) return (false, "Chỉ nhận kết quả CQT cho hóa đơn đang chờ phản hồi (SENT).");

        var accept = mltDiep == TctMessageType.Success202;
        inv.MltDiep = ((int)mltDiep).ToString();
        inv.TctChapNhan = accept ? TctAcceptStatus.Accept : TctAcceptStatus.Reject;
        inv.TctMaLoi = accept ? null : maLoi;
        inv.TctLyDo = accept ? null : lyDo;
        inv.TctReceiveDTimeUTC = DateTime.UtcNow;

        string msg;
        if (accept)
        {
            if (string.IsNullOrWhiteSpace(maCQT)) return (false, "CQT chấp nhận nhưng thiếu mã xác thực (MaCQT).");
            inv.Status = InvoiceStatus.Accepted;
            inv.TctCode = maCQT.Trim();
            inv.RejectReason = null;
            msg = $"CQT chấp nhận phát hành HĐ {inv.Symbol}-{inv.No}. Mã tra cứu: {inv.TctCode}";
            db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "202", Text = msg });
        }
        else
        {
            inv.Status = InvoiceStatus.Rejected;
            inv.RejectReason = string.IsNullOrWhiteSpace(lyDo) ? (maLoi ?? "CQT từ chối phát hành") : lyDo.Trim();
            msg = $"CQT từ chối phát hành HĐ {inv.Symbol}-{inv.No}" + (string.IsNullOrWhiteSpace(maLoi) ? "" : $" (mã lỗi {maLoi})");
            db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "204", Text = msg });
        }

        db.TctReceiveLogs.Add(new TctReceiveLog
        {
            InvoiceId = inv.Id, MltDiep = mltDiep, ChapNhan = inv.TctChapNhan.Value,
            MaCQT = accept ? inv.TctCode : null, MaLoi = maLoi, LyDo = lyDo, Message = msg,
        });
        await db.SaveChangesAsync();
        return (accept, msg);
    }

    public Task<List<TctReceiveLog>> TctReceiveLogsAsync(int? invoiceId)
    {
        var q = db.TctReceiveLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Cập nhật nội dung hóa đơn SAU KHI đã cấp số (theo Invoice_Invoice_UpdAfterAllocated của TVAN gốc):
    // cho phép sửa người mua, phương thức thanh toán, tiền hàng/thuế suất và ngày hóa đơn khi HĐ đang ở
    // trạng thái chờ (PENDING) và ĐÃ có số. Ràng buộc theo TVAN gốc:
    //  - HĐ phải là hóa đơn gốc (SourceInvoiceCode = INVOICEROOT);
    //  - ngày hóa đơn không được là ngày tương lai;
    //  - ngày hóa đơn phải nằm giữa ngày HĐ liền trước và liền sau trong cùng mẫu (TInvoiceCode).
    // Mọi lần cập nhật ghi nhật ký (InvoiceUpdateLog) để đối soát.
    public async Task<(bool ok, string msg)> UpdateAfterAllocatedAsync(
        int invoiceId, string? buyerName, string? buyerMst, string? buyerAddress,
        PaymentMethod paymentMethod, decimal amount, decimal vatRate, DateTime invoiceDate, string? note, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status != InvoiceStatus.Draft) return (false, "Chỉ cập nhật được hóa đơn đang ở trạng thái chờ (PENDING).");
        if (inv.SourceCode != SourceInvoiceCode.Root) return (false, "Chỉ cập nhật được hóa đơn gốc (không áp dụng cho HĐ điều chỉnh/thay thế).");
        if (string.IsNullOrWhiteSpace(inv.No)) return (false, "Hóa đơn chưa được cấp số, không thể cập nhật sau cấp số.");
        if (string.IsNullOrWhiteSpace(buyerName)) return (false, "Cần tên người mua.");
        if (amount <= 0) return (false, "Tiền hàng phải > 0.");
        if (vatRate < 0) return (false, "Thuế suất VAT không được âm.");

        var date = invoiceDate == default ? inv.IssuedDate.Date : invoiceDate.Date;
        if (date > DateTime.Today) return (false, "Ngày hóa đơn không được là ngày tương lai.");

        // Ràng buộc thứ tự ngày: ngày HĐ phải >= ngày HĐ liền trước và <= ngày HĐ liền sau trong cùng mẫu.
        if (int.TryParse(inv.No, out var curNo))
        {
            var siblings = await db.Invoices
                .Where(i => i.NntId == inv.NntId && i.Symbol == inv.Symbol && i.Id != inv.Id)
                .ToListAsync();
            var before = siblings.Where(i => int.TryParse(i.No, out var n) && n < curNo)
                                 .OrderByDescending(i => int.Parse(i.No)).FirstOrDefault();
            var after = siblings.Where(i => int.TryParse(i.No, out var n) && n > curNo)
                                .OrderBy(i => int.Parse(i.No)).FirstOrDefault();
            if (before != null && date < before.IssuedDate.Date)
                return (false, $"Ngày hóa đơn không được trước ngày HĐ liền trước ({before.Symbol}-{before.No}: {before.IssuedDate:dd/MM/yyyy}).");
            if (after != null && date > after.IssuedDate.Date)
                return (false, $"Ngày hóa đơn không được sau ngày HĐ liền sau ({after.Symbol}-{after.No}: {after.IssuedDate:dd/MM/yyyy}).");
        }

        inv.BuyerName = buyerName.Trim();
        inv.BuyerMst = string.IsNullOrWhiteSpace(buyerMst) ? null : buyerMst.Trim();
        inv.BuyerAddress = string.IsNullOrWhiteSpace(buyerAddress) ? null : buyerAddress.Trim();
        inv.PaymentMethod = paymentMethod;
        inv.Amount = amount;
        inv.VatRate = vatRate;
        inv.IssuedDate = date;

        db.InvoiceUpdateLogs.Add(new InvoiceUpdateLog
        {
            InvoiceId = inv.Id, BuyerName = inv.BuyerName, BuyerMst = inv.BuyerMst, BuyerAddress = inv.BuyerAddress,
            PaymentMethod = paymentMethod, Amount = amount, VatRate = vatRate, InvoiceDate = date, Note = note, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Cập nhật nội dung HĐ {inv.Symbol}-{inv.No} sau khi cấp số{(string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật nội dung HĐ {inv.Symbol}-{inv.No} (sau cấp số).");
    }

    public Task<List<InvoiceUpdateLog>> UpdateLogsAsync(int? invoiceId)
    {
        var q = db.InvoiceUpdateLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Hủy hóa đơn (theo Invoice_Invoice_Cancel của TVAN gốc):
    // chỉ hủy được hóa đơn đang ở trạng thái chờ (PENDING) hoặc đã duyệt (APPROVED).
    // Nếu HĐ đang chờ (PENDING) thì bắt buộc phải đã có số hóa đơn (InvoiceNoIsNotNull).
    // Đưa HĐ sang CANCELED, ghi thời điểm hủy (CancelDTimeUTC), người hủy (CancelBy) và lý do (Remark),
    // đồng thời ghi nhật ký (CancelInvoiceLog) để đối soát.
    public async Task<(bool ok, string msg)> CancelInvoiceAsync(int invoiceId, string? remark, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.");
        if (inv.Status is not (InvoiceStatus.Draft or InvoiceStatus.Approved))
            return (false, "Chỉ hủy được hóa đơn đang ở trạng thái chờ (PENDING) hoặc đã duyệt (APPROVED).");
        if (inv.Status == InvoiceStatus.Draft && string.IsNullOrWhiteSpace(inv.No))
            return (false, "Hóa đơn chưa được cấp số, không thể hủy.");

        inv.Status = InvoiceStatus.Cancelled;
        inv.CancelDTimeUTC = DateTime.UtcNow;
        inv.CancelBy = by;
        inv.Remark = remark;
        db.CancelInvoiceLogs.Add(new CancelInvoiceLog
        {
            InvoiceId = inv.Id, Action = CancelAction.Cancel, Remark = remark, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.CancelInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Hủy hóa đơn {inv.Symbol}-{inv.No}{(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã hủy hóa đơn {inv.Symbol}-{inv.No} (CANCELED).");
    }

    public Task<List<CancelInvoiceLog>> CancelLogsAsync(int? invoiceId)
    {
        var q = db.CancelInvoiceLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Tạo biên bản đính kèm hóa đơn (theo luồng TaoBienBan của TVAN gốc):
    // lưu tên file + nội dung base64 + đường dẫn + lý do hủy/điều chỉnh/thay thế vào hóa đơn,
    // đồng thời ghi nhật ký (InvoiceRecordLog) để đối soát.
    public async Task<(bool ok, string msg, int id)> CreateRecordAsync(int invoiceId, RecordType type, string fileName, string? fileSpec, string? reason, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.", 0);
        if (string.IsNullOrWhiteSpace(fileName)) return (false, "Cần tên file biên bản.", 0);

        var subFolder = DateTime.Now.ToString("yyyy-MM-dd");
        var path = $"{subFolder}/{fileName.Trim()}";
        inv.AttachedDelFileName = fileName.Trim();
        inv.AttachedDelFileSpec = fileSpec;
        inv.AttachedDelFilePath = path;
        inv.DeleteReason = reason;

        var log = new InvoiceRecordLog
        {
            InvoiceId = inv.Id, Type = type, FileName = fileName.Trim(),
            FileSpec = fileSpec, FilePath = path, Reason = reason, By = by,
        };
        db.InvoiceRecordLogs.Add(log);
        db.Messages.Add(new TranMessage
        {
            InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Tạo biên bản {RecordLabel(type)} cho HĐ {inv.Symbol}-{inv.No}: {fileName.Trim()}{(string.IsNullOrWhiteSpace(reason) ? "" : " — " + reason.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã tạo biên bản {RecordLabel(type)} cho HĐ {inv.Symbol}-{inv.No}.", log.Id);
    }

    public Task<List<InvoiceRecordLog>> RecordLogsAsync(int? invoiceId)
    {
        var q = db.InvoiceRecordLogs.Include(l => l.Invoice).AsQueryable();
        if (invoiceId.HasValue) q = q.Where(l => l.InvoiceId == invoiceId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    private static string RecordLabel(RecordType t) => t switch
    {
        RecordType.DieuChinh => "điều chỉnh",
        RecordType.ThayThe => "thay thế",
        _ => "hủy"
    };

    // Sửa lỗi hàng loạt hóa đơn theo mẫu (theo luồng Invoice_Invoice_Fix của TVAN gốc):
    // liệt kê các HĐ đã phát hành (ISSUED/Accepted) và CHƯA ký lại (FlagHotfix is null) của một mẫu hóa đơn
    // (theo TInvoiceCode) — đây là các HĐ cần ký lại hàng loạt khi mẫu/nội dung có sai sót.
    public Task<List<Invoice>> BulkFixCandidatesAsync(string tinvoiceCode)
    {
        var code = (tinvoiceCode ?? "").Trim();
        if (code.Length == 0) return Task.FromResult(new List<Invoice>());
        return db.Invoices.Include(i => i.Nnt)
            .Where(i => i.Status == InvoiceStatus.Accepted && i.FlagHotfix == HotfixFlag.None)
            .Where(i => db.InvoiceTemplates.Any(t => t.TInvoiceCode == code && t.FormNo == i.Symbol))
            .OrderBy(i => i.Id).ToListAsync();
    }

    // Ký lại hàng loạt HĐ đã phát hành chưa ký lại của một mẫu hóa đơn (theo luồng Invoice_Invoice_Fix của TVAN gốc):
    // mỗi HĐ được cập nhật nội dung đã ký (InvoiceFileSpec), đánh dấu FlagHotfix = Hotfixed,
    // ghi thời điểm & người ký lại (ApprDTimeUTC/ApprBy), ghi nhật ký ký lại (ReSignLog) và nhật ký sửa lỗi (BulkFixLog).
    public async Task<(bool ok, string msg, int fixedCount)> BulkFixByTemplateAsync(string tinvoiceCode, string? reason, string? by)
    {
        var code = (tinvoiceCode ?? "").Trim();
        if (code.Length == 0) return (false, "Cần nhập mã mẫu hóa đơn.", 0);

        var tpl = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.TInvoiceCode == code);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn với mã này.", 0);

        var invs = await db.Invoices.Include(i => i.Nnt)
            .Where(i => i.Status == InvoiceStatus.Accepted && i.FlagHotfix == HotfixFlag.None && i.Symbol == tpl.FormNo)
            .OrderBy(i => i.Id).ToListAsync();
        if (invs.Count == 0) return (false, "Không có hóa đơn đã phát hành nào cần sửa lỗi cho mẫu này.", 0);

        var subFolder = DateTime.Now.ToString("yyyy-MM-dd");
        foreach (var inv in invs)
        {
            var fileName = $"{DateTime.Now:yyyyMMdd.HHmmss}.{inv.Id}.SuaLoiHoaDon.xml";
            inv.InvoiceFileSpec = $"PD94bWwgdmVyc2lvbj0iMS4wIj8+PEhEPklOVk9JQ0U9XF{inv.Id}8Pg==";
            inv.InvoiceFilePath = $"{subFolder}/{fileName}";
            inv.FlagHotfix = HotfixFlag.Hotfixed;
            inv.ApprDTimeUTC = DateTime.UtcNow;
            inv.ApprBy = by;
            db.ReSignLogs.Add(new ReSignLog
            {
                InvoiceId = inv.Id, FilePath = inv.InvoiceFilePath, By = by,
                Note = $"Sửa lỗi hàng loạt theo mẫu {tpl.FormNo}{(string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason.Trim())}"
            });
        }

        db.BulkFixLogs.Add(new BulkFixLog
        {
            TemplateId = tpl.Id, Action = BulkFixAction.FixByTemplate, TInvoiceCode = tpl.TInvoiceCode,
            FormNo = tpl.FormNo, FixedCount = invs.Count,
            InvoiceNos = string.Join(", ", invs.Select(i => i.No)), Reason = reason, By = by
        });
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300",
            Text = $"Sửa lỗi hàng loạt {invs.Count} HĐ của mẫu {tpl.FormNo}{(string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã ký lại {invs.Count} hóa đơn của mẫu {tpl.FormNo}.", invs.Count);
    }

    public Task<List<BulkFixLog>> BulkFixLogsAsync(int? templateId)
    {
        var q = db.BulkFixLogs.Include(l => l.Template).AsQueryable();
        if (templateId.HasValue) q = q.Where(l => l.TemplateId == templateId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // Gửi mẫu hóa đơn tới CQT (theo Invoice_TempInvoice_SentTCT của TVAN gốc):
    // mẫu đang ở trạng thái chờ (Draft/PENDING) và đang hoạt động (FlagActive) được gửi tới CQT
    // để đăng ký phát hành. Ràng buộc theo TVAN gốc: dải số phải hợp lệ (StartInvoiceNo/EndInvoiceNo != 0).
    // Đưa mẫu sang SENTTCT, ghi mã V tham chiếu (TCTRefNo), thông báo CQT (TCTMessage),
    // thời điểm & người gửi (SentTCTDTime/SentTCTBy) và ghi nhật ký (TemplateTctLog) để đối soát.
    public async Task<(bool ok, string msg)> SendTemplateToTctAsync(int templateId, string? remark, string? by)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (tpl.TInvoiceStatus != TemplateStatus.Draft) return (false, "Chỉ gửi CQT được mẫu đang ở trạng thái chờ (PENDING).");
        if (!tpl.FlagActive) return (false, "Mẫu đã ngừng hoạt động, không thể gửi CQT.");
        if (tpl.StartInvoiceNo == 0 || tpl.EndInvoiceNo == 0)
            return (false, "Mẫu chưa có dải số hợp lệ (số bắt đầu/kết thúc phải khác 0).");

        // CQT giả lập tiếp nhận: cấp mã V tham chiếu file đã gửi.
        var refNo = "V" + DateTime.Now.ToString("yyMMddHHmmss");
        tpl.TInvoiceStatus = TemplateStatus.SentTct;
        tpl.TCTRefNo = refNo;
        tpl.TCTMessage = "CQT đã tiếp nhận mẫu hóa đơn, chờ phát hành.";
        tpl.SentTCTDTime = DateTime.UtcNow;
        tpl.SentTCTBy = by;
        tpl.TCTChapNhan = null;
        tpl.TCTChapNhanDTime = null;

        db.TemplateTctLogs.Add(new TemplateTctLog
        {
            TemplateId = tpl.Id, Action = TemplateTctAction.SendTct, TCTRefNo = refNo,
            Message = tpl.TCTMessage, Remark = remark, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Gửi mẫu hóa đơn {tpl.FormNo} ({tpl.TInvoiceCode}) tới CQT — mã V {refNo}{(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã gửi mẫu {tpl.FormNo} ({tpl.TInvoiceCode}) tới CQT. Mã V: {refNo}.");
    }

    // Nhận kết quả phát hành mẫu từ CQT (theo Invoice_TempInvoice_TCTIssued của TVAN gốc):
    // chỉ nhận kết quả cho mẫu đã gửi CQT (SENTTCT). CQT chấp nhận (ACCEPT) → mẫu chuyển ISSUED
    // (đang sử dụng); CQT từ chối (REJECT) → mẫu quay về PENDING (chờ). Ghi TCTChapNhan/TCTChapNhanDTime/
    // TCTMessage và nhật ký (TemplateTctLog) để đối soát.
    public async Task<(bool ok, string msg)> ReceiveTemplateTctResultAsync(int templateId, TctAcceptStatus chapNhan, string? message, string? by)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (tpl.TInvoiceStatus != TemplateStatus.SentTct) return (false, "Chỉ nhận kết quả CQT cho mẫu đã gửi CQT (SENTTCT).");

        var accept = chapNhan == TctAcceptStatus.Accept;
        tpl.TCTChapNhan = chapNhan;
        tpl.TCTChapNhanDTime = DateTime.UtcNow;
        tpl.TCTMessage = string.IsNullOrWhiteSpace(message)
            ? (accept ? "CQT chấp nhận phát hành mẫu hóa đơn." : "CQT từ chối phát hành mẫu hóa đơn.")
            : message.Trim();
        tpl.TInvoiceStatus = accept ? TemplateStatus.Issued : TemplateStatus.Draft;
        if (accept) tpl.EffDateStart = DateTime.Today;

        db.TemplateTctLogs.Add(new TemplateTctLog
        {
            TemplateId = tpl.Id, Action = TemplateTctAction.ReceiveTct, TCTRefNo = tpl.TCTRefNo,
            ChapNhan = chapNhan, Message = tpl.TCTMessage, By = by,
        });
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.In, Code = accept ? "202" : "204",
            Text = $"CQT {(accept ? "chấp nhận" : "từ chối")} phát hành mẫu {tpl.FormNo} ({tpl.TInvoiceCode}): {tpl.TCTMessage}"
        });
        await db.SaveChangesAsync();
        return (accept,
            accept ? $"CQT đã chấp nhận phát hành mẫu {tpl.FormNo} ({tpl.TInvoiceCode}) — mẫu chuyển sang đang sử dụng."
                   : $"CQT từ chối phát hành mẫu {tpl.FormNo} ({tpl.TInvoiceCode}) — mẫu quay về trạng thái chờ.");
    }

    public Task<List<TemplateTctLog>> TemplateTctLogsAsync(int? templateId)
    {
        var q = db.TemplateTctLogs.Include(l => l.Template).AsQueryable();
        if (templateId.HasValue) q = q.Where(l => l.TemplateId == templateId.Value);
        return q.OrderByDescending(l => l.Id).Take(50).ToListAsync();
    }

    // ===== Trường tùy chỉnh hóa đơn (theo Invoice_CustomField / Invoice_DtlCustomField của TVAN gốc) =====
    // Mỗi tổ chức tự định nghĩa các trường tùy chỉnh trên hóa đơn (InvCF1..InvCF10) và trên danh sách
    // hàng hóa (InvDCF1..InvDCF5). Khóa nghiệp vụ là (OrgId, Code); lưu lần đầu = tạo, lưu lại = cập nhật.
    public Task<List<InvoiceCustomField>> InvoiceCustomFieldsAsync() =>
        db.InvoiceCustomFields.OrderBy(x => x.InvoiceCustomFieldCode).ToListAsync();

    public Task<List<InvoiceDtlCustomField>> InvoiceDtlCustomFieldsAsync() =>
        db.InvoiceDtlCustomFields.OrderBy(x => x.InvoiceDtlCustomFieldCode).ToListAsync();

    public async Task<(bool ok, string msg, int id)> SaveInvoiceCustomFieldAsync(string code, string name, DBPhysicalType type, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã trường tùy chỉnh.", 0);
        if (name.Length == 0) return (false, "Cần tên trường tùy chỉnh.", 0);

        var e = await db.InvoiceCustomFields.FirstOrDefaultAsync(x => x.InvoiceCustomFieldCode == code);
        if (e == null)
        {
            e = new InvoiceCustomField { InvoiceCustomFieldCode = code, InvoiceCustomFieldName = name, DBPhysicalType = type, FlagActive = active, UpdatedBy = by };
            db.InvoiceCustomFields.Add(e);
            await db.SaveChangesAsync();
            return (true, $"Đã tạo trường tùy chỉnh hóa đơn {code}.", e.Id);
        }
        e.InvoiceCustomFieldName = name;
        e.DBPhysicalType = type;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật trường tùy chỉnh hóa đơn {code}.", e.Id);
    }

    public async Task<(bool ok, string msg, int id)> SaveInvoiceDtlCustomFieldAsync(string code, string name, DBPhysicalType type, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã trường tùy chỉnh.", 0);
        if (name.Length == 0) return (false, "Cần tên trường tùy chỉnh.", 0);

        var e = await db.InvoiceDtlCustomFields.FirstOrDefaultAsync(x => x.InvoiceDtlCustomFieldCode == code);
        if (e == null)
        {
            e = new InvoiceDtlCustomField { InvoiceDtlCustomFieldCode = code, InvoiceDtlCustomFieldName = name, DBPhysicalType = type, FlagActive = active, UpdatedBy = by };
            db.InvoiceDtlCustomFields.Add(e);
            await db.SaveChangesAsync();
            return (true, $"Đã tạo trường tùy chỉnh hàng hóa {code}.", e.Id);
        }
        e.InvoiceDtlCustomFieldName = name;
        e.DBPhysicalType = type;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật trường tùy chỉnh hàng hóa {code}.", e.Id);
    }

    public async Task<(bool ok, string msg)> DeleteInvoiceCustomFieldAsync(string code)
    {
        code = (code ?? "").Trim();
        var e = await db.InvoiceCustomFields.FirstOrDefaultAsync(x => x.InvoiceCustomFieldCode == code);
        if (e == null) return (false, "Không tìm thấy trường tùy chỉnh hóa đơn.");
        db.InvoiceCustomFields.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa trường tùy chỉnh hóa đơn {code}.");
    }

    public async Task<(bool ok, string msg)> DeleteInvoiceDtlCustomFieldAsync(string code)
    {
        code = (code ?? "").Trim();
        var e = await db.InvoiceDtlCustomFields.FirstOrDefaultAsync(x => x.InvoiceDtlCustomFieldCode == code);
        if (e == null) return (false, "Không tìm thấy trường tùy chỉnh hàng hóa.");
        db.InvoiceDtlCustomFields.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa trường tùy chỉnh hàng hóa {code}.");
    }

    // Nhóm mẫu hóa đơn (theo Invoice_TempGroup của TVAN gốc): danh sách nhóm mẫu, lọc theo MST nếu có.
    public Task<List<InvoiceTempGroup>> TempGroupsAsync(string? mst)
    {
        var q = db.InvoiceTempGroups.Include(g => g.Fields).AsQueryable();
        if (!string.IsNullOrWhiteSpace(mst)) q = q.Where(g => g.MST == mst.Trim());
        return q.OrderBy(g => g.InvoiceTGroupCode).ToListAsync();
    }

    public Task<InvoiceTempGroup?> GetTempGroupAsync(int id) =>
        db.InvoiceTempGroups.Include(g => g.Fields).FirstOrDefaultAsync(g => g.Id == id);

    // Lưu (tạo mới/cập nhật) nhóm mẫu hóa đơn theo mã (theo Invoice_TempGroup_Create/Update của TVAN gốc).
    // Ràng buộc: cần mã nhóm, MST phải là NNT đã đăng ký, cần tên nhóm; loại hàng hóa/serial không được
    // đồng thời là Spec và ProductId. Danh sách trường động được thay thế toàn bộ khi lưu.
    public async Task<(bool ok, string msg, int id)> SaveTempGroupAsync(int? id, string code, string mst, VATType vatType, string name, string? body, string? thumbnail, SpecPrdType specPrdType, bool active, List<(string fieldName, string tcfType)> fields, string? by)
    {
        code = (code ?? "").Trim();
        mst = (mst ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã nhóm mẫu hóa đơn.", 0);
        if (name.Length == 0) return (false, "Cần tên nhóm mẫu hóa đơn.", 0);
        if (mst.Length == 0) return (false, "Cần MST người nộp thuế.", 0);
        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == mst);
        if (nnt == null) return (false, "Không tìm thấy NNT với MST này.", 0);

        InvoiceTempGroup? e = null;
        if (id.HasValue && id.Value > 0) e = await db.InvoiceTempGroups.Include(g => g.Fields).FirstOrDefaultAsync(g => g.Id == id.Value);
        else e = await db.InvoiceTempGroups.Include(g => g.Fields).FirstOrDefaultAsync(g => g.InvoiceTGroupCode == code);

        if (e == null)
        {
            if (await db.InvoiceTempGroups.AnyAsync(g => g.InvoiceTGroupCode == code))
                return (false, "Mã nhóm mẫu đã tồn tại.", 0);
            e = new InvoiceTempGroup { InvoiceTGroupCode = code };
            db.InvoiceTempGroups.Add(e);
        }
        e.MST = mst;
        e.VATType = vatType;
        e.InvoiceTGroupName = name;
        e.InvoiceTGroupBody = body;
        e.FilePathThumbnail = thumbnail;
        e.SpecPrdType = specPrdType;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;

        // Thay thế toàn bộ danh sách trường động của nhóm mẫu.
        db.InvoiceTempGroupFields.RemoveRange(e.Fields);
        e.Fields.Clear();
        foreach (var (fieldName, tcfType) in fields ?? new())
        {
            var fn = (fieldName ?? "").Trim();
            if (fn.Length == 0) continue;
            e.Fields.Add(new InvoiceTempGroupField { DBFieldName = fn, TCFType = (tcfType ?? "").Trim(), FlagActive = true });
        }
        await db.SaveChangesAsync();
        return (true, $"Đã lưu nhóm mẫu hóa đơn {code} ({e.Fields.Count} trường động).", e.Id);
    }

    // Xóa nhóm mẫu hóa đơn theo id (theo Invoice_TempGroup_Delete của TVAN gốc).
    public async Task<(bool ok, string msg)> DeleteTempGroupAsync(int id)
    {
        var e = await db.InvoiceTempGroups.Include(g => g.Fields).FirstOrDefaultAsync(g => g.Id == id);
        if (e == null) return (false, "Không tìm thấy nhóm mẫu hóa đơn.");
        var code = e.InvoiceTGroupCode;
        db.InvoiceTempGroups.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa nhóm mẫu hóa đơn {code}.");
    }

    // Mẫu thông điệp/thông báo gửi CQT (theo Mst_MessageTemplate của TVAN gốc):
    // danh sách mẫu thông điệp của tổ chức, lọc theo loại thông điệp nếu có.
    public Task<List<MessageTemplate>> MessageTemplatesAsync(MessageTypeCode? type)
    {
        var q = db.MessageTemplates.AsQueryable();
        if (type.HasValue) q = q.Where(m => m.MessageTypeCode == type.Value);
        return q.OrderBy(m => m.MessageTypeCode).ThenBy(m => m.MessageTplCode).ToListAsync();
    }

    // Lưu (tạo mới/cập nhật) mẫu thông điệp theo mã (theo Mst_MessageTemplate_Create/Update của TVAN gốc):
    // lưu lần đầu = tạo (chặn trùng mã), lưu lại = cập nhật; chặn thiếu mã/tên/nội dung.
    public async Task<(bool ok, string msg, int id)> SaveMessageTemplateAsync(string code, string name, MessageTypeCode type, string content, string? fileName, string? fileSpec, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        content = (content ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã mẫu thông điệp.", 0);
        if (name.Length == 0) return (false, "Cần tên mẫu thông điệp.", 0);
        if (content.Length == 0) return (false, "Cần nội dung mẫu thông điệp.", 0);

        var e = await db.MessageTemplates.FirstOrDefaultAsync(m => m.MessageTplCode == code);
        var created = e == null;
        if (e == null) { e = new MessageTemplate { MessageTplCode = code }; db.MessageTemplates.Add(e); }
        e.MessageTplName = name;
        e.MessageTypeCode = type;
        e.MessageTplContent = content;
        if (!string.IsNullOrWhiteSpace(fileName)) e.MessageTplFileName = fileName.Trim();
        if (!string.IsNullOrWhiteSpace(fileSpec))
        {
            e.MessageTplFileSpec = fileSpec.Trim();
            e.MessageTplFilePath = $"{DateTime.Now:yyyy-MM-dd}/{e.MessageTplFileName ?? code + ".rtmpl"}";
        }
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã {(created ? "tạo" : "cập nhật")} mẫu thông điệp {code}.", e.Id);
    }

    // Xóa mẫu thông điệp theo mã (theo Mst_MessageTemplate_Delete của TVAN gốc): chặn khi mã không tồn tại.
    public async Task<(bool ok, string msg)> DeleteMessageTemplateAsync(string code)
    {
        code = (code ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã mẫu thông điệp.");
        var e = await db.MessageTemplates.FirstOrDefaultAsync(m => m.MessageTplCode == code);
        if (e == null) return (false, "Không tìm thấy mẫu thông điệp với mã này.");
        db.MessageTemplates.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa mẫu thông điệp {code}.");
    }

    // Danh mục khách hàng / người mua (theo Mst_CustomerNNT của TVAN gốc):
    // danh sách khách hàng của một NNT (lọc theo MST nếu có).
    public Task<List<CustomerNnt>> CustomerNntsAsync(string? mst)
    {
        var q = db.CustomerNnts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(mst)) q = q.Where(c => c.MST == mst.Trim());
        return q.OrderBy(c => c.CustomerNNTCode).ToListAsync();
    }

    public Task<CustomerNnt?> GetCustomerNntAsync(int id) =>
        db.CustomerNnts.FirstOrDefaultAsync(c => c.Id == id);

    // Lưu (tạo mới/cập nhật) khách hàng theo khóa nghiệp vụ (MST, CustomerNNTCode)
    // (theo Mst_CustomerNNT_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã khách hàng + tên khách hàng;
    //  - MST phải là NNT đã tồn tại (bên bán sở hữu danh mục);
    //  - khi tạo: mã khách hàng chưa tồn tại trong phạm vi NNT;
    //  - CustomerMST (nếu khai báo) không được trùng với khách hàng khác của cùng NNT.
    public async Task<(bool ok, string msg, int id)> SaveCustomerNntAsync(int? id, string mst, string code, string name, string? customerMst, string? type, string? address, string? email, string? phone, string? fax, string? contactName, string? contactPhone, string? contactEmail, DateTime? dob, string? provinceCode, string? districtCode, string? accNo, string? bankName, string? govIdType, string? govId, string? remark, bool active, string? by)
    {
        mst = (mst ?? "").Trim();
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        customerMst = string.IsNullOrWhiteSpace(customerMst) ? null : customerMst.Trim();
        if (code.Length == 0) return (false, "Cần mã khách hàng.", 0);
        if (name.Length == 0) return (false, "Cần tên khách hàng.", 0);
        if (mst.Length == 0) return (false, "Cần MST người nộp thuế.", 0);
        if (!await db.Nnts.AnyAsync(n => n.Mst == mst)) return (false, "Không tìm thấy NNT với MST này.", 0);

        CustomerNnt? e = null;
        if (id.HasValue && id.Value > 0) e = await db.CustomerNnts.FirstOrDefaultAsync(c => c.Id == id.Value);
        else e = await db.CustomerNnts.FirstOrDefaultAsync(c => c.MST == mst && c.CustomerNNTCode == code);

        if (e == null)
        {
            if (await db.CustomerNnts.AnyAsync(c => c.MST == mst && c.CustomerNNTCode == code))
                return (false, "Mã khách hàng đã tồn tại cho NNT này.", 0);
            e = new CustomerNnt { MST = mst, CustomerNNTCode = code };
            db.CustomerNnts.Add(e);
        }
        else
        {
            // Đổi mã khách hàng: chặn trùng với khách hàng khác của cùng NNT.
            if (!string.Equals(e.CustomerNNTCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.CustomerNnts.AnyAsync(c => c.MST == mst && c.CustomerNNTCode == code && c.Id != e.Id))
                return (false, "Mã khách hàng đã tồn tại cho NNT này.", 0);
            e.CustomerNNTCode = code;
        }

        // CustomerMST duy nhất trong phạm vi một NNT (theo Mst_CustomerNNT_CheckMST của TVAN gốc).
        if (customerMst != null && await db.CustomerNnts.AnyAsync(c => c.MST == mst && c.CustomerMST == customerMst && c.Id != e.Id))
            return (false, "MST khách hàng đã tồn tại cho NNT này.", 0);

        e.CustomerNNTName = name;
        e.CustomerMST = customerMst;
        e.CustomerNNTType = type;
        e.CustomerNNTAddress = address;
        e.CustomerNNTEmail = email;
        e.CustomerNNTPhone = phone;
        e.CustomerNNTFax = fax;
        e.ContactName = contactName;
        e.ContactPhone = contactPhone;
        e.ContactEmail = contactEmail;
        e.CustomerNNTDOB = dob;
        e.ProvinceCode = provinceCode;
        e.DistrictCode = districtCode;
        e.AccNo = accNo;
        e.BankName = bankName;
        e.GovIDType = govIdType;
        e.GovID = govId;
        e.Remark = remark;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu khách hàng {code} — {name}.", e.Id);
    }

    // Xóa khách hàng theo id (theo Mst_CustomerNNT_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteCustomerNntAsync(int id)
    {
        var e = await db.CustomerNnts.FirstOrDefaultAsync(c => c.Id == id);
        if (e == null) return (false, "Không tìm thấy khách hàng.");
        var code = e.CustomerNNTCode;
        db.CustomerNnts.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa khách hàng {code}.");
    }

    // Danh mục loại người nộp thuế (theo Mst_NNTType của TVAN gốc):
    // danh sách loại NNT (lọc theo từ khóa tên/mã nếu có).
    public Task<List<NntType>> NntTypesAsync(string? keyword)
    {
        var q = db.NntTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(t => t.NNTType.Contains(k) || t.NNTTypeName.Contains(k));
        }
        return q.OrderBy(t => t.NNTType).ToListAsync();
    }

    public Task<NntType?> GetNntTypeAsync(int id) =>
        db.NntTypes.FirstOrDefaultAsync(t => t.Id == id);

    // Lưu (tạo mới/cập nhật) loại NNT theo khóa nghiệp vụ (OrgId, NNTType)
    // (theo Mst_NNTType_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã loại NNT + tên loại NNT;
    //  - khi tạo: mã loại NNT chưa tồn tại trong tổ chức (Mst_NNTType_CheckDB_NNTTypeExist).
    public async Task<(bool ok, string msg, int id)> SaveNntTypeAsync(int? id, string code, string name, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã loại người nộp thuế.", 0);
        if (name.Length == 0) return (false, "Cần tên loại người nộp thuế.", 0);

        NntType? e = null;
        if (id.HasValue && id.Value > 0) e = await db.NntTypes.FirstOrDefaultAsync(t => t.Id == id.Value);
        else e = await db.NntTypes.FirstOrDefaultAsync(t => t.NNTType == code);

        if (e == null)
        {
            if (await db.NntTypes.AnyAsync(t => t.NNTType == code))
                return (false, "Mã loại người nộp thuế đã tồn tại.", 0);
            e = new NntType { NNTType = code };
            db.NntTypes.Add(e);
        }
        else
        {
            // Đổi mã loại NNT: chặn trùng với loại khác.
            if (!string.Equals(e.NNTType, code, StringComparison.OrdinalIgnoreCase)
                && await db.NntTypes.AnyAsync(t => t.NNTType == code && t.Id != e.Id))
                return (false, "Mã loại người nộp thuế đã tồn tại.", 0);
            e.NNTType = code;
        }

        e.NNTTypeName = name;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu loại người nộp thuế {code} — {name}.", e.Id);
    }

    // Xóa loại NNT theo id (theo Mst_NNTType_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteNntTypeAsync(int id)
    {
        var e = await db.NntTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (e == null) return (false, "Không tìm thấy loại người nộp thuế.");
        var code = e.NNTType;
        db.NntTypes.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa loại người nộp thuế {code}.");
    }

    // Danh mục Tỉnh/Thành phố (theo Mst_Province của TVAN gốc):
    // danh sách tỉnh/thành (lọc theo từ khóa mã/tên nếu có).
    public Task<List<Province>> ProvincesAsync(string? keyword)
    {
        var q = db.Provinces.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(p => p.ProvinceCode.Contains(k) || p.ProvinceName.Contains(k));
        }
        return q.OrderBy(p => p.ProvinceCode).ToListAsync();
    }

    public Task<Province?> GetProvinceAsync(int id) =>
        db.Provinces.FirstOrDefaultAsync(p => p.Id == id);

    // Lưu (tạo mới/cập nhật) tỉnh/thành theo khóa nghiệp vụ (OrgId, ProvinceCode)
    // (theo Mst_Province_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã tỉnh/thành + tên tỉnh/thành;
    //  - khi tạo: mã tỉnh/thành chưa tồn tại trong tổ chức (Mst_Province_CheckDB_ProvinceExist).
    public async Task<(bool ok, string msg, int id)> SaveProvinceAsync(int? id, string code, string name, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã tỉnh/thành phố.", 0);
        if (name.Length == 0) return (false, "Cần tên tỉnh/thành phố.", 0);

        Province? e = null;
        if (id.HasValue && id.Value > 0) e = await db.Provinces.FirstOrDefaultAsync(p => p.Id == id.Value);
        else e = await db.Provinces.FirstOrDefaultAsync(p => p.ProvinceCode == code);

        if (e == null)
        {
            if (await db.Provinces.AnyAsync(p => p.ProvinceCode == code))
                return (false, "Mã tỉnh/thành phố đã tồn tại.", 0);
            e = new Province { ProvinceCode = code };
            db.Provinces.Add(e);
        }
        else
        {
            // Đổi mã tỉnh/thành: chặn trùng với tỉnh/thành khác.
            if (!string.Equals(e.ProvinceCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.Provinces.AnyAsync(p => p.ProvinceCode == code && p.Id != e.Id))
                return (false, "Mã tỉnh/thành phố đã tồn tại.", 0);
            e.ProvinceCode = code;
        }

        e.ProvinceName = name;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu tỉnh/thành phố {code} — {name}.", e.Id);
    }

    // Xóa tỉnh/thành theo id (theo Mst_Province_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteProvinceAsync(int id)
    {
        var e = await db.Provinces.FirstOrDefaultAsync(p => p.Id == id);
        if (e == null) return (false, "Không tìm thấy tỉnh/thành phố.");
        var code = e.ProvinceCode;
        db.Provinces.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa tỉnh/thành phố {code}.");
    }

    // Danh mục Quận/Huyện (theo Mst_District của TVAN gốc):
    // danh sách quận/huyện (lọc theo tỉnh/thành và từ khóa mã/tên nếu có).
    public Task<List<District>> DistrictsAsync(string? provinceCode, string? keyword)
    {
        var q = db.Districts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(provinceCode))
        {
            var pc = provinceCode.Trim();
            q = q.Where(d => d.ProvinceCode == pc);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(d => d.DistrictCode.Contains(k) || d.DistrictName.Contains(k));
        }
        return q.OrderBy(d => d.ProvinceCode).ThenBy(d => d.DistrictCode).ToListAsync();
    }

    public Task<District?> GetDistrictAsync(int id) =>
        db.Districts.FirstOrDefaultAsync(d => d.Id == id);

    // Lưu (tạo mới/cập nhật) quận/huyện theo khóa nghiệp vụ (OrgId, ProvinceCode, DistrictCode)
    // (theo Mst_District_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã quận/huyện + tên quận/huyện;
    //  - tỉnh/thành (ProvinceCode) phải tồn tại và đang dùng (Mst_Province_CheckDB);
    //  - khi tạo: mã quận/huyện chưa tồn tại trong tỉnh (Mst_District_CheckDB_DistrictExist).
    public async Task<(bool ok, string msg, int id)> SaveDistrictAsync(int? id, string provinceCode, string code, string name, bool active, string? by)
    {
        provinceCode = (provinceCode ?? "").Trim();
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (provinceCode.Length == 0) return (false, "Cần chọn tỉnh/thành phố.", 0);
        if (code.Length == 0) return (false, "Cần mã quận/huyện.", 0);
        if (name.Length == 0) return (false, "Cần tên quận/huyện.", 0);

        // Tỉnh/thành phải tồn tại và đang dùng (theo Mst_Province_CheckDB của TVAN gốc).
        var province = await db.Provinces.FirstOrDefaultAsync(p => p.ProvinceCode == provinceCode);
        if (province == null) return (false, "Tỉnh/thành phố không tồn tại.", 0);
        if (!province.FlagActive) return (false, "Tỉnh/thành phố đã ngừng dùng.", 0);

        District? e = null;
        if (id.HasValue && id.Value > 0) e = await db.Districts.FirstOrDefaultAsync(d => d.Id == id.Value);
        else e = await db.Districts.FirstOrDefaultAsync(d => d.ProvinceCode == provinceCode && d.DistrictCode == code);

        if (e == null)
        {
            if (await db.Districts.AnyAsync(d => d.ProvinceCode == provinceCode && d.DistrictCode == code))
                return (false, "Mã quận/huyện đã tồn tại trong tỉnh/thành này.", 0);
            e = new District { ProvinceCode = provinceCode, DistrictCode = code };
            db.Districts.Add(e);
        }
        else
        {
            // Đổi mã quận/huyện: chặn trùng với quận/huyện khác trong cùng tỉnh.
            if (!string.Equals(e.DistrictCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.Districts.AnyAsync(d => d.ProvinceCode == provinceCode && d.DistrictCode == code && d.Id != e.Id))
                return (false, "Mã quận/huyện đã tồn tại trong tỉnh/thành này.", 0);
            e.ProvinceCode = provinceCode;
            e.DistrictCode = code;
        }

        e.DistrictName = name;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu quận/huyện {code} — {name}.", e.Id);
    }

    // Xóa quận/huyện theo id (theo Mst_District_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteDistrictAsync(int id)
    {
        var e = await db.Districts.FirstOrDefaultAsync(d => d.Id == id);
        if (e == null) return (false, "Không tìm thấy quận/huyện.");
        var code = e.DistrictCode;
        db.Districts.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa quận/huyện {code}.");
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
