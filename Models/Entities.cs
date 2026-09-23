namespace MiniTVAN.Models;

public interface IOrgOwned { Guid OrgId { get; set; } }

public enum RegStatus { None = 0, Pending = 1, Registered = 2, Rejected = 3 }
// Vòng đời hóa đơn khi truyền tới cơ quan thuế
// Deleted = xóa hóa đơn đã phát hành (theo TConst.InvoiceStatus.Deleted của TVAN gốc)
// Approved = đã duyệt hóa đơn (theo TConst.InvoiceStatus.Approved của TVAN gốc): PENDING → APPROVED trước khi phát hành.
public enum InvoiceStatus { Draft = 0, Sent = 1, Accepted = 2, Rejected = 3, Cancelled = 4, Deleted = 5, Approved = 6 }
public enum MsgType { RegisterNnt = 0, SendInvoice = 1, CancelInvoice = 2, AdjustInvoice = 3, ReplaceInvoice = 4 }
public enum MsgDir { Out = 0, In = 1 }   // Out = gửi tới TCT, In = TCT phản hồi

// Nguồn gốc hóa đơn (theo TConst.SourceInvoiceCode của TVAN gốc)
public enum SourceInvoiceCode { Root = 0, Replace = 1, Adjust = 2 }
// Loại điều chỉnh (theo TConst.InvoiceAdjType của TVAN gốc)
public enum InvoiceAdjType { Normal = 0, Increase = 1, Decrease = 2 }
// Loại thao tác trên hạn mức hóa đơn (theo Invoice_licenseCreHist của TVAN gốc)
public enum LicenseHistType { Create = 0, Increase = 1, Decrease = 2 }

// Loại kỳ dữ liệu của bảng tổng hợp gửi CQT (theo TConst LKDLieu — Phụ lục VII Nghị định 123/2020)
public enum PeriodType { Day = 0, Month = 1, Quarter = 2, Year = 3 }
// Trạng thái gửi bảng tổng hợp tới CQT (theo Mst_GuiTongHopInfo.MessageStatus của TVAN gốc)
public enum GthStatus { Draft = 0, Sent = 1, Accepted = 2, Rejected = 3 }

// Kết quả tra cứu thông tin NNT theo MST từ cơ quan thuế (theo RT_EFY.status của TVAN gốc)
public enum LookupResult { Success = 0, NotFound = 1, Error = 2 }

// Kết quả gửi email hóa đơn cho người mua (theo Invoice_Invoice_Support_SendMail của TVAN gốc)
public enum EmailSendResult { Success = 0, Failed = 1 }

// Cờ in chuyển đổi của hóa đơn (theo Invoice_Invoice.FlagChange của TVAN gốc):
// NotPrinted = '1' (chưa in chuyển đổi), Printed = '0' (đã in chuyển đổi).
public enum ConversionPrintFlag { NotPrinted = 1, Printed = 0 }

// Loại thao tác trên cờ in chuyển đổi (theo Invoice_Invoice_Support_BackFlagChange của TVAN gốc)
public enum ConversionPrintAction { Print = 0, Reset = 1 }

// Cờ kiểm tra ký quá 60 ngày (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc):
// Check = bật kiểm tra (mặc định), Uncheck = bỏ kiểm tra ký >60 ngày.
public enum Sign60DayFlag { Check = 1, Uncheck = 0 }

// Kiểu dấu phân cách động khi hiển thị số trên hóa đơn (theo Mst_DynamicComma.FlagStyle của TVAN gốc):
// Comma = '0' (dùng dấu phẩy ','), Dot = '1' (dùng dấu chấm '.').
public enum DynamicCommaStyle { Comma = 0, Dot = 1 }

// Cờ đánh dấu hóa đơn đã được ký lại (theo Invoice_Invoice.FlagHotfix của TVAN gốc):
// None = chưa ký lại (FlagHotfix is null), Hotfixed = đã ký lại (FlagHotfix = '1').
public enum HotfixFlag { None = 0, Hotfixed = 1 }

// Loại thao tác duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc)
public enum ApproveAction { Approve = 0, Unapprove = 1 }

// Loại thao tác duyệt NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_ApprovedMulti của TVAN gốc):
// BulkApprove = duyệt hàng loạt danh sách HĐ đang chờ (PENDING) đã có số → APPROVED.
public enum BulkApproveAction { BulkApprove = 0 }

// Loại thao tác xóa NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_DeleteMulti của TVAN gốc):
// BulkDelete = xóa hàng loạt danh sách HĐ đã phát hành (ISSUED) đã có số → DELETED.
public enum BulkDeleteAction { BulkDelete = 0 }

// Loại thao tác hủy hóa đơn (theo Invoice_Invoice_Cancel của TVAN gốc):
// Cancel = hủy hóa đơn đang chờ/đã duyệt (PENDING/APPROVED → CANCELED).
public enum CancelAction { Cancel = 0 }

// Loại biên bản đính kèm hóa đơn (theo Invoice_Invoice.AttachedDelFileName của TVAN gốc):
// Huy = biên bản hủy hóa đơn, DieuChinh = biên bản điều chỉnh, ThayThe = biên bản thay thế.
public enum RecordType { Huy = 0, DieuChinh = 1, ThayThe = 2 }

// Loại thao tác phát hành hóa đơn (theo Invoice_Invoice_Issued của TVAN gốc):
// Issue = phát hành HĐ đã duyệt (APPROVED → ISSUED) và gửi cho khách hàng.
public enum IssueAction { Issue = 0 }

// Mã loại thông điệp CQT phản hồi khi nhận kết quả phát hành (theo Invoice_Invoice_TCTReceive của TVAN gốc):
// 202 = phát hành thành công hóa đơn có mã CQT; 204 = phát hành thất bại.
public enum TctMessageType { Success202 = 202, Fail204 = 204 }

// Trạng thái CQT chấp nhận/từ chối (theo TConst.TCTStatus của TVAN gốc: ACCEPT/REJECT)
public enum TctAcceptStatus { Accept = 0, Reject = 1 }

// Cờ xử lý thay thế/điều chỉnh của hóa đơn (theo Invoice_Invoice.FlagReplaceOrAdjust của TVAN gốc):
// Normal = 0/null hóa đơn bình thường, Replace = 1 thay thế, Adjust = 2 điều chỉnh.
// Giá trị này được TCT trả về trong thông điệp 300 (TCTBao) và lưu lại trên hóa đơn.
public enum ReplaceOrAdjustFlag { Normal = 0, Replace = 1, Adjust = 2 }

// Cờ trạng thái gửi thông báo sai sót tới TCT (theo Invoice_Invoice.FlagSuaDoi của TVAN gốc):
// Sent = 0 (đã gửi thông báo, chờ TCT trả lời), Allowed = 1 (TCT cho phép tạo HĐ thay thế/điều chỉnh),
// Error = 2 (TCT trả lỗi 204).
public enum SuaDoiFlag { Sent = 0, Allowed = 1, Error = 2 }

// Trạng thái mẫu hóa đơn (theo Invoice_TempInvoice.TInvoiceStatus của TVAN gốc):
// Draft = PENDING (chờ), SentTct = SENTTCT (đã gửi CQT, chờ CQT phát hành), Issued = ISSUED (đang sử dụng),
// Inactive = ngừng, Cancel = CANCEL (đã hủy mẫu — theo Invoice_TempInvoice_Cancel của TVAN gốc).
public enum TemplateStatus { Draft = 0, Issued = 1, Inactive = 2, SentTct = 3, Cancel = 4 }

// Loại thao tác gửi/nhận kết quả mẫu hóa đơn với CQT (theo Invoice_TempInvoice_SentTCT /
// Invoice_TempInvoice_TCTIssued của TVAN gốc).
public enum TemplateTctAction { SendTct = 0, ReceiveTct = 1 }

// Phương thức thanh toán của hóa đơn (theo Mst_PaymentMethods của TVAN gốc):
// TM = tiền mặt, CK = chuyển khoản, TM/CK = tiền mặt/chuyển khoản.
public enum PaymentMethod { Cash = 0, Transfer = 1, CashOrTransfer = 2 }

// Loại thông tư quy định cách đánh số hóa đơn (theo Invoice_TempGroup.TTType của TVAN gốc):
// TT68 = số 8 chữ số liên tục; TT78 = số 7 chữ số, reset theo năm trên mẫu số.
public enum InvoiceNoRule { TT68 = 0, TT78 = 1 }

// Loại hóa đơn theo ký tự thứ 4 của Mẫu số (FormNo) — theo Thông tư 32/2025/TT-BTC:
// ký tự C2 (vị trí thứ 4) = 'M' → hóa đơn điện tử khởi tạo từ MÁY TÍNH TIỀN (MTT).
// Hóa đơn MTT khi cấp số KHÔNG sinh mã tra cứu thông thường mà sinh "Mã của CQT trên hóa đơn MTT" (MCCQTMTT).
public enum InvoiceTypeM { Normal = 0, Machine = 1 }

// Loại thao tác mở rộng dải số của mẫu hóa đơn (theo Invoice_TempInvoice_IncreaseEndInvoiceNo /
// Invoice_TempInvoice_IncreaseQtyInvoiceNo của TVAN gốc):
// IncreaseEndNo = tăng số hóa đơn cuối (mở rộng dải số được cấp phát).
// UpdateQtyNo = cập nhật lại CẢ dải số (số bắt đầu + số kết thúc) của mẫu đang chờ
// (theo Invoice_TempInvoice_UpdQtyInvoiceNo của TVAN gốc).
public enum TemplateRangeAction { IncreaseEndNo = 0, UpdateQtyNo = 1 }

// Loại thao tác sửa lỗi hàng loạt hóa đơn theo mẫu (theo luồng Invoice_Invoice_Fix của TVAN gốc):
// FixByTemplate = ký lại hàng loạt HĐ đã phát hành (ISSUED) chưa ký lại của một mẫu hóa đơn.
public enum BulkFixAction { FixByTemplate = 0 }

// Kiểu vật lý trong DB của trường tùy chỉnh (theo Invoice_CustomField.DBPhysicalType của TVAN gốc).
// Giao diện gốc luôn gửi "TEXT" (xem invoice_CustomField.js: DBPhysicalType = "TEXT").
public enum DBPhysicalType { Text = 0, Number = 1, Date = 2 }

// Trạng thái hóa đơn trong Bảng tổng hợp hóa đơn (BTH) — theo cột TThai của
// Invoice_Invoice_BTHGetX (TVAN gốc): 0=Mới, 1=Huỷ, 2=Điều chỉnh, 3=Thay thế.
// Suy ra từ SourceInvoiceCode + InvoiceStatus:
//   ROOT + ISSUED → Moi; ROOT + DELETED → Huy;
//   REPLACE + ISSUED → ThayThe; REPLACE + DELETED → Huy;
//   ADJ + ISSUED → DieuChinh; ADJ + DELETED → Huy.
public enum TThai { Moi = 0, Huy = 1, DieuChinh = 2, ThayThe = 3 }

// Loại thuế suất của nhóm mẫu hóa đơn (theo TConst.Client_VATType của TVAN gốc):
// 1VAT = mẫu có 1 thuế suất, NVAT = mẫu nhiều thuế suất.
public enum VATType { OneVat = 0, NVat = 1 }

// Loại hàng hóa - serial của nhóm mẫu hóa đơn (theo TConst.Spec_Prd_Type của TVAN gốc):
// Spec = mẫu có cột đặc tính/serial, ProductId = mẫu theo mã sản phẩm.
public enum SpecPrdType { Spec = 0, ProductId = 1 }

public class Org
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Người nộp thuế (bên bán) — phải đăng ký với TCT trước khi phát hành HĐ.
// Hồ sơ đầy đủ theo bảng Mst_NNT của TVAN gốc: thông tin định danh, địa giới hành chính,
// đại lý giới thiệu, cơ quan thuế quản lý, người đại diện, liên hệ, chứng thư số, ngân hàng,
// loại hình/lĩnh vực/quy mô tổ chức và cây đơn vị trực thuộc (MSTParent → MSTBUCode/MSTBUPattern/MSTLevel).
public class Nnt : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Mst { get; set; } = "";            // Mã số thuế
    public string Name { get; set; } = "";           // Tên doanh nghiệp (NNTFullName)
    public string? Address { get; set; }              // Địa chỉ NNT (NNTAddress)
    public string? Email { get; set; }                // Email liên hệ (ContactEmail)
    public RegStatus RegStatus { get; set; } = RegStatus.None;
    public DateTime? RegisteredAt { get; set; }

    // Mã của CQT cấp cho Máy tính tiền (theo Mst_NNT.MCCQT của TVAN gốc):
    // chuỗi 5 ký tự do CQT cấp, dùng để sinh mã CQT trên hóa đơn khởi tạo từ máy tính tiền (MCCQTMTT).
    public string? MCCQT { get; set; }

    // Đơn vị trực thuộc (theo Mst_NNT.MSTParent của TVAN gốc): MST của NNT cấp trên (rỗng = cấp gốc).
    public string? MstParent { get; set; }
    // Mã đơn vị nghiệp vụ / mẫu / cấp — hệ thống tự tính từ cây NNT (theo Mst_NNT_UpdBU của TVAN gốc).
    public string MstBuCode { get; set; } = "";
    public string MstBuPattern { get; set; } = "";
    public int MstLevel { get; set; } = 1;

    // Địa giới hành chính (theo Mst_NNT.ProvinceCode/DistrictCode của TVAN gốc).
    public string? ProvinceCode { get; set; }
    public string? DistrictCode { get; set; }
    // Đại lý giới thiệu (theo Mst_NNT.DLCode của TVAN gốc).
    public string? DLCode { get; set; }

    // Người đại diện (theo Mst_NNT.PresentBy/NNTPosition/PresentIDNo/PresentIDType/BusinessRegNo của TVAN gốc).
    public string? PresentBy { get; set; }
    public string? NntPosition { get; set; }
    public string? PresentIdNo { get; set; }
    public string? PresentIdType { get; set; }
    public string? BusinessRegNo { get; set; }

    // Liên hệ (theo Mst_NNT.NNTMobile/NNTPhone/NNTFax/ContactName/ContactPhone/ContactEmail/Website của TVAN gốc).
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Website { get; set; }

    // Cơ quan thuế quản lý (theo Mst_NNT.GovTaxID của TVAN gốc).
    public string? GovTaxID { get; set; }

    // Chứng thư số (theo Mst_NNT.CANumber/CAOrg/CAEffDTimeUTCStart/CAEffDTimeUTCEnd của TVAN gốc).
    public string? CANumber { get; set; }
    public string? CAOrg { get; set; }
    public DateTime? CAEffDTimeUTCStart { get; set; }
    public DateTime? CAEffDTimeUTCEnd { get; set; }

    // Ngân hàng (theo Mst_NNT.AccNo/AccHolder/BankName của TVAN gốc).
    public string? AccNo { get; set; }
    public string? AccHolder { get; set; }
    public string? BankName { get; set; }

    // Loại hình / lĩnh vực / quy mô tổ chức (theo Mst_NNT.BizType/BizFieldCode/BizSizeCode của TVAN gốc).
    public string? BizType { get; set; }
    public string? BizFieldCode { get; set; }
    public string? BizSizeCode { get; set; }

    // Ghi chú (theo Mst_NNT.Remark của TVAN gốc) — dùng khi cập nhật trạng thái đăng ký.
    public string? Remark { get; set; }

    public bool FlagActive { get; set; } = true;      // Đang dùng / ngừng dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục khách hàng / người mua (theo bảng Mst_CustomerNNT của TVAN gốc):
// mỗi NNT (bên bán, xác định bởi MST) quản lý danh sách khách hàng của mình để chọn nhanh khi lập hóa đơn.
// Khóa nghiệp vụ: (OrgId, MST, CustomerNNTCode) — MST là mã số thuế của NNT sở hữu danh mục.
// CustomerMST (MST của khách hàng) là duy nhất trong phạm vi một NNT nếu có khai báo.
public class CustomerNnt : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string MST { get; set; } = "";                 // MST của NNT (bên bán) sở hữu danh mục
    public string CustomerNNTCode { get; set; } = "";      // Mã khách hàng
    public string CustomerNNTName { get; set; } = "";      // Tên khách hàng
    public string? CustomerNNTType { get; set; }            // Loại khách hàng
    public string? CustomerNNTAddress { get; set; }         // Địa chỉ
    public string? CustomerNNTEmail { get; set; }           // Email
    public string? CustomerNNTPhone { get; set; }           // Số điện thoại
    public string? CustomerNNTFax { get; set; }             // Số Fax
    public string? ContactName { get; set; }                // Tên người liên hệ
    public string? ContactPhone { get; set; }               // Số điện thoại người liên hệ
    public string? ContactEmail { get; set; }               // Email người liên hệ
    public DateTime? CustomerNNTDOB { get; set; }           // Ngày sinh
    public string? CustomerMST { get; set; }                // MST của khách hàng
    public string? ProvinceCode { get; set; }               // Mã tỉnh
    public string? DistrictCode { get; set; }               // Mã huyện
    public string? AccNo { get; set; }                      // Số tài khoản
    public string? BankName { get; set; }                   // Tên ngân hàng
    public string? GovIDType { get; set; }                  // Loại giấy tờ
    public string? GovID { get; set; }                      // Số giấy tờ
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;            // Đang dùng / ngừng dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Loại người nộp thuế (theo bảng Mst_NNTType của TVAN gốc): danh mục phân loại NNT
// (VD: Doanh nghiệp, Hộ kinh doanh, Cá nhân...) dùng để gán loại cho NNT khi đăng ký.
// Khóa nghiệp vụ: (OrgId, NNTType). FlagActive = loại đang dùng hay không.
public class NntType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string NNTType { get; set; } = "";        // Mã loại NNT
    public string NNTTypeName { get; set; } = "";    // Tên loại NNT
    public bool FlagActive { get; set; } = true;      // Loại đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Loại khách hàng / người mua (theo bảng Mst_CustomerNNTType của TVAN gốc): danh mục phân loại
// khách hàng (VD: Doanh nghiệp, Cá nhân, Tổ chức nước ngoài...) dùng để gán loại cho khách hàng
// khi khai báo danh mục khách hàng (CustomerNnt.CustomerNNTType).
// Khóa nghiệp vụ: (OrgId, CustomerNNTType). FlagActive = loại đang dùng hay không.
public class CustomerNntType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string CustomerNNTType { get; set; } = "";        // Mã loại khách hàng
    public string CustomerNNTTypeName { get; set; } = "";    // Tên loại khách hàng
    public string? Remark { get; set; }                       // Ghi chú
    public bool FlagActive { get; set; } = true;              // Loại đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục Tỉnh/Thành phố (theo bảng Mst_Province của TVAN gốc): danh mục địa giới hành chính
// cấp tỉnh dùng để chọn khi khai báo địa chỉ NNT/khách hàng (CustomerNnt.ProvinceCode).
// Khóa nghiệp vụ: (OrgId, ProvinceCode). FlagActive = tỉnh/thành đang dùng hay không.
public class Province : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string ProvinceCode { get; set; } = "";      // Mã tỉnh/thành (VD 01)
    public string ProvinceName { get; set; } = "";      // Tên tỉnh/thành (VD Hà Nội)
    public bool FlagActive { get; set; } = true;         // Tỉnh/thành đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục Quận/Huyện (theo bảng Mst_District của TVAN gốc): danh mục địa giới hành chính
// cấp quận/huyện, thuộc một tỉnh/thành (ProvinceCode), dùng để chọn khi khai báo địa chỉ
// NNT/khách hàng (CustomerNnt.DistrictCode).
// Khóa nghiệp vụ: (OrgId, ProvinceCode, DistrictCode). FlagActive = quận/huyện đang dùng hay không.
public class District : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string ProvinceCode { get; set; } = "";      // Mã tỉnh/thành mà quận/huyện thuộc về (VD 01)
    public string DistrictCode { get; set; } = "";      // Mã quận/huyện (VD 0101)
    public string DistrictName { get; set; } = "";      // Tên quận/huyện (VD Quận Ba Đình)
    public bool FlagActive { get; set; } = true;         // Quận/huyện đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục Quốc gia (theo bảng Mst_Country của TVAN gốc): danh mục quốc tịch/quốc gia
// dùng khi khai báo thông tin NNT/khách hàng nước ngoài.
// Khóa nghiệp vụ: (OrgId, CountryCode). FlagActive = quốc gia đang dùng hay không.
public class Country : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string CountryCode { get; set; } = "";      // Mã quốc gia (VD VN)
    public string CountryName { get; set; } = "";      // Tên quốc gia (VD Việt Nam)
    public bool FlagActive { get; set; } = true;         // Quốc gia đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục Đại lý (theo bảng Mst_Dealer của TVAN gốc): đại lý phân phối/giới thiệu khách hàng
// cho NNT, gắn với một tỉnh/thành (ProvinceCode). Dùng để quản lý mạng lưới đại lý.
// Khóa nghiệp vụ: (OrgId, DLCode). FlagActive = đại lý đang hoạt động hay không.
public class Dealer : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string DLCode { get; set; } = "";            // Mã đại lý (VD DL001)
    public string DLName { get; set; } = "";            // Tên đại lý
    public string ProvinceCode { get; set; } = "";      // Mã tỉnh/thành của đại lý
    public string? DLAddress { get; set; }               // Địa chỉ
    public string? DLPresentBy { get; set; }             // Người đại diện
    public string? DLGovIDNumber { get; set; }           // Số CMT/Hộ chiếu người đại diện
    public string? DLEmail { get; set; }                 // Email
    public string? DLPhoneNo { get; set; }               // Điện thoại
    public bool FlagActive { get; set; } = true;         // Đại lý đang hoạt động
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục Phòng ban (theo bảng Mst_Department của TVAN gốc): cây phòng ban của một NNT (MST),
// mỗi phòng ban có phòng ban cha (DepartmentCodeParent) tạo thành cấu trúc phân cấp.
// Hệ thống tự tính mã đơn vị nghiệp vụ (DepartmentBUCode), mẫu (DepartmentBUPattern) và cấp
// (DepartmentLevel) từ cây phòng ban — theo Mst_Department_UpdBU của TVAN gốc.
// Khóa nghiệp vụ: (OrgId, DepartmentCode). FlagActive = phòng ban đang dùng hay không.
public class Department : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string DepartmentCode { get; set; } = "";        // Mã phòng ban (VD HO, KT, KT1)
    public string? DepartmentCodeParent { get; set; }         // Mã phòng ban cha (rỗng = cấp gốc)
    public string DepartmentBUCode { get; set; } = "";       // Mã đơn vị nghiệp vụ (tự tính từ cây)
    public string DepartmentBUPattern { get; set; } = "";    // Mẫu đơn vị nghiệp vụ (tự tính từ cây)
    public int DepartmentLevel { get; set; } = 1;            // Cấp phòng ban (tự tính từ cây)
    public string MST { get; set; } = "";                    // MST người nộp thuế sở hữu phòng ban
    public string DepartmentName { get; set; } = "";         // Tên phòng ban
    public bool FlagActive { get; set; } = true;             // Phòng ban đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class Invoice : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int NntId { get; set; }
    public Nnt? Nnt { get; set; }
    public string Symbol { get; set; } = "";         // Ký hiệu (VD 1C26TAA)
    public string No { get; set; } = "";             // Số hóa đơn
    public string BuyerName { get; set; } = "";
    public string? BuyerMst { get; set; }
    public string? BuyerAddress { get; set; }
    public decimal Amount { get; set; }              // Tiền hàng
    public decimal VatRate { get; set; } = 10;       // %
    public DateTime IssuedDate { get; set; } = DateTime.Today;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? TctCode { get; set; }             // Mã CQT cấp (mã tra cứu) — GLOBAL unique khi Accepted
    public string? RejectReason { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Xử lý hóa đơn sai sót: gốc / thay thế / điều chỉnh
    public SourceInvoiceCode SourceCode { get; set; } = SourceInvoiceCode.Root;
    public InvoiceAdjType AdjType { get; set; } = InvoiceAdjType.Normal;
    public int? RefInvoiceId { get; set; }           // HĐ gốc bị thay thế/điều chỉnh
    public Invoice? RefInvoice { get; set; }
    public string? RefTctCode { get; set; }          // Số tra cứu HĐ gốc (RefNo)
    public string? AdjReason { get; set; }           // Lý do điều chỉnh/thay thế

    // Gửi email hóa đơn cho người mua (theo Invoice_Invoice của TVAN gốc: EmailSend/SendEmailDTimeUTC/SendEmailBy)
    public string? EmailSend { get; set; }           // Email người nhận (nhiều địa chỉ cách nhau bởi ';')
    public DateTime? SendEmailDTimeUTC { get; set; } // Thời điểm gửi email gần nhất
    public string? SendEmailBy { get; set; }         // Người gửi email

    // Cờ in chuyển đổi (theo Invoice_Invoice.FlagChange của TVAN gốc):
    // NotPrinted = chưa in chuyển đổi (mặc định), Printed = đã in chuyển đổi.
    public ConversionPrintFlag FlagChange { get; set; } = ConversionPrintFlag.NotPrinted;

    // Ngày ký hóa đơn (theo Invoice_Invoice.SignedDate của TVAN gốc) — dùng cho kiểm tra ký quá 60 ngày.
    public DateTime? SignedDate { get; set; }

    // Ký lại hóa đơn (theo Invoice_Invoice_ReSign của TVAN gốc):
    // InvoiceFileSpec = nội dung hóa đơn đã ký (base64 XML), InvoiceFilePath = đường dẫn file XML đã lưu,
    // FlagHotfix = '1' khi đã ký lại, ApprDTimeUTC/ApprBy = thời điểm & người duyệt (ký lại).
    public string? InvoiceFileSpec { get; set; }
    public string? InvoiceFilePath { get; set; }
    public HotfixFlag FlagHotfix { get; set; } = HotfixFlag.None;
    public DateTime? ApprDTimeUTC { get; set; }
    public string? ApprBy { get; set; }

    // Xóa hóa đơn đã phát hành (theo Invoice_Invoice_Deleted của TVAN gốc):
    // DeleteDTimeUTC/DeleteBy = thời điểm & người xóa; Remark = lý do xóa.
    public DateTime? DeleteDTimeUTC { get; set; }
    public string? DeleteBy { get; set; }
    public string? Remark { get; set; }

    // Duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc):
    // InvoiceFilePath/InvoicePDFFilePath = đường dẫn file XML/PDF hóa đơn đã duyệt;
    // ApprDTimeUTC/ApprBy = thời điểm & người duyệt (dùng chung với ký lại).
    public string? InvoicePDFFilePath { get; set; }

    // Phát hành hóa đơn (theo Invoice_Invoice_Issued của TVAN gốc):
    // IssuedDTimeUTC/IssuedBy = thời điểm & người phát hành HĐ (APPROVED → ISSUED).
    public DateTime? IssuedDTimeUTC { get; set; }
    public string? IssuedBy { get; set; }

    // Cấp phát số hóa đơn (theo Invoice_Invoice_AllocatedInv của TVAN gốc):
    // InvoiceNoDTimeUTC/InvoiceNoBy = thời điểm & người ấn cấp số hóa đơn.
    public DateTime? InvoiceNoDTimeUTC { get; set; }
    public string? InvoiceNoBy { get; set; }

    // Hủy hóa đơn (theo Invoice_Invoice_Cancel của TVAN gốc):
    // CancelDTimeUTC/CancelBy = thời điểm & người hủy hóa đơn (PENDING/APPROVED → CANCELED).
    public DateTime? CancelDTimeUTC { get; set; }
    public string? CancelBy { get; set; }

    // Nhận kết quả phản hồi từ CQT (theo Invoice_Invoice_TCTReceive của TVAN gốc):
    // MltDiep = mã loại thông điệp CQT trả về (202/204); TctChapNhan = CQT chấp nhận/từ chối;
    // TctMaLoi = mã lỗi CQT; TctLyDo = lý do CQT; TctReceiveDTimeUTC = thời điểm nhận kết quả.
    public string? MltDiep { get; set; }
    public TctAcceptStatus? TctChapNhan { get; set; }
    public string? TctMaLoi { get; set; }
    public string? TctLyDo { get; set; }
    public DateTime? TctReceiveDTimeUTC { get; set; }

    // Gửi thông báo hóa đơn sai sót tới CQT (theo Invoice_Invoice_SentTCT_300 của TVAN gốc):
    // TCTSuaDoiRefNo = mã V tham chiếu file thông báo 300 đã gửi; FlagReplaceOrAdjust = cờ thay thế/điều chỉnh
    // do TCT trả về (TCTBao); FlagSuaDoi = trạng thái gửi thông báo sai sót (0 đã gửi, 1 TCT cho phép, 2 lỗi).
    public string? TCTSuaDoiRefNo { get; set; }
    public ReplaceOrAdjustFlag FlagReplaceOrAdjust { get; set; } = ReplaceOrAdjustFlag.Normal;
    public SuaDoiFlag? FlagSuaDoi { get; set; }

    // Phương thức thanh toán (theo Invoice_Invoice.PaymentMethodCode của TVAN gốc):
    // dùng cho cập nhật nội dung hóa đơn sau khi đã cấp số (Invoice_Invoice_UpdAfterAllocated).
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOrTransfer;

    // Mã của CQT trên hóa đơn khởi tạo từ máy tính tiền (theo Invoice_Invoice.MCCQTMTT của TVAN gốc):
    // sinh khi cấp số cho hóa đơn loại MTT (FormNo có ký tự thứ 4 = 'M'), định dạng M<C2>-<yy>-<MCCQT>-<MMdd><seq7>.
    public string? MCCQTMTT { get; set; }

    // Biên bản đính kèm hóa đơn (theo Invoice_Invoice.AttachedDelFileName/AttachedDelFileSpec/
    // AttachedDelFilePath/DeleteReason của TVAN gốc — luồng TaoBienBan):
    // AttachedDelFileName = tên file biên bản, AttachedDelFileSpec = nội dung file (base64),
    // AttachedDelFilePath = đường dẫn file đã lưu, DeleteReason = lý do hủy/điều chỉnh/thay thế.
    public string? AttachedDelFileName { get; set; }
    public string? AttachedDelFileSpec { get; set; }
    public string? AttachedDelFilePath { get; set; }
    public string? DeleteReason { get; set; }

    public decimal VatAmount => Math.Round(Amount * VatRate / 100m, 0);
    public decimal Total => Amount + VatAmount;
}

// Hạn mức hóa đơn (theo bảng Invoice_license của TVAN gốc): số lượng HĐ tối đa NNT được phát hành.
// TotalQty = hạn mức được cấp; TotalQtyIssued = đã phát hành (ISSUED/CANCEL); TotalQtyUsed = đã sử dụng.
public class InvoiceLicense : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int NntId { get; set; }
    public Nnt? Nnt { get; set; }
    public int TotalQty { get; set; }                // Hạn mức tổng được cấp
    public int TotalQtyIssued { get; set; }          // Đã phát hành (tính cả HĐ đã hủy)
    public int TotalQtyUsed { get; set; }            // Đã sử dụng (HĐ còn hiệu lực)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int Remaining => TotalQty - TotalQtyIssued;
}

// Lịch sử cấp/điều chỉnh hạn mức (theo bảng Invoice_licenseCreHist của TVAN gốc)
public class LicenseHist : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int NntId { get; set; }
    public LicenseHistType Type { get; set; }
    public int Qty { get; set; }                     // Số lượng thay đổi (dương)
    public int TotalQtyAfter { get; set; }           // Hạn mức tổng sau thay đổi
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Bảng tổng hợp dữ liệu hóa đơn điện tử gửi CQT (theo bảng Mst_GuiTongHop của TVAN gốc).
// NNT lập bảng tổng hợp theo kỳ (ngày/tháng/quý/năm) rồi gửi tới cơ quan thuế.
public class GuiTongHop : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int NntId { get; set; }
    public Nnt? Nnt { get; set; }
    public PeriodType LKDLieu { get; set; } = PeriodType.Month;   // Loại kỳ dữ liệu
    public string KDLieu { get; set; } = "";                       // Kỳ dữ liệu (VD 2026-06)
    public int BSLThu { get; set; }                                // Bổ sung lần thứ (0 = lần đầu)
    public bool LDau { get; set; } = true;                         // Lần đầu
    public string MSo { get; set; } = "01/THĐĐT";                  // Mẫu số bảng tổng hợp
    public string Ten { get; set; } = "Bảng tổng hợp dữ liệu hóa đơn điện tử";
    public string SBTHDLieu { get; set; } = "";                    // Số bảng tổng hợp dữ liệu
    public string TNNT { get; set; } = "";                         // Tên NNT
    public string MST { get; set; } = "";                          // MST NNT
    public DateTime NLap { get; set; } = DateTime.Today;           // Ngày lập
    public GthStatus Status { get; set; } = GthStatus.Draft;
    public string? MessageSentCode { get; set; }                   // Mã thông điệp gửi
    public string? MessageReplyCode { get; set; }                  // Mã thông điệp CQT trả về
    public string? Remark { get; set; }                            // Ghi chú / lý do từ chối
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<GuiTongHopDtl> Details { get; set; } = new();
    public decimal TotalAmount => Details.Sum(d => d.TTCThue);
    public decimal TotalVat => Details.Sum(d => d.TgTThue);
    public decimal TotalPayment => Details.Sum(d => d.TgTTToan);
}

// Dòng chi tiết bảng tổng hợp (theo bảng Mst_GuiTongHopDtl của TVAN gốc)
public class GuiTongHopDtl : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int GuiTongHopId { get; set; }
    public int STT { get; set; }                                   // Số thứ tự
    public string InvoiceCode { get; set; } = "";                  // Mã tra cứu hóa đơn
    public string KHMSHDon { get; set; } = "";                     // Ký hiệu mẫu số hóa đơn
    public string KHHDon { get; set; } = "";                       // Ký hiệu hóa đơn
    public string SHDon { get; set; } = "";                        // Số hóa đơn
    public DateTime NLap { get; set; } = DateTime.Today;           // Ngày lập
    public string TNMua { get; set; } = "";                        // Tên người mua
    public string? MSTNMua { get; set; }                           // MST người mua
    public string? THHDVu { get; set; }                            // Tên hàng hóa, dịch vụ
    public string? DVTinh { get; set; }                            // Đơn vị tính
    public decimal SLuong { get; set; }                            // Số lượng
    public decimal TTCThue { get; set; }                           // Tổng giá trị chưa thuế
    public decimal TSuat { get; set; }                             // Thuế suất
    public decimal TgTThue { get; set; }                           // Tổng tiền thuế
    public decimal TgTTToan { get; set; }                          // Tổng tiền thanh toán
    public string? GChu { get; set; }                              // Ghi chú
}

// Cơ quan thuế quản lý (theo bảng Mst_GovTaxID của TVAN gốc): danh mục CQT theo mã (GovTaxID).
// Dùng để tra cứu "CQT quản lý" của một MST người nộp thuế. CQT có cấu trúc phân cấp
// (GovTaxIDParent) và gắn với địa giới hành chính (ProvinceCode/DistrictCode).
// Hệ thống tự tính mã đơn vị nghiệp vụ (GovTaxIDBUCode), mẫu (GovTaxIDBUPattern) và cấp
// (GovTaxIDLevel) từ cây CQT — theo Mst_GovTaxID_UpdBU của TVAN gốc.
// Khóa nghiệp vụ: (OrgId, GovTaxID). FlagActive = CQT đang dùng hay không.
public class TaxOffice : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string GovTaxID { get; set; } = "";        // Mã cơ quan thuế
    public string? GovTaxIDParent { get; set; }        // Mã CQT cấp trên (rỗng = cấp gốc)
    public string GovTaxIDBUCode { get; set; } = "";  // Mã đơn vị nghiệp vụ (tự tính từ cây)
    public string GovTaxIDBUPattern { get; set; } = ""; // Mẫu đơn vị nghiệp vụ (tự tính từ cây)
    public int GovTaxIDLevel { get; set; }             // Cấp CQT (tự tính từ cây)
    public string? ProvinceCode { get; set; }          // Mã tỉnh/thành
    public string? DistrictCode { get; set; }          // Mã quận/huyện
    public string GovTaxName { get; set; } = "";      // Tên cơ quan thuế
    public string? Level { get; set; }                 // Cấp (nhập tay, theo Mst_GovTaxID.Level)
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Nhật ký tra cứu thông tin NNT theo MST từ cơ quan thuế (theo TCT_TraTTinMaSoThue của TVAN gốc).
// Mỗi lần tra cứu ghi lại kết quả (tìm thấy/không tìm thấy/lỗi) để đối soát.
public class NntLookupLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Mst { get; set; } = "";             // MST tra cứu
    public LookupResult Result { get; set; } = LookupResult.Success;
    public string? FullName { get; set; }              // Tên NNT tìm được
    public string? Address { get; set; }               // Địa chỉ NNT
    public string? GovTaxID { get; set; }              // CQT quản lý
    public string? GovTaxName { get; set; }            // Tên CQT quản lý
    public string? Message { get; set; }               // Thông báo kết quả
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký gửi email hóa đơn cho người mua (theo Invoice_Invoice_Support_SendMail của TVAN gốc).
// Mỗi lần gửi/gửi lại email hóa đơn đã phát hành ghi lại kết quả để đối soát.
public class InvoiceEmailLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string ToEmail { get; set; } = "";        // Địa chỉ nhận
    public string? Subject { get; set; }              // Tiêu đề email
    public EmailSendResult Result { get; set; } = EmailSendResult.Success;
    public string? Message { get; set; }              // Thông báo kết quả
    public string? SentBy { get; set; }               // Người gửi
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký in chuyển đổi hóa đơn (theo Invoice_Invoice_Support_BackFlagChange của TVAN gốc).
// Mỗi lần in chuyển đổi hoặc bỏ cờ in chuyển đổi ghi lại để đối soát.
public class ConversionPrintLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public ConversionPrintAction Action { get; set; }   // Print = in chuyển đổi, Reset = bỏ cờ in chuyển đổi
    public string? Note { get; set; }                  // Ghi chú / lý do
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc).
// Mỗi lần duyệt (PENDING → APPROVED) hoặc bỏ duyệt (APPROVED → PENDING) ghi lại để đối soát.
public class ApproveLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public ApproveAction Action { get; set; }          // Approve = duyệt, Unapprove = bỏ duyệt
    public string? FilePath { get; set; }              // Đường dẫn file XML hóa đơn đã duyệt
    public string? PdfFilePath { get; set; }           // Đường dẫn file PDF hóa đơn đã duyệt
    public string? Note { get; set; }                  // Ghi chú / lý do
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký duyệt NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_ApprovedMulti của TVAN gốc).
// Mỗi lần duyệt hàng loạt danh sách HĐ đang chờ (PENDING) đã có số ghi lại để đối soát:
// số lượng HĐ đã duyệt, danh sách số hóa đơn, ghi chú và người thực hiện.
public class BulkApproveLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public BulkApproveAction Action { get; set; } = BulkApproveAction.BulkApprove;   // Loại thao tác duyệt hàng loạt
    public int ApprovedCount { get; set; }             // Số hóa đơn đã duyệt
    public string? InvoiceNos { get; set; }            // Danh sách số hóa đơn đã duyệt (cách nhau bởi dấu phẩy)
    public string? Note { get; set; }                  // Ghi chú / lý do
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký xóa NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_DeleteMulti của TVAN gốc).
// Mỗi lần xóa hàng loạt danh sách HĐ đã phát hành (ISSUED) đã có số ghi lại để đối soát:
// số lượng HĐ đã xóa, danh sách số hóa đơn, lý do và người thực hiện.
public class BulkDeleteLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public BulkDeleteAction Action { get; set; } = BulkDeleteAction.BulkDelete;   // Loại thao tác xóa hàng loạt
    public int DeletedCount { get; set; }              // Số hóa đơn đã xóa
    public string? InvoiceNos { get; set; }            // Danh sách số hóa đơn đã xóa (cách nhau bởi dấu phẩy)
    public string? Note { get; set; }                  // Ghi chú / lý do
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
// Mỗi lần ký lại hóa đơn đã phát hành ghi lại để đối soát.
public class ReSignLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string? FilePath { get; set; }              // Đường dẫn file XML đã ký lại
    public string? Note { get; set; }                  // Ghi chú / lý do ký lại
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Cấu hình hệ thống theo tổ chức (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc).
// Sign60Day = Check: bật kiểm tra ký quá 60 ngày khi truyền HĐ; Uncheck: bỏ kiểm tra.
public class SystemSetting : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public Sign60DayFlag Sign60Day { get; set; } = Sign60DayFlag.Check;
    public string? Note { get; set; }                 // Ghi chú / người thay đổi
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Cấu hình dấu phân cách động theo tổ chức (theo bảng Mst_DynamicComma của TVAN gốc):
// mỗi tổ chức có một bản ghi quy định kiểu dấu phân cách khi hiển thị số trên hóa đơn
// (FlagStyle = '0' dùng dấu phẩy ','; '1' dùng dấu chấm '.').
// Khóa nghiệp vụ: OrgId (mỗi tổ chức một cấu hình).
public class DynamicComma : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public DynamicCommaStyle FlagStyle { get; set; } = DynamicCommaStyle.Comma;   // 0 = dấu ',', 1 = dấu '.'
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Mẫu hóa đơn (theo bảng Invoice_TempInvoice của TVAN gốc): mỗi NNT đăng ký một mẫu với dải số
// được cấp phát (StartInvoiceNo..EndInvoiceNo). Hệ thống cấp số tuần tự từ mẫu này khi phát hành HĐ.
public class InvoiceTemplate : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int NntId { get; set; }
    public Nnt? Nnt { get; set; }
    public string TInvoiceCode { get; set; } = "";        // Mã mẫu
    public string TInvoiceName { get; set; } = "";        // Tên mẫu
    public string FormNo { get; set; } = "";              // Mẫu số (VD 1C26TAA)
    public string Sign { get; set; } = "";                // Ký hiệu
    public InvoiceNoRule TTType { get; set; } = InvoiceNoRule.TT78;   // Loại thông tư (cách đánh số)
    public DateTime EffDateStart { get; set; } = DateTime.Today;      // Ngày bắt đầu sử dụng
    public DateTime? EffDateEnd { get; set; }             // Ngày kết thúc
    public int StartInvoiceNo { get; set; }               // Số bắt đầu
    public int EndInvoiceNo { get; set; }                 // Số kết thúc
    public string? LastInvoiceNo { get; set; }            // Số hóa đơn cuối đã cấp
    public int QtyUsed { get; set; }                      // Số lượng đã sử dụng
    public DateTime? LastInvoiceDateUTC { get; set; }     // Ngày hóa đơn cuối được cấp số
    public TemplateStatus TInvoiceStatus { get; set; } = TemplateStatus.Issued;
    public bool FlagActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Gửi mẫu hóa đơn tới CQT (theo Invoice_TempInvoice_SentTCT của TVAN gốc):
    // TCTRefNo = mã V tham chiếu file đã gửi, TCTMessage = thông tin CQT trả về (mã lỗi/ghi chú),
    // SentTCTDTime/SentTCTBy = thời điểm & người gửi mẫu tới CQT.
    public string? TCTRefNo { get; set; }
    public string? TCTMessage { get; set; }
    public DateTime? SentTCTDTime { get; set; }
    public string? SentTCTBy { get; set; }

    // CQT phát hành mẫu (theo Invoice_TempInvoice_TCTIssued của TVAN gốc):
    // TCTChapNhan = CQT chấp nhận/từ chối, TCTChapNhanDTime = thời điểm CQT phản hồi.
    public TctAcceptStatus? TCTChapNhan { get; set; }
    public DateTime? TCTChapNhanDTime { get; set; }

    // Thông tin liên hệ của NNT in trên mẫu hóa đơn (theo Invoice_TempInvoice_SupportUpdEmailAndAddress
    // của TVAN gốc): tên đơn vị, địa chỉ, điện thoại, email, website hiển thị trên hóa đơn phát hành.
    public string? NNTName { get; set; }
    public string? NNTAddress { get; set; }
    public string? NNTPhone { get; set; }
    public string? NNTEmail { get; set; }
    public string? NNTWebsite { get; set; }
    // Cấu hình dấu phân cách động (theo Invoice_TempInvoice.FlagStyleComma của TVAN gốc):
    // true = dùng dấu ',' động khi hiển thị số trên hóa đơn.
    public bool FlagStyleComma { get; set; }
    // Thời điểm & người cập nhật thông tin liên hệ gần nhất (theo LogLUDTimeUTC/LogLUBy của TVAN gốc).
    public DateTime? ContactUpdatedAt { get; set; }
    public string? ContactUpdatedBy { get; set; }

    // Số tài khoản & tên ngân hàng của NNT in trên mẫu hóa đơn
    // (theo Invoice_TempInvoice.NNTAccNo/NNTBankName của TVAN gốc — luồng Invoice_TempInvoice_SupportUpdAccNoAndBankName).
    public string? NNTAccNo { get; set; }
    public string? NNTBankName { get; set; }

    // Hủy mẫu hóa đơn (theo Invoice_TempInvoice_Cancel của TVAN gốc):
    // CancelDTimeUTC/CancelBy = thời điểm & người hủy mẫu; QtyCancel = số lượng hủy (số hóa đơn còn lại chưa dùng).
    public DateTime? CancelDTimeUTC { get; set; }
    public string? CancelBy { get; set; }
    public int QtyCancel { get; set; }

    public int QtyRemain => EndInvoiceNo - StartInvoiceNo + 1 - QtyUsed;
}

// Nhật ký gửi/nhận kết quả mẫu hóa đơn với CQT (theo Invoice_TempInvoice_SentTCT /
// Invoice_TempInvoice_TCTIssued của TVAN gốc). Mỗi lần gửi mẫu tới CQT hoặc nhận kết quả
// phát hành mẫu từ CQT ghi lại để đối soát.
public class TemplateTctLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int TemplateId { get; set; }
    public InvoiceTemplate? Template { get; set; }
    public TemplateTctAction Action { get; set; } = TemplateTctAction.SendTct;   // Gửi CQT / Nhận KQ CQT
    public string? TCTRefNo { get; set; }              // Mã V tham chiếu file đã gửi
    public TctAcceptStatus? ChapNhan { get; set; }     // CQT chấp nhận/từ chối (khi nhận KQ)
    public string? Message { get; set; }               // Thông báo kết quả
    public string? Remark { get; set; }                // Ghi chú / lý do
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký cấp phát số hóa đơn (theo Invoice_Invoice_AllocatedInv của TVAN gốc).
// Mỗi lần ấn cấp số cho một hóa đơn ghi lại để đối soát.
public class InvoiceNoAllocLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int TemplateId { get; set; }
    public string? FormNo { get; set; }                   // Mẫu số tại thời điểm cấp
    public string? Sign { get; set; }                     // Ký hiệu tại thời điểm cấp
    public string InvoiceNo { get; set; } = "";           // Số hóa đơn được cấp
    public DateTime InvoiceDate { get; set; } = DateTime.Today;   // Ngày hóa đơn
    public string? By { get; set; }                       // Người ấn cấp số
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký nhận kết quả phản hồi từ CQT cho hóa đơn đã gửi (theo Invoice_Invoice_TCTReceive của TVAN gốc).
// Mỗi lần CQT trả kết quả (202 phát hành thành công / 204 phát hành thất bại) ghi lại để đối soát.
public class TctReceiveLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public TctMessageType MltDiep { get; set; }        // Mã loại thông điệp CQT trả về (202/204)
    public TctAcceptStatus ChapNhan { get; set; }      // CQT chấp nhận/từ chối
    public string? MaCQT { get; set; }                 // Mã xác thực CQT (InvoiceVerifyCQTCode)
    public string? MaLoi { get; set; }                 // Mã lỗi CQT
    public string? LyDo { get; set; }                  // Lý do CQT
    public string? Message { get; set; }               // Thông báo kết quả
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký gửi thông báo hóa đơn sai sót tới CQT (theo Invoice_Invoice_SentTCT_300 của TVAN gốc).
// Mỗi lần gửi thông điệp 300 (04/SS — thông báo hóa đơn đã lập có sai sót) ghi lại để đối soát:
// mã V tham chiếu file đã gửi, cờ thay thế/điều chỉnh TCT trả về, loại thông báo, số/ngày thông báo CQT.
public class Tct300Log : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string? TCTRefNo { get; set; }              // Mã V tham chiếu file thông báo 300 đã gửi
    public ReplaceOrAdjustFlag FlagReplaceOrAdjust { get; set; } = ReplaceOrAdjustFlag.Normal;  // Cờ TCT trả về (TCTBao)
    public string? LoaiTB { get; set; }                // Loại thông báo (Loai)
    public string? SoTB { get; set; }                  // Số thông báo CQT (So)
    public DateTime? NgayTB { get; set; }              // Ngày thông báo CQT (NTBCCQT)
    public string? LyDo { get; set; }                  // Lý do sai sót
    public string? Message { get; set; }               // Thông báo kết quả
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký thông điệp trao đổi với TCT
public class TranMessage : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int? InvoiceId { get; set; }
    public int? NntId { get; set; }
    public MsgType Type { get; set; }
    public MsgDir Dir { get; set; }
    public string? Code { get; set; }                // Mã kết quả TCT: 202/204/301...
    public string? Text { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký mở rộng dải số của mẫu hóa đơn (theo Invoice_TempInvoice_IncreaseEndInvoiceNo /
// Invoice_TempInvoice_IncreaseQtyInvoiceNo / Invoice_TempInvoice_UpdQtyInvoiceNo của TVAN gốc).
// Mỗi lần tăng số hóa đơn cuối (EndInvoiceNo) hoặc cập nhật lại cả dải số ghi lại để đối soát.
public class TemplateRangeLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int TemplateId { get; set; }
    public InvoiceTemplate? Template { get; set; }
    public TemplateRangeAction Action { get; set; } = TemplateRangeAction.IncreaseEndNo;
    public int OldStartInvoiceNo { get; set; }         // Số hóa đơn bắt đầu trước khi cập nhật (dùng cho UpdateQtyNo)
    public int NewStartInvoiceNo { get; set; }         // Số hóa đơn bắt đầu sau khi cập nhật (dùng cho UpdateQtyNo)
    public int OldEndInvoiceNo { get; set; }           // Số hóa đơn cuối trước khi tăng/cập nhật
    public int NewEndInvoiceNo { get; set; }           // Số hóa đơn cuối sau khi tăng/cập nhật
    public string? Remark { get; set; }                // Ghi chú / lý do
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký phát hành hóa đơn (theo Invoice_Invoice_Issued của TVAN gốc).
// Mỗi lần phát hành HĐ đã duyệt (APPROVED → ISSUED) ghi lại để đối soát.
public class IssueLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public IssueAction Action { get; set; } = IssueAction.Issue;   // Phát hành hóa đơn
    public string? EmailSend { get; set; }             // Email người nhận khi phát hành
    public string? Note { get; set; }                  // Ghi chú / lý do
    public string? By { get; set; }                    // Người phát hành
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký hủy hóa đơn (theo Invoice_Invoice_Cancel của TVAN gốc).
// Mỗi lần hủy hóa đơn đang chờ/đã duyệt (PENDING/APPROVED → CANCELED) ghi lại để đối soát.
public class CancelInvoiceLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public CancelAction Action { get; set; } = CancelAction.Cancel;   // Hủy hóa đơn
    public string? Remark { get; set; }                // Lý do hủy
    public string? By { get; set; }                    // Người hủy
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký cập nhật nội dung hóa đơn sau khi đã cấp số (theo Invoice_Invoice_UpdAfterAllocated của TVAN gốc).
// Mỗi lần sửa nội dung HĐ (người mua, phương thức thanh toán, tiền hàng/thuế) sau khi đã cấp số ghi lại để đối soát.
public class InvoiceUpdateLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string? BuyerName { get; set; }             // Tên người mua sau cập nhật
    public string? BuyerMst { get; set; }              // MST người mua sau cập nhật
    public string? BuyerAddress { get; set; }          // Địa chỉ người mua sau cập nhật
    public PaymentMethod PaymentMethod { get; set; }   // Phương thức thanh toán sau cập nhật
    public decimal Amount { get; set; }                // Tiền hàng sau cập nhật
    public decimal VatRate { get; set; }               // Thuế suất VAT sau cập nhật
    public DateTime InvoiceDate { get; set; }          // Ngày hóa đơn sau cập nhật
    public string? Note { get; set; }                  // Ghi chú / lý do
    public string? By { get; set; }                    // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký tạo biên bản đính kèm hóa đơn (theo luồng TaoBienBan của TVAN gốc).
// Mỗi lần tạo biên bản (hủy/điều chỉnh/thay thế) ghi lại tên file, nội dung base64, đường dẫn,
// lý do và người thực hiện để đối soát.
public class InvoiceRecordLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public RecordType Type { get; set; } = RecordType.Huy;   // Loại biên bản
    public string FileName { get; set; } = "";               // Tên file biên bản
    public string? FileSpec { get; set; }                    // Nội dung file (base64)
    public string? FilePath { get; set; }                    // Đường dẫn file đã lưu
    public string? Reason { get; set; }                      // Lý do hủy/điều chỉnh/thay thế
    public string? By { get; set; }                          // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhật ký sửa lỗi hàng loạt hóa đơn theo mẫu (theo luồng Invoice_Invoice_Fix của TVAN gốc).
// Mỗi lần ký lại hàng loạt HĐ đã phát hành (ISSUED) chưa ký lại của một mẫu hóa đơn ghi lại để đối soát:
// mẫu hóa đơn, số lượng HĐ đã sửa, danh sách số hóa đơn, lý do và người thực hiện.
public class BulkFixLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int TemplateId { get; set; }
    public InvoiceTemplate? Template { get; set; }
    public BulkFixAction Action { get; set; } = BulkFixAction.FixByTemplate;   // Loại thao tác sửa lỗi
    public string? TInvoiceCode { get; set; }                // Mã mẫu hóa đơn được sửa lỗi
    public string? FormNo { get; set; }                      // Mẫu số tại thời điểm sửa
    public int FixedCount { get; set; }                      // Số hóa đơn đã ký lại
    public string? InvoiceNos { get; set; }                  // Danh sách số hóa đơn đã sửa (cách nhau bởi dấu phẩy)
    public string? Reason { get; set; }                      // Lý do sửa lỗi
    public string? By { get; set; }                          // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Trường tùy chỉnh trên HÓA ĐƠN (theo bảng Invoice_CustomField của TVAN gốc).
// Mỗi tổ chức tự định nghĩa tối đa 10 trường (InvCF1..InvCF10) để lưu thêm thông tin trên hóa đơn.
// Khóa nghiệp vụ: (OrgId, InvoiceCustomFieldCode). FlagActive = trường đang dùng hay không.
public class InvoiceCustomField : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string InvoiceCustomFieldCode { get; set; } = "";   // Mã trường (VD InvCF1)
    public string InvoiceCustomFieldName { get; set; } = "";   // Tên hiển thị của trường
    public DBPhysicalType DBPhysicalType { get; set; } = DBPhysicalType.Text;   // Kiểu vật lý trong DB
    public bool FlagActive { get; set; } = true;                // Trường đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Trường tùy chỉnh trên DANH SÁCH HÀNG HÓA của hóa đơn (theo bảng Invoice_DtlCustomField của TVAN gốc).
// Mỗi tổ chức tự định nghĩa tối đa 5 trường (InvDCF1..InvDCF5) cho từng dòng hàng hóa.
// Khóa nghiệp vụ: (OrgId, InvoiceDtlCustomFieldCode).
public class InvoiceDtlCustomField : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string InvoiceDtlCustomFieldCode { get; set; } = "";   // Mã trường (VD InvDCF1)
    public string InvoiceDtlCustomFieldName { get; set; } = "";   // Tên hiển thị của trường
    public DBPhysicalType DBPhysicalType { get; set; } = DBPhysicalType.Text;   // Kiểu vật lý trong DB
    public bool FlagActive { get; set; } = true;                  // Trường đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Nhóm mẫu hóa đơn (theo bảng Invoice_TempGroup của TVAN gốc): mỗi nhóm gắn với một NNT (MST),
// định nghĩa mẫu hóa đơn (thân HTML InvoiceTGroupBody) + loại thuế suất (VATType) + loại hàng hóa/serial
// (Spec_Prd_Type). Mẫu hóa đơn (Invoice_TempInvoice) tham chiếu tới nhóm này qua InvoiceTGroupCode.
// Khóa nghiệp vụ: (OrgId, InvoiceTGroupCode).
public class InvoiceTempGroup : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string InvoiceTGroupCode { get; set; } = "";   // Mã nhóm mẫu (VD MAU1VAT)
    public string MST { get; set; } = "";                  // MST người nộp thuế sở hữu nhóm mẫu
    public VATType VATType { get; set; } = VATType.OneVat; // Loại thuế suất (1VAT/NVAT)
    public string InvoiceTGroupName { get; set; } = "";   // Tên nhóm mẫu
    public string? InvoiceTGroupBody { get; set; }         // Thân mẫu hóa đơn (HTML)
    public string? FilePathThumbnail { get; set; }         // Đường dẫn ảnh thumbnail của mẫu
    public SpecPrdType SpecPrdType { get; set; } = SpecPrdType.Spec;   // Loại hàng hóa - serial
    public bool FlagActive { get; set; } = true;           // Nhóm mẫu đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public List<InvoiceTempGroupField> Fields { get; set; } = new();
}

// Dòng dữ liệu Bảng tổng hợp hóa đơn (BTH) — theo model InvoiceGTH của TVAN gốc
// (idN.TVAN.Common/Models/TDiepTCT/InvoiceGTH.cs). Đây là bản xem trước danh sách hóa đơn
// đã phát hành/đã hủy trong một kỳ (ngày/tháng/quý) kèm trạng thái (TThai) và thông tin
// hóa đơn gốc bị điều chỉnh/thay thế — dùng để đối chiếu trước khi lập bảng tổng hợp gửi CQT.
public class InvoiceGthRow
{
    public string InvoiceCode { get; set; } = "";        // Mã tra cứu hóa đơn
    public string Sign { get; set; } = "";               // Ký hiệu
    public string FormNo { get; set; } = "";             // Mẫu số
    public string InvoiceNo { get; set; } = "";          // Số hóa đơn
    public DateTime InvoiceDate { get; set; }             // Ngày hóa đơn
    public string BuyerName { get; set; } = "";          // Tên người mua
    public string? BuyerMst { get; set; }                 // MST người mua
    public decimal Amount { get; set; }                   // Tổng tiền hàng (chưa thuế)
    public decimal VatRate { get; set; }                  // Thuế suất
    public decimal VatAmount { get; set; }                // Tổng tiền thuế
    public decimal Total { get; set; }                    // Tổng tiền thanh toán
    public TThai TThai { get; set; }                      // Trạng thái (Mới/Huỷ/Điều chỉnh/Thay thế)
    public string? RefSign { get; set; }                  // Ký hiệu hóa đơn gốc (bị điều chỉnh/thay thế)
    public string? RefFormNo { get; set; }                // Mẫu số hóa đơn gốc
    public string? RefInvoiceNo { get; set; }             // Số hóa đơn gốc
}

// Trường động của nhóm mẫu hóa đơn (theo bảng Invoice_TempGroupField của TVAN gốc):
// mỗi nhóm mẫu khai báo danh sách trường động (DBFieldName) + kiểu trường (TCFType).
// Khóa nghiệp vụ: (OrgId, InvoiceTGroupCode, DBFieldName).
public class InvoiceTempGroupField : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int InvoiceTempGroupId { get; set; }
    public InvoiceTempGroup? Group { get; set; }
    public string DBFieldName { get; set; } = "";   // Tên trường trong DB (VD Temp_NameSale)
    public string TCFType { get; set; } = "";       // Kiểu trường (VD TEXT)
    public bool FlagActive { get; set; } = true;     // Trường đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Mã loại thông điệp trao đổi với CQT (theo Mst_MessageTemplate.MessageTypeCode của TVAN gốc):
// 100 = gửi tờ khai đăng ký/thay đổi thông tin sử dụng HĐĐT;
// 204 = thông báo mẫu số 01/TB-KTDL về kết quả kiểm tra dữ liệu HĐĐT;
// 300 = thông báo về hóa đơn điện tử đã lập có sai sót;
// 301 = thông báo về việc tiếp nhận và kết quả xử lý hóa đơn đã lập có sai sót.
public enum MessageTypeCode { Register100 = 100, Check204 = 204, Error300 = 300, ErrorReply301 = 301 }

// Mẫu thông điệp/thông báo gửi CQT (theo bảng Mst_MessageTemplate của TVAN gốc):
// mỗi tổ chức khai báo mẫu nội dung thông điệp theo loại thông điệp (MessageTypeCode),
// kèm file mẫu (.rtmpl) lưu base64 + đường dẫn file đã lưu. Khóa nghiệp vụ: (OrgId, MessageTplCode).
public class MessageTemplate : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string MessageTplCode { get; set; } = "";        // Mã mẫu thông điệp
    public string MessageTplName { get; set; } = "";        // Tên mẫu thông điệp
    public MessageTypeCode MessageTypeCode { get; set; } = MessageTypeCode.Register100;   // Mã loại thông điệp
    public string MessageTplContent { get; set; } = "";     // Nội dung mẫu (JSON/HTML)
    public string? MessageTplFileName { get; set; }          // Tên file mẫu (.rtmpl)
    public string? MessageTplFileSpec { get; set; }          // Nội dung file mẫu (base64)
    public string? MessageTplFilePath { get; set; }          // Đường dẫn file mẫu đã lưu
    public bool FlagActive { get; set; } = true;             // Mẫu đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Loại thông báo (theo bảng Mst_NotifyType của TVAN gốc): danh mục phân loại thông báo
// (VD: thông báo phát hành hóa đơn, thông báo sai sót, thông báo CQT...) dùng để gán loại
// cho thông báo gửi tới người dùng. Khóa nghiệp vụ: (OrgId, NotifyType).
// DefaultActive = loại thông báo mặc định bật khi người dùng chưa cấu hình.
public class NotifyType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string NotifyTypeCode { get; set; } = "";      // Mã loại thông báo
    public string NotifyDesc { get; set; } = "";          // Mô tả loại thông báo
    public bool DefaultActive { get; set; } = true;        // Bật mặc định
    public bool FlagActive { get; set; } = true;           // Loại thông báo đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Chứng thư số (CKS) của tổ chức (theo bảng Mst_OrgCKS của TVAN gốc):
// mỗi tổ chức khai báo các chứng thư số dùng để ký hóa đơn điện tử, gồm số chứng thư (CANumber),
// tổ chức cấp (CAOrg), chủ thể (Subject), hiệu lực (CAEffDTimeUTCStart..CAEffDTimeUTCEnd)
// và đường dẫn/mật khẩu keystore (CTSPath/CTSPwd). Khóa nghiệp vụ: (OrgId, CANumber).
public class OrgCks : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string CANumber { get; set; } = "";              // Số chứng thư số
    public string? CAOrg { get; set; }                       // Tổ chức cấp chứng thư số
    public string? Subject { get; set; }                     // Chủ thể chứng thư số
    public DateTime? CAEffDTimeUTCStart { get; set; }        // Hiệu lực từ
    public DateTime? CAEffDTimeUTCEnd { get; set; }          // Hiệu lực đến
    public string? CTSPath { get; set; }                     // Đường dẫn keystore (CTS)
    public string? CTSPwd { get; set; }                      // Mật khẩu keystore (CTS)
    public bool FlagActive { get; set; } = true;             // Chứng thư đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Loại thông báo theo phạm vi người nhận (theo TConst.NotifyType của TVAN gốc):
// ALLUSER = thông báo gửi tới tất cả người dùng.
public enum NotifyScope { AllUser = 0 }

// Loại thông báo theo nội dung (theo TConst.NotifyType1 của TVAN gốc):
// MAINTENANCE = thông báo bảo trì/hệ thống.
public enum NotifyKind { Maintenance = 0 }

// Thông báo hệ thống (theo bảng Notify_Notify của TVAN gốc): mỗi thông báo có số (NotifyNo),
// loại (NotifyType/NotifyType1), mô tả, khoảng hiệu lực (EffDateStart..EffDateEnd) và cờ
// gửi email (FlagSendEmail). Thông báo được gửi tới người dùng qua bảng chi tiết Notify_NotifyDtl.
// Khóa nghiệp vụ: (OrgId, NotifyNo).
public class Notify : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string NotifyNo { get; set; } = "";              // Số thông báo
    public NotifyScope NotifyType { get; set; } = NotifyScope.AllUser;   // Loại thông báo (phạm vi người nhận)
    public NotifyKind NotifyType1 { get; set; } = NotifyKind.Maintenance; // Loại thông báo (nội dung)
    public string NotifyDesc { get; set; } = "";            // Mô tả nội dung thông báo
    public DateTime EffDateStart { get; set; } = DateTime.Today;   // Hiệu lực từ
    public DateTime EffDateEnd { get; set; } = DateTime.Today;     // Hiệu lực đến
    public bool FlagSendEmail { get; set; }                 // Có gửi email kèm thông báo
    public bool FlagActive { get; set; } = true;            // Thông báo đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public List<NotifyDtl> Details { get; set; } = new();
}

// Chi tiết thông báo theo người dùng (theo bảng Notify_NotifyDtl của TVAN gốc):
// mỗi dòng gắn một thông báo (NotifyNo) với một người dùng (UserCode) và cờ đã đọc (FlagRead).
// Khóa nghiệp vụ: (OrgId, NotifyNo, UserCode).
public class NotifyDtl : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int NotifyId { get; set; }
    public Notify? Notify { get; set; }
    public string UserCode { get; set; } = "";              // Mã người dùng nhận thông báo
    public bool FlagRead { get; set; }                      // Đã đọc hay chưa
    public bool FlagActive { get; set; } = true;            // Dòng đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Người nhận thông báo (theo bảng Mst_ManageNotify của TVAN gốc): danh sách người dùng
// được phép nhận thông báo hệ thống. Khi thêm một người nhận, hệ thống tự tạo đăng ký nhận
// cho TẤT CẢ loại thông báo (Map_UserInNotifyType) với cờ mặc định lấy từ NotifyType.DefaultActive.
// Khóa nghiệp vụ: (OrgId, UserCode).
public class NotifyRecipient : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string UserCode { get; set; } = "";              // Mã người dùng nhận thông báo
    public string UserName { get; set; } = "";              // Tên người dùng nhận thông báo
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public List<NotifyRecipientType> Types { get; set; } = new();
}

// Đăng ký nhận loại thông báo của một người nhận (theo bảng Map_UserInNotifyType của TVAN gốc):
// mỗi dòng gắn một người nhận (UserCode) với một loại thông báo (NotifyType) và cờ bật/tắt nhận
// (FlagNotify). Khóa nghiệp vụ: (OrgId, UserCode, NotifyType).
public class NotifyRecipientType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int NotifyRecipientId { get; set; }
    public NotifyRecipient? Recipient { get; set; }
    public string UserCode { get; set; } = "";              // Mã người dùng nhận thông báo
    public string NotifyType { get; set; } = "";            // Mã loại thông báo (NotifyTypeCode)
    public bool FlagNotify { get; set; } = true;            // Bật/tắt nhận loại thông báo này
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Cấu hình định dạng cột hiển thị theo bảng (theo bảng Mst_ColumnConfig của TVAN gốc):
// mỗi tổ chức tự khai báo định dạng hiển thị (ColumnFormat) + mô tả (ColumnDesc) cho một cột
// (ColumnName) của một bảng (TableName) — dùng để tùy biến cách hiển thị dữ liệu trên lưới.
// Khóa nghiệp vụ: (OrgId, TableName, ColumnName). FlagActive = cấu hình đang dùng hay không.
public class ColumnConfig : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string TableName { get; set; } = "";      // Tên bảng (VD Invoice_Invoice)
    public string ColumnName { get; set; } = "";     // Tên cột (VD InvoiceNo)
    public string? ColumnFormat { get; set; }         // Định dạng hiển thị (VD dd/MM/yyyy, N0)
    public string? ColumnDesc { get; set; }           // Mô tả cột
    public bool FlagActive { get; set; } = true;      // Cấu hình đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Kiểu dữ liệu vật lý của cột hóa đơn (theo Mst_SortColumnInvoice.ColumnType của TVAN gốc):
// TEXT = chuỗi, NUMBER = số, DATE = ngày tháng.
public enum SortColumnType { Text = 0, Number = 1, Date = 2 }

// Danh mục tiền tệ / ngoại tệ (theo bảng Mst_CurrencyEx của TVAN gốc):
// dùng để lấy tên đơn vị tiền tệ (CurrencyName) khi đọc số tiền thành chữ trên hóa đơn
// (theo luồng DocTien của TVAN gốc — Invoice_InvoiceController.DocTien).
// Khóa nghiệp vụ: (OrgId, CurrencyCode). FlagActive = tiền tệ đang dùng hay không.
public class CurrencyEx : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string CurrencyCode { get; set; } = "";      // Mã tiền tệ (VD VND, USD)
    public string CurrencyName { get; set; } = "";      // Tên tiền tệ (VD đồng, đô la Mỹ)
    public string? BaseCurrencyCode { get; set; }         // Mã tiền tệ gốc (quy đổi)
    public decimal BuyRate { get; set; }                  // Tỷ giá mua
    public decimal SellRate { get; set; }                 // Tỷ giá bán
    public string? Remark { get; set; }
    public bool FlagActive { get; set; } = true;          // Tiền tệ đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục thuế suất VAT (theo bảng Mst_VATRate của TVAN gốc): mỗi tổ chức khai báo các mức
// thuế suất VAT dùng khi lập hóa đơn (VD VAT0 = 0%, VAT5 = 5%, VAT8 = 8%, VAT10 = 10%,
// KCT = không chịu thuế, KKKNT = không kê khai nộp thuế). Mã thuế suất (VATRateCode) là khóa
// nghiệp vụ trong phạm vi tổ chức; VATRate là giá trị hiển thị (VD "10%").
// Khóa nghiệp vụ: (OrgId, VATRateCode). FlagActive = thuế suất đang dùng hay không.
public class VatRate : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string VATRateCode { get; set; } = "";      // Mã thuế suất (VD VAT10)
    public string VATRate { get; set; } = "";          // Giá trị thuế suất (VD 10%)
    public string? VATDesc { get; set; }                 // Mô tả
    public bool FlagActive { get; set; } = true;         // Thuế suất đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục đơn vị tính (theo bảng Mst_Unit của TVAN gốc): mỗi tổ chức khai báo các đơn vị tính
// dùng cho dòng hàng hóa trên hóa đơn (VD cái, chiếc, hộp, kg, lần...). Mã đơn vị (UnitCode) là
// khóa nghiệp vụ trong phạm vi tổ chức; UnitName là tên hiển thị.
// Khóa nghiệp vụ: (OrgId, UnitCode). FlagActive = đơn vị tính đang dùng hay không.
public class Unit : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string UnitCode { get; set; } = "";      // Mã đơn vị tính (VD CAI, KG)
    public string UnitName { get; set; } = "";      // Tên đơn vị tính (VD Cái, Kilôgam)
    public string? Remark { get; set; }               // Ghi chú
    public bool FlagActive { get; set; } = true;      // Đơn vị tính đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục loại dòng hàng hóa/dịch vụ trên hóa đơn (theo bảng Mst_InvoiceDtlType của TVAN gốc):
// phân loại từng dòng chi tiết hóa đơn (VD GOODS = hàng hóa/dịch vụ, NOTES = dòng ghi chú,
// FEES/PHI = phí). Khi lưu hóa đơn, mỗi dòng chi tiết phải có InvoiceDtlType tồn tại trong danh mục
// (theo Invoice_InvoiceDtl_SaveX_Input_InvoiceDtlType của TVAN gốc).
// Khóa nghiệp vụ: (OrgId, InvoiceDtlType). FlagActive = loại dòng đang dùng hay không.
public class InvoiceDtlType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string InvoiceDtlTypeCode { get; set; } = "";   // Mã loại dòng (VD GOODS, NOTES, FEES)
    public string? Desc { get; set; }                        // Mô tả loại dòng
    public bool FlagActive { get; set; } = true;             // Loại dòng đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Nhật ký đọc tiền bằng chữ (theo luồng DocTien của TVAN gốc —
// Invoice_InvoiceController.DocTien gọi clsDocTien.DocSo). Mỗi lần đọc một số tiền
// thành chữ tiếng Việt ghi lại để đối soát: số tiền, mã + tên tiền tệ, kết quả chữ.
public class DocTienLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public decimal Amount { get; set; }                  // Số tiền đọc (đã làm tròn)
    public string CurrencyCode { get; set; } = "";      // Mã tiền tệ
    public string CurrencyName { get; set; } = "";      // Tên tiền tệ
    public string Text { get; set; } = "";              // Kết quả đọc tiền bằng chữ
    public string? By { get; set; }                      // Người thực hiện
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Cấu hình cột hiển thị danh sách hóa đơn theo tổ chức (theo bảng Mst_SortColumnInvoice của TVAN gốc):
// mỗi tổ chức tự khai báo danh sách cột hiển thị trên lưới hóa đơn, gồm mã cột (ColumnCode),
// thứ tự hiển thị (Idx), tên hiển thị (ColumnName), kiểu dữ liệu (ColumnType) và cờ đang dùng (FlagActive).
// Khóa nghiệp vụ: (OrgId, ColumnCode). FlagActive = cột đang hiển thị hay không.
public class SortColumnInvoice : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string ColumnCode { get; set; } = "";      // Mã cột (VD InvoiceNo)
    public int Idx { get; set; }                       // Thứ tự hiển thị
    public string ColumnName { get; set; } = "";      // Tên hiển thị của cột (VD Số hóa đơn)
    public SortColumnType ColumnType { get; set; } = SortColumnType.Text;   // Kiểu dữ liệu cột
    public bool FlagActive { get; set; } = true;       // Cột đang hiển thị
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Nhóm người dùng (theo bảng Sys_Group của TVAN gốc): mỗi tổ chức tự khai báo các nhóm người dùng
// (VD Kế toán, Quản trị, Bán hàng...) để gom người dùng phục vụ phân quyền. Nhóm có mã (GroupCode),
// tên (GroupName) và cờ đang dùng (FlagActive). Xóa nhóm sẽ xóa kèm toàn bộ phân gán người dùng.
// Khóa nghiệp vụ: (OrgId, GroupCode). FlagActive = nhóm đang dùng hay không.
public class SysGroup : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string GroupCode { get; set; } = "";        // Mã nhóm người dùng (VD KETOAN)
    public string GroupName { get; set; } = "";        // Tên nhóm người dùng (VD Nhóm kế toán)
    public bool FlagActive { get; set; } = true;       // Nhóm đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public List<SysUserInGroup> Members { get; set; } = new();
}

// Phân gán người dùng vào nhóm (theo bảng Sys_UserInGroup của TVAN gốc): mỗi dòng gắn một người dùng
// (UserCode) vào một nhóm (GroupCode). Lưu theo kiểu thay thế toàn bộ danh sách thành viên của nhóm
// (theo Sys_UserInGroup_Save của TVAN gốc: xóa hết rồi chèn lại).
// Khóa nghiệp vụ: (OrgId, GroupCode, UserCode).
public class SysUserInGroup : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int SysGroupId { get; set; }
    public SysGroup? Group { get; set; }
    public string GroupCode { get; set; } = "";        // Mã nhóm người dùng
    public string UserCode { get; set; } = "";         // Mã người dùng trong nhóm
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Gói Module (theo bảng Sys_Modules của TVAN gốc): mỗi gói Module thuộc một giải pháp (Sys_Solution)
// và quy định hạn mức sử dụng: số hóa đơn (QtyInvoice) và dung lượng (ValCapacity). Gói Module có
// vòng đời bật (Active) / ngừng (Inactive) — theo Sys_ModulesController.ActiveModule/InactiveModule.
// Khóa nghiệp vụ: (OrgId, ModuleCode). FlagActive = gói Module đang dùng hay không.
public class SysModule : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string ModuleCode { get; set; } = "";       // Mã gói Module (VD TVAN_BASIC)
    public string SolutionCode { get; set; } = "";     // Mã giải pháp mà gói thuộc về (Sys_Solution)
    public string ModuleName { get; set; } = "";       // Tên gói Module
    public string? Description { get; set; }            // Mô tả
    public double QtyInvoice { get; set; }              // Hạn mức số hóa đơn
    public double ValCapacity { get; set; }             // Hạn mức dung lượng
    public bool FlagActive { get; set; } = true;        // Gói Module đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Giải pháp (theo bảng Sys_Solution của TVAN gốc): gói Module phải thuộc một giải pháp đang dùng
// (theo Sys_Solution_CheckDB trong Sys_Modules_Create/Update của TVAN gốc).
// Khóa nghiệp vụ: (OrgId, SolutionCode). FlagActive = giải pháp đang dùng hay không.
public class SysSolution : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string SolutionCode { get; set; } = "";     // Mã giải pháp (VD TVAN)
    public string SolutionName { get; set; } = "";     // Tên giải pháp
    public bool FlagActive { get; set; } = true;        // Giải pháp đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Loại đối tượng (chức năng) trong hệ thống (theo Sys_Object.ObjectType của TVAN gốc):
// FUNC = chức năng/màn hình, MENU = menu, BUTTON = nút chức năng.
public enum SysObjectType { Func = 0, Menu = 1, Button = 2 }

// Đối tượng (chức năng/menu/nút) của hệ thống (theo bảng Sys_Object của TVAN gốc):
// mỗi đối tượng có mã (ObjectCode), tên (ObjectName), dịch vụ (ServiceCode), loại (ObjectType)
// và cờ đang dùng (FlagActive). Dùng để phân gán quyền chức năng vào gói Module (Sys_ObjectInModules).
// Khóa nghiệp vụ: (OrgId, ObjectCode). FlagActive = đối tượng đang dùng hay không.
public class SysObject : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string ObjectCode { get; set; } = "";       // Mã đối tượng (VD INV_ISSUE)
    public string ObjectName { get; set; } = "";       // Tên đối tượng (VD Phát hành hóa đơn)
    public string? ServiceCode { get; set; }             // Mã dịch vụ/nhóm chức năng
    public SysObjectType ObjectType { get; set; } = SysObjectType.Func;   // Loại đối tượng (FUNC/MENU/BUTTON)
    public bool FlagActive { get; set; } = true;         // Đối tượng đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Phân gán đối tượng (chức năng) vào gói Module (theo bảng Sys_ObjectInModules của TVAN gốc):
// mỗi dòng gắn một đối tượng (ObjectCode) vào một gói Module (ModuleCode). Lưu theo kiểu thay thế
// toàn bộ danh sách đối tượng của gói (theo Sys_ObjectInModules_Save của TVAN gốc: xóa hết rồi chèn lại).
// Khóa nghiệp vụ: (OrgId, ModuleCode, ObjectCode).
public class SysObjectInModule : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int SysModuleId { get; set; }
    public SysModule? Module { get; set; }
    public string ModuleCode { get; set; } = "";       // Mã gói Module
    public string ObjectCode { get; set; } = "";       // Mã đối tượng được gán vào gói
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục Thương hiệu (theo bảng Mst_Brand của TVAN gốc): mỗi tổ chức khai báo các thương hiệu
// (hãng sản xuất) dùng để phân loại sản phẩm/hàng hóa. Model sản phẩm (Mst_Model) tham chiếu tới
// thương hiệu qua BrandCode. Khóa nghiệp vụ: (OrgId, BrandCode). FlagActive = thương hiệu đang dùng.
public class Brand : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string BrandCode { get; set; } = "";        // Mã thương hiệu (VD SAMSUNG)
    public string BrandName { get; set; } = "";        // Tên thương hiệu (VD Samsung)
    public string? Remark { get; set; }                 // Ghi chú
    public bool FlagActive { get; set; } = true;         // Thương hiệu đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục Model sản phẩm (theo bảng Mst_Model của TVAN gốc): mỗi model thuộc một thương hiệu
// (BrandCode) và có thể có mã model nội bộ của tổ chức (OrgModelCode). Dùng để phân loại hàng hóa
// theo model khi lập hóa đơn. Khóa nghiệp vụ: (OrgId, ModelCode). FlagActive = model đang dùng.
public class ProductModel : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string ModelCode { get; set; } = "";        // Mã model (VD A54)
    public string ModelName { get; set; } = "";        // Tên model (VD Galaxy A54)
    public string? OrgModelCode { get; set; }            // Mã model nội bộ của tổ chức
    public string BrandCode { get; set; } = "";        // Mã thương hiệu (FK nghiệp vụ tới Brand.BrandCode)
    public string? Remark { get; set; }                 // Ghi chú
    public bool FlagActive { get; set; } = true;         // Model đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục Loại sản phẩm (theo bảng Mst_SpecType1 của TVAN gốc): mỗi tổ chức khai báo các
// loại sản phẩm (nhóm hàng hóa theo loại) dùng để phân loại sản phẩm/hàng hóa khi lập hóa đơn.
// Khóa nghiệp vụ: (OrgId, SpecType1). FlagActive = loại sản phẩm đang dùng hay không.
public class SpecType1 : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string SpecType1Code { get; set; } = "";     // Mã loại sản phẩm (VD DIENTU)
    public string SpecType1Name { get; set; } = "";     // Tên loại sản phẩm (VD Điện tử)
    public string? Remark { get; set; }                   // Ghi chú
    public bool FlagActive { get; set; } = true;          // Loại sản phẩm đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Tích hợp TVAN (theo bảng Mst_TVANInteg của TVAN gốc): mỗi tổ chức (OrgID) khai báo tổ chức
// giải pháp TVAN tương ứng để trao đổi hóa đơn — MSTTCTN_In (hóa đơn đầu vào) và MSTTCTN_Out
// (hóa đơn đầu ra). Lưu theo kiểu upsert theo OrgID (theo Mst_TVANInteg_Save của TVAN gốc:
// luôn set FlagActive = Active). Khóa nghiệp vụ: (OrgId, OrgCode).
public class TvanInteg : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string OrgCode { get; set; } = "";          // Mã tổ chức (OrgID) — khóa nghiệp vụ
    public string? MsttctnIn { get; set; }              // MST tổ chức giải pháp TVAN — hóa đơn đầu vào
    public string? MsttctnOut { get; set; }             // MST tổ chức giải pháp TVAN — hóa đơn đầu ra
    public bool FlagActive { get; set; } = true;         // Đang dùng (TVAN gốc hardcode = Active)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Danh mục mã loại (theo bảng Mst_TypeCode của TVAN gốc): mỗi tổ chức khai báo các mã loại
// dùng để phân loại giao dịch/nhật ký kết nối với cơ quan thuế (VD loại thông điệp trao đổi).
// Khóa nghiệp vụ: (OrgId, TypeCode). FlagActive = mã loại đang dùng hay không.
public class MstTypeCode : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string TypeCodeValue { get; set; } = "";   // Mã loại (VD 100, 200, 300)
    public string? TypeDesc { get; set; }               // Mô tả mã loại
    public string? TypeGroup { get; set; }              // Nhóm mã loại
    public bool FlagActive { get; set; } = true;         // Mã loại đang dùng
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Hành động của thông điệp trao đổi với cơ quan thuế (theo Log_TCTTransaction.MessageAction của TVAN gốc):
// Send = gửi tới CQT, Receive = nhận phản hồi từ CQT.
public enum TctMessageAction { Send = 0, Receive = 1 }

// Trạng thái xử lý của thông điệp trao đổi với cơ quan thuế (theo Log_TCTTransaction.MessageStatus của TVAN gốc):
// Success = thành công, Error = lỗi, Pending = đang chờ xử lý.
public enum TctMessageStatus { Success = 0, Error = 1, Pending = 2 }

// Kết quả của thông điệp trao đổi với cơ quan thuế (theo Log_TCTTransaction.MessageResult của TVAN gốc):
// Accept = CQT chấp nhận, Reject = CQT từ chối, None = chưa có kết quả.
public enum TctMessageResult { None = 0, Accept = 1, Reject = 2 }

// Nhật ký truyền nhận với cơ quan thuế (theo bảng Log_TCTTransaction của TVAN gốc —
// màn Log_NKTNController.Index/Detail). Mỗi lần hệ thống gửi/nhận một thông điệp trao đổi
// với cơ quan thuế (tờ khai đăng ký, hóa đơn, thông báo sai sót...) ghi lại để đối soát:
// mã thông điệp, hành động (gửi/nhận), loại thông điệp (TypeCode), trạng thái, kết quả,
// MST bên bán/bên mua, số lượng hóa đơn, mô tả, tag và đường dẫn file XML.
// Khóa nghiệp vụ: (OrgId, MessageCode).
public class TctTransactionLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string MessageCode { get; set; } = "";        // Mã thông điệp (VD 100..., 200..., 300...)
    public string? MstSeller { get; set; }                 // MST bên bán (NNT)
    public TctMessageAction MessageAction { get; set; } = TctMessageAction.Send;   // Gửi / nhận
    public DateTime? MessageDTime { get; set; }            // Thời điểm trao đổi thông điệp
    public string? TypeCode { get; set; }                  // Mã loại thông điệp (Mst_TypeCode)
    public TctMessageStatus MessageStatus { get; set; } = TctMessageStatus.Success;   // Trạng thái xử lý
    public TctMessageResult MessageResult { get; set; } = TctMessageResult.None;      // Kết quả (chấp nhận/từ chối)
    public string? MessageRefCode { get; set; }            // Mã tham chiếu (mã V / mã thông điệp liên quan)
    public string? Partner { get; set; }                   // Đối tác trao đổi (cơ quan thuế)
    public DateTime? MessageDate { get; set; }             // Ngày thông điệp
    public string? MstBuyer { get; set; }                  // MST bên mua
    public int InvoiceQty { get; set; }                    // Số lượng hóa đơn trong thông điệp
    public string? MessageDesc { get; set; }               // Mô tả nội dung thông điệp
    public string? Tag { get; set; }                       // Thẻ phân loại (tìm kiếm)
    public string? XmlFilePath { get; set; }               // Đường dẫn file XML thông điệp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
