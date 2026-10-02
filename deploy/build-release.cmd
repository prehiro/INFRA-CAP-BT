@echo off
REM ============================================================
REM  Build artifacts for flashdisk transfer to the production server.
REM  Run from the repository root on the DEV machine.
REM ============================================================

setlocal
set ROOT=%~dp0..
set STAGE=%ROOT%\..\dist

echo [1/4] Building API (publish)...
pushd "%ROOT%\api"
call dotnet publish -c Release -o "%STAGE%\api" --nologo
if errorlevel 1 goto :fail
popd

echo [2/4] Generating static SPA for /INFRA-CAP ...
pushd "%ROOT%\web"
REM baseURL is baked into asset paths at generate time, so it must be set here.
set NUXT_APP_BASE_URL=/INFRA-CAP/
call npx nuxt generate
if errorlevel 1 goto :fail
REM CRITICAL: clear it again. A leftover /INFRA-CAP base in the shell makes the
REM NEXT `nuxt dev` run with a production baseURL, and the dev router then rejects
REM every route with "No match found for location with path /infra-cap".
set NUXT_APP_BASE_URL=
set NUXT_PUBLIC_API_BASE=
popd

echo [3/4] Copying SPA output ...
if exist "%STAGE%\web" rmdir /s /q "%STAGE%\web"
xcopy "%ROOT%\web\.output\public" "%STAGE%\web\" /E /I /Q
REM The SPA Web.config must sit at the site root to control URL rewriting.
copy /Y "%ROOT%\web\deploy\web.config.spa" "%STAGE%\web\web.config" >nul

echo [4/4] Writing deploy checklist...
copy /Y "%ROOT%\DEPLOY.md" "%STAGE%\DEPLOY.md" >nul

echo.
echo DONE. Artifacts in %STAGE%
echo   dist\web    -> copy to C:\inetpub\INFRA-CAP      (IIS application)
echo   dist\api    -> copy to C:\inetpub\INFRA-CAP-api  (IIS application)
echo.
goto :eof

:fail
echo BUILD FAILED - see errors above.
popd
exit /b 1
