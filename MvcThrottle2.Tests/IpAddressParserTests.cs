using System;
using System.Collections.Generic;
using System.Net;
using Xunit;

namespace MvcThrottle2.Tests
{
	public class IpAddressParserTests
	{
		private readonly IpAddressParser _parser = new IpAddressParser();

		[Theory]
		[InlineData( "1.2.3.4", "1.2.3.4" )]
		[InlineData( " 1.2.3.4 ", "1.2.3.4" )]
		[InlineData( "1.2.3.4:8080", "1.2.3.4" )]
		[InlineData( "::1", "::1" )]
		[InlineData( "2001:db8::1", "2001:db8::1" )]
		[InlineData( "[2001:db8::1]:443", "2001:db8::1" )]
		public void ParseIp_StripsPort( string input, string expected )
		{
			Assert.Equal( IPAddress.Parse( expected ), _parser.ParseIp( input ) );
		}

		[Fact]
		public void ParseIp_InvalidAddress_ThrowsFormatException()
		{
			Assert.Throws<FormatException>( () => _parser.ParseIp( "not-an-ip" ) );
		}

		[Theory]
		[InlineData( "10.0.0.1", true )]
		[InlineData( "10.255.255.255", true )]
		[InlineData( "172.16.0.1", true )]
		[InlineData( "172.31.255.255", true )]
		[InlineData( "172.15.255.255", false )]
		[InlineData( "172.32.0.0", false )]
		[InlineData( "192.168.1.1", true )]
		[InlineData( "192.169.0.1", false )]
		[InlineData( "169.254.10.10", true )]
		[InlineData( "8.8.8.8", false )]
		[InlineData( "fd00::1", true )]
		[InlineData( "2001:db8::1", false )]
		public void IsPrivateIpAddress( string address, bool expected )
		{
			Assert.Equal( expected, _parser.IsPrivateIpAddress( address ) );
		}

		[Fact]
		public void ContainsIp_MatchingRule_ReturnsTrue()
		{
			List<string> rules = new List<string> { "10.0.0.0/8", "192.168.0.0/16" };

			Assert.True( _parser.ContainsIp( rules, "192.168.5.5" ) );
		}

		[Fact]
		public void ContainsIp_NoMatchingRule_ReturnsFalse()
		{
			List<string> rules = new List<string> { "10.0.0.0/8" };

			Assert.False( _parser.ContainsIp( rules, "8.8.8.8" ) );
		}

		[Fact]
		public void ContainsIp_NullOrEmptyRules_ReturnsFalse()
		{
			Assert.False( _parser.ContainsIp( null, "1.2.3.4" ) );
			Assert.False( _parser.ContainsIp( new List<string>(), "1.2.3.4" ) );
		}

		[Fact]
		public void ContainsIp_WithOut_ReturnsFirstMatchingRule()
		{
			List<string> rules = new List<string> { "10.0.0.0/8", "1.2.3.0/24", "1.2.3.4" };

			bool found = _parser.ContainsIp( rules, "1.2.3.4", out string rule );

			Assert.True( found );
			Assert.Equal( "1.2.3.0/24", rule );
		}

		[Fact]
		public void ContainsIp_WithOut_NoMatch_ReturnsNullRule()
		{
			List<string> rules = new List<string> { "10.0.0.0/8" };

			bool found = _parser.ContainsIp( rules, "1.2.3.4", out string rule );

			Assert.False( found );
			Assert.Null( rule );
		}

		[Fact]
		public void GetClientIp_NoForwardedHeader_ReturnsUserHostAddress()
		{
			TestRequest request = TestRequestFactory.Create( new TestRequestOptions { ClientIp = "5.6.7.8" } );

			Assert.Equal( "5.6.7.8", _parser.GetClientIp( request.Request ) );
		}

		[Fact]
		public void GetClientIp_ForwardedHeader_ReturnsLastPublicAddress()
		{
			TestRequest request = TestRequestFactory.Create( new TestRequestOptions
			{
				ClientIp = "10.0.0.1",
				ForwardedFor = "10.0.0.2, 1.1.1.1, 2.2.2.2, 192.168.0.1"
			} );

			Assert.Equal( "2.2.2.2", _parser.GetClientIp( request.Request ) );
		}

		[Fact]
		public void GetClientIp_ForwardedHeaderWithOnlyPrivateAddresses_ReturnsUserHostAddress()
		{
			TestRequest request = TestRequestFactory.Create( new TestRequestOptions
			{
				ClientIp = "5.6.7.8",
				ForwardedFor = "10.0.0.2, 192.168.0.1"
			} );

			Assert.Equal( "5.6.7.8", _parser.GetClientIp( request.Request ) );
		}
	}
}