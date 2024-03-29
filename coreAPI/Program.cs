using coreAPI.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
}

app.UseAuthorization();

app.MapControllers();

app.Run();
