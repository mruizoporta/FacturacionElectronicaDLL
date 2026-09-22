Imports System.Runtime.InteropServices
Imports System.Text

Public Class IniReader
    Private ReadOnly path As String

    Public Sub New(iniPath As String)
        Me.path = iniPath
    End Sub

    <DllImport("kernel32", CharSet:=CharSet.Unicode)>
    Private Shared Function GetPrivateProfileString(
        section As String,
        key As String,
        defaultValue As String,
        <Out> retVal As StringBuilder,
        size As Integer,
        filePath As String) As Integer
    End Function

    Public Function Read(key As String, section As String) As String
        Dim retVal As New StringBuilder(1024)
        GetPrivateProfileString(section, key, "", retVal, 1024, Me.path)
        Return retVal.ToString()
    End Function
End Class
