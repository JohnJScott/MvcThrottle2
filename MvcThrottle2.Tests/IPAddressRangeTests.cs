using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace MvcThrottle2.Tests
{
	public class IPAddressRangeTests
	{
		[Fact]
		public void DefaultConstructor_SetsBothEndsToZero()
		{
			IPAddressRange range = new IPAddressRange();

			Assert.Equal( IPAddress.Parse( "0.0.0.0" ), range.Begin );
			Assert.Equal( IPAddress.Parse( "0.0.0.0" ), range.End );
		}

		[Theory]
		[InlineData( "192.168.0.0/24", "192.168.0.0", "192.168.0.255" )]
		[InlineData( "192.168.0.77/24", "192.168.0.0", "192.168.0.255" )]
		[InlineData( "10.0.0.0/8", "10.0.0.0", "10.255.255.255" )]
		[InlineData( "172.16.0.0/12", "172.16.0.0", "172.31.255.255" )]
		[InlineData( "1.2.3.4/32", "1.2.3.4", "1.2.3.4" )]
		[InlineData( "fe80::/10", "fe80::", "febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff" )]
		public void Parse_CidrRange( string input, string expectedBegin, string expectedEnd )
		{
			IPAddressRange range = new IPAddressRange( input );

			Assert.Equal( IPAddress.Parse( expectedBegin ), range.Begin );
			Assert.Equal( IPAddress.Parse( expectedEnd ), range.End );
		}

		[Theory]
		[InlineData( "127.0.0.1" )]
		[InlineData( "::1" )]
		public void Parse_SingleAddress( string input )
		{
			IPAddressRange range = new IPAddressRange( input );

			Assert.Equal( IPAddress.Parse( input ), range.Begin );
			Assert.Equal( IPAddress.Parse( input ), range.End );
		}

		[Theory]
		[InlineData( "192.168.0.0-192.168.0.255" )]
		[InlineData( "192.168.0.0 - 192.168.0.255" )]
		public void Parse_BeginEndRange_IgnoresSpaces( string input )
		{
			IPAddressRange range = new IPAddressRange( input );

			Assert.Equal( IPAddress.Parse( "192.168.0.0" ), range.Begin );
			Assert.Equal( IPAddress.Parse( "192.168.0.255" ), range.End );
		}

		[Fact]
		public void Parse_BitMaskRange()
		{
			IPAddressRange range = new IPAddressRange( "192.168.1.10/255.255.255.0" );

			Assert.Equal( IPAddress.Parse( "192.168.1.0" ), range.Begin );
			Assert.Equal( IPAddress.Parse( "192.168.1.255" ), range.End );
		}

		[Theory]
		[InlineData( "not an ip" )]
		[InlineData( "1.2.3.4/" )]
		[InlineData( "" )]
		public void Parse_InvalidString_ThrowsFormatException( string input )
		{
			Assert.Throws<FormatException>( () => new IPAddressRange( input ) );
		}

		[Theory]
		[InlineData( "192.168.0.0/24", "192.168.0.0", true )]
		[InlineData( "192.168.0.0/24", "192.168.0.128", true )]
		[InlineData( "192.168.0.0/24", "192.168.0.255", true )]
		[InlineData( "192.168.0.0/24", "192.168.1.0", false )]
		[InlineData( "192.168.0.0/24", "192.167.255.255", false )]
		[InlineData( "10.0.0.5-10.0.0.10", "10.0.0.5", true )]
		[InlineData( "10.0.0.5-10.0.0.10", "10.0.0.10", true )]
		[InlineData( "10.0.0.5-10.0.0.10", "10.0.0.4", false )]
		[InlineData( "10.0.0.5-10.0.0.10", "10.0.0.11", false )]
		[InlineData( "fe80::/10", "fe80::1", true )]
		[InlineData( "fe80::/10", "fec0::1", false )]
		public void Contains( string range, string address, bool expected )
		{
			IPAddressRange ipRange = new IPAddressRange( range );

			Assert.Equal( expected, ipRange.Contains( IPAddress.Parse( address ) ) );
		}

		[Fact]
		public void Contains_DifferentAddressFamily_ReturnsFalse()
		{
			IPAddressRange range = new IPAddressRange( "0.0.0.0/0" );

			Assert.False( range.Contains( IPAddress.Parse( "::1" ) ) );
		}

		[Fact]
		public void Serialization_RoundTripsBeginAndEnd()
		{
			IPAddressRange original = new IPAddressRange( "10.1.0.0/16" );
			BinaryFormatter formatter = new BinaryFormatter();

			IPAddressRange copy;
			using( MemoryStream stream = new MemoryStream() )
			{
				formatter.Serialize( stream, original );
				stream.Position = 0;
				copy = ( IPAddressRange )formatter.Deserialize( stream );
			}

			Assert.Equal( original.Begin, copy.Begin );
			Assert.Equal( original.End, copy.End );
		}
	}
}