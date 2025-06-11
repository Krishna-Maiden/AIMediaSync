using AiMediaSync.Core.Extensions;
using AiMediaSync.API.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/aimediasync-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "AiMediaSync API", 
        Version = "v1",
        Description = "AI-Powered Lip Synchronization API for Enterprise Content Localization",
        Contact = new() { Name = "AiMediaSync Team", Email = "support@aimediasync.com" }
    });
    
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add AiMediaSync services
builder.Services.AddAiMediaSyncServices();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add authentication (if needed)
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options => { ... });

// Add background job processing
// builder.Services.AddHangfire(configuration => 
//     configuration.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AiMediaSync API v1");
        c.RoutePrefix = string.Empty; // Swagger at root
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

// Custom middleware
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<LoggingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0",
    Environment = app.Environment.EnvironmentName
})).WithTags("Health");

// System info endpoint
app.MapGet("/info", () => Results.Ok(new {
    Application = "AiMediaSync API",
    Version = "1.0.0",
    Description = "AI-Powered Lip Synchronization Service",
    Features = new[] { 
        "Real-time lip synchronization",
        "Multi-language support", 
        "GPU acceleration",
        "Cloud-ready architecture"
    }
})).WithTags("Info");

try
{
    Log.Information("Starting AiMediaSync API");
    Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}