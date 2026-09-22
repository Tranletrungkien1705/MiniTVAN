using MiniTVAN.Models;
namespace MiniTVAN.Services;

public static class Ui
{
    public static string Money(decimal v) => v.ToString("N0") + "đ";

    public static (string text, string css) Inv(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft     => ("Nháp", "secondary"),
        InvoiceStatus.Approved  => ("Đã duyệt", "primary"),
        InvoiceStatus.Sent      => ("Đang gửi TCT", "info"),
        InvoiceStatus.Accepted  => ("CQT chấp nhận", "success"),
        InvoiceStatus.Rejected  => ("CQT từ chối", "danger"),
        InvoiceStatus.Cancelled => ("Đã hủy", "dark"),
        InvoiceStatus.Deleted   => ("Đã xóa", "danger"),
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

    public static (string text, string css) ConvPrint(ConversionPrintFlag f) => f switch
    {
        ConversionPrintFlag.Printed    => ("Đã in chuyển đổi", "warning"),
        ConversionPrintFlag.NotPrinted => ("Chưa in chuyển đổi", "secondary"),
        _ => (f.ToString(), "secondary")
    };

    public static (string text, string css) ConvAction(ConversionPrintAction a) => a switch
    {
        ConversionPrintAction.Print => ("In chuyển đổi", "warning"),
        ConversionPrintAction.Reset => ("Bỏ cờ in chuyển đổi", "secondary"),
        _ => (a.ToString(), "secondary")
    };

    public static (string text, string css) Sign60Day(Sign60DayFlag f) => f switch
    {
        Sign60DayFlag.Check   => ("Đang kiểm tra", "success"),
        Sign60DayFlag.Uncheck => ("Bỏ kiểm tra", "warning"),
        _ => (f.ToString(), "secondary")
    };

    public static (string text, string css) Hotfix(HotfixFlag f) => f switch
    {
        HotfixFlag.Hotfixed => ("Đã ký lại", "primary"),
        HotfixFlag.None     => ("Chưa ký lại", "secondary"),
        _ => (f.ToString(), "secondary")
    };

    public static (string text, string css) ApproveAction(ApproveAction a) => a switch
    {
        Models.ApproveAction.Approve   => ("Duyệt hóa đơn", "primary"),
        Models.ApproveAction.Unapprove => ("Bỏ duyệt", "secondary"),
        _ => (a.ToString(), "secondary")
    };

    public static (string text, string css) Template(TemplateStatus s) => s switch
    {
        TemplateStatus.Draft    => ("Nháp", "secondary"),
        TemplateStatus.Issued   => ("Đang sử dụng", "success"),
        TemplateStatus.Inactive => ("Ngừng hoạt động", "dark"),
        _ => (s.ToString(), "secondary")
    };

    public static string NoRule(InvoiceNoRule r) => r switch
    {
        InvoiceNoRule.TT68 => "TT68 (8 số liên tục)",
        InvoiceNoRule.TT78 => "TT78 (7 số, reset theo năm)",
        _ => r.ToString()
    };

    // Thao tác vòng đời mẫu hóa đơn (theo Invoice_TempInvoice_Issued / Invoice_TempInvoice_InActive của TVAN gốc).
    public static (string text, string css) TemplateAction(TemplateStatus s) => s switch
    {
        TemplateStatus.Draft    => ("Phát hành", "success"),
        TemplateStatus.Issued   => ("Ngừng hoạt động", "dark"),
        _ => ("", "secondary")
    };

    // Mã loại thông điệp CQT phản hồi (theo Invoice_Invoice_TCTReceive của TVAN gốc).
    public static string TctMsg(TctMessageType t) => t switch
    {
        TctMessageType.Success202 => "202 — Phát hành thành công",
        TctMessageType.Fail204    => "204 — Phát hành thất bại",
        _ => ((int)t).ToString()
    };

    // Trạng thái CQT chấp nhận/từ chối (theo TConst.TCTStatus của TVAN gốc).
    public static (string text, string css) TctAccept(TctAcceptStatus s) => s switch
    {
        TctAcceptStatus.Accept => ("Chấp nhận", "success"),
        TctAcceptStatus.Reject => ("Từ chối", "danger"),
        _ => (s.ToString(), "secondary")
    };

    // Phương thức thanh toán (theo Mst_PaymentMethods của TVAN gốc).
    public static string Payment(PaymentMethod p) => p switch
    {
        PaymentMethod.Cash           => "Tiền mặt",
        PaymentMethod.Transfer       => "Chuyển khoản",
        PaymentMethod.CashOrTransfer => "Tiền mặt/Chuyển khoản",
        _ => p.ToString()
    };
}
