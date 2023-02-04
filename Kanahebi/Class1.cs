using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using ShioriRuntime;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace Kanahebi
{
	class Runtime : IShiori
	{
		private ScriptEngine scriptEngine;
		private ScriptScope scriptScope;

		//load()
		public bool Load(string ghostPath)
		{
			scriptEngine = Python.CreateEngine();
			scriptScope = scriptEngine.CreateScope();
			var source = scriptEngine.CreateScriptSourceFromFile(Path.Combine(ghostPath, "main.py"));
			source.Execute(scriptScope);
			return true;
		}

		//unload()
		public void Unload()
		{
		}

		//request()
		public string Request(string request)
		{
			var parser = new ShioriRequest(request);
			Func<string,string> function = scriptScope.GetVariable("request");
			var result = function.Invoke(request);
			return result;
		}
	}
}
