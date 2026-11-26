using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcThrottle2
{
	/// <summary>
	/// Log requests that exceed the limit
	/// </summary>
	public interface IThrottleLogger
	{
		/// <summary>
		/// Logs a request that was blocked because it exceeded a rate limit.
		/// </summary>
		/// <param name="entry">Details of the blocked request.</param>
		void Log( ThrottleLogEntry entry );
	}
}