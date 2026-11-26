using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MvcThrottle2.Demo.Helpers
{
	public class NginxIpAddressParser : IpAddressParser
	{
		/// <summary>
		/// Gets the client IP, using the first public address in <c>X-Forwarded-For</c> as set by NGINX.
		/// Falls back to <see cref="HttpRequestBase.UserHostAddress"/> if the header has no public address.
		/// </summary>
		/// <param name="request">The current HTTP request.</param>
		/// <returns>The client IP address.</returns>
		public override string GetClientIp( HttpRequestBase request )
		{
			string ipAddress = request.UserHostAddress;

			// get client IP from reverse proxy
			string xForwardedFor = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
			if( !string.IsNullOrEmpty( xForwardedFor ) )
			{
				// Search for public IP addresses 
				List<string> publicForwardingIps = xForwardedFor.Split( ',' ).Where( ip => !IsPrivateIpAddress( ip ) ).ToList();

				// If we found any public IP, return the first one when NGNIX is used, otherwise return the user host address
				return publicForwardingIps.Any() ? publicForwardingIps.First().Trim() : ipAddress;
			}

			return ipAddress;
		}
	}
}