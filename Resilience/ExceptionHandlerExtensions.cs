using Polly;
using Polly.Retry;

namespace PlayOffsApi.Resilience;

public static class ExceptionHandlerExtensions
{
    public static RetryPolicy ConfigureRetry(this PolicyBuilder policyBuilder)
    {
        return policyBuilder.WaitAndRetry(
            3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) 
        );
    }
}