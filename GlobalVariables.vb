Imports System.Configuration
Imports System.Drawing

Public Class GlobalVariables

    Public Shared pathCertificado As String
    Public Shared passCertificado As String
    Public Shared url_base As String
    Public Shared urlfc_base As String
    Public Shared emp_rnc As String
    Public Shared token As String = String.Empty
    Public Shared urlClienteRecepcion As String = ""


    ' Método para cargar configuración desde App.config
    Public Shared Sub CargarConfiguracion()
        pathCertificado = ConfigurationManager.AppSettings("pathCertificado")
        passCertificado = ConfigurationManager.AppSettings("passCertificado")
        url_base = ConfigurationManager.AppSettings("url_base")
        urlfc_base = ConfigurationManager.AppSettings("urlfc_base")
        emp_rnc = ConfigurationManager.AppSettings("emp_rnc")
    End Sub

End Class

