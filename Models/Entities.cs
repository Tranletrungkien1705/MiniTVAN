namespace MiniTVAN.Models;

public interface IOrgOwned { Guid OrgId { get; set; } }

public enum RegStatus { None = 0, Pending = 1, Registered = 2, Rejected = 3 }
// Vòng đời hóa đơn khi truyền tới cơ quan thuế
public enum InvoiceStatus { Draft = 0, Sent = 1, Accepted = 2, Rejected = 3, Cancelled = 4 }
public enum MsgType { RegisterNnt = 0, SendInvoice = 1, CancelInvoice = 2 }
public enum MsgDir { Out = 0, In = 1 }   // Out = gửi tới TCT, In = TCT phản hồi

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

    public decimal VatAmount => Math.Round(Amount * VatRate / 100m, 0);
    public decimal Total => Amount + VatAmount;
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
