using Docfy.Hubs;
using Docfy.Services;

var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços
builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    // Aumentar limite para 50MB (mas usaremos streaming para arquivos grandes)
    options.MaximumReceiveMessageSize = 1024 * 1024 * 50;
    options.StreamBufferCapacity = 50; // Buffer para streaming
})
.AddJsonProtocol(options =>
{
    // Permitir serialização de grandes objetos
    options.PayloadSerializerOptions.PropertyNamingPolicy = null;
});

// Registrar serviços da aplicação
builder.Services.AddScoped<IPdfExtractionService, PdfExtractionService>();
builder.Services.AddScoped<ILlmVisionService, LlmVisionService>();
builder.Services.AddScoped<MarkdownBuilderService>();

// HttpClient para chamadas à LLM
builder.Services.AddHttpClient("LlmClient", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

// CORS
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

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseRouting();

app.MapControllers();
app.MapHub<ConversionHub>("/conversionHub");

app.Run();
