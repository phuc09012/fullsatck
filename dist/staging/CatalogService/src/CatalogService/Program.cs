using CatalogService.Data;
using CatalogService.Options;
using CatalogService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<IntegrationEventDeliveryOptions>(
    builder.Configuration.GetSection("IntegrationEvents"));

builder.Services.AddHttpClient<IntegrationEventPublisher>();

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDb")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var retries = 10;

    while (true)
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await CatalogDbSeeder.SeedAsync(context);
            break;
        }
        catch (Exception ex) when (retries-- > 0)
        {
            logger.LogWarning(ex, "Catalog database is not ready yet. Retrying...");
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
