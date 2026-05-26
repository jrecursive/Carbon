@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "TARGET=%~1"
if not defined TARGET (
	set "ROOT=%~dp0..\..\.."
	for /f "usebackq delims=" %%B in (`git -C "!ROOT!" rev-parse --abbrev-ref HEAD 2^>nul`) do set "GIT_BRANCH=%%B"
	if /I "!GIT_BRANCH!"=="rust_beta/staging" set "TARGET=staging"
	if /I "!GIT_BRANCH!"=="rust_beta/aux01" set "TARGET=aux01-staging"
	if /I "!GIT_BRANCH!"=="rust_beta/aux02" set "TARGET=aux02-staging"
	if /I "!GIT_BRANCH!"=="rust_beta/aux03" set "TARGET=aux03-staging"
	if not defined TARGET set "TARGET=release"
)

call "%~dp0_runner.bat" tools/build/runners/update.cs "!TARGET!"
exit /b %ERRORLEVEL%
