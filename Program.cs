using ImportDataToERP.Data;
using ImportDataToERP.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Session (用於保存使用者選擇的ERP公司別，僅本次Session期間有效)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cookie 驗證
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// 註冊 DbConnectionFactory
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

// ERP Connection String
var erpConnectionString = builder.Configuration.GetConnectionString("ErpConnection") ?? "";

// 註冊 Services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ErpCompanyService>(sp => new ErpCompanyService(erpConnectionString));
builder.Services.AddScoped<ErpConnectionAccessor>(sp =>
    new ErpConnectionAccessor(erpConnectionString, sp.GetRequiredService<IHttpContextAccessor>()));
builder.Services.AddScoped<OrderImportService>(sp =>
    new OrderImportService(sp.GetRequiredService<DbConnectionFactory>(), sp.GetRequiredService<ErpConnectionAccessor>()));
builder.Services.AddScoped<OrderChangeImportService>(sp =>
    new OrderChangeImportService(sp.GetRequiredService<DbConnectionFactory>(), sp.GetRequiredService<ErpConnectionAccessor>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
