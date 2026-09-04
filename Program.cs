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

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

record TctCallback(string TctCode, string Code, string Text);
record ExtInvoiceDto(string? SellerMst, string? SellerName, string? BuyerName, string? BuyerMst, string? BuyerAddress, decimal Amount, decimal VatRate, string? DocRef);
record RegisterOrgDto(string Name);
record ImportNntDto(string? Mst, string? Name, string? Address, string? Email);
record ImportInvDto(string? SellerMst, string? Symbol, string? No, string? BuyerName, string? BuyerMst, string? BuyerAddress, decimal Amount, decimal VatRate, DateTime? IssuedDate, string? TctCode);
