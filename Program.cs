using PlayOffsApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<DbService>();
builder.Services.AddScoped<ChampionshipService>();
builder.Services.AddSingleton<RedisService>();
builder.Services.AddSingleton<ElasticService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
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

app.MapControllers();
app.UseCors();
app.Run();
