using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Wrap;
using Xablu.WebApiClient.Attributes;
using Xablu.WebApiClient.Client;
using Xablu.WebApiClient.Enums;
using Xablu.WebApiClient.Logging;
using Xablu.WebApiClient.Services.GraphQL;

namespace Xablu.WebApiClient
{
    public class WebApiClient<T> : IWebApiClient<T>
    {
        private static readonly ILog Logger = LogProvider.For<WebApiClient<T>>();

        private readonly IRefitService<T> _refitService;
        private readonly IGraphQLService _graphQLService;

        public WebApiClient(
            IRefitService<T> refitService,
            IGraphQLService graphQLService)
        {
            _refitService = refitService;
            _graphQLService = graphQLService;
        }

        public Task Call(Func<T, Task> operation)
        {
            return Call(operation, GetDefaultOptions());
        }

        public Task<TResult> Call<TResult>(Func<T, Task<TResult>> operation)
        {
            return Call<TResult>(operation, GetDefaultOptions());
        }

        public async Task Call(Func<T, Task> operation, Priority priority, int retryCount, Func<Exception, bool> shouldRetry, int timeout)
        {
            var service = _refitService.GetByPriority(priority);

            var policy = GetWrappedPolicy(retryCount, shouldRetry, timeout);

            if (Logger.IsTraceEnabled())
            {
                Logger.Trace($"Operation running with parameters: Priority: {priority}, retryCount: {retryCount}, has should retry condition: {shouldRetry != null}, timeout: {timeout}");
            }

            await policy.ExecuteAsync(() => operation.Invoke(service));
        }

        public async Task<TResult> Call<TResult>(Func<T, Task<TResult>> operation, Priority priority, int retryCount, Func<Exception, bool> shouldRetry, int timeout)
        {
            var service = _refitService.GetByPriority(priority);

            var policy = GetWrappedPolicy<TResult>(retryCount, shouldRetry, timeout);

            if (Logger.IsTraceEnabled())
            {
                Logger.Trace($"Operation running with parameters: Priority: {priority}, retryCount: {retryCount}, has should retry condition: {shouldRetry != null}, timeout: {timeout}");
            }

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

        public async Task<TModel> SendMutation<TModel>(Request<TModel> request, int retryCount, Func<Exception, bool> shouldRetry, int timeout, CancellationToken cancellationToken = default)
            where TModel : class, new()
        {
            var defaultOptions = GetDefaultOptions();

            var service = _graphQLService.GetByPriority(defaultOptions.Priority);

            var policy = GetWrappedPolicy(retryCount, shouldRetry, timeout);

            var result = policy.ExecuteAsync(async () =>
            {
                var result = await service.SendMutationAsync(request, cancellationToken);
                return result;
            })?.Result;

            //maybe throw null exception if result is null as well?

            var resultData = result?.Data as JObject;
            if (resultData == null)
            {
                throw new InvalidCastException("Result is not a valid Json");
            }
            var model = JsonConvert.DeserializeObject<TModel>(resultData.ToString());
            return model;
        }

        public async Task<TModel> SendQueryAsync<TModel>(Request<TModel> request, int retryCount, Func<Exception, bool> shouldRetry, int timeout, CancellationToken cancellationToken = default)
            where TModel : class, new()
        {
            var defaultOptions = GetDefaultOptions();

            var service = _graphQLService.GetByPriority(defaultOptions.Priority);

            service.EndPoint = new Uri(_graphQLService.BaseUrl + GetGraphQLEndpoint());

            var policy = GetWrappedPolicy(retryCount, shouldRetry, timeout);

            var result = policy.ExecuteAsync(async () =>
            {
                var result = await service.SendMutationAsync(request, cancellationToken);
                return result;
            })?.Result;

            var resultData = result?.Data as JObject;
            if (resultData == null)
            {
                throw new InvalidCastException("Result is not a valid Json");
            }
            var model = JsonConvert.DeserializeObject<TModel>(resultData.ToString());
            return model;
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

        private static string GetGraphQLEndpoint()
        {
            var attributes = typeof(T).GetCustomAttributes(false);
            if (attributes.Any(a => a.GetType() == typeof(GraphQLEndpointAttribute)))
            {
                var endpointAttribute = (GraphQLEndpointAttribute)attributes.First(a => a.GetType() == typeof(GraphQLEndpointAttribute));
                return endpointAttribute.Path;
            }
            throw new RequestException("No endpoint found");
        }

        private RequestOptions GetDefaultOptions()
        {
            return RequestOptions.DefaultRequestOptions
                ?? new RequestOptions
                {
                    Priority = RequestOptions.DefaultPriority,
                    RetryCount = RequestOptions.DefaultRetryCount,
                    Timeout = RequestOptions.DefaultTimeout,
                    ShouldRetry = RequestOptions.DefaultShouldRetry
                };
        }
    }
}
