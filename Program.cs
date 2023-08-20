using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PlayOffsApi.HostedService;
using PlayOffsApi.Middleware;
using PlayOffsApi.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var ISSUER = config["JwtSettings:Issuer"];
var AUDIENCE = config["JwtSettings:Audience"];
var KEY = config["JwtSettings:Key"];
// var CRYPT_KEY = config["CryptKey"].ToUtf8Bytes();

if (builder.Environment.IsProduction())
{
	ISSUER = Environment.GetEnvironmentVariable("AUTH_ISSUER");
	AUDIENCE = Environment.GetEnvironmentVariable("AUTH_AUDIENCE");
	KEY = Environment.GetEnvironmentVariable("AUTH_KEY");
	// CRYPT_KEY = Environment.GetEnvironmentVariable("CRYPT_KEY").ToUtf8Bytes();
}

var audienceProduction = AUDIENCE.Split(',');
var audienceLocal = new[] { "https://localhost:5173", "https://127.0.0.1:5173", "http://localhost" };
var audience = audienceProduction.Concat(audienceLocal);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(con =>
{
	con.TokenValidationParameters = new()
	{
		ValidIssuer = ISSUER,
		ValidAudiences = audience,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY)),
		ValidateIssuer = true,
		ValidateAudience = true,
		ValidateLifetime = true,
		ValidateIssuerSigningKey = true
	};

	con.Events = new JwtBearerEvents
	{
		OnMessageReceived = context =>
		{
			context.Token = context.Request.Cookies["playoffs-token"];
			return Task.CompletedTask;
		}
	};
});

builder.Services.AddAuthorization();

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddHostedService<BackgroundJobs>();
builder.Services.AddSingleton<IBackgroundJobsService, BackgroundJobs>();
builder.Services.AddSingleton<RedisService>();
builder.Services.AddScoped<DbService>();
builder.Services.AddScoped<ChampionshipService>();
builder.Services.AddSingleton<ElasticService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<PlayerTempProfileService>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<BracketingService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<GoalService>();
builder.Services.AddScoped<PenaltyService>();
builder.Services.AddScoped<ImageService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ErrorLogService>();
builder.Services.AddScoped<ChampionshipActivityLogService>();
builder.Services.AddScoped<CaptchaService>();
builder.Services.AddScoped<OrganizerService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(sp => new AuthService(KEY, ISSUER, AUDIENCE, sp.GetRequiredService<DbService>(), sp.GetRequiredService<ElasticService>()));


builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
	var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("pt-BR") };

	options.DefaultRequestCulture = new("pt-BR");
	options.SupportedCultures = supportedCultures;
	options.SupportedUICultures = supportedCultures;
	options.AddInitialRequestCultureProvider(new CustomRequestCultureProvider(context =>
	{
		var acceptLanguageHeader = context.Request.Headers["Accept-Language"].ToString();
		var culture = GetTrueLanguage(acceptLanguageHeader);
		return Task.FromResult(new ProviderCultureResult(culture));
	}));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Version = "v1",
		Title = "PlayOffs API",
		Description = "Uma API ASP.NET Core de administração esportiva",
		TermsOfService = new Uri("https://www.playoffs.app.br/pages/termos-de-uso.html"),
		Contact = new OpenApiContact
		{
			Name = "Email",
			Email = "equiperodamo@gmail.com"
		},
		License = new OpenApiLicense
		{
			Name = "Licença MIT",
			Url = new Uri("https://github.com/RoDaMo/pds-back-end/blob/main/LICENSE")
		}
	});
	c.UseAllOfToExtendReferenceSchemas();
	c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Description = "JWT Authorization header. É necessário fazer uma requsição POST /auth para obter o token de autenticação.",
		Name = "Authorization",
		In = ParameterLocation.Header,
		Type = SecuritySchemeType.Http,
		Scheme = "Bearer"
	});
	var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});
builder.Services.AddCors(options =>
{
	options.AddPolicy("cors", policy =>
	{
		policy.WithOrigins("https://localhost:5173", "https://127.0.0.1:5173", AUDIENCE);
		policy.AllowAnyHeader();
		policy.AllowAnyMethod();
		policy.AllowCredentials();
	});
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
	c.SwaggerEndpoint("/swagger/v1/swagger.json", "PlayOffs API V1");
});

if (app.Environment.IsDevelopment())
{
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("cors");
app.UseAuthentication();
app.UseAuthorization();

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseMiddleware<ErrorMiddleware>();

app.MapControllers();

app.Run();

static string GetTrueLanguage(string falseLanguage) => falseLanguage switch
{
	"ptbr" => "pt-BR",
	"en" => "en",
	_ => "pt-BR",
};