using System.Collections.Generic;
using System.Net;
using System.Web;

namespace MvcThrottle2
{
	/// <summary>
	/// Resolves client IP addresses and matches them against IP rules.
	/// Implement this interface to customise how the client IP is obtained (e.g. behind a reverse proxy).
	/// </summary>
	public interface IIpAddressParser
	{
		/// <summary>
		/// Determines whether the client IP matches any of the given IP rules.
		/// </summary>
		/// <param name="ipRules">IP rules: single addresses, CIDR ranges, bit mask ranges or begin-end ranges.</param>
		/// <param name="clientIp">The client IP address, optionally including a port.</param>
		/// <returns><c>true</c> if the IP matches at least one rule; otherwise <c>false</c>.</returns>
		bool ContainsIp( List<string> ipRules, string clientIp );

		/// <summary>
		/// Determines whether the client IP matches any of the given IP rules and returns the first matching rule.
		/// </summary>
		/// <param name="ipRules">IP rules: single addresses, CIDR ranges, bit mask ranges or begin-end ranges.</param>
		/// <param name="clientIp">The client IP address, optionally including a port.</param>
		/// <param name="rule">When this method returns, the first matching rule, or <c>null</c> if none matched.</param>
		/// <returns><c>true</c> if the IP matches at least one rule; otherwise <c>false</c>.</returns>
		bool ContainsIp( List<string> ipRules, string clientIp, out string rule );

		/// <summary>
		/// Gets the IP address of the client that made the request.
		/// </summary>
		/// <param name="request">The current HTTP request.</param>
		/// <returns>The client IP address.</returns>
		string GetClientIp( HttpRequestBase request );

		/// <summary>
		/// Determines whether the given address is in a private, link-local or unique local range.
		/// </summary>
		/// <param name="ipAddress">The IP address to test, optionally including a port.</param>
		/// <returns><c>true</c> if the address is private; otherwise <c>false</c>.</returns>
		bool IsPrivateIpAddress( string ipAddress );

		/// <summary>
		/// Parses an IP address string, stripping any port suffix.
		/// </summary>
		/// <param name="ipAddress">The IP address string, optionally including a port.</param>
		/// <returns>The parsed <see cref="IPAddress"/>.</returns>
		IPAddress ParseIp( string ipAddress );
	}
}