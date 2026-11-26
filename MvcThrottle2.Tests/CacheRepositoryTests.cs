using System;
using System.IO;
using System.Web;
using Xunit;

namespace MvcThrottle2.Tests
{
	/// <summary>
	/// <see cref="CacheRepository"/> reads <see cref="HttpContext.Current"/>, so each test sets up a minimal context.
	/// The ASP.NET cache is process-wide, so tests use unique keys.
	/// </summary>
	public class CacheRepositoryTests
	{
		private readonly CacheRepository _repository = new CacheRepository();

		public CacheRepositoryTests()
		{
			HttpContext.Current = new HttpContext( new HttpRequest( "", "http://localhost/", "" ), new HttpResponse( TextWriter.Null ) );
		}

		private static string NewKey()
		{
			return "test_" + Guid.NewGuid().ToString( "N" );
		}

		[Fact]
		public void Save_ThenFirstOrDefault_ReturnsCounter()
		{
			string key = NewKey();
			ThrottleCounter counter = new ThrottleCounter { Timestamp = DateTime.UtcNow, TotalRequests = 3 };

			_repository.Save( key, counter, TimeSpan.FromMinutes( 1 ) );

			ThrottleCounter? stored = _repository.FirstOrDefault( key );
			Assert.True( stored.HasValue );
			Assert.Equal( counter.Timestamp, stored.Value.Timestamp );
			Assert.Equal( 3, stored.Value.TotalRequests );
		}

		[Fact]
		public void Save_ExistingKey_UpdatesCounter()
		{
			string key = NewKey();
			_repository.Save( key, new ThrottleCounter { TotalRequests = 1 }, TimeSpan.FromMinutes( 1 ) );

			_repository.Save( key, new ThrottleCounter { TotalRequests = 2 }, TimeSpan.FromMinutes( 1 ) );

			Assert.Equal( 2, _repository.FirstOrDefault( key ).Value.TotalRequests );
		}

		[Fact]
		public void FirstOrDefault_MissingKey_ReturnsNull()
		{
			Assert.Null( _repository.FirstOrDefault( NewKey() ) );
		}

		[Fact]
		public void Any_ReflectsWhetherKeyExists()
		{
			string key = NewKey();
			Assert.False( _repository.Any( key ) );

			_repository.Save( key, new ThrottleCounter(), TimeSpan.FromMinutes( 1 ) );

			Assert.True( _repository.Any( key ) );
		}

		[Fact]
		public void Remove_DeletesCounter()
		{
			string key = NewKey();
			_repository.Save( key, new ThrottleCounter(), TimeSpan.FromMinutes( 1 ) );

			_repository.Remove( key );

			Assert.False( _repository.Any( key ) );
		}

		[Fact]
		public void Clear_RemovesCountersButKeepsOtherCacheItems()
		{
			string counterKey = NewKey();
			string otherKey = NewKey();
			_repository.Save( counterKey, new ThrottleCounter(), TimeSpan.FromMinutes( 1 ) );
			HttpContext.Current.Cache.Insert( otherKey, "not a counter" );

			_repository.Clear();

			Assert.False( _repository.Any( counterKey ) );
			Assert.Equal( "not a counter", HttpContext.Current.Cache[otherKey] );
			HttpContext.Current.Cache.Remove( otherKey );
		}
	}
}