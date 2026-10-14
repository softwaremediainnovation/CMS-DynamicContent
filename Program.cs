using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Hubs;
using CmsBillboard.DynamicContent.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// SQLite Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                      ?? "Data Source=DynamicContent.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Application Services
builder.Services.AddScoped<ISurveyService, SurveyService>();

var app = builder.Build();

// Ensure Database & Initial Seed Data is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapHub<SurveyHub>("/hubs/survey");

app.Run();
