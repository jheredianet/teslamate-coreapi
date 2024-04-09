using coreAPI.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
var Settings = builder.Configuration.GetSection("Settings").Get<AppSettings>();
if (Settings != null)
{
    builder.Services.AddTransient<AppSettings>(provider => new AppSettings(Settings));
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
