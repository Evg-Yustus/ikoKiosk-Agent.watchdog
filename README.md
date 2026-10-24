# iikoKiosk Agent watchdog

Теги: iiko, iikoKiosk

Готовый файл:

dist\\iikoKiosk-Agent.watchdog.exe

Программа:

- проверяет службу Resto.CashServer.Svc (отображаемое имя iiko Agent);
- проверяет процесс Resto.CashServer.ServiceControl.exe;
- при восстановлении сначала запускает службу, затем приложение;
- выполняет проверку сразу после запуска и далее каждые 3 минуты;
- использует паузу 2/4/8/16/30 минут после неудачных повторных запусков, чтобы исключить зацикливание;
- работает в системном трее и не допускает несколько экземпляров;
- содержит пункт «Добавить в автозапуск».

При первом ручном запуске без прав администратора EXE запрашивает UAC. Автозапуск создаётся как задача планировщика с уровнем HIGHEST, поэтому при последующих входах пользователя отдельный UAC-запрос не появляется.

Путь к приложению агента ищется в следующих местах:

1. рядом с watchdog;
2. C:\\RMS\\iikoRMS\\Front.Net\\Resto.CashServer.ServiceControl.exe;
3. C:\\Program Files\\iiko\\iikoRMS\\Front.Net\\Resto.CashServer.ServiceControl.exe;
4. C:\\Program Files (x86)\\iiko\\iikoRMS\\Front.Net\\Resto.CashServer.ServiceControl.exe;
5. в RestoCashServerStart.cmd на рабочем столе или в C:\\ProgramData.

Для отключения автозапуска снимите ту же галочку в меню значка tray.

Сборка:

    dotnet publish .\\iikoKiosk-Agent.watchdog.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\\dist
