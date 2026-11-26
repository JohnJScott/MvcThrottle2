using Moq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace MvcThrottle2.Tests
{
	/// <summary>
	/// In-memory <see cref="IThrottleRepository"/> so filter tests don't depend on <see cref="HttpContext.Current"/>.
	/// </summary>
	internal sealed class InMemoryThrottleRepository : IThrottleRepository
	{
		private readonly Dictionary<string, ThrottleCounter> _store = new Dictionary<string, ThrottleCounter>();

		public bool Any( string id )
		{
			return _store.ContainsKey( id );
		}

		public ThrottleCounter? FirstOrDefault( string id )
		{
			ThrottleCounter counter;
			if( _store.TryGetValue( id, out counter ) )
			{
				return counter;
			}

			return null;
		}

		public void Save( string id, ThrottleCounter throttleCounter, TimeSpan expirationTime )
		{
			_store[id] = throttleCounter;
		}

		public void Remove( string id )
		{
			_store.Remove( id );
		}

		public void Clear()
		{
			_store.Clear();
		}
	}

	/// <summary>
	/// Records every entry passed to <see cref="Log"/>.
	/// </summary>
	internal sealed class RecordingThrottleLogger : IThrottleLogger
	{
		public List<ThrottleLogEntry> Entries { get; } = new List<ThrottleLogEntry>();

		public void Log( ThrottleLogEntry entry )
		{
			Entries.Add( entry );
		}
	}

	/// <summary>
	/// Describes a fake request sent through the filter.
	/// </summary>
	internal sealed class TestRequestOptions
	{
		public string ClientIp { get; set; } = "1.2.3.4";
		public string ForwardedFor { get; set; }
		public bool IsAuthenticated { get; set; }
		public string PathAndQuery { get; set; } = "/Home/About";
		public string Controller { get; set; } = "Home";
		public string Action { get; set; } = "About";
		public string UserAgent { get; set; } = "Mozilla/5.0";
		public List<object> ControllerAttributes { get; } = new List<object>();
		public List<object> ActionAttributes { get; } = new List<object> { new EnableThrottlingAttribute() };
	}

	/// <summary>
	/// A fake request: the filter context plus the response pieces tests assert on.
	/// </summary>
	internal sealed class TestRequest
	{
		public ActionExecutingContext Context { get; set; }
		public Mock<HttpResponseBase> Response { get; set; }
		public NameValueCollection ResponseHeaders { get; set; }
		public HttpRequestBase Request { get; set; }
	}

	internal static class TestRequestFactory
	{
		public static TestRequest Create( TestRequestOptions options )
		{
			NameValueCollection serverVariables = new NameValueCollection();
			if( options.ForwardedFor != null )
			{
				serverVariables["HTTP_X_FORWARDED_FOR"] = options.ForwardedFor;
			}

			Mock<HttpRequestBase> request = new Mock<HttpRequestBase>();
			request.Setup( r => r.UserHostAddress ).Returns( options.ClientIp );
			request.Setup( r => r.ServerVariables ).Returns( serverVariables );
			request.Setup( r => r.IsAuthenticated ).Returns( options.IsAuthenticated );
			request.Setup( r => r.Url ).Returns( new Uri( "http://localhost" + options.PathAndQuery ) );
			request.Setup( r => r.UserAgent ).Returns( options.UserAgent );

			NameValueCollection responseHeaders = new NameValueCollection();
			Mock<HttpResponseBase> response = new Mock<HttpResponseBase>();
			response.SetupProperty( r => r.StatusCode, 200 );
			response.Setup( r => r.Headers ).Returns( responseHeaders );

			Mock<HttpContextBase> httpContext = new Mock<HttpContextBase>();
			httpContext.Setup( c => c.Request ).Returns( request.Object );
			httpContext.Setup( c => c.Response ).Returns( response.Object );

			RouteData routeData = new RouteData();
			routeData.Values["controller"] = options.Controller;
			routeData.Values["action"] = options.Action;
			RequestContext requestContext = new RequestContext( httpContext.Object, routeData );
			request.Setup( r => r.RequestContext ).Returns( requestContext );

			Mock<ControllerDescriptor> controllerDescriptor = new Mock<ControllerDescriptor>();
			controllerDescriptor
				.Setup( d => d.IsDefined( It.IsAny<Type>(), It.IsAny<bool>() ) )
				.Returns( ( Type type, bool inherit ) => options.ControllerAttributes.Any( a => type.IsInstanceOfType( a ) ) );
			controllerDescriptor
				.Setup( d => d.GetCustomAttributes( It.IsAny<Type>(), It.IsAny<bool>() ) )
				.Returns( ( Type type, bool inherit ) => options.ControllerAttributes.Where( a => type.IsInstanceOfType( a ) ).ToArray() );

			Mock<ActionDescriptor> actionDescriptor = new Mock<ActionDescriptor>();
			actionDescriptor.Setup( d => d.ControllerDescriptor ).Returns( controllerDescriptor.Object );
			actionDescriptor
				.Setup( d => d.IsDefined( It.IsAny<Type>(), It.IsAny<bool>() ) )
				.Returns( ( Type type, bool inherit ) => options.ActionAttributes.Any( a => type.IsInstanceOfType( a ) ) );
			actionDescriptor
				.Setup( d => d.GetCustomAttributes( It.IsAny<Type>(), It.IsAny<bool>() ) )
				.Returns( ( Type type, bool inherit ) => options.ActionAttributes.Where( a => type.IsInstanceOfType( a ) ).ToArray() );

			ControllerContext controllerContext = new ControllerContext( requestContext, new Mock<ControllerBase>().Object );
			ActionExecutingContext context = new ActionExecutingContext( controllerContext, actionDescriptor.Object, new Dictionary<string, object>() );

			return new TestRequest
			{
				Context = context,
				Response = response,
				ResponseHeaders = responseHeaders,
				Request = request.Object
			};
		}
	}
}