# Matrix Idle para Windows

Una animación de lluvia digital que aparece cuando dejas de usar Windows. Cubre todos los monitores conectados y se oculta al mover el ratón o pulsar una tecla.

[Read in English](README.md)

## Funciones

- Espera 60 segundos por defecto; permite entre 5 y 3600 segundos con `--idle-seconds`.
- Se muestra a pantalla completa en cada monitor.
- Funciona en la sesión del usuario, sin permisos de administrador.
- El instalador añade el inicio de sesión y una tarea que lo reinicia si se cierra. Solo funciona una instancia por sesión.

Es una aplicación visual de inactividad, **no** un módulo `.scr` de Windows. No bloquea el equipo ni pide contraseña. Configura el bloqueo de Windows por separado si lo necesitas. Otro protector de pantalla habilitado podría activarse al mismo tiempo.

## Requisitos

- Windows 11 (probado en Windows 11 Pro, compilación 26200); Windows 10 aún no se ha probado.
- .NET Framework 4.x con su compilador C# (`csc.exe`).
- PowerShell y el módulo de tareas programadas de Windows para instalarlo.

## Compilar y probar

Abre PowerShell en esta carpeta:

```powershell
.\scripts\build.ps1
.\dist\MatrixIdle.exe --idle-seconds 60
```

El primer comando crea `dist\MatrixIdle.exe`. El segundo deja el proceso en ejecución. Tras el tiempo indicado aparece la animación; cualquier movimiento del ratón o pulsación la oculta, pero el proceso sigue esperando la siguiente inactividad. Para detenerlo por completo:

```powershell
$builtExe = (Resolve-Path .\dist\MatrixIdle.exe).Path
Get-CimInstance Win32_Process -Filter "Name='MatrixIdle.exe'" |
    Where-Object { $_.ExecutablePath -eq $builtExe } |
    ForEach-Object { Stop-Process -Id $_.ProcessId }
```

## Instalar para el usuario actual

```powershell
.\scripts\install.ps1 -IdleSeconds 60
```

El script compila, instala en `%LOCALAPPDATA%\MatrixIdle`, inicia el programa y configura el inicio de sesión. La tarea `MatrixIdleWatchdog` lo recupera aproximadamente un minuto después si se cierra y también se activa al iniciar sesión. No requiere permisos de administrador. Si ya hay otra instalación que usa esas entradas de inicio, el script se detiene y pide reemplazarla de forma explícita.

Para quitarlo:

```powershell
.\scripts\uninstall.ps1
```

## Diagnóstico

```powershell
Get-Process MatrixIdle -ErrorAction SilentlyContinue
Get-ScheduledTaskInfo -TaskName MatrixIdleWatchdog
Get-Content "$env:LOCALAPPDATA\MatrixIdle\MatrixIdle.log" -Tail 20
```

El código está en [`src/MatrixIdle.cs`](src/MatrixIdle.cs). El repositorio excluye los ejecutables generados y los registros.

## Licencia

Consulta [`LICENSE`](LICENSE).
