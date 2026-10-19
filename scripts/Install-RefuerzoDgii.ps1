# Instala la tarea de Windows que refuerza envíos DGII cada N minutos.
# Corre en la máquina donde está registrada FacturacionElectronicaDGII.dll (servidor Vega / caja).
#
# Ejemplo Vega:
#   powershell -ExecutionPolicy Bypass -File .\Install-RefuerzoDgii.ps1 -Emp 1
param(
    [string]$Emp = "1",
    [int]$Horas = 24,
    [int]$Minutos = 15,
    [string]$TaskName = "DASHA-RefuerzoDGII"
)

$ErrorActionPreference = "Stop"
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$vbs = Join-Path $aqui "ReforzarPendientesDGII.vbs"
if (-not (Test-Path $vbs)) { throw "No esta $vbs" }

function Get-FeDllDir {
    $progId = "FacturacionElectronicaDGII.FacturaElectronicaService"
    $roots = @(
        "HKLM:\SOFTWARE\Classes\$progId\CLSID",
        "HKLM:\SOFTWARE\WOW6432Node\Classes\$progId\CLSID"
    )
    $clsid = $null
    foreach ($k in $roots) {
        if (Test-Path $k) {
            $clsid = (Get-ItemProperty $k).'(default)'
            if ($clsid) { break }
        }
    }
    if (-not $clsid) { return $null }
    $inproc = @(
        "HKLM:\SOFTWARE\Classes\CLSID\$clsid\InprocServer32",
        "HKLM:\SOFTWARE\Classes\Wow6432Node\CLSID\$clsid\InprocServer32",
        "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$clsid\InprocServer32"
    )
    foreach ($k in $inproc) {
        if (Test-Path $k) {
            $dll = (Get-ItemProperty $k).'(default)'
            if ($dll -and (Test-Path $dll)) {
                return Split-Path -Parent $dll
            }
        }
    }
    return $null
}

$dllDir = Get-FeDllDir
if ($dllDir) {
    Write-Host "DLL FE en: $dllDir"
} else {
    Write-Host "No se encontro la DLL registrada. La tarea igual se instala; config.ini debe estar junto a la DLL."
}

$cscript = Join-Path $env:SystemRoot "SysWOW64\cscript.exe"
if (-not (Test-Path $cscript)) { $cscript = Join-Path $env:SystemRoot "System32\cscript.exe" }

$argsCscript = "//B //Nologo `"$vbs`" $Emp $Horas"
$action = New-ScheduledTaskAction -Execute $cscript -Argument $argsCscript -WorkingDirectory $aqui
$trigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).AddMinutes(1)) `
    -RepetitionInterval (New-TimeSpan -Minutes $Minutos) `
    -RepetitionDuration (New-TimeSpan -Days 3650)
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -StartWhenAvailable -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal -Force | Out-Null

Write-Host "Tarea '$TaskName' lista: cada $Minutos min, emp=$Emp, horas=$Horas (SYSTEM)"
Write-Host "Ejecutable: $cscript $argsCscript"
Write-Host "Probar ahora: Start-ScheduledTask -TaskName '$TaskName'"
