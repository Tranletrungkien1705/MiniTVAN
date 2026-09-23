using Microsoft.EntityFrameworkCore;
using MiniTVAN.Models;
using MiniTVAN.Services;
namespace MiniTVAN.Data;

public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await MigratePostgresAsync(db);
        if (!await db.Orgs.AnyAsync(o => o.Id == TenantContext.DefaultOrgId))
        { db.Orgs.Add(new Org { Id = TenantContext.DefaultOrgId, Name = "Demo TVAN", ApiKey = TenantContext.DefaultApiKey }); await db.SaveChangesAsync(); }

        if (!await db.Nnts.AnyAsync())
        {
            var seller = new Nnt { Mst = "0101243150", Name = "Công ty CP Ô tô Đông Đô", Address = "Hà Nội", Email = "kt@dongdo.vn", RegStatus = RegStatus.Registered, RegisteredAt = DateTime.UtcNow.AddDays(-10) };
            var seller2 = new Nnt { Mst = "0312345678", Name = "Công ty TNHH Miền Nam", Address = "TP.HCM", RegStatus = RegStatus.None };
            db.Nnts.AddRange(seller, seller2); await db.SaveChangesAsync();

            var inv1 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000001", BuyerName = "Nguyễn Văn A", BuyerMst = "8012345678", BuyerAddress = "Hà Nội", Amount = 500_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-5), Status = InvoiceStatus.Accepted, TctCode = "0026082512345678", SentAt = DateTime.UtcNow.AddDays(-5) };
            var inv2 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000002", BuyerName = "Trần Thị B", BuyerAddress = "Hải Phòng", Amount = 30_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-1), Status = InvoiceStatus.Draft };
            db.Invoices.AddRange(inv1, inv2); await db.SaveChangesAsync();
            // HĐ điều chỉnh giảm cho HĐ gốc inv1 (minh họa xử lý sai sót)
            var inv3 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000003", BuyerName = "Nguyễn Văn A", BuyerMst = "8012345678", BuyerAddress = "Hà Nội", Amount = 5_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-2), Status = InvoiceStatus.Accepted, TctCode = "0026082612345679", SentAt = DateTime.UtcNow.AddDays(-2), SourceCode = SourceInvoiceCode.Adjust, AdjType = InvoiceAdjType.Decrease, RefInvoiceId = inv1.Id, RefTctCode = inv1.TctCode, AdjReason = "Giảm giá theo phụ lục hợp đồng" };
            db.Invoices.Add(inv3); await db.SaveChangesAsync();
            // Gửi email hóa đơn cho người mua (theo Invoice_Invoice_Support_SendMail của TVAN gốc):
            // inv1 đã phát hành → đã gửi email cho người mua.
            inv1.EmailSend = "nguyenvana@congty.vn";
            inv1.SendEmailDTimeUTC = DateTime.UtcNow.AddDays(-5);
            inv1.SendEmailBy = "kế toán";
            db.InvoiceEmailLogs.Add(new InvoiceEmailLog
            {
                InvoiceId = inv1.Id, ToEmail = "nguyenvana@congty.vn",
                Subject = $"Hóa đơn điện tử {inv1.Symbol}-{inv1.No} — {seller.Name}",
                Result = EmailSendResult.Success, SentBy = "kế toán",
                Message = "Đã gửi email hóa đơn tới nguyenvana@congty.vn.", CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            await db.SaveChangesAsync();
            // Xóa hóa đơn đã phát hành (theo Invoice_Invoice_Deleted của TVAN gốc):
            // inv4 đã phát hành nhưng bị xóa (DELETED) kèm lý do & người xóa.
            var inv4 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000004", BuyerName = "Lê Văn C", BuyerAddress = "Đà Nẵng", Amount = 8_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-3), Status = InvoiceStatus.Deleted, TctCode = "0026082712345680", SentAt = DateTime.UtcNow.AddDays(-3), DeleteDTimeUTC = DateTime.UtcNow.AddDays(-2), DeleteBy = "kế toán", Remark = "Lập sai thông tin người mua" };
            db.Invoices.Add(inv4); await db.SaveChangesAsync();
            // In chuyển đổi hóa đơn (theo Invoice_Invoice.FlagChange của TVAN gốc):
            // inv1 đã được in ở dạng chuyển đổi cho người mua.
            inv1.FlagChange = ConversionPrintFlag.Printed;
            db.ConversionPrintLogs.Add(new ConversionPrintLog
            {
                InvoiceId = inv1.Id, Action = ConversionPrintAction.Print, By = "kế toán",
                Note = "In bản chuyển đổi giao khách", CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            await db.SaveChangesAsync();
            // Ngày ký hóa đơn (theo Invoice_Invoice.SignedDate của TVAN gốc): inv1 ký trong hạn 60 ngày.
            inv1.SignedDate = DateTime.UtcNow.AddDays(-5);
            await db.SaveChangesAsync();
            // Ký lại hóa đơn (theo Invoice_Invoice_ReSign của TVAN gốc): inv1 đã được ký lại.
            inv1.InvoiceFileSpec = "PD94bWwgdmVyc2lvbj0iMS4wIj8+PEhEPg==";
            inv1.InvoiceFilePath = $"{DateTime.Today:yyyy-MM-dd}/demo.KyLaiHoaDon.xml";
            inv1.FlagHotfix = HotfixFlag.Hotfixed;
            inv1.ApprDTimeUTC = DateTime.UtcNow.AddDays(-4);
            inv1.ApprBy = "kế toán";
            db.ReSignLogs.Add(new ReSignLog
            {
                InvoiceId = inv1.Id, FilePath = inv1.InvoiceFilePath, By = "kế toán",
                Note = "Ký lại do lỗi chữ ký", CreatedAt = DateTime.UtcNow.AddDays(-4)
            });
            await db.SaveChangesAsync();
            // Duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc): inv1 đã được duyệt trước khi phát hành.
            db.ApproveLogs.Add(new ApproveLog
            {
                InvoiceId = inv1.Id, Action = ApproveAction.Approve,
                FilePath = $"{DateTime.Today.AddDays(-5):yyyy-MM-dd}/HD00000001.xml",
                PdfFilePath = $"{DateTime.Today.AddDays(-5):yyyy-MM-dd}/HD00000001.pdf",
                By = "kế toán trưởng", Note = "Duyệt phát hành", CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            await db.SaveChangesAsync();
            // Duyệt NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_ApprovedMulti của TVAN gốc):
            // minh họa 1 lần duyệt hàng loạt 2 HĐ đang chờ đã có số.
            db.BulkApproveLogs.Add(new BulkApproveLog
            {
                Action = BulkApproveAction.BulkApprove, ApprovedCount = 2,
                InvoiceNos = $"{inv1.Symbol}-{inv1.No}, {inv3.Symbol}-{inv3.No}",
                Note = "Duyệt lô phát hành đầu kỳ", By = "kế toán trưởng", CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            await db.SaveChangesAsync();
            // Xóa NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_DeleteMulti của TVAN gốc):
            // minh họa 1 lần xóa hàng loạt 2 HĐ đã phát hành đã có số.
            db.BulkDeleteLogs.Add(new BulkDeleteLog
            {
                Action = BulkDeleteAction.BulkDelete, DeletedCount = 2,
                InvoiceNos = $"{inv1.Symbol}-{inv1.No}, {inv3.Symbol}-{inv3.No}",
                Note = "Xóa lô hóa đơn lập sai", By = "kế toán trưởng", CreatedAt = DateTime.UtcNow.AddDays(-4)
            });
            await db.SaveChangesAsync();
            // Phát hành hóa đơn (theo Invoice_Invoice_Issued của TVAN gốc): inv1 đã được phát hành (ISSUED) sau khi duyệt.
            inv1.IssuedDTimeUTC = DateTime.UtcNow.AddDays(-5);
            inv1.IssuedBy = "kế toán";
            db.IssueLogs.Add(new IssueLog
            {
                InvoiceId = inv1.Id, Action = IssueAction.Issue,
                EmailSend = inv1.EmailSend, By = "kế toán",
                Note = "Phát hành gửi khách", CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            await db.SaveChangesAsync();
            // Nhận kết quả phản hồi từ CQT (theo Invoice_Invoice_TCTReceive của TVAN gốc):
            // inv1 đã được CQT chấp nhận phát hành (thông điệp 202) với mã xác thực CQT.
            inv1.MltDiep = "202";
            inv1.TctChapNhan = TctAcceptStatus.Accept;
            inv1.TctReceiveDTimeUTC = DateTime.UtcNow.AddDays(-5);
            db.TctReceiveLogs.Add(new TctReceiveLog
            {
                InvoiceId = inv1.Id, MltDiep = TctMessageType.Success202, ChapNhan = TctAcceptStatus.Accept,
                MaCQT = inv1.TctCode, Message = $"CQT chấp nhận phát hành HĐ {inv1.Symbol}-{inv1.No}. Mã tra cứu: {inv1.TctCode}",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
            await db.SaveChangesAsync();
            // Gửi thông báo hóa đơn sai sót tới CQT (theo Invoice_Invoice_SentTCT_300 của TVAN gốc):
            // inv1 đã được CQT chấp nhận → đã gửi thông báo 300 (04/SS) báo sai sót, lưu mã V + cờ điều chỉnh.
            inv1.TCTSuaDoiRefNo = $"V{DateTime.UtcNow.AddDays(-4):yyyyMMddHHmmss}{inv1.Id:D4}";
            inv1.FlagReplaceOrAdjust = ReplaceOrAdjustFlag.Adjust;
            inv1.FlagSuaDoi = SuaDoiFlag.Sent;
            db.Tct300Logs.Add(new Tct300Log
            {
                InvoiceId = inv1.Id, TCTRefNo = inv1.TCTSuaDoiRefNo, FlagReplaceOrAdjust = ReplaceOrAdjustFlag.Adjust,
                LoaiTB = "1", SoTB = "04/SS", NgayTB = DateTime.Today.AddDays(-4), LyDo = "Sai MST người mua",
                Message = $"Đã gửi thông báo hóa đơn sai sót (300) cho HĐ {inv1.Symbol}-{inv1.No}. Mã V: {inv1.TCTSuaDoiRefNo}",
                By = "kế toán", CreatedAt = DateTime.UtcNow.AddDays(-4)
            });
            await db.SaveChangesAsync();
            // Cập nhật nội dung hóa đơn sau khi đã cấp số (theo Invoice_Invoice_UpdAfterAllocated của TVAN gốc):
            // inv2 đang chờ (Draft) và đã có số → đã được cập nhật lại thông tin người mua.
            inv2.PaymentMethod = PaymentMethod.Transfer;
            db.InvoiceUpdateLogs.Add(new InvoiceUpdateLog
            {
                InvoiceId = inv2.Id, BuyerName = inv2.BuyerName, BuyerMst = inv2.BuyerMst, BuyerAddress = inv2.BuyerAddress,
                PaymentMethod = PaymentMethod.Transfer, Amount = inv2.Amount, VatRate = inv2.VatRate, InvoiceDate = inv2.IssuedDate,
                Note = "Sửa sai thông tin người mua sau khi cấp số", By = "kế toán", CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
            // Hủy hóa đơn (theo Invoice_Invoice_Cancel của TVAN gốc):
            // inv5 đang chờ (Draft) và đã có số → đã được hủy (CANCELED) kèm lý do & người hủy.
            var inv5 = new Invoice { NntId = seller.Id, Symbol = "1C26TAA", No = "00000005", BuyerName = "Phạm Thị D", BuyerAddress = "Cần Thơ", Amount = 12_000_000, VatRate = 10, IssuedDate = DateTime.Today.AddDays(-1), Status = InvoiceStatus.Cancelled, CancelDTimeUTC = DateTime.UtcNow.AddHours(-6), CancelBy = "kế toán", Remark = "Lập sai thông tin người mua" };
            db.Invoices.Add(inv5); await db.SaveChangesAsync();
            db.CancelInvoiceLogs.Add(new CancelInvoiceLog
            {
                InvoiceId = inv5.Id, Action = CancelAction.Cancel, Remark = inv5.Remark, By = "kế toán",
                CreatedAt = DateTime.UtcNow.AddHours(-6)
            });
            await db.SaveChangesAsync();
            // Tạo biên bản đính kèm hóa đơn (theo luồng TaoBienBan của TVAN gốc):
            // inv5 đã hủy → đã tạo biên bản hủy kèm file + lý do.
            inv5.AttachedDelFileName = "MauBienBanHuyHoaDon.docx";
            inv5.AttachedDelFileSpec = "UEsDBBQABgAIAAAAIQ==";
            inv5.AttachedDelFilePath = $"{DateTime.Today:yyyy-MM-dd}/MauBienBanHuyHoaDon.docx";
            inv5.DeleteReason = "Lập sai thông tin người mua";
            db.InvoiceRecordLogs.Add(new InvoiceRecordLog
            {
                InvoiceId = inv5.Id, Type = RecordType.Huy, FileName = inv5.AttachedDelFileName,
                FileSpec = inv5.AttachedDelFileSpec, FilePath = inv5.AttachedDelFilePath,
                Reason = inv5.DeleteReason, By = "kế toán", CreatedAt = DateTime.UtcNow.AddHours(-6)
            });
            await db.SaveChangesAsync();
            db.Messages.AddRange(
                new TranMessage { InvoiceId = inv1.Id, NntId = seller.Id, Type = MsgType.SendInvoice, Dir = MsgDir.Out, Code = "300", Text = "Gửi HĐ 1C26TAA-00000001", CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new TranMessage { InvoiceId = inv1.Id, NntId = seller.Id, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = "202", Text = "TCT cấp mã: 0026082512345678", CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new TranMessage { InvoiceId = inv3.Id, NntId = seller.Id, Type = MsgType.AdjustInvoice, Dir = MsgDir.Out, Code = "300", Text = "Gửi HĐ điều chỉnh giảm cho 1C26TAA-00000001", CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new TranMessage { InvoiceId = inv3.Id, NntId = seller.Id, Type = MsgType.AdjustInvoice, Dir = MsgDir.In, Code = "202", Text = "TCT cấp mã: 0026082612345679", CreatedAt = DateTime.UtcNow.AddDays(-2) });
            await db.SaveChangesAsync();

            // Hạn mức hóa đơn (theo Invoice_license của TVAN gốc): seller đã được cấp 1000 HĐ, đã phát hành 3.
            var lic = new InvoiceLicense { NntId = seller.Id, TotalQty = 1000, TotalQtyIssued = 3, TotalQtyUsed = 2, UpdatedAt = DateTime.UtcNow.AddDays(-2) };
            db.Licenses.Add(lic); await db.SaveChangesAsync();
            db.LicenseHists.AddRange(
                new LicenseHist { NntId = seller.Id, Type = LicenseHistType.Create, Qty = 1000, TotalQtyAfter = 1000, Note = "Cấp hạn mức ban đầu", CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new LicenseHist { NntId = seller.Id, Type = LicenseHistType.Increase, Qty = 0, TotalQtyAfter = 1000, Note = "Khởi tạo demo", CreatedAt = DateTime.UtcNow.AddDays(-2) });
            await db.SaveChangesAsync();

            // Bảng tổng hợp dữ liệu HĐĐT gửi CQT (theo Mst_GuiTongHop của TVAN gốc):
            // gom các HĐ đã được CQT chấp nhận của seller trong kỳ tháng hiện tại.
            var gth = new GuiTongHop
            {
                NntId = seller.Id, LKDLieu = PeriodType.Month, KDLieu = DateTime.Today.ToString("yyyy-MM"),
                BSLThu = 0, LDau = true, TNNT = seller.Name, MST = seller.Mst, NLap = DateTime.Today,
                SBTHDLieu = $"{seller.Mst}-{DateTime.Today:yyyy-MM}-0", Status = GthStatus.Draft
            };
            int stt = 1;
            foreach (var i in new[] { inv1, inv3 })
            {
                gth.Details.Add(new GuiTongHopDtl
                {
                    STT = stt++, InvoiceCode = i.TctCode ?? "", KHMSHDon = "01GTKT", KHHDon = i.Symbol, SHDon = i.No,
                    NLap = i.IssuedDate, TNMua = i.BuyerName, MSTNMua = i.BuyerMst, THHDVu = "Hàng hóa, dịch vụ",
                    DVTinh = "Lần", SLuong = 1, TTCThue = i.Amount, TSuat = i.VatRate, TgTThue = i.VatAmount, TgTTToan = i.Total
                });
            }
            db.GuiTongHops.Add(gth); await db.SaveChangesAsync();
        }

        // Danh mục cơ quan thuế (theo Mst_GovTaxID của TVAN gốc) + nhật ký tra cứu MST demo.
        if (!await db.TaxOffices.AnyAsync())
        {
            db.TaxOffices.AddRange(
                new TaxOffice { GovTaxID = "0100231226", GovTaxName = "Tổng cục Thuế", Level = "0", Address = "Hà Nội", ContactEmail = "tct@gdt.gov.vn", ContactPhone = "024 3825 0000", FlagActive = true, UpdatedBy = "quản trị" },
                new TaxOffice { GovTaxID = "0101", GovTaxIDParent = "0100231226", ProvinceCode = "01", DistrictCode = "0101", GovTaxName = "Cục Thuế TP Hà Nội", Level = "1", Address = "Hà Nội", ContactEmail = "hanoi@gdt.gov.vn", ContactPhone = "024 3825 0000", FlagActive = true, UpdatedBy = "quản trị" },
                new TaxOffice { GovTaxID = "0301", GovTaxIDParent = "0100231226", ProvinceCode = "79", DistrictCode = "7901", GovTaxName = "Cục Thuế TP Hồ Chí Minh", Level = "1", Address = "TP.HCM", ContactEmail = "hcm@gdt.gov.vn", ContactPhone = "028 3829 0000", FlagActive = true, UpdatedBy = "quản trị" });
            await db.SaveChangesAsync();

            // Tính lại mã đơn vị nghiệp vụ/mẫu/cấp cho cây CQT (theo Mst_GovTaxID_UpdBU của TVAN gốc).
            const string root = "0100231226";
            var allOffices = await db.TaxOffices.ToListAsync();
            var byCode = allOffices.ToDictionary(t => t.GovTaxID, StringComparer.OrdinalIgnoreCase);
            for (int pass = 0; pass < 7; pass++)
            {
                foreach (var t in allOffices)
                {
                    if (string.Equals(t.GovTaxID, root, StringComparison.OrdinalIgnoreCase)) { t.GovTaxIDBUCode = root; t.GovTaxIDBUPattern = root + "%"; t.GovTaxIDLevel = 0; continue; }
                    TaxOffice? parent = null;
                    if (!string.IsNullOrWhiteSpace(t.GovTaxIDParent)) byCode.TryGetValue(t.GovTaxIDParent!, out parent);
                    var parentBu = parent?.GovTaxIDBUCode;
                    t.GovTaxIDBUCode = (string.IsNullOrEmpty(parentBu) ? "" : parentBu + ".") + t.GovTaxID;
                    t.GovTaxIDBUPattern = t.GovTaxIDBUCode + "%";
                    t.GovTaxIDLevel = (parent?.GovTaxIDLevel ?? 0) + 1;
                }
            }
            await db.SaveChangesAsync();
        }
        if (!await db.NntLookupLogs.AnyAsync())
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            db.NntLookupLogs.Add(new NntLookupLog
            {
                Mst = "0101243150", Result = LookupResult.Success,
                FullName = seller?.Name ?? "Công ty CP Ô tô Đông Đô", Address = seller?.Address,
                GovTaxID = "0101", GovTaxName = "Cục Thuế TP Hà Nội",
                Message = "Tra cứu thành công từ cơ quan thuế.", CreatedAt = DateTime.UtcNow.AddDays(-3)
            });
            await db.SaveChangesAsync();
        }

        // Cấu hình hệ thống (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc): mặc định bật kiểm tra ký >60 ngày.
        if (!await db.SystemSettings.AnyAsync())
        {
            db.SystemSettings.Add(new SystemSetting { Sign60Day = Sign60DayFlag.Check, Note = "Mặc định bật kiểm tra ký >60 ngày" });
            await db.SaveChangesAsync();
        }

        // Cấu hình dấu phân cách động (theo Mst_DynamicComma của TVAN gốc):
        // tổ chức demo dùng dấu phẩy ',' khi hiển thị số trên hóa đơn.
        if (!await db.DynamicCommas.AnyAsync())
        {
            db.DynamicCommas.Add(new DynamicComma { FlagStyle = DynamicCommaStyle.Comma, UpdatedBy = "kế toán", UpdatedAt = DateTime.UtcNow.AddDays(-2) });
            await db.SaveChangesAsync();
        }

        // Mẫu hóa đơn (theo Invoice_TempInvoice của TVAN gốc): seller có mẫu 1C26TAA dải số 1..1000,
        // đã cấp tới số 00000003 (khớp 3 HĐ demo) + nhật ký cấp số cho HĐ gần nhất.
        if (!await db.InvoiceTemplates.AnyAsync())
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            if (seller != null)
            {
                var tpl = new InvoiceTemplate
                {
                    NntId = seller.Id, TInvoiceCode = "TINV-1C26TAA", TInvoiceName = "Hóa đơn GTGT 1C26TAA",
                    FormNo = "1C26TAA", Sign = "K26TAA", TTType = InvoiceNoRule.TT78,
                    EffDateStart = DateTime.Today.AddDays(-30), StartInvoiceNo = 1, EndInvoiceNo = 1000,
                    LastInvoiceNo = "00000003", QtyUsed = 3, LastInvoiceDateUTC = DateTime.Today.AddDays(-1),
                    TInvoiceStatus = TemplateStatus.Issued, FlagActive = true,
                    // Thông tin liên hệ của NNT in trên mẫu hóa đơn
                    // (theo Invoice_TempInvoice_SupportUpdEmailAndAddress của TVAN gốc).
                    NNTName = seller.Name, NNTAddress = seller.Address, NNTPhone = "024 3825 0000",
                    NNTEmail = seller.Email, NNTWebsite = "https://dongdo.vn", FlagStyleComma = true,
                    // Số tài khoản & tên ngân hàng của NNT in trên mẫu hóa đơn
                    // (theo Invoice_TempInvoice_SupportUpdAccNoAndBankName của TVAN gốc).
                    NNTAccNo = "1234567890", NNTBankName = "Vietcombank - CN Hà Nội",
                    ContactUpdatedAt = DateTime.UtcNow.AddDays(-2), ContactUpdatedBy = "kế toán"
                };
                db.InvoiceTemplates.Add(tpl); await db.SaveChangesAsync();

                var lastInv = await db.Invoices.OrderByDescending(i => i.Id).FirstOrDefaultAsync(i => i.NntId == seller.Id);
                if (lastInv != null)
                {
                    db.InvoiceNoAllocLogs.Add(new InvoiceNoAllocLog
                    {
                        InvoiceId = lastInv.Id, TemplateId = tpl.Id, FormNo = tpl.FormNo, Sign = tpl.Sign,
                        InvoiceNo = lastInv.No, InvoiceDate = lastInv.IssuedDate, By = "kế toán",
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    });
                    await db.SaveChangesAsync();
                }

                // Mẫu hóa đơn mới ở trạng thái chờ (Draft/PENDING) — minh họa phát hành mẫu
                // (theo Invoice_TempInvoice_Issued của TVAN gốc): chưa có hiệu lực, chờ phát hành.
                db.InvoiceTemplates.Add(new InvoiceTemplate
                {
                    NntId = seller.Id, TInvoiceCode = "TINV-1C26TAB", TInvoiceName = "Hóa đơn GTGT 1C26TAB",
                    FormNo = "1C26TAB", Sign = "K26TAB", TTType = InvoiceNoRule.TT78,
                    EffDateStart = DateTime.Today, StartInvoiceNo = 1, EndInvoiceNo = 500,
                    QtyUsed = 0, TInvoiceStatus = TemplateStatus.Draft, FlagActive = true
                });
                await db.SaveChangesAsync();

                // Mở rộng dải số mẫu hóa đơn (theo Invoice_TempInvoice_IncreaseEndInvoiceNo của TVAN gốc):
                // mẫu 1C26TAA đã được tăng số cuối từ 1000 lên 2000.
                db.TemplateRangeLogs.Add(new TemplateRangeLog
                {
                    TemplateId = tpl.Id, Action = TemplateRangeAction.IncreaseEndNo,
                    OldEndInvoiceNo = 1000, NewEndInvoiceNo = 2000,
                    Remark = "Mở rộng dải số theo đề nghị NNT", By = "kế toán",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                });
                await db.SaveChangesAsync();

                // Cập nhật lại CẢ dải số mẫu hóa đơn đang chờ (theo Invoice_TempInvoice_UpdQtyInvoiceNo của TVAN gốc):
                // mẫu 1C26TAB (Draft) đã được điều chỉnh dải số từ 1..500 thành 1..800.
                var draftTpl = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.TInvoiceCode == "TINV-1C26TAB");
                if (draftTpl != null)
                {
                    db.TemplateRangeLogs.Add(new TemplateRangeLog
                    {
                        TemplateId = draftTpl.Id, Action = TemplateRangeAction.UpdateQtyNo,
                        OldStartInvoiceNo = 1, NewStartInvoiceNo = 1,
                        OldEndInvoiceNo = 500, NewEndInvoiceNo = 800,
                        Remark = "Điều chỉnh dải số theo đề nghị NNT", By = "kế toán",
                        CreatedAt = DateTime.UtcNow.AddHours(-12)
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        // Hóa đơn khởi tạo từ MÁY TÍNH TIỀN (theo Invoice_Invoice_AllocatedInvoiceTypeM / GenMCCQTMTTTypeM của TVAN gốc):
        // seller được CQT cấp mã máy tính tiền (MCCQT), có mẫu loại MTT (FormNo ký tự thứ 4 = 'M')
        // và 1 HĐ MTT đang chờ đã cấp số + đã sinh mã CQT máy tính tiền (MCCQTMTT).
        if (!await db.InvoiceTemplates.AnyAsync(t => t.FormNo == "1C2MAA"))
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            if (seller != null)
            {
                seller.MCCQT = "A1B2C";   // mã CQT cấp cho máy tính tiền (5 ký tự)
                await db.SaveChangesAsync();

                var mttTpl = new InvoiceTemplate
                {
                    NntId = seller.Id, TInvoiceCode = "TINV-1C2MAA", TInvoiceName = "Hóa đơn MTT 1C2MAA",
                    FormNo = "1C2MAA", Sign = "2", TTType = InvoiceNoRule.TT78,
                    EffDateStart = DateTime.Today.AddDays(-30), StartInvoiceNo = 1, EndInvoiceNo = 1000,
                    LastInvoiceNo = "00000001", QtyUsed = 1, LastInvoiceDateUTC = DateTime.Today,
                    TInvoiceStatus = TemplateStatus.Issued, FlagActive = true
                };
                db.InvoiceTemplates.Add(mttTpl); await db.SaveChangesAsync();

                var mttInv = new Invoice
                {
                    NntId = seller.Id, Symbol = "1C2MAA", No = "00000001", BuyerName = "Khách lẻ",
                    Amount = 2_000_000, VatRate = 10, IssuedDate = DateTime.Today, Status = InvoiceStatus.Draft,
                    MCCQTMTT = $"M2-{DateTime.Now:yy}-A1B2C-{DateTime.Now:MMdd}0000001",
                    InvoiceNoDTimeUTC = DateTime.UtcNow, InvoiceNoBy = "kế toán"
                };
                db.Invoices.Add(mttInv); await db.SaveChangesAsync();

                db.InvoiceNoAllocLogs.Add(new InvoiceNoAllocLog
                {
                    InvoiceId = mttInv.Id, TemplateId = mttTpl.Id, FormNo = mttTpl.FormNo, Sign = mttTpl.Sign,
                    InvoiceNo = mttInv.No, InvoiceDate = mttInv.IssuedDate, By = "kế toán",
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        // Gửi mẫu hóa đơn tới CQT (theo Invoice_TempInvoice_SentTCT của TVAN gốc):
        // seller có mẫu 1C26TAC đã gửi CQT (SENTTCT) kèm mã V tham chiếu + nhật ký gửi CQT.
        if (!await db.InvoiceTemplates.AnyAsync(t => t.FormNo == "1C26TAC"))
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            if (seller != null)
            {
                var sentTpl = new InvoiceTemplate
                {
                    NntId = seller.Id, TInvoiceCode = "TINV-1C26TAC", TInvoiceName = "Hóa đơn GTGT 1C26TAC",
                    FormNo = "1C26TAC", Sign = "K26TAC", TTType = InvoiceNoRule.TT78,
                    EffDateStart = DateTime.Today, StartInvoiceNo = 1, EndInvoiceNo = 500,
                    QtyUsed = 0, TInvoiceStatus = TemplateStatus.SentTct, FlagActive = true,
                    TCTRefNo = "V" + DateTime.Now.AddDays(-1).ToString("yyMMddHHmmss"),
                    TCTMessage = "CQT đã tiếp nhận mẫu hóa đơn, chờ phát hành.",
                    SentTCTDTime = DateTime.UtcNow.AddDays(-1), SentTCTBy = "kế toán"
                };
                db.InvoiceTemplates.Add(sentTpl); await db.SaveChangesAsync();

                db.TemplateTctLogs.Add(new TemplateTctLog
                {
                    TemplateId = sentTpl.Id, Action = TemplateTctAction.SendTct, TCTRefNo = sentTpl.TCTRefNo,
                    Message = sentTpl.TCTMessage, Remark = "Gửi đăng ký mẫu theo thông báo", By = "kế toán",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                });
                await db.SaveChangesAsync();
            }
        }

        // Hủy mẫu hóa đơn (theo Invoice_TempInvoice_Cancel của TVAN gốc):
        // seller có mẫu 1C26TAD đã bị hủy (CANCEL) kèm số lượng hủy + người hủy.
        if (!await db.InvoiceTemplates.AnyAsync(t => t.FormNo == "1C26TAD"))
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            if (seller != null)
            {
                db.InvoiceTemplates.Add(new InvoiceTemplate
                {
                    NntId = seller.Id, TInvoiceCode = "TINV-1C26TAD", TInvoiceName = "Hóa đơn GTGT 1C26TAD",
                    FormNo = "1C26TAD", Sign = "K26TAD", TTType = InvoiceNoRule.TT78,
                    EffDateStart = DateTime.Today.AddDays(-60), StartInvoiceNo = 1, EndInvoiceNo = 500,
                    QtyUsed = 20, TInvoiceStatus = TemplateStatus.Cancel, FlagActive = false,
                    QtyCancel = 480, CancelDTimeUTC = DateTime.UtcNow.AddDays(-5), CancelBy = "kế toán"
                });
                await db.SaveChangesAsync();
            }
        }

        // Tạo mẫu hóa đơn mới (theo Invoice_TempInvoice_Save của TVAN gốc):
        // seller có mẫu 1C26TAE vừa được tạo ở trạng thái nháp (PENDING) với dải số rỗng chờ cấp phát.
        if (!await db.InvoiceTemplates.AnyAsync(t => t.FormNo == "1C26TAE"))
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            if (seller != null)
            {
                db.InvoiceTemplates.Add(new InvoiceTemplate
                {
                    NntId = seller.Id, TInvoiceCode = "TINV-1C26TAE", TInvoiceName = "Hóa đơn GTGT 1C26TAE",
                    FormNo = "1C26TAE", Sign = "K26TAE", TTType = InvoiceNoRule.TT78,
                    EffDateStart = DateTime.Today, StartInvoiceNo = 0, EndInvoiceNo = 0,
                    QtyUsed = 0, TInvoiceStatus = TemplateStatus.Draft, FlagActive = true
                });
                await db.SaveChangesAsync();
            }
        }

        // Trường tùy chỉnh hóa đơn (theo Invoice_CustomField / Invoice_DtlCustomField của TVAN gốc):
        // tổ chức demo định nghĩa 2 trường trên hóa đơn + 1 trường trên danh sách hàng hóa.
        if (!await db.InvoiceCustomFields.AnyAsync())
        {
            db.InvoiceCustomFields.AddRange(
                new InvoiceCustomField { InvoiceCustomFieldCode = "InvCF1", InvoiceCustomFieldName = "Số hợp đồng", DBPhysicalType = DBPhysicalType.Text, FlagActive = true, UpdatedBy = "kế toán" },
                new InvoiceCustomField { InvoiceCustomFieldCode = "InvCF2", InvoiceCustomFieldName = "Mã dự án", DBPhysicalType = DBPhysicalType.Text, FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }
        if (!await db.InvoiceDtlCustomFields.AnyAsync())
        {
            db.InvoiceDtlCustomFields.Add(
                new InvoiceDtlCustomField { InvoiceDtlCustomFieldCode = "InvDCF1", InvoiceDtlCustomFieldName = "Mã kho", DBPhysicalType = DBPhysicalType.Text, FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Nhóm mẫu hóa đơn (theo Invoice_TempGroup của TVAN gốc): seller có 1 nhóm mẫu 1VAT
        // kèm danh sách trường động hiển thị trên hóa đơn.
        if (!await db.InvoiceTempGroups.AnyAsync())
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            if (seller != null)
            {
                var grp = new InvoiceTempGroup
                {
                    InvoiceTGroupCode = "MAU1VAT", MST = seller.Mst, VATType = VATType.OneVat,
                    InvoiceTGroupName = "Mẫu hóa đơn 1VAT (không có QR code)",
                    InvoiceTGroupBody = "<div id=\"divTemp\"><h3 id=\"Temp_TInvoiceName\">HÓA ĐƠN GIÁ TRỊ GIA TĂNG</h3></div>",
                    FilePathThumbnail = "/Images/mau1vat.png", SpecPrdType = SpecPrdType.Spec,
                    FlagActive = true, UpdatedBy = "kế toán"
                };
                grp.Fields.Add(new InvoiceTempGroupField { DBFieldName = "Temp_NameSale", TCFType = "TEXT", FlagActive = true });
                grp.Fields.Add(new InvoiceTempGroupField { DBFieldName = "Temp_MSTSale", TCFType = "TEXT", FlagActive = true });
                db.InvoiceTempGroups.Add(grp); await db.SaveChangesAsync();
            }
        }

        // Mẫu thông điệp/thông báo gửi CQT (theo Mst_MessageTemplate của TVAN gốc):
        // tổ chức demo khai báo 2 mẫu thông điệp (100 đăng ký HĐĐT, 300 HĐĐT sai sót).
        if (!await db.MessageTemplates.AnyAsync())
        {
            db.MessageTemplates.AddRange(
                new MessageTemplate
                {
                    MessageTplCode = "TPL100", MessageTplName = "100 - Thông điệp gửi tờ khai đăng ký/thay đổi thông tin sử dụng hóa đơn điện tử",
                    MessageTypeCode = MessageTypeCode.Register100,
                    MessageTplContent = "{\"Title\":\"Tờ khai đăng ký sử dụng HĐĐT\",\"MST\":\"{MST}\",\"TenNNT\":\"{TenNNT}\"}",
                    MessageTplFileName = "mau100.rtmpl", MessageTplFilePath = $"{DateTime.Today:yyyy-MM-dd}/mau100.rtmpl",
                    FlagActive = true, UpdatedBy = "kế toán"
                },
                new MessageTemplate
                {
                    MessageTplCode = "TPL300", MessageTplName = "300 - Thông điệp thông báo về hóa đơn điện tử đã lập có sai sót",
                    MessageTypeCode = MessageTypeCode.Error300,
                    MessageTplContent = "{\"Title\":\"Thông báo HĐĐT sai sót\",\"SoHoaDon\":\"{SoHoaDon}\",\"LyDo\":\"{LyDo}\"}",
                    FlagActive = true, UpdatedBy = "kế toán"
                });
            await db.SaveChangesAsync();
        }

        // Danh mục khách hàng / người mua (theo Mst_CustomerNNT của TVAN gốc):
        // seller có 2 khách hàng demo để chọn nhanh khi lập hóa đơn.
        if (!await db.CustomerNnts.AnyAsync())
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            if (seller != null)
            {
                db.CustomerNnts.AddRange(
                    new CustomerNnt
                    {
                        MST = seller.Mst, CustomerNNTCode = "KH001", CustomerNNTName = "Công ty TNHH Thương mại An Phát",
                        CustomerMST = "8012345678", CustomerNNTType = "Doanh nghiệp", CustomerNNTAddress = "Số 12 Lê Lợi, Hà Nội",
                        CustomerNNTEmail = "ketoan@anphat.vn", CustomerNNTPhone = "024 3933 1122", ContactName = "Nguyễn Văn A",
                        ContactPhone = "0912 345 678", ContactEmail = "a.nguyen@anphat.vn", ProvinceCode = "01", DistrictCode = "0101",
                        AccNo = "1234567890", BankName = "Vietcombank - CN Hà Nội", GovIDType = "CCCD", GovID = "001090012345",
                        Remark = "Khách hàng thân thiết", FlagActive = true, UpdatedBy = "kế toán"
                    },
                    new CustomerNnt
                    {
                        MST = seller.Mst, CustomerNNTCode = "KH002", CustomerNNTName = "Công ty CP Dịch vụ Miền Nam",
                        CustomerMST = "0312345678", CustomerNNTType = "Doanh nghiệp", CustomerNNTAddress = "Số 45 Nguyễn Huệ, TP.HCM",
                        CustomerNNTEmail = "info@miennam.vn", CustomerNNTPhone = "028 3822 3344", ContactName = "Trần Thị B",
                        ContactPhone = "0987 654 321", ProvinceCode = "79", DistrictCode = "7901",
                        AccNo = "9876543210", BankName = "BIDV - CN Sài Gòn", FlagActive = true, UpdatedBy = "kế toán"
                    });
                await db.SaveChangesAsync();
            }
        }

        // Danh mục loại người nộp thuế (theo Mst_NNTType của TVAN gốc):
        // các loại NNT demo dùng khi đăng ký NNT.
        if (!await db.NntTypes.AnyAsync())
        {
            db.NntTypes.AddRange(
                new NntType { NNTType = "DN", NNTTypeName = "Doanh nghiệp", FlagActive = true, UpdatedBy = "kế toán" },
                new NntType { NNTType = "HKD", NNTTypeName = "Hộ kinh doanh", FlagActive = true, UpdatedBy = "kế toán" },
                new NntType { NNTType = "CN", NNTTypeName = "Cá nhân", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục thuế suất VAT (theo Mst_VATRate của TVAN gốc):
        // các mức thuế suất demo dùng khi lập hóa đơn (mã theo Client_Mst_VATRate của TVAN gốc).
        if (!await db.VatRates.AnyAsync())
        {
            db.VatRates.AddRange(
                new VatRate { VATRateCode = "VAT0", VATRate = "0%", VATDesc = "Thuế suất GTGT 0%", FlagActive = true, UpdatedBy = "kế toán" },
                new VatRate { VATRateCode = "VAT5", VATRate = "5%", VATDesc = "Thuế suất GTGT 5%", FlagActive = true, UpdatedBy = "kế toán" },
                new VatRate { VATRateCode = "VAT8", VATRate = "8%", VATDesc = "Thuế suất GTGT 8%", FlagActive = true, UpdatedBy = "kế toán" },
                new VatRate { VATRateCode = "VAT10", VATRate = "10%", VATDesc = "Thuế suất GTGT 10%", FlagActive = true, UpdatedBy = "kế toán" },
                new VatRate { VATRateCode = "KCT", VATRate = "KCT", VATDesc = "Không chịu thuế", FlagActive = true, UpdatedBy = "kế toán" },
                new VatRate { VATRateCode = "KKKNT", VATRate = "KKKNT", VATDesc = "Không kê khai nộp thuế", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục đơn vị tính (theo Mst_Unit của TVAN gốc):
        // các đơn vị tính demo dùng cho dòng hàng hóa trên hóa đơn.
        if (!await db.Units.AnyAsync())
        {
            db.Units.AddRange(
                new Unit { UnitCode = "CAI", UnitName = "Cái", Remark = "Đơn vị tính hàng hóa", FlagActive = true, UpdatedBy = "kế toán" },
                new Unit { UnitCode = "CHIEC", UnitName = "Chiếc", Remark = "Đơn vị tính hàng hóa", FlagActive = true, UpdatedBy = "kế toán" },
                new Unit { UnitCode = "HOP", UnitName = "Hộp", Remark = "Đơn vị tính hàng hóa", FlagActive = true, UpdatedBy = "kế toán" },
                new Unit { UnitCode = "KG", UnitName = "Kilôgam", Remark = "Đơn vị tính khối lượng", FlagActive = true, UpdatedBy = "kế toán" },
                new Unit { UnitCode = "LAN", UnitName = "Lần", Remark = "Đơn vị tính dịch vụ", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục loại dòng hàng hóa/dịch vụ trên hóa đơn (theo Mst_InvoiceDtlType của TVAN gốc):
        // các loại dòng demo dùng để phân loại dòng chi tiết hóa đơn.
        if (!await db.InvoiceDtlTypes.AnyAsync())
        {
            db.InvoiceDtlTypes.AddRange(
                new InvoiceDtlType { InvoiceDtlTypeCode = "GOODS", Desc = "Hàng hóa / dịch vụ", FlagActive = true, UpdatedBy = "kế toán" },
                new InvoiceDtlType { InvoiceDtlTypeCode = "NOTES", Desc = "Dòng ghi chú", FlagActive = true, UpdatedBy = "kế toán" },
                new InvoiceDtlType { InvoiceDtlTypeCode = "FEES", Desc = "Phí / lệ phí", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục mã loại (theo Mst_TypeCode của TVAN gốc):
        // các mã loại demo dùng để phân loại giao dịch/nhật ký kết nối với cơ quan thuế.
        if (!await db.TypeCodes.AnyAsync())
        {
            db.TypeCodes.AddRange(
                new MstTypeCode { TypeCodeValue = "100", TypeDesc = "Tờ khai đăng ký/thay đổi thông tin sử dụng HĐĐT", TypeGroup = "TCT", FlagActive = true, UpdatedBy = "kế toán" },
                new MstTypeCode { TypeCodeValue = "200", TypeDesc = "Hóa đơn điện tử gửi cơ quan thuế", TypeGroup = "TCT", FlagActive = true, UpdatedBy = "kế toán" },
                new MstTypeCode { TypeCodeValue = "300", TypeDesc = "Thông báo hóa đơn đã lập có sai sót", TypeGroup = "TCT", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục Thương hiệu (theo Mst_Brand của TVAN gốc):
        // các thương hiệu demo dùng để phân loại sản phẩm/hàng hóa.
        if (!await db.Brands.AnyAsync())
        {
            db.Brands.AddRange(
                new Brand { BrandCode = "SAMSUNG", BrandName = "Samsung", Remark = "Thương hiệu điện tử", FlagActive = true, UpdatedBy = "kế toán" },
                new Brand { BrandCode = "APPLE", BrandName = "Apple", Remark = "Thương hiệu điện tử", FlagActive = true, UpdatedBy = "kế toán" },
                new Brand { BrandCode = "SONY", BrandName = "Sony", Remark = "Thương hiệu điện tử", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục Model sản phẩm (theo Mst_Model của TVAN gốc):
        // các model demo thuộc thương hiệu ở trên.
        if (!await db.ProductModels.AnyAsync())
        {
            db.ProductModels.AddRange(
                new ProductModel { ModelCode = "A54", ModelName = "Galaxy A54", BrandCode = "SAMSUNG", OrgModelCode = "SS-A54", Remark = "Điện thoại", FlagActive = true, UpdatedBy = "kế toán" },
                new ProductModel { ModelCode = "IP15", ModelName = "iPhone 15", BrandCode = "APPLE", OrgModelCode = "AP-IP15", Remark = "Điện thoại", FlagActive = true, UpdatedBy = "kế toán" },
                new ProductModel { ModelCode = "WH1000", ModelName = "WH-1000XM5", BrandCode = "SONY", Remark = "Tai nghe", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục Loại sản phẩm (theo Mst_SpecType1 của TVAN gốc):
        // các loại sản phẩm demo dùng để phân loại sản phẩm/hàng hóa khi lập hóa đơn.
        if (!await db.SpecType1s.AnyAsync())
        {
            db.SpecType1s.AddRange(
                new SpecType1 { SpecType1Code = "DIENTU", SpecType1Name = "Điện tử", Remark = "Hàng điện tử", FlagActive = true, UpdatedBy = "kế toán" },
                new SpecType1 { SpecType1Code = "GIAYDEP", SpecType1Name = "Giày dép", Remark = "Hàng may mặc / giày dép", FlagActive = true, UpdatedBy = "kế toán" },
                new SpecType1 { SpecType1Code = "THUCPHAM", SpecType1Name = "Thực phẩm", Remark = "Hàng tiêu dùng", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục loại khách hàng / người mua (theo Mst_CustomerNNTType của TVAN gốc):
        // các loại khách hàng demo dùng khi khai báo danh mục khách hàng.
        if (!await db.CustomerNntTypes.AnyAsync())
        {
            db.CustomerNntTypes.AddRange(
                new CustomerNntType { CustomerNNTType = "DN", CustomerNNTTypeName = "Doanh nghiệp", Remark = "Khách hàng là doanh nghiệp", FlagActive = true, UpdatedBy = "kế toán" },
                new CustomerNntType { CustomerNNTType = "CN", CustomerNNTTypeName = "Cá nhân", Remark = "Khách hàng là cá nhân", FlagActive = true, UpdatedBy = "kế toán" },
                new CustomerNntType { CustomerNNTType = "NN", CustomerNNTTypeName = "Tổ chức nước ngoài", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục Tỉnh/Thành phố (theo Mst_Province của TVAN gốc):
        // các tỉnh/thành demo dùng khi khai báo địa chỉ NNT/khách hàng.
        if (!await db.Provinces.AnyAsync())
        {
            db.Provinces.AddRange(
                new Province { ProvinceCode = "01", ProvinceName = "Hà Nội", FlagActive = true, UpdatedBy = "kế toán" },
                new Province { ProvinceCode = "79", ProvinceName = "TP Hồ Chí Minh", FlagActive = true, UpdatedBy = "kế toán" },
                new Province { ProvinceCode = "48", ProvinceName = "Đà Nẵng", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục Quận/Huyện (theo Mst_District của TVAN gốc):
        // các quận/huyện demo thuộc các tỉnh/thành ở trên, dùng khi khai báo địa chỉ NNT/khách hàng.
        if (!await db.Districts.AnyAsync())
        {
            db.Districts.AddRange(
                new District { ProvinceCode = "01", DistrictCode = "0101", DistrictName = "Quận Ba Đình", FlagActive = true, UpdatedBy = "kế toán" },
                new District { ProvinceCode = "01", DistrictCode = "0102", DistrictName = "Quận Hoàn Kiếm", FlagActive = true, UpdatedBy = "kế toán" },
                new District { ProvinceCode = "79", DistrictCode = "7901", DistrictName = "Quận 1", FlagActive = true, UpdatedBy = "kế toán" },
                new District { ProvinceCode = "48", DistrictCode = "4801", DistrictName = "Quận Hải Châu", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục Quốc gia (theo Mst_Country của TVAN gốc):
        // các quốc gia demo dùng khi khai báo thông tin NNT/khách hàng nước ngoài.
        if (!await db.Countries.AnyAsync())
        {
            db.Countries.AddRange(
                new Country { CountryCode = "VN", CountryName = "Việt Nam", FlagActive = true, UpdatedBy = "kế toán" },
                new Country { CountryCode = "US", CountryName = "Hoa Kỳ", FlagActive = true, UpdatedBy = "kế toán" },
                new Country { CountryCode = "JP", CountryName = "Nhật Bản", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục Đại lý (theo Mst_Dealer của TVAN gốc):
        // các đại lý demo gắn với tỉnh/thành, dùng để quản lý mạng lưới đại lý.
        if (!await db.Dealers.AnyAsync())
        {
            db.Dealers.AddRange(
                new Dealer
                {
                    DLCode = "DL001", DLName = "Đại lý Ô tô Hà Nội", ProvinceCode = "01",
                    DLAddress = "Số 1 Lê Lợi, Ba Đình", DLPresentBy = "Nguyễn Văn A", DLGovIDNumber = "001090012345",
                    DLEmail = "daily.hn@dongdo.vn", DLPhoneNo = "024 3933 1122", FlagActive = true, UpdatedBy = "kế toán"
                },
                new Dealer
                {
                    DLCode = "DL002", DLName = "Đại lý Ô tô Miền Nam", ProvinceCode = "79",
                    DLAddress = "Số 45 Nguyễn Huệ, Quận 1", DLPresentBy = "Trần Thị B", DLGovIDNumber = "079090098765",
                    DLEmail = "daily.mn@dongdo.vn", DLPhoneNo = "028 3822 3344", FlagActive = true, UpdatedBy = "kế toán"
                });
            await db.SaveChangesAsync();
        }

        // Danh mục Phòng ban (theo Mst_Department của TVAN gốc):
        // seller có cây phòng ban demo (HO → KT → KT1) — mã đơn vị nghiệp vụ/cấp được tính từ cây.
        if (!await db.Departments.AnyAsync())
        {
            var seller = await db.Nnts.FirstOrDefaultAsync(n => n.Mst == "0101243150");
            if (seller != null)
            {
                db.Departments.AddRange(
                    new Department { DepartmentCode = "HO", DepartmentCodeParent = null, MST = seller.Mst, DepartmentName = "Hội sở", FlagActive = true, UpdatedBy = "kế toán" },
                    new Department { DepartmentCode = "KT", DepartmentCodeParent = "HO", MST = seller.Mst, DepartmentName = "Phòng Kế toán", FlagActive = true, UpdatedBy = "kế toán" },
                    new Department { DepartmentCode = "KT1", DepartmentCodeParent = "KT", MST = seller.Mst, DepartmentName = "Bộ phận Kế toán 1", FlagActive = true, UpdatedBy = "kế toán" });
                await db.SaveChangesAsync();

                // Tính lại mã đơn vị nghiệp vụ/mẫu/cấp cho cây phòng ban (theo Mst_Department_UpdBU của TVAN gốc).
                var all = await db.Departments.ToListAsync();
                var byCode = all.ToDictionary(d => d.DepartmentCode, StringComparer.OrdinalIgnoreCase);
                for (int pass = 0; pass < 7; pass++)
                {
                    foreach (var d in all)
                    {
                        if (d.DepartmentCode == "HO") { d.DepartmentBUCode = "HO"; d.DepartmentBUPattern = "HO%"; d.DepartmentLevel = 1; continue; }
                        Department? parent = null;
                        if (!string.IsNullOrWhiteSpace(d.DepartmentCodeParent)) byCode.TryGetValue(d.DepartmentCodeParent!, out parent);
                        var parentBu = parent?.DepartmentBUCode;
                        d.DepartmentBUCode = (string.IsNullOrEmpty(parentBu) ? "" : parentBu + ".") + d.DepartmentCode;
                        d.DepartmentBUPattern = d.DepartmentBUCode + "%";
                        d.DepartmentLevel = (parent?.DepartmentLevel ?? 0) + 1;
                    }
                }
                await db.SaveChangesAsync();
            }
        }

        // Chứng thư số của tổ chức (theo Mst_OrgCKS của TVAN gốc):
        // 1 chứng thư số demo đang dùng để ký hóa đơn điện tử.
        if (!await db.OrgCkses.AnyAsync())
        {
            db.OrgCkses.Add(new OrgCks
            {
                CANumber = "1234567890", CAOrg = "VNPT-CA", Subject = "CN=Công ty CP Ô tô Đông Đô, O=Đông Đô, C=VN",
                CAEffDTimeUTCStart = DateTime.UtcNow.AddYears(-1), CAEffDTimeUTCEnd = DateTime.UtcNow.AddYears(1),
                CTSPath = "/keys/dongdo.p12", FlagActive = true, UpdatedBy = "kế toán"
            });
            await db.SaveChangesAsync();
        }

        // Loại thông báo (theo Mst_NotifyType của TVAN gốc):
        // 3 loại thông báo demo dùng để phân loại thông báo gửi người dùng.
        if (!await db.NotifyTypes.AnyAsync())
        {
            db.NotifyTypes.AddRange(
                new NotifyType { NotifyTypeCode = "NOTIFY_ISSUED", NotifyDesc = "Thông báo phát hành hóa đơn", DefaultActive = true, FlagActive = true, UpdatedBy = "quản trị" },
                new NotifyType { NotifyTypeCode = "NOTIFY_ERROR", NotifyDesc = "Thông báo hóa đơn sai sót", DefaultActive = true, FlagActive = true, UpdatedBy = "quản trị" },
                new NotifyType { NotifyTypeCode = "NOTIFY_TCT", NotifyDesc = "Thông báo kết quả từ cơ quan thuế", DefaultActive = false, FlagActive = true, UpdatedBy = "quản trị" });
            await db.SaveChangesAsync();
        }

        // Thông báo hệ thống (theo Notify_Notify / Notify_NotifyDtl của TVAN gốc):
        // 1 thông báo bảo trì đang hiệu lực đã gửi tới 2 người dùng (1 đã đọc, 1 chưa đọc).
        if (!await db.Notifies.AnyAsync())
        {
            var notify = new Notify
            {
                NotifyNo = "TB2026-001", NotifyType = NotifyScope.AllUser, NotifyType1 = NotifyKind.Maintenance,
                NotifyDesc = "Bảo trì hệ thống hóa đơn điện tử định kỳ",
                EffDateStart = DateTime.Today, EffDateEnd = DateTime.Today.AddDays(7),
                FlagSendEmail = true, FlagActive = true, UpdatedBy = "quản trị"
            };
            notify.Details.Add(new NotifyDtl { UserCode = "ketoan01", FlagRead = true, FlagActive = true });
            notify.Details.Add(new NotifyDtl { UserCode = "ketoan02", FlagRead = false, FlagActive = true });
            db.Notifies.Add(notify);
            await db.SaveChangesAsync();
        }

        // Người nhận thông báo (theo Mst_ManageNotify / Map_UserInNotifyType của TVAN gốc):
        // 2 người nhận demo, mỗi người tự đăng ký nhận theo từng loại thông báo.
        if (!await db.NotifyRecipients.AnyAsync())
        {
            var types = await db.NotifyTypes.OrderBy(t => t.NotifyTypeCode).ToListAsync();
            var r1 = new NotifyRecipient { UserCode = "ketoan01", UserName = "Nguyễn Văn A", UpdatedBy = "quản trị" };
            var r2 = new NotifyRecipient { UserCode = "ketoan02", UserName = "Trần Thị B", UpdatedBy = "quản trị" };
            db.NotifyRecipients.AddRange(r1, r2);
            await db.SaveChangesAsync();
            foreach (var t in types)
            {
                db.NotifyRecipientTypes.Add(new NotifyRecipientType { NotifyRecipientId = r1.Id, UserCode = r1.UserCode, NotifyType = t.NotifyTypeCode, FlagNotify = t.DefaultActive, UpdatedBy = "quản trị" });
                db.NotifyRecipientTypes.Add(new NotifyRecipientType { NotifyRecipientId = r2.Id, UserCode = r2.UserCode, NotifyType = t.NotifyTypeCode, FlagNotify = t.DefaultActive, UpdatedBy = "quản trị" });
            }
            await db.SaveChangesAsync();
        }

        // Cấu hình định dạng cột hiển thị theo bảng (theo Mst_ColumnConfig của TVAN gốc):
        // tổ chức demo khai báo định dạng hiển thị cho một số cột của bảng hóa đơn.
        if (!await db.ColumnConfigs.AnyAsync())
        {
            db.ColumnConfigs.AddRange(
                new ColumnConfig { TableName = "Invoice_Invoice", ColumnName = "InvoiceDateUTC", ColumnFormat = "dd/MM/yyyy", ColumnDesc = "Ngày hóa đơn", FlagActive = true, UpdatedBy = "kế toán" },
                new ColumnConfig { TableName = "Invoice_Invoice", ColumnName = "TotalValPmt", ColumnFormat = "N0", ColumnDesc = "Tổng tiền thanh toán", FlagActive = true, UpdatedBy = "kế toán" },
                new ColumnConfig { TableName = "Invoice_Invoice", ColumnName = "InvoiceNo", ColumnFormat = "00000000", ColumnDesc = "Số hóa đơn", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Cột hiển thị danh sách hóa đơn theo tổ chức (theo Mst_SortColumnInvoice của TVAN gốc):
        // tổ chức demo khai báo thứ tự + tên hiển thị + kiểu dữ liệu cho một số cột của lưới hóa đơn.
        if (!await db.SortColumnInvoices.AnyAsync())
        {
            db.SortColumnInvoices.AddRange(
                new SortColumnInvoice { ColumnCode = "InvoiceNo", Idx = 1, ColumnName = "Số hóa đơn", ColumnType = SortColumnType.Text, FlagActive = true, UpdatedBy = "kế toán" },
                new SortColumnInvoice { ColumnCode = "InvoiceDateUTC", Idx = 2, ColumnName = "Ngày hóa đơn", ColumnType = SortColumnType.Date, FlagActive = true, UpdatedBy = "kế toán" },
                new SortColumnInvoice { ColumnCode = "CustomerNNTName", Idx = 3, ColumnName = "Người mua", ColumnType = SortColumnType.Text, FlagActive = true, UpdatedBy = "kế toán" },
                new SortColumnInvoice { ColumnCode = "TotalValPmt", Idx = 4, ColumnName = "Tổng tiền thanh toán", ColumnType = SortColumnType.Number, FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Danh mục tiền tệ / ngoại tệ (theo Mst_CurrencyEx của TVAN gốc):
        // tổ chức demo khai báo VND (tiền tệ gốc) + USD để đọc tiền bằng chữ.
        if (!await db.CurrencyExes.AnyAsync())
        {
            db.CurrencyExes.AddRange(
                new CurrencyEx { CurrencyCode = "VND", CurrencyName = "đồng", BaseCurrencyCode = "VND", BuyRate = 1, SellRate = 1, Remark = "Tiền tệ gốc", FlagActive = true, UpdatedBy = "kế toán" },
                new CurrencyEx { CurrencyCode = "USD", CurrencyName = "đô la Mỹ", BaseCurrencyCode = "VND", BuyRate = 25400, SellRate = 25600, Remark = "Ngoại tệ", FlagActive = true, UpdatedBy = "kế toán" });
            await db.SaveChangesAsync();
        }

        // Nhật ký đọc tiền bằng chữ (theo luồng DocTien của TVAN gốc):
        // minh họa 1 lần đọc số tiền 1.500.000 đồng thành chữ.
        if (!await db.DocTienLogs.AnyAsync())
        {
            db.DocTienLogs.Add(new DocTienLog
            {
                Amount = 1_500_000, CurrencyCode = "VND", CurrencyName = "đồng",
                Text = DocTienService.DocSo("1500000", "VND", "đồng"), By = "kế toán",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        // Nhóm người dùng (theo Sys_Group / Sys_UserInGroup của TVAN gốc):
        // 2 nhóm demo, mỗi nhóm phân gán một số người dùng.
        if (!await db.SysGroups.AnyAsync())
        {
            var g1 = new SysGroup { GroupCode = "KETOAN", GroupName = "Nhóm kế toán", FlagActive = true, UpdatedBy = "quản trị" };
            var g2 = new SysGroup { GroupCode = "BANHANG", GroupName = "Nhóm bán hàng", FlagActive = true, UpdatedBy = "quản trị" };
            db.SysGroups.AddRange(g1, g2);
            await db.SaveChangesAsync();
            db.SysUserInGroups.AddRange(
                new SysUserInGroup { SysGroupId = g1.Id, GroupCode = g1.GroupCode, UserCode = "ketoan01", UpdatedBy = "quản trị" },
                new SysUserInGroup { SysGroupId = g1.Id, GroupCode = g1.GroupCode, UserCode = "ketoan02", UpdatedBy = "quản trị" },
                new SysUserInGroup { SysGroupId = g2.Id, GroupCode = g2.GroupCode, UserCode = "ketoan01", UpdatedBy = "quản trị" });
            await db.SaveChangesAsync();
        }

        // Gói Module (theo Sys_Modules / Sys_Solution của TVAN gốc):
        // 1 giải pháp demo + 2 gói Module thuộc giải pháp đó.
        if (!await db.SysSolutions.AnyAsync())
        {
            db.SysSolutions.Add(new SysSolution { SolutionCode = "TVAN", SolutionName = "Giải pháp hóa đơn điện tử TVAN", FlagActive = true, UpdatedBy = "quản trị" });
            await db.SaveChangesAsync();
        }
        if (!await db.SysModules.AnyAsync())
        {
            db.SysModules.AddRange(
                new SysModule { ModuleCode = "TVAN_BASIC", SolutionCode = "TVAN", ModuleName = "Gói cơ bản", Description = "Gói dùng thử", QtyInvoice = 1000, ValCapacity = 5000, FlagActive = true, UpdatedBy = "quản trị" },
                new SysModule { ModuleCode = "TVAN_PRO", SolutionCode = "TVAN", ModuleName = "Gói chuyên nghiệp", Description = "Gói đầy đủ tính năng", QtyInvoice = 100000, ValCapacity = 500000, FlagActive = true, UpdatedBy = "quản trị" });
            await db.SaveChangesAsync();
        }

        // Đối tượng (chức năng) + phân gán vào gói Module (theo Sys_Object / Sys_ObjectInModules của TVAN gốc):
        // 4 đối tượng demo, gán một số đối tượng vào gói TVAN_BASIC.
        if (!await db.SysObjects.AnyAsync())
        {
            db.SysObjects.AddRange(
                new SysObject { ObjectCode = "INV_ISSUE", ObjectName = "Phát hành hóa đơn", ServiceCode = "INVOICE", ObjectType = SysObjectType.Func, FlagActive = true, UpdatedBy = "quản trị" },
                new SysObject { ObjectCode = "INV_CANCEL", ObjectName = "Hủy hóa đơn", ServiceCode = "INVOICE", ObjectType = SysObjectType.Func, FlagActive = true, UpdatedBy = "quản trị" },
                new SysObject { ObjectCode = "MENU_INVOICE", ObjectName = "Menu hóa đơn", ServiceCode = "INVOICE", ObjectType = SysObjectType.Menu, FlagActive = true, UpdatedBy = "quản trị" },
                new SysObject { ObjectCode = "BTN_APPROVE", ObjectName = "Nút duyệt hóa đơn", ServiceCode = "INVOICE", ObjectType = SysObjectType.Button, FlagActive = true, UpdatedBy = "quản trị" });
            await db.SaveChangesAsync();
        }
        if (!await db.SysObjectInModules.AnyAsync())
        {
            var basic = await db.SysModules.FirstOrDefaultAsync(m => m.ModuleCode == "TVAN_BASIC");
            if (basic != null)
            {
                db.SysObjectInModules.AddRange(
                    new SysObjectInModule { SysModuleId = basic.Id, ModuleCode = basic.ModuleCode, ObjectCode = "INV_ISSUE", UpdatedBy = "quản trị" },
                    new SysObjectInModule { SysModuleId = basic.Id, ModuleCode = basic.ModuleCode, ObjectCode = "MENU_INVOICE", UpdatedBy = "quản trị" });
                await db.SaveChangesAsync();
            }
        }

        // Tích hợp TVAN (theo Mst_TVANInteg của TVAN gốc):
        // 1 cấu hình demo gắn OrgID (MST NNT) với tổ chức giải pháp TVAN (hóa đơn đầu vào/đầu ra).
        if (!await db.TvanIntegs.AnyAsync())
        {
            db.TvanIntegs.Add(new TvanInteg
            {
                OrgCode = "0101243150", MsttctnIn = "0101243150", MsttctnOut = "0101243150",
                FlagActive = true, UpdatedBy = "quản trị"
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task MigratePostgresAsync(AppDbContext db)
    {
        if (!db.Database.IsNpgsql()) return;
        var def = TenantContext.DefaultOrgId;
        var tables = new[] { "Nnts", "Invoices", "Messages" };
        var sql = new List<string> {
            "CREATE TABLE IF NOT EXISTS minitvan.\"Orgs\" (\"Id\" uuid PRIMARY KEY, \"Name\" text NOT NULL DEFAULT '', \"ApiKey\" text NOT NULL DEFAULT '', \"CreatedAt\" timestamp NOT NULL DEFAULT now())",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Orgs_ApiKey\" ON minitvan.\"Orgs\" (\"ApiKey\")" };
        foreach (var t in tables) sql.Add($"ALTER TABLE minitvan.\"{t}\" ADD COLUMN IF NOT EXISTS \"OrgId\" uuid NOT NULL DEFAULT '{def}'");
        foreach (var s in sql) try { await db.Database.ExecuteSqlRawAsync(s); } catch { }
    }
}
