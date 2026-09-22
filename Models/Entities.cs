namespace MiniTVAN.Models;

public interface IOrgOwned { Guid OrgId { get; set; } }

public enum RegStatus { None = 0, Pending = 1, Registered = 2, Rejected = 3 }
// Vòng đời hóa đơn khi truyền tới cơ quan thuế
// Deleted = xóa hóa đơn đã phát hành (theo TConst.InvoiceStatus.Deleted của TVAN gốc)
public enum InvoiceStatus { Draft = 0, Sent = 1, Accepted = 2, Rejected = 3, Cancelled = 4, Deleted = 5 }
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

// Cờ đánh dấu hóa đơn đã được ký lại (theo Invoice_Invoice.FlagHotfix của TVAN gốc):
// None = chưa ký lại (FlagHotfix is null), Hotfixed = đã ký lại (FlagHotfix = '1').
public enum HotfixFlag { None = 0, Hotfixed = 1 }

public class Org
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Người nộp thuế (bên bán) — phải đăng ký với TCT trước khi phát hành HĐ
public class Nnt : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Mst { get; set; } = "";            // Mã số thuế
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Email { get; set; }
    public RegStatus RegStatus { get; set; } = RegStatus.None;
    public DateTime? RegisteredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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
// Dùng để tra cứu "CQT quản lý" của một MST người nộp thuế.
public class TaxOffice : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string GovTaxID { get; set; } = "";        // Mã cơ quan thuế
    public string GovTaxName { get; set; } = "";      // Tên cơ quan thuế
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool FlagActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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

// Nhật ký ký lại hóa đơn (theo Invoice_Invoice_ReSign của TVAN gốc).
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
