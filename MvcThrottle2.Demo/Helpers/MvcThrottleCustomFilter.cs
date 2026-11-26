using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace MvcThrottle2.Demo.Helpers
{
	public class MvcThrottleCustomFilter 
		: MvcThrottle2.ThrottlingFilter
	{
		/// <summary>
		/// Returns the "RateLimited" view with the quota exceeded message instead of a bare status code.
		/// </summary>
		/// <param name="filterContext">The request context.</param>
		/// <param name="message">The formatted quota exceeded message.</param>
		/// <param name="responseCode">The HTTP status code to return.</param>
		/// <param name="requestId">The throttle counter key of the request.</param>
		/// <returns>The RateLimited view.</returns>
		protected override ActionResult QuotaExceededResult( RequestContext filterContext, string message, System.Net.HttpStatusCode responseCode, string requestId )
		{
			ViewResult rateLimitedView = new ViewResult
			{
				ViewName = "RateLimited"
			};
			rateLimitedView.ViewData["Message"] = message;

			return rateLimitedView;
		}
	}
}