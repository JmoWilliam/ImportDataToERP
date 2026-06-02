using ImportDataToERP.Data;
using ImportDataToERP.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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
builder.Services.AddScoped<QuotationImportService>();
builder.Services.AddScoped<OrderImportService>(sp =>
    new OrderImportService(sp.GetRequiredService<DbConnectionFactory>(), erpConnectionString));
builder.Services.AddScoped<OrderChangeImportService>(sp =>
    new OrderChangeImportService(sp.GetRequiredService<DbConnectionFactory>(), erpConnectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
