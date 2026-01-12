using ApiStudy.Models;
using ApiStudy.Models.Auth;
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

var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? throw new ArgumentNullException("Jwt Secret Key not found.");
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = builder.Configuration["JwtSettings:Audience"];

builder.Services.AddAuthentication(options =>
{
    // Define o esquema de autenticação padrão como "Bearer" (JWT)
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Mude para TRUE em produção!
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, // Deve validar o emissor (Issuer)
        ValidateAudience = true, // Deve validar o público (Audience)
        ValidateLifetime = true, // Deve validar a expiração do token
        ValidateIssuerSigningKey = true, // Deve validar a chave de assinatura

        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        // Converte a chave secreta em bytes para validação
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization(); // Habilita o serviço de Autorização

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "My API Study", Version = "v1" });

    var securitySchema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Cabeçalho de autorização JWT usando o esquema Bearer.",
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

var connectionString = builder.Configuration.GetConnectionString("DatabaseConnection");

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseSqlServer(connectionString)
);

// Registra o Repositório Genérico. 
// O Scoped garante que o Repositório e o Contexto durem o tempo de uma única requisição HTTP.
builder.Services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<UserRepository>(); // Registra sua classe de repositório

// Registra o serviço de gerenciamento (geralmente como Scoped)
builder.Services.AddScoped(typeof(CardManagementService<>), typeof(BaseRepository<>));

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<CardServices>();
builder.Services.AddScoped<MatchService>();

builder.Services.AddHttpContextAccessor();

// Refit Scryfall Client
builder.Services.AddRefitClient<IScryfallApi>().ConfigureHttpClient(client => {
    var scryfallApiUrl = builder.Configuration["ScryfallConfigs:ScryfallApiUrl"] 
        ?? throw new ArgumentNullException("ScryfallConfigs:ScryfallApiUrl não encontrado.");
    client.BaseAddress = new Uri(scryfallApiUrl);

    Console.WriteLine($"Scryfall API URL: {scryfallApiUrl}");
    Console.WriteLine($"Scryfall Accept Header: {builder.Configuration["ScryfallConfigs:ScryfallApiRequiredHeaders:Accept"]}");
    Console.WriteLine($"Scryfall User-Agent Header: {builder.Configuration["ScryfallConfigs:ScryfallApiRequiredHeaders:User-Agent"]}");

    client.DefaultRequestHeaders.Add("Accept", builder.Configuration["ScryfallConfigs:ScryfallApiRequiredHeaders:Accept"]);
    client.DefaultRequestHeaders.Add("User-Agent", builder.Configuration["ScryfallConfigs:ScryfallApiRequiredHeaders:User-Agent"]);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
