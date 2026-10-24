@echo off
chcp 65001 >nul

set TASK_NAME=RestoCashServerAutoStart
set SCRIPT_PATH=C:\ProgramData\RestoCashServerStart.cmd

echo ==========================================
echo Создание вспомогательного скрипта...
echo ==========================================

(
echo @echo off
echo net start "Resto.CashServer.Svc"
echo timeout /t 3 /nobreak ^>nul
echo start "" "C:\Program Files\iiko\iikoRMS\Front.Net\Resto.CashServer.ServiceControl.exe"
) > "%SCRIPT_PATH%"

echo ==========================================
echo Создание задачи планировщика...
echo ==========================================

schtasks /create ^
/tn "%TASK_NAME%" ^
/tr "cmd.exe /c "%SCRIPT_PATH%"" ^
/sc onlogon ^
/rl HIGHEST ^
/f

if %errorlevel% neq 0 (
echo.
echo Ошибка создания задачи!
pause
exit /b 1
)

echo.
echo ==========================================
echo Настройка автозапуска службы...
echo ==========================================

sc config "Resto.CashServer.Svc" start= auto

echo.
echo ==========================================
echo Готово.
echo ==========================================
echo.
echo Создан файл:
echo %SCRIPT_PATH%
echo.
echo Создана задача:
echo %TASK_NAME%
echo ==========================================

pause
