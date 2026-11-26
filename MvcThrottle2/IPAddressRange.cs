using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MvcThrottle2
{
	/// <summary>
	/// IP v4 and v6 range helper by jsakamoto
	/// Fork from https://github.com/jsakamoto/ipaddressrange
	/// </summary>
	/// <example>
	/// "192.168.0.0/24" 
	/// "fe80::/10" 
	/// "192.168.0.0/255.255.255.0" 
	/// "192.168.0.0-192.168.0.255"
	/// </example>
	[Serializable]
	public class IPAddressRange : ISerializable
	{
		/// <summary>
		/// Gets or sets the first address in the range (inclusive).
		/// </summary>
		public IPAddress Begin { get; set; }

		/// <summary>
		/// Gets or sets the last address in the range (inclusive).
		/// </summary>
		public IPAddress End { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IPAddressRange"/> class with both ends set to 0.0.0.0.
		/// </summary>
		public IPAddressRange()
		{
			Begin = new IPAddress( 0L );
			End = new IPAddress( 0L );
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IPAddressRange"/> class by parsing a range string.
		/// </summary>
		/// <param name="ipRangeString">
		/// A single address ("127.0.0.1"), CIDR range ("192.168.0.0/24"), bit mask range
		/// ("192.168.0.0/255.255.255.0") or begin-end range ("192.168.0.0-192.168.0.255"). Spaces are ignored.
		/// </param>
		/// <exception cref="FormatException">The string is not in any of the supported formats.</exception>
		public IPAddressRange( string ipRangeString )
		{
			// remove all spaces.
			ipRangeString = ipRangeString.Replace( " ", "" );

			// Pattern 1. CIDR range: "192.168.0.0/24", "fe80::/10"
			Match m1 = Regex.Match( ipRangeString, @"^(?<adr>[\da-f\.:]+)/(?<maskLen>\d+)$", RegexOptions.IgnoreCase );
			if( m1.Success )
			{
				byte[] baseAdrBytes = IPAddress.Parse( m1.Groups["adr"].Value ).GetAddressBytes();
				byte[] maskBytes = Bits.GetBitMask( baseAdrBytes.Length, int.Parse( m1.Groups["maskLen"].Value ) );
				baseAdrBytes = Bits.And( baseAdrBytes, maskBytes );
				Begin = new IPAddress( baseAdrBytes );
				End = new IPAddress( Bits.Or( baseAdrBytes, Bits.Not( maskBytes ) ) );
				return;
			}

			// Pattern 2. Uni address: "127.0.0.1", ":;1"
			Match m2 = Regex.Match( ipRangeString, @"^(?<adr>[\da-f\.:]+)$", RegexOptions.IgnoreCase );
			if( m2.Success )
			{
				Begin = End = IPAddress.Parse( ipRangeString );
				return;
			}

			// Pattern 3. Begin end range: "169.258.0.0-169.258.0.255"
			Match m3 = Regex.Match( ipRangeString, @"^(?<begin>[\da-f\.:]+)-(?<end>[\da-f\.:]+)$", RegexOptions.IgnoreCase );
			if( m3.Success )
			{
				Begin = IPAddress.Parse( m3.Groups["begin"].Value );
				End = IPAddress.Parse( m3.Groups["end"].Value );
				return;
			}

			// Pattern 4. Bit mask range: "192.168.0.0/255.255.255.0"
			Match m4 = Regex.Match( ipRangeString, @"^(?<adr>[\da-f\.:]+)/(?<bitmask>[\da-f\.:]+)$", RegexOptions.IgnoreCase );
			if( m4.Success )
			{
				byte[] baseAdrBytes = IPAddress.Parse( m4.Groups["adr"].Value ).GetAddressBytes();
				byte[] maskBytes = IPAddress.Parse( m4.Groups["bitmask"].Value ).GetAddressBytes();
				baseAdrBytes = Bits.And( baseAdrBytes, maskBytes );
				Begin = new IPAddress( baseAdrBytes );
				End = new IPAddress( Bits.Or( baseAdrBytes, Bits.Not( maskBytes ) ) );
				return;
			}

			throw new FormatException( "Unknown IP range string." );
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IPAddressRange"/> class from serialized data.
		/// </summary>
		/// <param name="info">The serialized object data.</param>
		/// <param name="context">The source of the serialized stream.</param>
		protected IPAddressRange( SerializationInfo info, StreamingContext context )
		{
			List<string> names = new List<string>();
			foreach( SerializationEntry item in info )
			{
				names.Add( item.Name );
			}

			Func<string, IPAddress> deserialize = ( name ) => names.Contains( name ) ?
				IPAddress.Parse( info.GetValue( name, typeof( object ) ).ToString() ) :
				new IPAddress( 0L );

			Begin = deserialize( "Begin" );
			End = deserialize( "End" );
		}

		/// <summary>
		/// Determines whether the given address falls within this range.
		/// </summary>
		/// <param name="ipaddress">The address to test.</param>
		/// <returns>
		/// <c>true</c> if the address is between <see cref="Begin"/> and <see cref="End"/> inclusive;
		/// <c>false</c> if it is outside the range or of a different address family.
		/// </returns>
		public bool Contains( IPAddress ipaddress )
		{
			if( ipaddress.AddressFamily != Begin.AddressFamily )
			{
				return false;
			}

			byte[] adrBytes = ipaddress.GetAddressBytes();
			return Bits.GE( Begin.GetAddressBytes(), adrBytes ) && Bits.LE( End.GetAddressBytes(), adrBytes );
		}

		/// <summary>
		/// Populates a <see cref="SerializationInfo"/> with the begin and end addresses as strings.
		/// </summary>
		/// <param name="info">The object to populate.</param>
		/// <param name="context">The destination of the serialization.</param>
		public void GetObjectData( SerializationInfo info, StreamingContext context )
		{
			info.AddValue( "Begin", Begin != null ? Begin.ToString() : "" );
			info.AddValue( "End", End != null ? End.ToString() : "" );
		}
	}

	internal static class Bits
	{
		internal static byte[] Not( byte[] bytes )
		{
			return bytes.Select( b => ( byte )~b ).ToArray();
		}

		internal static byte[] And( byte[] A, byte[] B )
		{
			return A.Zip( B, ( a, b ) => ( byte )( a & b ) ).ToArray();
		}

		internal static byte[] Or( byte[] A, byte[] B )
		{
			return A.Zip( B, ( a, b ) => ( byte )( a | b ) ).ToArray();
		}

		internal static bool GE( byte[] A, byte[] B )
		{
			return A.Zip( B, ( a, b ) => a == b ? 0 : a < b ? 1 : -1 )
				.SkipWhile( c => c == 0 )
				.FirstOrDefault() >= 0;
		}

		internal static bool LE( byte[] A, byte[] B )
		{
			return A.Zip( B, ( a, b ) => a == b ? 0 : a < b ? 1 : -1 )
				.SkipWhile( c => c == 0 )
				.FirstOrDefault() <= 0;
		}

		internal static byte[] GetBitMask( int sizeOfBuff, int bitLen )
		{
			byte[] maskBytes = new byte[sizeOfBuff];
			int bytesLen = bitLen / 8;
			int bitsLen = bitLen % 8;
			for( int i = 0; i < bytesLen; i++ )
			{
				maskBytes[i] = 0xff;
			}
			if( bitsLen > 0 )
			{
				maskBytes[bytesLen] = ( byte )~Enumerable.Range( 1, 8 - bitsLen ).Select( n => 1 << n - 1 ).Aggregate( ( a, b ) => a | b );
			}

			return maskBytes;
		}

	}
}