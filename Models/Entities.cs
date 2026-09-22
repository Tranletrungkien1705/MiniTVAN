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

// Cờ đánh dấu hóa đơn đã được ký lại (theo Invoice_Invoice.FlagHotfix của TVAN gốc):
// None = chưa ký lại (FlagHotfix is null), Hotfixed = đã ký lại (FlagHotfix = '1').
public enum HotfixFlag { None = 0, Hotfixed = 1 }

// Loại thao tác duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc)
public enum ApproveAction { Approve = 0, Unapprove = 1 }

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

// Trạng thái mẫu hóa đơn (theo Invoice_TempInvoice.TInvoiceStatus của TVAN gốc):
// Draft = PENDING (chờ), SentTct = SENTTCT (đã gửi CQT, chờ CQT phát hành), Issued = ISSUED (đang sử dụng), Inactive = ngừng.
public enum TemplateStatus { Draft = 0, Issued = 1, Inactive = 2, SentTct = 3 }

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
public enum TemplateRangeAction { IncreaseEndNo = 0 }

// Loại thao tác sửa lỗi hàng loạt hóa đơn theo mẫu (theo luồng Invoice_Invoice_Fix của TVAN gốc):
// FixByTemplate = ký lại hàng loạt HĐ đã phát hành (ISSUED) chưa ký lại của một mẫu hóa đơn.
public enum BulkFixAction { FixByTemplate = 0 }

// Kiểu vật lý trong DB của trường tùy chỉnh (theo Invoice_CustomField.DBPhysicalType của TVAN gốc).
// Giao diện gốc luôn gửi "TEXT" (xem invoice_CustomField.js: DBPhysicalType = "TEXT").
public enum DBPhysicalType { Text = 0, Number = 1, Date = 2 }

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

    // Mã của CQT cấp cho Máy tính tiền (theo Mst_NNT.MCCQT của TVAN gốc):
    // chuỗi 5 ký tự do CQT cấp, dùng để sinh mã CQT trên hóa đơn khởi tạo từ máy tính tiền (MCCQTMTT).
    public string? MCCQT { get; set; }

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
// Invoice_TempInvoice_IncreaseQtyInvoiceNo của TVAN gốc).
// Mỗi lần tăng số hóa đơn cuối (EndInvoiceNo) ghi lại để đối soát.
public class TemplateRangeLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int TemplateId { get; set; }
    public InvoiceTemplate? Template { get; set; }
    public TemplateRangeAction Action { get; set; } = TemplateRangeAction.IncreaseEndNo;
    public int OldEndInvoiceNo { get; set; }           // Số hóa đơn cuối trước khi tăng
    public int NewEndInvoiceNo { get; set; }           // Số hóa đơn cuối sau khi tăng
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
