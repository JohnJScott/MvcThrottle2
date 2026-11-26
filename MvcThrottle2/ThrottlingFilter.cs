using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace MvcThrottle2
{
	/// <summary>
	/// Action filter that enforces a <see cref="ThrottlePolicy"/> on controllers and actions
	/// marked with <see cref="EnableThrottlingAttribute"/>.
	/// </summary>
	public class ThrottlingFilter : ActionFilterAttribute, IActionFilter
	{
		/// <summary>
		/// Creates a new instance of the <see cref="ThrottlingFilter"/> class.
		/// By default, the <see cref="QuotaExceededResponseCode"/> property 
		/// is set to 429 (Too Many Requests).
		/// </summary>
		public ThrottlingFilter()
		{
			QuotaExceededResponseCode = ( HttpStatusCode )429;
			Repository = new CacheRepository();
			IpAddressParser = new IpAddressParser();
		}

		/// <summary>
		/// Throttling rate limits policy
		/// </summary>
		public ThrottlePolicy Policy { get; set; }

		/// <summary>
		/// Throttle metrics storage
		/// </summary>
		public IThrottleRepository Repository { get; set; }

		/// <summary>
		/// Log blocked requests
		/// </summary>
		public IThrottleLogger Logger { get; set; }

		/// <summary>
		/// If none specifed the default will be: 
		/// HTTP request quota exceeded! maximum admitted {0} per {1}
		/// </summary>
		public string QuotaExceededMessage { get; set; }

		/// <summary>
		/// Gets or sets the value to return as the HTTP status 
		/// code when a request is rejected because of the
		/// throttling policy. The default value is 429 (Too Many Requests).
		/// </summary>
		public HttpStatusCode QuotaExceededResponseCode { get; set; }

		/// <summary>
		/// Gets or sets the parser used to resolve client IPs and match IP rules.
		/// The default is <see cref="MvcThrottle2.IpAddressParser"/>.
		/// </summary>
		public IIpAddressParser IpAddressParser { get; set; }

		/// <summary>
		/// Applies the throttling policy before the action executes. If a limit is exceeded, the request
		/// is logged, the <c>Retry-After</c> header and status code are set, and <see cref="ActionExecutingContext.Result"/>
		/// is set to the result of <see cref="QuotaExceededResult"/>.
		/// </summary>
		/// <param name="filterContext">The action executing context.</param>
		public override void OnActionExecuting( ActionExecutingContext filterContext )
		{
			bool applyThrottling = ApplyThrottling( filterContext, out EnableThrottlingAttribute attrPolicy );

			if( Policy != null && applyThrottling )
			{
				RequestIdentity identity = SetIdentity( filterContext.HttpContext.Request );

				if( !IsWhitelisted( identity ) )
				{
					TimeSpan timeSpan = TimeSpan.FromSeconds( 1 );

					IEnumerable<KeyValuePair<RateLimitPeriod, long>> rates = Policy.Rates.AsEnumerable();
					if( Policy.StackBlockedRequests )
					{
						//all requests including the rejected ones will stack in this order: day, hour, min, sec
						//if a client hits the hour limit then the minutes and seconds counters will expire and will eventually get erased from cache
						rates = Policy.Rates.Reverse();
					}

					//apply policy
					//the IP rules are applied last and will overwrite any client rule you might defined
					foreach( KeyValuePair<RateLimitPeriod, long> rate in rates )
					{
						RateLimitPeriod rateLimitPeriod = rate.Key;
						long rateLimit = rate.Value;

						switch( rateLimitPeriod )
						{
							case RateLimitPeriod.Second:
								timeSpan = TimeSpan.FromSeconds( 1 );
								break;
							case RateLimitPeriod.Minute:
								timeSpan = TimeSpan.FromMinutes( 1 );
								break;
							case RateLimitPeriod.Hour:
								timeSpan = TimeSpan.FromHours( 1 );
								break;
							case RateLimitPeriod.Day:
								timeSpan = TimeSpan.FromDays( 1 );
								break;
							case RateLimitPeriod.Week:
								timeSpan = TimeSpan.FromDays( 7 );
								break;
						}

						//increment counter
						ThrottleCounter throttleCounter = ProcessRequest( identity, timeSpan, rateLimitPeriod, out string requestId );

						if( throttleCounter.Timestamp + timeSpan < DateTime.UtcNow )
						{
							continue;
						}

						//apply EnableThrottlingAttribute policy
						long attrLimit = attrPolicy.GetLimit( rateLimitPeriod );
						if( attrLimit > 0 )
						{
							rateLimit = attrLimit;
						}

						//apply endpoint rate limits
						if( Policy.EndpointRules != null )
						{
							List<KeyValuePair<string, RateLimits>> rules = Policy.EndpointRules.Where( x => identity.Endpoint.IndexOf( x.Key, 0, StringComparison.InvariantCultureIgnoreCase ) != -1 ).ToList();
							if( rules.Any() )
							{
								//get the lower limit from all applying rules
								long customRate = ( from r in rules let rateValue = r.Value.GetLimit( rateLimitPeriod ) select rateValue ).Min();

								if( customRate > 0 )
								{
									rateLimit = customRate;
								}
							}
						}

						//apply custom rate limit for clients that will override endpoint limits
						if( Policy.ClientRules != null && Policy.ClientRules.Keys.Contains( identity.ClientKey ) )
						{
							long limit = Policy.ClientRules[identity.ClientKey].GetLimit( rateLimitPeriod );
							if( limit > 0 )
							{
								rateLimit = limit;
							}
						}

						//apply custom rate limit for user agent
						if( Policy.UserAgentRules != null && !string.IsNullOrEmpty( identity.UserAgent ) )
						{
							List<KeyValuePair<string, RateLimits>> rules = Policy.UserAgentRules.Where( x => identity.UserAgent.IndexOf( x.Key, 0, StringComparison.InvariantCultureIgnoreCase ) != -1 ).ToList();
							if( rules.Any() )
							{
								//get the lower limit from all applying rules
								long customRate = ( from r in rules let rateValue = r.Value.GetLimit( rateLimitPeriod ) select rateValue ).Min();
								rateLimit = customRate;
							}
						}

						//enforce ip rate limit as is most specific 
						if( Policy.IpRules != null && IpAddressParser.ContainsIp( Policy.IpRules.Keys.ToList(), identity.ClientIp, out string ipRule ) )
						{
							long limit = Policy.IpRules[ipRule].GetLimit( rateLimitPeriod );
							if( limit > 0 )
							{
								rateLimit = limit;
							}
						}

						//check if limit is reached
						if( rateLimit > 0 && throttleCounter.TotalRequests > rateLimit )
						{
							//log blocked request
							Logger?.Log( ComputeLogEntry( requestId, identity, throttleCounter, rateLimitPeriod.ToString(), rateLimit, filterContext.HttpContext.Request ) );

							//break execution and return 409 
							string message = string.IsNullOrEmpty( QuotaExceededMessage ) ?
								"HTTP request quota exceeded! maximum admitted {0} per {1}" : QuotaExceededMessage;

							//add status code and retry after x seconds to response
							filterContext.HttpContext.Response.StatusCode = ( int )QuotaExceededResponseCode;
							filterContext.HttpContext.Response.Headers.Set( "Retry-After", RetryAfterFrom( throttleCounter.Timestamp, rateLimitPeriod ) );

							filterContext.Result = QuotaExceededResult(
								filterContext.RequestContext,
								string.Format( message, rateLimit, rateLimitPeriod ),
								QuotaExceededResponseCode,
								requestId );

							return;
						}
					}
				}
			}

			base.OnActionExecuting( filterContext );
		}

		/// <summary>
		/// Builds the <see cref="RequestIdentity"/> used to track the request. Override to use a custom client key.
		/// </summary>
		/// <param name="request">The current HTTP request.</param>
		/// <returns>The identity of the request.</returns>
		protected virtual RequestIdentity SetIdentity( HttpRequestBase request )
		{
			RequestIdentity entry = new RequestIdentity
			{
				ClientIp = IpAddressParser.GetClientIp( request ).ToString(),

				ClientKey = request.IsAuthenticated ? "auth" : "anon"
			};

			RouteData rd = request.RequestContext.RouteData;
			string currentAction = rd.GetRequiredString( "action" );
			string currentController = rd.GetRequiredString( "controller" );

			switch( Policy.EndpointType )
			{
				case EndpointThrottlingType.AbsolutePath:
					entry.Endpoint = request.Url.AbsolutePath;
					break;
				case EndpointThrottlingType.PathAndQuery:
					entry.Endpoint = request.Url.PathAndQuery;
					break;
				case EndpointThrottlingType.ControllerAndAction:
					entry.Endpoint = currentController + "/" + currentAction;
					break;
				case EndpointThrottlingType.Controller:
					entry.Endpoint = currentController;
					break;
				default:
					break;
			}

			//case insensitive routes
			entry.Endpoint = entry.Endpoint.ToLowerInvariant();

			entry.UserAgent = request.UserAgent;

			return entry;
		}

		private static readonly object _processLocker = new object();
		private ThrottleCounter ProcessRequest( RequestIdentity requestIdentity, TimeSpan timeSpan, RateLimitPeriod period, out string id )
		{
			ThrottleCounter throttleCounter = new ThrottleCounter()
			{
				Timestamp = DateTime.UtcNow,
				TotalRequests = 1
			};

			id = ComputeThrottleKey( requestIdentity, period );

			//serial reads and writes
			lock( _processLocker )
			{
				ThrottleCounter? entry = Repository.FirstOrDefault( id );
				if( entry.HasValue )
				{
					//entry has not expired
					if( entry.Value.Timestamp + timeSpan >= DateTime.UtcNow )
					{
						//increment request count
						long totalRequests = entry.Value.TotalRequests + 1;

						//deep copy
						throttleCounter = new ThrottleCounter
						{
							Timestamp = entry.Value.Timestamp,
							TotalRequests = totalRequests
						};

					}
				}

				//stores: id (string) - timestamp (datetime) - total (long)
				Repository.Save( id, throttleCounter, timeSpan );
			}

			return throttleCounter;
		}

		/// <summary>
		/// Computes the key under which the throttle counter is stored, as a SHA-1 hex hash of the
		/// identity parts enabled in the policy and the period.
		/// </summary>
		/// <param name="requestIdentity">The identity of the request.</param>
		/// <param name="period">The rate limit period.</param>
		/// <returns>The throttle counter key.</returns>
		protected virtual string ComputeThrottleKey( RequestIdentity requestIdentity, RateLimitPeriod period )
		{
			List<string> keyValues = new List<string>()
				{
					"throttle"
				};

			if( Policy.IpThrottling )
			{
				keyValues.Add( requestIdentity.ClientIp );
			}

			if( Policy.ClientThrottling )
			{
				keyValues.Add( requestIdentity.ClientKey );
			}

			if( Policy.EndpointThrottling )
			{
				keyValues.Add( requestIdentity.Endpoint );
			}

			if( Policy.UserAgentThrottling )
			{
				keyValues.Add( requestIdentity.UserAgent );
			}

			keyValues.Add( period.ToString() );

			string id = string.Join( "_", keyValues );
			byte[] idBytes = Encoding.UTF8.GetBytes( id );
			byte[] hashBytes = new System.Security.Cryptography.SHA1Managed().ComputeHash( idBytes );
			string hex = BitConverter.ToString( hashBytes ).Replace( "-", "" );
			return hex;
		}

		private string RetryAfterFrom( DateTime timestamp, RateLimitPeriod period )
		{
			int secondsPast = Convert.ToInt32( ( DateTime.UtcNow - timestamp ).TotalSeconds );
			int retryAfter = 1;
			switch( period )
			{
				case RateLimitPeriod.Minute:
					retryAfter = 60;
					break;
				case RateLimitPeriod.Hour:
					retryAfter = 60 * 60;
					break;
				case RateLimitPeriod.Day:
					retryAfter = 60 * 60 * 24;
					break;
				case RateLimitPeriod.Week:
					retryAfter = 60 * 60 * 24 * 7;
					break;
			}
			retryAfter = retryAfter > 1 ? retryAfter - secondsPast : 1;
			return retryAfter.ToString( CultureInfo.InvariantCulture );
		}

		private bool IsWhitelisted( RequestIdentity requestIdentity )
		{
			if( Policy.IpThrottling )
			{
				if( Policy.IpWhitelist != null && IpAddressParser.ContainsIp( Policy.IpWhitelist, requestIdentity.ClientIp ) )
				{
					return true;
				}
			}

			if( Policy.ClientThrottling )
			{
				if( Policy.ClientWhitelist != null && Policy.ClientWhitelist.Contains( requestIdentity.ClientKey ) )
				{
					return true;
				}
			}

			if( Policy.EndpointThrottling )
			{
				if( Policy.EndpointWhitelist != null &&
					Policy.EndpointWhitelist.Any( x => requestIdentity.Endpoint.IndexOf( x, 0, StringComparison.InvariantCultureIgnoreCase ) != -1 ) )
				{
					return true;
				}
			}

			if( Policy.UserAgentThrottling && requestIdentity.UserAgent != null )
			{
				if( Policy.UserAgentWhitelist != null &&
					Policy.UserAgentWhitelist.Any( x => requestIdentity.UserAgent.IndexOf( x, 0, StringComparison.InvariantCultureIgnoreCase ) != -1 ) )
				{
					return true;
				}
			}

			return false;
		}

		private bool ApplyThrottling( ActionExecutingContext filterContext, out EnableThrottlingAttribute attr )
		{
			bool applyThrottling = false;
			attr = null;

			if( filterContext.ActionDescriptor.ControllerDescriptor.IsDefined( typeof( EnableThrottlingAttribute ), true ) )
			{
				attr = ( EnableThrottlingAttribute )filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes( typeof( EnableThrottlingAttribute ), true ).First();
				applyThrottling = true;
			}

			//disabled on the class
			if( filterContext.ActionDescriptor.ControllerDescriptor.IsDefined( typeof( DisableThrottlingAttribute ), true ) )
			{
				applyThrottling = false;
			}

			if( filterContext.ActionDescriptor.IsDefined( typeof( EnableThrottlingAttribute ), true ) )
			{
				attr = ( EnableThrottlingAttribute )filterContext.ActionDescriptor.GetCustomAttributes( typeof( EnableThrottlingAttribute ), true ).First();
				applyThrottling = true;
			}

			//explicit disabled
			if( filterContext.ActionDescriptor.IsDefined( typeof( DisableThrottlingAttribute ), true ) )
			{
				applyThrottling = false;
			}

			return applyThrottling;
		}

		/// <summary>
		/// Creates the result returned when a request exceeds its quota. Override to return a custom view.
		/// </summary>
		/// <param name="filterContext">The request context.</param>
		/// <param name="message">The formatted quota exceeded message.</param>
		/// <param name="responseCode">The HTTP status code to return.</param>
		/// <param name="requestId">The throttle counter key of the request.</param>
		/// <returns>An <see cref="HttpStatusCodeResult"/> by default.</returns>
		protected virtual ActionResult QuotaExceededResult( RequestContext filterContext, string message, HttpStatusCode responseCode, string requestId )
		{
			return new HttpStatusCodeResult( responseCode, message );
		}

		private ThrottleLogEntry ComputeLogEntry( string requestId, RequestIdentity identity, ThrottleCounter throttleCounter, string rateLimitPeriod, long rateLimit, HttpRequestBase request )
		{
			return new ThrottleLogEntry
			{
				ClientIp = identity.ClientIp,
				ClientKey = identity.ClientKey,
				Endpoint = identity.Endpoint,
				UserAgent = identity.UserAgent,
				LogDate = DateTime.UtcNow,
				RateLimit = rateLimit,
				RateLimitPeriod = rateLimitPeriod,
				RequestId = requestId,
				StartPeriod = throttleCounter.Timestamp,
				TotalRequests = throttleCounter.TotalRequests,
				Request = request
			};
		}
	}
}