using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp.Services
{
    class SampleHttpClientHandler : DelegatingHandler
    {
        readonly Func<HttpRequestMessage, string> getToken;

        public SampleHttpClientHandler(Func<HttpRequestMessage, string> getToken)
        {
            this.getToken = getToken ?? throw new ArgumentNullException(nameof(getToken));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // See if the request has an authorize header
            var auth = request.Headers.Authorization;
            if (auth != null)
            {
                var token = getToken(request);
                request.Headers.Authorization = new AuthenticationHeaderValue(auth.Scheme, token);
            }

            return await SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
