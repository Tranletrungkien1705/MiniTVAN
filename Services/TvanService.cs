using Microsoft.EntityFrameworkCore;
using MiniTVAN.Data;
using MiniTVAN.Models;

namespace MiniTVAN.Services;

public record TvanDash(int Nnts, int Registered, int Invoices, int Sent, int Accepted, int Rejected, decimal AcceptedValue, List<Invoice> Recent);

public interface ITvanService
{
    Task<List<Nnt>> NntsAsync();
    Task<List<Nnt>> NntsAsync(string? keyword, string? mst, string? dlCode, RegStatus? regStatus);
    Task<Nnt?> GetNntAsync(int id);
    Task<(bool ok, string msg, int id)> CreateNntAsync(Nnt n);
    Task<(bool ok, string msg)> RegisterNntAsync(int id);
    // Hồ sơ NNT đầy đủ (theo Mst_NNT_Create/Update/Delete của TVAN gốc).
    Task<(bool ok, string msg, int id)> SaveNntAsync(int? id, NntProfile p);
    Task<(bool ok, string msg)> DeleteNntAsync(int id);
    // Cập nhật trạng thái đăng ký NNT (theo Mst_NNT_UpdateRegisterStatusX của TVAN gốc).
    Task<(bool ok, string msg)> UpdateNntRegisterStatusAsync(int id, RegStatus status, string? remark, string? by);
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
    Task<TaxOffice?> GetTaxOfficeAsync(int id);
    Task<(bool ok, string msg, int id)> SaveTaxOfficeAsync(int? id, string code, string? codeParent, string? provinceCode, string? districtCode, string name, string? level, string? address, string? contactEmail, string? contactPhone, bool active, string? by);
    Task<(bool ok, string msg)> DeleteTaxOfficeAsync(int id);
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
    Task<DynamicComma> GetDynamicCommaAsync();
    Task<(bool ok, string msg)> SetDynamicCommaAsync(DynamicCommaStyle flagStyle, string? by);
    Task<List<InvoiceTemplate>> TemplatesAsync(int? nntId);
    Task<(bool ok, string msg)> IssueTemplateAsync(int templateId, DateTime effDateStart, string? remark);
    Task<(bool ok, string msg)> InactivateTemplateAsync(int templateId, string? remark);
    Task<(bool ok, string msg)> CancelTemplateAsync(int templateId, string? remark, string? by);
    Task<(bool ok, string msg)> IncreaseTemplateEndNoAsync(int templateId, int newEndInvoiceNo, string? remark, string? by);
    Task<(bool ok, string msg)> UpdateTemplateQtyNoAsync(int templateId, int startInvoiceNo, int endInvoiceNo, string? remark, string? by);
    Task<List<TemplateRangeLog>> TemplateRangeLogsAsync(int? templateId);
    Task<(bool ok, string msg)> UpdateTemplateContactAsync(int templateId, string? nntName, string? nntAddress, string? nntPhone, string? nntEmail, string? nntWebsite, bool flagStyleComma, string? by);
    Task<(bool ok, string msg)> UpdateTemplateBankAsync(int templateId, string? nntAccNo, string? nntBankName, string? by);
    Task<(bool ok, string msg, int id)> SaveTemplateAsync(int? id, string tInvoiceCode, int nntId, string tInvoiceName, string formNo, string sign, InvoiceNoRule ttType, string? remark, string? by);
    Task<(bool ok, string msg)> DeleteTemplateAsync(int templateId);
    Task<(bool ok, string msg, string? invoiceNo)> AllocateInvoiceNoAsync(int invoiceId, DateTime invoiceDate, string? by);
    Task<(bool ok, string msg, string? invoiceNo)> AllocateApproveIssueAsync(int invoiceId, DateTime invoiceDate, string? filePath, string? pdfFilePath, string? emailSend, string? note, string? by);
    Task<List<InvoiceNoAllocLog>> AllocLogsAsync(int? invoiceId);
    Task<(bool ok, string msg, string? mccqtmtt)> AllocateInvoiceNoTypeMAsync(int invoiceId, DateTime invoiceDate, string? by);
    Task<(bool ok, string msg, string? mccqtmtt)> GenMccqtMttAsync(int invoiceId);
    Task<(bool ok, string msg)> ReceiveTctResultAsync(int invoiceId, TctMessageType mltDiep, string? maCQT, string? maLoi, string? lyDo);
    Task<List<TctReceiveLog>> TctReceiveLogsAsync(int? invoiceId);
    Task<(bool ok, string msg, string? tctRefNo)> SendTct300Async(int invoiceId, ReplaceOrAdjustFlag flagReplaceOrAdjust, string? loaiTb, string? soTb, DateTime? ngayTb, string? lyDo, string? by);
    Task<List<Tct300Log>> Tct300LogsAsync(int? invoiceId);
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
    Task<List<CustomerNntType>> CustomerNntTypesAsync(string? keyword);
    Task<CustomerNntType?> GetCustomerNntTypeAsync(int id);
    Task<(bool ok, string msg, int id)> SaveCustomerNntTypeAsync(int? id, string code, string name, string? remark, bool active, string? by);
    Task<(bool ok, string msg)> DeleteCustomerNntTypeAsync(int id);
    Task<List<VatRate>> VatRatesAsync(string? keyword);
    Task<VatRate?> GetVatRateAsync(int id);
    Task<(bool ok, string msg, int id)> SaveVatRateAsync(int? id, string code, string rate, string? desc, bool active, string? by);
    Task<(bool ok, string msg)> DeleteVatRateAsync(int id);
    Task<List<Unit>> UnitsAsync(string? keyword);
    Task<Unit?> GetUnitAsync(int id);
    Task<(bool ok, string msg, int id)> SaveUnitAsync(int? id, string code, string name, string? remark, bool active, string? by);
    Task<(bool ok, string msg)> DeleteUnitAsync(int id);
    Task<List<Province>> ProvincesAsync(string? keyword);
    Task<Province?> GetProvinceAsync(int id);
    Task<(bool ok, string msg, int id)> SaveProvinceAsync(int? id, string code, string name, bool active, string? by);
    Task<(bool ok, string msg)> DeleteProvinceAsync(int id);
    Task<List<District>> DistrictsAsync(string? provinceCode, string? keyword);
    Task<District?> GetDistrictAsync(int id);
    Task<(bool ok, string msg, int id)> SaveDistrictAsync(int? id, string provinceCode, string code, string name, bool active, string? by);
    Task<(bool ok, string msg)> DeleteDistrictAsync(int id);
    Task<List<Country>> CountriesAsync(string? keyword);
    Task<Country?> GetCountryAsync(int id);
    Task<(bool ok, string msg, int id)> SaveCountryAsync(int? id, string code, string name, bool active, string? by);
    Task<(bool ok, string msg)> DeleteCountryAsync(int id);
    Task<List<Dealer>> DealersAsync(string? keyword, string? provinceCode);
    Task<Dealer?> GetDealerAsync(int id);
    Task<(bool ok, string msg, int id)> SaveDealerAsync(int? id, string code, string name, string provinceCode, string? address, string? presentBy, string? govIdNumber, string? email, string? phone, bool active, string? by);
    Task<(bool ok, string msg)> DeleteDealerAsync(int id);
    Task<List<Department>> DepartmentsAsync(string? mst, string? keyword);
    Task<Department?> GetDepartmentAsync(int id);
    Task<(bool ok, string msg, int id)> SaveDepartmentAsync(int? id, string code, string? codeParent, string mst, string name, bool active, string? by);
    Task<(bool ok, string msg)> DeleteDepartmentAsync(int id);
    Task<List<OrgCks>> OrgCksesAsync(string? keyword);
    Task<OrgCks?> GetOrgCksAsync(int id);
    Task<(bool ok, string msg, int id)> SaveOrgCksAsync(int? id, string caNumber, string? caOrg, string? subject, DateTime? effStart, DateTime? effEnd, string? ctsPath, string? ctsPwd, bool active, string? by);
    Task<(bool ok, string msg)> DeleteOrgCksAsync(int id);

    // Loại thông báo (theo Mst_NotifyType của TVAN gốc)
    Task<List<NotifyType>> NotifyTypesAsync(string? keyword);
    Task<NotifyType?> GetNotifyTypeAsync(int id);
    Task<(bool ok, string msg, int id)> SaveNotifyTypeAsync(int? id, string code, string? desc, bool defaultActive, bool active, string? by);
    Task<(bool ok, string msg)> DeleteNotifyTypeAsync(int id);

    // Thông báo hệ thống (theo Notify_Notify / Notify_NotifyDtl của TVAN gốc)
    Task<List<Notify>> NotifiesAsync(string? keyword);
    Task<Notify?> GetNotifyAsync(int id);
    Task<(bool ok, string msg, int id)> CreateNotifyAsync(string notifyNo, string desc, DateTime effDateStart, DateTime effDateEnd, bool sendEmail, string? by);
    Task<(bool ok, string msg)> UpdateNotifyAsync(int id, string? desc, bool sendEmail, string? by);
    Task<(bool ok, string msg)> DeleteNotifyAsync(int id);
    Task<(bool ok, string msg, int id)> AddNotifyDtlAsync(int notifyId, string userCode, bool flagRead, string? by);
    Task<(bool ok, string msg)> MarkNotifyReadAsync(int notifyId, string userCode);

    // Người nhận thông báo (theo Mst_ManageNotify / Map_UserInNotifyType của TVAN gốc)
    Task<List<NotifyRecipient>> NotifyRecipientsAsync(string? keyword);
    Task<NotifyRecipient?> GetNotifyRecipientAsync(int id);
    Task<(bool ok, string msg, int id)> CreateNotifyRecipientAsync(string userCode, string? userName, string? by);
    Task<(bool ok, string msg)> UpdateNotifyRecipientAsync(int id, string? userName, string? by);
    Task<(bool ok, string msg)> DeleteNotifyRecipientAsync(int id);
    Task<(bool ok, string msg)> SaveNotifyRecipientTypesAsync(int id, List<(string notifyType, bool flagNotify)> types, string? by);

    // Nhóm người dùng (theo Sys_Group / Sys_UserInGroup của TVAN gốc)
    Task<List<SysGroup>> SysGroupsAsync(string? keyword);
    Task<SysGroup?> GetSysGroupAsync(int id);
    Task<(bool ok, string msg, int id)> SaveSysGroupAsync(int? id, string groupCode, string groupName, bool active, string? by);
    Task<(bool ok, string msg)> DeleteSysGroupAsync(int id);
    Task<(bool ok, string msg)> SaveSysGroupMembersAsync(int id, List<string> userCodes, string? by);

    // Gói Module (theo Sys_Modules / Sys_Solution của TVAN gốc)
    Task<List<SysModule>> SysModulesAsync(string? keyword);
    Task<SysModule?> GetSysModuleAsync(int id);
    Task<(bool ok, string msg, int id)> SaveSysModuleAsync(int? id, string moduleCode, string solutionCode, string moduleName, string? description, double qtyInvoice, double valCapacity, bool active, string? by);
    Task<(bool ok, string msg)> DeleteSysModuleAsync(int id);
    Task<(bool ok, string msg)> SetSysModuleActiveAsync(int id, bool active, string? by);
    Task<List<SysSolution>> SysSolutionsAsync(string? keyword);

    // Đối tượng (chức năng) + phân gán vào gói Module (theo Sys_Object / Sys_ObjectInModules của TVAN gốc)
    Task<List<SysObject>> SysObjectsAsync(string? keyword);
    Task<(bool ok, string msg, int id)> SaveSysObjectAsync(int? id, string objectCode, string objectName, string? serviceCode, SysObjectType objectType, bool active, string? by);
    Task<(bool ok, string msg)> DeleteSysObjectAsync(int id);
    Task<List<SysObjectInModule>> SysObjectInModulesAsync(string? moduleCode);
    Task<(bool ok, string msg)> SaveSysObjectInModulesAsync(int moduleId, List<string> objectCodes, string? by);

    // Cấu hình định dạng cột hiển thị theo bảng (theo Mst_ColumnConfig của TVAN gốc)
    Task<List<ColumnConfig>> ColumnConfigsAsync(string? tableName, string? keyword);
    Task<ColumnConfig?> GetColumnConfigAsync(int id);
    Task<(bool ok, string msg, int id)> SaveColumnConfigAsync(int? id, string tableName, string columnName, string? columnFormat, string? columnDesc, bool active, string? by);
    Task<(bool ok, string msg)> DeleteColumnConfigAsync(int id);
    Task<List<SortColumnInvoice>> SortColumnInvoicesAsync(string? keyword);
    Task<SortColumnInvoice?> GetSortColumnInvoiceAsync(int id);
    Task<(bool ok, string msg, int id)> SaveSortColumnInvoiceAsync(int? id, string columnCode, int idx, string columnName, SortColumnType columnType, bool active, string? by);
    Task<(bool ok, string msg)> DeleteSortColumnInvoiceAsync(int id);

    // Danh mục tiền tệ / ngoại tệ (theo Mst_CurrencyEx của TVAN gốc)
    Task<List<CurrencyEx>> CurrencyExesAsync(string? keyword);
    Task<CurrencyEx?> GetCurrencyExAsync(int id);
    Task<(bool ok, string msg, int id)> SaveCurrencyExAsync(int? id, string code, string name, string? baseCode, decimal buyRate, decimal sellRate, string? remark, bool active, string? by);
    Task<(bool ok, string msg)> DeleteCurrencyExAsync(int id);

    // Đọc tiền bằng chữ (theo luồng DocTien của TVAN gốc)
    Task<(bool ok, string msg, string text, int id)> DocTienAsync(decimal amount, string? currencyCode, string? by);
    Task<List<DocTienLog>> DocTienLogsAsync();
}

// Hồ sơ NNT đầy đủ dùng khi lưu (theo Mst_NNT_Create/Update của TVAN gốc).
public record NntProfile(
    string Mst, string Name, string? MstParent, string? ProvinceCode, string? DistrictCode, string? DLCode,
    string? Address, string? Mobile, string? Phone, string? Fax, string? PresentBy, string? BusinessRegNo,
    string? NntPosition, string? PresentIdNo, string? PresentIdType, string? GovTaxID, string? ContactName,
    string? ContactPhone, string? ContactEmail, string? Website, string? CANumber, string? CAOrg,
    DateTime? CAEffDTimeUTCStart, DateTime? CAEffDTimeUTCEnd, string? AccNo, string? AccHolder, string? BankName,
    string? BizType, string? BizFieldCode, string? BizSizeCode, bool Active, string? By);

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

    // Danh sách NNT (theo Mst_NNT_Get của TVAN gốc): lọc theo từ khóa (MST/tên/liên hệ),
    // MST chính xác, mã đại lý và trạng thái đăng ký.
    public Task<List<Nnt>> NntsAsync(string? keyword, string? mst, string? dlCode, RegStatus? regStatus)
    {
        var q = db.Nnts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(mst))
        {
            var m = mst.Trim();
            q = q.Where(n => n.Mst == m);
        }
        if (!string.IsNullOrWhiteSpace(dlCode))
        {
            var d = dlCode.Trim();
            q = q.Where(n => n.DLCode == d);
        }
        if (regStatus.HasValue) q = q.Where(n => n.RegStatus == regStatus.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(n => n.Mst.Contains(k) || n.Name.Contains(k)
                || (n.ContactEmail != null && n.ContactEmail.Contains(k))
                || (n.ContactPhone != null && n.ContactPhone.Contains(k)));
        }
        return q.OrderBy(n => n.Name).ToListAsync();
    }

    // Lưu (tạo mới/cập nhật) hồ sơ NNT theo khóa nghiệp vụ (OrgId, MST)
    // (theo Mst_NNT_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần MST + tên NNT + địa chỉ + người đại diện + chức vụ + tên/ĐT/email liên hệ
    //    (Mst_NNT_Create_InvalidMST / _InvalidNNTFullName / _InvalidNNTAddress / _InvalidPresentBy /
    //     _InvalidNNTPosition / _InvalidContactName / _InvalidContactPhone / _InvalidContactEmail);
    //  - khi tạo: MST chưa tồn tại trong tổ chức (Mst_NNT_CheckDB_NNTExist);
    //  - đơn vị trực thuộc (nếu có) phải tồn tại + đang dùng (Mst_NNT_CheckDB);
    //  - tỉnh/thành + quận/huyện phải tồn tại + đang dùng (Mst_Province_CheckDB / Mst_District_CheckDB);
    //  - đại lý (nếu có) phải tồn tại + đang dùng (Mst_Dealer_CheckDB);
    //  - cơ quan thuế quản lý (nếu có) phải tồn tại + đang dùng (Mst_GovTaxID_CheckDB).
    public async Task<(bool ok, string msg, int id)> SaveNntAsync(int? id, NntProfile p)
    {
        var mst = (p.Mst ?? "").Trim();
        var name = (p.Name ?? "").Trim();
        var address = (p.Address ?? "").Trim();
        var presentBy = (p.PresentBy ?? "").Trim();
        var position = (p.NntPosition ?? "").Trim();
        var contactName = (p.ContactName ?? "").Trim();
        var contactPhone = (p.ContactPhone ?? "").Trim();
        var contactEmail = (p.ContactEmail ?? "").Trim();
        if (mst.Length == 0) return (false, "Cần mã số thuế.", 0);
        if (name.Length == 0) return (false, "Cần tên người nộp thuế.", 0);
        if (address.Length == 0) return (false, "Cần địa chỉ NNT.", 0);
        if (presentBy.Length == 0) return (false, "Cần người đại diện.", 0);
        if (position.Length == 0) return (false, "Cần chức vụ người đại diện.", 0);
        if (contactName.Length == 0) return (false, "Cần tên người liên hệ.", 0);
        if (contactPhone.Length == 0) return (false, "Cần điện thoại người liên hệ.", 0);
        if (contactEmail.Length == 0) return (false, "Cần email người liên hệ.", 0);

        // Đơn vị trực thuộc (nếu có) phải tồn tại + đang dùng (theo Mst_NNT_CheckDB của TVAN gốc).
        var mstParent = (p.MstParent ?? "").Trim();
        if (mstParent.Length > 0)
        {
            var parent = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == mstParent);
            if (parent == null) return (false, "Đơn vị trực thuộc (MST cấp trên) không tồn tại.", 0);
            if (!parent.FlagActive) return (false, "Đơn vị trực thuộc (MST cấp trên) đã ngừng dùng.", 0);
        }

        // Tỉnh/thành + quận/huyện phải tồn tại + đang dùng (theo Mst_Province_CheckDB / Mst_District_CheckDB).
        var provinceCode = (p.ProvinceCode ?? "").Trim();
        var districtCode = (p.DistrictCode ?? "").Trim();
        if (provinceCode.Length > 0)
        {
            var prov = await db.Provinces.FirstOrDefaultAsync(x => x.ProvinceCode == provinceCode);
            if (prov == null) return (false, "Tỉnh/thành phố không tồn tại.", 0);
            if (!prov.FlagActive) return (false, "Tỉnh/thành phố đã ngừng dùng.", 0);
        }
        if (districtCode.Length > 0)
        {
            var dist = await db.Districts.FirstOrDefaultAsync(x => x.ProvinceCode == provinceCode && x.DistrictCode == districtCode);
            if (dist == null) return (false, "Quận/huyện không tồn tại.", 0);
            if (!dist.FlagActive) return (false, "Quận/huyện đã ngừng dùng.", 0);
        }

        // Đại lý (nếu có) phải tồn tại + đang dùng (theo Mst_Dealer_CheckDB).
        var dlCode = (p.DLCode ?? "").Trim();
        if (dlCode.Length > 0)
        {
            var dealer = await db.Dealers.FirstOrDefaultAsync(x => x.DLCode == dlCode);
            if (dealer == null) return (false, "Đại lý không tồn tại.", 0);
            if (!dealer.FlagActive) return (false, "Đại lý đã ngừng hoạt động.", 0);
        }

        // Cơ quan thuế quản lý (nếu có) phải tồn tại + đang dùng (theo Mst_GovTaxID_CheckDB).
        var govTaxID = (p.GovTaxID ?? "").Trim();
        if (govTaxID.Length > 0)
        {
            var office = await db.TaxOffices.FirstOrDefaultAsync(x => x.GovTaxID == govTaxID);
            if (office == null) return (false, "Cơ quan thuế quản lý không tồn tại.", 0);
            if (!office.FlagActive) return (false, "Cơ quan thuế quản lý đã ngừng dùng.", 0);
        }

        Nnt? e = null;
        if (id.HasValue && id.Value > 0) e = await db.Nnts.FirstOrDefaultAsync(n => n.Id == id.Value);
        else e = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == mst);

        if (e == null)
        {
            if (await db.Nnts.AnyAsync(n => n.Mst == mst))
                return (false, "MST đã tồn tại.", 0);
            e = new Nnt { Mst = mst, RegStatus = RegStatus.Pending };
            db.Nnts.Add(e);
        }
        else
        {
            // Đổi MST: chặn trùng với NNT khác.
            if (!string.Equals(e.Mst, mst, StringComparison.OrdinalIgnoreCase)
                && await db.Nnts.AnyAsync(n => n.Mst == mst && n.Id != e.Id))
                return (false, "MST đã tồn tại.", 0);
            e.Mst = mst;
        }

        e.Name = name;
        e.Address = address;
        e.Email = contactEmail;
        e.MstParent = mstParent.Length > 0 ? mstParent : null;
        e.ProvinceCode = provinceCode.Length > 0 ? provinceCode : null;
        e.DistrictCode = districtCode.Length > 0 ? districtCode : null;
        e.DLCode = dlCode.Length > 0 ? dlCode : null;
        e.Mobile = p.Mobile;
        e.Phone = p.Phone;
        e.Fax = p.Fax;
        e.PresentBy = presentBy;
        e.BusinessRegNo = p.BusinessRegNo;
        e.NntPosition = position;
        e.PresentIdNo = p.PresentIdNo;
        e.PresentIdType = p.PresentIdType;
        e.GovTaxID = govTaxID.Length > 0 ? govTaxID : null;
        e.ContactName = contactName;
        e.ContactPhone = contactPhone;
        e.Website = p.Website;
        e.CANumber = p.CANumber;
        e.CAOrg = p.CAOrg;
        e.CAEffDTimeUTCStart = p.CAEffDTimeUTCStart;
        e.CAEffDTimeUTCEnd = p.CAEffDTimeUTCEnd;
        e.AccNo = p.AccNo;
        e.AccHolder = p.AccHolder;
        e.BankName = p.BankName;
        e.BizType = p.BizType;
        e.BizFieldCode = p.BizFieldCode;
        e.BizSizeCode = p.BizSizeCode;
        e.FlagActive = p.Active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = p.By;
        await db.SaveChangesAsync();

        // Tính lại mã đơn vị nghiệp vụ/mẫu/cấp cho cây NNT (theo Mst_NNT_UpdBU của TVAN gốc).
        await UpdNntBuAsync();
        return (true, $"Đã lưu người nộp thuế {mst} — {name}.", e.Id);
    }

    // Xóa NNT theo id (theo Mst_NNT_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteNntAsync(int id)
    {
        var e = await db.Nnts.FirstOrDefaultAsync(n => n.Id == id);
        if (e == null) return (false, "Không tìm thấy người nộp thuế.");
        var mst = e.Mst;
        db.Nnts.Remove(e);
        await db.SaveChangesAsync();
        await UpdNntBuAsync();
        return (true, $"Đã xóa người nộp thuế {mst}.");
    }

    // Cập nhật trạng thái đăng ký NNT (theo Mst_NNT_UpdateRegisterStatusX của TVAN gốc):
    // chỉ cập nhật RegisterStatus + Remark, ghi người/thời điểm cập nhật.
    public async Task<(bool ok, string msg)> UpdateNntRegisterStatusAsync(int id, RegStatus status, string? remark, string? by)
    {
        var e = await db.Nnts.FirstOrDefaultAsync(n => n.Id == id);
        if (e == null) return (false, "Không tìm thấy người nộp thuế.");
        e.RegStatus = status;
        e.Remark = remark;
        if (status == RegStatus.Registered && e.RegisteredAt == null) e.RegisteredAt = DateTime.UtcNow;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật trạng thái đăng ký NNT {e.Mst}.");
    }

    // Tính lại mã đơn vị nghiệp vụ/mẫu/cấp cho cây NNT (theo Mst_NNT_UpdBU của TVAN gốc):
    // gốc 'ALL' (MstBuCode='ALL', pattern='ALL%', level=1), lan truyền 7 lần
    // MstBuCode = <MstBuCode cha> + '.' + <MST>, level = <level cha> + 1.
    private async Task UpdNntBuAsync()
    {
        const string root = "ALL";
        var all = await db.Nnts.ToListAsync();
        var byMst = all.ToDictionary(n => n.Mst, StringComparer.OrdinalIgnoreCase);
        for (int pass = 0; pass < 7; pass++)
        {
            foreach (var n in all)
            {
                if (string.Equals(n.Mst, root, StringComparison.OrdinalIgnoreCase)) { n.MstBuCode = root; n.MstBuPattern = root + "%"; n.MstLevel = 1; continue; }
                Nnt? parent = null;
                if (!string.IsNullOrWhiteSpace(n.MstParent)) byMst.TryGetValue(n.MstParent!, out parent);
                var parentBu = parent?.MstBuCode;
                n.MstBuCode = (string.IsNullOrEmpty(parentBu) ? "" : parentBu + ".") + n.Mst;
                n.MstBuPattern = n.MstBuCode + "%";
                n.MstLevel = (parent?.MstLevel ?? 0) + 1;
            }
        }
        await db.SaveChangesAsync();
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
        db.TaxOffices.OrderBy(t => t.GovTaxIDBUCode).ThenBy(t => t.GovTaxID).ToListAsync();

    public Task<TaxOffice?> GetTaxOfficeAsync(int id) =>
        db.TaxOffices.FirstOrDefaultAsync(t => t.Id == id);

    // Lưu (tạo mới/cập nhật) cơ quan thuế theo khóa nghiệp vụ (OrgId, GovTaxID)
    // (theo Mst_GovTaxID_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã CQT (Mst_GovTaxID_Create_InvalidGovTaxID);
    //  - cần tên CQT (Mst_GovTaxID_Create_InvalidGovTaxName / _Update_InvalidGovTaxName);
    //  - CQT cấp trên (nếu có) phải tồn tại và đang dùng (Mst_GovTaxID_CheckDB);
    //  - tỉnh/thành + quận/huyện (nếu có) phải tồn tại và đang dùng (Mst_District_CheckDB);
    //  - khi tạo: mã CQT chưa tồn tại (Mst_GovTaxID_CheckDB_GovTaxIDExist).
    // Sau khi lưu, tính lại mã đơn vị nghiệp vụ/mẫu/cấp cho toàn bộ cây (Mst_GovTaxID_UpdBU).
    public async Task<(bool ok, string msg, int id)> SaveTaxOfficeAsync(int? id, string code, string? codeParent, string? provinceCode, string? districtCode, string name, string? level, string? address, string? contactEmail, string? contactPhone, bool active, string? by)
    {
        code = (code ?? "").Trim();
        codeParent = (codeParent ?? "").Trim();
        provinceCode = (provinceCode ?? "").Trim();
        districtCode = (districtCode ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã cơ quan thuế.", 0);
        if (name.Length == 0) return (false, "Cần tên cơ quan thuế.", 0);

        // CQT cấp trên (nếu có) phải tồn tại và đang dùng (theo Mst_GovTaxID_CheckDB của TVAN gốc).
        if (codeParent.Length > 0)
        {
            var parent = await db.TaxOffices.FirstOrDefaultAsync(t => t.GovTaxID == codeParent);
            if (parent == null) return (false, "Cơ quan thuế cấp trên không tồn tại.", 0);
            if (!parent.FlagActive) return (false, "Cơ quan thuế cấp trên đã ngừng dùng.", 0);
        }

        // Tỉnh/thành + quận/huyện (nếu có) phải tồn tại và đang dùng (theo Mst_District_CheckDB của TVAN gốc).
        if (provinceCode.Length > 0 && districtCode.Length > 0)
        {
            var district = await db.Districts.FirstOrDefaultAsync(d => d.ProvinceCode == provinceCode && d.DistrictCode == districtCode);
            if (district == null) return (false, "Quận/huyện không tồn tại trong tỉnh/thành đã chọn.", 0);
            if (!district.FlagActive) return (false, "Quận/huyện đã ngừng dùng.", 0);
        }

        TaxOffice? e = null;
        if (id.HasValue && id.Value > 0) e = await db.TaxOffices.FirstOrDefaultAsync(t => t.Id == id.Value);
        else e = await db.TaxOffices.FirstOrDefaultAsync(t => t.GovTaxID == code);

        if (e == null)
        {
            if (await db.TaxOffices.AnyAsync(t => t.GovTaxID == code))
                return (false, "Mã cơ quan thuế đã tồn tại.", 0);
            e = new TaxOffice { GovTaxID = code };
            db.TaxOffices.Add(e);
        }
        else
        {
            // Đổi mã CQT: chặn trùng với CQT khác.
            if (!string.Equals(e.GovTaxID, code, StringComparison.OrdinalIgnoreCase)
                && await db.TaxOffices.AnyAsync(t => t.GovTaxID == code && t.Id != e.Id))
                return (false, "Mã cơ quan thuế đã tồn tại.", 0);
            e.GovTaxID = code;
        }

        e.GovTaxIDParent = codeParent.Length > 0 ? codeParent : null;
        e.ProvinceCode = provinceCode.Length > 0 ? provinceCode : null;
        e.DistrictCode = districtCode.Length > 0 ? districtCode : null;
        e.GovTaxName = name;
        e.Level = string.IsNullOrWhiteSpace(level) ? null : level.Trim();
        e.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        e.ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        e.ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();

        // Tính lại mã đơn vị nghiệp vụ/mẫu/cấp cho toàn bộ cây (theo Mst_GovTaxID_UpdBU của TVAN gốc).
        await RecomputeTaxOfficeBuAsync();
        return (true, $"Đã lưu cơ quan thuế {code} — {name}.", e.Id);
    }

    // Xóa cơ quan thuế theo id (theo Mst_GovTaxID_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteTaxOfficeAsync(int id)
    {
        var e = await db.TaxOffices.FirstOrDefaultAsync(t => t.Id == id);
        if (e == null) return (false, "Không tìm thấy cơ quan thuế.");
        var code = e.GovTaxID;
        db.TaxOffices.Remove(e);
        await db.SaveChangesAsync();
        await RecomputeTaxOfficeBuAsync();
        return (true, $"Đã xóa cơ quan thuế {code}.");
    }

    // Tính lại mã đơn vị nghiệp vụ (GovTaxIDBUCode), mẫu (GovTaxIDBUPattern) và cấp
    // (GovTaxIDLevel) cho toàn bộ cây CQT — theo Mst_GovTaxID_UpdBU của TVAN gốc:
    // CQT gốc '0100231226' có BUCode='0100231226', pattern='0100231226%', level=0;
    // các CQT khác có BUCode = <BUCode cha> + '.' + <mã>, pattern = BUCode + '%', level = <level cha> + 1.
    private async Task RecomputeTaxOfficeBuAsync()
    {
        const string root = "0100231226";
        var all = await db.TaxOffices.ToListAsync();
        var byCode = all.ToDictionary(t => t.GovTaxID, StringComparer.OrdinalIgnoreCase);
        for (int pass = 0; pass < 7; pass++)
        {
            foreach (var t in all)
            {
                if (string.Equals(t.GovTaxID, root, StringComparison.OrdinalIgnoreCase))
                {
                    t.GovTaxIDBUCode = root; t.GovTaxIDBUPattern = root + "%"; t.GovTaxIDLevel = 0; continue;
                }
                TaxOffice? parent = null;
                if (!string.IsNullOrWhiteSpace(t.GovTaxIDParent)) byCode.TryGetValue(t.GovTaxIDParent!, out parent);
                var parentBu = parent?.GovTaxIDBUCode;
                t.GovTaxIDBUCode = (string.IsNullOrEmpty(parentBu) ? "" : parentBu + ".") + t.GovTaxID;
                t.GovTaxIDBUPattern = t.GovTaxIDBUCode + "%";
                t.GovTaxIDLevel = (parent?.GovTaxIDLevel ?? 0) + 1;
            }
        }
        await db.SaveChangesAsync();
    }

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

    // Cấu hình dấu phân cách động (theo Mst_DynamicComma của TVAN gốc):
    // mỗi tổ chức có một bản ghi; mặc định dùng dấu phẩy ',' (Comma).
    public async Task<DynamicComma> GetDynamicCommaAsync()
    {
        var c = await db.DynamicCommas.FirstOrDefaultAsync();
        if (c == null)
        {
            c = new DynamicComma { FlagStyle = DynamicCommaStyle.Comma };
            db.DynamicCommas.Add(c); await db.SaveChangesAsync();
        }
        return c;
    }

    // Cập nhật kiểu dấu phân cách động (theo Mst_DynamicComma_Update của TVAN gốc:
    // FlagStyle = '0' dùng dấu phẩy ','; '1' dùng dấu chấm '.').
    public async Task<(bool ok, string msg)> SetDynamicCommaAsync(DynamicCommaStyle flagStyle, string? by)
    {
        var c = await GetDynamicCommaAsync();
        c.FlagStyle = flagStyle;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, flagStyle == DynamicCommaStyle.Dot
            ? "Đã đặt dấu phân cách động là dấu chấm '.'."
            : "Đã đặt dấu phân cách động là dấu phẩy ','.");
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

    // Hủy mẫu hóa đơn (theo Invoice_TempInvoice_Cancel của TVAN gốc):
    // chỉ hủy được mẫu đang sử dụng (Issued) và đang hoạt động (FlagActive).
    // Đưa mẫu sang CANCEL, ghi thời điểm/người hủy + lý do và số lượng hủy
    // (QtyCancel = EndInvoiceNo - QtyUsed, tức số hóa đơn còn lại chưa dùng).
    public async Task<(bool ok, string msg)> CancelTemplateAsync(int templateId, string? remark, string? by)
    {
        var tpl = await db.InvoiceTemplates.Include(t => t.Nnt).FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (tpl.TInvoiceStatus != TemplateStatus.Issued) return (false, "Chỉ hủy được mẫu đang sử dụng (ISSUED).");
        if (!tpl.FlagActive) return (false, "Mẫu đã ngừng hoạt động, không thể hủy.");

        var qtyCancel = tpl.EndInvoiceNo - tpl.QtyUsed;
        tpl.TInvoiceStatus = TemplateStatus.Cancel;
        tpl.QtyCancel = qtyCancel;
        tpl.CancelDTimeUTC = DateTime.UtcNow;
        tpl.CancelBy = by;
        tpl.FlagActive = false;
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Hủy mẫu hóa đơn {tpl.FormNo} ({tpl.TInvoiceCode}), số lượng hủy {qtyCancel}{(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã hủy mẫu {tpl.FormNo} ({tpl.TInvoiceCode}), số lượng hủy {qtyCancel}.");
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

    // Tạo mới / cập nhật mẫu hóa đơn (theo Invoice_TempInvoice_Save của TVAN gốc):
    // lưu lần đầu (id rỗng) = tạo mẫu mới ở trạng thái chờ (PENDING), lưu lại cùng mã = cập nhật.
    // Ràng buộc theo TVAN gốc:
    //  - Mã mẫu (TInvoiceCode) không được rỗng;
    //  - Nếu mẫu đã tồn tại thì phải đang ở trạng thái chờ (PENDING) mới được sửa;
    //  - Mẫu số (FormNo) và ký hiệu (Sign) không được rỗng;
    //  - NNT (MST) phải tồn tại.
    // Mẫu mới tạo có dải số rỗng (StartInvoiceNo = EndInvoiceNo = 0) chờ cấp phát.
    public async Task<(bool ok, string msg, int id)> SaveTemplateAsync(int? id, string tInvoiceCode, int nntId, string tInvoiceName, string formNo, string sign, InvoiceNoRule ttType, string? remark, string? by)
    {
        if (string.IsNullOrWhiteSpace(tInvoiceCode)) return (false, "Mã mẫu hóa đơn không được để trống.", 0);
        if (string.IsNullOrWhiteSpace(formNo)) return (false, "Mẫu số không được để trống.", 0);
        if (string.IsNullOrWhiteSpace(sign)) return (false, "Ký hiệu hóa đơn không được để trống.", 0);

        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.Id == nntId);
        if (nnt == null) return (false, "Không tìm thấy người nộp thuế.", 0);

        var code = tInvoiceCode.Trim();
        InvoiceTemplate? tpl = null;
        if (id.HasValue && id.Value > 0) tpl = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.Id == id.Value);
        tpl ??= await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.TInvoiceCode == code);

        if (tpl != null)
        {
            // Đã tồn tại → chỉ sửa được mẫu đang ở trạng thái chờ (PENDING).
            if (tpl.TInvoiceStatus != TemplateStatus.Draft)
                return (false, "Chỉ sửa được mẫu đang ở trạng thái chờ (PENDING).", tpl.Id);

            tpl.TInvoiceCode = code;
            tpl.NntId = nntId;
            tpl.TInvoiceName = string.IsNullOrWhiteSpace(tInvoiceName) ? code : tInvoiceName.Trim();
            tpl.FormNo = formNo.Trim();
            tpl.Sign = sign.Trim();
            tpl.TTType = ttType;
            db.Messages.Add(new TranMessage
            {
                NntId = nntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
                Text = $"Cập nhật mẫu hóa đơn {tpl.FormNo} ({tpl.TInvoiceCode}){(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
            });
            await db.SaveChangesAsync();
            return (true, $"Đã cập nhật mẫu {tpl.FormNo} ({tpl.TInvoiceCode}).", tpl.Id);
        }

        // Chưa tồn tại → tạo mẫu mới ở trạng thái chờ (PENDING), dải số rỗng chờ cấp phát.
        var created = new InvoiceTemplate
        {
            NntId = nntId, TInvoiceCode = code,
            TInvoiceName = string.IsNullOrWhiteSpace(tInvoiceName) ? code : tInvoiceName.Trim(),
            FormNo = formNo.Trim(), Sign = sign.Trim(), TTType = ttType,
            EffDateStart = DateTime.Today, StartInvoiceNo = 0, EndInvoiceNo = 0, QtyUsed = 0,
            TInvoiceStatus = TemplateStatus.Draft, FlagActive = true
        };
        db.InvoiceTemplates.Add(created);
        db.Messages.Add(new TranMessage
        {
            NntId = nntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Tạo mẫu hóa đơn {created.FormNo} ({created.TInvoiceCode}){(string.IsNullOrWhiteSpace(remark) ? "" : ": " + remark.Trim())}"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã tạo mẫu {created.FormNo} ({created.TInvoiceCode}).", created.Id);
    }

    // Xóa mẫu hóa đơn (theo Invoice_TempInvoice_Save với FlagIsDelete của TVAN gốc):
    // chỉ xóa được mẫu đang ở trạng thái chờ (PENDING) và chưa dùng số nào (QtyUsed = 0).
    public async Task<(bool ok, string msg)> DeleteTemplateAsync(int templateId)
    {
        var tpl = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.Id == templateId);
        if (tpl == null) return (false, "Không tìm thấy mẫu hóa đơn.");
        if (tpl.TInvoiceStatus != TemplateStatus.Draft) return (false, "Chỉ xóa được mẫu đang ở trạng thái chờ (PENDING).");
        if (tpl.QtyUsed > 0) return (false, "Mẫu đã dùng số hóa đơn, không thể xóa.");

        db.InvoiceTemplates.Remove(tpl);
        db.Messages.Add(new TranMessage
        {
            NntId = tpl.NntId, Type = MsgType.RegisterNnt, Dir = MsgDir.Out, Code = "300",
            Text = $"Xóa mẫu hóa đơn {tpl.FormNo} ({tpl.TInvoiceCode})"
        });
        await db.SaveChangesAsync();
        return (true, $"Đã xóa mẫu {tpl.FormNo} ({tpl.TInvoiceCode}).");
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

    // Gửi thông báo hóa đơn đã lập có sai sót tới CQT (theo Invoice_Invoice_SentTCT_300 của TVAN gốc):
    // dựng thông điệp loại 300 (mẫu 04/SS) cho một hóa đơn đã phát hành, gửi tới CQT rồi lưu lại
    // mã V tham chiếu (TCTSuaDoiRefNo), cờ thay thế/điều chỉnh TCT trả về (FlagReplaceOrAdjust = TCTBao)
    // và đánh dấu FlagSuaDoi = Sent (đã gửi thông báo, chờ TCT trả lời). Mọi lần gửi ghi nhật ký (Tct300Log).
    public async Task<(bool ok, string msg, string? tctRefNo)> SendTct300Async(
        int invoiceId, ReplaceOrAdjustFlag flagReplaceOrAdjust, string? loaiTb, string? soTb, DateTime? ngayTb, string? lyDo, string? by)
    {
        var inv = await db.Invoices.Include(i => i.Nnt).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv == null) return (false, "Không tìm thấy hóa đơn.", null);
        // Chỉ gửi thông báo sai sót cho hóa đơn đã được CQT chấp nhận phát hành (có mã tra cứu).
        if (inv.Status != InvoiceStatus.Accepted) return (false, "Chỉ gửi thông báo sai sót cho hóa đơn đã được CQT chấp nhận (ACCEPTED).", null);
        if (string.IsNullOrWhiteSpace(inv.TctCode)) return (false, "Hóa đơn thiếu mã tra cứu CQT, không thể gửi thông báo sai sót.", null);
        if (string.IsNullOrWhiteSpace(lyDo)) return (false, "Cần nhập lý do sai sót.", null);

        // Mã V tham chiếu file thông báo 300 đã gửi (mô phỏng round-trip tới CQT).
        var tctRefNo = $"V{DateTime.UtcNow:yyyyMMddHHmmss}{inv.Id:D4}";
        inv.TCTSuaDoiRefNo = tctRefNo;
        inv.FlagReplaceOrAdjust = flagReplaceOrAdjust;
        inv.FlagSuaDoi = SuaDoiFlag.Sent;

        var msg = $"Đã gửi thông báo hóa đơn sai sót (300) cho HĐ {inv.Symbol}-{inv.No}. Mã V: {tctRefNo}";
        db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300", Text = msg });
        db.Tct300Logs.Add(new Tct300Log
        {
            InvoiceId = inv.Id, TCTRefNo = tctRefNo, FlagReplaceOrAdjust = flagReplaceOrAdjust,
            LoaiTB = loaiTb, SoTB = soTb, NgayTB = ngayTb, LyDo = lyDo, Message = msg, By = by,
        });
        await db.SaveChangesAsync();
        return (true, msg, tctRefNo);
    }

    public Task<List<Tct300Log>> Tct300LogsAsync(int? invoiceId)
    {
        var q = db.Tct300Logs.Include(l => l.Invoice).AsQueryable();
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

    // Danh mục thuế suất VAT (theo Mst_VATRate của TVAN gốc):
    // danh sách thuế suất (lọc theo từ khóa mã/giá trị/mô tả nếu có).
    public Task<List<VatRate>> VatRatesAsync(string? keyword)
    {
        var q = db.VatRates.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(t => t.VATRateCode.Contains(k) || t.VATRate.Contains(k) || (t.VATDesc != null && t.VATDesc.Contains(k)));
        }
        return q.OrderBy(t => t.VATRateCode).ToListAsync();
    }

    public Task<VatRate?> GetVatRateAsync(int id) =>
        db.VatRates.FirstOrDefaultAsync(t => t.Id == id);

    // Lưu (tạo mới/cập nhật) thuế suất VAT theo khóa nghiệp vụ (OrgId, VATRateCode)
    // (theo Mst_VATRate_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã thuế suất + giá trị thuế suất;
    //  - khi tạo: mã thuế suất chưa tồn tại trong tổ chức (Mst_VATRate_CheckDB_VATRateExist).
    public async Task<(bool ok, string msg, int id)> SaveVatRateAsync(int? id, string code, string rate, string? desc, bool active, string? by)
    {
        code = (code ?? "").Trim();
        rate = (rate ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã thuế suất VAT.", 0);
        if (rate.Length == 0) return (false, "Cần giá trị thuế suất VAT.", 0);

        VatRate? e = null;
        if (id.HasValue && id.Value > 0) e = await db.VatRates.FirstOrDefaultAsync(t => t.Id == id.Value);
        else e = await db.VatRates.FirstOrDefaultAsync(t => t.VATRateCode == code);

        if (e == null)
        {
            if (await db.VatRates.AnyAsync(t => t.VATRateCode == code))
                return (false, "Mã thuế suất VAT đã tồn tại.", 0);
            e = new VatRate { VATRateCode = code };
            db.VatRates.Add(e);
        }
        else
        {
            // Đổi mã thuế suất: chặn trùng với thuế suất khác.
            if (!string.Equals(e.VATRateCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.VatRates.AnyAsync(t => t.VATRateCode == code && t.Id != e.Id))
                return (false, "Mã thuế suất VAT đã tồn tại.", 0);
            e.VATRateCode = code;
        }

        e.VATRate = rate;
        e.VATDesc = desc;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu thuế suất VAT {code} — {rate}.", e.Id);
    }

    // Xóa thuế suất VAT theo id (theo Mst_VATRate_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteVatRateAsync(int id)
    {
        var e = await db.VatRates.FirstOrDefaultAsync(t => t.Id == id);
        if (e == null) return (false, "Không tìm thấy thuế suất VAT.");
        var code = e.VATRateCode;
        db.VatRates.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa thuế suất VAT {code}.");
    }

    // Danh mục đơn vị tính (theo Mst_Unit của TVAN gốc):
    // danh sách đơn vị tính (lọc theo từ khóa mã/tên/mô tả nếu có).
    public Task<List<Unit>> UnitsAsync(string? keyword)
    {
        var q = db.Units.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(t => t.UnitCode.Contains(k) || t.UnitName.Contains(k) || (t.Remark != null && t.Remark.Contains(k)));
        }
        return q.OrderBy(t => t.UnitCode).ToListAsync();
    }

    public Task<Unit?> GetUnitAsync(int id) =>
        db.Units.FirstOrDefaultAsync(t => t.Id == id);

    // Lưu (tạo mới/cập nhật) đơn vị tính theo khóa nghiệp vụ (OrgId, UnitCode)
    // (theo Mst_Unit_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã đơn vị tính + tên đơn vị tính;
    //  - khi tạo: mã đơn vị tính chưa tồn tại trong tổ chức (Mst_Unit_CheckDB_UnitCodeExist).
    public async Task<(bool ok, string msg, int id)> SaveUnitAsync(int? id, string code, string name, string? remark, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã đơn vị tính.", 0);
        if (name.Length == 0) return (false, "Cần tên đơn vị tính.", 0);

        Unit? e = null;
        if (id.HasValue && id.Value > 0) e = await db.Units.FirstOrDefaultAsync(t => t.Id == id.Value);
        else e = await db.Units.FirstOrDefaultAsync(t => t.UnitCode == code);

        if (e == null)
        {
            if (await db.Units.AnyAsync(t => t.UnitCode == code))
                return (false, "Mã đơn vị tính đã tồn tại.", 0);
            e = new Unit { UnitCode = code };
            db.Units.Add(e);
        }
        else
        {
            // Đổi mã đơn vị tính: chặn trùng với đơn vị tính khác.
            if (!string.Equals(e.UnitCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.Units.AnyAsync(t => t.UnitCode == code && t.Id != e.Id))
                return (false, "Mã đơn vị tính đã tồn tại.", 0);
            e.UnitCode = code;
        }

        e.UnitName = name;
        e.Remark = remark;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu đơn vị tính {code} — {name}.", e.Id);
    }

    // Xóa đơn vị tính theo id (theo Mst_Unit_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteUnitAsync(int id)
    {
        var e = await db.Units.FirstOrDefaultAsync(t => t.Id == id);
        if (e == null) return (false, "Không tìm thấy đơn vị tính.");
        var code = e.UnitCode;
        db.Units.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa đơn vị tính {code}.");
    }

    // Danh mục loại khách hàng / người mua (theo Mst_CustomerNNTType của TVAN gốc):
    // danh sách loại khách hàng (lọc theo từ khóa mã/tên nếu có).
    public Task<List<CustomerNntType>> CustomerNntTypesAsync(string? keyword)
    {
        var q = db.CustomerNntTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(t => t.CustomerNNTType.Contains(k) || t.CustomerNNTTypeName.Contains(k));
        }
        return q.OrderBy(t => t.CustomerNNTType).ToListAsync();
    }

    public Task<CustomerNntType?> GetCustomerNntTypeAsync(int id) =>
        db.CustomerNntTypes.FirstOrDefaultAsync(t => t.Id == id);

    // Lưu (tạo mới/cập nhật) loại khách hàng theo khóa nghiệp vụ (OrgId, CustomerNNTType)
    // (theo Mst_CustomerNNTType_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã loại khách hàng + tên loại khách hàng;
    //  - khi tạo: mã loại khách hàng chưa tồn tại trong tổ chức (Mst_CustomerNNTType_CheckDB_CustomerNNTTypeExist).
    public async Task<(bool ok, string msg, int id)> SaveCustomerNntTypeAsync(int? id, string code, string name, string? remark, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã loại khách hàng.", 0);
        if (name.Length == 0) return (false, "Cần tên loại khách hàng.", 0);

        CustomerNntType? e = null;
        if (id.HasValue && id.Value > 0) e = await db.CustomerNntTypes.FirstOrDefaultAsync(t => t.Id == id.Value);
        else e = await db.CustomerNntTypes.FirstOrDefaultAsync(t => t.CustomerNNTType == code);

        if (e == null)
        {
            if (await db.CustomerNntTypes.AnyAsync(t => t.CustomerNNTType == code))
                return (false, "Mã loại khách hàng đã tồn tại.", 0);
            e = new CustomerNntType { CustomerNNTType = code };
            db.CustomerNntTypes.Add(e);
        }
        else
        {
            // Đổi mã loại khách hàng: chặn trùng với loại khác.
            if (!string.Equals(e.CustomerNNTType, code, StringComparison.OrdinalIgnoreCase)
                && await db.CustomerNntTypes.AnyAsync(t => t.CustomerNNTType == code && t.Id != e.Id))
                return (false, "Mã loại khách hàng đã tồn tại.", 0);
            e.CustomerNNTType = code;
        }

        e.CustomerNNTTypeName = name;
        e.Remark = remark;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu loại khách hàng {code} — {name}.", e.Id);
    }

    // Xóa loại khách hàng theo id (theo Mst_CustomerNNTType_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteCustomerNntTypeAsync(int id)
    {
        var e = await db.CustomerNntTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (e == null) return (false, "Không tìm thấy loại khách hàng.");
        var code = e.CustomerNNTType;
        db.CustomerNntTypes.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa loại khách hàng {code}.");
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

    // Danh mục Quốc gia (theo Mst_Country của TVAN gốc):
    // danh sách quốc gia (lọc theo từ khóa mã/tên nếu có).
    public Task<List<Country>> CountriesAsync(string? keyword)
    {
        var q = db.Countries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.CountryCode.Contains(k) || c.CountryName.Contains(k));
        }
        return q.OrderBy(c => c.CountryCode).ToListAsync();
    }

    public Task<Country?> GetCountryAsync(int id) =>
        db.Countries.FirstOrDefaultAsync(c => c.Id == id);

    // Lưu (tạo mới/cập nhật) quốc gia theo khóa nghiệp vụ (OrgId, CountryCode)
    // (theo Mst_Country_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã quốc gia + tên quốc gia;
    //  - khi tạo: mã quốc gia chưa tồn tại trong tổ chức (Mst_Country_CheckDB_CountryExist).
    public async Task<(bool ok, string msg, int id)> SaveCountryAsync(int? id, string code, string name, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã quốc gia.", 0);
        if (name.Length == 0) return (false, "Cần tên quốc gia.", 0);

        Country? e = null;
        if (id.HasValue && id.Value > 0) e = await db.Countries.FirstOrDefaultAsync(c => c.Id == id.Value);
        else e = await db.Countries.FirstOrDefaultAsync(c => c.CountryCode == code);

        if (e == null)
        {
            if (await db.Countries.AnyAsync(c => c.CountryCode == code))
                return (false, "Mã quốc gia đã tồn tại.", 0);
            e = new Country { CountryCode = code };
            db.Countries.Add(e);
        }
        else
        {
            // Đổi mã quốc gia: chặn trùng với quốc gia khác.
            if (!string.Equals(e.CountryCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.Countries.AnyAsync(c => c.CountryCode == code && c.Id != e.Id))
                return (false, "Mã quốc gia đã tồn tại.", 0);
            e.CountryCode = code;
        }

        e.CountryName = name;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu quốc gia {code} — {name}.", e.Id);
    }

    // Xóa quốc gia theo id (theo Mst_Country_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteCountryAsync(int id)
    {
        var e = await db.Countries.FirstOrDefaultAsync(c => c.Id == id);
        if (e == null) return (false, "Không tìm thấy quốc gia.");
        var code = e.CountryCode;
        db.Countries.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa quốc gia {code}.");
    }

    // Danh mục Đại lý (theo Mst_Dealer của TVAN gốc):
    // danh sách đại lý (lọc theo từ khóa mã/tên/điện thoại/email + tỉnh/thành nếu có).
    public Task<List<Dealer>> DealersAsync(string? keyword, string? provinceCode)
    {
        var q = db.Dealers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(provinceCode))
        {
            var p = provinceCode.Trim();
            q = q.Where(d => d.ProvinceCode == p);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(d => d.DLCode.Contains(k) || d.DLName.Contains(k)
                || (d.DLPhoneNo != null && d.DLPhoneNo.Contains(k))
                || (d.DLEmail != null && d.DLEmail.Contains(k)));
        }
        return q.OrderBy(d => d.DLCode).ToListAsync();
    }

    public Task<Dealer?> GetDealerAsync(int id) =>
        db.Dealers.FirstOrDefaultAsync(d => d.Id == id);

    // Lưu (tạo mới/cập nhật) đại lý theo khóa nghiệp vụ (OrgId, DLCode)
    // (theo Mst_Dealer_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã đại lý + tên đại lý (Mst_Dealer_Create_InvalidDLCode / _InvalidDLName);
    //  - tỉnh/thành phải tồn tại và đang dùng (Mst_Province_CheckDB);
    //  - khi tạo: mã đại lý chưa tồn tại trong tổ chức (Mst_Dealer_CheckDB_DLCodeExist).
    public async Task<(bool ok, string msg, int id)> SaveDealerAsync(int? id, string code, string name, string provinceCode, string? address, string? presentBy, string? govIdNumber, string? email, string? phone, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        provinceCode = (provinceCode ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã đại lý.", 0);
        if (name.Length == 0) return (false, "Cần tên đại lý.", 0);

        // Tỉnh/thành phải tồn tại và đang dùng (theo Mst_Province_CheckDB của TVAN gốc).
        var prov = await db.Provinces.FirstOrDefaultAsync(p => p.ProvinceCode == provinceCode);
        if (prov == null) return (false, "Tỉnh/thành phố không tồn tại.", 0);
        if (!prov.FlagActive) return (false, "Tỉnh/thành phố đã ngừng dùng.", 0);

        Dealer? e = null;
        if (id.HasValue && id.Value > 0) e = await db.Dealers.FirstOrDefaultAsync(d => d.Id == id.Value);
        else e = await db.Dealers.FirstOrDefaultAsync(d => d.DLCode == code);

        if (e == null)
        {
            if (await db.Dealers.AnyAsync(d => d.DLCode == code))
                return (false, "Mã đại lý đã tồn tại.", 0);
            e = new Dealer { DLCode = code };
            db.Dealers.Add(e);
        }
        else
        {
            // Đổi mã đại lý: chặn trùng với đại lý khác.
            if (!string.Equals(e.DLCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.Dealers.AnyAsync(d => d.DLCode == code && d.Id != e.Id))
                return (false, "Mã đại lý đã tồn tại.", 0);
            e.DLCode = code;
        }

        e.DLName = name;
        e.ProvinceCode = provinceCode;
        e.DLAddress = address;
        e.DLPresentBy = presentBy;
        e.DLGovIDNumber = govIdNumber;
        e.DLEmail = email;
        e.DLPhoneNo = phone;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu đại lý {code} — {name}.", e.Id);
    }

    // Xóa đại lý theo id (theo Mst_Dealer_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteDealerAsync(int id)
    {
        var e = await db.Dealers.FirstOrDefaultAsync(d => d.Id == id);
        if (e == null) return (false, "Không tìm thấy đại lý.");
        var code = e.DLCode;
        db.Dealers.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa đại lý {code}.");
    }

    // Danh mục Phòng ban (theo Mst_Department của TVAN gốc): danh sách phòng ban của một NNT (MST),
    // lọc theo MST + từ khóa (mã/tên).
    public Task<List<Department>> DepartmentsAsync(string? mst, string? keyword)
    {
        var q = db.Departments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(mst)) q = q.Where(d => d.MST == mst.Trim());
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(d => d.DepartmentCode.Contains(k) || d.DepartmentName.Contains(k));
        }
        return q.OrderBy(d => d.DepartmentBUCode).ThenBy(d => d.DepartmentCode).ToListAsync();
    }

    public Task<Department?> GetDepartmentAsync(int id) =>
        db.Departments.FirstOrDefaultAsync(d => d.Id == id);

    // Lưu (tạo mới/cập nhật) phòng ban theo khóa nghiệp vụ (OrgId, DepartmentCode)
    // (theo Mst_Department_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã phòng ban (Mst_Department_Create_InvalidDepartmentCode);
    //  - cần tên phòng ban (Mst_Department_Create_InvalidDepartmentName / _UpdateX_InvalidDepartmentName);
    //  - MST phải là NNT đã tồn tại và đang dùng (Mst_NNT_CheckDB);
    //  - phòng ban cha (nếu có) phải tồn tại và đang dùng (Mst_Department_CheckDB);
    //  - khi tạo: mã phòng ban chưa tồn tại (Mst_Department_CheckDB_DepartmentExist).
    // Sau khi lưu, tính lại mã đơn vị nghiệp vụ/mẫu/cấp cho toàn bộ cây (Mst_Department_UpdBU).
    public async Task<(bool ok, string msg, int id)> SaveDepartmentAsync(int? id, string code, string? codeParent, string mst, string name, bool active, string? by)
    {
        code = (code ?? "").Trim();
        codeParent = (codeParent ?? "").Trim();
        mst = (mst ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã phòng ban.", 0);
        if (name.Length == 0) return (false, "Cần tên phòng ban.", 0);

        // MST phải là NNT đã tồn tại và đang dùng (theo Mst_NNT_CheckDB của TVAN gốc).
        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == mst);
        if (nnt == null) return (false, "MST người nộp thuế không tồn tại.", 0);

        // Phòng ban cha (nếu có) phải tồn tại và đang dùng (theo Mst_Department_CheckDB của TVAN gốc).
        if (codeParent.Length > 0)
        {
            var parent = await db.Departments.FirstOrDefaultAsync(d => d.DepartmentCode == codeParent);
            if (parent == null) return (false, "Phòng ban cha không tồn tại.", 0);
            if (!parent.FlagActive) return (false, "Phòng ban cha đã ngừng dùng.", 0);
        }

        Department? e = null;
        if (id.HasValue && id.Value > 0) e = await db.Departments.FirstOrDefaultAsync(d => d.Id == id.Value);
        else e = await db.Departments.FirstOrDefaultAsync(d => d.DepartmentCode == code);

        if (e == null)
        {
            if (await db.Departments.AnyAsync(d => d.DepartmentCode == code))
                return (false, "Mã phòng ban đã tồn tại.", 0);
            e = new Department { DepartmentCode = code };
            db.Departments.Add(e);
        }
        else
        {
            // Đổi mã phòng ban: chặn trùng với phòng ban khác.
            if (!string.Equals(e.DepartmentCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.Departments.AnyAsync(d => d.DepartmentCode == code && d.Id != e.Id))
                return (false, "Mã phòng ban đã tồn tại.", 0);
            e.DepartmentCode = code;
        }

        e.DepartmentCodeParent = codeParent.Length > 0 ? codeParent : null;
        e.MST = mst;
        e.DepartmentName = name;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();

        // Tính lại mã đơn vị nghiệp vụ/mẫu/cấp cho toàn bộ cây (theo Mst_Department_UpdBU của TVAN gốc).
        await RecomputeDepartmentBuAsync();
        return (true, $"Đã lưu phòng ban {code} — {name}.", e.Id);
    }

    // Xóa phòng ban theo id (theo Mst_Department_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteDepartmentAsync(int id)
    {
        var e = await db.Departments.FirstOrDefaultAsync(d => d.Id == id);
        if (e == null) return (false, "Không tìm thấy phòng ban.");
        var code = e.DepartmentCode;
        db.Departments.Remove(e);
        await db.SaveChangesAsync();
        await RecomputeDepartmentBuAsync();
        return (true, $"Đã xóa phòng ban {code}.");
    }

    // Tính lại mã đơn vị nghiệp vụ (DepartmentBUCode), mẫu (DepartmentBUPattern) và cấp
    // (DepartmentLevel) cho toàn bộ cây phòng ban — theo Mst_Department_UpdBU của TVAN gốc:
    // phòng ban gốc 'HO' có BUCode='HO', pattern='HO%', level=1; các phòng ban khác có
    // BUCode = <BUCode cha> + '.' + <mã>, pattern = BUCode + '%', level = <level cha> + 1.
    private async Task RecomputeDepartmentBuAsync()
    {
        var all = await db.Departments.ToListAsync();
        var byCode = all.ToDictionary(d => d.DepartmentCode, StringComparer.OrdinalIgnoreCase);
        const string root = "HO";

        // Lặp tối đa 7 lần (như vòng while @nDeepDealer <= 6 của TVAN gốc) để lan truyền theo cây.
        for (int pass = 0; pass < 7; pass++)
        {
            foreach (var d in all)
            {
                if (string.Equals(d.DepartmentCode, root, StringComparison.OrdinalIgnoreCase))
                {
                    d.DepartmentBUCode = root;
                    d.DepartmentBUPattern = root + "%";
                    d.DepartmentLevel = 1;
                    continue;
                }
                Department? parent = null;
                if (!string.IsNullOrWhiteSpace(d.DepartmentCodeParent))
                    byCode.TryGetValue(d.DepartmentCodeParent!, out parent);
                var parentBu = parent?.DepartmentBUCode;
                d.DepartmentBUCode = (string.IsNullOrEmpty(parentBu) ? "" : parentBu + ".") + d.DepartmentCode;
                d.DepartmentBUPattern = d.DepartmentBUCode + "%";
                d.DepartmentLevel = (parent?.DepartmentLevel ?? 0) + 1;
            }
        }
        await db.SaveChangesAsync();
    }

    // Chứng thư số của tổ chức (theo Mst_OrgCKS của TVAN gốc):
    // danh sách chứng thư số (lọc theo từ khóa số chứng thư/tổ chức cấp/chủ thể nếu có).
    public Task<List<OrgCks>> OrgCksesAsync(string? keyword)
    {
        var q = db.OrgCkses.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.CANumber.Contains(k)
                || (c.CAOrg != null && c.CAOrg.Contains(k))
                || (c.Subject != null && c.Subject.Contains(k)));
        }
        return q.OrderBy(c => c.CANumber).ToListAsync();
    }

    public Task<OrgCks?> GetOrgCksAsync(int id) =>
        db.OrgCkses.FirstOrDefaultAsync(c => c.Id == id);

    // Lưu (tạo mới/cập nhật) chứng thư số theo khóa nghiệp vụ (OrgId, CANumber)
    // (theo Mst_OrgCKS_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần số chứng thư (CANumber);
    //  - khi tạo: số chứng thư chưa tồn tại trong tổ chức (Mst_OrgCKS_CheckDB_OrganExist);
    //  - khi sửa/xóa: chứng thư phải tồn tại (Mst_OrgCKS_CheckDB_OrganNotFound).
    public async Task<(bool ok, string msg, int id)> SaveOrgCksAsync(int? id, string caNumber, string? caOrg, string? subject, DateTime? effStart, DateTime? effEnd, string? ctsPath, string? ctsPwd, bool active, string? by)
    {
        caNumber = (caNumber ?? "").Trim();
        if (caNumber.Length == 0) return (false, "Cần số chứng thư số.", 0);

        OrgCks? e = null;
        if (id.HasValue && id.Value > 0) e = await db.OrgCkses.FirstOrDefaultAsync(c => c.Id == id.Value);
        else e = await db.OrgCkses.FirstOrDefaultAsync(c => c.CANumber == caNumber);

        if (e == null)
        {
            if (await db.OrgCkses.AnyAsync(c => c.CANumber == caNumber))
                return (false, "Số chứng thư số đã tồn tại.", 0);
            e = new OrgCks { CANumber = caNumber };
            db.OrgCkses.Add(e);
        }
        else
        {
            // Đổi số chứng thư: chặn trùng với chứng thư khác.
            if (!string.Equals(e.CANumber, caNumber, StringComparison.OrdinalIgnoreCase)
                && await db.OrgCkses.AnyAsync(c => c.CANumber == caNumber && c.Id != e.Id))
                return (false, "Số chứng thư số đã tồn tại.", 0);
            e.CANumber = caNumber;
        }

        e.CAOrg = caOrg;
        e.Subject = subject;
        e.CAEffDTimeUTCStart = effStart;
        e.CAEffDTimeUTCEnd = effEnd;
        e.CTSPath = ctsPath;
        e.CTSPwd = ctsPwd;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu chứng thư số {caNumber}.", e.Id);
    }

    // Xóa chứng thư số theo id (theo Mst_OrgCKS_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteOrgCksAsync(int id)
    {
        var e = await db.OrgCkses.FirstOrDefaultAsync(c => c.Id == id);
        if (e == null) return (false, "Không tìm thấy chứng thư số.");
        var ca = e.CANumber;
        db.OrgCkses.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa chứng thư số {ca}.");
    }

    // Loại thông báo (theo Mst_NotifyType của TVAN gốc):
    // danh sách loại thông báo (lọc theo từ khóa mã/mô tả nếu có).
    public Task<List<NotifyType>> NotifyTypesAsync(string? keyword)
    {
        var q = db.NotifyTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(t => t.NotifyTypeCode.Contains(k) || t.NotifyDesc.Contains(k));
        }
        return q.OrderBy(t => t.NotifyTypeCode).ToListAsync();
    }

    public Task<NotifyType?> GetNotifyTypeAsync(int id) =>
        db.NotifyTypes.FirstOrDefaultAsync(t => t.Id == id);

    // Lưu (tạo mới/cập nhật) loại thông báo theo khóa nghiệp vụ (OrgId, NotifyTypeCode)
    // (theo Mst_NotifyType_Create/Update của TVAN gốc). Ràng buộc:
    //  - cần mã loại thông báo (Mst_NotifyType_Create_InvalidNotifyType);
    //  - khi tạo: mã loại chưa tồn tại trong tổ chức (Mst_NotifyType_CheckDB_NotifyTypeExist);
    //  - khi sửa/xóa: loại thông báo phải tồn tại (Mst_NotifyType_CheckDB_NotifyTypeFound).
    public async Task<(bool ok, string msg, int id)> SaveNotifyTypeAsync(int? id, string code, string? desc, bool defaultActive, bool active, string? by)
    {
        code = (code ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã loại thông báo.", 0);

        NotifyType? e = null;
        if (id.HasValue && id.Value > 0) e = await db.NotifyTypes.FirstOrDefaultAsync(t => t.Id == id.Value);
        else e = await db.NotifyTypes.FirstOrDefaultAsync(t => t.NotifyTypeCode == code);

        if (e == null)
        {
            if (await db.NotifyTypes.AnyAsync(t => t.NotifyTypeCode == code))
                return (false, "Mã loại thông báo đã tồn tại.", 0);
            e = new NotifyType { NotifyTypeCode = code };
            db.NotifyTypes.Add(e);
        }
        else
        {
            // Đổi mã loại: chặn trùng với loại khác.
            if (!string.Equals(e.NotifyTypeCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.NotifyTypes.AnyAsync(t => t.NotifyTypeCode == code && t.Id != e.Id))
                return (false, "Mã loại thông báo đã tồn tại.", 0);
            e.NotifyTypeCode = code;
        }

        e.NotifyDesc = desc ?? "";
        e.DefaultActive = defaultActive;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu loại thông báo {code}.", e.Id);
    }

    // Xóa loại thông báo theo id (theo Mst_NotifyType_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteNotifyTypeAsync(int id)
    {
        var e = await db.NotifyTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (e == null) return (false, "Không tìm thấy loại thông báo.");
        var code = e.NotifyTypeCode;
        db.NotifyTypes.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa loại thông báo {code}.");
    }

    // ===== Thông báo hệ thống (theo Notify_Notify / Notify_NotifyDtl của TVAN gốc) =====

    // Danh sách thông báo (lọc theo từ khóa số thông báo / mô tả nếu có).
    public Task<List<Notify>> NotifiesAsync(string? keyword)
    {
        var q = db.Notifies.Include(n => n.Details).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(n => n.NotifyNo.Contains(k) || n.NotifyDesc.Contains(k));
        }
        return q.OrderByDescending(n => n.CreatedAt).ToListAsync();
    }

    public Task<Notify?> GetNotifyAsync(int id) =>
        db.Notifies.Include(n => n.Details).FirstOrDefaultAsync(n => n.Id == id);

    // Tạo thông báo (theo Notify_Notify_CreateX_New20200131 của TVAN gốc):
    // chặn thiếu số thông báo, chặn trùng số thông báo, chặn thiếu mô tả,
    // chặn hiệu lực bắt đầu/kết thúc rỗng, chặn hiệu lực bắt đầu sau hiệu lực kết thúc,
    // chặn hiệu lực bắt đầu/kết thúc trước ngày hệ thống.
    public async Task<(bool ok, string msg, int id)> CreateNotifyAsync(string notifyNo, string desc, DateTime effDateStart, DateTime effDateEnd, bool sendEmail, string? by)
    {
        notifyNo = (notifyNo ?? "").Trim();
        desc = (desc ?? "").Trim();
        if (string.IsNullOrWhiteSpace(notifyNo)) return (false, "Cần số thông báo.", 0);
        if (string.IsNullOrWhiteSpace(desc)) return (false, "Cần mô tả thông báo.", 0);
        if (await db.Notifies.AnyAsync(n => n.NotifyNo == notifyNo)) return (false, $"Số thông báo {notifyNo} đã tồn tại.", 0);
        var start = effDateStart.Date;
        var end = effDateEnd.Date;
        if (start == default) return (false, "Cần hiệu lực bắt đầu.", 0);
        if (end == default) return (false, "Cần hiệu lực kết thúc.", 0);
        if (start > end) return (false, "Hiệu lực bắt đầu không được sau hiệu lực kết thúc.", 0);
        if (start < DateTime.Today) return (false, "Hiệu lực bắt đầu không được trước ngày hiện tại.", 0);
        if (end < DateTime.Today) return (false, "Hiệu lực kết thúc không được trước ngày hiện tại.", 0);

        var e = new Notify
        {
            NotifyNo = notifyNo, NotifyType = NotifyScope.AllUser, NotifyType1 = NotifyKind.Maintenance,
            NotifyDesc = desc, EffDateStart = start, EffDateEnd = end, FlagSendEmail = sendEmail,
            FlagActive = true, UpdatedBy = by
        };
        db.Notifies.Add(e);
        await db.SaveChangesAsync();
        return (true, $"Đã tạo thông báo {notifyNo}.", e.Id);
    }

    // Cập nhật thông báo (theo Notify_Notify_UpdateX của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> UpdateNotifyAsync(int id, string? desc, bool sendEmail, string? by)
    {
        var e = await db.Notifies.FirstOrDefaultAsync(n => n.Id == id);
        if (e == null) return (false, "Không tìm thấy thông báo.");
        if (!string.IsNullOrWhiteSpace(desc)) e.NotifyDesc = desc.Trim();
        e.FlagSendEmail = sendEmail;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật thông báo {e.NotifyNo}.");
    }

    // Xóa thông báo (theo Notify_Notify_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteNotifyAsync(int id)
    {
        var e = await db.Notifies.FirstOrDefaultAsync(n => n.Id == id);
        if (e == null) return (false, "Không tìm thấy thông báo.");
        var no = e.NotifyNo;
        db.Notifies.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa thông báo {no}.");
    }

    // Gửi thông báo tới một người dùng (theo Notify_NotifyDtl_CreateX của TVAN gốc):
    // chặn thiếu người dùng, chặn thông báo không tồn tại, chặn gửi trùng cho cùng người dùng.
    public async Task<(bool ok, string msg, int id)> AddNotifyDtlAsync(int notifyId, string userCode, bool flagRead, string? by)
    {
        userCode = (userCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userCode)) return (false, "Cần mã người dùng nhận thông báo.", 0);
        var n = await db.Notifies.FirstOrDefaultAsync(x => x.Id == notifyId);
        if (n == null) return (false, "Không tìm thấy thông báo.", 0);
        if (await db.NotifyDtls.AnyAsync(d => d.NotifyId == notifyId && d.UserCode == userCode))
            return (false, $"Người dùng {userCode} đã nhận thông báo {n.NotifyNo}.", 0);
        var d = new NotifyDtl { NotifyId = notifyId, UserCode = userCode, FlagRead = flagRead, FlagActive = true };
        db.NotifyDtls.Add(d);
        await db.SaveChangesAsync();
        return (true, $"Đã gửi thông báo {n.NotifyNo} tới {userCode}.", d.Id);
    }

    // Đánh dấu đã đọc thông báo của một người dùng (theo Notify_NotifyDtl_UpdateX của TVAN gốc).
    public async Task<(bool ok, string msg)> MarkNotifyReadAsync(int notifyId, string userCode)
    {
        userCode = (userCode ?? "").Trim();
        var d = await db.NotifyDtls.FirstOrDefaultAsync(x => x.NotifyId == notifyId && x.UserCode == userCode);
        if (d == null) return (false, "Không tìm thấy thông báo của người dùng.");
        d.FlagRead = true;
        await db.SaveChangesAsync();
        return (true, $"Đã đánh dấu đã đọc thông báo cho {userCode}.");
    }

    // ===== Người nhận thông báo (theo Mst_ManageNotify / Map_UserInNotifyType của TVAN gốc) =====

    // Danh sách người nhận thông báo (lọc theo từ khóa mã/tên nếu có), kèm đăng ký nhận loại thông báo.
    public Task<List<NotifyRecipient>> NotifyRecipientsAsync(string? keyword)
    {
        var q = db.NotifyRecipients.Include(r => r.Types).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(r => r.UserCode.Contains(k) || r.UserName.Contains(k));
        }
        return q.OrderBy(r => r.UserCode).ToListAsync();
    }

    public Task<NotifyRecipient?> GetNotifyRecipientAsync(int id) =>
        db.NotifyRecipients.Include(r => r.Types).FirstOrDefaultAsync(r => r.Id == id);

    // Thêm người nhận thông báo (theo Mst_ManageNotify_CreateX của TVAN gốc):
    // chặn thiếu mã người dùng, chặn trùng mã người dùng; sau khi thêm, tự tạo đăng ký nhận
    // cho TẤT CẢ loại thông báo với cờ mặc định lấy từ NotifyType.DefaultActive.
    public async Task<(bool ok, string msg, int id)> CreateNotifyRecipientAsync(string userCode, string? userName, string? by)
    {
        userCode = (userCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userCode)) return (false, "Cần mã người dùng nhận thông báo.", 0);
        if (await db.NotifyRecipients.AnyAsync(r => r.UserCode == userCode))
            return (false, $"Người nhận {userCode} đã tồn tại.", 0);

        var e = new NotifyRecipient { UserCode = userCode, UserName = (userName ?? "").Trim(), UpdatedBy = by };
        db.NotifyRecipients.Add(e);
        await db.SaveChangesAsync();

        // Tự tạo đăng ký nhận cho tất cả loại thông báo (theo Mst_ManageNotify_CreateX của TVAN gốc).
        var types = await db.NotifyTypes.OrderBy(t => t.NotifyTypeCode).ToListAsync();
        foreach (var t in types)
            db.NotifyRecipientTypes.Add(new NotifyRecipientType
            {
                NotifyRecipientId = e.Id, UserCode = userCode, NotifyType = t.NotifyTypeCode,
                FlagNotify = t.DefaultActive, UpdatedBy = by
            });
        await db.SaveChangesAsync();
        return (true, $"Đã thêm người nhận thông báo {userCode}.", e.Id);
    }

    // Cập nhật tên người nhận (theo Mst_ManageNotify_UpdateX của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> UpdateNotifyRecipientAsync(int id, string? userName, string? by)
    {
        var e = await db.NotifyRecipients.FirstOrDefaultAsync(r => r.Id == id);
        if (e == null) return (false, "Không tìm thấy người nhận thông báo.");
        e.UserName = (userName ?? "").Trim();
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã cập nhật người nhận {e.UserCode}.");
    }

    // Xóa người nhận (theo Mst_ManageNotify_DeleteX của TVAN gốc): chặn khi không tồn tại;
    // xóa kèm toàn bộ đăng ký nhận loại thông báo của người này.
    public async Task<(bool ok, string msg)> DeleteNotifyRecipientAsync(int id)
    {
        var e = await db.NotifyRecipients.FirstOrDefaultAsync(r => r.Id == id);
        if (e == null) return (false, "Không tìm thấy người nhận thông báo.");
        var code = e.UserCode;
        var maps = await db.NotifyRecipientTypes.Where(t => t.NotifyRecipientId == id).ToListAsync();
        db.NotifyRecipientTypes.RemoveRange(maps);
        db.NotifyRecipients.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa người nhận {code}.");
    }

    // Lưu đăng ký nhận loại thông báo của một người nhận (theo Map_UserInNotifyType_Save của TVAN gốc):
    // thay thế toàn bộ danh sách (UserCode, NotifyType, FlagNotify) của người nhận.
    public async Task<(bool ok, string msg)> SaveNotifyRecipientTypesAsync(int id, List<(string notifyType, bool flagNotify)> types, string? by)
    {
        var e = await db.NotifyRecipients.FirstOrDefaultAsync(r => r.Id == id);
        if (e == null) return (false, "Không tìm thấy người nhận thông báo.");
        if (types == null || types.Count == 0) return (false, "Cần danh sách loại thông báo.");

        var existing = await db.NotifyRecipientTypes.Where(t => t.NotifyRecipientId == id).ToListAsync();
        db.NotifyRecipientTypes.RemoveRange(existing);
        foreach (var (notifyType, flagNotify) in types)
        {
            var nt = (notifyType ?? "").Trim();
            if (nt.Length == 0) continue;
            db.NotifyRecipientTypes.Add(new NotifyRecipientType
            {
                NotifyRecipientId = id, UserCode = e.UserCode, NotifyType = nt,
                FlagNotify = flagNotify, UpdatedBy = by
            });
        }
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu đăng ký nhận thông báo cho {e.UserCode}.");
    }

    // ===== Nhóm người dùng (theo Sys_Group / Sys_UserInGroup của TVAN gốc) =====

    // Danh sách nhóm người dùng (lọc theo từ khóa mã/tên nếu có), kèm danh sách thành viên.
    public Task<List<SysGroup>> SysGroupsAsync(string? keyword)
    {
        var q = db.SysGroups.Include(g => g.Members).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(g => g.GroupCode.Contains(k) || g.GroupName.Contains(k));
        }
        return q.OrderBy(g => g.GroupCode).ToListAsync();
    }

    public Task<SysGroup?> GetSysGroupAsync(int id) =>
        db.SysGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id);

    // Lưu (tạo mới/cập nhật) nhóm người dùng theo mã (theo Sys_Group_Create/Update của TVAN gốc):
    // lưu lần đầu = tạo nhóm mới (FlagActive = đang dùng), lưu lại cùng mã = cập nhật tên/cờ.
    // Chặn thiếu mã nhóm, chặn thiếu tên nhóm, chặn trùng mã nhóm khi tạo.
    public async Task<(bool ok, string msg, int id)> SaveSysGroupAsync(int? id, string groupCode, string groupName, bool active, string? by)
    {
        groupCode = (groupCode ?? "").Trim();
        groupName = (groupName ?? "").Trim();
        if (groupCode.Length == 0) return (false, "Cần mã nhóm người dùng.", 0);
        if (groupName.Length == 0) return (false, "Cần tên nhóm người dùng.", 0);

        SysGroup? e;
        if (id is > 0)
        {
            e = await db.SysGroups.FirstOrDefaultAsync(g => g.Id == id);
            if (e == null) return (false, "Không tìm thấy nhóm người dùng.", 0);
            if (await db.SysGroups.AnyAsync(g => g.GroupCode == groupCode && g.Id != e.Id))
                return (false, $"Mã nhóm {groupCode} đã tồn tại.", 0);
            e.GroupCode = groupCode;
            e.GroupName = groupName;
            e.FlagActive = active;
            e.UpdatedAt = DateTime.UtcNow;
            e.UpdatedBy = by;
            await db.SaveChangesAsync();
            return (true, $"Đã cập nhật nhóm người dùng {groupCode}.", e.Id);
        }

        if (await db.SysGroups.AnyAsync(g => g.GroupCode == groupCode))
            return (false, $"Mã nhóm {groupCode} đã tồn tại.", 0);
        e = new SysGroup { GroupCode = groupCode, GroupName = groupName, FlagActive = active, UpdatedBy = by };
        db.SysGroups.Add(e);
        await db.SaveChangesAsync();
        return (true, $"Đã tạo nhóm người dùng {groupCode}.", e.Id);
    }

    // Xóa nhóm người dùng theo id (theo Sys_Group_Delete của TVAN gốc): chặn khi không tồn tại;
    // xóa kèm toàn bộ phân gán người dùng của nhóm (Sys_UserInGroup_Delete_ByGroup).
    public async Task<(bool ok, string msg)> DeleteSysGroupAsync(int id)
    {
        var e = await db.SysGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (e == null) return (false, "Không tìm thấy nhóm người dùng.");
        var code = e.GroupCode;
        var members = await db.SysUserInGroups.Where(m => m.SysGroupId == id).ToListAsync();
        db.SysUserInGroups.RemoveRange(members);
        db.SysGroups.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa nhóm người dùng {code}.");
    }

    // Lưu danh sách thành viên của nhóm (theo Sys_UserInGroup_Save của TVAN gốc):
    // thay thế toàn bộ danh sách (xóa hết rồi chèn lại). Chặn khi nhóm không tồn tại.
    public async Task<(bool ok, string msg)> SaveSysGroupMembersAsync(int id, List<string> userCodes, string? by)
    {
        var e = await db.SysGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (e == null) return (false, "Không tìm thấy nhóm người dùng.");

        var existing = await db.SysUserInGroups.Where(m => m.SysGroupId == id).ToListAsync();
        db.SysUserInGroups.RemoveRange(existing);
        foreach (var raw in userCodes ?? new())
        {
            var uc = (raw ?? "").Trim();
            if (uc.Length == 0) continue;
            db.SysUserInGroups.Add(new SysUserInGroup { SysGroupId = id, GroupCode = e.GroupCode, UserCode = uc, UpdatedBy = by });
        }
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu thành viên cho nhóm {e.GroupCode}.");
    }

    // ===== Gói Module (theo Sys_Modules / Sys_Solution của TVAN gốc) =====

    // Danh sách gói Module (lọc theo từ khóa mã/tên/mô tả nếu có).
    public Task<List<SysModule>> SysModulesAsync(string? keyword)
    {
        var q = db.SysModules.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(m => m.ModuleCode.Contains(k) || m.ModuleName.Contains(k) || (m.Description != null && m.Description.Contains(k)));
        }
        return q.OrderBy(m => m.ModuleCode).ToListAsync();
    }

    public Task<SysModule?> GetSysModuleAsync(int id) =>
        db.SysModules.FirstOrDefaultAsync(m => m.Id == id);

    // Lưu (tạo mới/cập nhật) gói Module theo mã (theo Sys_Modules_Create/Update của TVAN gốc):
    // lưu lần đầu = tạo gói mới (FlagActive = đang dùng), lưu lại cùng mã = cập nhật.
    // Chặn thiếu mã gói, chặn thiếu tên gói, chặn trùng mã gói khi tạo, chặn giải pháp không tồn tại/đã ngừng.
    public async Task<(bool ok, string msg, int id)> SaveSysModuleAsync(int? id, string moduleCode, string solutionCode, string moduleName, string? description, double qtyInvoice, double valCapacity, bool active, string? by)
    {
        moduleCode = (moduleCode ?? "").Trim();
        solutionCode = (solutionCode ?? "").Trim();
        moduleName = (moduleName ?? "").Trim();
        if (moduleCode.Length == 0) return (false, "Cần mã gói Module.", 0);
        if (moduleName.Length == 0) return (false, "Cần tên gói Module.", 0);

        // Giải pháp phải tồn tại và đang dùng (theo Sys_Solution_CheckDB của TVAN gốc).
        var sol = await db.SysSolutions.FirstOrDefaultAsync(s => s.SolutionCode == solutionCode);
        if (sol == null) return (false, $"Giải pháp {solutionCode} không tồn tại.", 0);
        if (!sol.FlagActive) return (false, $"Giải pháp {solutionCode} đã ngừng dùng.", 0);

        SysModule? e;
        if (id is > 0)
        {
            e = await db.SysModules.FirstOrDefaultAsync(m => m.Id == id);
            if (e == null) return (false, "Không tìm thấy gói Module.", 0);
            if (await db.SysModules.AnyAsync(m => m.ModuleCode == moduleCode && m.Id != e.Id))
                return (false, $"Mã gói Module {moduleCode} đã tồn tại.", 0);
            e.ModuleCode = moduleCode;
            e.SolutionCode = solutionCode;
            e.ModuleName = moduleName;
            e.Description = description;
            e.QtyInvoice = qtyInvoice;
            e.ValCapacity = valCapacity;
            e.FlagActive = active;
            e.UpdatedAt = DateTime.UtcNow;
            e.UpdatedBy = by;
            await db.SaveChangesAsync();
            return (true, $"Đã cập nhật gói Module {moduleCode}.", e.Id);
        }

        if (await db.SysModules.AnyAsync(m => m.ModuleCode == moduleCode))
            return (false, $"Mã gói Module {moduleCode} đã tồn tại.", 0);
        e = new SysModule { ModuleCode = moduleCode, SolutionCode = solutionCode, ModuleName = moduleName, Description = description, QtyInvoice = qtyInvoice, ValCapacity = valCapacity, FlagActive = active, UpdatedBy = by };
        db.SysModules.Add(e);
        await db.SaveChangesAsync();
        return (true, $"Đã tạo gói Module {moduleCode}.", e.Id);
    }

    // Xóa gói Module theo id (theo Sys_Modules_Delete của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteSysModuleAsync(int id)
    {
        var e = await db.SysModules.FirstOrDefaultAsync(m => m.Id == id);
        if (e == null) return (false, "Không tìm thấy gói Module.");
        var code = e.ModuleCode;
        db.SysModules.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa gói Module {code}.");
    }

    // Bật/ngừng gói Module (theo Sys_ModulesController.ActiveModule/InactiveModule của TVAN gốc):
    // chỉ cập nhật cờ FlagActive. Chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> SetSysModuleActiveAsync(int id, bool active, string? by)
    {
        var e = await db.SysModules.FirstOrDefaultAsync(m => m.Id == id);
        if (e == null) return (false, "Không tìm thấy gói Module.");
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, active ? $"Đã bật gói Module {e.ModuleCode}." : $"Đã ngừng gói Module {e.ModuleCode}.");
    }

    // Danh sách giải pháp (lọc theo từ khóa mã/tên nếu có).
    public Task<List<SysSolution>> SysSolutionsAsync(string? keyword)
    {
        var q = db.SysSolutions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(s => s.SolutionCode.Contains(k) || s.SolutionName.Contains(k));
        }
        return q.OrderBy(s => s.SolutionCode).ToListAsync();
    }

    // ===== Đối tượng (chức năng) + phân gán vào gói Module (theo Sys_Object / Sys_ObjectInModules của TVAN gốc) =====

    // Danh sách đối tượng (lọc theo từ khóa mã/tên/dịch vụ nếu có).
    public Task<List<SysObject>> SysObjectsAsync(string? keyword)
    {
        var q = db.SysObjects.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(o => o.ObjectCode.Contains(k) || o.ObjectName.Contains(k) || (o.ServiceCode != null && o.ServiceCode.Contains(k)));
        }
        return q.OrderBy(o => o.ObjectCode).ToListAsync();
    }

    // Lưu (tạo mới/cập nhật) đối tượng theo mã (theo Sys_Object của TVAN gốc):
    // lưu lần đầu = tạo mới, lưu lại cùng mã = cập nhật. Chặn thiếu mã/tên, chặn trùng mã khi tạo.
    public async Task<(bool ok, string msg, int id)> SaveSysObjectAsync(int? id, string objectCode, string objectName, string? serviceCode, SysObjectType objectType, bool active, string? by)
    {
        objectCode = (objectCode ?? "").Trim();
        objectName = (objectName ?? "").Trim();
        if (objectCode.Length == 0) return (false, "Cần mã đối tượng.", 0);
        if (objectName.Length == 0) return (false, "Cần tên đối tượng.", 0);

        SysObject? e;
        if (id is > 0)
        {
            e = await db.SysObjects.FirstOrDefaultAsync(o => o.Id == id);
            if (e == null) return (false, "Không tìm thấy đối tượng.", 0);
            if (await db.SysObjects.AnyAsync(o => o.ObjectCode == objectCode && o.Id != e.Id))
                return (false, $"Mã đối tượng {objectCode} đã tồn tại.", 0);
            e.ObjectCode = objectCode;
            e.ObjectName = objectName;
            e.ServiceCode = serviceCode;
            e.ObjectType = objectType;
            e.FlagActive = active;
            e.UpdatedAt = DateTime.UtcNow;
            e.UpdatedBy = by;
            await db.SaveChangesAsync();
            return (true, $"Đã cập nhật đối tượng {objectCode}.", e.Id);
        }

        if (await db.SysObjects.AnyAsync(o => o.ObjectCode == objectCode))
            return (false, $"Mã đối tượng {objectCode} đã tồn tại.", 0);
        e = new SysObject { ObjectCode = objectCode, ObjectName = objectName, ServiceCode = serviceCode, ObjectType = objectType, FlagActive = active, UpdatedBy = by };
        db.SysObjects.Add(e);
        await db.SaveChangesAsync();
        return (true, $"Đã tạo đối tượng {objectCode}.", e.Id);
    }

    // Xóa đối tượng theo id (theo Sys_Object của TVAN gốc): chặn khi không tồn tại;
    // xóa kèm toàn bộ phân gán đối tượng vào gói Module.
    public async Task<(bool ok, string msg)> DeleteSysObjectAsync(int id)
    {
        var e = await db.SysObjects.FirstOrDefaultAsync(o => o.Id == id);
        if (e == null) return (false, "Không tìm thấy đối tượng.");
        var code = e.ObjectCode;
        var maps = await db.SysObjectInModules.Where(m => m.ObjectCode == code).ToListAsync();
        db.SysObjectInModules.RemoveRange(maps);
        db.SysObjects.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa đối tượng {code}.");
    }

    // Danh sách phân gán đối tượng của một gói Module (lọc theo mã gói nếu có).
    public Task<List<SysObjectInModule>> SysObjectInModulesAsync(string? moduleCode)
    {
        var q = db.SysObjectInModules.AsQueryable();
        if (!string.IsNullOrWhiteSpace(moduleCode)) q = q.Where(m => m.ModuleCode == moduleCode.Trim());
        return q.OrderBy(m => m.ModuleCode).ThenBy(m => m.ObjectCode).ToListAsync();
    }

    // Lưu danh sách đối tượng gán vào gói Module (theo Sys_ObjectInModules_Save của TVAN gốc):
    // thay thế toàn bộ danh sách (xóa hết rồi chèn lại). Chặn khi gói Module không tồn tại.
    public async Task<(bool ok, string msg)> SaveSysObjectInModulesAsync(int moduleId, List<string> objectCodes, string? by)
    {
        var m = await db.SysModules.FirstOrDefaultAsync(x => x.Id == moduleId);
        if (m == null) return (false, "Không tìm thấy gói Module.");

        var existing = await db.SysObjectInModules.Where(x => x.SysModuleId == moduleId).ToListAsync();
        db.SysObjectInModules.RemoveRange(existing);
        foreach (var raw in objectCodes ?? new())
        {
            var oc = (raw ?? "").Trim();
            if (oc.Length == 0) continue;
            db.SysObjectInModules.Add(new SysObjectInModule { SysModuleId = moduleId, ModuleCode = m.ModuleCode, ObjectCode = oc, UpdatedBy = by });
        }
        m.UpdatedAt = DateTime.UtcNow;
        m.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu đối tượng cho gói Module {m.ModuleCode}.");
    }

    // Cấu hình định dạng cột hiển thị theo bảng (theo Mst_ColumnConfig của TVAN gốc):
    // danh sách cấu hình (lọc theo bảng + từ khóa tên bảng/cột/mô tả).
    public Task<List<ColumnConfig>> ColumnConfigsAsync(string? tableName, string? keyword)
    {
        var q = db.ColumnConfigs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(tableName)) q = q.Where(c => c.TableName == tableName.Trim());
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.TableName.Contains(k) || c.ColumnName.Contains(k) || (c.ColumnDesc != null && c.ColumnDesc.Contains(k)));
        }
        return q.OrderBy(c => c.TableName).ThenBy(c => c.ColumnName).ToListAsync();
    }

    public Task<ColumnConfig?> GetColumnConfigAsync(int id) => db.ColumnConfigs.FirstOrDefaultAsync(c => c.Id == id);

    // Lưu (tạo mới/cập nhật) cấu hình cột theo khóa nghiệp vụ (TableName, ColumnName)
    // (theo Mst_ColumnConfig_Create / Mst_ColumnConfig_Update của TVAN gốc).
    public async Task<(bool ok, string msg, int id)> SaveColumnConfigAsync(int? id, string tableName, string columnName, string? columnFormat, string? columnDesc, bool active, string? by)
    {
        tableName = (tableName ?? "").Trim();
        columnName = (columnName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tableName)) return (false, "Cần tên bảng.", 0);
        if (string.IsNullOrWhiteSpace(columnName)) return (false, "Cần tên cột.", 0);

        ColumnConfig? e = null;
        if (id.HasValue) e = await db.ColumnConfigs.FirstOrDefaultAsync(c => c.Id == id.Value);
        e ??= await db.ColumnConfigs.FirstOrDefaultAsync(c => c.TableName == tableName && c.ColumnName == columnName);

        if (e == null)
        {
            e = new ColumnConfig { TableName = tableName, ColumnName = columnName };
            db.ColumnConfigs.Add(e);
        }
        e.ColumnFormat = columnFormat;
        e.ColumnDesc = columnDesc;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu cấu hình cột {tableName}.{columnName}.", e.Id);
    }

    // Xóa cấu hình cột theo id (theo Mst_ColumnConfig_Delete của TVAN gốc).
    public async Task<(bool ok, string msg)> DeleteColumnConfigAsync(int id)
    {
        var e = await db.ColumnConfigs.FirstOrDefaultAsync(c => c.Id == id);
        if (e == null) return (false, "Không tìm thấy cấu hình cột.");
        db.ColumnConfigs.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa cấu hình cột {e.TableName}.{e.ColumnName}.");
    }

    // Cấu hình cột hiển thị danh sách hóa đơn theo tổ chức (theo Mst_SortColumnInvoice của TVAN gốc):
    // danh sách cột (lọc theo từ khóa mã/tên cột), sắp theo thứ tự hiển thị (Idx).
    public Task<List<SortColumnInvoice>> SortColumnInvoicesAsync(string? keyword)
    {
        var q = db.SortColumnInvoices.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.ColumnCode.Contains(k) || c.ColumnName.Contains(k));
        }
        return q.OrderBy(c => c.Idx).ThenBy(c => c.ColumnCode).ToListAsync();
    }

    public Task<SortColumnInvoice?> GetSortColumnInvoiceAsync(int id) => db.SortColumnInvoices.FirstOrDefaultAsync(c => c.Id == id);

    // Lưu (tạo mới/cập nhật) cấu hình cột danh sách hóa đơn theo khóa nghiệp vụ (OrgId, ColumnCode)
    // (theo Mst_SortColumnInvoice_Create / Mst_SortColumnInvoice_Update của TVAN gốc).
    public async Task<(bool ok, string msg, int id)> SaveSortColumnInvoiceAsync(int? id, string columnCode, int idx, string columnName, SortColumnType columnType, bool active, string? by)
    {
        columnCode = (columnCode ?? "").Trim();
        columnName = (columnName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(columnCode)) return (false, "Cần mã cột.", 0);
        if (string.IsNullOrWhiteSpace(columnName)) return (false, "Cần tên hiển thị của cột.", 0);

        SortColumnInvoice? e = null;
        if (id.HasValue) e = await db.SortColumnInvoices.FirstOrDefaultAsync(c => c.Id == id.Value);
        e ??= await db.SortColumnInvoices.FirstOrDefaultAsync(c => c.ColumnCode == columnCode);

        if (e == null)
        {
            e = new SortColumnInvoice { ColumnCode = columnCode };
            db.SortColumnInvoices.Add(e);
        }
        e.Idx = idx;
        e.ColumnName = columnName;
        e.ColumnType = columnType;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu cấu hình cột {columnCode}.", e.Id);
    }

    // Xóa cấu hình cột danh sách hóa đơn theo id (theo Mst_SortColumnInvoice_Delete của TVAN gốc).
    public async Task<(bool ok, string msg)> DeleteSortColumnInvoiceAsync(int id)
    {
        var e = await db.SortColumnInvoices.FirstOrDefaultAsync(c => c.Id == id);
        if (e == null) return (false, "Không tìm thấy cấu hình cột.");
        db.SortColumnInvoices.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa cấu hình cột {e.ColumnCode}.");
    }

    // Danh mục tiền tệ / ngoại tệ (theo Mst_CurrencyEx của TVAN gốc): lọc theo từ khóa mã/tên.
    public Task<List<CurrencyEx>> CurrencyExesAsync(string? keyword)
    {
        var q = db.CurrencyExes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.CurrencyCode.Contains(k) || c.CurrencyName.Contains(k));
        }
        return q.OrderBy(c => c.CurrencyCode).ToListAsync();
    }

    public Task<CurrencyEx?> GetCurrencyExAsync(int id) => db.CurrencyExes.FirstOrDefaultAsync(c => c.Id == id);

    // Lưu (tạo mới/cập nhật) tiền tệ theo khóa nghiệp vụ (OrgId, CurrencyCode)
    // (theo Mst_CurrencyEx của TVAN gốc). Ràng buộc: cần mã + tên tiền tệ; khi tạo chặn trùng mã.
    public async Task<(bool ok, string msg, int id)> SaveCurrencyExAsync(int? id, string code, string name, string? baseCode, decimal buyRate, decimal sellRate, string? remark, bool active, string? by)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (code.Length == 0) return (false, "Cần mã tiền tệ.", 0);
        if (name.Length == 0) return (false, "Cần tên tiền tệ.", 0);

        CurrencyEx? e = null;
        if (id.HasValue && id.Value > 0) e = await db.CurrencyExes.FirstOrDefaultAsync(c => c.Id == id.Value);
        else e = await db.CurrencyExes.FirstOrDefaultAsync(c => c.CurrencyCode == code);

        if (e == null)
        {
            if (await db.CurrencyExes.AnyAsync(c => c.CurrencyCode == code))
                return (false, "Mã tiền tệ đã tồn tại.", 0);
            e = new CurrencyEx { CurrencyCode = code };
            db.CurrencyExes.Add(e);
        }
        else
        {
            if (!string.Equals(e.CurrencyCode, code, StringComparison.OrdinalIgnoreCase)
                && await db.CurrencyExes.AnyAsync(c => c.CurrencyCode == code && c.Id != e.Id))
                return (false, "Mã tiền tệ đã tồn tại.", 0);
            e.CurrencyCode = code;
        }

        e.CurrencyName = name;
        e.BaseCurrencyCode = string.IsNullOrWhiteSpace(baseCode) ? null : baseCode.Trim();
        e.BuyRate = buyRate;
        e.SellRate = sellRate;
        e.Remark = remark;
        e.FlagActive = active;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = by;
        await db.SaveChangesAsync();
        return (true, $"Đã lưu tiền tệ {code} — {name}.", e.Id);
    }

    // Xóa tiền tệ theo id (theo Mst_CurrencyEx của TVAN gốc): chặn khi không tồn tại.
    public async Task<(bool ok, string msg)> DeleteCurrencyExAsync(int id)
    {
        var e = await db.CurrencyExes.FirstOrDefaultAsync(c => c.Id == id);
        if (e == null) return (false, "Không tìm thấy tiền tệ.");
        var code = e.CurrencyCode;
        db.CurrencyExes.Remove(e);
        await db.SaveChangesAsync();
        return (true, $"Đã xóa tiền tệ {code}.");
    }

    // Đọc số tiền thành chữ tiếng Việt (theo luồng DocTien của TVAN gốc —
    // Invoice_InvoiceController.DocTien gọi clsDocTien.DocSo). Lấy tên tiền tệ từ danh mục
    // Mst_CurrencyEx theo mã (mặc định VND → "đồng"); mọi lần đọc ghi nhật ký đối soát.
    public async Task<(bool ok, string msg, string text, int id)> DocTienAsync(decimal amount, string? currencyCode, string? by)
    {
        var code = string.IsNullOrWhiteSpace(currencyCode) ? "VND" : currencyCode.Trim().ToUpperInvariant();
        var cur = await db.CurrencyExes.FirstOrDefaultAsync(c => c.CurrencyCode == code);
        var name = cur?.CurrencyName ?? (code == "USD" ? "đô la Mỹ" : "đồng");

        // Làm tròn 2 chữ số thập phân rồi đọc (theo clsDocTien.DocSo của TVAN gốc).
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var so = rounded.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var text = DocTienService.DocSo(so, code, name);
        if (string.IsNullOrWhiteSpace(text)) return (false, "Số tiền không hợp lệ.", "", 0);

        var log = new DocTienLog { Amount = rounded, CurrencyCode = code, CurrencyName = name, Text = text, By = by };
        db.DocTienLogs.Add(log);
        await db.SaveChangesAsync();
        return (true, text, text, log.Id);
    }

    // Nhật ký đọc tiền bằng chữ (theo luồng DocTien của TVAN gốc): mới nhất trước.
    public Task<List<DocTienLog>> DocTienLogsAsync() =>
        db.DocTienLogs.OrderByDescending(l => l.Id).ToListAsync();

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
