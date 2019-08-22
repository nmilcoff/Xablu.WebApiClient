using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Wrap;
using Xablu.WebApiClient.Client;
using Xablu.WebApiClient.Enums;

namespace Xablu.WebApiClient
{
    public class WebApiClient<T> : IWebApiClient<T>
    {
        private readonly RefitService<T> _refitService;

        public WebApiClient(string baseUrl = "", Func<DelegatingHandler> delegatingHandler = null)
        {
            _refitService = new RefitService<T>(baseUrl, delegatingHandler);
        }

        public async Task Call(Func<T, Task> operation, Priority priority, int retryCount, Func<Exception, bool> shouldRetry, int timeout)
        {
            var service = GetServiceByPriority(priority);

            var policy = GetWrappedPolicy(retryCount, shouldRetry, timeout);

            await policy.ExecuteAsync(() => operation.Invoke(service));
        }

        public async Task<TResult> Call<TResult>(Func<T, Task<TResult>> operation, Priority priority, int retryCount, Func<Exception, bool> shouldRetry, int timeout)
        {
            var service = GetServiceByPriority(priority);

            var policy = GetWrappedPolicy<TResult>(retryCount, shouldRetry, timeout);

            return await policy.ExecuteAsync(() => operation.Invoke(service));
        }

        public Task Call(Func<T, Task> operation, RequestOptions options)
        {
            return Call(operation, options.Priority, options.RetryCount, options.ShouldRetry, options.Timeout);
        }

        public Task<TResult> Call<TResult>(Func<T, Task<TResult>> operation, RequestOptions options)
        {
            return Call<TResult>(operation, options.Priority, options.RetryCount, options.ShouldRetry, options.Timeout);
        }

        private T GetServiceByPriority(Priority priority)
        {
            switch(priority)
            {
                case Priority.Background:
                    return _refitService.Background;
                case Priority.Speculative:
                    return _refitService.Speculative;
                case Priority.UserInitiated:
                default:
                    return _refitService.UserInitiated;
            }
        }

        private static AsyncPolicyWrap GetWrappedPolicy(int retryCount, Func<Exception, bool> shouldRetry, int timeout)
        {
            var retryPolicy = Policy.Handle<Exception>(e => shouldRetry?.Invoke(e) ?? true)
                                    .RetryAsync(retryCount);
            var timeoutPolicy = Policy.TimeoutAsync(timeout);

            return Policy.WrapAsync(retryPolicy, timeoutPolicy);
        }

        private static AsyncPolicyWrap<TResult> GetWrappedPolicy<TResult>(int retryCount, Func<Exception, bool> shouldRetry, int timeout)
        {
            var retryPolicy = Policy.Handle<Exception>(e => shouldRetry?.Invoke(e) ?? true)
                                    .RetryAsync(retryCount)
                                    .AsAsyncPolicy<TResult>();
            var timeoutPolicy = Policy.TimeoutAsync<TResult>(timeout);

            return Policy.WrapAsync<TResult>(retryPolicy, timeoutPolicy);
        }
    }
}