using System;
using System.Collections.Generic;
using System.Web;
using System.IO;

namespace TestServer
{
	/// <summary>
	/// Return result for method HttpFileCache.ProcessFileRequest
	/// </summary>
	public enum ProcessFileResult
	{
		/// <summary>
		/// Local file, was successfully read and returned.
		/// </summary>
		Sucess,
		
		/// <summary>
		/// Local file, but failed to access and/or read.
		/// </summary>
		Failed,
		
		/// <summary>
		/// Not a local file, needs a different processing.
		/// </summary>
		NotFile
	}

	/// <summary>
	/// Handles HTTP requests for files, while implementing basic caching.
	/// </summary>
	/// <remarks>
	/// This class was designed to handle only regular file reads, such as
	/// HTTP, JavaScript, CSS, etc. It will not work properly in case of
	/// big files, such as binary uploads or some huge PDF-s.
	/// It also does not provide for downloads from other sites, it only
	/// works with local files on the server.
	/// </remarks>
	public class HttpFileCache
	{
		/// <summary>
		/// File cache.
		/// </summary>
		private Dictionary<string, FileCache> cache = new Dictionary<string, FileCache>();

		/// <summary>
		/// Checks if the request is for an existing file, and if so,
		/// reads it and outputs into the response stream.
		/// </summary>
		/// <remarks>
		/// 1 .It uses cache to check if the file has already been read.
		/// 2. If file has changed since last read, it is read again.
		/// </remarks>
		/// <param name="ctx">HTTP context object</param>
		/// <returns>see type ProcessFileResult</returns>
		public ProcessFileResult ProcessFileRequest(HttpContext ctx)
		{
			string path = ctx.Request.PhysicalPath; // Full path to the file;
			FileInfo info = new FileInfo(path);
			if(!info.Exists)
				return ProcessFileResult.NotFile; // The request is not for an existing file;
			lock (cache)
			{
				FileCache fc;
				if (cache.ContainsKey(path))
				{
					fc = cache[path];
					if (fc.LastRead < info.LastWriteTime)
					{
						// File has been changed since it was last read;
						fc.Data = ReadFile(info);
						if (fc.Data == null)
						{
							cache.Remove(path);
							return ProcessFileResult.Failed;
						}
					}
				}
				else
				{
					// File hasn't been read yet;
					fc = new FileCache()
					{
						Data = ReadFile(info),
						LastRead = DateTime.Now
					};
					if (fc.Data == null)
						return ProcessFileResult.Failed;
					cache.Add(path, fc); // Add it to the cache;
				}
				ctx.Response.OutputStream.Write(fc.Data, 0, fc.Data.Length); // Write into the response;
			}
			return ProcessFileResult.Sucess;
		}

		/// <summary>
		/// Clears all the file cache, in case it is needed.
		/// </summary>
		/// <remarks>
		/// This method here is just in case one needs it,
		/// although in most cases it is not needed.
		/// </remarks>
		public void Reset()
		{
			lock (cache)
			{
				cache.Clear();
			}
		}

		/// <summary>
		/// Helper for reading entire file into array of bytes.
		/// </summary>
		/// <param name="info">file information object</param>
		/// <returns>array of bytes, or null, if failed</returns>
		private static byte[] ReadFile(FileInfo info)
		{
			try
			{
				FileStream fs = info.OpenRead();
				if (fs != null)
				{
					BinaryReader r = new BinaryReader(fs);
					byte[] data = r.ReadBytes((int)info.Length);
					r.Close();
					return data;
				}
			}
			catch (Exception)
			{
			}
			return null;
		}

		/// <summary>
		/// Contains information about file cache.
		/// </summary>
		internal class FileCache
		{
			/// <summary>
			/// File contents.
			/// </summary>
			public byte[] Data;
			
			/// <summary>
			/// Time when the file was las read.
			/// </summary>
			public DateTime LastRead;
		}
	}
}
