using System;
using System.Web;
using System.Diagnostics;

using BasicRouter;

namespace TestServer
{
	public class SimpleHandler : IHttpHandler
	{
		/// <summary>
		/// Router instance. Making it static
		/// is not required, but recommended;
		/// </summary>
		private static SimpleRouter router = null;

		#region IHttpHandler Members
		public bool IsReusable
		{
			// NOTE: It is recommended to be true.
			get { return true; }
		}

		public void ProcessRequest(HttpContext ctx)
		{
			if (!router.InvokeAction(ctx)) // If failed to map the request to controller/action;
				router.InvokeAction(ctx, "error", "details"); // Forward to our error handler;
		}
		#endregion

		/// <summary>
		/// Default Constructor.
		/// </summary>
		public SimpleHandler()
		{
			if (router == null)
			{
				// Initialize routing according to the handler's re-usability:
				router = new SimpleRouter(IsReusable);

				// Adding namespaces where our controller classes reside:
				// - add null or "", if you have controllers in the root.
				// - also specify which assembly, if it is not this one.
				router.AddNamespace("TestServer.Controllers");

				// OPTIONAL: Setting exception handler for any action call:
				router.OnActionException += new SimpleRouter.ActionExceptionHandler(OnActionException);
			}
		}

		/// <summary>
		/// Handles exceptions thrown by any action method.
		/// </summary>
		/// <example>
		/// /simple/exception?msg=Ops!:)
		/// </example>
		/// <param name="ctx">current http context</param>
		/// <param name="action">fully-qualified action name</param>
		/// <param name="ex">exception that was raised</param>
		private void OnActionException(HttpContext ctx, string action, Exception ex)
		{
			// Here we just write formatted exception details into the response...
			Exception e = ex.InnerException ?? ex;
			StackFrame frame = new StackTrace(e, true).GetFrame(0);

			string source, fileName = frame.GetFileName();
			if(fileName == null)
				source = "Not Available";
			else
				source = String.Format("{0}, <b>Line {1}</b>", fileName, frame.GetFileLineNumber());

			ctx.Response.Write(String.Format("<h3>Exception was raised while calling an action</h3><ul><li><b>Action:</b> {0}</li><li><b>Source:</b> {1}</li><li><b>Message:</b> <span style=\"color:Red;\">{2}</span></li></ul>", action, source, e.Message));
		}

	}
}
