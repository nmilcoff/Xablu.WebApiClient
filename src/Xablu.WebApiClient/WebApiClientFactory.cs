using System;
using System.Collections.Generic;
using System.Net.Http;
using Xablu.WebApiClient.Services.GraphQL;
using Xablu.WebApiClient.Services.Rest;

namespace Xablu.WebApiClient
{
    public static class WebApiClientFactory
    {
        public static IWebApiClient<T> Get<T>(
            string baseUrl,
            bool autoRedirectRequests = true,
            Func<DelegatingHandler> delegatingHandler = default,
            IDictionary<string, string> defaultHeaders = default)
            where T : class
        {
            var refitService = new RefitService<T>(baseUrl, autoRedirectRequests, delegatingHandler, defaultHeaders);
            var graphQLService = new GraphQLService(baseUrl, autoRedirectRequests, delegatingHandler, defaultHeaders);

            return new WebApiClient<T>(refitService, graphQLService);
        }
    }
}
