using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FINAPSA.Data;
using FINAPSA.Models;
using FINAPSA.Services;

var builder = WebApplication.CreateBuilder(args);

// ===================== DATABASE =====================
builder.Services.AddDbContext<FINAPSADbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===================== IDENTITY =====================
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // Students use their admission number as their account password.
    // Admission numbers can be any format (e.g. "ADM001", "2025/001", "FIN-01")
    // so all strict password rules are turned off.
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 3;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase = false;
    options.Password.RequiredUniqueChars = 1;
})
.AddEntityFrameworkStores<FINAPSADbContext>()
.AddDefaultTokenProviders();

// ===================== GOOGLE OAUTH =====================
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.SaveTokens = true;
            options.Scope.Add("profile");
            options.Scope.Add("email");
        });
}

// ===================== PAYSTACK =====================
builder.Services.AddHttpClient<PaystackService>();

// ===================== OTHER SERVICES =====================
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<BursarService>();
builder.Services.AddScoped<IBulkOperationService, BulkOperationService>();
builder.Services.AddScoped<IClassService, ClassService>();

// ===================== MVC =====================
builder.Services.AddControllersWithViews();

// ===================== COOKIE =====================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<FINAPSA.Middleware.NavigationAuditMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<FINAPSADbContext>();
        await db.Database.MigrateAsync();

        await RoleSeeder.SeedRolesAsync(services);
        await RoleSeeder.SeedAdminUserAsync(services);

        // Seed fixed classes (Creche, Playgroup, Basic 1-6 etc.)
        await ClassSeeder.SeedClassesAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Startup seeding failed: {ex.Message}");
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();