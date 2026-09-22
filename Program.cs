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
    return Results.Ok(ls.Select(t => new { t.GovTaxID, t.GovTaxName, t.Address, t.ContactEmail, t.ContactPhone, t.FlagActive }));
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
    return Results.Ok(ls.Select(l => new { l.Id, l.TemplateId, form = l.Template != null ? l.Template.FormNo : null, action = l.Action.ToString(), l.OldEndInvoiceNo, l.NewEndInvoiceNo, l.Remark, l.By, l.CreatedAt }));
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
record IssueDto(string? EmailSend, string? Note, string? By);
record Sign60DayDto(Sign60DayFlag Flag, string? Note);
record AllocateNoDto(DateTime? InvoiceDate, string? By);
record IssueTemplateDto(DateTime? EffDateStart, string? Remark);
record InactivateTemplateDto(string? Remark);
record IncreaseEndNoDto(int NewEndInvoiceNo, string? Remark, string? By);
record TctReceiveDto(TctMessageType MltDiep, string? MaCQT, string? MaLoi, string? LyDo);
record UpdateAfterAllocatedDto(string? BuyerName, string? BuyerMst, string? BuyerAddress, PaymentMethod PaymentMethod, decimal Amount, decimal VatRate, DateTime? InvoiceDate, string? Note, string? By);
