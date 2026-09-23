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

    // Kiểu dấu phân cách động khi hiển thị số trên hóa đơn (theo Mst_DynamicComma.FlagStyle của TVAN gốc).
    public static string DynamicComma(DynamicCommaStyle s) => s switch
    {
        DynamicCommaStyle.Comma => "Dấu phẩy ','",
        DynamicCommaStyle.Dot   => "Dấu chấm '.'",
        _ => s.ToString()
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

    // Loại thao tác phát hành hóa đơn (theo Invoice_Invoice_Issued của TVAN gốc).
    public static (string text, string css) IssueAction(IssueAction a) => a switch
    {
        Models.IssueAction.Issue => ("Phát hành hóa đơn", "success"),
        _ => (a.ToString(), "secondary")
    };

    public static (string text, string css) Template(TemplateStatus s) => s switch
    {
        TemplateStatus.Draft    => ("Nháp", "secondary"),
        TemplateStatus.SentTct  => ("Đã gửi CQT", "info"),
        TemplateStatus.Issued   => ("Đang sử dụng", "success"),
        TemplateStatus.Inactive => ("Ngừng hoạt động", "dark"),
        TemplateStatus.Cancel   => ("Đã hủy", "danger"),
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

    // Loại thao tác mở rộng dải số mẫu hóa đơn (theo Invoice_TempInvoice_IncreaseEndInvoiceNo /
    // Invoice_TempInvoice_UpdQtyInvoiceNo của TVAN gốc).
    public static string RangeAction(TemplateRangeAction a) => a switch
    {
        TemplateRangeAction.IncreaseEndNo => "Tăng số cuối",
        TemplateRangeAction.UpdateQtyNo => "Cập nhật dải số",
        _ => a.ToString()
    };

    // Loại thao tác gửi/nhận kết quả mẫu hóa đơn với CQT (theo Invoice_TempInvoice_SentTCT /
    // Invoice_TempInvoice_TCTIssued của TVAN gốc).
    public static (string text, string css) TemplateTctAction(TemplateTctAction a) => a switch
    {
        Models.TemplateTctAction.SendTct    => ("Gửi CQT", "primary"),
        Models.TemplateTctAction.ReceiveTct => ("Nhận KQ CQT", "info"),
        _ => (a.ToString(), "secondary")
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

    // Cờ xử lý thay thế/điều chỉnh của hóa đơn (theo Invoice_Invoice.FlagReplaceOrAdjust của TVAN gốc).
    public static (string text, string css) ReplaceOrAdjust(ReplaceOrAdjustFlag f) => f switch
    {
        ReplaceOrAdjustFlag.Replace => ("Thay thế", "warning"),
        ReplaceOrAdjustFlag.Adjust  => ("Điều chỉnh", "info"),
        _ => ("Bình thường", "secondary")
    };

    // Trạng thái gửi thông báo sai sót tới CQT (theo Invoice_Invoice.FlagSuaDoi của TVAN gốc).
    public static (string text, string css) SuaDoi(SuaDoiFlag? f) => f switch
    {
        SuaDoiFlag.Sent    => ("Đã gửi thông báo", "info"),
        SuaDoiFlag.Allowed => ("TCT cho phép", "success"),
        SuaDoiFlag.Error   => ("TCT trả lỗi", "danger"),
        _ => ("Chưa gửi", "secondary")
    };

    // Phương thức thanh toán (theo Mst_PaymentMethods của TVAN gốc).
    public static string Payment(PaymentMethod p) => p switch
    {
        PaymentMethod.Cash           => "Tiền mặt",
        PaymentMethod.Transfer       => "Chuyển khoản",
        PaymentMethod.CashOrTransfer => "Tiền mặt/Chuyển khoản",
        _ => p.ToString()
    };

    // Loại thao tác hủy hóa đơn (theo Invoice_Invoice_Cancel của TVAN gốc).
    public static (string text, string css) CancelAction(CancelAction a) => a switch
    {
        Models.CancelAction.Cancel => ("Hủy hóa đơn", "dark"),
        _ => (a.ToString(), "secondary")
    };

    // Loại hóa đơn theo ký tự thứ 4 của Mẫu số (theo Thông tư 32/2025/TT-BTC):
    // 'M' → hóa đơn điện tử khởi tạo từ máy tính tiền (MTT).
    public static (string text, string css) InvoiceType(InvoiceTypeM t) => t switch
    {
        InvoiceTypeM.Machine => ("HĐ máy tính tiền", "info"),
        _ => ("HĐ thông thường", "secondary")
    };

    // Loại biên bản đính kèm hóa đơn (theo luồng TaoBienBan của TVAN gốc).
    public static (string text, string css) Record(RecordType t) => t switch
    {
        RecordType.Huy       => ("Biên bản hủy", "dark"),
        RecordType.DieuChinh => ("Biên bản điều chỉnh", "info"),
        RecordType.ThayThe   => ("Biên bản thay thế", "warning"),
        _ => (t.ToString(), "secondary")
    };

    // Loại thuế suất của nhóm mẫu hóa đơn (theo TConst.Client_VATType của TVAN gốc).
    public static string VatType(VATType t) => t switch
    {
        VATType.OneVat => "1VAT (một thuế suất)",
        VATType.NVat   => "NVAT (nhiều thuế suất)",
        _ => t.ToString()
    };

    // Loại hàng hóa - serial của nhóm mẫu hóa đơn (theo TConst.Spec_Prd_Type của TVAN gốc).
    public static string SpecPrd(SpecPrdType t) => t switch
    {
        SpecPrdType.Spec      => "Đặc tính/Serial",
        SpecPrdType.ProductId => "Mã sản phẩm",
        _ => t.ToString()
    };

    // Trạng thái hóa đơn trong Bảng tổng hợp hóa đơn (BTH) — theo cột TThai của
    // Invoice_Invoice_BTHGetX (TVAN gốc): 0=Mới, 1=Huỷ, 2=Điều chỉnh, 3=Thay thế.
    public static (string text, string css) TThai(TThai t) => t switch
    {
        Models.TThai.Moi        => ("Mới", "success"),
        Models.TThai.Huy        => ("Huỷ", "dark"),
        Models.TThai.DieuChinh  => ("Điều chỉnh", "info"),
        Models.TThai.ThayThe    => ("Thay thế", "warning"),
        _ => (t.ToString(), "secondary")
    };

    // Mã loại thông điệp trao đổi với CQT (theo Mst_MessageTemplate.MessageTypeCode của TVAN gốc).
    public static string MessageType(MessageTypeCode t) => t switch
    {
        MessageTypeCode.Register100    => "100 — Đăng ký/thay đổi thông tin sử dụng HĐĐT",
        MessageTypeCode.Check204       => "204 — Kết quả kiểm tra dữ liệu HĐĐT",
        MessageTypeCode.Error300       => "300 — HĐĐT đã lập có sai sót",
        MessageTypeCode.ErrorReply301  => "301 — Tiếp nhận & xử lý HĐĐT sai sót",
        _ => ((int)t).ToString()
    };

    // Phạm vi người nhận thông báo (theo TConst.NotifyType của TVAN gốc).
    public static string NotifyScope(NotifyScope t) => t switch
    {
        Models.NotifyScope.AllUser => "Tất cả người dùng",
        _ => t.ToString()
    };

    // Loại nội dung thông báo (theo TConst.NotifyType1 của TVAN gốc).
    public static string NotifyKind(NotifyKind t) => t switch
    {
        Models.NotifyKind.Maintenance => "Bảo trì / hệ thống",
        _ => t.ToString()
    };

    // Kiểu dữ liệu cột hiển thị danh sách hóa đơn (theo Mst_SortColumnInvoice.ColumnType của TVAN gốc).
    public static string SortColumnType(SortColumnType t) => t switch
    {
        Models.SortColumnType.Text => "Chuỗi",
        Models.SortColumnType.Number => "Số",
        Models.SortColumnType.Date => "Ngày tháng",
        _ => t.ToString()
    };

    // Loại đối tượng (chức năng) trong hệ thống (theo Sys_Object.ObjectType của TVAN gốc).
    public static string SysObjectType(SysObjectType t) => t switch
    {
        Models.SysObjectType.Func   => "FUNC — Chức năng",
        Models.SysObjectType.Menu   => "MENU — Menu",
        Models.SysObjectType.Button => "BUTTON — Nút chức năng",
        _ => t.ToString()
    };

    // Hành động của thông điệp trao đổi với CQT (theo Log_TCTTransaction.MessageAction của TVAN gốc).
    public static (string text, string css) TctAction(TctMessageAction a) => a switch
    {
        TctMessageAction.Send    => ("Gửi CQT", "primary"),
        TctMessageAction.Receive => ("Nhận từ CQT", "info"),
        _ => (a.ToString(), "secondary")
    };

    // Trạng thái xử lý của thông điệp trao đổi với CQT (theo Log_TCTTransaction.MessageStatus của TVAN gốc).
    public static (string text, string css) TctStatus(TctMessageStatus s) => s switch
    {
        TctMessageStatus.Success => ("Thành công", "success"),
        TctMessageStatus.Error   => ("Lỗi", "danger"),
        TctMessageStatus.Pending => ("Đang chờ", "warning"),
        _ => (s.ToString(), "secondary")
    };

    // Kết quả của thông điệp trao đổi với CQT (theo Log_TCTTransaction.MessageResult của TVAN gốc).
    public static (string text, string css) TctResult(TctMessageResult r) => r switch
    {
        TctMessageResult.Accept => ("Chấp nhận", "success"),
        TctMessageResult.Reject => ("Từ chối", "danger"),
        _ => ("Chưa có", "secondary")
    };

    // Trạng thái hóa đơn đầu vào (theo Invoice_InvoiceInput.InvoiceStatus của TVAN gốc).
    public static (string text, string css) InputInv(InputInvoiceStatus s) => s switch
    {
        InputInvoiceStatus.Draft   => ("Mới nhập", "secondary"),
        InputInvoiceStatus.Issued  => ("Đã ghi nhận", "success"),
        InputInvoiceStatus.Deleted => ("Đã xóa", "danger"),
        _ => (s.ToString(), "secondary")
    };

    // Trạng thái của một mã sản phẩm / serial (theo Prd_ProductID.ProductIDStatus của TVAN gốc).
    public static (string text, string css) ProductIdStatus(Models.ProductIdStatus s) => s switch
    {
        Models.ProductIdStatus.New    => ("Mới tạo", "secondary"),
        Models.ProductIdStatus.Sold   => ("Đã bán", "success"),
        Models.ProductIdStatus.Locked => ("Đã khóa", "danger"),
        _ => (s.ToString(), "secondary")
    };

    // Trạng thái xử lý của một bản ghi lịch sử đăng ký dịch vụ (theo Hist_RegisterServices.TThai của TVAN gốc).
    public static (string text, string css) RegServiceStatus(Models.RegServiceStatus s) => s switch
    {
        Models.RegServiceStatus.Pending => ("Chờ gửi", "secondary"),
        Models.RegServiceStatus.SentTCT => ("Đã gửi CQT", "info"),
        Models.RegServiceStatus.Receive => ("CQT tiếp nhận", "primary"),
        Models.RegServiceStatus.Accept  => ("CQT chấp nhận", "success"),
        Models.RegServiceStatus.Reject  => ("CQT từ chối", "danger"),
        _ => (s.ToString(), "secondary")
    };

    // Phương thức gửi hóa đơn tới CQT (theo TConst.PTGui của TVAN gốc).
    public static string RegSendMethod(Models.RegSendMethod m) => m switch
    {
        Models.RegSendMethod.Full => "Gửi đầy đủ",
        Models.RegSendMethod.BTH  => "Gửi tổng hợp",
        _ => m.ToString()
    };

    // Loại thao tác nhập hóa đơn từ Excel (theo tham số `type` của Invoice_ImportExcelController.ImportResult của TVAN gốc).
    public static string ImportType(Models.ImportType t) => t switch
    {
        Models.ImportType.Luu        => "Lưu",
        Models.ImportType.LuuVaCapSo => "Lưu và cấp số",
        Models.ImportType.PhatHanh   => "Phát hành",
        _ => t.ToString()
    };

    // Kết quả xử lý của một dòng dữ liệu nhập hóa đơn từ Excel (theo Invoice_ImportExcel.FlagResult của TVAN gốc).
    public static (string text, string css) ImportFlag(Models.ImportFlagResult f) => f switch
    {
        Models.ImportFlagResult.Success => ("Thành công", "success"),
        Models.ImportFlagResult.Fail    => ("Không thành công", "danger"),
        _ => ("Bỏ qua", "secondary")
    };
}
