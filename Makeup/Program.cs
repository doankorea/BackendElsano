using Makeup.Hubs;
using Makeup.Models;
using Makeup.Services;
using Makeup.Services.Vnpay;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IMessageService, MessageService>();

// Đăng ký service gửi email
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Đăng ký Background Service để tự động hủy các lịch hẹn đã quá hạn
builder.Services.AddHostedService<AppointmentCleanupService>();

// Đăng ký Background Service để tự động hủy các lịch hẹn đã quá hạn
builder.Services.AddHostedService<AppointmentCleanupJob>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSignalR();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.IsEssential = true;
});
builder.Services.AddDbContext<MakeupContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình CORS
builder.Services.AddCors(options =>
{
    // Policy cho các API
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // Policy riêng cho SignalR
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Default CORS policy for all controllers
builder.Services.AddMvc(options => 
{
    options.EnableEndpointRouting = false;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // giữ nguyên PascalCase
    });
// Đăng ký Identity với User và Role đều dùng khóa int
builder.Services.AddIdentity<User, IdentityRole<int>>(options => {
    // Yêu cầu xác thực email
    options.SignIn.RequireConfirmedEmail = true;
    // Cấu hình mã thông báo (token) cho xác nhận email
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
})
    .AddEntityFrameworkStores<MakeupContext>()
    .AddDefaultTokenProviders();

// Cấu hình cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.Cookie.Name = "MakeupAuth";
    options.SlidingExpiration = true;
});

builder.Services.AddAuthentication(options =>
{
    //options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    //options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    //options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;

});
//    .AddCookie().AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
//{
//    options.ClientId = builder.Configuration.GetSection("GoogleKeys:ClientId").Value;
//    options.ClientSecret = builder.Configuration.GetSection("GoogleKeys:ClientSecret").Value;
//});
// builder.Services.AddHostedService<AppointmentReminderService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;

    // User settings.
    options.User.RequireUniqueEmail = true;
});

var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Middleware pipeline đúng thứ tự
app.UseRouting();

// CORS phải nằm sau UseRouting và trước UseAuthentication
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Cấu hình endpoints
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
        .RequireCors("AllowAll");

    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Account}/{action=Login}/{id?}")
        .RequireCors("AllowAll");

    // Cấu hình hub với CORS riêng cho SignalR
    endpoints.MapHub<ChatHub>("/chatHub").RequireCors("SignalRPolicy");
    endpoints.MapHub<MobileChatHub>("/mobileChatHub").RequireCors("SignalRPolicy");
});

app.Run();
