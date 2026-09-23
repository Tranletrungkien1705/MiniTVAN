using Microsoft.EntityFrameworkCore;
using MiniTVAN.Data;
using MiniTVAN.Models;
using MiniTVAN.Services;
using Serilog;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
FleetObs.ConfigureLogger("minitvan");   // Serilog + OpenSearch(Bonsai) + correlation-id

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

var conn = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=minitvan.db";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (DbUtil.IsPostgres(conn)) o.UseNpgsql(DbUtil.ToNpgsql(conn));
    else o.UseSqlite(conn);
});
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ITvanService, TvanService>();
builder.Services.AddFleetObs();   // Redis cache + Swagger + endpoints explorer
builder.Services.AddControllersWithViews();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await Seeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

app.UseFleetObs();
FleetObs.ReportLicense(Environment.GetEnvironmentVariable("SSO_AUTHORITY") ?? "https://minisso.onrender.com", "minitvan");   // correlation middleware + request logging + swagger

app.Use(async (ctx, next) =>
{
    var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(key)) ctx.Request.Cookies.TryGetValue(TenantContext.CookieName, out key);
    if (!string.IsNullOrWhiteSpace(key))
    {
        using var lookup = app.Services.CreateScope();
        var ldb = lookup.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = await ldb.Orgs.FirstOrDefaultAsync(o => o.ApiKey == key);
        if (org != null) ctx.RequestServices.GetRequiredService<ITenantContext>().OrgId = org.Id;
    }
    await next();
});

app.UseStaticFiles();
app.MapGet("/healthz", () => "ok");
app.MapGet("/api/summary", async (ITvanService svc) =>
{
    var d = await svc.DashboardAsync();
    return Results.Ok(new { nnts = d.Nnts, registered = d.Registered, invoices = d.Invoices, accepted = d.Accepted, rejected = d.Rejected, acceptedValue = d.AcceptedValue });
});

// Tra cứu HĐ công khai theo mã CQT (xuyên tenant) — như trang tra cứu của Tổng cục Thuế.
app.MapGet("/api/lookup/{code}", async (string code, ITvanService svc) =>
{
    var i = await svc.LookupByCodeAsync(code);
    if (i == null) return Results.NotFound(new { error = "Không tìm thấy hóa đơn hợp lệ với mã này." });
    return Results.Ok(new { tctCode = i.TctCode, symbol = i.Symbol, no = i.No, seller = i.Nnt?.Name, sellerMst = i.Nnt?.Mst, buyer = i.BuyerName, i.Amount, i.VatAmount, i.Total, issued = i.IssuedDate, status = i.Status.ToString() });
});

// Webhook giả lập TCT đẩy kết quả về (minh họa luồng bất đồng bộ).
app.MapPost("/api/tct/callback", async (TctCallback cb, AppDbContext db) =>
{
    var inv = await db.Invoices.FirstOrDefaultAsync(i => i.TctCode == cb.TctCode);
    if (inv == null) return Results.NotFound();
    db.Messages.Add(new TranMessage { InvoiceId = inv.Id, NntId = inv.NntId, Type = MsgType.SendInvoice, Dir = MsgDir.In, Code = cb.Code, Text = cb.Text });
    await db.SaveChangesAsync();
    return Results.Ok(new { received = true });
});

// Hệ ngoài (MiniService, DMS...) đẩy hóa đơn để phát hành + truyền TCT (cần X-Api-Key).
app.MapPost("/api/invoices", async (ExtInvoiceDto dto, ITvanService svc) =>
{
    var r = await svc.ExternalIssueAsync(dto.SellerMst ?? "", dto.SellerName, dto.BuyerName ?? "", dto.BuyerMst, dto.BuyerAddress, dto.Amount, dto.VatRate, dto.DocRef);
    return r.ok
        ? Results.Ok(new { id = r.id, tctCode = r.tctCode, status = r.status })
        : Results.BadRequest(new { id = r.id, status = r.status, error = r.msg, tctCode = r.tctCode });
});

app.MapPost("/api/orgs/register", async (RegisterOrgDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest(new { error = "Cần Name." });
    var org = new Org { Name = dto.Name.Trim(), ApiKey = "tvan_" + Guid.NewGuid().ToString("N") };
    db.Orgs.Add(org); await db.SaveChangesAsync();
    return Results.Ok(new { orgId = org.Id, apiKey = org.ApiKey });
});

// Import NNT (người nộp thuế) thật từ danh sách đại lý HTC (dedupe theo Mst)
app.MapPost("/api/import/nnts", async (List<ImportNntDto> rows, AppDbContext db, ITenantContext tc) =>
{
    if (rows == null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu." });
    int added = 0, skipped = 0;
    var orgId = tc.OrgId;
    foreach (var row in rows)
    {
        if (string.IsNullOrWhiteSpace(row.Mst)) { skipped++; continue; }
        if (await db.Nnts.AnyAsync(n => n.OrgId == orgId && n.Mst == row.Mst.Trim())) { skipped++; continue; }
        db.Nnts.Add(new Nnt { OrgId = orgId, Mst = row.Mst.Trim(), Name = row.Name ?? row.Mst.Trim(), Address = row.Address, Email = row.Email, RegStatus = RegStatus.Registered, RegisteredAt = DateTime.UtcNow.AddDays(-30) });
        added++;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { added, skipped, total = added + skipped });
});

// Import hóa đơn thật từ HTC (dedupe theo NntId+Symbol+No)
app.MapPost("/api/import/invoices", async (List<ImportInvDto> rows, AppDbContext db, ITenantContext tc) =>
{
    if (rows == null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu." });
    int added = 0, skipped = 0;
    var orgId = tc.OrgId;
    foreach (var row in rows)
    {
        if (string.IsNullOrWhiteSpace(row.SellerMst) || string.IsNullOrWhiteSpace(row.No)) { skipped++; continue; }
        var nnt = await db.Nnts.FirstOrDefaultAsync(n => n.OrgId == orgId && n.Mst == row.SellerMst.Trim());
        if (nnt == null) { skipped++; continue; }
        var sym = (row.Symbol ?? "1C26TAA").Trim();
        var no = row.No.Trim();
        if (await db.Invoices.AnyAsync(i => i.OrgId == orgId && i.NntId == nnt.Id && i.Symbol == sym && i.No == no)) { skipped++; continue; }
        db.Invoices.Add(new Invoice
        {
            OrgId = orgId, NntId = nnt.Id, Symbol = sym, No = no,
            BuyerName = row.BuyerName ?? "", BuyerMst = row.BuyerMst, BuyerAddress = row.BuyerAddress,
            Amount = row.Amount, VatRate = row.VatRate > 0 ? row.VatRate : 10,
            IssuedDate = row.IssuedDate ?? DateTime.Today,
            Status = InvoiceStatus.Accepted,
            TctCode = row.TctCode ?? $"TC{DateTime.UtcNow:yyyyMMdd}{no.PadLeft(8, '0')}",
            SentAt = DateTime.UtcNow.AddDays(-7)
        });
        added++;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { added, skipped, total = added + skipped });
});

// Xử lý hóa đơn sai sót: điều chỉnh (tăng/giảm) hóa đơn đã phát hành.
app.MapPost("/api/invoices/{id:int}/adjust", async (int id, AdjustDto dto, ITvanService svc) =>
{
    var (ok, msg, newId) = await svc.AdjustAsync(id, dto.AdjType, dto.Amount, dto.VatRate, dto.Reason);
    return ok ? Results.Ok(new { id = newId, msg }) : Results.BadRequest(new { id = newId, error = msg });
});

// Xử lý hóa đơn sai sót: thay thế hóa đơn gốc (HĐ gốc bị hủy).
app.MapPost("/api/invoices/{id:int}/replace", async (int id, ReplaceDto dto, ITvanService svc) =>
{
    var (ok, msg, newId) = await svc.ReplaceAsync(id, dto.Amount, dto.VatRate, dto.Reason);
    return ok ? Results.Ok(new { id = newId, msg }) : Results.BadRequest(new { id = newId, error = msg });
});

// Chuyển hóa đơn về trạng thái chờ (PENDING) — giữ nguyên số (theo Invoice_Invoice_Support_InvoiceToPending của TVAN gốc).
app.MapPost("/api/invoices/{id:int}/to-pending", async (int id, ToPendingDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.ResetToPendingAsync(id, dto.Reason);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Khôi phục hóa đơn đã hủy (DELETED) về trạng thái đã phát hành (ISSUED) để làm thông báo sai sót
// (theo Invoice_Invoice_Support_BackInvoiceStatus của TVAN gốc).
app.MapPost("/api/invoices/{id:int}/restore", async (int id, RestoreDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.RestoreInvoiceAsync(id, dto.Reason);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa hóa đơn điều chỉnh/thay thế CHƯA phát hành (theo Invoice_Invoice_Support_DeleteInvoiceRefNo của TVAN gốc).
app.MapPost("/api/invoices/{id:int}/delete-adjust-replace", async (int id, DeleteAdjReplaceDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteAdjustReplaceAsync(id, dto.Reason);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa hóa đơn đã phát hành (theo Invoice_Invoice_Deleted của TVAN gốc): ISSUED → DELETED.
app.MapPost("/api/invoices/{id:int}/delete", async (int id, DeleteInvoiceDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteInvoiceAsync(id, dto.Remark, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Hạn mức hóa đơn (theo Invoice_license của TVAN gốc): xem hạn mức + lịch sử cấp/điều chỉnh.
app.MapGet("/api/licenses", async (ITvanService svc) =>
{
    var ls = await svc.LicensesAsync();
    return Results.Ok(ls.Select(l => new { nntId = l.NntId, nnt = l.Nnt?.Name, mst = l.Nnt?.Mst, totalQty = l.TotalQty, issued = l.TotalQtyIssued, used = l.TotalQtyUsed, remaining = l.Remaining }));
});

// Cấp/tăng hạn mức cho NNT (theo Invoice_license_IncreaseQty của TVAN gốc).
app.MapPost("/api/licenses/increase", async (LicenseIncreaseDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.IncreaseLicenseAsync(dto.NntId, dto.Qty, dto.Note);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Bảng tổng hợp dữ liệu HĐĐT gửi CQT (theo Mst_GuiTongHop của TVAN gốc): danh sách.
app.MapGet("/api/guitonghop", async (int? nntId, ITvanService svc) =>
{
    var ls = await svc.GuiTongHopsAsync(nntId);
    return Results.Ok(ls.Select(g => new { g.Id, g.SBTHDLieu, nnt = g.Nnt?.Name, mst = g.MST, period = g.LKDLieu.ToString(), g.KDLieu, g.BSLThu, lines = g.Details.Count, total = g.TotalPayment, status = g.Status.ToString() }));
});

// Lập bảng tổng hợp theo kỳ (gom HĐ đã được CQT chấp nhận trong kỳ).
app.MapPost("/api/guitonghop", async (GuiTongHopDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.CreateGuiTongHopAsync(dto.NntId, dto.LKDLieu, dto.KDLieu ?? "", dto.BSLThu, dto.Note);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Gửi bảng tổng hợp tới CQT (mô phỏng round-trip 202/204).
app.MapPost("/api/guitonghop/{id:int}/send", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.SendGuiTongHopAsync(id);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Bảng tổng hợp hóa đơn (BTH) — theo Invoice_Invoice_BTHGet của TVAN gốc: liệt kê hóa đơn
// đã phát hành (ISSUED) / đã hủy (DELETED) trong một kỳ kèm trạng thái TThai + hóa đơn gốc.
app.MapGet("/api/bth", async (PeriodType? lkdlieu, string? kdlieu, ITvanService svc) =>
{
    var t = lkdlieu ?? PeriodType.Month;
    var k = string.IsNullOrWhiteSpace(kdlieu) ? DateTime.Today.ToString("yyyy-MM") : kdlieu.Trim();
    var rows = await svc.BthRowsAsync(t, k);
    return Results.Ok(new
    {
        lkdlieu = t.ToString(), kdlieu = k, count = rows.Count,
        totalAmount = rows.Sum(r => r.Amount), totalVat = rows.Sum(r => r.VatAmount), totalPayment = rows.Sum(r => r.Total),
        rows = rows.Select(r => new { r.InvoiceCode, r.FormNo, r.Sign, r.InvoiceNo, r.InvoiceDate, r.BuyerName, r.BuyerMst, r.Amount, r.VatRate, r.VatAmount, r.Total, tthai = r.TThai.ToString(), r.RefFormNo, r.RefInvoiceNo })
    });
});

// Tra cứu thông tin NNT theo MST từ cơ quan thuế (theo TCT_TraTTinMaSoThue của TVAN gốc).
app.MapGet("/api/tax/lookup/{mst}", async (string mst, ITvanService svc) =>
{
    var (ok, msg, log) = await svc.LookupNntByMstAsync(mst);
    if (!ok) return Results.NotFound(new { error = msg });
    return Results.Ok(new { mst = log!.Mst, fullName = log.FullName, address = log.Address, govTaxId = log.GovTaxID, govTaxName = log.GovTaxName, result = log.Result.ToString() });
});

// Danh mục cơ quan thuế (theo Mst_GovTaxID của TVAN gốc).
app.MapGet("/api/tax/offices", async (ITvanService svc) =>
{
    var ls = await svc.TaxOfficesAsync();
    return Results.Ok(ls.Select(t => new { t.GovTaxID, t.GovTaxIDParent, t.GovTaxIDBUCode, t.GovTaxIDBUPattern, t.GovTaxIDLevel, t.ProvinceCode, t.DistrictCode, t.GovTaxName, t.Level, t.Address, t.ContactEmail, t.ContactPhone, t.FlagActive }));
});

// Lưu (tạo mới/cập nhật) cơ quan thuế theo mã (theo Mst_GovTaxID_Create/Update của TVAN gốc).
app.MapPost("/api/tax/offices", async (TaxOfficeDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveTaxOfficeAsync(dto.Id, dto.Code ?? "", dto.CodeParent, dto.ProvinceCode, dto.DistrictCode, dto.Name ?? "", dto.Level, dto.Address, dto.ContactEmail, dto.ContactPhone, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa cơ quan thuế theo id (theo Mst_GovTaxID_Delete của TVAN gốc).
app.MapDelete("/api/tax/offices/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteTaxOfficeAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Gửi/gửi lại email hóa đơn đã phát hành cho người mua (theo Invoice_Invoice_Support_SendMail của TVAN gốc).
app.MapPost("/api/invoices/{id:int}/send-email", async (int id, SendEmailDto dto, ITvanService svc) =>
{
    var (ok, msg, logId) = await svc.SendInvoiceEmailAsync(id, dto.ToEmail, dto.SentBy);
    return ok ? Results.Ok(new { id = logId, msg }) : Results.BadRequest(new { id = logId, error = msg });
});

// Nhật ký gửi email hóa đơn (lọc theo hóa đơn nếu có).
app.MapGet("/api/email-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.EmailLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, l.ToEmail, l.Subject, result = l.Result.ToString(), l.Message, l.SentBy, l.CreatedAt }));
});

// In chuyển đổi hóa đơn (theo Invoice_Invoice.FlagChange của TVAN gốc): đánh dấu HĐ đã in ở dạng chuyển đổi.
app.MapPost("/api/invoices/{id:int}/conversion-print", async (int id, ConversionPrintDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.MarkConversionPrintedAsync(id, dto.Note, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Bỏ cờ in chuyển đổi (theo Invoice_Invoice_Support_BackFlagChange của TVAN gốc): đưa HĐ về in thường.
app.MapPost("/api/invoices/{id:int}/conversion-print/reset", async (int id, ConversionPrintDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.ResetConversionPrintAsync(id, dto.Note, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký in chuyển đổi hóa đơn (lọc theo hóa đơn nếu có).
app.MapGet("/api/conversion-print-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.ConversionPrintLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, action = l.Action.ToString(), l.Note, l.By, l.CreatedAt }));
});

// Ký lại hóa đơn đã phát hành (theo Invoice_Invoice_ReSign của TVAN gốc): cập nhật nội dung đã ký + đánh dấu FlagHotfix.
app.MapPost("/api/invoices/{id:int}/re-sign", async (int id, ReSignDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.ReSignAsync(id, dto.FileSpec, dto.Note, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký ký lại hóa đơn (lọc theo hóa đơn nếu có).
app.MapGet("/api/re-sign-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.ReSignLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, l.FilePath, l.Note, l.By, l.CreatedAt }));
});

// Duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc): PENDING → APPROVED, ghi đường dẫn file XML/PDF + người duyệt.
app.MapPost("/api/invoices/{id:int}/approve", async (int id, ApproveDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.ApproveAsync(id, dto.FilePath, dto.PdfFilePath, dto.Note, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Bỏ duyệt hóa đơn (theo Invoice_Invoice_Approved của TVAN gốc): APPROVED → PENDING.
app.MapPost("/api/invoices/{id:int}/unapprove", async (int id, UnapproveDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.UnapproveAsync(id, dto.Note, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký duyệt hóa đơn (lọc theo hóa đơn nếu có).
app.MapGet("/api/approve-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.ApproveLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, action = l.Action.ToString(), l.FilePath, l.PdfFilePath, l.Note, l.By, l.CreatedAt }));
});

// Duyệt NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_ApprovedMulti của TVAN gốc):
// duyệt hàng loạt danh sách HĐ đang chờ (PENDING) đã có số → APPROVED.
app.MapPost("/api/invoices/bulk-approve", async (BulkApproveDto dto, ITvanService svc) =>
{
    var (ok, msg, count) = await svc.BulkApproveAsync(dto.Ids ?? new(), dto.Note, dto.By);
    return ok ? Results.Ok(new { approvedCount = count, msg }) : Results.BadRequest(new { error = msg });
});

// Nhật ký duyệt nhiều hóa đơn cùng lúc.
app.MapGet("/api/bulk-approve-logs", async (ITvanService svc) =>
{
    var ls = await svc.BulkApproveLogsAsync();
    return Results.Ok(ls.Select(l => new { l.Id, action = l.Action.ToString(), l.ApprovedCount, l.InvoiceNos, l.Note, l.By, l.CreatedAt }));
});

// Xóa NHIỀU hóa đơn cùng lúc (theo Invoice_Invoice_DeleteMulti của TVAN gốc):
// xóa hàng loạt danh sách HĐ đã phát hành (ISSUED) đã có số → DELETED.
app.MapPost("/api/invoices/bulk-delete", async (BulkDeleteDto dto, ITvanService svc) =>
{
    var (ok, msg, count) = await svc.BulkDeleteAsync(dto.Ids ?? new(), dto.Note, dto.By);
    return ok ? Results.Ok(new { deletedCount = count, msg }) : Results.BadRequest(new { error = msg });
});

// Nhật ký xóa nhiều hóa đơn cùng lúc.
app.MapGet("/api/bulk-delete-logs", async (ITvanService svc) =>
{
    var ls = await svc.BulkDeleteLogsAsync();
    return Results.Ok(ls.Select(l => new { l.Id, action = l.Action.ToString(), l.DeletedCount, l.InvoiceNos, l.Note, l.By, l.CreatedAt }));
});

// Phát hành hóa đơn đã duyệt (theo Invoice_Invoice_Issued của TVAN gốc): APPROVED → ISSUED,
// ghi thời điểm/người phát hành + email người nhận.
app.MapPost("/api/invoices/{id:int}/issue", async (int id, IssueDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.IssueAsync(id, dto.EmailSend, dto.Note, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký phát hành hóa đơn (lọc theo hóa đơn nếu có).
app.MapGet("/api/issue-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.IssueLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, action = l.Action.ToString(), l.EmailSend, l.Note, l.By, l.CreatedAt }));
});

// Cấu hình hệ thống: trạng thái kiểm tra ký quá 60 ngày (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc).
app.MapGet("/api/settings/sign60day", async (ITvanService svc) =>
{
    var s = await svc.GetSettingAsync();
    return Results.Ok(new { sign60Day = s.Sign60Day.ToString(), check = s.Sign60Day == Sign60DayFlag.Check, s.Note, s.UpdatedAt });
});

// Bật/bỏ kiểm tra ký quá 60 ngày (theo Invoice_Invoice_Support_Sign60Day của TVAN gốc).
app.MapPost("/api/settings/sign60day", async (Sign60DayDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.SetSign60DayAsync(dto.Flag, dto.Note);
    return ok ? Results.Ok(new { ok, msg }) : Results.BadRequest(new { ok, error = msg });
});

// Cấu hình dấu phân cách động theo tổ chức (theo Mst_DynamicComma của TVAN gốc).
app.MapGet("/api/settings/dynamic-comma", async (ITvanService svc) =>
{
    var c = await svc.GetDynamicCommaAsync();
    return Results.Ok(new { flagStyle = (int)c.FlagStyle, style = c.FlagStyle.ToString(), c.UpdatedAt, c.UpdatedBy });
});

// Cập nhật kiểu dấu phân cách động (theo Mst_DynamicComma_Update của TVAN gốc).
app.MapPost("/api/settings/dynamic-comma", async (DynamicCommaDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.SetDynamicCommaAsync(dto.FlagStyle, dto.By);
    return ok ? Results.Ok(new { ok, msg }) : Results.BadRequest(new { ok, error = msg });
});

// Danh mục mẫu hóa đơn (theo Invoice_TempInvoice của TVAN gốc): dải số được cấp phát theo NNT.
app.MapGet("/api/templates", async (int? nntId, ITvanService svc) =>
{
    var ls = await svc.TemplatesAsync(nntId);
    return Results.Ok(ls.Select(t => new { t.Id, t.TInvoiceCode, t.TInvoiceName, nnt = t.Nnt?.Name, mst = t.Nnt?.Mst, t.FormNo, t.Sign, rule = t.TTType.ToString(), t.StartInvoiceNo, t.EndInvoiceNo, t.LastInvoiceNo, t.QtyUsed, remain = t.QtyRemain, t.EffDateStart, status = t.TInvoiceStatus.ToString() }));
});

// Phát hành mẫu hóa đơn (theo Invoice_TempInvoice_Issued của TVAN gốc): PENDING → ISSUED, ghi ngày bắt đầu sử dụng.
app.MapPost("/api/templates/{id:int}/issue", async (int id, IssueTemplateDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.IssueTemplateAsync(id, dto.EffDateStart ?? DateTime.Today, dto.Remark);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Ngừng hoạt động mẫu hóa đơn (theo Invoice_TempInvoice_InActive của TVAN gốc): ISSUED → INACTIVE, ghi ngày kết thúc.
app.MapPost("/api/templates/{id:int}/inactivate", async (int id, InactivateTemplateDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.InactivateTemplateAsync(id, dto.Remark);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Hủy mẫu hóa đơn (theo Invoice_TempInvoice_Cancel của TVAN gốc): ISSUED → CANCEL, ghi số lượng hủy + người hủy.
app.MapPost("/api/templates/{id:int}/cancel", async (int id, CancelTemplateDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.CancelTemplateAsync(id, dto.Remark, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Tăng số hóa đơn cuối (EndInvoiceNo) của mẫu hóa đơn — mở rộng dải số được cấp phát
// (theo Invoice_TempInvoice_IncreaseEndInvoiceNo của TVAN gốc).
app.MapPost("/api/templates/{id:int}/increase-end-no", async (int id, IncreaseEndNoDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.IncreaseTemplateEndNoAsync(id, dto.NewEndInvoiceNo, dto.Remark, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký mở rộng dải số mẫu hóa đơn (lọc theo mẫu nếu có).
app.MapGet("/api/template-range-logs", async (int? templateId, ITvanService svc) =>
{
    var ls = await svc.TemplateRangeLogsAsync(templateId);
    return Results.Ok(ls.Select(l => new { l.Id, l.TemplateId, form = l.Template != null ? l.Template.FormNo : null, action = l.Action.ToString(), l.OldStartInvoiceNo, l.NewStartInvoiceNo, l.OldEndInvoiceNo, l.NewEndInvoiceNo, l.Remark, l.By, l.CreatedAt }));
});

// Cập nhật lại CẢ dải số (số bắt đầu + số kết thúc) của mẫu hóa đơn đang chờ
// (theo Invoice_TempInvoice_UpdQtyInvoiceNo của TVAN gốc).
app.MapPost("/api/templates/{id:int}/update-qty-no", async (int id, UpdateQtyNoDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.UpdateTemplateQtyNoAsync(id, dto.StartInvoiceNo, dto.EndInvoiceNo, dto.Remark, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Gửi mẫu hóa đơn tới CQT (theo Invoice_TempInvoice_SentTCT của TVAN gốc): PENDING → SENTTCT, ghi mã V tham chiếu.
app.MapPost("/api/templates/{id:int}/send-tct", async (int id, SendTemplateTctDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.SendTemplateToTctAsync(id, dto.Remark, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhận kết quả phát hành mẫu từ CQT (theo Invoice_TempInvoice_TCTIssued của TVAN gốc): ACCEPT → ISSUED, REJECT → PENDING.
app.MapPost("/api/templates/{id:int}/receive-tct", async (int id, ReceiveTemplateTctDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.ReceiveTemplateTctResultAsync(id, dto.ChapNhan, dto.Message, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký gửi/nhận kết quả mẫu hóa đơn với CQT (lọc theo mẫu nếu có).
app.MapGet("/api/template-tct-logs", async (int? templateId, ITvanService svc) =>
{
    var ls = await svc.TemplateTctLogsAsync(templateId);
    return Results.Ok(ls.Select(l => new { l.Id, l.TemplateId, form = l.Template != null ? l.Template.FormNo : null, action = l.Action.ToString(), l.TCTRefNo, chapNhan = l.ChapNhan?.ToString(), l.Message, l.Remark, l.By, l.CreatedAt }));
});

// Cập nhật thông tin liên hệ của NNT in trên mẫu hóa đơn
// (theo Invoice_TempInvoice_SupportUpdEmailAndAddress của TVAN gốc).
app.MapPost("/api/templates/{id:int}/contact", async (int id, TemplateContactDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.UpdateTemplateContactAsync(id, dto.NntName, dto.NntAddress, dto.NntPhone, dto.NntEmail, dto.NntWebsite, dto.FlagStyleComma, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Cập nhật số tài khoản & tên ngân hàng của NNT in trên mẫu hóa đơn
// (theo Invoice_TempInvoice_SupportUpdAccNoAndBankName của TVAN gốc).
app.MapPost("/api/templates/{id:int}/bank", async (int id, TemplateBankDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.UpdateTemplateBankAsync(id, dto.NntAccNo, dto.NntBankName, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Tạo mới/cập nhật mẫu hóa đơn (theo Invoice_TempInvoice_Save của TVAN gốc):
// lưu lần đầu = tạo mẫu mới ở trạng thái chờ (PENDING), lưu lại cùng mã = cập nhật.
app.MapPost("/api/templates", async (SaveTemplateDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveTemplateAsync(dto.Id, dto.TInvoiceCode ?? "", dto.NntId, dto.TInvoiceName ?? "", dto.FormNo ?? "", dto.Sign ?? "", dto.TTType, dto.Remark, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa mẫu hóa đơn đang chờ chưa dùng số (theo Invoice_TempInvoice_Save với FlagIsDelete của TVAN gốc).
app.MapDelete("/api/templates/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteTemplateAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Cấp phát số hóa đơn (theo Invoice_Invoice_AllocatedInv của TVAN gốc): PENDING + chưa có số → cấp số kế tiếp từ mẫu.
app.MapPost("/api/invoices/{id:int}/allocate-no", async (int id, AllocateNoDto dto, ITvanService svc) =>
{
    var (ok, msg, invoiceNo) = await svc.AllocateInvoiceNoAsync(id, dto.InvoiceDate ?? DateTime.Today, dto.By);
    return ok ? Results.Ok(new { id, invoiceNo, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký cấp phát số hóa đơn (lọc theo hóa đơn nếu có).
app.MapGet("/api/invoice-no-alloc-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.AllocLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, l.FormNo, l.Sign, l.InvoiceNo, l.InvoiceDate, l.By, l.CreatedAt }));
});

// Cấp số + Duyệt + Phát hành trong MỘT bước (theo Invoice_Invoice_AllocatedAndApprovedAndIssued của TVAN gốc):
// gộp 3 thao tác tuần tự trên HĐ đang chờ chưa có số → cấp số từ mẫu, duyệt (APPROVED), phát hành (ISSUED).
app.MapPost("/api/invoices/{id:int}/allocate-approve-issue", async (int id, AllocateApproveIssueDto dto, ITvanService svc) =>
{
    var (ok, msg, invoiceNo) = await svc.AllocateApproveIssueAsync(id, dto.InvoiceDate ?? DateTime.Today, dto.FilePath, dto.PdfFilePath, dto.EmailSend, dto.Note, dto.By);
    return ok ? Results.Ok(new { id, invoiceNo, msg }) : Results.BadRequest(new { id, error = msg });
});

// Cấp số hóa đơn khởi tạo từ MÁY TÍNH TIỀN (theo Invoice_Invoice_AllocatedInvoiceTypeM của TVAN gốc):
// PENDING + chưa có số + mẫu loại MTT (FormNo ký tự thứ 4 = 'M') + NNT có MCCQT 5 ký tự → cấp số kế tiếp từ mẫu.
app.MapPost("/api/invoices/{id:int}/allocate-no-type-m", async (int id, AllocateNoDto dto, ITvanService svc) =>
{
    var (ok, msg, invoiceNo) = await svc.AllocateInvoiceNoTypeMAsync(id, dto.InvoiceDate ?? DateTime.Today, dto.By);
    return ok ? Results.Ok(new { id, invoiceNo, msg }) : Results.BadRequest(new { id, error = msg });
});

// Sinh mã CQT trên hóa đơn khởi tạo từ máy tính tiền (theo Invoice_Invoice_GenMCCQTMTTTypeM của TVAN gốc).
app.MapPost("/api/invoices/{id:int}/gen-mccqt-mtt", async (int id, ITvanService svc) =>
{
    var (ok, msg, mccqtmtt) = await svc.GenMccqtMttAsync(id);
    return ok ? Results.Ok(new { id, mccqtmtt, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhận kết quả phản hồi từ CQT cho hóa đơn đã gửi (theo Invoice_Invoice_TCTReceive của TVAN gốc):
// 202 = phát hành thành công (có mã CQT), 204 = phát hành thất bại.
app.MapPost("/api/invoices/{id:int}/tct-receive", async (int id, TctReceiveDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.ReceiveTctResultAsync(id, dto.MltDiep, dto.MaCQT, dto.MaLoi, dto.LyDo);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký nhận kết quả CQT (lọc theo hóa đơn nếu có).
app.MapGet("/api/tct-receive-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.TctReceiveLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, mltDiep = (int)l.MltDiep, chapNhan = l.ChapNhan.ToString(), l.MaCQT, l.MaLoi, l.LyDo, l.Message, l.CreatedAt }));
});

// Gửi thông báo hóa đơn sai sót tới CQT (theo Invoice_Invoice_SentTCT_300 của TVAN gốc):
// dựng thông điệp 300 (04/SS) cho HĐ đã được CQT chấp nhận, lưu mã V + cờ thay thế/điều chỉnh TCT trả về.
app.MapPost("/api/invoices/{id:int}/send-tct-300", async (int id, SendTct300Dto dto, ITvanService svc) =>
{
    var (ok, msg, tctRefNo) = await svc.SendTct300Async(id, dto.FlagReplaceOrAdjust, dto.LoaiTb, dto.SoTb, dto.NgayTb, dto.LyDo, dto.By);
    return ok ? Results.Ok(new { id, tctRefNo, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký gửi thông báo hóa đơn sai sót (300) tới CQT (lọc theo hóa đơn nếu có).
app.MapGet("/api/tct-300-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.Tct300LogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, l.TCTRefNo, flagReplaceOrAdjust = (int)l.FlagReplaceOrAdjust, l.LoaiTB, l.SoTB, l.NgayTB, l.LyDo, l.Message, l.By, l.CreatedAt }));
});

// Cập nhật nội dung hóa đơn sau khi đã cấp số (theo Invoice_Invoice_UpdAfterAllocated của TVAN gốc):
// sửa người mua, phương thức thanh toán, tiền hàng/thuế suất, ngày HĐ khi HĐ đang chờ và đã có số.
app.MapPost("/api/invoices/{id:int}/update-after-allocated", async (int id, UpdateAfterAllocatedDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.UpdateAfterAllocatedAsync(id, dto.BuyerName, dto.BuyerMst, dto.BuyerAddress, dto.PaymentMethod, dto.Amount, dto.VatRate, dto.InvoiceDate ?? DateTime.Today, dto.Note, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký cập nhật nội dung hóa đơn sau cấp số (lọc theo hóa đơn nếu có).
app.MapGet("/api/invoice-update-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.UpdateLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, l.BuyerName, l.BuyerMst, l.BuyerAddress, paymentMethod = l.PaymentMethod.ToString(), l.Amount, l.VatRate, l.InvoiceDate, l.Note, l.By, l.CreatedAt }));
});

// Hủy hóa đơn đang chờ/đã duyệt (theo Invoice_Invoice_Cancel của TVAN gốc): PENDING/APPROVED → CANCELED,
// ghi thời điểm/người hủy + lý do.
app.MapPost("/api/invoices/{id:int}/cancel-invoice", async (int id, CancelInvoiceDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.CancelInvoiceAsync(id, dto.Remark, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký hủy hóa đơn (lọc theo hóa đơn nếu có).
app.MapGet("/api/cancel-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.CancelLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, action = l.Action.ToString(), l.Remark, l.By, l.CreatedAt }));
});

// Tạo biên bản đính kèm hóa đơn (theo luồng TaoBienBan của TVAN gốc): lưu file biên bản (base64) + lý do.
app.MapPost("/api/invoices/{id:int}/record", async (int id, CreateRecordDto dto, ITvanService svc) =>
{
    var (ok, msg, logId) = await svc.CreateRecordAsync(id, dto.Type, dto.FileName ?? "", dto.FileSpec, dto.Reason, dto.By);
    return ok ? Results.Ok(new { id = logId, msg }) : Results.BadRequest(new { id = logId, error = msg });
});

// Nhật ký tạo biên bản đính kèm hóa đơn (lọc theo hóa đơn nếu có).
app.MapGet("/api/record-logs", async (int? invoiceId, ITvanService svc) =>
{
    var ls = await svc.RecordLogsAsync(invoiceId);
    return Results.Ok(ls.Select(l => new { l.Id, l.InvoiceId, invoice = l.Invoice != null ? $"{l.Invoice.Symbol}-{l.Invoice.No}" : null, type = l.Type.ToString(), l.FileName, l.FilePath, l.Reason, l.By, l.CreatedAt }));
});

// Trường tùy chỉnh hóa đơn (theo Invoice_CustomField / Invoice_DtlCustomField của TVAN gốc): danh sách.
app.MapGet("/api/custom-fields", async (ITvanService svc) =>
{
    var ls = await svc.InvoiceCustomFieldsAsync();
    return Results.Ok(ls.Select(f => new { f.InvoiceCustomFieldCode, f.InvoiceCustomFieldName, type = f.DBPhysicalType.ToString(), f.FlagActive }));
});

// Trường tùy chỉnh trên danh sách hàng hóa: danh sách.
app.MapGet("/api/custom-fields/dtl", async (ITvanService svc) =>
{
    var ls = await svc.InvoiceDtlCustomFieldsAsync();
    return Results.Ok(ls.Select(f => new { f.InvoiceDtlCustomFieldCode, f.InvoiceDtlCustomFieldName, type = f.DBPhysicalType.ToString(), f.FlagActive }));
});

// Lưu (tạo mới/cập nhật) trường tùy chỉnh hóa đơn theo mã (theo Invoice_CustomField_Create/Update của TVAN gốc).
app.MapPost("/api/custom-fields", async (CustomFieldDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveInvoiceCustomFieldAsync(dto.Code ?? "", dto.Name ?? "", dto.Type, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Lưu (tạo mới/cập nhật) trường tùy chỉnh hàng hóa theo mã (theo Invoice_DtlCustomField_Create/Update của TVAN gốc).
app.MapPost("/api/custom-fields/dtl", async (CustomFieldDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveInvoiceDtlCustomFieldAsync(dto.Code ?? "", dto.Name ?? "", dto.Type, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa trường tùy chỉnh hóa đơn theo mã (theo Invoice_CustomField_Delete của TVAN gốc).
app.MapDelete("/api/custom-fields/{code}", async (string code, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteInvoiceCustomFieldAsync(code);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Xóa trường tùy chỉnh hàng hóa theo mã (theo Invoice_DtlCustomField_Delete của TVAN gốc).
app.MapDelete("/api/custom-fields/dtl/{code}", async (string code, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteInvoiceDtlCustomFieldAsync(code);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Nhóm mẫu hóa đơn (theo Invoice_TempGroup của TVAN gốc): danh sách nhóm mẫu (lọc theo MST nếu có).
app.MapGet("/api/temp-groups", async (string? mst, ITvanService svc) =>
{
    var ls = await svc.TempGroupsAsync(mst);
    return Results.Ok(ls.Select(g => new { g.Id, g.InvoiceTGroupCode, g.MST, vatType = g.VATType.ToString(), g.InvoiceTGroupName, specPrdType = g.SpecPrdType.ToString(), g.FlagActive, fields = g.Fields.Count }));
});

// Lưu (tạo mới/cập nhật) nhóm mẫu hóa đơn theo mã (theo Invoice_TempGroup_Create/Update của TVAN gốc).
app.MapPost("/api/temp-groups", async (TempGroupDto dto, ITvanService svc) =>
{
    var fields = (dto.Fields ?? new()).Select(f => (f.FieldName ?? "", f.TcfType ?? "")).ToList();
    var (ok, msg, id) = await svc.SaveTempGroupAsync(dto.Id, dto.Code ?? "", dto.Mst ?? "", dto.VatType, dto.Name ?? "", dto.Body, dto.Thumbnail, dto.SpecPrdType, dto.Active, fields, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa nhóm mẫu hóa đơn theo id (theo Invoice_TempGroup_Delete của TVAN gốc).
app.MapDelete("/api/temp-groups/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteTempGroupAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Mẫu thông điệp/thông báo gửi CQT (theo Mst_MessageTemplate của TVAN gốc): danh sách (lọc theo loại thông điệp nếu có).
app.MapGet("/api/message-templates", async (MessageTypeCode? type, ITvanService svc) =>
{
    var ls = await svc.MessageTemplatesAsync(type);
    return Results.Ok(ls.Select(m => new { m.MessageTplCode, m.MessageTplName, type = (int)m.MessageTypeCode, typeText = Ui.MessageType(m.MessageTypeCode), m.MessageTplFileName, m.MessageTplFilePath, m.FlagActive, m.UpdatedAt, m.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) mẫu thông điệp theo mã (theo Mst_MessageTemplate_Create/Update của TVAN gốc).
app.MapPost("/api/message-templates", async (MessageTemplateDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveMessageTemplateAsync(dto.Code ?? "", dto.Name ?? "", dto.Type, dto.Content ?? "", dto.FileName, dto.FileSpec, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa mẫu thông điệp theo mã (theo Mst_MessageTemplate_Delete của TVAN gốc).
app.MapDelete("/api/message-templates/{code}", async (string code, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteMessageTemplateAsync(code);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục khách hàng / người mua (theo Mst_CustomerNNT của TVAN gốc): danh sách (lọc theo MST nếu có).
app.MapGet("/api/customers", async (string? mst, ITvanService svc) =>
{
    var ls = await svc.CustomerNntsAsync(mst);
    return Results.Ok(ls.Select(c => new { c.Id, c.MST, c.CustomerNNTCode, c.CustomerNNTName, c.CustomerMST, c.CustomerNNTType, c.CustomerNNTAddress, c.CustomerNNTEmail, c.CustomerNNTPhone, c.ContactName, c.AccNo, c.BankName, c.FlagActive, c.UpdatedAt, c.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) khách hàng theo khóa (MST, CustomerNNTCode) (theo Mst_CustomerNNT_Create/Update của TVAN gốc).
app.MapPost("/api/customers", async (CustomerNntDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveCustomerNntAsync(dto.Id, dto.Mst ?? "", dto.Code ?? "", dto.Name ?? "", dto.CustomerMst, dto.Type, dto.Address, dto.Email, dto.Phone, dto.Fax, dto.ContactName, dto.ContactPhone, dto.ContactEmail, dto.Dob, dto.ProvinceCode, dto.DistrictCode, dto.AccNo, dto.BankName, dto.GovIdType, dto.GovId, dto.Remark, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa khách hàng theo id (theo Mst_CustomerNNT_Delete của TVAN gốc).
app.MapDelete("/api/customers/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteCustomerNntAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục loại người nộp thuế (theo Mst_NNTType của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/nnt-types", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.NntTypesAsync(keyword);
    return Results.Ok(ls.Select(t => new { t.Id, t.NNTType, t.NNTTypeName, t.FlagActive, t.UpdatedAt, t.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) loại NNT theo mã (theo Mst_NNTType_Create/Update của TVAN gốc).
app.MapPost("/api/nnt-types", async (NntTypeDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveNntTypeAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa loại NNT theo id (theo Mst_NNTType_Delete của TVAN gốc).
app.MapDelete("/api/nnt-types/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteNntTypeAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục thuế suất VAT (theo Mst_VATRate của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/vat-rates", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.VatRatesAsync(keyword);
    return Results.Ok(ls.Select(t => new { t.Id, t.VATRateCode, t.VATRate, t.VATDesc, t.FlagActive, t.UpdatedAt, t.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) thuế suất VAT theo mã (theo Mst_VATRate_Create/Update của TVAN gốc).
app.MapPost("/api/vat-rates", async (VatRateDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveVatRateAsync(dto.Id, dto.Code ?? "", dto.Rate ?? "", dto.Desc, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa thuế suất VAT theo id (theo Mst_VATRate_Delete của TVAN gốc).
app.MapDelete("/api/vat-rates/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteVatRateAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục đơn vị tính (theo Mst_Unit của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/units", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.UnitsAsync(keyword);
    return Results.Ok(ls.Select(t => new { t.Id, t.UnitCode, t.UnitName, t.Remark, t.FlagActive, t.UpdatedAt, t.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) đơn vị tính theo mã (theo Mst_Unit_Create/Update của TVAN gốc).
app.MapPost("/api/units", async (UnitDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveUnitAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.Remark, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa đơn vị tính theo id (theo Mst_Unit_Delete của TVAN gốc).
app.MapDelete("/api/units/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteUnitAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục Thương hiệu (theo Mst_Brand của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/brands", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.BrandsAsync(keyword);
    return Results.Ok(ls.Select(t => new { t.Id, t.BrandCode, t.BrandName, t.Remark, t.FlagActive, t.UpdatedAt, t.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) thương hiệu theo mã (theo Mst_Brand_Create/Update của TVAN gốc).
app.MapPost("/api/brands", async (BrandDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveBrandAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.Remark, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa thương hiệu theo id (theo Mst_Brand_Delete của TVAN gốc).
app.MapDelete("/api/brands/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteBrandAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục Model sản phẩm (theo Mst_Model của TVAN gốc): danh sách (lọc theo thương hiệu + từ khóa nếu có).
app.MapGet("/api/product-models", async (string? brandCode, string? keyword, ITvanService svc) =>
{
    var ls = await svc.ProductModelsAsync(brandCode, keyword);
    return Results.Ok(ls.Select(t => new { t.Id, t.ModelCode, t.ModelName, t.OrgModelCode, t.BrandCode, t.Remark, t.FlagActive, t.UpdatedAt, t.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) model sản phẩm theo mã (theo Mst_Model_Create/Update của TVAN gốc).
app.MapPost("/api/product-models", async (ProductModelDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveProductModelAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.OrgModelCode, dto.BrandCode ?? "", dto.Remark, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa model sản phẩm theo id (theo Mst_Model_Delete của TVAN gốc).
app.MapDelete("/api/product-models/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteProductModelAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục loại khách hàng / người mua (theo Mst_CustomerNNTType của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/customer-nnt-types", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.CustomerNntTypesAsync(keyword);
    return Results.Ok(ls.Select(t => new { t.Id, t.CustomerNNTType, t.CustomerNNTTypeName, t.Remark, t.FlagActive, t.UpdatedAt, t.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) loại khách hàng theo mã (theo Mst_CustomerNNTType_Create/Update của TVAN gốc).
app.MapPost("/api/customer-nnt-types", async (CustomerNntTypeDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveCustomerNntTypeAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.Remark, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa loại khách hàng theo id (theo Mst_CustomerNNTType_Delete của TVAN gốc).
app.MapDelete("/api/customer-nnt-types/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteCustomerNntTypeAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục Tỉnh/Thành phố (theo Mst_Province của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/provinces", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.ProvincesAsync(keyword);
    return Results.Ok(ls.Select(p => new { p.Id, p.ProvinceCode, p.ProvinceName, p.FlagActive, p.UpdatedAt, p.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) tỉnh/thành theo mã (theo Mst_Province_Create/Update của TVAN gốc).
app.MapPost("/api/provinces", async (ProvinceDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveProvinceAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa tỉnh/thành theo id (theo Mst_Province_Delete của TVAN gốc).
app.MapDelete("/api/provinces/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteProvinceAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục Quận/Huyện (theo Mst_District của TVAN gốc): danh sách (lọc theo tỉnh/thành + từ khóa nếu có).
app.MapGet("/api/districts", async (string? provinceCode, string? keyword, ITvanService svc) =>
{
    var ls = await svc.DistrictsAsync(provinceCode, keyword);
    return Results.Ok(ls.Select(d => new { d.Id, d.ProvinceCode, d.DistrictCode, d.DistrictName, d.FlagActive, d.UpdatedAt, d.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) quận/huyện theo khóa (ProvinceCode, DistrictCode) (theo Mst_District_Create/Update của TVAN gốc).
app.MapPost("/api/districts", async (DistrictDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveDistrictAsync(dto.Id, dto.ProvinceCode ?? "", dto.Code ?? "", dto.Name ?? "", dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa quận/huyện theo id (theo Mst_District_Delete của TVAN gốc).
app.MapDelete("/api/districts/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteDistrictAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục Quốc gia (theo Mst_Country của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/countries", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.CountriesAsync(keyword);
    return Results.Ok(ls.Select(c => new { c.Id, c.CountryCode, c.CountryName, c.FlagActive, c.UpdatedAt, c.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) quốc gia theo mã (theo Mst_Country_Create/Update của TVAN gốc).
app.MapPost("/api/countries", async (CountryDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveCountryAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa quốc gia theo id (theo Mst_Country_Delete của TVAN gốc).
app.MapDelete("/api/countries/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteCountryAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục Đại lý (theo Mst_Dealer của TVAN gốc): danh sách (lọc theo tỉnh/thành + từ khóa nếu có).
app.MapGet("/api/dealers", async (string? provinceCode, string? keyword, ITvanService svc) =>
{
    var ls = await svc.DealersAsync(keyword, provinceCode);
    return Results.Ok(ls.Select(d => new { d.Id, d.DLCode, d.DLName, d.ProvinceCode, d.DLAddress, d.DLPresentBy, d.DLGovIDNumber, d.DLEmail, d.DLPhoneNo, d.FlagActive, d.UpdatedAt, d.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) đại lý theo mã (theo Mst_Dealer_Create/Update của TVAN gốc).
app.MapPost("/api/dealers", async (DealerDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveDealerAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.ProvinceCode ?? "", dto.Address, dto.PresentBy, dto.GovIdNumber, dto.Email, dto.Phone, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa đại lý theo id (theo Mst_Dealer_Delete của TVAN gốc).
app.MapDelete("/api/dealers/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteDealerAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục Phòng ban (theo Mst_Department của TVAN gốc): danh sách (lọc theo MST + từ khóa nếu có).
app.MapGet("/api/departments", async (string? mst, string? keyword, ITvanService svc) =>
{
    var ls = await svc.DepartmentsAsync(mst, keyword);
    return Results.Ok(ls.Select(d => new { d.Id, d.DepartmentCode, d.DepartmentCodeParent, d.DepartmentBUCode, d.DepartmentBUPattern, d.DepartmentLevel, d.MST, d.DepartmentName, d.FlagActive, d.UpdatedAt, d.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) phòng ban theo mã (theo Mst_Department_Create/Update của TVAN gốc).
app.MapPost("/api/departments", async (DepartmentDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveDepartmentAsync(dto.Id, dto.Code ?? "", dto.CodeParent, dto.Mst ?? "", dto.Name ?? "", dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa phòng ban theo id (theo Mst_Department_Delete của TVAN gốc).
app.MapDelete("/api/departments/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteDepartmentAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Chứng thư số của tổ chức (theo Mst_OrgCKS của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/org-cks", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.OrgCksesAsync(keyword);
    return Results.Ok(ls.Select(c => new { c.Id, c.CANumber, c.CAOrg, c.Subject, c.CAEffDTimeUTCStart, c.CAEffDTimeUTCEnd, c.CTSPath, c.FlagActive, c.UpdatedAt, c.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) chứng thư số theo số chứng thư (theo Mst_OrgCKS_Create/Update của TVAN gốc).
app.MapPost("/api/org-cks", async (OrgCksDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveOrgCksAsync(dto.Id, dto.CaNumber ?? "", dto.CaOrg, dto.Subject, dto.EffStart, dto.EffEnd, dto.CtsPath, dto.CtsPwd, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa chứng thư số theo id (theo Mst_OrgCKS_Delete của TVAN gốc).
app.MapDelete("/api/org-cks/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteOrgCksAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục loại thông báo (theo Mst_NotifyType của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/notify-types", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.NotifyTypesAsync(keyword);
    return Results.Ok(ls.Select(t => new { t.Id, t.NotifyTypeCode, t.NotifyDesc, t.DefaultActive, t.FlagActive, t.UpdatedAt, t.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) loại thông báo theo mã (theo Mst_NotifyType_Create/Update của TVAN gốc).
app.MapPost("/api/notify-types", async (NotifyTypeDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveNotifyTypeAsync(dto.Id, dto.Code ?? "", dto.Desc, dto.DefaultActive, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa loại thông báo theo id (theo Mst_NotifyType_Delete của TVAN gốc).
app.MapDelete("/api/notify-types/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteNotifyTypeAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Thông báo hệ thống (theo Notify_Notify của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/notifies", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.NotifiesAsync(keyword);
    return Results.Ok(ls.Select(n => new { n.Id, n.NotifyNo, n.NotifyDesc, n.EffDateStart, n.EffDateEnd, n.FlagSendEmail, n.FlagActive, recipients = n.Details.Count, read = n.Details.Count(d => d.FlagRead) }));
});

// Tạo thông báo (theo Notify_Notify_CreateX_New20200131 của TVAN gốc).
app.MapPost("/api/notifies", async (CreateNotifyDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.CreateNotifyAsync(dto.NotifyNo ?? "", dto.Desc ?? "", dto.EffDateStart, dto.EffDateEnd, dto.SendEmail, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Cập nhật thông báo (theo Notify_Notify_UpdateX của TVAN gốc).
app.MapPost("/api/notifies/{id:int}/update", async (int id, UpdateNotifyDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.UpdateNotifyAsync(id, dto.Desc, dto.SendEmail, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa thông báo theo id (theo Notify_Notify_Delete của TVAN gốc).
app.MapDelete("/api/notifies/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteNotifyAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Gửi thông báo tới một người dùng (theo Notify_NotifyDtl_CreateX của TVAN gốc).
app.MapPost("/api/notifies/{id:int}/recipients", async (int id, AddNotifyDtlDto dto, ITvanService svc) =>
{
    var (ok, msg, dtlId) = await svc.AddNotifyDtlAsync(id, dto.UserCode ?? "", dto.FlagRead, dto.By);
    return ok ? Results.Ok(new { id = dtlId, msg }) : Results.BadRequest(new { id = dtlId, error = msg });
});

// Đánh dấu đã đọc thông báo của một người dùng (theo Notify_NotifyDtl_UpdateX của TVAN gốc).
app.MapPost("/api/notifies/{id:int}/read", async (int id, MarkNotifyReadDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.MarkNotifyReadAsync(id, dto.UserCode ?? "");
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Người nhận thông báo (theo Mst_ManageNotify của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/notify-recipients", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.NotifyRecipientsAsync(keyword);
    return Results.Ok(ls.Select(r => new { r.Id, r.UserCode, r.UserName, r.UpdatedAt, r.UpdatedBy, types = r.Types.Select(t => new { t.NotifyType, t.FlagNotify }) }));
});

// Thêm người nhận thông báo (theo Mst_ManageNotify_CreateX của TVAN gốc): tự tạo đăng ký nhận cho tất cả loại thông báo.
app.MapPost("/api/notify-recipients", async (CreateNotifyRecipientDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.CreateNotifyRecipientAsync(dto.UserCode ?? "", dto.UserName, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Cập nhật tên người nhận (theo Mst_ManageNotify_UpdateX của TVAN gốc).
app.MapPost("/api/notify-recipients/{id:int}/update", async (int id, UpdateNotifyRecipientDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.UpdateNotifyRecipientAsync(id, dto.UserName, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa người nhận (theo Mst_ManageNotify_DeleteX của TVAN gốc): xóa kèm đăng ký nhận loại thông báo.
app.MapDelete("/api/notify-recipients/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteNotifyRecipientAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Lưu đăng ký nhận loại thông báo của một người nhận (theo Map_UserInNotifyType_Save của TVAN gốc).
app.MapPost("/api/notify-recipients/{id:int}/types", async (int id, SaveNotifyRecipientTypesDto dto, ITvanService svc) =>
{
    var types = (dto.Types ?? new()).Select(t => (t.NotifyType ?? "", t.FlagNotify)).ToList();
    var (ok, msg) = await svc.SaveNotifyRecipientTypesAsync(id, types, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Cấu hình định dạng cột hiển thị theo bảng (theo Mst_ColumnConfig của TVAN gốc): danh sách (lọc theo bảng + từ khóa).
app.MapGet("/api/column-configs", async (string? tableName, string? keyword, ITvanService svc) =>
{
    var ls = await svc.ColumnConfigsAsync(tableName, keyword);
    return Results.Ok(ls.Select(c => new { c.Id, c.TableName, c.ColumnName, c.ColumnFormat, c.ColumnDesc, c.FlagActive, c.UpdatedAt, c.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) cấu hình cột theo khóa (TableName, ColumnName) (theo Mst_ColumnConfig_Create/Update của TVAN gốc).
app.MapPost("/api/column-configs", async (ColumnConfigDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveColumnConfigAsync(dto.Id, dto.TableName ?? "", dto.ColumnName ?? "", dto.ColumnFormat, dto.ColumnDesc, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa cấu hình cột theo id (theo Mst_ColumnConfig_Delete của TVAN gốc).
app.MapDelete("/api/column-configs/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteColumnConfigAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Cột hiển thị danh sách hóa đơn theo tổ chức (theo Mst_SortColumnInvoice của TVAN gốc): danh sách (lọc theo từ khóa).
app.MapGet("/api/sort-columns", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.SortColumnInvoicesAsync(keyword);
    return Results.Ok(ls.Select(c => new { c.Id, c.ColumnCode, c.Idx, c.ColumnName, type = c.ColumnType.ToString(), c.FlagActive, c.UpdatedAt, c.UpdatedBy }));
});
// Lưu (tạo mới/cập nhật) cột hiển thị danh sách hóa đơn theo mã cột (theo Mst_SortColumnInvoice_Create/Update của TVAN gốc).
app.MapPost("/api/sort-columns", async (SortColumnDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveSortColumnInvoiceAsync(dto.Id, dto.ColumnCode ?? "", dto.Idx, dto.ColumnName ?? "", dto.Type, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa cột hiển thị danh sách hóa đơn theo id (theo Mst_SortColumnInvoice_Delete của TVAN gốc).
app.MapDelete("/api/sort-columns/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteSortColumnInvoiceAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Danh mục tiền tệ / ngoại tệ (theo Mst_CurrencyEx của TVAN gốc): danh sách (lọc theo từ khóa).
app.MapGet("/api/currencies", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.CurrencyExesAsync(keyword);
    return Results.Ok(ls.Select(c => new { c.Id, c.CurrencyCode, c.CurrencyName, c.BaseCurrencyCode, c.BuyRate, c.SellRate, c.Remark, c.FlagActive, c.UpdatedAt, c.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) tiền tệ theo mã (theo Mst_CurrencyEx của TVAN gốc).
app.MapPost("/api/currencies", async (CurrencyExDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveCurrencyExAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.BaseCode, dto.BuyRate, dto.SellRate, dto.Remark, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa tiền tệ theo id (theo Mst_CurrencyEx của TVAN gốc).
app.MapDelete("/api/currencies/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteCurrencyExAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Đọc số tiền thành chữ tiếng Việt (theo luồng DocTien của TVAN gốc).
app.MapPost("/api/doc-tien", async (DocTienDto dto, ITvanService svc) =>
{
    var (ok, msg, text, id) = await svc.DocTienAsync(dto.Amount, dto.CurrencyCode, dto.By);
    return ok ? Results.Ok(new { id, text, msg }) : Results.BadRequest(new { id, error = msg });
});

// Nhật ký đọc tiền bằng chữ (theo luồng DocTien của TVAN gốc).
app.MapGet("/api/doc-tien-logs", async (ITvanService svc) =>
{
    var ls = await svc.DocTienLogsAsync();
    return Results.Ok(ls.Select(l => new { l.Id, l.Amount, l.CurrencyCode, l.CurrencyName, l.Text, l.By, l.CreatedAt }));
});

// Nhóm người dùng (theo Sys_Group của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/sys-groups", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.SysGroupsAsync(keyword);
    return Results.Ok(ls.Select(g => new { g.Id, g.GroupCode, g.GroupName, g.FlagActive, g.UpdatedAt, g.UpdatedBy, members = g.Members.Select(m => m.UserCode) }));
});

// Lưu (tạo mới/cập nhật) nhóm người dùng theo mã (theo Sys_Group_Create/Update của TVAN gốc).
app.MapPost("/api/sys-groups", async (SysGroupDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveSysGroupAsync(dto.Id, dto.Code ?? "", dto.Name ?? "", dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa nhóm người dùng theo id (theo Sys_Group_Delete của TVAN gốc): xóa kèm phân gán người dùng.
app.MapDelete("/api/sys-groups/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteSysGroupAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Lưu danh sách thành viên của nhóm (theo Sys_UserInGroup_Save của TVAN gốc): thay thế toàn bộ.
app.MapPost("/api/sys-groups/{id:int}/members", async (int id, SaveSysGroupMembersDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.SaveSysGroupMembersAsync(id, dto.UserCodes ?? new(), dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Gói Module (theo Sys_Modules của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/sys-modules", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.SysModulesAsync(keyword);
    return Results.Ok(ls.Select(m => new { m.Id, m.ModuleCode, m.SolutionCode, m.ModuleName, m.Description, m.QtyInvoice, m.ValCapacity, m.FlagActive, m.UpdatedAt, m.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) gói Module theo mã (theo Sys_Modules_Create/Update của TVAN gốc).
app.MapPost("/api/sys-modules", async (SysModuleDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveSysModuleAsync(dto.Id, dto.ModuleCode ?? "", dto.SolutionCode ?? "", dto.ModuleName ?? "", dto.Description, dto.QtyInvoice, dto.ValCapacity, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa gói Module theo id (theo Sys_Modules_Delete của TVAN gốc).
app.MapDelete("/api/sys-modules/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteSysModuleAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Bật/ngừng gói Module (theo Sys_ModulesController.ActiveModule/InactiveModule của TVAN gốc).
app.MapPost("/api/sys-modules/{id:int}/active", async (int id, SysModuleActiveDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.SetSysModuleActiveAsync(id, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Danh sách giải pháp (theo Sys_Solution của TVAN gốc).
app.MapGet("/api/sys-solutions", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.SysSolutionsAsync(keyword);
    return Results.Ok(ls.Select(s => new { s.Id, s.SolutionCode, s.SolutionName, s.FlagActive, s.UpdatedAt, s.UpdatedBy }));
});

// Đối tượng (chức năng) của hệ thống (theo Sys_Object của TVAN gốc): danh sách (lọc theo từ khóa nếu có).
app.MapGet("/api/sys-objects", async (string? keyword, ITvanService svc) =>
{
    var ls = await svc.SysObjectsAsync(keyword);
    return Results.Ok(ls.Select(o => new { o.Id, o.ObjectCode, o.ObjectName, o.ServiceCode, type = o.ObjectType.ToString(), o.FlagActive, o.UpdatedAt, o.UpdatedBy }));
});

// Lưu (tạo mới/cập nhật) đối tượng theo mã (theo Sys_Object của TVAN gốc).
app.MapPost("/api/sys-objects", async (SysObjectDto dto, ITvanService svc) =>
{
    var (ok, msg, id) = await svc.SaveSysObjectAsync(dto.Id, dto.ObjectCode ?? "", dto.ObjectName ?? "", dto.ServiceCode, dto.ObjectType, dto.Active, dto.By);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { id, error = msg });
});

// Xóa đối tượng theo id (theo Sys_Object của TVAN gốc): xóa kèm phân gán vào gói Module.
app.MapDelete("/api/sys-objects/{id:int}", async (int id, ITvanService svc) =>
{
    var (ok, msg) = await svc.DeleteSysObjectAsync(id);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

// Phân gán đối tượng vào gói Module (theo Sys_ObjectInModules của TVAN gốc): danh sách (lọc theo mã gói nếu có).
app.MapGet("/api/sys-object-in-modules", async (string? moduleCode, ITvanService svc) =>
{
    var ls = await svc.SysObjectInModulesAsync(moduleCode);
    return Results.Ok(ls.Select(m => new { m.Id, m.ModuleCode, m.ObjectCode, m.UpdatedAt, m.UpdatedBy }));
});

// Lưu danh sách đối tượng gán vào gói Module (theo Sys_ObjectInModules_Save của TVAN gốc): thay thế toàn bộ.
app.MapPost("/api/sys-object-in-modules", async (SaveSysObjectInModulesDto dto, ITvanService svc) =>
{
    var (ok, msg) = await svc.SaveSysObjectInModulesAsync(dto.ModuleId, dto.ObjectCodes ?? new(), dto.By);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { error = msg });
});

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

record TctCallback(string TctCode, string Code, string Text);
record ExtInvoiceDto(string? SellerMst, string? SellerName, string? BuyerName, string? BuyerMst, string? BuyerAddress, decimal Amount, decimal VatRate, string? DocRef);
record RegisterOrgDto(string Name);
record ImportNntDto(string? Mst, string? Name, string? Address, string? Email);
record ImportInvDto(string? SellerMst, string? Symbol, string? No, string? BuyerName, string? BuyerMst, string? BuyerAddress, decimal Amount, decimal VatRate, DateTime? IssuedDate, string? TctCode);
record AdjustDto(InvoiceAdjType AdjType, decimal Amount, decimal VatRate, string? Reason);
record ReplaceDto(decimal Amount, decimal VatRate, string? Reason);
record ToPendingDto(string? Reason);
record RestoreDto(string? Reason);
record DeleteAdjReplaceDto(string? Reason);
record DeleteInvoiceDto(string? Remark, string? By);
record LicenseIncreaseDto(int NntId, int Qty, string? Note);
record GuiTongHopDto(int NntId, PeriodType LKDLieu, string? KDLieu, int BSLThu, string? Note);
record SendEmailDto(string? ToEmail, string? SentBy);
record ConversionPrintDto(string? Note, string? By);
record ReSignDto(string? FileSpec, string? Note, string? By);
record ApproveDto(string? FilePath, string? PdfFilePath, string? Note, string? By);
record UnapproveDto(string? Note, string? By);
record BulkApproveDto(List<int>? Ids, string? Note, string? By);
record BulkDeleteDto(List<int>? Ids, string? Note, string? By);
record IssueDto(string? EmailSend, string? Note, string? By);
record Sign60DayDto(Sign60DayFlag Flag, string? Note);
record DynamicCommaDto(DynamicCommaStyle FlagStyle, string? By);
record AllocateNoDto(DateTime? InvoiceDate, string? By);
record AllocateApproveIssueDto(DateTime? InvoiceDate, string? FilePath, string? PdfFilePath, string? EmailSend, string? Note, string? By);
record IssueTemplateDto(DateTime? EffDateStart, string? Remark);
record InactivateTemplateDto(string? Remark);
record CancelTemplateDto(string? Remark, string? By);
record IncreaseEndNoDto(int NewEndInvoiceNo, string? Remark, string? By);
record UpdateQtyNoDto(int StartInvoiceNo, int EndInvoiceNo, string? Remark, string? By);
record SendTemplateTctDto(string? Remark, string? By);
record ReceiveTemplateTctDto(TctAcceptStatus ChapNhan, string? Message, string? By);
record TctReceiveDto(TctMessageType MltDiep, string? MaCQT, string? MaLoi, string? LyDo);
record SendTct300Dto(ReplaceOrAdjustFlag FlagReplaceOrAdjust, string? LoaiTb, string? SoTb, DateTime? NgayTb, string? LyDo, string? By);
record UpdateAfterAllocatedDto(string? BuyerName, string? BuyerMst, string? BuyerAddress, PaymentMethod PaymentMethod, decimal Amount, decimal VatRate, DateTime? InvoiceDate, string? Note, string? By);
record CancelInvoiceDto(string? Remark, string? By);
record CreateRecordDto(RecordType Type, string? FileName, string? FileSpec, string? Reason, string? By);
record CustomFieldDto(string? Code, string? Name, DBPhysicalType Type, bool Active, string? By);
record TemplateContactDto(string? NntName, string? NntAddress, string? NntPhone, string? NntEmail, string? NntWebsite, bool FlagStyleComma, string? By);
record TemplateBankDto(string? NntAccNo, string? NntBankName, string? By);
record SaveTemplateDto(int? Id, string? TInvoiceCode, int NntId, string? TInvoiceName, string? FormNo, string? Sign, InvoiceNoRule TTType, string? Remark, string? By);
record TempGroupFieldDto(string? FieldName, string? TcfType);
record TempGroupDto(int? Id, string? Code, string? Mst, VATType VatType, string? Name, string? Body, string? Thumbnail, SpecPrdType SpecPrdType, bool Active, List<TempGroupFieldDto>? Fields, string? By);
record MessageTemplateDto(string? Code, string? Name, MessageTypeCode Type, string? Content, string? FileName, string? FileSpec, string? By);
record CustomerNntDto(int? Id, string? Mst, string? Code, string? Name, string? CustomerMst, string? Type, string? Address, string? Email, string? Phone, string? Fax, string? ContactName, string? ContactPhone, string? ContactEmail, DateTime? Dob, string? ProvinceCode, string? DistrictCode, string? AccNo, string? BankName, string? GovIdType, string? GovId, string? Remark, bool Active, string? By);
record NntTypeDto(int? Id, string? Code, string? Name, bool Active, string? By);
record VatRateDto(int? Id, string? Code, string? Rate, string? Desc, bool Active, string? By);
record UnitDto(int? Id, string? Code, string? Name, string? Remark, bool Active, string? By);
record BrandDto(int? Id, string? Code, string? Name, string? Remark, bool Active, string? By);
record ProductModelDto(int? Id, string? Code, string? Name, string? OrgModelCode, string? BrandCode, string? Remark, bool Active, string? By);
record CustomerNntTypeDto(int? Id, string? Code, string? Name, string? Remark, bool Active, string? By);
record ProvinceDto(int? Id, string? Code, string? Name, bool Active, string? By);
record DistrictDto(int? Id, string? ProvinceCode, string? Code, string? Name, bool Active, string? By);
record CountryDto(int? Id, string? Code, string? Name, bool Active, string? By);
record DealerDto(int? Id, string? Code, string? Name, string? ProvinceCode, string? Address, string? PresentBy, string? GovIdNumber, string? Email, string? Phone, bool Active, string? By);
record DepartmentDto(int? Id, string? Code, string? CodeParent, string? Mst, string? Name, bool Active, string? By);
record OrgCksDto(int? Id, string? CaNumber, string? CaOrg, string? Subject, DateTime? EffStart, DateTime? EffEnd, string? CtsPath, string? CtsPwd, bool Active, string? By);
record NotifyTypeDto(int? Id, string? Code, string? Desc, bool DefaultActive, bool Active, string? By);
record CreateNotifyDto(string? NotifyNo, string? Desc, DateTime EffDateStart, DateTime EffDateEnd, bool SendEmail, string? By);
record UpdateNotifyDto(string? Desc, bool SendEmail, string? By);
record AddNotifyDtlDto(string? UserCode, bool FlagRead, string? By);
record MarkNotifyReadDto(string? UserCode);
record CreateNotifyRecipientDto(string? UserCode, string? UserName, string? By);
record UpdateNotifyRecipientDto(string? UserName, string? By);
record NotifyRecipientTypeDto(string? NotifyType, bool FlagNotify);
record SaveNotifyRecipientTypesDto(List<NotifyRecipientTypeDto>? Types, string? By);
record ColumnConfigDto(int? Id, string? TableName, string? ColumnName, string? ColumnFormat, string? ColumnDesc, bool Active, string? By);
record SortColumnDto(int? Id, string? ColumnCode, int Idx, string? ColumnName, SortColumnType Type, bool Active, string? By);
record CurrencyExDto(int? Id, string? Code, string? Name, string? BaseCode, decimal BuyRate, decimal SellRate, string? Remark, bool Active, string? By);
record DocTienDto(decimal Amount, string? CurrencyCode, string? By);
record SysGroupDto(int? Id, string? Code, string? Name, bool Active, string? By);
record SaveSysGroupMembersDto(List<string>? UserCodes, string? By);
record SysModuleDto(int? Id, string? ModuleCode, string? SolutionCode, string? ModuleName, string? Description, double QtyInvoice, double ValCapacity, bool Active, string? By);
record SysModuleActiveDto(bool Active, string? By);
record SysObjectDto(int? Id, string? ObjectCode, string? ObjectName, string? ServiceCode, SysObjectType ObjectType, bool Active, string? By);
record SaveSysObjectInModulesDto(int ModuleId, List<string>? ObjectCodes, string? By);
record TaxOfficeDto(int? Id, string? Code, string? CodeParent, string? ProvinceCode, string? DistrictCode, string? Name, string? Level, string? Address, string? ContactEmail, string? ContactPhone, bool Active, string? By);
