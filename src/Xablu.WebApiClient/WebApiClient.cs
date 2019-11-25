using System;
using System.Linq;
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


        public Task Call(Func<T, Task> operation, RequestOptions options)
        {
            return Call(operation, options.Priority, options.RetryCount, options.ShouldRetry, options.Timeout);
        }

        public Task<TResult> Call<TResult>(Func<T, Task<TResult>> operation, RequestOptions options)
        {
            return Call<TResult>(operation, options.Priority, options.RetryCount, options.ShouldRetry, options.Timeout);
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

        public Task SendQueryAsync<TModel>(Request<TModel> request, CancellationToken cancellationToken = default)
           where TModel : class, new()
        {
            return SendQueryAsync(request, GetDefaultOptions(), cancellationToken);
        }

        public Task<TModel> SendQueryAsync<TModel>(Request<TModel> request, RequestOptions options, CancellationToken cancellationToken = default)
            where TModel : class, new()
        {
            return SendQueryAsync(request, options.Priority, options.RetryCount, options.ShouldRetry, options.Timeout, cancellationToken);
        }

        public async Task<TModel> SendQueryAsync<TModel>(Request<TModel> request, Priority priority, int retryCount, Func<Exception, bool> shouldRetry, int timeout, CancellationToken cancellationToken = default)
            where TModel : class, new()
        {
            var service = _graphQLService.GetByPriority(priority);
            service.EndPoint = new Uri(_graphQLService.BaseUrl + GetGraphQLEndpoint());

            var policy = GetWrappedPolicy(retryCount, shouldRetry, timeout);
            var result = await policy.ExecuteAsync(async () => await service.SendQueryAsync(request, cancellationToken));

            if (result?.Errors?.Length > 0)
            {
                string error = "";
                int numberOfError = 1;
                foreach (var item in result.Errors)
                {
                    error += $"{numberOfError}. {item}";
                    numberOfError++;
                }
                var message = $"The following has occured: {error}";
                throw new RequestException(message);
            }

            var resultData = result?.Data as JObject;
            if (resultData == null)
            {
                throw new InvalidCastException("Result is not a valid Json");
            }
            var model = JsonConvert.DeserializeObject<TModel>(resultData.ToString());
            return model;
        }

        public Task<TModel> SendMutationAsync<TModel>(Request<TModel> request, CancellationToken cancellationToken = default)
            where TModel : class, new()
        {
            return SendMutationAsync(request, GetDefaultOptions(), cancellationToken);
        }

        public Task<TModel> SendMutationAsync<TModel>(Request<TModel> request, RequestOptions options, CancellationToken cancellationToken = default)
            where TModel : class, new()
        {
            return SendMutationAsync(request, options.Priority, options.RetryCount, options.ShouldRetry, options.Timeout, cancellationToken);
        }

        public async Task<TModel> SendMutationAsync<TModel>(Request<TModel> request, Priority priority, int retryCount, Func<Exception, bool> shouldRetry, int timeout, CancellationToken cancellationToken = default)
            where TModel : class, new()
        {
            var service = _graphQLService.GetByPriority(priority);
            service.EndPoint = new Uri(_graphQLService.BaseUrl + GetGraphQLEndpoint());
            var policy = GetWrappedPolicy(retryCount, shouldRetry, timeout);

            var result = await policy.ExecuteAsync(async () => await service.SendMutationAsync(request, cancellationToken));

            if (result?.Errors?.Length > 0)
            {
                string error = "";
                int numberOfError = 1;
                foreach (var item in result.Errors)
                {
                    error += $"{numberOfError}. {item}";
                    numberOfError++;
                }
                var message = $"The following has occured: {error}";
                throw new RequestException(message);
            }

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
