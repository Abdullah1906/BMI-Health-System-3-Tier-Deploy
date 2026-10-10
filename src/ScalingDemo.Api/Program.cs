using ScalingDemo.Api.Data;
using ScalingDemo.Api.Infrastructure;
using ScalingDemo.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IBmiRepository, BmiRepository>();
builder.Services.AddScoped<IBmiService, BmiService>();
builder.Services.AddHostedService<DatabaseInitializer>();

var app = builder.Build();

// Registered before the exception handler so error responses carry X-Served-By too.
app.Use(ServedByHeader.Add);
app.UseExceptionHandler();

app.MapControllers();
app.MapLabEndpoints();

app.Run();
