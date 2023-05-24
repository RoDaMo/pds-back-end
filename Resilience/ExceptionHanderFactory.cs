using Polly;

namespace PlayOffsApi.Resilience;

public static class ExceptionHanderFactory
{
    public static PolicyBuilder BuildForHttpClient()
    {
        return Policy.Handle<Exception>();
    }
}