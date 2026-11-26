using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvcThrottle2
{
	/// <summary>
	/// Stores the initial access time and the numbers of calls made from that point
	/// </summary>
	[Serializable]
	public struct ThrottleCounter
	{
		/// <summary>
		/// Gets or sets the UTC time of the first request in the current period.
		/// </summary>
		public DateTime Timestamp { get; set; }

		/// <summary>
		/// Gets or sets the number of requests made since <see cref="Timestamp"/>.
		/// </summary>
		public long TotalRequests { get; set; }
	}
}