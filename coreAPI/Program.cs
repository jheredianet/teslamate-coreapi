using coreAPI.Classes;
using coreAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

var cultureInfo = new CultureInfo("es-ES");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;


// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// Access to PostgreSQL 
var ConnectionString = builder.Configuration.GetConnectionString("PostgreSqlConnection");
builder.Services.AddDbContext<teslamateContext>(options => options.UseNpgsql(ConnectionString));

// Access to InfluxDB
builder.Services.AddTransient(provider =>
    new InfluxDbConnection(
        builder.Configuration["InfluxDB:Url"] ?? "",
        builder.Configuration["InfluxDB:Token"] ?? "",
        builder.Configuration["InfluxDB:Bucket"] ?? "",
        builder.Configuration["InfluxDB:Organization"] ?? "")
    );

// Access to AppSettings
var settings = builder.Configuration.GetSection("Settings").Get<AppSettings>() ?? new AppSettings();
settings.CurrentPath = builder.Environment.ContentRootPath;

// ImportPath puede ser absoluto en producción (/app/import) o relativo en desarrollo (import).
settings.ImportPath = string.IsNullOrWhiteSpace(settings.ImportPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "import")
    : Path.IsPathRooted(settings.ImportPath)
        ? settings.ImportPath
        : Path.Combine(builder.Environment.ContentRootPath, settings.ImportPath);

Directory.CreateDirectory(settings.ImportPath);
builder.Services.AddSingleton(settings);
builder.Services.AddOptions<M3UOptions>().Configure(options =>
{
    options.FilePath = Path.Combine(settings.ImportPath, "lista.m3u");
    options.DatabasePath = Path.Combine(settings.ImportPath, "m3u.sqlite");
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<M3UService>();
builder.Services.AddSingleton<ServerMappingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.UseExceptionHandler("/Home/Error");
}

//var ContentRootPath = builder.Configuration["ContentRootPath"];
//if (!string.IsNullOrEmpty(ContentRootPath))
//{
//    app.UseStaticFiles(new StaticFileOptions
//    {
//        // Serve files from the current directory. Necessary for the web interface to work in Docker
//        FileProvider = new PhysicalFileProvider(ContentRootPath),
//        RequestPath = ""
//    });
//}
//else
//{
//    app.UseStaticFiles();
//}

app.UseStaticFiles();
app.UseRouting();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "m3u_shortcut",
    pattern: "m3u/{action=Index}/{id?}",
    defaults: new { controller = "M3U" });

app.Run();
