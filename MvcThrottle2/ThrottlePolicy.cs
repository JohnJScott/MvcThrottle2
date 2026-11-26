using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcThrottle2
{
	/// <summary>
	/// Rate limits policy
	/// </summary>
	[Serializable]
	public class ThrottlePolicy
	{
		/// <summary>
		/// Enables IP throttling
		/// </summary>
		public bool IpThrottling { get; set; }

		/// <summary>
		/// Gets or sets IP addresses or ranges that are never throttled. Applies when <see cref="IpThrottling"/> is enabled.
		/// </summary>
		public List<string> IpWhitelist { get; set; }

		/// <summary>
		/// Gets or sets custom limits keyed by IP address or range. These are the most specific rules and override all others.
		/// </summary>
		public Dictionary<string, RateLimits> IpRules { get; set; }

		/// <summary>
		/// Enables User-Agent throttling
		/// </summary>
		public bool UserAgentThrottling { get; set; }

		/// <summary>
		/// Gets or sets User-Agent substrings (case-insensitive) that are never throttled. Applies when <see cref="UserAgentThrottling"/> is enabled.
		/// </summary>
		public List<string> UserAgentWhitelist { get; set; }

		/// <summary>
		/// Gets or sets custom limits keyed by User-Agent substring (case-insensitive). The lowest matching limit is used.
		/// </summary>
		public Dictionary<string, RateLimits> UserAgentRules { get; set; }

		/// <summary>
		/// Enables Client Key throttling
		/// </summary>
		public bool ClientThrottling { get; set; }

		/// <summary>
		/// Gets or sets client keys that are never throttled. Applies when <see cref="ClientThrottling"/> is enabled.
		/// </summary>
		public List<string> ClientWhitelist { get; set; }

		/// <summary>
		/// Gets or sets custom limits keyed by client key. These override endpoint limits.
		/// </summary>
		public Dictionary<string, RateLimits> ClientRules { get; set; }

		/// <summary>
		/// Enables routes throttling
		/// </summary>
		public bool EndpointThrottling { get; set; }

		/// <summary>
		/// Gets or sets endpoint substrings (case-insensitive) that are never throttled. Applies when <see cref="EndpointThrottling"/> is enabled.
		/// </summary>
		public List<string> EndpointWhitelist { get; set; }

		/// <summary>
		/// Gets or sets custom limits keyed by endpoint substring (case-insensitive). The lowest matching limit is used.
		/// </summary>
		public Dictionary<string, RateLimits> EndpointRules { get; set; }

		/// <summary>
		/// Gets or sets how the endpoint of a request is identified. The default is <see cref="EndpointThrottlingType.AbsolutePath"/>.
		/// </summary>
		public EndpointThrottlingType EndpointType { get; set; }

		/// <summary>
		/// All requests including the rejected ones will stack in this order: week, day, hour, min, sec
		/// </summary>
		public bool StackBlockedRequests { get; set; }

		internal Dictionary<RateLimitPeriod, long> Rates { get; set; }

		/// <summary>
		/// Configure default request limits per second, minute, hour, day or week
		/// </summary>
		/// <param name="perSecond">Maximum requests per second, or <c>null</c> for no limit.</param>
		/// <param name="perMinute">Maximum requests per minute, or <c>null</c> for no limit.</param>
		/// <param name="perHour">Maximum requests per hour, or <c>null</c> for no limit.</param>
		/// <param name="perDay">Maximum requests per day, or <c>null</c> for no limit.</param>
		/// <param name="perWeek">Maximum requests per week, or <c>null</c> for no limit.</param>
		public ThrottlePolicy( long? perSecond, long? perMinute = null, long? perHour = null, long? perDay = null, long? perWeek = null )
		{
			EndpointType = EndpointThrottlingType.AbsolutePath;
			Rates = new Dictionary<RateLimitPeriod, long>();
			if( perSecond.HasValue )
			{
				Rates.Add( RateLimitPeriod.Second, perSecond.Value );
			}

			if( perMinute.HasValue )
			{
				Rates.Add( RateLimitPeriod.Minute, perMinute.Value );
			}

			if( perHour.HasValue )
			{
				Rates.Add( RateLimitPeriod.Hour, perHour.Value );
			}

			if( perDay.HasValue )
			{
				Rates.Add( RateLimitPeriod.Day, perDay.Value );
			}

			if( perWeek.HasValue )
			{
				Rates.Add( RateLimitPeriod.Week, perWeek.Value );
			}
		}

	}

	/// <summary>
	/// Determines which part of a request identifies its endpoint.
	/// </summary>
	public enum EndpointThrottlingType
	{
		/// <summary>The URL absolute path, e.g. "/home/about".</summary>
		AbsolutePath = 1,
		/// <summary>The URL path and query string, e.g. "/home/about?id=1".</summary>
		PathAndQuery,
		/// <summary>The route controller and action, e.g. "home/about".</summary>
		ControllerAndAction,
		/// <summary>The route controller only, e.g. "home".</summary>
		Controller
	}
}