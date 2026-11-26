using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcThrottle2
{
	/// <summary>
	/// Implement this interface if you want to create a persistent store for the throttle metrics
	/// </summary>
	public interface IThrottleRepository
	{
		/// <summary>
		/// Determines whether a throttle counter exists for the given key.
		/// </summary>
		/// <param name="id">The throttle counter key.</param>
		/// <returns><c>true</c> if a counter exists; otherwise <c>false</c>.</returns>
		bool Any( string id );

		/// <summary>
		/// Gets the throttle counter stored under the given key.
		/// </summary>
		/// <param name="id">The throttle counter key.</param>
		/// <returns>The stored counter, or <c>null</c> if none exists.</returns>
		ThrottleCounter? FirstOrDefault( string id );

		/// <summary>
		/// Inserts or updates the throttle counter stored under the given key.
		/// </summary>
		/// <param name="id">The throttle counter key.</param>
		/// <param name="throttleCounter">The counter to store.</param>
		/// <param name="expirationTime">How long the counter should be kept.</param>
		void Save( string id, ThrottleCounter throttleCounter, TimeSpan expirationTime );

		/// <summary>
		/// Removes the throttle counter stored under the given key.
		/// </summary>
		/// <param name="id">The throttle counter key.</param>
		void Remove( string id );

		/// <summary>
		/// Removes all throttle counters from the store.
		/// </summary>
		void Clear();
	}
}