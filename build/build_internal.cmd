@ECHO OFF
SETLOCAL

rem SET PATH=%PATH%;C:\Windows\Microsoft.NET\Framework\v4.0.30319
call "%VS100COMNTOOLS%"\\vsvars32.bat
ECHO ### Building csharp-script-solution %1 %2 ###
msbuild ..\csharp-code-evaluator-ut\csharp-script-solution.sln /p:Configuration=%1 /p:Platform=%2
IF ERRORLEVEL 1 EXIT

ENDLOCAL
