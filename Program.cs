using Microsoft.EntityFrameworkCore;
using TwoFactorAuthApp.Models;
using TwoFactorAuthApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Configure Entity Framework with SQL Server
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure session for 2FA state management
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add IHttpContextAccessor for views
builder.Services.AddHttpContextAccessor();

// Register Email Service — toggle via appsettings.json EmailSettings.UseRealEmailService
var emailSettings = builder.Configuration.GetSection("EmailSettings");
bool useRealEmail = emailSettings.GetValue<bool>("UseRealEmailService");

if (useRealEmail)
{
    builder.Services.Configure<SmtpSettings>(
        emailSettings.GetSection("Smtp"));
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
    Console.WriteLine("Email Service: SMTP (MailKit) — real emails enabled.");
}
else
{
    builder.Services.AddScoped<IEmailService, MockEmailService>();
    Console.WriteLine("Email Service: Mock — codes logged to console only.");
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseSession();  // Enable session middleware

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Register}/{id?}");

app.Run();
