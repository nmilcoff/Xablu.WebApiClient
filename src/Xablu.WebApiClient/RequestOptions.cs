using System;
using Xablu.WebApiClient.Enums;

namespace Xablu.WebApiClient
{
    public class RequestOptions
    {
        /// <summary>
        /// Default RequestOptions object. If defined, it will be used when no options are specified
        /// </summary>
        public static RequestOptions DefaultRequestOptions { get; set; }

        /// <summary>
        /// Default value for retry intents
        /// </summary>
        public static int DefaultRetryCount { get; set; } = 1;

        /// <summary>
        /// Default timeout for requests
        /// </summary>
        public static int DefaultTimeout { get; set; } = 60;

        /// <summary>
        /// Default priority for requests. Matches fusillade policy
        /// </summary>
        public static Priority DefaultPriority { get; set; } = Priority.UserInitiated;

        /// <summary>
        /// Default should retry condition. Default value is null
        /// </summary>
        public static Func<Exception, bool> DefaultShouldRetry { get; set; }

        public int RetryCount { get; set; } = DefaultRetryCount;

        public int Timeout { get; set; } = DefaultTimeout;

        public Priority Priority { get; set; } = DefaultPriority;

        public Func<Exception, bool> ShouldRetry { get; set; } = DefaultShouldRetry;
    }
}
