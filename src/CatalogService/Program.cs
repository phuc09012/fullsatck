using System.Text;
using CatalogService.Data;
using CatalogService.Options;
using CatalogService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<IntegrationEventDeliveryOptions>(
    builder.Configuration.GetSection("IntegrationEvents"));
builder.Services.Configure<InternalApiOptions>(
    builder.Configuration.GetSection("InternalApi"));

var internalApiKey = builder.Configuration["InternalApi:Key"] ?? "LibraryInternalSecretChangeMe!";

builder.Services.AddHttpClient<IntegrationEventPublisher>(client =>
{
    client.DefaultRequestHeaders.Add(InternalRequestHeaders.ApiKey, internalApiKey);
});
builder.Services.AddHttpClient<ExternalBookLookupService>(client =>
{
    client.BaseAddress = new Uri("https://openlibrary.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BtlFullstackLibrary/1.0");
});

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDb")));

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? "ChangeThisKeyToSomethingAtLeast32CharsLong!";
var jwtIssuer = jwtSection["Issuer"] ?? "LibraryAuth";
var jwtAudience = jwtSection["Audience"] ?? "LibraryUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var retries = 30;

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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
