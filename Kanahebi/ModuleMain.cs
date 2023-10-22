using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanahebi
{
	internal static class ModuleMain
	{
		private static ScriptEngine scriptEngine;
		private static ScriptScope scriptScope;

		/// <summary>
		/// SAORIのload時に呼ばれる
		/// </summary>
		public static void Load(string moduleDir)
		{
			scriptEngine = Python.CreateEngine();
			scriptScope = scriptEngine.CreateScope();
			var source = scriptEngine.CreateScriptSourceFromFile(Path.Combine(moduleDir, "main.py"));
			source.Execute(scriptScope);
		}

		/// <summary>
		/// Saoriのunload時に呼ばれる
		/// </summary>
		public static void Unload()
		{
			scriptEngine = null;
			scriptScope = null;
		}

		/// <summary>
		/// どういうタイプのDLL(SHIORI, SAORI)としてロードされているかが、ukastreamから通知される
		/// </summary>
		/// <param name="model"></param>
		public static void NotifyModuleMode(UkastreamInterface.ModuleMode model)
		{

		}

		/// <summary>
		/// Saoriのrequest時に呼ばれる
		/// </summary>
		public static string RequestSaori(Dictionary<int, string> args, ref string[] values)
		{
			//SHIORIとして使うので無視
			return null;
		}

		/// <summary>
		/// SHIORIのrequest時に呼ばれる
		/// </summary>
		public static string RequestShiori(string request)
		{
			Func<string, string> function = scriptScope.GetVariable("request");
			var result = function.Invoke(request);
			return result;
		}
	}
}
