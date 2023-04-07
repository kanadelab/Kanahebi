#include "pch.h"
#include <assert.h>
#include <string>
#include <fstream>
#include <sstream>
#include <vector>

BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}


DWORD WINAPI PipeReadThread(void*);

//ハンドル類
HANDLE outputWritePipe = INVALID_HANDLE_VALUE;
HANDLE outputReadPipe = INVALID_HANDLE_VALUE;
HANDLE inputWritePipe = INVALID_HANDLE_VALUE;
HANDLE inputReadPipe = INVALID_HANDLE_VALUE;
HANDLE childWritePipe = INVALID_HANDLE_VALUE;
HANDLE readCompletedEvent = INVALID_HANDLE_VALUE;
HANDLE readBeginEvent = INVALID_HANDLE_VALUE;
HANDLE readThreadEndEvent = INVALID_HANDLE_VALUE;
HANDLE readThreadHandle = INVALID_HANDLE_VALUE;
HANDLE processHandle = INVALID_HANDLE_VALUE;
HANDLE jobObject = INVALID_HANDLE_VALUE;

//読込結果
std::string readThreadResponse;

//レスポンスを読み込み
std::string ReadResponse()
{
	//レスポンスの読み込みを開始
	ResetEvent(readCompletedEvent);
	SetEvent(readBeginEvent);

	//待機
	HANDLE handles[] = { processHandle, readCompletedEvent };
	DWORD waitResult = WaitForMultipleObjects(2, handles, FALSE, INFINITE);
	DWORD waitIndex = waitResult - WAIT_OBJECT_0;

	if (waitIndex == 1)
	{
		std::string result = readThreadResponse;
		readThreadResponse.clear();
		return result;
	}
	else
	{
		//状態が異常なのでエラー
		//あまりよろしくないがベースウェアごと落として脱出しておく
		exit(1);
		return std::string();
	}
}

//SHIORI load
extern "C" __declspec(dllexport) BOOL __cdecl load(HGLOBAL h, long len)
{
	//呼出の解析
	char* ghostPath = (char*)malloc(len + 1);
	memcpy(ghostPath, h, len);
	ghostPath[len] = '\0';
	GlobalFree(h);

	//descript.txtを読む。設定をdescriptに書き込んでしまう
	std::string fileName;
	fileName.append(ghostPath);
	fileName.append("ukastream.txt");
	std::ifstream descriptStream(fileName);
	if (descriptStream.fail())
	{
		//descriptが読めない
		return FALSE;
	}

	//設定ファイル解析
	std::string shioriFile;
	std::string prefix = "shiori,";
	std::string descriptLine;
	while (std::getline(descriptStream, descriptLine))
	{
		if (descriptLine.find(prefix) == 0)
		{
			shioriFile = descriptLine.substr(prefix.size());
			break;
		}
	}

	descriptStream.close();
	if (shioriFile.empty())
	{
		//shioriが無効
		return FALSE;
	}

	//継承可能ハンドルを設定
	SECURITY_ATTRIBUTES securityAttributes = {};
	securityAttributes.bInheritHandle = TRUE;
	securityAttributes.lpSecurityDescriptor = nullptr;
	securityAttributes.nLength = sizeof(securityAttributes);

	//標準出力
	CreatePipe(&outputReadPipe, &outputWritePipe, &securityAttributes, 0);
	assert(GetLastError() == S_OK);

	//標準入力
	CreatePipe(&inputReadPipe, &inputWritePipe, &securityAttributes, 0);
	assert(GetLastError() == S_OK);

	//読み込み完了通知用のオブジェクト作成
	readCompletedEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
	readBeginEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
	readThreadEndEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);;

	//親プロセスのクラッシュ時に子プロセスをキルするためのジョブを作成
	jobObject = CreateJobObject(nullptr, nullptr);
	JOBOBJECT_EXTENDED_LIMIT_INFORMATION limitInformation = {};
	limitInformation.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
	SetInformationJobObject(jobObject, JobObjectExtendedLimitInformation, &limitInformation, sizeof(limitInformation));

	//パイプ読み込みスレッドを開始
	STARTUPINFO startupInfo = {};
	PROCESS_INFORMATION processInfo = {};

	startupInfo.cb = sizeof(startupInfo);
	startupInfo.dwFlags = STARTF_USESTDHANDLES;
	startupInfo.hStdInput = inputReadPipe;
	startupInfo.hStdOutput = outputWritePipe;

	readThreadHandle = CreateThread(nullptr, 0, PipeReadThread, nullptr, 0, nullptr);

	std::string shioriPath;
	shioriPath.append(ghostPath);
	shioriPath.append(shioriFile);
	CreateProcess(shioriPath.c_str(), nullptr, nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &startupInfo, &processInfo);
	CloseHandle(processInfo.hThread);
	processHandle = processInfo.hProcess;
	assert(GetLastError() == S_OK);

	//プロセスをアサイン
	AssignProcessToJobObject(jobObject, processHandle);

	//一番上に通信内容だけ送る仕組みにする
	std::string loadRequest;
	loadRequest.append("LOAD BRIDGE/1.0\r\n");
	loadRequest.append(ghostPath);
	loadRequest.append("\r\n\r\n");
	free(ghostPath);
	WriteFile(inputWritePipe, loadRequest.c_str(), (DWORD)loadRequest.size(), nullptr, nullptr);

	//ロード処理を待機
	std::string response = ReadResponse();

	//先頭にBRIDGE/1.0 2xx OKが帰ればいいということにする
	std::istringstream ist(response);
	std::string part;
	std::vector<std::string> headParts;
	while (std::getline(ist, part, ' ')) {
		headParts.push_back(part);
	}

	//2xxだったら成功で返す
	if (headParts.size() >= 2 && (std::stoi(headParts[1]) / 100) == 2) {
		return TRUE;
	}
	else {
		//初期化に失敗しているので蹴るしかない
		return FALSE;
	}
}

//SHIORI unload
extern "C" __declspec(dllexport) BOOL __cdecl unload()
{
	//パイプ読み込みスレッドに終了を指示
	SetEvent(readThreadEndEvent);

	//SHIORIにアンロード、終了を指示
	std::string unloadRequest("UNLOAD BRIDGE/1.0\r\n\r\n");
	WriteFile(inputWritePipe, unloadRequest.c_str(), (DWORD)unloadRequest.size(), nullptr, nullptr);

	//子プロセスと、スレッドの終了を待機
	HANDLE handles[] = { readThreadHandle, processHandle };
	WaitForMultipleObjects(2, handles, TRUE, INFINITE);

	//ハンドル類の開放
	CloseHandle(outputWritePipe);
	CloseHandle(outputReadPipe);
	CloseHandle(inputWritePipe);
	CloseHandle(inputReadPipe);
	CloseHandle(readCompletedEvent);
	CloseHandle(readBeginEvent);
	CloseHandle(readThreadEndEvent);
	CloseHandle(readThreadHandle);
	CloseHandle(processHandle);
	CloseHandle(jobObject);
	return TRUE;
}

//パイプ読み込みスレッド
DWORD WINAPI PipeReadThread(void*)
{
	HANDLE waitHandles[] = { readBeginEvent, readThreadEndEvent };

	//開始待機
	DWORD waitResult = WaitForMultipleObjects(2, waitHandles, FALSE, INFINITE);
	DWORD signalIndex = waitResult - WAIT_OBJECT_0;

	if (signalIndex == 1)
	{
		//終了指示
		return 0;
	}
	else
	{
		ResetEvent(readBeginEvent);
	}

	DWORD readSize = 0;
	while (true)
	{
		char output[1025] = {};
		ReadFile(outputReadPipe, output, sizeof(output) - 1, &readSize, nullptr);
		readThreadResponse.append(output);

		//末尾の確認
		if (readThreadResponse.size() >= 4) {
			if (
				readThreadResponse[readThreadResponse.size() - 4] == '\r' &&
				readThreadResponse[readThreadResponse.size() - 3] == '\n' &&
				readThreadResponse[readThreadResponse.size() - 2] == '\r' &&
				readThreadResponse[readThreadResponse.size() - 1] == '\n'
				)
			{
				//完了を通知
				SetEvent(readCompletedEvent);

				//再度開始を待機
				DWORD waitResult = WaitForMultipleObjects(2, waitHandles, FALSE, INFINITE);
				DWORD signalIndex = waitResult - WAIT_OBJECT_0;

				if (signalIndex == 1)
				{
					//終了指示
					return 0;
				}
				else
				{
					ResetEvent(readBeginEvent);
				}
			}
		}
	}
}


//SHIORI request
extern "C" __declspec(dllexport) HGLOBAL __cdecl request(HGLOBAL h, long* len)
{
	//リクエスト取得
	char* requestBody = (char*)malloc(*len + 1);
	memcpy(requestBody, h, *len);
	requestBody[*len] = '\0';
	GlobalFree(h);

	//リクエスト作成
	//プロトコルのほうに末尾の空行がついているので、それに任せる
	std::string shioriRequest;
	shioriRequest.append("REQUEST BRIDGE/1.0\r\n");
	shioriRequest.append(requestBody);
	free(requestBody);

	//リクエストの書き込み
	WriteFile(inputWritePipe, shioriRequest.c_str(), (DWORD)shioriRequest.size(), nullptr, nullptr);
	std::string rawResponse = ReadResponse();
	std::string response;

	//最初の行はBRIDGEレスポンスなので読み飛ばす
	size_t fistNewLine = rawResponse.find("\r\n");
	if (fistNewLine != std::string::npos)
	{
		//１行目が成功しているかどうかチェックしてもいいかもだけどとりあえずそのまま返してる
		response = rawResponse.substr(fistNewLine + 2);
	}
	else
	{
		//フォーマット不正
		response = "SHIORI/300 500 Internal Server Error\r\n\r\n";
	}

	//読み込みを完了
	HGLOBAL res = GlobalAlloc(GMEM_FIXED, response.size());
	memcpy(res, response.c_str(), response.size());
	*len = (long)response.size();
	return res;
}

int main()
{
	SECURITY_ATTRIBUTES securityAttributes = {};
	securityAttributes.bInheritHandle = TRUE;
	securityAttributes.lpSecurityDescriptor = nullptr;
	securityAttributes.nLength = sizeof(securityAttributes);

	HANDLE outputWritePipe = INVALID_HANDLE_VALUE;
	HANDLE outputReadPipe = INVALID_HANDLE_VALUE;
	HANDLE inputWritePipe = INVALID_HANDLE_VALUE;
	HANDLE inputReadPipe = INVALID_HANDLE_VALUE;
	HANDLE childWritePipe = INVALID_HANDLE_VALUE;

	//標準出力
	CreatePipe(&outputReadPipe, &outputWritePipe, &securityAttributes, 0);
	assert(GetLastError() == S_OK);

	//標準入力
	CreatePipe(&inputReadPipe, &inputWritePipe, &securityAttributes, 0);
	assert(GetLastError() == S_OK);

	STARTUPINFO startupInfo = {};
	PROCESS_INFORMATION processInfo = {};

	startupInfo.cb = sizeof(startupInfo);
	startupInfo.dwFlags = STARTF_USESTDHANDLES;
	startupInfo.hStdInput = inputReadPipe;
	startupInfo.hStdOutput = outputWritePipe;

	char cmdLine[] = { "/c echo aaa" };
	CreateProcess("D:\\SSP_2\\ghost\\angel_test\\ghost\\master\\ScriptRuntime.exe", cmdLine, nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &startupInfo, &processInfo);
	assert(GetLastError() == S_OK);

	DWORD readBytes = 0;
	char loadRequest[] = { "LOAD\r\nD:\\SSP_2\\ghost\\angel_test\\ghost\\master\r\n\r\n" };
	WriteFile(inputWritePipe, loadRequest, sizeof(loadRequest) - 1, &readBytes, nullptr);

	char shioriRequest[] = { "REQUEST\r\nGET Version/3.0\r\n\r\n" };
	WriteFile(inputWritePipe, shioriRequest, sizeof(shioriRequest) - 1, &readBytes, nullptr);

	char output[1024] = {};
	ReadFile(outputReadPipe, output, sizeof(output), &readBytes, nullptr);

	return 0;
}