@ECHO OFF
call build_internal Release "Any CPU"
call build_internal Debug "Any CPU"

EXIT /B 0