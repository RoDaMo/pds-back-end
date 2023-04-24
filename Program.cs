using Microsoft.AspNetCore.Localization;
using PlayOffsApi.Services;
using ServiceStack;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddScoped<DbService>();
builder.Services.AddScoped<ChampionshipService>();
builder.Services.AddSingleton<RedisService>();
builder.Services.AddSingleton<ElasticService>();
builder.Services.AddScoped<SportService>();
builder.Services.AddScoped<TeamService>();

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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

// string connectionString; 
// connectionString = builder.Configuration.GetConnectionString("LOCALHOST");

// builder.Services.AddDbContext<AstroContext>(options =>
//     options.UseNpgsql(connectionString));

app.UseHttpsRedirection();

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