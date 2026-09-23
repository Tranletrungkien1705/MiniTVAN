namespace MiniTVAN.Services;

// Đọc số tiền thành chữ tiếng Việt (theo clsDocTien của TVAN gốc —
// idN.TVAN.Utils/clsDocTien.cs, dùng bởi Invoice_InvoiceController.DocTien).
// Thuật toán: tách phần nguyên thành các nhóm 3 chữ số (đơn vị/nghìn/triệu/tỷ),
// đọc từng nhóm 3 số rồi ghép lại; phần thập phân đọc theo từng chữ số (VND)
// hoặc theo "xu" (USD). Kết quả viết hoa chữ đầu và kết thúc bằng "./.".
public static class DocTienService
{
    private static readonly string[] ArrSo = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

    // Đọc 3 chữ số (một nhóm). isDau = true khi đây là nhóm cao nhất (bỏ "không trăm").
    private static string Doc3So(string so, bool isDau)
    {
        if (so == "000") return string.Empty;
        int s1 = so[0] - '0', s2 = so[1] - '0', s3 = so[2] - '0';
        var kq = string.Empty;
        if (isDau != true || s1 != 0) kq = ArrSo[s1] + " trăm";
        if (s2 != 0)
        {
            if (s2 == 1) kq += " mười";
            else kq += " " + ArrSo[s2] + " mươi";
        }
        else if (isDau != true || s1 != 0) if (s3 != 0) kq += " linh";
        if (s3 == 1)
        {
            if (s2 == 0 || s2 == 1) kq += " một";
            else kq += " mốt";
        }
        else if (s3 == 4)
        {
            if (s2 == 0 || s2 == 1) kq += " bốn";
            else kq += " tư";
        }
        else if (s3 == 5)
        {
            if (s2 == 0) kq += " năm";
            else kq += " lăm";
        }
        else
        {
            if (s3 != 0) kq += " " + ArrSo[s3];
        }
        return kq.Trim();
    }

    // Đọc 9 chữ số = 3 nhóm 3 số (triệu / nghìn / đơn vị).
    private static string Doc9So(string so, bool isDau)
    {
        var kq = string.Empty;
        var t = Doc3So(so.Substring(0, 3), isDau);
        if (t != string.Empty) { kq = t + " triệu"; isDau = false; }
        t = Doc3So(so.Substring(3, 3), isDau);
        if (t != string.Empty) { kq += " " + t + " nghìn"; isDau = false; }
        kq += " " + Doc3So(so.Substring(6, 3), isDau);
        return kq.Trim();
    }

    // Đọc 1 chữ số (dùng cho phần thập phân của VND).
    private static string Doc1So(char c) => c is >= '0' and <= '9' ? ArrSo[c - '0'] : "";

    // Đọc phần dư (phần thập phân). VND: "phẩy <từng chữ số>"; USD: "và ... xu".
    private static string DocPhanDu(string so, string currencyCode)
    {
        var str = "";
        if (so == "0") return str;
        if (currencyCode == "USD")
        {
            str += " và ";
            var xu = "0";
            var a = "0";
            var length = so.Length;
            if (length >= 3) { a = so.Substring(0, 2); xu = so.Substring(2); }
            else if (length == 1) { a = so + "0"; }
            else { a = so; }
            if (xu != "0")
            {
                var sot1 = FormatLetter(a, 3);
                var sot2 = FormatLetter(xu, 3);
                if (sot1 == "000") str += " không phẩy " + Doc3So(sot2, true) + " xu";
                else str += Doc3So(sot1, true) + " phẩy " + Doc3So(sot2, true) + " xu";
            }
            else
            {
                str += Doc3So(FormatLetter(a, 3), true) + " xu";
            }
        }
        else
        {
            str += " phẩy ";
            for (int i = 0; i < so.Length; i++)
                str += (i == 0 ? "" : " ") + Doc1So(so[i]);
        }
        return str;
    }

    private static string FormatLetter(string so, int soKitu)
    {
        while (so.Length < soKitu) so = "0" + so;
        return so;
    }

    private static string NormalizeSpaces(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s ?? "", "\\s+", " ").Trim();

    // Đọc số tiền thành chữ. currencyName truyền THUẦN ("đồng", "đô la Mỹ");
    // hàm tự thêm dấu cách và hậu tố "./.". Nhận diện USD theo currencyCode == "USD".
    public static string DocSo(string so, string currencyCode, string currencyName)
    {
        so = (so ?? "").Trim();
        if (so.Length == 0) return "";
        var soAm = "";
        if (so[0] == '-') { soAm = "Giảm "; so = so.Substring(1).Trim(); }
        if (so.Length == 0) return "";

        var arr = so.Split('.');
        var intPart = arr[0];
        var fracPart = arr.Length > 1 ? arr[1] : "0";

        intPart = intPart.TrimStart('0');
        if (intPart.Length == 0) intPart = "0";

        var s = intPart;
        while (s.Length % 9 != 0) s = "0" + s;
        int slTy = s.Length / 9;
        var intWords = "";
        for (int i = 0; i < slTy; i++)
        {
            var tempt = i == 0 ? Doc9So(s.Substring(i * 9, 9), true) : Doc9So(s.Substring(i * 9, 9), false);
            if (tempt.Length > 0)
            {
                intWords += " " + tempt;
                for (int j = slTy - 1; j > i; j--) intWords += " tỷ";
            }
        }
        intWords = NormalizeSpaces(intWords);
        if (intWords.Length == 0) intWords = "Không";
        intWords = char.ToUpper(intWords[0]) + intWords.Substring(1);

        var fracText = DocPhanDu(fracPart, currencyCode);
        var core = currencyCode == "USD"
            ? intWords + " " + currencyName + fracText
            : intWords + fracText + " " + currencyName;
        if (soAm.Length > 0) core = soAm + core;
        return NormalizeSpaces(core) + "./.";
    }
}
