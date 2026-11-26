using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MvcThrottle2.Demo.Controllers
{
	public class HomeController : BaseController
	{
		/// <summary>
		/// Shows the home page. Throttled by the global policy.
		/// </summary>
		/// <returns>The Index view.</returns>
		public ActionResult Index()
		{
			return View();
		}

		/// <summary>
		/// Shows the about page. Throttled to 2 requests per second and 5 per minute.
		/// </summary>
		/// <returns>The About view.</returns>
		[EnableThrottling( PerSecond = 2, PerMinute = 5 )]
		public ActionResult About()
		{
			ViewBag.Message = "Your application description page.";

			return View();
		}

		/// <summary>
		/// Shows the contact page. Not throttled.
		/// </summary>
		/// <returns>The Contact view.</returns>
		[DisableThrottling]
		public ActionResult Contact()
		{
			ViewBag.Message = "Your contact page.";

			return View();
		}
	}
}