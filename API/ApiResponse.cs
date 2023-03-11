namespace pds_back_end.API;

public class ApiResponse<T>
{
    public ApiResponse()
    {
        Message = string.Empty;
        Succeed = false;
    }

    public ApiResponse(string message, bool succeed, T results)
    {
        Message = message;
        Succeed = succeed;
        Results = results;
    }


    public string Message { get; set; }
    public bool Succeed { get; set; }
    public T? Results { get; set; }
}

