using Xunit;

namespace MvcThrottle2.Tests
{
	public class RateLimitTests
	{
		[Theory]
		[InlineData( RateLimitPeriod.Second, 1 )]
		[InlineData( RateLimitPeriod.Minute, 2 )]
		[InlineData( RateLimitPeriod.Hour, 3 )]
		[InlineData( RateLimitPeriod.Day, 4 )]
		[InlineData( RateLimitPeriod.Week, 5 )]
		public void RateLimits_GetLimit_ReturnsValueForPeriod( RateLimitPeriod period, long expected )
		{
			RateLimits limits = new RateLimits { PerSecond = 1, PerMinute = 2, PerHour = 3, PerDay = 4, PerWeek = 5 };

			Assert.Equal( expected, limits.GetLimit( period ) );
		}

		[Fact]
		public void RateLimits_GetLimit_UnknownPeriod_ReturnsPerSecond()
		{
			RateLimits limits = new RateLimits { PerSecond = 7, PerMinute = 2 };

			Assert.Equal( 7, limits.GetLimit( ( RateLimitPeriod )99 ) );
		}

		[Fact]
		public void RateLimits_Defaults_AreZero()
		{
			RateLimits limits = new RateLimits();

			Assert.Equal( 0, limits.GetLimit( RateLimitPeriod.Minute ) );
		}

		[Theory]
		[InlineData( RateLimitPeriod.Second, 1 )]
		[InlineData( RateLimitPeriod.Minute, 2 )]
		[InlineData( RateLimitPeriod.Hour, 3 )]
		[InlineData( RateLimitPeriod.Day, 4 )]
		[InlineData( RateLimitPeriod.Week, 5 )]
		public void EnableThrottlingAttribute_GetLimit_ReturnsValueForPeriod( RateLimitPeriod period, long expected )
		{
			EnableThrottlingAttribute attribute = new EnableThrottlingAttribute { PerSecond = 1, PerMinute = 2, PerHour = 3, PerDay = 4, PerWeek = 5 };

			Assert.Equal( expected, attribute.GetLimit( period ) );
		}

		[Fact]
		public void RequestIdentity_ToString_JoinsAllParts()
		{
			RequestIdentity identity = new RequestIdentity
			{
				ClientIp = "1.2.3.4",
				ClientKey = "anon",
				Endpoint = "/home/about",
				UserAgent = "Mozilla"
			};

			Assert.Equal( "1.2.3.4_anon_/home/about_Mozilla", identity.ToString() );
		}

		[Fact]
		public void ThrottlePolicy_DefaultEndpointType_IsAbsolutePath()
		{
			ThrottlePolicy policy = new ThrottlePolicy( 1 );

			Assert.Equal( EndpointThrottlingType.AbsolutePath, policy.EndpointType );
		}
	}
}