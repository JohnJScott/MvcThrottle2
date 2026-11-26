using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcThrottle2
{
	/// <summary>
	/// A set of request limits per time period. A value of 0 means no limit is set for that period.
	/// </summary>
	[Serializable]
	public class RateLimits
	{
		/// <summary>
		/// Gets or sets the maximum number of requests allowed per second.
		/// </summary>
		public long PerSecond { get; set; }

		/// <summary>
		/// Gets or sets the maximum number of requests allowed per minute.
		/// </summary>
		public long PerMinute { get; set; }

		/// <summary>
		/// Gets or sets the maximum number of requests allowed per hour.
		/// </summary>
		public long PerHour { get; set; }

		/// <summary>
		/// Gets or sets the maximum number of requests allowed per day.
		/// </summary>
		public long PerDay { get; set; }

		/// <summary>
		/// Gets or sets the maximum number of requests allowed per week.
		/// </summary>
		public long PerWeek { get; set; }

		/// <summary>
		/// Gets the limit configured for the given period.
		/// </summary>
		/// <param name="period">The rate limit period.</param>
		/// <returns>The limit for <paramref name="period"/>; <see cref="PerSecond"/> for an unknown period.</returns>
		public long GetLimit( RateLimitPeriod period )
		{
			switch( period )
			{
				case RateLimitPeriod.Second:
					return PerSecond;
				case RateLimitPeriod.Minute:
					return PerMinute;
				case RateLimitPeriod.Hour:
					return PerHour;
				case RateLimitPeriod.Day:
					return PerDay;
				case RateLimitPeriod.Week:
					return PerWeek;
				default:
					return PerSecond;
			}
		}
	}

	/// <summary>
	/// The time period a rate limit applies to.
	/// </summary>
	public enum RateLimitPeriod
	{
		/// <summary>One second.</summary>
		Second = 1,
		/// <summary>One minute.</summary>
		Minute,
		/// <summary>One hour.</summary>
		Hour,
		/// <summary>One day.</summary>
		Day,
		/// <summary>Seven days.</summary>
		Week
	}
}