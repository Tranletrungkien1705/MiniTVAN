using MiniTVAN.Models;
namespace MiniTVAN.Services;

public static class Ui
{
    public static string Money(decimal v) => v.ToString("N0") + "đ";

    public static (string text, string css) Inv(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft     => ("Nháp", "secondary"),
        InvoiceStatus.Sent      => ("Đang gửi TCT", "info"),
        InvoiceStatus.Accepted  => ("CQT chấp nhận", "success"),
        InvoiceStatus.Rejected  => ("CQT từ chối", "danger"),
        InvoiceStatus.Cancelled => ("Đã hủy", "dark"),
        _ => (s.ToString(), "secondary")
    };

    public static (string text, string css) Reg(RegStatus s) => s switch
    {
        RegStatus.None       => ("Chưa đăng ký", "secondary"),
        RegStatus.Pending    => ("Chờ duyệt", "warning"),
        RegStatus.Registered => ("Đã đăng ký", "success"),
        RegStatus.Rejected   => ("Bị từ chối", "danger"),
        _ => (s.ToString(), "secondary")
    };

    public static string Msg(MsgType t) => t switch
    {
        MsgType.RegisterNnt => "Đăng ký NNT",
        MsgType.SendInvoice => "Gửi hóa đơn",
        MsgType.CancelInvoice => "Hủy hóa đơn",
        MsgType.AdjustInvoice => "Điều chỉnh hóa đơn",
        MsgType.ReplaceInvoice => "Thay thế hóa đơn",
        _ => t.ToString()
    };
    public static string Dir(MsgDir d) => d == MsgDir.Out ? "→ TCT" : "← TCT";
    public static string DirCss(MsgDir d) => d == MsgDir.Out ? "text-primary" : "text-success";

    public static (string text, string css) Source(SourceInvoiceCode s) => s switch
    {
        SourceInvoiceCode.Root    => ("Hóa đơn gốc", "secondary"),
        SourceInvoiceCode.Replace => ("Hóa đơn thay thế", "warning"),
        SourceInvoiceCode.Adjust  => ("Hóa đơn điều chỉnh", "info"),
        _ => (s.ToString(), "secondary")
    };

    public static string Adj(InvoiceAdjType t) => t switch
    {
        InvoiceAdjType.Increase => "Tăng",
        InvoiceAdjType.Decrease => "Giảm",
        _ => "Bình thường"
    };

    public static (string text, string css) LicHist(LicenseHistType t) => t switch
    {
        LicenseHistType.Create   => ("Cấp mới", "primary"),
        LicenseHistType.Increase => ("Tăng", "success"),
        LicenseHistType.Decrease => ("Giảm", "warning"),
        _ => (t.ToString(), "secondary")
    };

    public static string Period(PeriodType t) => t switch
    {
        PeriodType.Day     => "Ngày",
        PeriodType.Month   => "Tháng",
        PeriodType.Quarter => "Quý",
        PeriodType.Year    => "Năm",
        _ => t.ToString()
    };

    public static (string text, string css) Gth(GthStatus s) => s switch
    {
        GthStatus.Draft    => ("Nháp", "secondary"),
        GthStatus.Sent     => ("Đang gửi CQT", "info"),
        GthStatus.Accepted => ("CQT chấp nhận", "success"),
        GthStatus.Rejected => ("CQT từ chối", "danger"),
        _ => (s.ToString(), "secondary")
    };

    public static (string text, string css) Lookup(LookupResult r) => r switch
    {
        LookupResult.Success  => ("Tìm thấy", "success"),
        LookupResult.NotFound => ("Không tìm thấy", "warning"),
        LookupResult.Error    => ("Lỗi", "danger"),
        _ => (r.ToString(), "secondary")
    };

    public static (string text, string css) Email(EmailSendResult r) => r switch
    {
        EmailSendResult.Success => ("Đã gửi", "success"),
        EmailSendResult.Failed  => ("Gửi lỗi", "danger"),
        _ => (r.ToString(), "secondary")
    };
}
