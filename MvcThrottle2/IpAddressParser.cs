using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MvcThrottle2
{
	/// <summary>
	/// Default <see cref="IIpAddressParser"/> implementation. Reads the client IP from
	/// <c>X-Forwarded-For</c> when present, otherwise from <see cref="HttpRequestBase.UserHostAddress"/>.
	/// </summary>
	public class IpAddressParser : IIpAddressParser
	{
		/// <summary>
		/// Determines whether the client IP matches any of the given IP rules.
		/// </summary>
		/// <param name="ipRules">IP rules: single addresses, CIDR ranges, bit mask ranges or begin-end ranges.</param>
		/// <param name="clientIp">The client IP address, optionally including a port.</param>
		/// <returns><c>true</c> if the IP matches at least one rule; otherwise <c>false</c>.</returns>
		public bool ContainsIp( List<string> ipRules, string clientIp )
		{
			IPAddress ip = ParseIp( clientIp );
			if( ipRules != null && ipRules.Any() )
			{
				foreach( string rule in ipRules )
				{
					IPAddressRange range = new IPAddressRange( rule );
					if( range.Contains( ip ) )
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Determines whether the client IP matches any of the given IP rules and returns the first matching rule.
		/// </summary>
		/// <param name="ipRules">IP rules: single addresses, CIDR ranges, bit mask ranges or begin-end ranges.</param>
		/// <param name="clientIp">The client IP address, optionally including a port.</param>
		/// <param name="rule">When this method returns, the first matching rule, or <c>null</c> if none matched.</param>
		/// <returns><c>true</c> if the IP matches at least one rule; otherwise <c>false</c>.</returns>
		public bool ContainsIp( List<string> ipRules, string clientIp, out string rule )
		{
			rule = null;
			IPAddress ip = ParseIp( clientIp );
			if( ipRules != null && ipRules.Any() )
			{
				foreach( string r in ipRules )
				{
					IPAddressRange range = new IPAddressRange( r );
					if( range.Contains( ip ) )
					{
						rule = r;
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Gets the IP address of the client that made the request.
		/// </summary>
		/// <remarks>
		/// If the <c>X-Forwarded-For</c> header is present, the last public IP address in it is returned;
		/// if it contains no public addresses, <see cref="HttpRequestBase.UserHostAddress"/> is returned.
		/// Override this method to change how proxied addresses are resolved.
		/// </remarks>
		/// <param name="request">The current HTTP request.</param>
		/// <returns>The client IP address.</returns>
		public virtual string GetClientIp( HttpRequestBase request )
		{
			string ipAddress = request.UserHostAddress;

			// get client IP from reverse proxy
			string xForwardedFor = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
			if( !string.IsNullOrEmpty( xForwardedFor ) )
			{
				// Search for public IP addresses 
				List<string> publicForwardingIps = xForwardedFor.Split( ',' ).Where( ip => !IsPrivateIpAddress( ip ) ).ToList();

				// If we found any public IP, return the last one, otherwise return the user host address
				return publicForwardingIps.Any() ? publicForwardingIps.Last().Trim() : ipAddress;
			}

			return ipAddress;
		}

		/// <summary>
		/// Parses an IP address string, stripping any port suffix (fix for Azure LB).
		/// </summary>
		/// <param name="ipAddress">The IP address string, e.g. "1.2.3.4", "1.2.3.4:80" or "[::1]:80".</param>
		/// <returns>The parsed <see cref="IPAddress"/>.</returns>
		/// <exception cref="FormatException">The string is not a valid IP address.</exception>
		public IPAddress ParseIp( string ipAddress )
		{
			ipAddress = ipAddress.Trim();
			int portDelimiterPos = ipAddress.LastIndexOf( ":", StringComparison.InvariantCultureIgnoreCase );
			bool ipv6WithPortStart = ipAddress.StartsWith( "[" );
			int ipv6End = ipAddress.IndexOf( "]" );
			if( ( portDelimiterPos != -1
				&& portDelimiterPos == ipAddress.IndexOf( ":", StringComparison.InvariantCultureIgnoreCase ) )
				|| ( ipv6WithPortStart && ipv6End != -1 && ipv6End < portDelimiterPos ) )
			{
				ipAddress = ipAddress.Substring( 0, portDelimiterPos );
			}

			return IPAddress.Parse( ipAddress );
		}

		/// <summary>
		/// Determines whether the given address is private.
		/// </summary>
		/// <remarks>
		/// IPv4: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16 and link-local 169.254.0.0/16.
		/// IPv6: addresses whose first byte is 0xFD (unique local).
		/// </remarks>
		/// <param name="ipAddress">The IP address to test, optionally including a port.</param>
		/// <returns><c>true</c> if the address is private; otherwise <c>false</c>.</returns>
		public bool IsPrivateIpAddress( string ipAddress )
		{
			// http://en.wikipedia.org/wiki/Private_network
			// Private IP Addresses are: 
			//  24-bit block: 10.0.0.0 through 10.255.255.255
			//  20-bit block: 172.16.0.0 through 172.31.255.255
			//  16-bit block: 192.168.0.0 through 192.168.255.255
			//  Link-local addresses: 169.254.0.0 through 169.254.255.255 (http://en.wikipedia.org/wiki/Link-local_address)

			IPAddress ip = ParseIp( ipAddress );
			byte[] octets = ip.GetAddressBytes();

			bool isIpv6 = octets.Length == 16;

			if( isIpv6 )
			{
				bool isUniqueLocalAddress = octets[0] == 253;
				return isUniqueLocalAddress;
			}
			else
			{
				bool is24BitBlock = octets[0] == 10;
				if( is24BitBlock )
				{
					return true;
				}

				bool is20BitBlock = octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31;
				if( is20BitBlock )
				{
					return true;
				}

				bool is16BitBlock = octets[0] == 192 && octets[1] == 168;
				if( is16BitBlock )
				{
					return true;
				}

				bool isLinkLocalAddress = octets[0] == 169 && octets[1] == 254;
				return isLinkLocalAddress;
			}
		}

	}
}