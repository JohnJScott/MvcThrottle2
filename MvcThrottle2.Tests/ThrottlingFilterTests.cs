using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Xunit;

namespace MvcThrottle2.Tests
{
	public class ThrottlingFilterTests
	{
		private readonly InMemoryThrottleRepository _repository = new InMemoryThrottleRepository();
		private readonly RecordingThrottleLogger _logger = new RecordingThrottleLogger();

		/// <summary>
		/// Exposes the protected members of <see cref="ThrottlingFilter"/>.
		/// </summary>
		private sealed class TestableThrottlingFilter : ThrottlingFilter
		{
			public string ComputeKey( RequestIdentity identity, RateLimitPeriod period )
			{
				return ComputeThrottleKey( identity, period );
			}

			public RequestIdentity Identify( HttpRequestBase request )
			{
				return SetIdentity( request );
			}
		}

		private sealed class CustomResultThrottlingFilter : ThrottlingFilter
		{
			protected override ActionResult QuotaExceededResult( RequestContext filterContext, string message, HttpStatusCode responseCode, string requestId )
			{
				return new ContentResult { Content = "custom:" + message };
			}
		}

		private TestableThrottlingFilter CreateFilter( ThrottlePolicy policy )
		{
			return new TestableThrottlingFilter
			{
				Policy = policy,
				Repository = _repository,
				Logger = _logger
			};
		}

		private static ThrottlePolicy PerMinute( long limit )
		{
			return new ThrottlePolicy( null, perMinute: limit ) { IpThrottling = true };
		}

		private static TestRequest Send( ThrottlingFilter filter, TestRequestOptions options )
		{
			TestRequest request = TestRequestFactory.Create( options );
			filter.OnActionExecuting( request.Context );
			return request;
		}

		private static TestRequest Send( ThrottlingFilter filter )
		{
			return Send( filter, new TestRequestOptions() );
		}

		/// <summary>
		/// Sends <paramref name="count"/> identical requests and returns the last one.
		/// </summary>
		private static TestRequest SendMany( ThrottlingFilter filter, int count, Func<TestRequestOptions> options )
		{
			TestRequest last = null;
			for( int i = 0; i < count; i++ )
			{
				last = Send( filter, options() );
			}

			return last;
		}

		private static bool IsBlocked( TestRequest request )
		{
			return request.Context.Result != null;
		}

		[Fact]
		public void RequestsWithinLimit_AreAllowed()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 2 ) );

			Assert.False( IsBlocked( Send( filter ) ) );
			Assert.False( IsBlocked( Send( filter ) ) );
		}

		[Fact]
		public void RequestOverLimit_IsBlockedWith429AndRetryAfter()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 2 ) );
			Send( filter );
			Send( filter );

			TestRequest blocked = Send( filter );

			HttpStatusCodeResult result = Assert.IsType<HttpStatusCodeResult>( blocked.Context.Result );
			Assert.Equal( 429, result.StatusCode );
			Assert.Equal( "HTTP request quota exceeded! maximum admitted 2 per Minute", result.StatusDescription );
			Assert.Equal( 429, blocked.Response.Object.StatusCode );

			int retryAfter = int.Parse( blocked.ResponseHeaders["Retry-After"], CultureInfo.InvariantCulture );
			Assert.InRange( retryAfter, 1, 60 );
		}

		[Fact]
		public void NoPolicy_NothingIsThrottled()
		{
			ThrottlingFilter filter = CreateFilter( null );

			TestRequest last = SendMany( filter, 5, () => new TestRequestOptions() );

			Assert.False( IsBlocked( last ) );
		}

		[Fact]
		public void NoEnableThrottlingAttribute_NothingIsThrottled()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			Func<TestRequestOptions> options = () =>
			{
				TestRequestOptions o = new TestRequestOptions();
				o.ActionAttributes.Clear();
				return o;
			};

			Assert.False( IsBlocked( SendMany( filter, 5, options ) ) );
		}

		[Fact]
		public void EnableThrottlingOnController_ThrottlesActions()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			Func<TestRequestOptions> options = () =>
			{
				TestRequestOptions o = new TestRequestOptions();
				o.ActionAttributes.Clear();
				o.ControllerAttributes.Add( new EnableThrottlingAttribute() );
				return o;
			};

			Assert.True( IsBlocked( SendMany( filter, 2, options ) ) );
		}

		[Fact]
		public void DisableThrottlingOnAction_OverridesEnableOnController()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			Func<TestRequestOptions> options = () =>
			{
				TestRequestOptions o = new TestRequestOptions();
				o.ActionAttributes.Clear();
				o.ActionAttributes.Add( new DisableThrottlingAttribute() );
				o.ControllerAttributes.Add( new EnableThrottlingAttribute() );
				return o;
			};

			Assert.False( IsBlocked( SendMany( filter, 5, options ) ) );
		}

		[Fact]
		public void DisableThrottlingOnController_IsOverriddenByEnableOnAction()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			Func<TestRequestOptions> options = () =>
			{
				TestRequestOptions o = new TestRequestOptions();
				o.ControllerAttributes.Add( new DisableThrottlingAttribute() );
				return o;
			};

			Assert.True( IsBlocked( SendMany( filter, 2, options ) ) );
		}

		[Fact]
		public void AttributeLimit_OverridesPolicyLimit()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 10 ) );
			Func<TestRequestOptions> options = () =>
			{
				TestRequestOptions o = new TestRequestOptions();
				o.ActionAttributes.Clear();
				o.ActionAttributes.Add( new EnableThrottlingAttribute { PerMinute = 1 } );
				return o;
			};

			Assert.True( IsBlocked( SendMany( filter, 2, options ) ) );
		}

		[Fact]
		public void IpThrottling_CountsEachClientIpSeparately()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			Send( filter, new TestRequestOptions { ClientIp = "1.1.1.1" } );

			Assert.False( IsBlocked( Send( filter, new TestRequestOptions { ClientIp = "2.2.2.2" } ) ) );
			Assert.True( IsBlocked( Send( filter, new TestRequestOptions { ClientIp = "1.1.1.1" } ) ) );
		}

		[Fact]
		public void IpWhitelist_IsNeverThrottled()
		{
			ThrottlePolicy policy = PerMinute( 1 );
			policy.IpWhitelist = new List<string> { "1.2.3.0/24" };
			ThrottlingFilter filter = CreateFilter( policy );

			Assert.False( IsBlocked( SendMany( filter, 5, () => new TestRequestOptions { ClientIp = "1.2.3.4" } ) ) );
		}

		[Fact]
		public void IpRules_OverrideDefaultLimit()
		{
			ThrottlePolicy policy = PerMinute( 10 );
			policy.IpRules = new Dictionary<string, RateLimits> { { "1.2.3.4", new RateLimits { PerMinute = 1 } } };
			ThrottlingFilter filter = CreateFilter( policy );

			Assert.True( IsBlocked( SendMany( filter, 2, () => new TestRequestOptions { ClientIp = "1.2.3.4" } ) ) );
			Assert.False( IsBlocked( SendMany( filter, 2, () => new TestRequestOptions { ClientIp = "5.6.7.8" } ) ) );
		}

		[Fact]
		public void ClientWhitelist_AuthenticatedClientsAreNotThrottled()
		{
			ThrottlePolicy policy = new ThrottlePolicy( null, perMinute: 1 )
			{
				ClientThrottling = true,
				ClientWhitelist = new List<string> { "auth" }
			};
			ThrottlingFilter filter = CreateFilter( policy );

			Assert.False( IsBlocked( SendMany( filter, 5, () => new TestRequestOptions { IsAuthenticated = true } ) ) );
			Assert.True( IsBlocked( SendMany( filter, 2, () => new TestRequestOptions { IsAuthenticated = false } ) ) );
		}

		[Fact]
		public void ClientRules_OverrideDefaultLimit()
		{
			ThrottlePolicy policy = new ThrottlePolicy( null, perMinute: 10 )
			{
				ClientThrottling = true,
				ClientRules = new Dictionary<string, RateLimits> { { "anon", new RateLimits { PerMinute = 1 } } }
			};
			ThrottlingFilter filter = CreateFilter( policy );

			Assert.True( IsBlocked( SendMany( filter, 2, () => new TestRequestOptions() ) ) );
		}

		[Fact]
		public void EndpointWhitelist_IsNeverThrottled()
		{
			ThrottlePolicy policy = new ThrottlePolicy( null, perMinute: 1 )
			{
				EndpointThrottling = true,
				EndpointWhitelist = new List<string> { "/home/contact" }
			};
			ThrottlingFilter filter = CreateFilter( policy );

			Assert.False( IsBlocked( SendMany( filter, 5, () => new TestRequestOptions { PathAndQuery = "/Home/Contact" } ) ) );
		}

		[Fact]
		public void EndpointThrottling_CountsEachEndpointSeparately()
		{
			ThrottlePolicy policy = new ThrottlePolicy( null, perMinute: 1 ) { EndpointThrottling = true };
			ThrottlingFilter filter = CreateFilter( policy );
			Send( filter, new TestRequestOptions { PathAndQuery = "/Home/About" } );

			Assert.False( IsBlocked( Send( filter, new TestRequestOptions { PathAndQuery = "/Home/Index" } ) ) );
			Assert.True( IsBlocked( Send( filter, new TestRequestOptions { PathAndQuery = "/home/about" } ) ) );
		}

		[Fact]
		public void EndpointRules_UseLowestMatchingLimit()
		{
			ThrottlePolicy policy = new ThrottlePolicy( null, perMinute: 10 )
			{
				EndpointThrottling = true,
				EndpointRules = new Dictionary<string, RateLimits>
				{
					{ "/home", new RateLimits { PerMinute = 5 } },
					{ "/home/about", new RateLimits { PerMinute = 2 } }
				}
			};
			ThrottlingFilter filter = CreateFilter( policy );

			Assert.False( IsBlocked( SendMany( filter, 2, () => new TestRequestOptions() ) ) );
			Assert.True( IsBlocked( Send( filter ) ) );
		}

		[Fact]
		public void UserAgentWhitelist_IsNeverThrottled()
		{
			ThrottlePolicy policy = new ThrottlePolicy( null, perMinute: 1 )
			{
				UserAgentThrottling = true,
				UserAgentWhitelist = new List<string> { "googlebot" }
			};
			ThrottlingFilter filter = CreateFilter( policy );

			Assert.False( IsBlocked( SendMany( filter, 5, () => new TestRequestOptions { UserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1)" } ) ) );
		}

		[Fact]
		public void UserAgentRules_OverrideDefaultLimit()
		{
			ThrottlePolicy policy = new ThrottlePolicy( null, perMinute: 10 )
			{
				UserAgentThrottling = true,
				UserAgentRules = new Dictionary<string, RateLimits> { { "Facebot", new RateLimits { PerMinute = 1 } } }
			};
			ThrottlingFilter filter = CreateFilter( policy );

			Assert.True( IsBlocked( SendMany( filter, 2, () => new TestRequestOptions { UserAgent = "Facebot/1.0" } ) ) );
			Assert.False( IsBlocked( SendMany( filter, 2, () => new TestRequestOptions { UserAgent = "Mozilla/5.0" } ) ) );
		}

		[Fact]
		public void BlockedRequest_IsLogged()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			Send( filter );
			Assert.Empty( _logger.Entries );

			TestRequest blocked = Send( filter );

			ThrottleLogEntry entry = Assert.Single( _logger.Entries );
			Assert.Equal( "1.2.3.4", entry.ClientIp );
			Assert.Equal( "anon", entry.ClientKey );
			Assert.Equal( "/home/about", entry.Endpoint );
			Assert.Equal( "Mozilla/5.0", entry.UserAgent );
			Assert.Equal( 1, entry.RateLimit );
			Assert.Equal( "Minute", entry.RateLimitPeriod );
			Assert.Equal( 2, entry.TotalRequests );
			Assert.False( string.IsNullOrEmpty( entry.RequestId ) );
			Assert.Same( blocked.Request, entry.Request );
		}

		[Fact]
		public void CustomMessageAndResponseCode_AreUsed()
		{
			ThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			filter.QuotaExceededMessage = "Limit {0} per {1}";
			filter.QuotaExceededResponseCode = HttpStatusCode.ServiceUnavailable;
			Send( filter );

			TestRequest blocked = Send( filter );

			HttpStatusCodeResult result = Assert.IsType<HttpStatusCodeResult>( blocked.Context.Result );
			Assert.Equal( 503, result.StatusCode );
			Assert.Equal( "Limit 1 per Minute", result.StatusDescription );
			Assert.Equal( 503, blocked.Response.Object.StatusCode );
		}

		[Fact]
		public void QuotaExceededResult_CanBeOverridden()
		{
			CustomResultThrottlingFilter filter = new CustomResultThrottlingFilter
			{
				Policy = PerMinute( 1 ),
				Repository = _repository
			};
			Send( filter );

			TestRequest blocked = Send( filter );

			ContentResult result = Assert.IsType<ContentResult>( blocked.Context.Result );
			Assert.Equal( "custom:HTTP request quota exceeded! maximum admitted 1 per Minute", result.Content );
		}

		[Fact]
		public void ExpiredCounter_IsReset()
		{
			TestableThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			TestRequest first = TestRequestFactory.Create( new TestRequestOptions() );
			string key = filter.ComputeKey( filter.Identify( first.Request ), RateLimitPeriod.Minute );
			_repository.Save( key, new ThrottleCounter { Timestamp = DateTime.UtcNow.AddMinutes( -2 ), TotalRequests = 100 }, TimeSpan.FromMinutes( 1 ) );

			filter.OnActionExecuting( first.Context );

			Assert.False( IsBlocked( first ) );
			Assert.Equal( 1, _repository.FirstOrDefault( key ).Value.TotalRequests );
		}

		[Fact]
		public void MultiplePeriods_ShorterPeriodBlocksFirst()
		{
			ThrottlingFilter filter = CreateFilter( new ThrottlePolicy( null, perMinute: 2, perHour: 100 ) { IpThrottling = true } );

			TestRequest blocked = SendMany( filter, 3, () => new TestRequestOptions() );

			Assert.True( IsBlocked( blocked ) );
			Assert.Equal( "Minute", Assert.Single( _logger.Entries ).RateLimitPeriod );
		}

		[Theory]
		[InlineData( EndpointThrottlingType.AbsolutePath, "/home/about" )]
		[InlineData( EndpointThrottlingType.PathAndQuery, "/home/about?id=1" )]
		[InlineData( EndpointThrottlingType.ControllerAndAction, "home/about" )]
		[InlineData( EndpointThrottlingType.Controller, "home" )]
		public void SetIdentity_EndpointFollowsEndpointType( EndpointThrottlingType type, string expected )
		{
			ThrottlePolicy policy = PerMinute( 1 );
			policy.EndpointType = type;
			TestableThrottlingFilter filter = CreateFilter( policy );
			TestRequest request = TestRequestFactory.Create( new TestRequestOptions { PathAndQuery = "/Home/About?id=1" } );

			RequestIdentity identity = filter.Identify( request.Request );

			Assert.Equal( expected, identity.Endpoint );
		}

		[Theory]
		[InlineData( true, "auth" )]
		[InlineData( false, "anon" )]
		public void SetIdentity_ClientKeyReflectsAuthentication( bool isAuthenticated, string expected )
		{
			TestableThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			TestRequest request = TestRequestFactory.Create( new TestRequestOptions { IsAuthenticated = isAuthenticated } );

			Assert.Equal( expected, filter.Identify( request.Request ).ClientKey );
		}

		[Fact]
		public void SetIdentity_UsesIpAddressParser()
		{
			TestableThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			TestRequest request = TestRequestFactory.Create( new TestRequestOptions { ClientIp = "10.0.0.1", ForwardedFor = "9.9.9.9" } );

			Assert.Equal( "9.9.9.9", filter.Identify( request.Request ).ClientIp );
		}

		[Fact]
		public void ComputeThrottleKey_IsStableSha1Hex()
		{
			TestableThrottlingFilter filter = CreateFilter( PerMinute( 1 ) );
			RequestIdentity identity = new RequestIdentity { ClientIp = "1.2.3.4", ClientKey = "anon", Endpoint = "/home", UserAgent = "ua" };

			string key = filter.ComputeKey( identity, RateLimitPeriod.Minute );

			Assert.Matches( "^[0-9A-F]{40}$", key );
			Assert.Equal( key, filter.ComputeKey( identity, RateLimitPeriod.Minute ) );
			Assert.NotEqual( key, filter.ComputeKey( identity, RateLimitPeriod.Hour ) );
		}

		[Fact]
		public void ComputeThrottleKey_OnlyIncludesEnabledDimensions()
		{
			TestableThrottlingFilter filter = CreateFilter( new ThrottlePolicy( null, perMinute: 1 ) { EndpointThrottling = true } );
			RequestIdentity a = new RequestIdentity { ClientIp = "1.1.1.1", ClientKey = "anon", Endpoint = "/home", UserAgent = "a" };
			RequestIdentity b = new RequestIdentity { ClientIp = "2.2.2.2", ClientKey = "auth", Endpoint = "/home", UserAgent = "b" };
			RequestIdentity c = new RequestIdentity { ClientIp = "1.1.1.1", ClientKey = "anon", Endpoint = "/other", UserAgent = "a" };

			Assert.Equal( filter.ComputeKey( a, RateLimitPeriod.Minute ), filter.ComputeKey( b, RateLimitPeriod.Minute ) );
			Assert.NotEqual( filter.ComputeKey( a, RateLimitPeriod.Minute ), filter.ComputeKey( c, RateLimitPeriod.Minute ) );
		}

		[Fact]
		public void Constructor_SetsDefaults()
		{
			ThrottlingFilter filter = new ThrottlingFilter();

			Assert.Equal( ( HttpStatusCode )429, filter.QuotaExceededResponseCode );
			Assert.IsType<CacheRepository>( filter.Repository );
			Assert.IsType<IpAddressParser>( filter.IpAddressParser );
			Assert.Null( filter.Policy );
			Assert.Null( filter.Logger );
		}
	}
}