@echo off
setlocal
set "GENSW_ROOT=%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%GENSW_ROOT%scripts\local\stop-gensw.ps1" %*
set "GENSW_EXIT_CODE=%ERRORLEVEL%"

if not "%GENSW_EXIT_CODE%"=="0" (
  echo.
  echo [GenSW] O encerramento falhou. Consulte a mensagem acima e tente novamente.
  echo %CMDCMDLINE% | findstr /I " /c " >nul
  if not errorlevel 1 if not defined GENSW_NO_PAUSE pause
)

exit /b %GENSW_EXIT_CODE%
