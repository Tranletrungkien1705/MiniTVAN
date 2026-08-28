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
        _ => t.ToString()
    };
    public static string Dir(MsgDir d) => d == MsgDir.Out ? "→ TCT" : "← TCT";
    public static string DirCss(MsgDir d) => d == MsgDir.Out ? "text-primary" : "text-success";
}
