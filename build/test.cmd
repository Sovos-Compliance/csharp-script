rd TestResults /s /q
md TestResults

nunit-console.exe ..\bin\Release\csharp-code-evaluator-ut.dll /xml=TestResults\csharp-code-evaluator-ut.xml

exit /b 0