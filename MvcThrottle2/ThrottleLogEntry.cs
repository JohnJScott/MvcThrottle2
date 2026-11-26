using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MvcThrottle2
{
	/// <summary>
	/// Details of a request that was blocked, passed to <see cref="IThrottleLogger.Log"/>.
	/// </summary>
	[Serializable]
	public class ThrottleLogEntry
	{
		/// <summary>
		/// Gets or sets the throttle counter key of the blocked request.
		/// </summary>
		public string RequestId { get; set; }

		/// <summary>
		/// Gets or sets the client IP address.
		/// </summary>
		public string ClientIp { get; set; }

		/// <summary>
		/// Gets or sets the client key.
		/// </summary>
		public string ClientKey { get; set; }

		/// <summary>
		/// Gets or sets the requested endpoint.
		/// </summary>
		public string Endpoint { get; set; }

		/// <summary>
		/// Gets or sets the client User-Agent string.
		/// </summary>
		public string UserAgent { get; set; }

		/// <summary>
		/// Gets or sets the number of requests made in the current period, including the blocked one.
		/// </summary>
		public long TotalRequests { get; set; }

		/// <summary>
		/// Gets or sets the UTC start time of the current period.
		/// </summary>
		public DateTime StartPeriod { get; set; }

		/// <summary>
		/// Gets or sets the limit that was exceeded.
		/// </summary>
		public long RateLimit { get; set; }

		/// <summary>
		/// Gets or sets the name of the <see cref="MvcThrottle2.RateLimitPeriod"/> whose limit was exceeded.
		/// </summary>
		public string RateLimitPeriod { get; set; }

		/// <summary>
		/// Gets or sets the UTC time the entry was created.
		/// </summary>
		public DateTime LogDate { get; set; }

		/// <summary>
		/// Gets or sets the blocked HTTP request.
		/// </summary>
		public HttpRequestBase Request { get; set; }
	}
}