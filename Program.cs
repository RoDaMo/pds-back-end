using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.Services;
using ServiceStack;
using System.Globalization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var ISSUER = config["JwtSettings:Issuer"];
var AUDIENCE = config["JwtSettings:Audience"];
var KEY = config["JwtSettings:Key"];
var CRYPT_KEY = config["CryptKey"].ToUtf8Bytes();

if (builder.Environment.IsProduction())
{
	ISSUER = Environment.GetEnvironmentVariable("AUTH_ISSUER");
	AUDIENCE = Environment.GetEnvironmentVariable("AUTH_AUDIENCE");
	KEY = Environment.GetEnvironmentVariable("AUTH_KEY");
	CRYPT_KEY = Environment.GetEnvironmentVariable("CRYPT_KEY").ToUtf8Bytes();
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(con =>
{
	con.TokenValidationParameters = new TokenValidationParameters
	{
		ValidIssuer = ISSUER,
		ValidAudience = AUDIENCE,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY)),
		ValidateIssuer = true,
		ValidateAudience = true,
		ValidateLifetime = true,
		ValidateIssuerSigningKey = true
	};
});

builder.Services.AddAuthorization();

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddSingleton<DbService>();
builder.Services.AddScoped<ChampionshipService>();
builder.Services.AddSingleton<RedisService>();
builder.Services.AddSingleton<ElasticService>();
builder.Services.AddScoped<SportService>();
builder.Services.AddSingleton(sp => new AuthService(KEY, ISSUER, AUDIENCE, sp.GetRequiredService<DbService>(), CRYPT_KEY));

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
	var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("pt-BR") };

	options.DefaultRequestCulture = new RequestCulture("pt-BR");
	options.SupportedCultures = supportedCultures;
	options.SupportedUICultures = supportedCultures;
	options.AddInitialRequestCultureProvider(new CustomRequestCultureProvider(async context =>
	{
		var acceptLanguageHeader = context.Request.Headers["Accept-Language"].ToString();
		var culture = GetTrueLanguage(acceptLanguageHeader);
		return new ProviderCultureResult(culture);
	}));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		policy.AllowAnyOrigin();
		policy.AllowAnyHeader();
		policy.AllowAnyMethod();
	});
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseRequestLocalization(app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value);

app.MapControllers();

app.UseCors();
app.Run();

static string GetTrueLanguage(string falseLanguage) => falseLanguage switch
{
	"ptbr" => "pt-BR",
	"en" => "en",
	_ => "pt-BR",
};