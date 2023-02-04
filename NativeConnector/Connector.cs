using ShioriRuntime;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

#if CONNECTOR_BUILD
namespace NativeConnector
{
	class Connector
	{
		private static readonly Encoding sjis = Encoding.GetEncoding("Shift_JIS");
		private static IShiori shioriMain;
		private static string ghostPath;

		[DllExport]
		public static int load(IntPtr i_data, int i_data_len)
		{
			//dll解決用
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

			//ゴースト起動パス確認
			var pathBytes = new byte[i_data_len];
			Marshal.Copy(i_data, pathBytes, 0, pathBytes.Length);
			Marshal.FreeHGlobal(i_data);
			ghostPath = sjis.GetString(pathBytes);

			//自身のアセンブリから
			var types = Assembly.GetExecutingAssembly().DefinedTypes;
			var shioriType = types.Where(o => o.ImplementedInterfaces.Any(t => t == typeof(IShiori)));
			if(shioriType.Count() != 1)
			{
				//SHIORI実装が1つ以外の場合は無効
				return 0;
			}

			//shioriインスタンスの作成
			shioriMain = (IShiori)Activator.CreateInstance(shioriType.First());

			return shioriMain.Load(ghostPath) ? 1 : 0;
		}

		[DllExport]
		public static int unload()
		{
			shioriMain.Unload();
			shioriMain = null;
			return 1;
		}

		[DllExport]
		public static IntPtr request(IntPtr intPtr, ref int io_data_len)
		{
			var requestBytes = new byte[io_data_len];
			Marshal.Copy(intPtr, requestBytes, 0, requestBytes.Length);
			string data = sjis.GetString(requestBytes);
			Marshal.FreeHGlobal(intPtr);

			//リクエストをパース
			var sp = data.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
			var head = sp[0];

			var result = "";

			//get versionだけ処理する
			if (head.StartsWith("GET Version"))
			{
				result = "SHIORI/2.0 200 OK\r\n\r\n";
			}
			else
			{
				//shioriに流す
				var res = shioriMain.Request(data);
				result = res;
			}

			var bytes = sjis.GetBytes(result);
			io_data_len = bytes.Length;
			var resultMem = Marshal.AllocHGlobal(bytes.Length);
			Marshal.Copy(bytes, 0, resultMem, bytes.Length);

			return resultMem;
		}

		//アセンブリを奥におしやる
		private static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var name = new AssemblyName(args.Name);
			var path = string.Format(@"{0}", name.Name);
			if (!string.IsNullOrEmpty(name.CultureName))
				path = string.Format(@"{1}\{0}", name.Name, name.CultureName);
			path = System.IO.Path.Combine(ghostPath, path);

			if (System.IO.File.Exists(path + ".dll"))
				return Assembly.LoadFile(path + ".dll");
			else
				return null;
		}

	}

	
}
#endif


namespace ShioriRuntime
{
	//SHIORIインターフェース
	public interface IShiori
	{
		bool Load(string ghostPath);
		void Unload();
		string Request(string requestBody);
	}
}

namespace ShioriRuntime
{
	//リクエスト情報
	public class ShioriRequest
	{
		public Dictionary<string, string> Values { get; private set; }
		public string Protocol { get; private set; }

		public string GetValue(string key)
		{
			if (Values.ContainsKey(key))
				return Values[key];
			return null;
		}

		//Event
		public string Event { get { return GetValue("Event"); }}

		//Reference値
		public string Reference0 { get { return GetValue("Reference0"); } }
		public string Reference1 { get { return GetValue("Reference1"); } }
		public string Reference2 { get { return GetValue("Reference2"); } }
		public string Reference3 { get { return GetValue("Reference3"); } }
		public string Reference4 { get { return GetValue("Reference4"); } }
		public string Reference5 { get { return GetValue("Reference5"); } }
		public string Reference6 { get { return GetValue("Reference6"); } }
		public string Reference7 { get { return GetValue("Reference7"); } }
		public string Reference8 { get { return GetValue("Reference8"); } }
		public string Reference9 { get { return GetValue("Reference9"); } }

		public ShioriRequest(string data)
		{
			var sp = data.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
			Protocol = sp[0];
			Values = new Dictionary<string, string>();

			for(int i = 1; i < sp.Length; i++)
			{
				var v = sp[i];
				var sp2 = v.Split(new string[] { ": " }, 2, StringSplitOptions.RemoveEmptyEntries);
				if (sp2.Length == 2)
				{
					Values[sp2[0]] = sp2[1];
				}
			}
		}
	}

	//レスポンス情報
	public class ShioriResponse
	{
		public enum StatusCode
		{
			OK,
			NoContent,
			InternalServerError
		}

		public StatusCode Code { get; set; }
		public Dictionary<string, string> Values { get; private set; }

		public string Sentence
		{
			get
			{
				if(Values.ContainsKey("Sentence"))
				{
					return Values["Sentence"];
				}
				return null;
			}
			set
			{
				if(value == null)
				{
					Values.Remove("Sentence");
				}
				else
				{
					Values["Sentence"] = value;
				}
			}
		}

		public ShioriResponse()
		{
			Code = StatusCode.OK;
			Values = new Dictionary<string, string>();
			Values["Charset"] = "Shift_JIS";
		}

		public string Serialize()
		{
			var builder = new StringBuilder();

			//ステータスコード
			builder.Append("SHIORI/2.2 ");
			switch (Code)
			{
				case StatusCode.OK:
					builder.AppendLine("200 OK");
					break;
				case StatusCode.NoContent:
					builder.AppendLine("204 No Content");
					break;
				case StatusCode.InternalServerError:
				default:
					builder.AppendLine("500 Internal Server Error");
					break;
			}

			//値設定
			foreach (var item in Values)
			{
				builder.AppendLine(string.Format("{0}: {1}", item.Key, item.Value));
			}

			//終端
			builder.Append("\r\n");
			return builder.ToString();
		}
	}
}