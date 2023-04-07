using System.Data;
using Dapper;
using Npgsql;

namespace pds_back_end.Services;

public class DbService
{
    private readonly IDbConnection _db;

    public DbService(IConfiguration configuration)
    {
        _db = new NpgsqlConnection(configuration.GetConnectionString("LOCALHOST"));
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
