using ApiStudy.Models.Scryfall;
using ApiStudy.Repository;
using ApiStudy.Repository.Context;
using ApiStudy.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Refit;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── JWT ───────────────────────────────────────────────────────────────────────

var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? throw new ArgumentNullException("JwtSettings:Secret não encontrado.");
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = builder.Configuration["JwtSettings:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // alterar para true em produção com HTTPS
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization();

// ── Controllers + Swagger ─────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ApiStudy — MTG Scanner", Version = "v1" });

    var securitySchema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Cabeçalho JWT usando o esquema Bearer.",
        In = ParameterLocation.Header,
        Scheme = "bearer",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securitySchema);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securitySchema, Array.Empty<string>() }
    });
});

// ── Banco de Dados ────────────────────────────────────────────────────────────

var connectionString = builder.Configuration.GetConnectionString("DatabaseConnection");

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector())
);

// ── Repositórios ──────────────────────────────────────────────────────────────

builder.Services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<IScannerRepository, ScannerRepository>();

// ── Serviços de Aplicação (Scoped) ───────────────────────────────────────────

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<CardServices>();
builder.Services.AddScoped<MatchService>();

// ── Serviços do Scanner ───────────────────────────────────────────────────────
//
// Ciclo de vida:
//
//   Singleton  → VectorService, CardDetectionService, OcrService, DecisionEngine
//     - Sem estado mutável entre chamadas
//     - VectorService: carrega modelo ONNX (~45MB) uma única vez
//     - OcrService: inicializa TesseractEngine uma única vez
//     - Ambos são thread-safe por garantia dos seus respectivos runtimes
//
//   Scoped     → ScannerService
//     - Orquestrador da pipeline por requisição
//     - Usa IServiceScopeFactory para criar escopos temporários ao resolver
//       IScannerRepository (Scoped) durante seed e identificação
//
//   Hosted     → SeedBackgroundService
//     - Loop de background que verifica e semeia sets novos a cada 24h
//     - Registrado como AddHostedService — iniciado junto com a aplicação
//     - Usa IServiceScopeFactory para resolver serviços Scoped dentro do loop

builder.Services.AddSingleton<VectorService>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var path = Path.Combine(env.ContentRootPath, "Assets", "resnet18.onnx");

    if (!File.Exists(path))
        throw new FileNotFoundException(
            $"Modelo ONNX não encontrado: {path}. " +
            "Coloque resnet18.onnx em /Assets/ e marque como CopyToOutputDirectory no .csproj.");

    return new VectorService(path);
});

// CardDetectionService não tem parâmetros no construtor — AddSingleton direto
builder.Services.AddSingleton<CardDetectionService>();

builder.Services.AddSingleton<OcrService>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    // tessdata/ deve estar na raiz do projeto com eng.traineddata.
    // No .csproj:
    //   <Content Include="tessdata\**\*">
    //     <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    //   </Content>
    var path = Path.Combine(env.ContentRootPath, "tessdata");
    return new OcrService(path);
});

builder.Services.AddSingleton<DecisionEngine>();

builder.Services.AddScoped<ScannerService>();

// SeedBackgroundService: verifica sets novos a cada 24h automaticamente
builder.Services.AddHostedService<SeedBackgroundService>();

// ── HttpClient ────────────────────────────────────────────────────────────────

// Obrigatório para IHttpClientFactory funcionar em controllers e background services
builder.Services.AddHttpClient();

// ── Refit — Scryfall API ──────────────────────────────────────────────────────

builder.Services.AddRefitClient<IScryfallApi>().ConfigureHttpClient(client =>
{
    var scryfallUrl = builder.Configuration["ScryfallConfigs:ScryfallApiUrl"]
        ?? throw new ArgumentNullException("ScryfallConfigs:ScryfallApiUrl não encontrado.");

    client.BaseAddress = new Uri(scryfallUrl);
    client.DefaultRequestHeaders.Add("Accept",
        builder.Configuration["ScryfallConfigs:ScryfallApiRequiredHeaders:Accept"]);
    client.DefaultRequestHeaders.Add("User-Agent",
        builder.Configuration["ScryfallConfigs:ScryfallApiRequiredHeaders:User-Agent"]);
});

// ── CORS ──────────────────────────────────────────────────────────────────────

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// ── Misc ──────────────────────────────────────────────────────────────────────

builder.Services.AddHttpContextAccessor();

// ═══════════════════════════════════════════════════════════════════════════════

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();

app.Run();