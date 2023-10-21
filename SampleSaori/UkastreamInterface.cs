using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleSaori
{
	internal class UkastreamInterface
	{
		/// <summary>
		/// 今のところsjis固定で読んでるので問題がある場合は注意が必要かも
		/// </summary>
		private static readonly Encoding sjis = Encoding.GetEncoding("Shift_JIS");
		private static StreamReader inputReader;
		private static StreamWriter outputWriter;
		
		private static string RequestModule(Dictionary<int, string> args, ref string[] values)
		{
			try
			{
				return SaoriMain.Request(args, ref values);
			}
			catch
			{
				//エラーを握り潰しているけど、bad requestなりを返したほうがいいかも
				return string.Empty;
			}
		}

		private static void UnloadModule()
		{
			try
			{
				SaoriMain.Unload();
			}
			catch { }
		}

		private static void LoadModule()
		{
			try
			{
				SaoriMain.Load();
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
						LoadModule();
						outputWriter.Write("BRIDGE/1.0 200 OK\r\n\r\n");
						outputWriter.Flush();
					}
					else if (lines.First().StartsWith("REQUEST"))
					{
						//req
						var requestProtocol = lines.Skip(1).ToArray();
						if (requestProtocol.FirstOrDefault()?.StartsWith("GET Version") == true)
						{
							outputWriter.Write("BRIDGE/1.0 200 OK\r\nSAORI/1.0 200 OK\r\n\r\n");
							outputWriter.Flush();
						}
						else if (requestProtocol.FirstOrDefault()?.StartsWith("EXECUTE") == true)
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
							var result = RequestModule(saoriArgs, ref values);
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
