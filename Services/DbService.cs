using System.Data;
using Dapper;
using Npgsql;

namespace pds_back_end.Services;

public class DbService
{
    private readonly IDbConnection _db;

    public DbService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        string connectionString;

        if (environment.IsProduction())
        {
            var PGUSER = Environment.GetEnvironmentVariable("PGUSER");
            var PGPASSWORD = Environment.GetEnvironmentVariable("PGPASSWORD");
            var PGHOST = Environment.GetEnvironmentVariable("PGHOST");
            var PGPORT = Environment.GetEnvironmentVariable("PGPORT");
            var PGDATABASE = Environment.GetEnvironmentVariable("PGDATABASE");

            connectionString = $"User ID={PGUSER};Password={PGPASSWORD};Host={PGHOST};Port={PGPORT};Database={PGDATABASE};";
        }
        else connectionString = configuration.GetConnectionString("LOCALHOST");

        _db = new NpgsqlConnection(connectionString);
    }

     public async Task<T> GetAsync<T>(string command, object parms)
    {
        try
        {
            T result;

            result = (await _db.QueryAsync<T>(command, parms).ConfigureAwait(false)).FirstOrDefault();

            return result;  
        }
        catch (Exception)
        {
            throw new ApplicationException("Houve um erro de conexão, tente novamente mais tarde.");
        }
    }

    public async Task<List<T>> GetAll<T>(string command, object parms)
    {
        try
        {
            List<T> result = new List<T>();

            result = (await _db.QueryAsync<T>(command, parms)).ToList();

            return result; 
        }
        catch (Exception)
        {
            throw new ApplicationException("Houve um erro de conexão, tente novamente mais tarde.");
        }
        
    }

    public async Task<int> EditData(string command, object parms)
    {
        try
        {
            int result;

            result = await _db.ExecuteAsync(command, parms);

            return result;
        }
        catch (Exception)
        {
            throw new ApplicationException("Houve um erro de conexão, tente novamente mais tarde.");
        }
       
    }

}