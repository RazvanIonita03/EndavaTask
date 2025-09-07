using CarInsurance.Api.Configuration;
using CarInsurance.Api.Data;
using CarInsurance.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PolicyExpirationOptions>(
    builder.Configuration.GetSection(PolicyExpirationOptions.SectionName));

// Use InMemory for integration tests; Sqlite otherwise
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(opt =>
    {
        opt.UseInMemoryDatabase("TestDb");
    });
}
else
{
    builder.Services.AddDbContext<AppDbContext>(opt =>
    {
        opt.UseSqlite(builder.Configuration.GetConnectionString("Default"));
    });
}

builder.Services.AddScoped<CarService>();
builder.Services.AddScoped<IPolicyExpirationService, PolicyExpirationService>();

builder.Services.AddHostedService<PolicyExpirationBackgroundService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure DB and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedData.EnsureSeeded(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
