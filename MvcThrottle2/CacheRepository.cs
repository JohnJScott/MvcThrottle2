using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;

namespace MvcThrottle2
{
	/// <summary>
	/// Stores throttle metrics in the ASP.NET cache (<see cref="HttpContext.Cache"/>).
	/// </summary>
	public class CacheRepository : IThrottleRepository
	{
		/// <summary>
		/// Inserts a new throttle counter or updates the existing one with the same key.
		/// </summary>
		/// <param name="id">The throttle counter key.</param>
		/// <param name="throttleCounter">The counter to store.</param>
		/// <param name="expirationTime">The sliding expiration applied when the entry is first inserted.</param>
		public void Save( string id, ThrottleCounter throttleCounter, TimeSpan expirationTime )
		{
			if( HttpContext.Current.Cache[id] != null )
			{
				HttpContext.Current.Cache[id] = throttleCounter;
			}
			else
			{
				HttpContext.Current.Cache.Add(
					id,
					throttleCounter,
					null,
					Cache.NoAbsoluteExpiration,
					expirationTime,
					CacheItemPriority.Low,
					null );
			}
		}

		/// <summary>
		/// Determines whether a throttle counter exists for the given key.
		/// </summary>
		/// <param name="id">The throttle counter key.</param>
		/// <returns><c>true</c> if an entry exists in the cache; otherwise <c>false</c>.</returns>
		public bool Any( string id )
		{
			return HttpContext.Current.Cache[id] != null;
		}

		/// <summary>
		/// Gets the throttle counter stored under the given key.
		/// </summary>
		/// <param name="id">The throttle counter key.</param>
		/// <returns>The stored counter, or <c>null</c> if none exists.</returns>
		public ThrottleCounter? FirstOrDefault( string id )
		{
			return ( ThrottleCounter? )HttpContext.Current.Cache[id];
		}

		/// <summary>
		/// Removes the throttle counter stored under the given key.
		/// </summary>
		/// <param name="id">The throttle counter key.</param>
		public void Remove( string id )
		{
			HttpContext.Current.Cache.Remove( id );
		}

		/// <summary>
		/// Removes every <see cref="ThrottleCounter"/> entry from the cache, leaving other cached items untouched.
		/// </summary>
		public void Clear()
		{
			IDictionaryEnumerator cacheEnumerator = HttpContext.Current.Cache.GetEnumerator();
			while( cacheEnumerator.MoveNext() )
			{
				if( cacheEnumerator.Value.GetType() == typeof( ThrottleCounter ) )
				{
					HttpContext.Current.Cache.Remove( cacheEnumerator.Key.ToString() );
				}
			}
		}
	}
}