using Microsoft.EntityFrameworkCore;
using NPMS.Core.Data;
using NPMS.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure SQLite database
var dbPath = Path.Combine(AppContext.BaseDirectory, "npms.db");
builder.Services.AddDbContext<NpmsDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<IProductService, ProductService>();

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Auto seed database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NpmsDbContext>();
    NpmsDbContext.SeedDatabase(db);
}

app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Fallback to index.html for Web UI single page app
app.MapFallbackToFile("index.html");

app.Run();
