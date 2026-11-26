using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MvcThrottle2.Demo.Controllers
{
	[DisableThrottling]
	public class BlogController : BaseController
	{
		/// <summary>
		/// Shows the blog index page. Not throttled.
		/// </summary>
		/// <returns>The Index view.</returns>
		public ActionResult Index()
		{
			ViewBag.Message = "The blog is not throttled.";

			return View();
		}

		/// <summary>
		/// Shows the blog search page. Throttled to 2 requests per second and 5 per minute.
		/// </summary>
		/// <returns>The Search view.</returns>
		[EnableThrottling( PerSecond = 2, PerMinute = 5 )]
		public ActionResult Search()
		{
			ViewBag.Message = "Searches are throttled.";

			return View();
		}
	}
}