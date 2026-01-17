@echo off
:: =======================================================
:: Packet Generator Script (Protobuf & FlatBuffers)
:: =======================================================

:: 1. 경로 설정
set ROOT_DIR=%~dp0..\..
set TOOLS_DIR=%~dp0
set PROTOC=%TOOLS_DIR%Bin\protoc.exe
set FLATC=%TOOLS_DIR%Bin\flatc.exe

:: 출력 경로 (서버 C#)
set SERVER_OUT=%ROOT_DIR%\Server\Packet\Gen
:: 출력 경로 (클라이언트 C++ / 예시 경로)
set CLIENT_OUT=%ROOT_DIR%\Client\Source\Network\Gen

:: 폴더 없으면 생성
if not exist "%SERVER_OUT%" mkdir "%SERVER_OUT%"
if not exist "%CLIENT_OUT%" mkdir "%CLIENT_OUT%"

echo [1/2] Generating Protobuf Packets...
:: C# (Server)
"%PROTOC%" --proto_path="%TOOLS_DIR%Schemas" --csharp_out="%SERVER_OUT%" "Protocol.proto"
:: C++ (Client/Unreal) - 필요 시 주석 해제
:: "%PROTOC%" --proto_path="%TOOLS_DIR%Schemas" --cpp_out="%CLIENT_OUT%" "Protocol.proto"

echo [2/2] Generating FlatBuffers Packets...
:: C# (Server)
"%FLATC%" --csharp --gen-object-api -o "%SERVER_OUT%" "%TOOLS_DIR%Schemas\Protocol.fbs"
:: C++ (Client/Unreal) - 필요 시 주석 해제
:: "%FLATC%" --cpp -o "%CLIENT_OUT%" "%TOOLS_DIR%Schemas\Protocol.fbs"

echo.
echo =======================================================
echo Packet Generation Complete!
echo Server Output: %SERVER_OUT%
echo Client Output: %CLIENT_OUT%
echo =======================================================
pause