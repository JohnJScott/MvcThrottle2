using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcThrottle2
{
	/// <summary>
	/// Stores the client IP, client key, endpoint and User-Agent that identify a request for throttling.
	/// </summary>
	[Serializable]
	public class RequestIdentity
	{
		/// <summary>
		/// Gets or sets the client IP address.
		/// </summary>
		public string ClientIp { get; set; }

		/// <summary>
		/// Gets or sets the client key. By default "auth" for authenticated requests and "anon" otherwise.
		/// </summary>
		public string ClientKey { get; set; }

		/// <summary>
		/// Gets or sets the requested endpoint, in the form selected by <see cref="ThrottlePolicy.EndpointType"/>.
		/// </summary>
		public string Endpoint { get; set; }

		/// <summary>
		/// Gets or sets the client User-Agent string.
		/// </summary>
		public string UserAgent { get; set; }

		/// <summary>
		/// Returns the identity as "ClientIp_ClientKey_Endpoint_UserAgent".
		/// </summary>
		/// <returns>A string representation of the identity.</returns>
		public override string ToString()
		{
			return $"{ClientIp}_{ClientKey}_{Endpoint}_{UserAgent}";
		}
	}
}