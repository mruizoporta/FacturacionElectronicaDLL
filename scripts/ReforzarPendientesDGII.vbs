' Llama a la DLL COM. Usar cscript de 32 bits (SysWOW64).
' Uso: cscript //nologo ReforzarPendientesDGII.vbs [emp] [horas]
Option Explicit
Dim emp, horas, svc, r
emp = "1"
horas = 24
If WScript.Arguments.Count >= 1 Then emp = Trim(WScript.Arguments(0))
If WScript.Arguments.Count >= 2 Then horas = CInt(WScript.Arguments(1))
If emp = "" Then emp = "1"
If horas < 1 Then horas = 24

Set svc = CreateObject("FacturacionElectronicaDGII.FacturaElectronicaService")
r = svc.ReforzarPendientesDGII(emp, horas)
WScript.Echo r
