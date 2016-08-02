using System;
using System.Web;
using System.Reflection;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Timers;

namespace BasicRouter
{
	/// <summary>
	/// Base class for any controller.
	/// </summary>
	public class BaseController
	{
		/// <summary>
		/// Current HTTP Context.
		/// </summary>
		protected HttpContext ctx { get; set; }

		/// <summary>
		/// List of prefix segments from the request URL, if available.
		/// </summary>
		/// <example>
		/// 1. For request {host.com/controller/action} there will be
		///    no prefix segments;
		/// 2. For request {host.com/one/two/controller/action} prefix
		///    segments will be {"one", "two"};
		/// 3. For request {host.com/virt/one/two/controller/action},
		///    where {virt} is the virtual folder, prefix segments
		///    will still be {"one", "two"}, because virtual folders
		///    and their sub-folders are skipped.
		/// </example>
		/// <remarks>
		/// 1. Prefix segments are not directly used by this library, it only
		///    uses the last two segments to identify them with controller/action.
		///    Your controller, however, may choose to use them as additional
		///    piece of information, like mapping those to a database record.
		/// 2. Prefix segments play an important role when implementing
		///    support for custom namespaces + assemblies. For details see
		///    method SimpleRouter.OnValidatePrefix.
		/// 3. Segments are passed converted to lowercase.
		/// </remarks>
		protected string[] prefix { get; set; }

		/// <summary>
		/// Simple helper for writing text into the Response.
		/// </summary>
		/// <param name="txt">Text to be written</param>
		protected void Write(string txt)
		{
			ctx.Response.Write(txt);
		}

		/// <summary>
		/// Returns url string of all request segments.
		/// </summary>
		/// <remarks>
		/// 1. Segments of the virtual folders are skipped.
		/// 2. The url string is returned in lowercase.
		/// </remarks>
		/// <returns>request segments</returns>
		protected string GetQueryPath()
		{
			// Below is the only way to retrieve complete list of request
			// segments that excludes the list of virtual folders...
			return ctx.Request.AppRelativeCurrentExecutionFilePath.Replace("~/", "").ToLower();
		}
	}

	/// <summary>
	/// Implements simple routing for URL request into controller/action.
	/// </summary>
	public class SimpleRouter
	{
		/// <summary>
		/// Separator for splitting values for an action parameter of array type.
		/// </summary>
		/// <remarks>
		/// It is an ongoing debate in the internet, whether using a comma in URL
		/// for passing a list of values is a good idea. However, this only matters
		/// in a unique scenario, when you need to pass an array of strings, where
		/// each may contain a comma. In this case your options are:
		///  a. Set this separator to a different, unique in your case symbol;
		///  b. Replace comma before passing values, and revert it in the action;
		///  c. Pass array of strings in an alternative format, so it can be passed
		///     as just one string, and then split it within the action.
		///  The bottom line, in at least 9 out of 10 cases you won't need to care
		///  about it, and using comma is the most untiutive way to pass an array.
		/// </remarks>
		public char arraySeparator = ',';

		/// <summary>
		/// Indicates whether controller classes are to be reused.
		/// </summary>
		/// <remarks>
		/// It is recommended to be set to true, or else it will
		/// impact the performance.
		/// </remarks>
		public bool ReuseControllers
		{
			get
			{
				lock (safe)
					return _reuseControllers;
			}
			set
			{
				lock (safe)
					_reuseControllers = value;
			}
		}

		/// <summary>
		/// Default Constructor.
		/// </summary>
		public SimpleRouter(bool reuseControllers = true)
		{
			ReuseControllers = reuseControllers;
		}

		/// <summary>
		/// Adds namespace from the specified assembly.
		/// </summary>
		/// <remarks>
		/// - Namespaces will be checked for controllers in the same order
		///   in which they are added;
		/// - Empty or null namespace will cause search in the root (no-namespace).
		/// </remarks>
		/// <param name="ns">name of the namespace</param>
		/// <param name="asm">assembly object</param>
		public void AddNamespace(string ns, Assembly asm)
		{
			namespaces.Add(new Namespace(ns, asm));
		}

		/// <summary>
		/// Adds namespace from assembly specified by its name.
		/// </summary>
		/// <remarks>
		/// Throws an exception, if the assembly could not be loaded.
		/// </remarks>
		/// <param name="ns">name of the namespace</param>
		/// <param name="asm">assembly's name/path</param>
		public void AddNamespace(string ns, string asm)
		{
			namespaces.Add(new Namespace(ns, asm));
		}

		/// <summary>
		/// Adds namespace from the calling assembly.
		/// </summary>
		/// <param name="ns">name of the namespace</param>
		public void AddNamespace(string ns)
		{
			namespaces.Add(new Namespace(ns, Assembly.GetCallingAssembly()));
		}

		/// <summary>
		/// Initiates support for time-outs, to release unused controllers.
		/// </summary>
		/// <remarks>
		/// 1. In most cases this functionality is not needed, and thus switched off.
		///    But when it is needed, it allows release of long-unused controllers.
		///    Any controller that hasn't been used for the specified number of seconds
		///    will be released to be disposed of by the garbage collector.
		/// 2. Passing 0 will switch the verification off.
		/// 3. Verification executes every 1/10-th of the time-out interval.
		/// </remarks>
		/// <param name="life">lifespan for unused controllers, in seconds.</param>
		public void SetTimeout(ushort life)
		{
			TimeOut = life;
			if (TimeOut == 0)
			{
				// If it is ON, switch it OFF.
				if (timer != null)
				{
					timer.Stop();
					timer = null;
				}
				return;
			}
			double frequency = TimeOut * 100; // Check every 1/10-th of the time-out;
			if (timer == null)
			{
				timer = new Timer(frequency);
				timer.Elapsed += new ElapsedEventHandler(OnCheckExpiration);
				timer.Start();
			}
			else
				timer.Interval = frequency;
		}

		/// <summary>
		/// Attempts to map the request to {controller}/{action}[parameters]
		/// and, if found - invokes the target action.
		/// </summary>
		/// <param name="ctx">Current HTTP Context</param>
		/// <returns>True, if action was located and invoked.</returns>
		public bool InvokeAction(HttpContext ctx)
		{
			string[] segments = GetSegments(ctx.Request);
			if (segments == null)
				return false; // {controller/action} segments not present;

			int nps = segments.Length - 2; // Number of prefix segments;

			// Extracting {controller/action} segments:
			string controller = segments[nps];
			string action = segments[nps + 1];

			// Copying prefix segments, if available:
			string[] prefix = new string[nps];
			Array.Copy(segments, prefix, nps);

			return InvokeActionInternal(ctx, controller, action, null, prefix);
		}

		/// <summary>
		/// Attempts to locate and invoke target controller/action by their
		/// explicit names.
		/// </summary>
		/// <param name="ctx">Current HTTP Context</param>
		/// <param name="controller">Name of the controller class.</param>
		/// <param name="action">Name of the action method.</param>
		/// <param name="parameters">Parameters for the action method. If null is passed, the parameters will be initialized according to the action signature.</param>
		/// <param name="prefix">Prefix segment parameters. If null is passed, the values will be extracted from the request URL.</param>
		/// <returns>True, if action was located and invoked.</returns>
		public bool InvokeAction(HttpContext ctx, string controller, string action, object[] parameters = null, string[] prefix = null)
		{
			string ctrl = CleanSegment(controller);
			string act = CleanSegment(action);

			if (string.IsNullOrEmpty(ctrl) || string.IsNullOrEmpty(act))
				return false; // Cannot process empty controller or action;

			return InvokeActionInternal(ctx, ctrl, act, parameters, prefix);
		}

		/// <summary>
		/// Validates the list of prefix segments passed in the request.
		/// </summary>
		/// <remarks>
		/// Overriding this method in a derived class gives you complete
		/// freedom in how you want prefix segments handled - control
		/// their number, length, content or syntax; and to change the
		/// response according to your own rules.
		/// 
		/// Segments are passed in lowercase and without spaces.
		/// However, the derived class can change them to anything.
		/// </remarks>
		/// <param name="prefix">list of prefix segments</param>
		/// <param name="nsOverride">optional namespace override</param>
		/// <returns>true, if prefix segments are valid</returns>
		protected virtual bool OnValidatePrefix(string[] prefix, ref Namespace nsOverride)
		{
			// 1. If a derived class decides it does not like what's in the prefix,
			//    it will return false, which in turn will fail method InvokeAction,
			//    making it also return false.
			// 2. A derived class can change the passed list of prefixes to anything it may
			//    like, in which exact way they will be made available to controllers.
			// 3. A derived class can override the namespace in which controller
			//    is to be sought (depending on the prefix content), by setting
			//    its own Namespace object to parameter nsOverride.

			// Returning true by default, meaning we do not care at all,
			// whether prefix segments are even present or not.
			return true;
		}

		/// <summary>
		/// Core implementation of locating and invoking an action.
		/// </summary>
		/// <param name="ctx">current HTTP request object</param>
		/// <param name="controller">controller name</param>
		/// <param name="action">action name</param>
		/// <param name="parameters">action parameters</param>
		/// <param name="prefix">prefix segments</param>
		/// <returns>true, if action was located and invoked</returns>
		private bool InvokeActionInternal(HttpContext ctx, string controller, string action, object[] parameters, string[] prefix)
		{
			string[] pref = prefix;
			if (pref == null)
			{
				string[] segments = GetSegments(ctx.Request);
				if (segments == null)
					pref = new string[0];
				else
				{
					int n = segments.Length - 2; // Number of prefix segments;
					pref = new string[n];
					Array.Copy(segments, pref, n);
				}
			}

			Namespace nsOverride = null;
			if (!OnValidatePrefix(pref, ref nsOverride))
				return false; // Prefix segment validation failed;

			Type t = FindControllerType(controller, nsOverride); // Locating the controller's Type;
			if (t == null)
				return false; // Controller's Type not found;

			// Locating the action method (only public, non-static methods within the class itself, ignoring the name case)
			MethodInfo info = t.GetMethod(action, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.InvokeMethod);
			if (info == null)
				return false; // Action not found;
			
			// Preparing collection of parameters for the action...
			object[] prm = parameters;
			if (prm == null)
			{
				prm = PrepareActionParameters(info.GetParameters(), ctx.Request.QueryString);
				if (prm == null)
					return false; // Failed to get the expected parameters;
			}

			string signature = t.Module.Name + "/" + t.FullName; // Unique signature of assembly+namespace+controller;

			CacheHolder ch = GetFromCache(signature); // Try getting details from cache;
			if (ch == null)
				ch = new CacheHolder();

			if (ch.instance == null)
				ch.instance = Activator.CreateInstance(t); // Creating new instance of the controller class;

			ch.lastUse = DateTime.Now; // last use of the controller;

			// Updating protected properties in BaseController indirectly...
			BindingFlags flags = BindingFlags.SetProperty | BindingFlags.NonPublic | BindingFlags.Instance;
			t.InvokeMember("ctx", flags, null, ch.instance, new[] { ctx });
			t.InvokeMember("prefix", flags, null, ch.instance, new[] { pref });

			try
			{
				info.Invoke(ch.instance, prm); // Invoking the target action;
			}
			catch (Exception ex)
			{
				if (OnActionException != null)
				{
					string a = info.ReflectedType.FullName + "." + info.Name; // Fully-qualified name of the action method;
					OnActionException(ctx, a, ex); // Trigger event;
				}
			}

			AddToCache(signature, ch); // Adding details to the cache;

			return true; // Action was located and invoked;
		}

		/// <summary>
		/// Attempts locating controller Type by its class name.
		/// </summary>
		/// <remarks>
		///  - iterates through list of namespaces;
		///  - allows omitting ending "Controller" in the class name;
		///  - the search mode is case-insensitive.
		/// </remarks>
		/// <param name="ctrlName">Conroller Name</param>
		/// <returns>Found controller's Type, or null otherwise</returns>
		private Type FindControllerType(string ctrlName, Namespace nsOverride)
		{
			if (nsOverride != null && nsOverride.IsValid)
				return GetControllerType(ctrlName, nsOverride);

			foreach (Namespace ns in namespaces)
			{
				Type t = GetControllerType(ctrlName, ns);
				if (t != null)
					return t;
			}
			return null;
		}

		/// <summary>
		/// Searches for controller type within one namespace.
		/// </summary>
		/// <param name="ctrlName">controller name</param>
		/// <param name="ns">namespace</param>
		/// <returns>controller type, if found</returns>
		private Type GetControllerType(string ctrlName, Namespace ns)
		{
			Type t = null;
			string name = ctrlName; // start in the root;
			if (!string.IsNullOrEmpty(ns.name))
				name = ns.name + "." + ctrlName; // prepend namespace;

			t = ns.asm.GetType(name, false, true);
			if (t == null)
				t = ns.asm.GetType(name + "Controller", false, true); // try with "Controller" in the end;
			if (t != null && (!t.IsClass || t.BaseType != typeof(BaseController)))
				return null; // Conflict in type declaration;
			return t;
		}

		/// <summary>
		/// Prepares parameters to be passed to an action method.
		/// </summary>
		/// <remarks>
		/// For each action parameter in 'pi' tries to locate corresponding value in 'nvc',
		/// adjusts the type, if needed, pack it all and return as array of objects.
		/// </remarks>
		/// <param name="pi">Parameters of the target action</param>
		/// <param name="nvc">Parameters from the URL request</param>
		/// <returns>Parameters for the target action, or null, if failed</returns>
		private object[] PrepareActionParameters(ParameterInfo[] pi, NameValueCollection nvc)
		{
			List<object> parameters = new List<object>(); // resulting array;
			foreach (ParameterInfo p in pi)
			{
				object obj = nvc.Get(p.Name); // Get URL parameter;
				if (string.IsNullOrEmpty((string)obj))
				{
					if (!p.IsOptional)
						return null; // failed;

					parameters.Add(p.DefaultValue); // Use default value;
					continue;
				}
				if (p.ParameterType != typeof(string))
				{
					// Expected parameter's type isn't just a string;
					// Try to convert the type into the expected one.
					try
					{
						if (p.ParameterType.IsArray)
						{
							// For parameter-array we try splitting values
							// using a separator symbol:
							string[] str = ((string)obj).Split(arraySeparator);
							Type baseType = p.ParameterType.GetElementType();
							Array arr = Array.CreateInstance(baseType, str.Length);
							int idx = 0;
							foreach (string s in str)
								arr.SetValue(Convert.ChangeType(s, baseType), idx++);
							obj = arr;
						}
						else
						{
							Type t = p.ParameterType;
							if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) // If parameter is nullable;
								t = Nullable.GetUnderlyingType(t); // Use the underlying type instead;

							obj = Convert.ChangeType(obj, t); // Convert into expected type;
						}
					}
					catch (Exception)
					{
						// Failed to map passed value(s) to the action parameter,
						// and it is not terribly important why exactly.
						return null; // fail;
					}
				}
				parameters.Add(obj);
			}
			return parameters.ToArray(); // Success, returning the array;
		}

		/// <summary>
		/// Executes on timer to verify which controller hasn't been used
		/// for the specified number of seconds, and releases each such
		/// controller to the garbage collector.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnCheckExpiration(object sender, ElapsedEventArgs e)
		{
			lock (safe)
			{
				if (ctrlCache == null)
					return;
				List<string> expired = new List<string>();
				foreach (string s in ctrlCache.Keys)
				{
					if (DateTime.Now - ctrlCache[s].lastUse > TimeSpan.FromSeconds(TimeOut))
						expired.Add(s);
				}
				foreach (string s in expired)
					ctrlCache.Remove(s);
			}
		}

		/// <summary>
		/// Attempts adding controller/action details to the cache.
		/// </summary>
		/// <param name="key">unique key</param>
		/// <param name="ch">cache object</param>
		/// <returns>true, if added successfully.</returns>
		private bool AddToCache(string key, CacheHolder ch)
		{
			lock (safe)
			{
				if (ReuseControllers)
				{
					if (ctrlCache == null)
						ctrlCache = new Dictionary<string, CacheHolder>();

					if (!ctrlCache.ContainsKey(key))
					{
						ctrlCache.Add(key, ch);
						return true; // success;
					}
				}
			}
			return false; // failed;
		}

		/// <summary>
		/// Attempts retrieving controller/action details from cache.
		/// </summary>
		/// <param name="key">unique key</param>
		/// <returns>cache object, or null, if not found</returns>
		private CacheHolder GetFromCache(string key)
		{
			lock (safe)
			{
				if (ReuseControllers && ctrlCache != null && ctrlCache.ContainsKey(key))
					return ctrlCache[key];
			}
			return null;
		}

		/// <summary>
		/// Extracts complete list of query segments from the request,
		/// while skipping the list of possible virtual folders.
		/// </summary>
		/// <remarks>
		/// Segments are returned in lowesrcase and stripped of white spaces.
		/// </remarks>
		/// <param name="request">request object</param>
		/// <returns>true, if at least two segments found</returns>
		private static string[] GetSegments(HttpRequest request)
		{
			// Below is the only way to retrieve complete list of request
			// segments that excludes the list of virtual folders...
			string[] segments = request.AppRelativeCurrentExecutionFilePath.Replace("~/", "").Split('/');
			
			if (segments.Length < 2)
				return null; // We require at least two segments for controller/action;

			// Remove white spaces and set to lowercase;
			for (int i = 0; i < segments.Length; i++)
				segments[i] = CleanSegment(segments[i]);

			return segments;
		}

		/// <summary>
		/// Cleans up up the segment name.
		/// </summary>
		/// <param name="segment">Segment name</param>
		/// <returns>Clean segment name</returns>
		private static string CleanSegment(string segment)
		{
			if (segment == null)
				return null;
			
			return segment.Replace(" ", "").ToLower();
		}

		/// <summary>
		/// Namespaces where controllers reside.
		/// </summary>
		private List<Namespace> namespaces = new List<Namespace>();

		/// <summary>
		/// Cache of controller classes.
		/// </summary>
		/// <remarks>
		///  It is used only when ReuseControllers is true,
		///  to reuse controller classes on repeated calls.
		/// </remarks>
		private Dictionary<string, CacheHolder> ctrlCache = null;

		/// <summary>
		/// Timer used when Timeout is enabled via method SetTimeout.
		/// </summary>
		private Timer timer = null;

		/// <summary>
		/// Used for thread safety.
		/// </summary>
		private Object safe = new Object();

		/// <summary>
		/// Thread-safe value holder for property ReuseControllers.
		/// </summary>
		private bool _reuseControllers;

		/// <summary>
		/// Timeout length in seconds. 0 means it is disabled.
		/// </summary>
		private ushort TimeOut = 0;

		/// <summary>
		/// Type + Event for handling exceptions thrown by actions.
		/// </summary>
		public delegate void ActionExceptionHandler(HttpContext ctx, string action, Exception ex);
		public event ActionExceptionHandler OnActionException;

		/// <summary>
		/// Simple container of controller/action details.
		/// </summary>
		internal class CacheHolder
		{
			/// <summary>
			/// Controller class instance.
			/// </summary>
			public object instance = null;

			/// <summary>
			/// Date/Time when the controller was last used;
			/// </summary>
			public DateTime ? lastUse = null;
		}
	}

	/// <summary>
	/// Container of namespace details.
	/// </summary>
	public class Namespace
	{
		public Namespace(string ns, Assembly asm)
		{
			this.name = ns;
			this.asm = asm;
		}

		public Namespace(string ns, string asm)
		{
			this.name = ns;
			this.asm = Assembly.Load(asm);
		}

		public Namespace(string ns)
		{
			this.name = ns;
			this.asm = Assembly.GetCallingAssembly();
		}

		public bool IsValid
		{
			get
			{
				return !string.IsNullOrEmpty(name) && asm != null;
			}
		}

		/// <summary>
		/// Name of the namespace.
		/// </summary>
		public string name;

		/// <summary>
		/// Assembly that contains the namespace.
		/// </summary>
		public Assembly asm;
	}

}
