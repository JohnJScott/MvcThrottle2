using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace MvcThrottle2
{
	/// <summary>
	/// Enables throttling for a controller or action. Any limit greater than 0 overrides
	/// the default limit of the global <see cref="ThrottlePolicy"/> for that period.
	/// </summary>
	public class EnableThrottlingAttribute 
		: ActionFilterAttribute
		, IActionFilter
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
		public virtual long GetLimit( RateLimitPeriod period )
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
	/// Disables throttling for a controller or action, overriding an <see cref="EnableThrottlingAttribute"/>
	/// applied at the same level.
	/// </summary>
	public class DisableThrottlingAttribute 
		: ActionFilterAttribute
		, IActionFilter
	{

	}
}