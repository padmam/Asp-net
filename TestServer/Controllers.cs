using System;
using System.IO;
using BasicRouter;

namespace TestServer.Controllers
{
	/// <summary>
	/// Simplest controller example.
	/// </summary>
	public class SimpleController : BaseController
	{
		/// <example>
		/// /simple/time
		/// </example>
		public void Time()
		{
			Write(DateTime.Now.ToString("MMM dd, yyyy; HH:mm:ss.fff"));
		}

		/// <example>
		/// /simple/birthday?name=John&age=25
		/// </example>
		public void Birthday(string name, int age)
		{
			Write(String.Format("<h1>Happy {0}, dear {1}! ;)</h1>", age, name));
		}
		
		/// <example>
		/// /simple/exception?msg=exception message demo
		/// </example>
		public void Exception(string msg)
		{
			throw new Exception(msg);
		}

		/// <example>
		/// /one/two/three/simple/prefix
		/// - prefix will be {"one", "two", "three"}
		/// </example>
		public void Prefix()
		{
			string s = String.Format("{0} segments in the request prefix:<ol>", prefix.Length);
			foreach (string p in prefix)
				s += String.Format("<li>{0}</li>", p);
			Write(s + "</ol>");
		}
	}

	/// <summary>
	/// Demonstrates use of arrays and default parameters.
	/// </summary>
	public class ListController : BaseController
	{
		/// <example>
		/// /list/sum?values=1,2,3,4,5
		/// </example>
		public void Sum(int [] values)
		{
			int total = 0;
			string s = "";
			foreach (int i in values)
			{
				if (!string.IsNullOrEmpty(s))
					s += " + ";
				s += i.ToString();
				total += i;
			}
			s += " = " + total.ToString();
			Write(s);
		}

		/// <summary>
		/// Outputs the sum of all double values, with optional description.
		/// </summary>
		/// <example>
		/// /list/add?values=1.05,2.17,...[&units=dollars]
		/// </example>
		public void Add(double [] values, string units = null)
		{
			double total = 0;
			foreach (double d in values)
				total += d;
			Write(String.Format("Total: {0} {1}", total, units));
		}

		/// <summary>
		/// Spits out the array of passed strings into a paragraph,
		/// optionally changing the color.
		/// </summary>
		/// <example>
		/// /list/text?values=one,two,three,...[&color=Red]
		/// </example>
		public void Text(string[] values, string color = "Green")
		{
			string result = String.Format("<p style=\"color:{0};\">", color);
			foreach(string s in values)
				result += s + "<br/>";
			result += "</p>";
			Write(result);
		}

		/// <summary>
		/// Shows that we can pass an array of mixed types.
		/// </summary>
		/// <example>
		/// /list/any?values=1,two,-3.45
		/// </example>
		public void Any(object[] values, string desc = null)
		{
			string s = (desc ?? "") + "<ol>";
			foreach (object obj in values)
				s += "<li>" + obj.ToString() + "</li>";
			Write(s + "</ol>");
		}
	}

	/// <summary>
	/// Shows how to quickly and efficiently render an image file.
	/// </summary>
	public class ImageController : BaseController
	{
		/// <summary>
		/// Returns a cached image.
		/// </summary>
		public void Diagram()
		{
			if (image == null)
				image = FileToByteArray(ctx.Server.MapPath("~/Routing.jpg"));

			ctx.Response.ContentType = "image/jpeg";
			ctx.Response.BinaryWrite(image);
		}

		/// <summary>
		/// Simplest and quickest way for reading entire file,
		/// and returning its content as array of bytes.
		/// </summary>
		private static byte[] FileToByteArray(string fileName)
		{
			FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
			long nBytes = new FileInfo(fileName).Length;
			return new BinaryReader(fs).ReadBytes((int)nBytes);
		}

		private byte[] image = null; // Cached image;
	}

	/// <summary>
	/// Controller for error handling;
	/// </summary>
	public class ErrorController:BaseController
	{
		/// <summary>
		/// Shows formatted details about the request.
		/// </summary>
		public void Details()
		{
			string path = GetQueryPath();
			if (string.IsNullOrEmpty(path))
				path = "<span style=\"color:Red;\">Empty</span>";

			string msg = String.Format("<p>Failed to process request: <b>{0}</b></p>", path);
			msg += "<p>Passed Parameters:";
			if (ctx.Request.QueryString.Count > 0)
			{
				msg += "</p><ol>";
				foreach (string s in ctx.Request.QueryString)
					msg += String.Format("<li>{0} = {1}</li>", s, ctx.Request.QueryString[s]);
				msg += "</ol>";
			}
			else
				msg += " <b>None</b></p>";

			Write(msg);
		}
	}
}
