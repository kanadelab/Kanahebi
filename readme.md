# ukastream
C# DLL でSAORIやSHIORIを作るためのテンプレート。

混合アセンブリやDllExportなどを使ってC#でSAORIやSHIORIを作成した場合に
windowsの使用上dllがアンロードできなくなってしまいネットワーク更新等に問題が起きてしまうため
C++ dll と C# exe の2ファイルに切り離し、C# exe は別プロセスとして立ち上げることで問題を回避しています。

## ukastream C++ プロジェクト
デフォルトでは SampleSaori.dll という名前で出力されます。
拡張子違いのexeファイルをSAORI本体として認識して、独自のプロトコルで標準入出力経由で通信をexeに中継します。

## SampleSaori C# プロジェクト
SampleSaori.exe として出力されます。
ukastream C++ プロジェクトの出力dllと拡張子違いの同名にしておき、dllのほうをSAORIとしてロードすると
dllからこのexeが起動されて、SAORI呼び出しを中継します。

UkastreamInterface.cs がdllとの通信部分、SaoriMain.cs がSAORIの処理のテンプレートです。