using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Hubs;
using CmsBillboard.DynamicContent.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    // Protect /Admin folder with authorization, except /Admin/Login
    options.Conventions.AuthorizeFolder("/Admin");
    options.Conventions.AllowAnonymousToPage("/Admin/Login");
});

builder.Services.AddControllers();
builder.Services.AddSignalR();

// Cookie Authentication for Admin Dashboard
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.AccessDeniedPath = "/Admin/Login";
        options.Cookie.Name = "WWM_DynamicDOOH_Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// SQL Server Database Context (Matching ERP_WEB pattern)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Application Services
builder.Services.AddSingleton<ISecurityTokenService, SecurityTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISurveyService, SurveyService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapHub<SurveyHub>("/hubs/survey");

app.Run();
