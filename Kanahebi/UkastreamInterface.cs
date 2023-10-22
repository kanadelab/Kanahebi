using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Threading.Tasks;


namespace Kanahebi
{
	internal class UkastreamInterface
	{
		/// <summary>
		/// 今のところsjis固定で読んでるので問題がある場合は注意が必要かも
		/// </summary>
		private static readonly Encoding sjis = Encoding.GetEncoding("Shift_JIS");
		private static StreamReader inputReader;
		private static StreamWriter outputWriter;
		private static ModuleMode mode = ModuleMode.NONE;

		public enum ModuleMode
		{
			NONE,
			SAORI,
			SHIORI
		}
		
		private static string RequestSaoriModule(Dictionary<int, string> args, ref string[] values)
		{
			try
			{
				return ModuleMain.RequestSaori(args, ref values);
			}
			catch
			{
				//エラーを握り潰しているけど、bad requestなりを返したほうがいいかも
				return string.Empty;
			}
		}

		public static string RequestShioriModule(string request)
		{
			try
			{
				return ModuleMain.RequestShiori(request);
			}
			catch
			{
				return string.Empty;
			}
		}

		private static void UnloadModule()
		{
			try
			{
				ModuleMain.Unload();
			}
			catch { }
		}

		private static void LoadModule(string moduleDir)
		{
			try
			{
				ModuleMain.Load(moduleDir);
			}
			catch { }
		}

		/// <summary>
		/// リクエスト処理
		/// </summary>
		private static int Main(string[] args)
		{
			//ukastream bridgeの実装
			inputReader = new StreamReader(Console.OpenStandardInput(), sjis);
			outputWriter = new StreamWriter(Console.OpenStandardOutput(), sjis);
			bool isExit = false;

			//空白行がくるまでが１くくり
			var lines = new List<string>();
			while (true)
			{
				var line = inputReader.ReadLine();
				if (line == null)
					break;
				if (line != string.Empty)
				{
					lines.Add(line);
				}
				else if (lines.Any())
				{
					if (lines.First().StartsWith("LOAD"))
					{
						string moduleDir = null;
						if(lines.Count >= 2)
						{
							moduleDir = lines[1];
						}
						LoadModule(moduleDir);
						outputWriter.Write("BRIDGE/1.0 200 OK\r\n\r\n");
						outputWriter.Flush();
					}
					else if (lines.First().StartsWith("REQUEST"))
					{
						//req
						var requestProtocol = lines.Skip(1).ToArray();

						if (mode == ModuleMode.NONE)
						{
							if (requestProtocol.FirstOrDefault()?.StartsWith("GET Version SHIORI") == true)
							{
								mode = ModuleMode.SHIORI;
								outputWriter.Write("BRIDGE/1.0 200 OK\r\nSHIORI/2.0 200 OK\r\n\r\n");
								outputWriter.Flush();
							}
							else if (requestProtocol.FirstOrDefault()?.StartsWith("GET Version SAORI") == true)
							{
								mode = ModuleMode.SAORI;
								outputWriter.Write("BRIDGE/1.0 200 OK\r\nSAORI/1.0 200 OK\r\n\r\n");
								outputWriter.Flush();
							}
							else
							{
								outputWriter.Write("501 Not Implemented\r\n\r\n");
								outputWriter.Flush();
							}
						}
						else if (mode == ModuleMode.SAORI)
						{
							if (requestProtocol.FirstOrDefault()?.StartsWith("EXECUTE SAORI") == true)
							{
								//引数データの収集
								var saoriRawArgs = requestProtocol.Skip(1);
								var saoriArgs = new Dictionary<int, string>();

								foreach (string rawArg in saoriRawArgs)
								{
									if (rawArg.StartsWith("Argument"))
									{
										var sp = rawArg.Split(new string[] { ": " }, 2, StringSplitOptions.None);
										int index;
										if (sp.Length == 2 && int.TryParse(sp[0].Substring("Argument".Length), out index))
										{
											saoriArgs.Add(index, sp[1]);
										}
									}
								}

								string[] values = Array.Empty<string>();
								var result = RequestSaoriModule(saoriArgs, ref values);
								var rawValues = new List<string>();

								for (int i = 0; i < values.Length; i++)
								{
									rawValues.Add(string.Format("Value{0}: {1}", i, values[i]));
								}

								outputWriter.Write(string.Format("BRIDGE/1.0 200 OK\r\nSAORI/1.0 200 OK\r\nCharset: Shift_JIS\r\nResult: {0}\r\n{1}\r\n\r\n",
									result,
									string.Join("\r\n", rawValues)));
								outputWriter.Flush();
							}
							else
							{
								outputWriter.Write("501 Not Implemented\r\n\r\n");
								outputWriter.Flush();
							}
						}
						else if (mode == ModuleMode.SHIORI)
						{
							//SHIORIの場合システム側でパースすると余計に複雑なのでそのまま投げてSHIORI実装に任せる
							var result = RequestShioriModule(string.Join("\r\n", requestProtocol));

							//通信側プロトコルでラップ
							result = "BRIDGE/1.0 200 OK\r\n" + result;
							outputWriter.Write(result);
							outputWriter.Flush();
						}
					}
					else if (lines.First().StartsWith("UNLOAD"))
					{
						UnloadModule();
						isExit = true;
					}
					else
					{
						//未実装
						outputWriter.Write("501 Not Implemented\r\n\r\n");
						outputWriter.Flush();
					}
					lines.Clear();
				}

				//終了指示による脱出
				if (isExit)
					break;
			}
			return 0;
		}
	}
}
