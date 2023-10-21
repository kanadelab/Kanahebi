using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleSaori
{
	internal static class SaoriMain
	{
		/// <summary>
		/// SAORIのload時に呼ばれる
		/// </summary>
		public static void Load()
		{

		}

		/// <summary>
		/// Saoriのunload時に呼ばれる
		/// </summary>
		public static void Unload()
		{

		}

		/// <summary>
		/// Saoriのrequest時に呼ばれる
		/// </summary>
		public static string Request(Dictionary<int, string> args, ref string[] values)
		{
			var command = args[0];
			switch (command)
			{
				case "Test":
					return "SampleSaori-Test";
			}
			throw new NotImplementedException();
		}
	}
}
