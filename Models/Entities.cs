namespace MiniTVAN.Models;

public interface IOrgOwned { Guid OrgId { get; set; } }

public enum RegStatus { None = 0, Pending = 1, Registered = 2, Rejected = 3 }
// Vòng đời hóa đơn khi truyền tới cơ quan thuế
public enum InvoiceStatus { Draft = 0, Sent = 1, Accepted = 2, Rejected = 3, Cancelled = 4 }
public enum MsgType { RegisterNnt = 0, SendInvoice = 1, CancelInvoice = 2, AdjustInvoice = 3, ReplaceInvoice = 4 }
public enum MsgDir { Out = 0, In = 1 }   // Out = gửi tới TCT, In = TCT phản hồi

// Nguồn gốc hóa đơn (theo TConst.SourceInvoiceCode của TVAN gốc)
public enum SourceInvoiceCode { Root = 0, Replace = 1, Adjust = 2 }
// Loại điều chỉnh (theo TConst.InvoiceAdjType của TVAN gốc)
public enum InvoiceAdjType { Normal = 0, Increase = 1, Decrease = 2 }
// Loại thao tác trên hạn mức hóa đơn (theo Invoice_licenseCreHist của TVAN gốc)
public enum LicenseHistType { Create = 0, Increase = 1, Decrease = 2 }

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
