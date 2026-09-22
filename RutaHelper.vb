Imports System.IO
Imports System.Reflection
Imports System.Text

Public Module RutaHelper

    ''' <summary>
    ''' Devuelve la ruta donde está ubicada la DLL (la misma carpeta que contiene config.ini y certificados).
    ''' </summary>
    Public Function ObtenerRutaBaseDLL() As String
        Return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    End Function

    ''' <summary>
    ''' Devuelve la ruta completa al archivo config.ini que debe estar junto a la DLL.
    ''' </summary>
    Public Function ObtenerRutaConfigIni() As String
        Return Path.Combine(ObtenerRutaBaseDLL(), "config.ini")
    End Function

    ''' <summary>
    ''' Devuelve la ruta completa al archivo .p12 del certificado, que debe estar en la subcarpeta 'certificados'.
    ''' </summary>
    Public Function ObtenerRutaCertificado(nombreArchivo As String) As String
        Return Path.Combine(ObtenerRutaBaseDLL(), "certificados", nombreArchivo)
    End Function

    Public Function ObtenerRutaArchivo(nombreArchivo As String) As String
        Return IO.Path.Combine(IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), nombreArchivo)
    End Function

    Public Const UrlBaseDgiiProduccion As String = "https://ecf.dgii.gov.do/CerteCF/"
    Public Const UrlBaseDgiiCertificacion As String = "https://ecf.dgii.gov.do/TesteCF/"

    ''' <summary>Base URL DGII con barra final (autenticacion, consultadirectorio, etc.).</summary>
    Public Function NormalizarUrlBaseDgii(urlBase As String) As String
        Dim u = If(urlBase, "").Trim()
        If u = "" Then Return ""
        If Not u.EndsWith("/", StringComparison.Ordinal) Then u &= "/"
        Return u
    End Function

    ''' <summary>Solo dígitos; quita guiones y espacios del RNC.</summary>
    Public Function NormalizarRnc(rnc As String) As String
        If String.IsNullOrWhiteSpace(rnc) Then Return ""
        Dim sb As New StringBuilder()
        For Each c As Char In rnc.Trim()
            If Char.IsDigit(c) Then sb.Append(c)
        Next
        Return sb.ToString()
    End Function

End Module
