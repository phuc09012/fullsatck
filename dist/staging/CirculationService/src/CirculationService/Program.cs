using CirculationService.Data;
using CirculationService.Options;
using CirculationService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<BorrowingOptions>(builder.Configuration.GetSection("Borrowing"));
builder.Services.Configure<IntegrationEventDeliveryOptions>(builder.Configuration.GetSection("IntegrationEvents"));

builder.Services.AddHttpClient<CatalogClient>(client =>
{
    var baseUrl = builder.Configuration["CatalogService:BaseUrl"];
    client.BaseAddress = new Uri(baseUrl ?? "http://localhost:5001");
});

builder.Services.AddHttpClient<IntegrationEventPublisher>();

builder.Services.AddDbContext<CirculationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CirculationDb")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var retries = 10;

    while (true)
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
            await CirculationDbSeeder.SeedAsync(context);
            break;
        }
        catch (Exception ex) when (retries-- > 0)
        {
            logger.LogWarning(ex, "Circulation database is not ready yet. Retrying...");
            await Task.Delay(3000);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
