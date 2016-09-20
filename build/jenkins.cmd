@ECHO OFF

:: Exit if not run by Jenkins
IF [%JOB_NAME%]==[] (
  ECHO "ONLY RUNS ON JENKINS"
  EXIT /B
)
:: Get branch name from jenkins job
FOR /F "tokens=1,2 delims=." %%A IN ("%JOB_NAME%") DO SET BRANCH=%%B
CALL X:\__Dev\__Source\build-toolbox.development\Scripts\pre-jenkins.cmd
IF ERRORLEVEL 1 EXIT /B
CALL setup.cmd
IF ERRORLEVEL 1 EXIT /B
call build.cmd
IF ERRORLEVEL 1 EXIT /B
call test.cmd
IF ERRORLEVEL 1 EXIT /B

