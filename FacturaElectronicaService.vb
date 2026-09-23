' FacturaElectronicaService.vb
Imports System.Xml
Imports System.IO
Imports System.Security.Cryptography.X509Certificates
Imports System.Net
Imports QRCoder.PayloadGenerator.SwissQrCode
Imports System.Security.Cryptography
Imports System.Text
Imports System.Data
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Net.Security
Imports System.Threading.Tasks
Imports System.Xml.Schema
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.Security.Cryptography.Xml
Imports Reference = System.Security.Cryptography.Xml.Reference
Imports System.Runtime.InteropServices
Imports System.Threading

<ClassInterface(ClassInterfaceType.AutoDual)>
<ComVisible(True)>
Public Class FacturaElectronicaService
    Public Shared glbTipoeCF As String = ""
    Public Shared glbncfEnvia As String
    Private Shared cadenaConexion As String
    ''' <summary>RNC configurado con ConfigurarPorRnc; vacío = solo sección [general] del INI.</summary>
    Private Shared rncConfigActual As String = ""
    Private Shared configCargada As Boolean = False
    Private Shared isPOS As Boolean = False
    ''' <summary>Flujo alterno (mismas claves que POS: ticket, usu_codigo, caja) con vistas/tablas RESBAR en BD.</summary>
    Private Shared isResBar As Boolean = False
    ''' <summary>ADO.NET usa 30 s por defecto; GenerarXML y vistas pesadas o red lenta (p. ej. RDP/servidor remoto) superan ese lÃ­mite.</summary>
    Private Const SqlCommandTimeoutSegundos As Integer = 120

    ''' <summary>
    ''' EnvÃ­a un documento a LUGANIS a partir de su contenido XML.
    ''' Devuelve un JSON (como String) para facilitar el consumo desde Delphi.
    ''' El JSON incluye: ok, message, trackId, filename, txtContent (opcional), rawResponse (opcional).
    ''' </summary>
    Public Function EnviarDocumentoALuganis(
        xmlContent As String,
        baseUrl As String,
        companyCode As String,
        username As String,
        password As String,
        appVersion As String,
        os As String,
        deviceId As String,
        latitude As String,
        longitude As String,
        providerIpAddress As String,
        Optional saveGeneratedTxt As Boolean = False,
        Optional outputFolder As String = Nothing
    ) As String
        ' Estructura base de respuesta para garantizar que siempre devolvemos un JSON bien formado.
        Dim respuestaBase = New With {
            .ok = False,
            .message = "",
            .trackId = CType(Nothing, String),
            .filename = CType(Nothing, String),
            .txtContent = CType(Nothing, String),
            .rawResponse = CType(Nothing, String)
        }

        Try
            If String.IsNullOrWhiteSpace(xmlContent) Then
                Dim res = respuestaBase
                Dim resError = New With {
                    .ok = False,
                    .message = "xmlContent no puede estar vacÃ­o.",
                    .trackId = res.trackId,
                    .filename = res.filename,
                    .txtContent = res.txtContent,
                    .rawResponse = res.rawResponse
                }
                Return JsonConvert.SerializeObject(resError)
            End If

            ' Cargar el XML recibido.
            Dim xmlDoc As New XmlDocument() With {
                .PreserveWhitespace = True
            }
            xmlDoc.LoadXml(xmlContent)

            ' Extraer datos mÃ­nimos desde el XML para LUGANIS.
            ' TODO: ajustar XPath/nodos exactos segÃºn la estructura final del XML ECF.
            Dim tipoECF As String = ExtraerValorNodo(xmlDoc, "//TipoeCF")
            Dim rncEmisor As String = ExtraerValorNodo(xmlDoc, "//Emisor/RNCEmisor")
            Dim eNCF As String = ExtraerValorNodo(xmlDoc, "//IdDoc/eNCF")

            If String.IsNullOrWhiteSpace(tipoECF) Then
                Dim resError = New With {
                    .ok = False,
                    .message = "No se pudo determinar el TipoeCF desde el XML.",
                    .trackId = CType(Nothing, String),
                    .filename = CType(Nothing, String),
                    .txtContent = CType(Nothing, String),
                    .rawResponse = CType(Nothing, String)
                }
                Return JsonConvert.SerializeObject(resError)
            End If

            If String.IsNullOrWhiteSpace(rncEmisor) Then
                ' Aunque el RNC emisor es crÃ­tico para el filename, preferimos
                ' devolver un mensaje claro en lugar de lanzar excepciÃ³n directa.
                Dim resError = New With {
                    .ok = False,
                    .message = "No se pudo determinar el RNC del emisor desde el XML.",
                    .trackId = CType(Nothing, String),
                    .filename = CType(Nothing, String),
                    .txtContent = CType(Nothing, String),
                    .rawResponse = CType(Nothing, String)
                }
                Return JsonConvert.SerializeObject(resError)
            End If

            ' Preparar la solicitud de login a LUGANIS con los datos recibidos desde Delphi.
            Dim loginRequest As New LuganisLoginRequest With {
                .BaseUrl = baseUrl,
                .CompanyCode = companyCode,
                .Username = username,
                .Password = password,
                .AppVersion = appVersion,
                .Os = os,
                .DeviceId = deviceId,
                .Latitude = latitude,
                .Longitude = longitude,
                .ProviderIpAddress = providerIpAddress
            }

            ' Orquestar el envÃ­o usando el servicio interno de integraciÃ³n.
            ' NOTA: este mÃ©todo es sÃ­ncrono para ser compatible con Delphi/COM,
            ' por lo que esperamos explÃ­citamente el resultado de la tarea asÃ­ncrona.
            Dim resultado As LuganisResult = LuganisIntegrationService.EnviarAsync(
                xmlDoc:=xmlDoc,
                tipoECF:=tipoECF,
                rncEmisor:=rncEmisor,
                eNCF:=eNCF,
                loginRequest:=loginRequest
            ).GetAwaiter().GetResult()

            ' Guardar el TXT generado en disco si se solicita.
            If saveGeneratedTxt AndAlso Not String.IsNullOrEmpty(resultado.TxtContent) Then
                Dim carpetaSalida As String = outputFolder
                If String.IsNullOrWhiteSpace(carpetaSalida) Then
                    ' Si no se especifica carpeta, usamos la misma carpeta donde se guardan los XML (carpeta de la DLL).
                    carpetaSalida = RutaHelper.ObtenerRutaBaseDLL()
                End If

                If Not Directory.Exists(carpetaSalida) Then
                    Directory.CreateDirectory(carpetaSalida)
                End If

                Dim nombreArchivo As String = If(String.IsNullOrWhiteSpace(resultado.FileName),
                                                 "Luganis_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".txt",
                                                 resultado.FileName)
                Dim rutaCompleta As String = Path.Combine(carpetaSalida, nombreArchivo)

                Dim utf8SinBom As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False)
                File.WriteAllText(rutaCompleta, resultado.TxtContent, utf8SinBom)
            End If

            ' Construir el JSON de respuesta para Delphi.
            Dim respuestaOk = New With {
                .ok = resultado.Success,
                .message = resultado.Mensaje,
                .trackId = resultado.TrackId,
                .filename = resultado.FileName,
                .txtContent = resultado.TxtContent,
                .rawResponse = resultado.RawResponse
            }

            Return JsonConvert.SerializeObject(respuestaOk)

        Catch ex As Exception
            ' Capturamos cualquier excepciÃ³n y la devolvemos en el JSON.
            Dim respuestaError = New With {
                .ok = False,
                .message = "ExcepciÃ³n en EnviarDocumentoALuganis: " & ex.Message,
                .trackId = CType(Nothing, String),
                .filename = CType(Nothing, String),
                .txtContent = CType(Nothing, String),
                .rawResponse = CType(Nothing, String)
            }
            Return JsonConvert.SerializeObject(respuestaError)
        End Try
    End Function

    ''' <summary>
    ''' Helper interno para extraer el texto de un nodo XML usando XPath.
    ''' Devuelve String.Empty si el nodo no existe.
    ''' </summary>
    Private Shared Function ExtraerValorNodo(doc As XmlDocument, xpath As String) As String
        If doc Is Nothing OrElse String.IsNullOrWhiteSpace(xpath) Then
            Return String.Empty
        End If

        Try
            Dim nodo As XmlNode = doc.SelectSingleNode(xpath)
            If nodo Is Nothing OrElse String.IsNullOrWhiteSpace(nodo.InnerText) Then
                Return String.Empty
            End If
            Return nodo.InnerText.Trim()
        Catch
            ' Si el XPath es invÃ¡lido o cualquier otro error, devolvemos vacÃ­o para no romper el flujo.
            Return String.Empty
        End Try
    End Function


    ''' <summary>
    ''' Consulta el estado de una factura electrÃ³nica en la DGII usando el trackId.
    ''' </summary>
    ''' <param name="trackId">Identificador de seguimiento retornado al enviar la factura.</param>
    ''' <returns>Respuesta de la API en formato JSON o XML segÃºn la configuraciÃ³n.</returns>
    Public Function ConsultarEstadoFacturaSimple(ByVal trackId As String) As String
        Try
            ' Asegurar que la configuraciÃ³n y token estÃ©n listos
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            ' Construir la URL de consulta
            Dim urlConsulta As String = GlobalVariables.url_base &
            "consultaresultado/api/consultas/estado?trackid=" & trackId

            ' Crear la solicitud HTTP
            Dim request As HttpWebRequest = CType(WebRequest.Create(urlConsulta), HttpWebRequest)
            request.Method = "GET"
            request.Headers.Add("Authorization", "Bearer " & GlobalVariables.token)

            ' Ejecutar y leer la respuesta
            Using response As HttpWebResponse = CType(request.GetResponse(), HttpWebResponse)
                Using reader As New StreamReader(response.GetResponseStream())
                    Return reader.ReadToEnd()
                End Using
            End Using
            '  ProcesarRespuestaAPI(RncCliente, tipoECF, cadenaConexion, emp, suc, respuestaConsulta, facNumero, xmlFacturaFirmada, codigoSeguridad, facForma, tfaCodigo, "", fechaFirma)

        Catch ex As WebException
            ' Si hay error de red, intentar leer el mensaje de error de la API
            If ex.Response IsNot Nothing Then
                Using reader As New StreamReader(ex.Response.GetResponseStream())
                    Return "Error API: " & reader.ReadToEnd()
                End Using
            End If
            Return "Error de red: " & ex.Message

        Catch ex As Exception
            Return "Error general: " & ex.Message
        End Try
    End Function


    ''' <summary>
    ''' Reconsulta el estado de una factura en la DGII por trackId y actualiza en BD (AceptadoDGII, Error_DGII, bitÃ¡cora).
    ''' Desde Delphi solo se llama este mÃ©todo; la DLL hace la consulta, el parseo y la actualizaciÃ³n.
    ''' </summary>
    ''' <param name="emp">CÃ³digo empresa</param>
    ''' <param name="suc">CÃ³digo sucursal</param>
    ''' <param name="facNumero">NÃºmero de factura</param>
    ''' <param name="facForma">Forma de factura</param>
    ''' <param name="tfaCodigo">CÃ³digo tipo factura</param>
    ''' <param name="trackId">TrackId retornado al enviar la factura a DGII</param>
    ''' <param name="tipoECF">Tipo e-CF (31, 32, 33, 34, 41, 43, 44, 45, 46, 47)</param>
    ''' <param name="supCodigo">Opcional; para compras (41, 47)</param>
    ''' <param name="isNotaCredito">Opcional; para tipo 34 si es nota de crÃ©dito</param>
    ''' <param name="usuCodigoTicket">Opcional; si viene con <paramref name="cajaTicket"/>, la reconsulta actualiza Montos_Ticket o Factura_RestBar (no Facturas).</param>
    ''' <param name="cajaTicket">Opcional; caja del ticket POS / RestBar.</param>
    ''' <param name="esRestBarDocumento">Si true y flujo ticket, se usa dbo.SP_UPDATEDATOSDGIRESBAR; si false, dbo.SP_UPDATEDATOSDGIPOS.</param>
    ''' <returns>"Aceptado", "Rechazado", "En proceso", o "Error: mensaje"</returns>
    <ComVisible(True)>
    Public Function ReconsultarEstadoFacturaDGII(
            emp As String,
            suc As String,
            facNumero As String,
            facForma As String,
            tfaCodigo As String,
            trackId As String,
            tipoECF As String,
            Optional supCodigo As String = "",
            Optional isNotaCredito As Boolean = False,
            Optional usuCodigoTicket As String = "",
            Optional cajaTicket As String = "",
            Optional esRestBarDocumento As Boolean = False) As String
        Try
            If String.IsNullOrWhiteSpace(trackId) Then
                Return Helper.ConstruirJsonRespuestaOperacion(
                    New Helper.DgiiEstadoRespuesta With {.Estado = "Error", .Mensaje = "trackId es requerido.", .Aceptado = False})
            End If
            CargarConfiguracion()
            Seguridad.ObtenerToken()
            Dim token As String = GlobalVariables.token
            Dim respuestaConsulta As String = ConsultarEstadoConReintentos(trackId, token)
            Dim estadoResultante As String
            If Not String.IsNullOrWhiteSpace(usuCodigoTicket) AndAlso Not String.IsNullOrWhiteSpace(cajaTicket) Then
                estadoResultante = ProcesarRespuestaAPISoloEstadoTicket(
                    cadenaConexion, emp, suc, facNumero, usuCodigoTicket, cajaTicket,
                    respuestaConsulta, esRestBarDocumento)
            Else
                estadoResultante = ProcesarRespuestaAPISoloEstado(
                    cadenaConexion, emp, suc, facNumero,
                    If(facForma, ""), If(tfaCodigo, ""), If(tipoECF, ""),
                    respuestaConsulta, If(supCodigo, ""), isNotaCredito)
            End If
            Return estadoResultante
        Catch ex As Exception
            Helper.RegistrarLogCliente("ReconsultarEstadoFacturaDGII EX: " & ex.ToString())
            Return Helper.ConstruirJsonRespuestaOperacion(
                New Helper.DgiiEstadoRespuesta With {.Estado = "Error", .Mensaje = ex.Message, .Aceptado = False},
                ex.ToString())
        End Try
    End Function


    ''' <summary>
    ''' MÃ©todo principal para enviar una factura electrÃ³nica desde Delphi
    ''' </summary>
    ''' <param name="emp">ID de la empresa</param>
    ''' <param name="suc">ID de la sucursal</param>
    ''' <param name="facNumero">NÃºmero de factura</param>
    ''' <param name="empRnc">RNC de la empresa</param>
    ''' <param name="facForma">Forma de factura</param>
    ''' <param name="tfaCodigo">CÃ³digo de tipo de factura</param>
    ''' <param name="tipoECF">Tipo de comprobante e-CF (ej. 31, 33, 44)</param>

    Public Function EnviarFacturaElectronica(
                                             emp As String,
                                             suc As String,
                                             facNumero As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Dim xmlFactura As XmlDocument
            Seguridad.ObtenerToken()

            Dim jsonYaEnviado = IntentarReusarEnvioExistenteDgii(
                emp, suc, facNumero, facForma, tfaCodigo, tipoECF, False)
            If Not String.IsNullOrWhiteSpace(jsonYaEnviado) Then
                Return jsonYaEnviado
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     If(String.IsNullOrEmpty(tfaCodigo), 0, Convert.ToInt32(tfaCodigo)),
                                                     facForma,
                                                     Convert.ToInt32(facNumero))

            glbncfEnvia = nuevoENCF

            If tipoECF = "44" Then
                ' Paso 1: Obtener el XML de la factura 
                xmlFactura = GenerarXML(nuevoENCF, cadenaConexion, facNumero, emp, suc, facForma, tfaCodigo, "", "") ' MÃ©todo que obtendrÃ¡ el XML de la factura de la BD

            ElseIf tipoECF = "45" Then
                ' Paso 1: Obtener el XML de la factura 
                xmlFactura = GenerarXML(nuevoENCF, cadenaConexion, facNumero, emp, suc, facForma, tfaCodigo, "", "") ' MÃ©todo que obtendrÃ¡ el XML de la factura de la BD


            ElseIf tipoECF = "46" Then
                ' Paso 1: Obtener el XML de la factura 
                xmlFactura = GenerarXMLE46(nuevoENCF, cadenaConexion, facNumero, emp, suc, facForma, tfaCodigo) ' MÃ©todo que obtendrÃ¡ el XML de la factura de la BD

            Else
                ' Paso 1: Obtener el XML de la factura 
                xmlFactura = GenerarXML(nuevoENCF, cadenaConexion, facNumero, emp, suc, facForma, tfaCodigo, "", "") ' MÃ©todo que obtendrÃ¡ el XML de la factura de la BD

            End If

            ' Paso 2: Firmar el XML de la factura
            '  Dim pathCert As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GlobalVariables.pathCertificado)
            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado

            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFactura(xmlFactura, pathCert, passCert, glbncfEnvia, empRnc)
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)

            ' Paso 3: Obtener el token 
            Dim token As String = GlobalVariables.token

            ' Paso 4: Enviar el XML firmado
            Dim trackId As String = EnviarXMLFacturaFirmado(xmlFacturaFirmada, token, glbncfEnvia, empRnc)

            ' Paso 5: Consultar el estado de la factura
            Dim respuestaConsulta As String = ConsultarEstadoConReintentos(trackId, token)
            ProcesarRespuestaAPI(RncCliente, tipoECF, cadenaConexion, emp, suc, respuestaConsulta, facNumero, xmlFacturaFirmada, codigoSeguridad, facForma, tfaCodigo, "", fechaFirma, False)


            Dim parseada = Helper.ParsearRespuestaDgii(respuestaConsulta)
            Return Helper.ConstruirJsonRespuestaOperacion(parseada, respuestaConsulta)
        Catch ex As Exception
            Return Helper.ConstruirJsonRespuestaOperacion(
                New Helper.DgiiEstadoRespuesta With {.Estado = "Error", .Mensaje = ex.Message, .Aceptado = False},
                ex.ToString())
        End Try
    End Function

    ''' <summary>
    ''' EnvÃ­a una factura electrÃ³nica a LUGANIS utilizando los mismos parÃ¡metros de negocio
    ''' que el flujo actual, generando internamente el XML, construyendo el TXT y llamando
    ''' a los servicios de LUGANIS. Devuelve siempre un JSON serializado como String.
    ''' </summary>
    Public Function EnviarFacturaElectronicaLuganis(
        emp As String,
        suc As String,
        facNumero As String,
        empRnc As String,
        eNCF As String,
        RncCliente As String,
        baseUrl As String,
        companyCode As String,
        username As String,
        password As String,
        appVersion As String,
        os As String,
        deviceId As String,
        latitude As String,
        longitude As String,
        providerIpAddress As String,
        Optional facForma As String = "",
        Optional tfaCodigo As String = "",
        Optional tipoECF As String = "",
        Optional saveGeneratedTxt As Boolean = False,
        Optional outputFolder As String = "",
        Optional isNotaCredito As Boolean = False,
        Optional supCodigo As String = "",
        Optional logDebug As Boolean = False
    ) As String

        Try
            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] === Inicio EnviarFacturaElectronicaLuganis ===")

            ' Validaciones mÃ­nimas de parÃ¡metros crÃ­ticos de negocio.
            If String.IsNullOrWhiteSpace(emp) OrElse
               String.IsNullOrWhiteSpace(suc) OrElse
               String.IsNullOrWhiteSpace(facNumero) OrElse
               String.IsNullOrWhiteSpace(empRnc) OrElse
               String.IsNullOrWhiteSpace(tipoECF) Then

                Dim errorMsg As String = "ParÃ¡metros obligatorios faltantes: emp, suc, facNumero, empRnc y tipoECF son requeridos."
                Dim errorJson = ConstruirJsonLuganis(
                    ok:=False,
                    message:=errorMsg,
                    trackId:=Nothing,
                    filename:=Nothing,
                    txtContent:=Nothing,
                    rawResponse:=Nothing,
                    errorDetail:=errorMsg,
                    savedFilePath:=Nothing
                )
                Return errorJson
            End If
            ' Validaciones de configuraciÃ³n de LUGANIS.
            If String.IsNullOrWhiteSpace(baseUrl) OrElse String.IsNullOrWhiteSpace(companyCode) Then
                Dim errorMsg As String = "ParÃ¡metros de configuraciÃ³n LUGANIS incompletos (baseUrl y companyCode son requeridos)."
                Dim errorJson = ConstruirJsonLuganis(
                    ok:=False,
                    message:=errorMsg,
                    trackId:=Nothing,
                    filename:=Nothing,
                    txtContent:=Nothing,
                    rawResponse:=Nothing,
                    errorDetail:=errorMsg,
                    savedFilePath:=Nothing
                )
                Return errorJson
            End If

            ' Validaciones de credenciales y metadatos de dispositivo / app.
            If String.IsNullOrWhiteSpace(username) OrElse String.IsNullOrWhiteSpace(password) Then
                Dim errorMsg As String = "Credenciales de LUGANIS incompletas (username y password son requeridos)."
                Dim errorJson = ConstruirJsonLuganis(
                    ok:=False,
                    message:=errorMsg,
                    trackId:=Nothing,
                    filename:=Nothing,
                    txtContent:=Nothing,
                    rawResponse:=Nothing,
                    errorDetail:=errorMsg,
                    savedFilePath:=Nothing
                )
                Return errorJson
            End If

            If String.IsNullOrWhiteSpace(appVersion) OrElse
               String.IsNullOrWhiteSpace(os) OrElse
               String.IsNullOrWhiteSpace(deviceId) OrElse
               String.IsNullOrWhiteSpace(latitude) OrElse
               String.IsNullOrWhiteSpace(longitude) OrElse
               String.IsNullOrWhiteSpace(providerIpAddress) Then

                Dim errorMsg As String = "Metadatos de dispositivo incompletos (appVersion, os, deviceId, latitude, longitude y providerIpAddress son requeridos)."
                Dim errorJson = ConstruirJsonLuganis(
                    ok:=False,
                    message:=errorMsg,
                    trackId:=Nothing,
                    filename:=Nothing,
                    txtContent:=Nothing,
                    rawResponse:=Nothing,
                    errorDetail:=errorMsg,
                    savedFilePath:=Nothing
                )
                Return errorJson
            End If

            Dim loginRequestPre As New LuganisLoginRequest With {
                .BaseUrl = baseUrl,
                .CompanyCode = companyCode,
                .Username = username,
                .Password = password,
                .AppVersion = appVersion,
                .Os = os,
                .DeviceId = deviceId,
                .Latitude = latitude,
                .Longitude = longitude,
                .ProviderIpAddress = providerIpAddress
            }
            Dim jsonLugYaEnviado = IntentarReusarEnvioExistenteLuganis(
                emp, suc, facNumero, facForma, tfaCodigo, tipoECF, isNotaCredito, supCodigo,
                empRnc, loginRequestPre, logDebug)
            If Not String.IsNullOrWhiteSpace(jsonLugYaEnviado) Then
                Return jsonLugYaEnviado
            End If

            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] Paso 1: Generando XML (GenerarXmlFacturaParaLuganis)...")
            ' 1. Generar el XML de la factura utilizando la misma lÃ³gica DGII,
            ' pero aislada en un helper especÃ­fico para LUGANIS.
            Dim nuevoENCF As String = Nothing
            Dim xmlFactura As XmlDocument = GenerarXmlFacturaParaLuganis(
                emp:=emp,
                suc:=suc,
                facNumero:=facNumero,
                empRnc:=empRnc,
                facForma:=facForma,
                tfaCodigo:=tfaCodigo,
                tipoECF:=tipoECF,
                nuevoENCF:=nuevoENCF
            )
            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] Paso 1: XML generado OK. eNCF=" & If(nuevoENCF, "(nuevo)") & " / encfRecibido=" & eNCF)

            ' 2. Preparar la solicitud de login/configuraciÃ³n a LUGANIS.
            Dim loginRequest As New LuganisLoginRequest With {
                .BaseUrl = baseUrl,
                .CompanyCode = companyCode,
                .Username = username,
                .Password = password,
                .AppVersion = appVersion,
                .Os = os,
                .DeviceId = deviceId,
                .Latitude = latitude,
                .Longitude = longitude,
                .ProviderIpAddress = providerIpAddress
            }

            ' 3. Orquestar el envÃ­o a LUGANIS (bloqueante para ser compatible con Delphi/COM).
            ' Regla: si nuevoENCF fue generado correctamente, se usarÃ¡ para LUGANIS;
            ' de lo contrario, se cae de vuelta al eNCF recibido como parÃ¡metro.
            Dim encfParaLuganis As String = If(String.IsNullOrWhiteSpace(nuevoENCF), eNCF, nuevoENCF)
            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] Paso 2: Enviando a LUGANIS (Parse XMLâ†’TXTâ†’Loginâ†’Send)... encfParaLuganis=" & encfParaLuganis)

            Dim resultado As LuganisResult = LuganisIntegrationService.EnviarAsync(
                xmlDoc:=xmlFactura,
                tipoECF:=tipoECF,
                rncEmisor:=empRnc,
                eNCF:=encfParaLuganis,
                loginRequest:=loginRequest,
                logDebug:=logDebug
            ).GetAwaiter().GetResult()

            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] Paso 2: Respuesta LUGANIS - Success=" & resultado.Success & " TrackId=" & If(String.IsNullOrEmpty(resultado.TrackId), "(nulo)", resultado.TrackId) & " Msg=" & If(String.IsNullOrEmpty(resultado.Mensaje), "", resultado.Mensaje))

            ' 4. Guardar el TXT generado en disco, si se solicita.
            Dim savedFilePath As String = Nothing
            If saveGeneratedTxt AndAlso Not String.IsNullOrEmpty(resultado.TxtContent) Then
                Dim carpetaSalida As String = outputFolder
                If String.IsNullOrWhiteSpace(carpetaSalida) Then
                    ' Si no se especifica carpeta, usar la misma carpeta donde se guardan los XML (carpeta de la DLL).
                    carpetaSalida = RutaHelper.ObtenerRutaBaseDLL()
                End If

                If Not Directory.Exists(carpetaSalida) Then
                    Directory.CreateDirectory(carpetaSalida)
                End If

                Dim nombreArchivo As String = If(String.IsNullOrWhiteSpace(resultado.FileName),
                                                 "Luganis_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".txt",
                                                 resultado.FileName)
                Dim rutaCompleta As String = Path.Combine(carpetaSalida, nombreArchivo)

                Dim utf8SinBom As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False)
                File.WriteAllText(rutaCompleta, resultado.TxtContent, utf8SinBom)
                savedFilePath = rutaCompleta
                If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] Paso 3: TXT guardado en " & rutaCompleta)
            End If

            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] Paso 4: Actualizando BD (SP_UPDATEDATOSLUGANIS)...")
            ' Aceptado solo si LUGANIS confirmó. Pendiente ≠ error/rechazo.
            Dim esPendiente As Boolean = (Not resultado.AceptadoPorLuganis.HasValue) AndAlso resultado.Success AndAlso
                (String.Equals(If(resultado.Codigo, ""), "PENDIENTE", StringComparison.OrdinalIgnoreCase) OrElse
                 String.Equals(If(resultado.EstadoLuganis, ""), "Pendiente", StringComparison.OrdinalIgnoreCase) OrElse
                 String.IsNullOrWhiteSpace(resultado.Codigo) OrElse
                 String.Equals(If(resultado.Codigo, ""), "SIN_CONFIRMACION_ESTADO", StringComparison.OrdinalIgnoreCase))
            Dim esRechazoReal As Boolean = resultado.AceptadoPorLuganis.HasValue AndAlso Not resultado.AceptadoPorLuganis.Value
            Dim aceptadoParaBD As Boolean = resultado.AceptadoPorLuganis.HasValue AndAlso resultado.AceptadoPorLuganis.Value

            If aceptadoParaBD Then
                Helper.RegistrarLogCliente("[LUGANIS] ACEPTADO por LUGANIS/DGII. eNCF=" & encfParaLuganis & " trackId=" & If(resultado.TrackId, ""))
            ElseIf esPendiente Then
                Helper.RegistrarLogCliente("[LUGANIS] PENDIENTE (enviado OK; aún no final). NO se marca Error_Luganis. trackId=" & If(resultado.TrackId, "") &
                                          " | estado=" & If(resultado.EstadoLuganis, "") & " | " & If(resultado.Mensaje, ""))
            Else
                Helper.RegistrarLogCliente("[LUGANIS] NO ACEPTADO - motivo real: codigo=" & If(resultado.Codigo, "") &
                                          " | mensaje=" & If(resultado.Mensaje, "") &
                                          " | estado=" & If(resultado.EstadoLuganis, "") &
                                          " | trackId=" & If(resultado.TrackId, "(nulo)") &
                                          " | raw=" & If(String.IsNullOrWhiteSpace(resultado.RawResponse), "(vacío)",
                                                         If(resultado.RawResponse.Length > 1200, resultado.RawResponse.Substring(0, 1200) & "...", resultado.RawResponse)))
            End If

            ' Intentar obtener el QR solo cuando realmente fue aceptado.
            Dim qrCodeLuganis As String = Nothing
            If aceptadoParaBD Then
                Try
                    For intentoQr As Integer = 1 To 2
                        Dim qrJson As String = ObtenerQrLuganis(
                            baseUrl:=baseUrl,
                            companyCode:=companyCode,
                            username:=username,
                            password:=password,
                            appVersion:=appVersion,
                            os:=os,
                            deviceId:=deviceId,
                            latitude:=latitude,
                            longitude:=longitude,
                            providerIpAddress:=providerIpAddress,
                            rncEmisor:=empRnc,
                            eNCF:=encfParaLuganis
                        )

                        Dim jo = Newtonsoft.Json.Linq.JObject.Parse(qrJson)
                        Dim okQrToken = jo.SelectToken("ok")
                        If okQrToken IsNot Nothing AndAlso okQrToken.Type = Newtonsoft.Json.Linq.JTokenType.Boolean AndAlso CBool(okQrToken) Then
                            Dim qrToken = jo.SelectToken("qrCode")
                            If qrToken IsNot Nothing AndAlso qrToken.Type = Newtonsoft.Json.Linq.JTokenType.String AndAlso Not String.IsNullOrWhiteSpace(CStr(qrToken)) Then
                                qrCodeLuganis = CStr(qrToken)
                                Helper.RegistrarLogCliente("[LUGANIS] QR obtenido correctamente.")
                                Exit For
                            End If
                        End If
                        Helper.RegistrarLogCliente("[LUGANIS] No se pudo obtener QR (intento " & intentoQr & "): " & qrJson)
                        If intentoQr < 2 Then Threading.Thread.Sleep(1500)
                    Next
                Catch exQr As Exception
                    Helper.RegistrarLogCliente("[LUGANIS] Error al obtener QR: " & exQr.ToString())
                End Try
            End If

            Try
                ActualizarDatosLuganisEnBD(
                    cadenaConexion:=cadenaConexion,
                    tipoECF:=tipoECF,
                    emp:=emp,
                    suc:=suc,
                    facNumero:=facNumero,
                    encf:=encfParaLuganis,
                    trackId:=resultado.TrackId,
                    aceptado:=aceptadoParaBD,
                    filename:=resultado.FileName,
                    qrCode:=qrCodeLuganis,
                    facForma:=facForma,
                    tfaCodigo:=tfaCodigo,
                    supCodigo:=supCodigo,
                    isNotaCredito:=isNotaCredito,
                    mensajeRechazo:=If(esRechazoReal, resultado.Mensaje, If(esPendiente, "PENDIENTE: " & If(resultado.Mensaje, ""), Nothing)),
                    esPendiente:=esPendiente
                )
            Catch exSP As Exception
                Helper.RegistrarLogCliente("[LUGANIS] ERROR Paso 4 (SP): " & exSP.Message)
            End Try
            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] Paso 5: Construyendo JSON de respuesta...")

            Dim tipoDocLug As String = ResolverTipoDocumentoDesdeEcf(tipoECF, isNotaCredito)
            Dim okFinal As Boolean = resultado.Success AndAlso (aceptadoParaBD OrElse esPendiente)
            ' Pendiente no es rechazo; solo rechazo real o fallo de envío
            Dim rechazadoLug As Boolean = esRechazoReal OrElse (Not resultado.Success AndAlso Not esPendiente)
            Dim msgUsuario As String = ResolverMensajeUsuarioLuganis(
                cadenaConexion, resultado, aceptadoParaBD, tipoDocLug, emp, suc, facNumero, facForma, tfaCodigo, supCodigo)
            If esPendiente AndAlso String.IsNullOrWhiteSpace(msgUsuario) Then
                msgUsuario = "Documento enviado a LUGANIS; estado Pendiente (aún no Aceptado/Rechazado)."
            End If

            Dim errorDetail As String = Nothing
            If rechazadoLug Then
                If Not String.IsNullOrWhiteSpace(resultado.RawResponse) Then
                    errorDetail = resultado.RawResponse
                ElseIf Not String.IsNullOrWhiteSpace(resultado.Codigo) OrElse Not String.IsNullOrWhiteSpace(resultado.Mensaje) Then
                    errorDetail = $"{resultado.Codigo}: {resultado.Mensaje}"
                End If
            End If

            Dim jsonOk = ConstruirJsonLuganis(
                ok:=okFinal,
                message:=msgUsuario,
                trackId:=resultado.TrackId,
                filename:=resultado.FileName,
                txtContent:=resultado.TxtContent,
                rawResponse:=resultado.RawResponse,
                errorDetail:=errorDetail,
                savedFilePath:=savedFilePath,
                estadoLuganis:=resultado.EstadoLuganis,
                rechazado:=rechazadoLug,
                codigoError:=If(rechazadoLug, resultado.Codigo, If(esPendiente, "PENDIENTE", Nothing)),
                mensajeRechazo:=If(rechazadoLug, msgUsuario, Nothing)
            )

            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] === Fin OK ===")
            Return jsonOk

        Catch ex As Exception
            Helper.RegistrarLogCliente("[LUGANIS] EXCEPCION: " & ex.ToString())
            Dim jsonError = ConstruirJsonLuganis(
                ok:=False,
                message:="ExcepciÃ³n en EnviarFacturaElectronicaLuganis: " & ex.Message,
                trackId:=Nothing,
                filename:=Nothing,
                txtContent:=Nothing,
                rawResponse:=Nothing,
                errorDetail:=ex.ToString(),
                savedFilePath:=Nothing
            )
            Return jsonError
        End Try
    End Function

    ''' <summary>
    ''' EnvÃ­a una devoluciÃ³n (E34) a LUGANIS. Genera XML E34, construye TXT y actualiza BD.
    ''' Para devoluciones con ticket usar isDevolucionPOS=True.
    ''' </summary>
    <ComVisible(True)>
    Public Function EnviarDevolucionLuganis(
        emp As String,
        suc As String,
        devNumero As String,
        empRnc As String,
        eNCF As String,
        RncCliente As String,
        baseUrl As String,
        companyCode As String,
        username As String,
        password As String,
        appVersion As String,
        os As String,
        deviceId As String,
        latitude As String,
        longitude As String,
        providerIpAddress As String,
        Optional isDevolucionPOS As Boolean = False,
        Optional saveGeneratedTxt As Boolean = False,
        Optional outputFolder As String = "",
        Optional logDebug As Boolean = False
    ) As String
        Try
            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] === Inicio EnviarDevolucionLuganis ===")

            If String.IsNullOrWhiteSpace(emp) OrElse String.IsNullOrWhiteSpace(suc) OrElse String.IsNullOrWhiteSpace(devNumero) OrElse
               String.IsNullOrWhiteSpace(empRnc) OrElse String.IsNullOrWhiteSpace(baseUrl) OrElse String.IsNullOrWhiteSpace(companyCode) OrElse
               String.IsNullOrWhiteSpace(username) OrElse String.IsNullOrWhiteSpace(password) OrElse
               String.IsNullOrWhiteSpace(appVersion) OrElse String.IsNullOrWhiteSpace(os) OrElse String.IsNullOrWhiteSpace(deviceId) OrElse
               String.IsNullOrWhiteSpace(latitude) OrElse String.IsNullOrWhiteSpace(longitude) OrElse String.IsNullOrWhiteSpace(providerIpAddress) Then
                Dim errorMsg As String = "ParÃ¡metros obligatorios incompletos (emp, suc, devNumero, empRnc, credenciales LUGANIS y metadatos dispositivo)."
                Return ConstruirJsonLuganis(ok:=False, message:=errorMsg, trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=errorMsg, savedFilePath:=Nothing)
            End If

            CargarConfiguracion()
            Dim loginRequestPreDev As New LuganisLoginRequest With {
                .BaseUrl = baseUrl,
                .CompanyCode = companyCode,
                .Username = username,
                .Password = password,
                .AppVersion = appVersion,
                .Os = os,
                .DeviceId = deviceId,
                .Latitude = latitude,
                .Longitude = longitude,
                .ProviderIpAddress = providerIpAddress
            }
            Dim empDL As Integer, sucDL As Integer, numDL As Integer
            If Integer.TryParse(emp, empDL) AndAlso Integer.TryParse(suc, sucDL) AndAlso Integer.TryParse(devNumero, numDL) Then
                Dim jsonLugDev = IntentarReusarEnvioExistenteLuganis(
                    Helper.EncfDocumentoRef.Devolucion(empDL, sucDL, numDL),
                    "34", False, "", "", "", empRnc, loginRequestPreDev, logDebug)
                If Not String.IsNullOrWhiteSpace(jsonLugDev) Then Return jsonLugDev
            End If
            Dim nuevoENCF As String = LlamarAsignarSecuenciaDevolucionDGII(cadenaConexion, Convert.ToInt32(emp), Convert.ToInt32(suc), devNumero)
            If String.IsNullOrWhiteSpace(nuevoENCF) AndAlso Not String.IsNullOrWhiteSpace(eNCF) Then
                nuevoENCF = eNCF.Trim()
                Helper.RegistrarLogCliente("[LUGANIS] Devolución: SP no asignó eNCF; se usa eNCF recibido=" & nuevoENCF)
            End If
            If String.IsNullOrWhiteSpace(nuevoENCF) Then
                Dim errorMsg As String = "No se pudo asignar eNCF a la devolución " & devNumero &
                    " (AsignarSecuenciaDevolucionDGII vacío). No se enviará a LUGANIS para evitar error 3006 (nombre archivo ≠ trama)."
                Helper.RegistrarLogCliente("[LUGANIS] " & errorMsg)
                Return ConstruirJsonLuganis(ok:=False, message:=errorMsg, trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=errorMsg, savedFilePath:=Nothing)
            End If
            glbncfEnvia = nuevoENCF
            Helper.RegistrarLogCliente("[LUGANIS] Devolución eNCF a usar=" & nuevoENCF)

            isPOS = isDevolucionPOS
            Dim xmlFactura As XmlDocument = GenerarXMLE34(nuevoENCF, devNumero, emp, suc, False)
            isPOS = False

            Dim loginRequest As New LuganisLoginRequest With {
                .BaseUrl = baseUrl,
                .CompanyCode = companyCode,
                .Username = username,
                .Password = password,
                .AppVersion = appVersion,
                .Os = os,
                .DeviceId = deviceId,
                .Latitude = latitude,
                .Longitude = longitude,
                .ProviderIpAddress = providerIpAddress
            }

            Dim resultado As LuganisResult = LuganisIntegrationService.EnviarAsync(
                xmlDoc:=xmlFactura,
                tipoECF:="34",
                rncEmisor:=empRnc,
                eNCF:=nuevoENCF,
                loginRequest:=loginRequest,
                logDebug:=logDebug
            ).GetAwaiter().GetResult()

            Dim savedFilePath As String = Nothing
            If saveGeneratedTxt AndAlso Not String.IsNullOrEmpty(resultado.TxtContent) Then
                Dim carpetaSalida As String = outputFolder
                If String.IsNullOrWhiteSpace(carpetaSalida) Then carpetaSalida = RutaHelper.ObtenerRutaBaseDLL()
                If Not Directory.Exists(carpetaSalida) Then Directory.CreateDirectory(carpetaSalida)
                Dim nombreArchivo As String = If(String.IsNullOrWhiteSpace(resultado.FileName), "Luganis_E34_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".txt", resultado.FileName)
                Dim rutaCompleta As String = Path.Combine(carpetaSalida, nombreArchivo)
                Dim utf8SinBom As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False)
                File.WriteAllText(rutaCompleta, resultado.TxtContent, utf8SinBom)
                savedFilePath = rutaCompleta
            End If

            Dim esPendienteDev As Boolean = (Not resultado.AceptadoPorLuganis.HasValue) AndAlso resultado.Success
            Dim aceptadoParaBD As Boolean = If(resultado.AceptadoPorLuganis.HasValue, resultado.AceptadoPorLuganis.Value, False)
            Dim qrCodeLuganis As String = Nothing
            If aceptadoParaBD Then
                Try
                    Dim qrJson As String = ObtenerQrLuganis(baseUrl, companyCode, username, password, appVersion, os, deviceId, latitude, longitude, providerIpAddress, empRnc, nuevoENCF)
                    Dim jo = Newtonsoft.Json.Linq.JObject.Parse(qrJson)
                    If jo.SelectToken("ok") IsNot Nothing AndAlso CBool(jo("ok")) Then
                        Dim qrToken = jo.SelectToken("qrCode")
                        If qrToken IsNot Nothing Then qrCodeLuganis = CStr(qrToken)
                    End If
                Catch
                End Try
            End If

            Try
                ActualizarDatosLuganisEnBD(
                    cadenaConexion:=cadenaConexion,
                    tipoECF:="34",
                    emp:=emp,
                    suc:=suc,
                    facNumero:=devNumero,
                    encf:=nuevoENCF,
                    trackId:=resultado.TrackId,
                    aceptado:=aceptadoParaBD,
                    filename:=resultado.FileName,
                    qrCode:=qrCodeLuganis,
                    facForma:="",
                    tfaCodigo:="",
                    supCodigo:="",
                    isNotaCredito:=False,
                    mensajeRechazo:=If(Not aceptadoParaBD AndAlso Not esPendienteDev, resultado.Mensaje, If(esPendienteDev, "PENDIENTE: " & If(resultado.Mensaje, ""), Nothing)),
                    esPendiente:=esPendienteDev
                )
            Catch exSP As Exception
                Helper.RegistrarLogCliente("[LUGANIS] ERROR SP_UPDATEDATOSLUGANIS devoluciÃ³n: " & exSP.Message)
            End Try

            Dim okFinal As Boolean = resultado.Success AndAlso (aceptadoParaBD OrElse esPendienteDev)
            Dim msgUsuario As String = ResolverMensajeUsuarioLuganis(
                cadenaConexion, resultado, aceptadoParaBD, "DEVOLUCION", emp, suc, devNumero, "", "", "")
            Dim errorDetail As String = Nothing
            If Not okFinal Then
                If Not String.IsNullOrWhiteSpace(resultado.RawResponse) Then
                    errorDetail = resultado.RawResponse
                ElseIf Not String.IsNullOrWhiteSpace(resultado.Mensaje) Then
                    errorDetail = resultado.Mensaje
                End If
            End If
            Return ConstruirJsonLuganis(
                ok:=okFinal,
                message:=msgUsuario,
                trackId:=resultado.TrackId,
                filename:=resultado.FileName,
                txtContent:=resultado.TxtContent,
                rawResponse:=resultado.RawResponse,
                errorDetail:=errorDetail,
                savedFilePath:=savedFilePath,
                estadoLuganis:=resultado.EstadoLuganis,
                rechazado:=Not okFinal AndAlso Not esPendienteDev,
                codigoError:=If(Not okFinal, resultado.Codigo, If(esPendienteDev, "PENDIENTE", Nothing)),
                mensajeRechazo:=If(Not okFinal, msgUsuario, Nothing))
        Catch ex As Exception
            Helper.RegistrarLogCliente("[LUGANIS] EXCEPCION EnviarDevolucionLuganis: " & ex.ToString())
            Return ConstruirJsonLuganis(ok:=False, message:="ExcepciÃ³n en EnviarDevolucionLuganis.", trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=ex.ToString(), savedFilePath:=Nothing)
        End Try
    End Function

    ''' <summary>
    ''' Vigencia del .p12 configurado en config.ini.
    ''' Formato: OK|VENCIDO|DIAS|FECHA|TITULAR|RUTA|ERROR
    ''' OK/VENCIDO = 1 o 0. DIAS negativo = ya vencido. Fecha = yyyy-MM-dd.
    ''' </summary>
    <ComVisible(True)>
    Public Function ConsultarVigenciaCertificado() As String
        Dim pathCert As String = ""
        Dim cert As X509Certificate2 = Nothing
        Try
            Try
                CargarConfiguracion()
            Catch exCfg As Exception
                Return "0|0|0||| |No se pudo leer config.ini: " & exCfg.Message
            End Try

            Dim pathCfg = If(GlobalVariables.pathCertificado, "").Trim()
            If pathCfg = "" Then
                Return "0|0|0||||pathCertificado vacío en config.ini"
            End If

            If Path.IsPathRooted(pathCfg) Then
                pathCert = pathCfg
            Else
                pathCert = ObtenerRutaArchivo(pathCfg)
            End If

            If Not File.Exists(pathCert) Then
                Return "0|0|0|||" & pathCert & "|Certificado no encontrado: " & pathCert
            End If

            Dim pass = If(GlobalVariables.passCertificado, "")
            Try
                cert = New X509Certificate2(pathCert, pass, X509KeyStorageFlags.Exportable)
            Catch exCert As Exception
                Return "0|0|0|||" & pathCert & "|No se pudo abrir el .p12 (clave o archivo): " & exCert.Message
            End Try

            Dim vencido As Boolean = (DateTime.Now > cert.NotAfter)
            Dim dias As Integer = CInt(Math.Floor((cert.NotAfter.Date - DateTime.Now.Date).TotalDays))
            Dim titular = If(cert.GetNameInfo(X509NameType.SimpleName, False), cert.Subject)
            titular = titular.Replace("|"c, " "c)
            Dim errTxt = If(vencido, "VENCIDO", "")
            Return "1|" &
                   (If(vencido, "1", "0")) & "|" &
                   dias.ToString() & "|" &
                   cert.NotAfter.ToString("yyyy-MM-dd") & "|" &
                   titular & "|" &
                   pathCert & "|" &
                   errTxt
        Catch ex As Exception
            Return "0|0|0|||" & pathCert & "|" & ex.Message
        Finally
            If cert IsNot Nothing Then
                cert.Dispose()
            End If
        End Try
    End Function

    ''' <summary>
    ''' Obtiene la URL del cÃ³digo QR desde LUGANIS para un documento ya enviado.
    ''' Devuelve JSON con: ok, qrCode, message, rawResponse, errorDetail.
    ''' El campo qrCode se guarda en la tabla SQL para mostrar el QR en la factura.
    ''' </summary>
    Public Function ObtenerQrLuganis(
        baseUrl As String,
        companyCode As String,
        username As String,
        password As String,
        appVersion As String,
        os As String,
        deviceId As String,
        latitude As String,
        longitude As String,
        providerIpAddress As String,
        rncEmisor As String,
        eNCF As String
    ) As String
        Try
            If String.IsNullOrWhiteSpace(baseUrl) OrElse String.IsNullOrWhiteSpace(companyCode) OrElse
               String.IsNullOrWhiteSpace(username) OrElse String.IsNullOrWhiteSpace(password) OrElse
               String.IsNullOrWhiteSpace(rncEmisor) OrElse String.IsNullOrWhiteSpace(eNCF) Then
                Return JsonConvert.SerializeObject(New With {
                    .ok = False,
                    .qrCode = CType(Nothing, String),
                    .message = "ParÃ¡metros obligatorios: baseUrl, companyCode, username, password, rncEmisor y eNCF.",
                    .rawResponse = CType(Nothing, String),
                    .errorDetail = CType(Nothing, String)
                })
            End If

            If String.IsNullOrWhiteSpace(appVersion) OrElse String.IsNullOrWhiteSpace(os) OrElse String.IsNullOrWhiteSpace(deviceId) OrElse
               String.IsNullOrWhiteSpace(latitude) OrElse String.IsNullOrWhiteSpace(longitude) OrElse String.IsNullOrWhiteSpace(providerIpAddress) Then
                Return JsonConvert.SerializeObject(New With {
                    .ok = False,
                    .qrCode = CType(Nothing, String),
                    .message = "Metadatos de dispositivo requeridos: appVersion, os, deviceId, latitude, longitude, providerIpAddress.",
                    .rawResponse = CType(Nothing, String),
                    .errorDetail = CType(Nothing, String)
                })
            End If

            Dim loginRequest As New LuganisLoginRequest With {
                .BaseUrl = baseUrl,
                .CompanyCode = companyCode,
                .Username = username,
                .Password = password,
                .AppVersion = appVersion,
                .Os = os,
                .DeviceId = deviceId,
                .Latitude = latitude,
                .Longitude = longitude,
                .ProviderIpAddress = providerIpAddress
            }

            Dim loginResp As LuganisLoginResponse = LuganisApiClient.LoginAsync(loginRequest).GetAwaiter().GetResult()
            If Not loginResp.Success OrElse String.IsNullOrWhiteSpace(loginResp.Token) Then
                Return JsonConvert.SerializeObject(New With {
                    .ok = False,
                    .qrCode = CType(Nothing, String),
                    .message = "No se pudo autenticar en LUGANIS: " & If(loginResp.ErrorMessage, "sin detalle"),
                    .rawResponse = loginResp.RawResponse,
                    .errorDetail = loginResp.ErrorMessage
                })
            End If

            Dim qrRequest As New LuganisQrRequest With {
                .BaseUrl = baseUrl,
                .Token = loginResp.Token,
                .DeviceId = deviceId,
                .RncEmisor = rncEmisor,
                .ENcf = eNCF
            }

            Dim qrResp As LuganisQrResponse = LuganisApiClient.DownloadQrAsync(qrRequest).GetAwaiter().GetResult()
            If Not qrResp.Success Then
                Return JsonConvert.SerializeObject(New With {
                    .ok = False,
                    .qrCode = CType(Nothing, String),
                    .message = If(qrResp.ErrorMessage, "Error al obtener QR."),
                    .rawResponse = qrResp.RawResponse,
                    .errorDetail = qrResp.ErrorMessage
                })
            End If

            Return JsonConvert.SerializeObject(New With {
                .ok = True,
                .qrCode = qrResp.QrCode,
                .message = "CÃ³digo QR obtenido correctamente.",
                .rawResponse = qrResp.RawResponse,
                .errorDetail = CType(Nothing, String)
            })
        Catch ex As Exception
            Return JsonConvert.SerializeObject(New With {
                .ok = False,
                .qrCode = CType(Nothing, String),
                .message = "ExcepciÃ³n al obtener QR de LUGANIS.",
                .rawResponse = CType(Nothing, String),
                .errorDetail = ex.ToString()
            })
        End Try
    End Function

    ''' <summary>
    ''' Actualiza las tablas con datos LUGANIS llamando a SP_UPDATEDATOSLUGANIS.
    ''' SegÃºn tipoECF determina el tipo_documento (FACTURA, COMPRA, etc.) igual que el flujo DGII.
    ''' </summary>
    Private Shared Sub ActualizarDatosLuganisEnBD(
        cadenaConexion As String,
        tipoECF As String,
        emp As String,
        suc As String,
        facNumero As String,
        encf As String,
        trackId As String,
        aceptado As Boolean,
        filename As String,
        qrCode As String,
        facForma As String,
        tfaCodigo As String,
        supCodigo As String,
        isNotaCredito As Boolean,
        Optional mensajeRechazo As String = Nothing,
        Optional esPendiente As Boolean = False
    )
        If String.IsNullOrWhiteSpace(cadenaConexion) Then Return
        Dim tipoDoc As String = ResolverTipoDocumentoDesdeEcf(tipoECF, isNotaCredito)

        ' Intentar extraer FechaFirma y CodigoSeguridad desde el QR (si viene con URL DGII)
        Dim fechaFirmaDesdeQr As Nullable(Of DateTime) = Nothing
        Dim codigoSegDesdeQr As String = Nothing
        If Not String.IsNullOrWhiteSpace(qrCode) Then
            Try
                Dim q As String = qrCode
                Dim idx As Integer = q.IndexOf("?"c)
                If idx >= 0 AndAlso idx < q.Length - 1 Then
                    q = q.Substring(idx + 1)
                End If

                Dim pares = q.Split("&"c)
                For Each par In pares
                    If String.IsNullOrWhiteSpace(par) Then Continue For
                    Dim kv = par.Split("="c)
                    If kv.Length <> 2 Then Continue For
                    Dim clave = Uri.UnescapeDataString(kv(0)).Trim()
                    Dim valor = Uri.UnescapeDataString(kv(1)).Trim()

                    If clave.Equals("CodigoSeguridad", StringComparison.OrdinalIgnoreCase) Then
                        If Not String.IsNullOrWhiteSpace(valor) Then
                            codigoSegDesdeQr = valor
                        End If
                    ElseIf clave.Equals("FechaFirma", StringComparison.OrdinalIgnoreCase) Then
                        If Not String.IsNullOrWhiteSpace(valor) Then
                            Dim dt As DateTime
                            ' Formato tÃ­pico: 11-03-2026 13:45:43
                            If DateTime.TryParseExact(valor, "dd-MM-yyyy HH:mm:ss", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, dt) _
                               OrElse DateTime.TryParse(valor, dt) Then
                                fechaFirmaDesdeQr = dt
                            End If
                        End If
                    End If
                Next
            Catch ex As Exception
                Helper.RegistrarLogCliente("[LUGANIS] No se pudo parsear QR para fecha/cÃ³digo: " & ex.Message)
            End Try
        End If

        Dim p As SqlParameter() = {
            New SqlParameter("@tipo_documento", tipoDoc),
            New SqlParameter("@emp_codigo", CInt(emp)),
            New SqlParameter("@suc_codigo", CInt(suc)),
            New SqlParameter("@fac_numero", facNumero),
            New SqlParameter("@eNCF", If(encf, "")),
            New SqlParameter("@trackId", If(trackId, "")),
            New SqlParameter("@aceptado", If(aceptado, 1, 0)),
            New SqlParameter("@filename", If(filename, "")),
            New SqlParameter("@qrCode", If(String.IsNullOrEmpty(qrCode), CObj(DBNull.Value), CObj(qrCode))),
            New SqlParameter("@fac_forma", If(String.IsNullOrEmpty(facForma), CObj(DBNull.Value), CObj(facForma))),
            New SqlParameter("@tfa_codigo", If(String.IsNullOrEmpty(tfaCodigo), CObj(DBNull.Value), CObj(tfaCodigo))),
            New SqlParameter("@sup_codigo", If(String.IsNullOrEmpty(supCodigo), CObj(DBNull.Value), CObj(supCodigo))),
            New SqlParameter("@codigoseguridad", If(String.IsNullOrEmpty(codigoSegDesdeQr), CObj(DBNull.Value), CObj(codigoSegDesdeQr))),
            New SqlParameter("@fechafirma", If(fechaFirmaDesdeQr.HasValue, CType(fechaFirmaDesdeQr.Value, Object), CObj(DBNull.Value))),
            New SqlParameter("@pendiente", If(esPendiente, 1, 0))
        }
        Try
            Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSLUGANIS", p)
        Catch exPendParam As Exception
            If esPendiente Then
                Helper.RegistrarLogCliente("[LUGANIS] SP sin @pendiente; reintento sin el parámetro: " & exPendParam.Message)
                Dim pOld(p.Length - 2) As SqlParameter
                Array.Copy(p, pOld, p.Length - 1)
                Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSLUGANIS", pOld)
            Else
                Throw
            End If
        End Try

        ' SP viejo pone Error_Luganis=1 cuando @aceptado=0. En Pendiente limpiar error.
        If esPendiente Then
            Try
                Dim sqlPend As String = Nothing
                Select Case tipoDoc.ToUpperInvariant()
                    Case "FACTURA"
                        sqlPend = "UPDATE dbo.Facturas SET Error_Luganis = 0, AceptadoLuganis = 0 WHERE emp_codigo=@emp AND suc_codigo=@suc AND fac_numero=CAST(@fac AS INT) AND fac_forma=ISNULL(@forma, fac_forma) AND tfa_codigo=ISNULL(@tfa, tfa_codigo);"
                    Case "COMPRA"
                        sqlPend = "UPDATE dbo.ProvFacturas SET Error_Luganis = 0, AceptadoLuganis = 0 WHERE emp_codigo=@emp AND suc_codigo=@suc AND fac_numero=@fac AND sup_codigo=ISNULL(@sup, sup_codigo);"
                    Case "DESEMBOLSO"
                        sqlPend = "UPDATE dbo.Desembolsos SET Error_Luganis = 0, AceptadoLuganis = 0 WHERE emp_codigo=@emp AND suc_codigo=@suc AND des_numero=CAST(@fac AS INT);"
                    Case "DEVOLUCION", "DEV"
                        sqlPend = "UPDATE dbo.Devolucion SET Error_Luganis = 0, AceptadoLuganis = 0 WHERE emp_codigo=@emp AND suc_codigo=@suc AND dev_numero=CAST(@fac AS INT);"
                    Case "NOTACREDITO", "NC"
                        sqlPend = "UPDATE dbo.NotasCredito SET Error_Luganis = 0, AceptadoLuganis = 0 WHERE emp_codigo=@emp AND suc_codigo=@suc AND ncr_numero=CAST(@fac AS INT);"
                    Case "NOTADEBITO", "ND"
                        sqlPend = "UPDATE dbo.NotasDebito SET Error_Luganis = 0, AceptadoLuganis = 0 WHERE emp_codigo=@emp AND suc_codigo=@suc AND nde_numero=CAST(@fac AS INT);"
                End Select
                If sqlPend IsNot Nothing Then
                    Using conn As New SqlClient.SqlConnection(cadenaConexion)
                        Using cmd As New SqlClient.SqlCommand(sqlPend, conn)
                            cmd.Parameters.AddWithValue("@emp", CInt(emp))
                            cmd.Parameters.AddWithValue("@suc", CInt(suc))
                            cmd.Parameters.AddWithValue("@fac", facNumero)
                            cmd.Parameters.AddWithValue("@forma", If(String.IsNullOrEmpty(facForma), CObj(DBNull.Value), CObj(facForma)))
                            cmd.Parameters.AddWithValue("@tfa", If(String.IsNullOrEmpty(tfaCodigo), CObj(DBNull.Value), CObj(tfaCodigo)))
                            cmd.Parameters.AddWithValue("@sup", If(String.IsNullOrEmpty(supCodigo), CObj(DBNull.Value), CObj(supCodigo)))
                            conn.Open()
                            Dim n = cmd.ExecuteNonQuery()
                            Helper.RegistrarLogCliente("[LUGANIS] Pendiente: Error_Luganis limpiado a 0 (filas=" & n & ").")
                        End Using
                    End Using
                End If
            Catch exPend As Exception
                Helper.RegistrarLogCliente("[LUGANIS] No se pudo limpiar Error_Luganis en Pendiente: " & exPend.Message)
            End Try
        End If

        ' Bitácora: no marcar Error_Luganis si está pendiente
        RegistrarBitacoraEncfLuganis(
            cadenaConexion, tipoDoc, emp, suc, facNumero, encf, facForma, tfaCodigo, supCodigo,
            aceptado, If(esPendiente, Nothing, mensajeRechazo), esPendiente)
    End Sub

    Private Shared Sub RegistrarBitacoraEncfLuganis(
        cadenaConexion As String,
        tipoDoc As String,
        emp As String,
        suc As String,
        docNumero As String,
        encf As String,
        facForma As String,
        tfaCodigo As String,
        supCodigo As String,
        aceptado As Boolean,
        mensajeRechazo As String,
        Optional esPendiente As Boolean = False
    )
        If String.IsNullOrWhiteSpace(encf) Then Return
        Dim doc = CrearDocRefDesdeTipoDocumento(tipoDoc, emp, suc, docNumero, facForma, tfaCodigo, supCodigo)
        If doc Is Nothing Then Return

        Dim motivo As String = Nothing
        Dim estadoLug As Helper.DocumentoEncfEstado = Nothing
        If esPendiente Then
            ' No marcar rechazo en bitácora: solo quedó pendiente de aceptación
            estadoLug = Nothing
            motivo = Nothing
        ElseIf Not aceptado Then
            estadoLug = New Helper.DocumentoEncfEstado With {.ErrorLuganis = True, .AceptadoLuganis = False}
            If Not String.IsNullOrWhiteSpace(mensajeRechazo) Then motivo = mensajeRechazo.Trim()
        End If
        Helper.InsertarBitacoraEncf(cadenaConexion, doc, encf.Trim(), origen:="LUGANIS", motivoRechazo:=motivo, estadoAnterior:=estadoLug)
    End Sub

    Private Shared Function ResolverTipoDocumentoDesdeEcf(tipoECF As String, isNotaCredito As Boolean) As String
        Select Case If(tipoECF, "").Trim()
            Case "31", "32", "44", "45", "46"
                Return "FACTURA"
            Case "41", "47"
                Return "COMPRA"
            Case "34"
                Return If(isNotaCredito, "NOTACREDITO", "DEVOLUCION")
            Case "43"
                Return "DESEMBOLSO"
            Case "33"
                Return "NOTADEBITO"
            Case Else
                Return "FACTURA"
        End Select
    End Function

    Private Shared Function CrearDocRefDesdeTipoDocumento(
        tipoDoc As String,
        emp As String,
        suc As String,
        docNumero As String,
        facForma As String,
        tfaCodigo As String,
        supCodigo As String
    ) As Helper.EncfDocumentoRef
        Dim empI = CInt(emp)
        Dim sucI = CInt(suc)
        Dim numInt As Integer
        Integer.TryParse(If(docNumero, "").Trim(), numInt)

        Select Case If(tipoDoc, "").Trim().ToUpperInvariant()
            Case "FACTURA"
                Return Helper.EncfDocumentoRef.Factura(empI, sucI, If(String.IsNullOrEmpty(facForma), "A", facForma),
                    If(String.IsNullOrEmpty(tfaCodigo), 0, CInt(tfaCodigo)), numInt)
            Case "COMPRA"
                Return Helper.EncfDocumentoRef.Compra(empI, sucI, If(String.IsNullOrEmpty(supCodigo), 0, CInt(supCodigo)), docNumero)
            Case "DESEMBOLSO"
                Return Helper.EncfDocumentoRef.Desembolso(empI, sucI, numInt)
            Case "DEVOLUCION"
                Return Helper.EncfDocumentoRef.Devolucion(empI, sucI, numInt)
            Case "NOTACREDITO"
                Return Helper.EncfDocumentoRef.NotaCredito(empI, sucI, numInt)
            Case "NOTADEBITO"
                Return Helper.EncfDocumentoRef.NotaDebito(empI, sucI, numInt)
            Case Else
                Return Nothing
        End Select
    End Function

    Private Shared Sub RegistrarBitacoraEncfTrasAsignacion(
        cadenaConexion As String,
        doc As Helper.EncfDocumentoRef,
        encfGenerado As String,
        estadoPrevio As Helper.DocumentoEncfEstado,
        origen As String
    )
        If doc Is Nothing OrElse String.IsNullOrWhiteSpace(encfGenerado) Then Return
        Helper.InsertarBitacoraEncf(cadenaConexion, doc, encfGenerado, estadoPrevio?.ENCF, origen, Nothing, estadoPrevio)
    End Sub

    ''' <summary>
    ''' Helper interno para construir siempre un JSON consistente de respuesta para LUGANIS.
    ''' </summary>
    Private Shared Function ConstruirJsonLuganis(
        ok As Boolean,
        message As String,
        trackId As String,
        filename As String,
        txtContent As String,
        rawResponse As String,
        errorDetail As String,
        savedFilePath As String,
        Optional estadoLuganis As String = Nothing,
        Optional rechazado As Boolean? = Nothing,
        Optional codigoError As String = Nothing,
        Optional mensajeRechazo As String = Nothing
    ) As String

        Dim msgFinal = If(String.IsNullOrWhiteSpace(message), "", message)
        If rechazado.HasValue AndAlso rechazado.Value Then
            If Not String.IsNullOrWhiteSpace(mensajeRechazo) Then
                msgFinal = mensajeRechazo
            ElseIf String.IsNullOrWhiteSpace(msgFinal) Then
                msgFinal = If(String.IsNullOrWhiteSpace(estadoLuganis), "Documento rechazado", estadoLuganis)
            End If
        End If

        Dim payload = New With {
            .ok = ok,
            .message = msgFinal,
            .estado = estadoLuganis,
            .rechazado = If(rechazado.HasValue, CObj(rechazado.Value), CObj(Nothing)),
            .codigoError = codigoError,
            .mensajeRechazo = mensajeRechazo,
            .trackId = trackId,
            .filename = filename,
            .txtContent = txtContent,
            .rawResponse = rawResponse,
            .errorDetail = errorDetail,
            .savedFilePath = savedFilePath
        }

        Return JsonConvert.SerializeObject(payload)
    End Function

    Private Shared Function ResolverMensajeUsuarioLuganis(
        cadenaConexion As String,
        resultado As LuganisResult,
        aceptadoParaBd As Boolean,
        tipoDoc As String,
        emp As String,
        suc As String,
        docNumero As String,
        facForma As String,
        tfaCodigo As String,
        supCodigo As String
    ) As String
        If resultado Is Nothing Then Return "Sin respuesta de LUGANIS"
        If aceptadoParaBd AndAlso resultado.Success Then
            Return If(String.IsNullOrWhiteSpace(resultado.Mensaje), "Aceptado por LUGANIS", resultado.Mensaje)
        End If
        If String.Equals(If(resultado.Codigo, ""), "PENDIENTE", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(If(resultado.EstadoLuganis, ""), "Pendiente", StringComparison.OrdinalIgnoreCase) Then
            Return If(String.IsNullOrWhiteSpace(resultado.Mensaje),
                      "Documento enviado a LUGANIS; estado Pendiente (aún no Aceptado/Rechazado).",
                      resultado.Mensaje)
        End If

        Dim msg = If(String.IsNullOrWhiteSpace(resultado.Mensaje), "", resultado.Mensaje.Trim())
        If Not String.IsNullOrWhiteSpace(resultado.EstadoLuganis) AndAlso
           (String.IsNullOrWhiteSpace(msg) OrElse msg.IndexOf(resultado.EstadoLuganis, StringComparison.OrdinalIgnoreCase) < 0) Then
            msg = If(msg = "", resultado.EstadoLuganis, resultado.EstadoLuganis & ": " & msg)
        End If

        Dim doc = CrearDocRefDesdeTipoDocumento(tipoDoc, emp, suc, docNumero, facForma, tfaCodigo, supCodigo)
        If doc IsNot Nothing Then
            Dim motivoBitacora = Helper.ObtenerMotivoRechazoEncfBitacora(cadenaConexion, doc)
            If Not String.IsNullOrWhiteSpace(motivoBitacora) Then Return motivoBitacora
            motivoBitacora = Helper.ObtenerUltimoMensajeErrorDgii(cadenaConexion, doc)
            If Not String.IsNullOrWhiteSpace(motivoBitacora) Then Return motivoBitacora
        End If

        If Not String.IsNullOrWhiteSpace(msg) Then Return msg
        If Not resultado.Success Then Return "Error al enviar a LUGANIS"
        Return "Documento rechazado por LUGANIS"
    End Function

    ''' <summary>
    ''' Si el documento ya fue aceptado o quedó pendiente/enviado, no reenvía
    ''' (evita un segundo eNCF cuando el primero sí fue aceptado en DGII).
    ''' Devuelve JSON listo para Delphi, o Nothing si hay que enviar.
    ''' </summary>
    Private Const MsgYaEnviadoNoReenviar As String = "OK: ya enviado; no se reenvía para evitar duplicado."

    Private Shared Function EncFYaAsignado(estado As Helper.DocumentoEncfEstado) As String
        If estado Is Nothing Then Return ""
        Return If(estado.ENCF, "").Trim()
    End Function

    Private Shared Function ConstruirDocReuso(
        empInt As Integer,
        sucInt As Integer,
        facInt As Integer,
        forma1 As String,
        tfaInt As Integer,
        tipoECF As String,
        isNotaCredito As Boolean,
        supCodigo As String,
        facNumero As String
    ) As Helper.EncfDocumentoRef
        Dim tipo = If(tipoECF, "").Trim()
        If isNotaCredito Then
            Return Helper.EncfDocumentoRef.NotaCredito(empInt, sucInt, facInt)
        End If
        If String.Equals(tipo, "33", StringComparison.OrdinalIgnoreCase) Then
            Return Helper.EncfDocumentoRef.NotaDebito(empInt, sucInt, facInt)
        End If
        If String.Equals(tipo, "43", StringComparison.OrdinalIgnoreCase) Then
            Return Helper.EncfDocumentoRef.Desembolso(empInt, sucInt, facInt)
        End If
        If String.Equals(tipo, "34", StringComparison.OrdinalIgnoreCase) Then
            Return Helper.EncfDocumentoRef.Devolucion(empInt, sucInt, facInt)
        End If
        If Not String.IsNullOrWhiteSpace(supCodigo) AndAlso
           (String.Equals(tipo, "41", StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(tipo, "47", StringComparison.OrdinalIgnoreCase)) Then
            Dim supInt As Integer
            Integer.TryParse(supCodigo, supInt)
            Return Helper.EncfDocumentoRef.Compra(empInt, sucInt, supInt, facNumero)
        End If
        Return Helper.EncfDocumentoRef.Factura(empInt, sucInt, forma1, tfaInt, facInt)
    End Function

    Private Shared Function NumeroDocumento(doc As Helper.EncfDocumentoRef) As String
        If doc Is Nothing Then Return "0"
        If Not String.IsNullOrWhiteSpace(doc.DocNumeroStr) Then Return doc.DocNumeroStr.Trim()
        If doc.DocNumero.HasValue Then Return doc.DocNumero.Value.ToString()
        Return "0"
    End Function

    Private Shared Function IntentarReusarEnvioExistenteDgii(
        emp As String,
        suc As String,
        facNumero As String,
        facForma As String,
        tfaCodigo As String,
        tipoECF As String,
        isNotaCredito As Boolean
    ) As String
        Dim empInt As Integer, sucInt As Integer, tfaInt As Integer, facInt As Integer
        If Not Integer.TryParse(emp, empInt) OrElse Not Integer.TryParse(suc, sucInt) Then Return Nothing
        Integer.TryParse(If(tfaCodigo, "0"), tfaInt)
        If Not Integer.TryParse(facNumero, facInt) Then Return Nothing
        Dim forma1 = If(facForma, "").Trim()
        If forma1.Length > 1 Then forma1 = forma1.Substring(0, 1)
        Return IntentarReusarEnvioExistenteDgii(
            ConstruirDocReuso(empInt, sucInt, facInt, forma1, tfaInt, tipoECF, isNotaCredito, "", facNumero),
            tipoECF, isNotaCredito)
    End Function

    Private Shared Function IntentarReusarEnvioExistenteDgii(
        doc As Helper.EncfDocumentoRef,
        tipoECF As String,
        Optional isNotaCredito As Boolean = False
    ) As String
        If doc Is Nothing Then Return Nothing
        Try
            Dim estadoPrev = Helper.ObtenerEstadoEncf(cadenaConexion, doc)
            If Helper.DocumentoYaAceptado(estadoPrev) Then
                Helper.RegistrarLogCliente("[DGII] Ya aceptado. No se reenvía. eNCF=" & If(estadoPrev.ENCF, "") & " tipo=" & doc.TipoDocumento)
                Return Helper.ConstruirJsonRespuestaOperacion(New Helper.DgiiEstadoRespuesta With {
                    .Estado = "Aceptado",
                    .Aceptado = True,
                    .Encf = estadoPrev.ENCF,
                    .Mensaje = "Documento ya aceptado. No se reenvía para evitar duplicado."
                })
            End If

            Dim bit = Helper.ObtenerUltimaBitacoraDgii(cadenaConexion, doc)
            Dim trackExistente = If(bit IsNot Nothing, If(bit.TrackId, "").Trim(), "")
            If String.IsNullOrWhiteSpace(trackExistente) Then trackExistente = If(estadoPrev.TrackIdLuganis, "").Trim()

            If String.IsNullOrWhiteSpace(trackExistente) AndAlso Not Helper.DocumentoYaTransmitido(estadoPrev) Then
                Return Nothing
            End If

            If Not String.IsNullOrWhiteSpace(trackExistente) Then
                Try
                    Dim token As String = GlobalVariables.token
                    If String.IsNullOrWhiteSpace(token) Then
                        Seguridad.ObtenerToken()
                        token = GlobalVariables.token
                    End If
                    Dim respExist = ConsultarEstadoConReintentos(trackExistente, token)
                    Dim parsedExist = Helper.ParsearRespuestaDgii(respExist)
                    If parsedExist.Aceptado Then
                        Helper.RegistrarLogCliente("[DGII] Reconsulta: ACEPTADO. No se reenvía. trackId=" & trackExistente)
                        Return PersistirReconsultaDgii(doc, tipoECF, isNotaCredito, respExist)
                    End If
                    If parsedExist.EsPendiente Then
                        Helper.RegistrarLogCliente("[DGII] Reconsulta: PENDIENTE. No se reenvía ni se asigna otro eNCF. trackId=" & trackExistente)
                        Return PersistirReconsultaDgii(doc, tipoECF, isNotaCredito, respExist)
                    End If
                    If parsedExist.EsRechazado AndAlso
                       (String.Equals(If(parsedExist.CodigoMensaje, "").Trim(), "1209", StringComparison.OrdinalIgnoreCase) OrElse
                        ContainsSecuenciaYaUsada(parsedExist.Mensaje)) Then
                        Helper.RegistrarLogCliente("[DGII] Reconsulta 1209: la secuencia ya está en DGII. No se emite otro eNCF.")
                        parsedExist.EsPendiente = True
                        parsedExist.EsRechazado = False
                        parsedExist.Mensaje = "Secuencia ya utilizada en DGII (posible aceptación previa). No se reenvía."
                        Return Helper.ConstruirJsonRespuestaOperacion(parsedExist, respExist)
                    End If
                Catch exCons As Exception
                    Helper.RegistrarLogCliente("[DGII] Reconsulta previa falló: " & exCons.Message)
                    If Helper.DocumentoYaTransmitido(estadoPrev) Then
                        Return Helper.ConstruirJsonRespuestaOperacion(New Helper.DgiiEstadoRespuesta With {
                            .Estado = "Pendiente",
                            .EsPendiente = True,
                            .Encf = estadoPrev.ENCF,
                            .TrackId = trackExistente,
                            .Mensaje = "Ya fue enviado; no se pudo confirmar el estado. No se reenvía para evitar duplicado."
                        })
                    End If
                End Try
            ElseIf Helper.DocumentoYaTransmitido(estadoPrev) Then
                Helper.RegistrarLogCliente("[DGII] Ya transmitida sin trackId consultable. Se reutiliza eNCF, no se emite otro.")
            End If
        Catch ex As Exception
            Helper.RegistrarLogCliente("[DGII] IntentarReusarEnvioExistenteDgii EX: " & ex.Message)
        End Try
        Return Nothing
    End Function

    Private Shared Function PersistirReconsultaDgii(
        doc As Helper.EncfDocumentoRef,
        tipoECF As String,
        isNotaCredito As Boolean,
        respuestaConsulta As String
    ) As String
        Dim num = NumeroDocumento(doc)
        Dim emp = doc.EmpCodigo.ToString()
        Dim suc = doc.SucCodigo.ToString()
        If doc.TipoDocumento.Equals(Helper.TipoEncfPos, StringComparison.OrdinalIgnoreCase) OrElse
           doc.TipoDocumento.Equals(Helper.TipoEncfResBar, StringComparison.OrdinalIgnoreCase) Then
            Return ProcesarRespuestaAPISoloEstadoTicket(
                cadenaConexion, emp, suc, num,
                If(doc.UsuCodigo, 0).ToString(), If(doc.CajaCodigo, 0).ToString(),
                respuestaConsulta,
                doc.TipoDocumento.Equals(Helper.TipoEncfResBar, StringComparison.OrdinalIgnoreCase))
        End If
        Return ProcesarRespuestaAPISoloEstado(
            cadenaConexion, emp, suc, num,
            If(doc.FacForma, ""), If(doc.TfaCodigo, 0).ToString(), If(tipoECF, ""),
            respuestaConsulta, If(doc.SupCodigo, 0).ToString(), isNotaCredito)
    End Function

    Private Shared Function IntentarReusarEnvioExistenteLuganis(
        emp As String,
        suc As String,
        facNumero As String,
        facForma As String,
        tfaCodigo As String,
        tipoECF As String,
        isNotaCredito As Boolean,
        supCodigo As String,
        empRnc As String,
        loginRequest As LuganisLoginRequest,
        logDebug As Boolean
    ) As String
        Dim empInt As Integer, sucInt As Integer, tfaInt As Integer, facInt As Integer
        If Not Integer.TryParse(emp, empInt) OrElse Not Integer.TryParse(suc, sucInt) Then Return Nothing
        Integer.TryParse(If(tfaCodigo, "0"), tfaInt)
        If Not Integer.TryParse(facNumero, facInt) Then Return Nothing
        Dim forma1 = If(facForma, "").Trim()
        If forma1.Length > 1 Then forma1 = forma1.Substring(0, 1)
        Dim doc = ConstruirDocReuso(empInt, sucInt, facInt, forma1, tfaInt, tipoECF, isNotaCredito, supCodigo, facNumero)
        Return IntentarReusarEnvioExistenteLuganis(doc, tipoECF, isNotaCredito, facForma, tfaCodigo, supCodigo, empRnc, loginRequest, logDebug)
    End Function

    Private Shared Function IntentarReusarEnvioExistenteLuganis(
        doc As Helper.EncfDocumentoRef,
        tipoECF As String,
        isNotaCredito As Boolean,
        facForma As String,
        tfaCodigo As String,
        supCodigo As String,
        empRnc As String,
        loginRequest As LuganisLoginRequest,
        logDebug As Boolean
    ) As String
        If doc Is Nothing Then Return Nothing
        Try
            Dim estadoPrev = Helper.ObtenerEstadoEncf(cadenaConexion, doc)
            If Helper.DocumentoYaAceptado(estadoPrev) Then
                Helper.RegistrarLogCliente("[LUGANIS] Ya aceptada. No se reenvía. eNCF=" & If(estadoPrev.ENCF, ""))
                Return ConstruirJsonLuganis(
                    ok:=True,
                    message:="Documento ya aceptado. No se reenvía para evitar duplicado.",
                    trackId:=estadoPrev.TrackIdLuganis,
                    filename:=Nothing,
                    txtContent:=Nothing,
                    rawResponse:=Nothing,
                    errorDetail:=Nothing,
                    savedFilePath:=Nothing,
                    estadoLuganis:="Aceptado",
                    rechazado:=False)
            End If

            Dim track = If(estadoPrev.TrackIdLuganis, "").Trim()
            Dim encfPrev = If(estadoPrev.ENCF, "").Trim()
            If String.IsNullOrWhiteSpace(track) AndAlso String.IsNullOrWhiteSpace(encfPrev) Then Return Nothing
            If String.IsNullOrWhiteSpace(track) AndAlso Not Helper.DocumentoYaTransmitido(estadoPrev) Then Return Nothing

            Dim consulta = LuganisIntegrationService.ConsultarEstadoExistente(
                loginRequest, empRnc, encfPrev, track, logDebug)

            If consulta Is Nothing Then Return Nothing

            If consulta.AceptadoPorLuganis.HasValue AndAlso consulta.AceptadoPorLuganis.Value Then
                Try
                    ActualizarDatosLuganisEnBD(
                        cadenaConexion:=cadenaConexion,
                        tipoECF:=tipoECF,
                        emp:=doc.EmpCodigo.ToString(),
                        suc:=doc.SucCodigo.ToString(),
                        facNumero:=NumeroDocumento(doc),
                        encf:=encfPrev,
                        trackId:=If(consulta.TrackId, track),
                        aceptado:=True,
                        filename:=consulta.FileName,
                        qrCode:=Nothing,
                        facForma:=facForma,
                        tfaCodigo:=tfaCodigo,
                        supCodigo:=supCodigo,
                        isNotaCredito:=isNotaCredito,
                        mensajeRechazo:=Nothing,
                        esPendiente:=False)
                Catch exSp As Exception
                    Helper.RegistrarLogCliente("[LUGANIS] Reconsulta aceptada, error al persistir: " & exSp.Message)
                End Try
                Return ConstruirJsonLuganis(
                    ok:=True,
                    message:="Ya aceptado por LUGANIS/DGII. No se reenvía.",
                    trackId:=If(consulta.TrackId, track),
                    filename:=consulta.FileName,
                    txtContent:=Nothing,
                    rawResponse:=consulta.RawResponse,
                    errorDetail:=Nothing,
                    savedFilePath:=Nothing,
                    estadoLuganis:=If(consulta.EstadoLuganis, "Aceptado"),
                    rechazado:=False)
            End If

            Dim esPendienteCons = (Not consulta.AceptadoPorLuganis.HasValue) AndAlso
                (consulta.Success OrElse String.Equals(If(consulta.Codigo, ""), "PENDIENTE", StringComparison.OrdinalIgnoreCase))
            If esPendienteCons AndAlso (consulta.Success OrElse Not String.IsNullOrWhiteSpace(track)) Then
                Try
                    ActualizarDatosLuganisEnBD(
                        cadenaConexion:=cadenaConexion,
                        tipoECF:=tipoECF,
                        emp:=doc.EmpCodigo.ToString(),
                        suc:=doc.SucCodigo.ToString(),
                        facNumero:=NumeroDocumento(doc),
                        encf:=encfPrev,
                        trackId:=If(consulta.TrackId, track),
                        aceptado:=False,
                        filename:=consulta.FileName,
                        qrCode:=Nothing,
                        facForma:=facForma,
                        tfaCodigo:=tfaCodigo,
                        supCodigo:=supCodigo,
                        isNotaCredito:=isNotaCredito,
                        mensajeRechazo:="PENDIENTE: " & If(consulta.Mensaje, ""),
                        esPendiente:=True)
                Catch exSp2 As Exception
                    Helper.RegistrarLogCliente("[LUGANIS] Reconsulta pendiente, error al persistir: " & exSp2.Message)
                End Try
                Return ConstruirJsonLuganis(
                    ok:=True,
                    message:="Documento ya enviado; estado Pendiente. No se reenvía para evitar duplicado.",
                    trackId:=If(consulta.TrackId, track),
                    filename:=consulta.FileName,
                    txtContent:=Nothing,
                    rawResponse:=consulta.RawResponse,
                    errorDetail:=Nothing,
                    savedFilePath:=Nothing,
                    estadoLuganis:=If(consulta.EstadoLuganis, "Pendiente"),
                    rechazado:=False,
                    codigoError:="PENDIENTE")
            End If

            If Not consulta.Success AndAlso Helper.DocumentoYaTransmitido(estadoPrev) AndAlso
               Not (consulta.AceptadoPorLuganis.HasValue AndAlso Not consulta.AceptadoPorLuganis.Value) Then
                Helper.RegistrarLogCliente("[LUGANIS] Reconsulta falló y el documento ya salió. No se reenvía.")
                Return ConstruirJsonLuganis(
                    ok:=True,
                    message:="Ya fue enviado; no se pudo confirmar el estado. No se reenvía para evitar duplicado.",
                    trackId:=track,
                    filename:=Nothing,
                    txtContent:=Nothing,
                    rawResponse:=consulta.RawResponse,
                    errorDetail:=consulta.Mensaje,
                    savedFilePath:=Nothing,
                    estadoLuganis:="Pendiente",
                    rechazado:=False,
                    codigoError:="PENDIENTE")
            End If
        Catch ex As Exception
            Helper.RegistrarLogCliente("[LUGANIS] IntentarReusarEnvioExistenteLuganis EX: " & ex.Message)
        End Try
        Return Nothing
    End Function

    ''' <summary>
    ''' Genera el XmlDocument de la factura electrÃ³nica para el flujo LUGANIS,
    ''' utilizando la misma lÃ³gica de secuencia y generaciÃ³n de XML que el flujo DGII,
    ''' pero sin modificar el mÃ©todo pÃºblico EnviarFacturaElectronica.
    ''' </summary>
    Private Shared Function GenerarXmlFacturaParaLuganis(
        emp As String,
        suc As String,
        facNumero As String,
        empRnc As String,
        facForma As String,
        tfaCodigo As String,
        tipoECF As String,
        ByRef nuevoENCF As String
    ) As XmlDocument

        ' Para LUGANIS solo necesitamos generar el XML a partir de la BD.
        ' No es necesario (ni deseable) obtener el token de la DGII ni llamar a sus servicios.
        CargarConfiguracion()

        Dim xmlFactura As XmlDocument

        ' Asignar secuencia utilizando el mismo SP que el flujo actual.
        Dim secuenciaGenerada As String = LlamarAsignarSecuenciaDGII(
            cadenaConexion,
            Convert.ToInt32(emp),
            Convert.ToInt32(suc),
            If(String.IsNullOrEmpty(tfaCodigo), 0, Convert.ToInt32(tfaCodigo)),
            facForma,
            Convert.ToInt32(facNumero))

        glbncfEnvia = secuenciaGenerada
        nuevoENCF = secuenciaGenerada

        ' Seleccionar el generador de XML segÃºn el tipo de e-CF,
        ' replicando la lÃ³gica existente del mÃ©todo EnviarFacturaElectronica.
        If tipoECF = "44" Then
            xmlFactura = GenerarXML(secuenciaGenerada, cadenaConexion, facNumero, emp, suc, facForma, tfaCodigo, "", "")
        ElseIf tipoECF = "45" Then
            xmlFactura = GenerarXML(secuenciaGenerada, cadenaConexion, facNumero, emp, suc, facForma, tfaCodigo, "", "")
        ElseIf tipoECF = "46" Then
            xmlFactura = GenerarXMLE46(secuenciaGenerada, cadenaConexion, facNumero, emp, suc, facForma, tfaCodigo)
        Else
            xmlFactura = GenerarXML(secuenciaGenerada, cadenaConexion, facNumero, emp, suc, facForma, tfaCodigo, "", "")
        End If

        Return xmlFactura
    End Function

    Public Function EnviarFacturaElectronicaPOS(
                                             emp As String,
                                             suc As String,
                                             ticket As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                             Optional usu_codigo As String = "",
                                             Optional caja As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            isPOS = True
            isResBar = False
            CargarConfiguracion()
            Dim xmlFactura As XmlDocument
            Seguridad.ObtenerToken()

            Dim empPos As Integer, sucPos As Integer, tickPos As Integer, usuPos As Integer, cajaPos As Integer
            If Integer.TryParse(emp, empPos) AndAlso Integer.TryParse(suc, sucPos) AndAlso Integer.TryParse(ticket, tickPos) Then
                Integer.TryParse(If(usu_codigo, "0"), usuPos)
                Integer.TryParse(If(caja, "0"), cajaPos)
                Dim jsonYaPos = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.PosTicket(empPos, sucPos, usuPos, cajaPos, tickPos), tipoECF)
                If Not String.IsNullOrWhiteSpace(jsonYaPos) Then Return jsonYaPos
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaDGIIPOS(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     usu_codigo, caja,
                                                     Convert.ToInt32(ticket))

            glbncfEnvia = nuevoENCF

            ' Paso 1: Obtener el XML de la factura 
            xmlFactura = GenerarXML(nuevoENCF, cadenaConexion, ticket, emp, suc, "", "", usu_codigo, caja) ' MÃ©todo que obtendrÃ¡ el XML de la factura de la BD

            ' Paso 2: Firmar el XML de la factura
            '  Dim pathCert As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GlobalVariables.pathCertificado)
            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado

            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFactura(xmlFactura, pathCert, passCert, glbncfEnvia, empRnc)
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)

            ' Paso 3: Obtener el token 
            Dim token As String = GlobalVariables.token

            ' Paso 4: Enviar el XML firmado
            Dim trackId As String = EnviarXMLFacturaFirmado(xmlFacturaFirmada, token, glbncfEnvia, empRnc)

            ' Paso 5: Consultar el estado de la factura
            Dim respuestaConsulta As String = ConsultarEstadoConReintentos(trackId, token)
            ProcesarRespuestaAPIPOS(RncCliente, tipoECF, cadenaConexion, emp, suc, respuestaConsulta, ticket, xmlFacturaFirmada, codigoSeguridad, usu_codigo, caja, "", fechaFirma)

            Dim parseada = Helper.ParsearRespuestaDgii(respuestaConsulta)
            Return Helper.ConstruirJsonRespuestaOperacion(parseada, respuestaConsulta)
        Catch ex As Exception
            Return Helper.ConstruirJsonRespuestaOperacion(
                New Helper.DgiiEstadoRespuesta With {.Estado = "Error", .Mensaje = ex.Message, .Aceptado = False},
                ex.ToString())
        End Try
    End Function

    ''' <summary>
    ''' Igual que <see cref="EnviarFacturaElectronicaPOS"/> pero lee de vistas RESBAR (vwFacturasXMLRESBAR, vwFormasPagoXMLRESBAR, vwFacturasDetalleXMLRESBAR)
    ''' y usa <c>dbo.AsignarSecuenciaFacturasDGIIPOSRESBAR</c> + <c>SP_UPDATEDATOSDGIRESBAR</c>.
    ''' </summary>
    Public Function EnviarFacturaElectronicaResBar(
                                             emp As String,
                                             suc As String,
                                             ticket As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                             Optional usu_codigo As String = "",
                                             Optional caja As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            isResBar = True
            isPOS = False
            CargarConfiguracion()
            Dim xmlFactura As XmlDocument
            Seguridad.ObtenerToken()

            Dim empRb As Integer, sucRb As Integer, tickRb As Integer, usuRb As Integer, cajaRb As Integer
            If Integer.TryParse(emp, empRb) AndAlso Integer.TryParse(suc, sucRb) AndAlso Integer.TryParse(ticket, tickRb) Then
                Integer.TryParse(If(usu_codigo, "0"), usuRb)
                Integer.TryParse(If(caja, "0"), cajaRb)
                Dim jsonYaRb = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.ResBar(empRb, sucRb, usuRb, cajaRb, tickRb), tipoECF)
                If Not String.IsNullOrWhiteSpace(jsonYaRb) Then Return jsonYaRb
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaDGIIPOSResBar(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     usu_codigo, caja,
                                                     Convert.ToInt32(ticket))

            glbncfEnvia = nuevoENCF

            xmlFactura = GenerarXML(nuevoENCF, cadenaConexion, ticket, emp, suc, "", "", usu_codigo, caja)

            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado

            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFactura(xmlFactura, pathCert, passCert, glbncfEnvia, empRnc)
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)

            Dim token As String = GlobalVariables.token

            Dim trackId As String = EnviarXMLFacturaFirmado(xmlFacturaFirmada, token, glbncfEnvia, empRnc)

            Dim respuestaConsulta As String = ConsultarEstadoConReintentos(trackId, token)
            ProcesarRespuestaAPIPOS(RncCliente, tipoECF, cadenaConexion, emp, suc, respuestaConsulta, ticket, xmlFacturaFirmada, codigoSeguridad, usu_codigo, caja, "", fechaFirma)

            Dim parseada = Helper.ParsearRespuestaDgii(respuestaConsulta)
            Return Helper.ConstruirJsonRespuestaOperacion(parseada, respuestaConsulta)
        Catch ex As Exception
            Return Helper.ConstruirJsonRespuestaOperacion(
                New Helper.DgiiEstadoRespuesta With {.Estado = "Error", .Mensaje = ex.Message, .Aceptado = False},
                ex.ToString())
        Finally
            isResBar = False
        End Try
    End Function

    Public Function EnviarFacturaResumenPOS(emp As String,
                                             suc As String,
                                             ticket As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                             Optional usu_codigo As String = "",
                                             Optional caja As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            isPOS = True
            isResBar = False
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            Dim empPosR As Integer, sucPosR As Integer, tickPosR As Integer, usuPosR As Integer, cajaPosR As Integer
            If Integer.TryParse(emp, empPosR) AndAlso Integer.TryParse(suc, sucPosR) AndAlso Integer.TryParse(ticket, tickPosR) Then
                Integer.TryParse(If(usu_codigo, "0"), usuPosR)
                Integer.TryParse(If(caja, "0"), cajaPosR)
                Dim jsonYaPosR = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.PosTicket(empPosR, sucPosR, usuPosR, cajaPosR, tickPosR), tipoECF)
                If Not String.IsNullOrWhiteSpace(jsonYaPosR) Then Return MsgYaEnviadoNoReenviar
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaDGIIPOS(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     usu_codigo, caja,
                                                     Convert.ToInt32(ticket))

            glbncfEnvia = nuevoENCF

            Dim trackId As String = ProcesoEnvioFacturaResumenPOS(RncCliente, nuevoENCF, emp, suc, ticket, empRnc, usu_codigo, caja, tipoECF, esResBarFlow:=False)
            Return trackId
        Catch ex As Exception
            Return "Error: " & ex.Message
        End Try

    End Function

    ''' <summary>Resumen RFCE para flujo RESBAR (vistas RESBAR + secuencia RESBAR + SP_UPDATEDATOSDGIRESBAR).</summary>
    Public Function EnviarFacturaResumenResBar(emp As String,
                                             suc As String,
                                             ticket As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                             Optional usu_codigo As String = "",
                                             Optional caja As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            isResBar = True
            isPOS = False
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            Dim empRbR As Integer, sucRbR As Integer, tickRbR As Integer, usuRbR As Integer, cajaRbR As Integer
            If Integer.TryParse(emp, empRbR) AndAlso Integer.TryParse(suc, sucRbR) AndAlso Integer.TryParse(ticket, tickRbR) Then
                Integer.TryParse(If(usu_codigo, "0"), usuRbR)
                Integer.TryParse(If(caja, "0"), cajaRbR)
                Dim jsonYaRbR = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.ResBar(empRbR, sucRbR, usuRbR, cajaRbR, tickRbR), tipoECF)
                If Not String.IsNullOrWhiteSpace(jsonYaRbR) Then Return MsgYaEnviadoNoReenviar
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaDGIIPOSResBar(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     usu_codigo, caja,
                                                     Convert.ToInt32(ticket))

            glbncfEnvia = nuevoENCF

            Dim trackId As String = ProcesoEnvioFacturaResumenPOS(RncCliente, nuevoENCF, emp, suc, ticket, empRnc, usu_codigo, caja, tipoECF, esResBarFlow:=True)
            Return trackId
        Catch ex As Exception
            Return "Error: " & ex.Message
        Finally
            isResBar = False
        End Try

    End Function

    Public Function EnviarFacturaResumen(emp As String,
                                             suc As String,
                                             facNumero As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            Dim jsonYaRes = IntentarReusarEnvioExistenteDgii(
                emp, suc, facNumero, facForma, tfaCodigo, tipoECF, False)
            If Not String.IsNullOrWhiteSpace(jsonYaRes) Then Return MsgYaEnviadoNoReenviar

            Dim nuevoENCF As String = LlamarAsignarSecuenciaDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     If(String.IsNullOrEmpty(tfaCodigo), 0, Convert.ToInt32(tfaCodigo)),
                                                     facForma,
                                                     Convert.ToInt32(facNumero))

            glbncfEnvia = nuevoENCF

            Dim trackId As String = ProcesoEnvioFacturaResumen(RncCliente, nuevoENCF, emp, suc, facNumero, empRnc, facForma, tfaCodigo, tipoECF)
            Return trackId
        Catch ex As Exception
            Return "Error: " & ex.Message
        End Try

    End Function

    Public Function EnviarCompras(emp As String, suc As String, sup As String,
                              facNumero As String, empRnc As String, eNCF As String, RncCliente As String,
                              Optional facForma As String = "", Optional tfaCodigo As String = "",
                              Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            Dim esPagoExterior As Boolean = EsCompraPagoExterior(cadenaConexion, emp, sup, facNumero, tipoECF)
            Dim tipoeCFAsignacion As Integer = If(esPagoExterior, 47, 41)

            Dim empC As Integer, sucC As Integer, supC As Integer
            If Integer.TryParse(emp, empC) AndAlso Integer.TryParse(suc, sucC) AndAlso Integer.TryParse(sup, supC) Then
                Dim jsonYaCompra = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.Compra(empC, sucC, supC, facNumero),
                    If(esPagoExterior, "47", "41"))
                If Not String.IsNullOrWhiteSpace(jsonYaCompra) Then Return MsgYaEnviadoNoReenviar
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaComprasDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                      Convert.ToInt32(sup),
                                                     facNumero,
                                                     tipoeCFAsignacion)
            If String.IsNullOrWhiteSpace(nuevoENCF) Then
                If esPagoExterior Then
                    Return "ERROR: No se pudo asignar eNCF tipo 47. Ejecute docs/SP_AsignarSecuenciaFacturasProvDGII.sql y verifique SecuenciaDGII tipo 47."
                End If
                Return "ERROR: No se pudo asignar eNCF de compra (tipo 41). Verifique ProvFacturas y SecuenciaDGII tipo 41."
            End If

            If Not (nuevoENCF.StartsWith("E41", StringComparison.OrdinalIgnoreCase) OrElse
                    nuevoENCF.StartsWith("E47", StringComparison.OrdinalIgnoreCase)) Then
                Helper.RegistrarLogCliente("EnviarCompras: eNCF inesperado para compra: " & nuevoENCF & " (esperado E41/E47). tip_codigo/TipoNCF mal configurado.")
                Return "ERROR: Se asignó eNCF inválido para compras (" & nuevoENCF & "). Debe ser E41 o E47. Revise tip_codigo en ProvFacturas y TipoNCF.cod_dgii."
            End If

            glbncfEnvia = nuevoENCF
            If esPagoExterior OrElse nuevoENCF.StartsWith("E47", StringComparison.OrdinalIgnoreCase) Then
                Return ProcesoEnvioFacturaE47(RncCliente, nuevoENCF, "47", emp, suc, facNumero, empRnc, sup)
            End If
            Dim r As String = ProcesoEnvioFacturaE41(RncCliente, nuevoENCF, "41", emp, suc, sup, facNumero, empRnc)
            Return r                                 ' â† devuelve lo que pasÃ³ (OK o ERROR)
        Catch ex As Exception
            Return "ERROR: " & ex.Message
        End Try
    End Function

    ''' <summary>
    ''' EnvÃ­a una compra (e-CF 41) a LUGANIS.
    ''' Usa el mismo SP de compras (AsignarSecuenciaFacturasProvDGII) y el generador XML existente (GenerarXMLE41),
    ''' y luego reutiliza el flujo de integraciÃ³n LuganisIntegrationService.EnviarAsync.
    ''' </summary>
    Public Function EnviarComprasLuganis(
        emp As String,
        suc As String,
        sup As String,
        facNumero As String,
        empRnc As String,
        RncProveedor As String,
        baseUrl As String,
        companyCode As String,
        username As String,
        password As String,
        appVersion As String,
        os As String,
        deviceId As String,
        latitude As String,
        longitude As String,
        providerIpAddress As String,
        Optional saveGeneratedTxt As Boolean = False,
        Optional outputFolder As String = "",
        Optional logDebug As Boolean = False
    ) As String
        Try
            If logDebug Then Helper.RegistrarLogCliente("[LUGANIS] === Inicio EnviarComprasLuganis ===")

            ' Validaciones mÃ­nimas
            If String.IsNullOrWhiteSpace(emp) OrElse
               String.IsNullOrWhiteSpace(suc) OrElse
               String.IsNullOrWhiteSpace(sup) OrElse
               String.IsNullOrWhiteSpace(facNumero) OrElse
               String.IsNullOrWhiteSpace(empRnc) OrElse
               String.IsNullOrWhiteSpace(RncProveedor) Then
                Dim errorMsg As String = "ParÃ¡metros obligatorios faltantes: emp, suc, sup, facNumero, empRnc y RncProveedor son requeridos."
                Return ConstruirJsonLuganis(ok:=False, message:=errorMsg, trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=errorMsg, savedFilePath:=Nothing)
            End If

            If String.IsNullOrWhiteSpace(baseUrl) OrElse String.IsNullOrWhiteSpace(companyCode) Then
                Dim errorMsg As String = "ParÃ¡metros de configuraciÃ³n LUGANIS incompletos (baseUrl y companyCode son requeridos)."
                Return ConstruirJsonLuganis(ok:=False, message:=errorMsg, trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=errorMsg, savedFilePath:=Nothing)
            End If

            If String.IsNullOrWhiteSpace(username) OrElse String.IsNullOrWhiteSpace(password) Then
                Dim errorMsg As String = "Credenciales de LUGANIS incompletas (username y password son requeridos)."
                Return ConstruirJsonLuganis(ok:=False, message:=errorMsg, trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=errorMsg, savedFilePath:=Nothing)
            End If

            If String.IsNullOrWhiteSpace(appVersion) OrElse
               String.IsNullOrWhiteSpace(os) OrElse
               String.IsNullOrWhiteSpace(deviceId) OrElse
               String.IsNullOrWhiteSpace(latitude) OrElse
               String.IsNullOrWhiteSpace(longitude) OrElse
               String.IsNullOrWhiteSpace(providerIpAddress) Then

                Dim errorMsg As String = "Metadatos de dispositivo incompletos (appVersion, os, deviceId, latitude, longitude y providerIpAddress son requeridos)."
                Return ConstruirJsonLuganis(ok:=False, message:=errorMsg, trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=errorMsg, savedFilePath:=Nothing)
            End If

            CargarConfiguracion()

            Dim loginRequestPreCompra As New LuganisLoginRequest With {
                .BaseUrl = baseUrl,
                .CompanyCode = companyCode,
                .Username = username,
                .Password = password,
                .AppVersion = appVersion,
                .Os = os,
                .DeviceId = deviceId,
                .Latitude = latitude,
                .Longitude = longitude,
                .ProviderIpAddress = providerIpAddress
            }
            Dim empCL As Integer, sucCL As Integer, supCL As Integer
            If Integer.TryParse(emp, empCL) AndAlso Integer.TryParse(suc, sucCL) AndAlso Integer.TryParse(sup, supCL) Then
                Dim jsonLugCompra = IntentarReusarEnvioExistenteLuganis(
                    Helper.EncfDocumentoRef.Compra(empCL, sucCL, supCL, facNumero),
                    "41", False, "", "", sup, empRnc, loginRequestPreCompra, logDebug)
                If Not String.IsNullOrWhiteSpace(jsonLugCompra) Then Return jsonLugCompra
            End If

            ' 1) Obtener eNCF de compras (SP de proveedores)
            Dim nuevoENCF As String = LlamarAsignarSecuenciaComprasDGII(
                cadenaConexion,
                Convert.ToInt32(emp),
                Convert.ToInt32(suc),
                Convert.ToInt32(sup),
                facNumero)

            If String.IsNullOrWhiteSpace(nuevoENCF) Then
                Dim errorMsg As String = "El SP de compras no devolviÃ³ un eNCF vÃ¡lido. Verifique empresa, sucursal, proveedor y nÃºmero de factura."
                Return ConstruirJsonLuganis(ok:=False, message:=errorMsg, trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=errorMsg, savedFilePath:=Nothing)
            End If

            glbncfEnvia = nuevoENCF

            ' 2) Generar XML E41 existente (compras)
            Dim xmlFactura As XmlDocument = GenerarXMLE41(nuevoENCF, cadenaConexion, facNumero, emp, sup)

            ' 3) Preparar login LUGANIS
            Dim loginRequest As New LuganisLoginRequest With {
                .BaseUrl = baseUrl,
                .CompanyCode = companyCode,
                .Username = username,
                .Password = password,
                .AppVersion = appVersion,
                .Os = os,
                .DeviceId = deviceId,
                .Latitude = latitude,
                .Longitude = longitude,
                .ProviderIpAddress = providerIpAddress
            }

            ' 4) Enviar a LUGANIS (tipo 41)
            Dim resultado As LuganisResult = LuganisIntegrationService.EnviarAsync(
                xmlDoc:=xmlFactura,
                tipoECF:="41",
                rncEmisor:=empRnc,
                eNCF:=nuevoENCF,
                loginRequest:=loginRequest,
                logDebug:=logDebug
            ).GetAwaiter().GetResult()

            ' 5) Guardar TXT opcionalmente
            Dim savedFilePath As String = Nothing
            If saveGeneratedTxt AndAlso Not String.IsNullOrEmpty(resultado.TxtContent) Then
                Dim carpetaSalida As String = outputFolder
                If String.IsNullOrWhiteSpace(carpetaSalida) Then carpetaSalida = RutaHelper.ObtenerRutaBaseDLL()
                If Not Directory.Exists(carpetaSalida) Then Directory.CreateDirectory(carpetaSalida)
                Dim nombreArchivo As String = If(String.IsNullOrWhiteSpace(resultado.FileName), "Luganis_E41_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".txt", resultado.FileName)
                Dim rutaCompleta As String = Path.Combine(carpetaSalida, nombreArchivo)
                Dim utf8SinBom As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False)
                File.WriteAllText(rutaCompleta, resultado.TxtContent, utf8SinBom)
                savedFilePath = rutaCompleta
            End If

            ' 6) No actualizamos SP_UPDATEDATOSLUGANIS aquÃ­ (se puede agregar si lo necesitas para ProvFacturas)

            Dim errorDetail As String = Nothing
            If Not resultado.Success Then
                If Not String.IsNullOrWhiteSpace(resultado.RawResponse) Then
                    errorDetail = resultado.RawResponse
                ElseIf Not String.IsNullOrWhiteSpace(resultado.Mensaje) Then
                    errorDetail = resultado.Mensaje
                End If
            End If

            Return ConstruirJsonLuganis(
                ok:=resultado.Success,
                message:=resultado.Mensaje,
                trackId:=resultado.TrackId,
                filename:=resultado.FileName,
                txtContent:=resultado.TxtContent,
                rawResponse:=resultado.RawResponse,
                errorDetail:=errorDetail,
                savedFilePath:=savedFilePath)

        Catch ex As Exception
            Helper.RegistrarLogCliente("[LUGANIS] EXCEPCION EnviarComprasLuganis: " & ex.ToString())
            Return ConstruirJsonLuganis(ok:=False, message:="ExcepciÃ³n en EnviarComprasLuganis.", trackId:=Nothing, filename:=Nothing, txtContent:=Nothing, rawResponse:=Nothing, errorDetail:=ex.ToString(), savedFilePath:=Nothing)
        End Try
    End Function

    Public Function EnviarGastosMenores(emp As String,
                                             suc As String,
                                                sup As String,
                                             facNumero As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            Dim empDes As Integer, sucDes As Integer, numDes As Integer
            If Integer.TryParse(emp, empDes) AndAlso Integer.TryParse(suc, sucDes) AndAlso Integer.TryParse(facNumero, numDes) Then
                Dim jsonYaDes = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.Desembolso(empDes, sucDes, numDes),
                    If(String.IsNullOrWhiteSpace(tipoECF), "43", tipoECF))
                If Not String.IsNullOrWhiteSpace(jsonYaDes) Then Return MsgYaEnviadoNoReenviar
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaDesembolsosDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                      Convert.ToInt32(sup),
                                                     facNumero)
            glbncfEnvia = nuevoENCF
            'Return glbncfEnvia
            Dim r As String = ProcesoEnvioFacturaE43(RncCliente, nuevoENCF, tipoECF, emp, suc, facNumero, empRnc)
            Return r
            ' Return $"OK: Factura {facNumero} tipo {tipoECF} lista para envÃ­o desde empresa {emp}, sucursal {suc}."
        Catch ex As Exception
            Return "Error: " & ex.Message
        End Try

    End Function

    Public Function EnviarDevolucion(emp As String,
                                             suc As String,
                                                sup As String,
                                             facNumero As String,
                                             empRnc As String,
                                                eNCF As String,
                                               RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            Dim empDev As Integer, sucDev As Integer, numDev As Integer
            If Integer.TryParse(emp, empDev) AndAlso Integer.TryParse(suc, sucDev) AndAlso Integer.TryParse(facNumero, numDev) Then
                Dim jsonYaDev = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.Devolucion(empDev, sucDev, numDev),
                    If(String.IsNullOrWhiteSpace(tipoECF), "34", tipoECF))
                If Not String.IsNullOrWhiteSpace(jsonYaDev) Then Return MsgYaEnviadoNoReenviar
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaDevolucionDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     facNumero)

            Helper.RegistrarLogCliente("nuevoENCF " + nuevoENCF)

            Dim r As String = ProcesoEnvioFacturaE34(RncCliente, nuevoENCF, tipoECF, emp, suc, "", facNumero, empRnc, "", "", False)
            Return r
            'Return nuevoENCF
            'Return $"OK: Factura {facNumero} tipo {tipoECF} lista para envÃ­o desde empresa {emp}, sucursal {suc}."
        Catch ex As Exception
            Return $"Error: {facNumero} tipo {tipoECF}  empresa {emp}, sucursal {suc} {ex.Message}"
        End Try

    End Function

    Public Function EnviarNotasCredito(emp As String,
                                             suc As String,
                                                sup As String,
                                             facNumero As String,
                                             empRnc As String,
                                                eNCF As String,
                                               RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            Dim empNc As Integer, sucNc As Integer, numNc As Integer
            If Integer.TryParse(emp, empNc) AndAlso Integer.TryParse(suc, sucNc) AndAlso Integer.TryParse(facNumero, numNc) Then
                Dim jsonYaNc = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.NotaCredito(empNc, sucNc, numNc),
                    If(String.IsNullOrWhiteSpace(tipoECF), "34", tipoECF),
                    True)
                If Not String.IsNullOrWhiteSpace(jsonYaNc) Then Return MsgYaEnviadoNoReenviar
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaNotaCreditoDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     facNumero)


            Dim r As String = ProcesoEnvioFacturaE34(RncCliente, nuevoENCF, tipoECF, emp, suc, "", facNumero, empRnc, "", "", True)
            Return r
            'Return nuevoENCF
            'Return $"OK: Factura {facNumero} tipo {tipoECF} lista para envÃ­o desde empresa {emp}, sucursal {suc}."
        Catch ex As Exception
            Return $"Error: {facNumero} tipo {tipoECF}  empresa {emp}, sucursal {suc} {ex.Message}"
        End Try

    End Function

    Public Function EnviarDevolucionPOS(emp As String,
                                             suc As String,
                                                sup As String,
                                             facNumero As String,
                                             empRnc As String,
                                                eNCF As String,
                                               RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()
            isPOS = True
            isResBar = False
            Dim empDevP As Integer, sucDevP As Integer, numDevP As Integer
            If Integer.TryParse(emp, empDevP) AndAlso Integer.TryParse(suc, sucDevP) AndAlso Integer.TryParse(facNumero, numDevP) Then
                Dim jsonYaDevP = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.Devolucion(empDevP, sucDevP, numDevP),
                    If(String.IsNullOrWhiteSpace(tipoECF), "34", tipoECF))
                If Not String.IsNullOrWhiteSpace(jsonYaDevP) Then Return MsgYaEnviadoNoReenviar
            End If
            Dim nuevoENCF As String = LlamarAsignarSecuenciaDevolucionDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     facNumero)

            Helper.RegistrarLogCliente("nuevoENCF " + nuevoENCF)

            Dim r As String = ProcesoEnvioFacturaE34(RncCliente, nuevoENCF, tipoECF, emp, suc, "", facNumero, empRnc, "", "", False)
            'Return nuevoENCF
            Return $"OK: Factura {facNumero} tipo {tipoECF} lista para envÃ­o desde empresa {emp}, sucursal {suc}."
        Catch ex As Exception
            Return $"Error: {facNumero} tipo {tipoECF}  empresa {emp}, sucursal {suc} {ex.Message}"
        End Try

    End Function

    ''' <summary>Nota de crÃ©dito E34 con encabezado/detalle desde vistas RESBAR (vwDevolucionesRESBARXML / vwDevolucionesDetalleXMLRESBAR).</summary>
    Public Function EnviarDevolucionResBar(emp As String,
                                             suc As String,
                                                sup As String,
                                             facNumero As String,
                                             empRnc As String,
                                                eNCF As String,
                                               RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()
            isResBar = True
            isPOS = False
            Dim empDevR As Integer, sucDevR As Integer, numDevR As Integer
            If Integer.TryParse(emp, empDevR) AndAlso Integer.TryParse(suc, sucDevR) AndAlso Integer.TryParse(facNumero, numDevR) Then
                Dim jsonYaDevR = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.Devolucion(empDevR, sucDevR, numDevR),
                    If(String.IsNullOrWhiteSpace(tipoECF), "34", tipoECF))
                If Not String.IsNullOrWhiteSpace(jsonYaDevR) Then Return MsgYaEnviadoNoReenviar
            End If
            Dim nuevoENCF As String = LlamarAsignarSecuenciaDevolucionDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     facNumero)

            Helper.RegistrarLogCliente("nuevoENCF " + nuevoENCF)

            Dim r As String = ProcesoEnvioFacturaE34(RncCliente, nuevoENCF, tipoECF, emp, suc, "", facNumero, empRnc, "", "", False)
            Return $"OK: Factura {facNumero} tipo {tipoECF} lista para envÃ­o desde empresa {emp}, sucursal {suc}."
        Catch ex As Exception
            Return $"Error: {facNumero} tipo {tipoECF}  empresa {emp}, sucursal {suc} {ex.Message}"
        Finally
            isResBar = False
        End Try

    End Function
    Private Shared Function DebeGuardarXmlMontoEnTablas() As Boolean
        ' OpciÃ³n por INI (recomendado). Si no existe, default False para no romper nada.
        Try
            Dim rutaIni = RutaHelper.ObtenerRutaConfigIni()
            Dim ini As New IniReader(rutaIni)
            Dim v = (ini.Read("guardar_xml_monto_tablas", "general") & "").Trim().ToLowerInvariant()
            Return (v = "1" OrElse v = "true" OrElse v = "si" OrElse v = "yes")
        Catch
            Return False
        End Try
    End Function

    Private Shared Function ToInt(value As String, fieldName As String) As Integer
        Dim n As Integer
        If Not Integer.TryParse((value & "").Trim(), n) Then
            Throw New ApplicationException($"Valor invÃ¡lido para {fieldName}: '{value}'")
        End If
        Return n
    End Function


    ' ==========================================
    ' 1) Dispatcher: segÃºn tipo eCF
    '    (AGREGO parÃ¡metros que son necesarios por PK)
    ' ==========================================
    Private Shared Sub GuardarXmlYTotalEnTablaSegunTipo(
    cadenaConexion As String,
    tipoECF As String,
    emp_codigo As String,
    suc_codigo As String,
    numeroDoc As String,
    xmlText As String,
    signedXml As XmlDocument,
    fechaEnvio As DateTime,
    isNotaCredito As Boolean,
    Optional fac_forma As String = Nothing,
    Optional tfa_codigo As String = Nothing,
    Optional sup_codigo As String = Nothing,
    Optional prov_fac_numero As String = Nothing
)

        Dim empI As Integer = ToInt(emp_codigo, NameOf(emp_codigo))
        Dim sucI As Integer = ToInt(suc_codigo, NameOf(suc_codigo))

        Dim monto As Decimal = ExtraerMontoTotalDesdeXml(signedXml)
        LogDetallado("Entro GuardarXmlYTotalEnTablaSegunTipo: ")
        LogDetallado("Entro GuardarXmlYTotalEnTablaSegunTipo: " + monto.ToString())

        Select Case (tipoECF & "").Trim()
            Case "31", "32", "44", "45", "46"
                Dim facNumI As Integer = ToInt(numeroDoc, "fac_numero")
                Dim tfaI As Integer = ToInt(tfa_codigo, "tfa_codigo")
                Dim forma As String = (fac_forma & "").Trim()
                If forma = "" Then Throw New ApplicationException("fac_forma requerida para actualizar Facturas.")

                UpdateFactura(cadenaConexion, empI, forma, tfaI, facNumI, sucI, monto, xmlText, fechaEnvio)

            Case "34"
                If isNotaCredito Then
                    Dim ncrI As Integer = ToInt(numeroDoc, "ncr_numero")
                    UpdateNotaCredito(cadenaConexion, empI, sucI, ncrI, monto, xmlText, fechaEnvio)
                Else
                    Dim devI As Integer = ToInt(numeroDoc, "dev_numero")
                    UpdateDevolucion(cadenaConexion, empI, sucI, devI, monto, xmlText, fechaEnvio)
                End If

            Case "41", "47"
                Dim supI As Integer = ToInt(sup_codigo, "sup_codigo")
                Dim facProv As String = (prov_fac_numero & "").Trim()
                If facProv = "" Then Throw New ApplicationException("prov_fac_numero requerido para actualizar ProvFacturas.")

                UpdateCompraProveedor(cadenaConexion, empI, supI, facProv, monto, xmlText, fechaEnvio)

            Case "43"
                Dim desI As Integer = ToInt(numeroDoc, "des_numero")
                UpdateDesembolso(cadenaConexion, empI, sucI, desI, monto, xmlText, fechaEnvio)

            Case Else
                ' fallback a Facturas
                Dim facNumI As Integer = ToInt(numeroDoc, "fac_numero")
                Dim tfaI As Integer = ToInt(tfa_codigo, "tfa_codigo")
                Dim forma As String = (fac_forma & "").Trim()
                If forma = "" Then Throw New ApplicationException("fac_forma requerida para actualizar Facturas.")

                UpdateFactura(cadenaConexion, empI, forma, tfaI, facNumI, sucI, monto, xmlText, fechaEnvio)
        End Select

    End Sub


    ' ==========================================
    ' 2) Monto desde XML
    ' ==========================================
    Private Shared Function ExtraerMontoTotalDesdeXml(doc As XmlDocument) As Decimal
        If doc Is Nothing OrElse doc.DocumentElement Is Nothing Then Return 0D

        Dim paths As String() = {
        "//Totales/MontoTotal",
        "//Totales/TotalMontoFactura",
        "//Totales/TotalFactura",
        "//Totales/MontoTotalFactura"
    }

        For Each p In paths
            Dim n = doc.SelectSingleNode(p)
            If n IsNot Nothing Then
                Dim s = (n.InnerText & "").Trim()
                Dim d As Decimal
                If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, d) Then Return d
                If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("es-DO"), d) Then Return d
            End If
        Next

        Return 0D
    End Function


    ' ==========================================
    ' 3) UPDATES por tabla (con PK reales)
    ' ==========================================

    ' dbo.Facturas PK: emp_codigo, fac_forma, tfa_codigo, fac_numero, suc_codigo
    Private Shared Sub UpdateFactura(
    cadena As String,
    emp As Integer,
    facForma As String,
    tfa As Integer,
    facNumero As Integer,
    suc As Integer,
    monto As Decimal,
    xmlText As String,
    fechaEnvio As DateTime
)
        Using cn As New SqlConnection(cadena)
            Using cmd As New SqlCommand("
UPDATE dbo.Facturas
SET DGII_MontoEnviado = @monto,
    DGII_XmlEnviado   = @xml,
    DGII_FechaEnvio   = @fecha
WHERE emp_codigo = @emp
  AND fac_forma  = @fac_forma
  AND tfa_codigo = @tfa
  AND fac_numero = @fac_num
  AND suc_codigo = @suc;", cn)

                cmd.Parameters.Add("@monto", SqlDbType.Decimal).Value = monto
                cmd.Parameters("@monto").Precision = 18
                cmd.Parameters("@monto").Scale = 2

                cmd.Parameters.Add("@xml", SqlDbType.NVarChar).Value = If(xmlText, "")
                cmd.Parameters("@xml").Size = -1 ' NVARCHAR(MAX)

                cmd.Parameters.Add("@fecha", SqlDbType.DateTime2).Value = fechaEnvio

                cmd.Parameters.Add("@emp", SqlDbType.Int).Value = emp
                cmd.Parameters.Add("@fac_forma", SqlDbType.VarChar, 1).Value = facForma
                cmd.Parameters.Add("@tfa", SqlDbType.Int).Value = tfa
                cmd.Parameters.Add("@fac_num", SqlDbType.Int).Value = facNumero
                cmd.Parameters.Add("@suc", SqlDbType.Int).Value = suc

                cn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub


    ' dbo.Devolucion PK: emp_codigo, suc_codigo, dev_numero
    Private Shared Sub UpdateDevolucion(
    cadena As String,
    emp As Integer,
    suc As Integer,
    devNumero As Integer,
    monto As Decimal,
    xmlText As String,
    fechaEnvio As DateTime
)
        Using cn As New SqlConnection(cadena)
            Using cmd As New SqlCommand("
UPDATE dbo.Devolucion
SET DGII_MontoEnviado = @monto,
    DGII_XmlEnviado   = @xml,
    DGII_FechaEnvio   = @fecha
WHERE emp_codigo = @emp
  AND suc_codigo = @suc
  AND dev_numero = @dev;", cn)

                cmd.Parameters.Add("@monto", SqlDbType.Decimal).Value = monto
                cmd.Parameters("@monto").Precision = 18
                cmd.Parameters("@monto").Scale = 2

                cmd.Parameters.Add("@xml", SqlDbType.NVarChar).Value = If(xmlText, "")
                cmd.Parameters("@xml").Size = -1

                cmd.Parameters.Add("@fecha", SqlDbType.DateTime2).Value = fechaEnvio

                cmd.Parameters.Add("@emp", SqlDbType.Int).Value = emp
                cmd.Parameters.Add("@suc", SqlDbType.Int).Value = suc
                cmd.Parameters.Add("@dev", SqlDbType.Int).Value = devNumero

                cn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub


    ' dbo.NotasCredito PK: emp_codigo, suc_codigo, ncr_numero
    Private Shared Sub UpdateNotaCredito(
    cadena As String,
    emp As Integer,
    suc As Integer,
    ncrNumero As Integer,
    monto As Decimal,
    xmlText As String,
    fechaEnvio As DateTime
)
        Using cn As New SqlConnection(cadena)
            Using cmd As New SqlCommand("
UPDATE dbo.NotasCredito
SET DGII_MontoEnviado = @monto,
    DGII_XmlEnviado   = @xml,
    DGII_FechaEnvio   = @fecha
WHERE emp_codigo = @emp
  AND suc_codigo = @suc
  AND ncr_numero = @ncr;", cn)

                cmd.Parameters.Add("@monto", SqlDbType.Decimal).Value = monto
                cmd.Parameters("@monto").Precision = 18
                cmd.Parameters("@monto").Scale = 2

                cmd.Parameters.Add("@xml", SqlDbType.NVarChar).Value = If(xmlText, "")
                cmd.Parameters("@xml").Size = -1

                cmd.Parameters.Add("@fecha", SqlDbType.DateTime2).Value = fechaEnvio

                cmd.Parameters.Add("@emp", SqlDbType.Int).Value = emp
                cmd.Parameters.Add("@suc", SqlDbType.Int).Value = suc
                cmd.Parameters.Add("@ncr", SqlDbType.Int).Value = ncrNumero

                cn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub


    ' dbo.ProvFacturas PK: emp_codigo, sup_codigo, fac_numero (VARCHAR(15))
    Private Shared Sub UpdateCompraProveedor(
    cadena As String,
    emp As Integer,
    sup As Integer,
    facNumero As String,
    monto As Decimal,
    xmlText As String,
    fechaEnvio As DateTime
)
        Using cn As New SqlConnection(cadena)
            Using cmd As New SqlCommand("
UPDATE dbo.ProvFacturas
SET DGII_MontoEnviado = @monto,
    DGII_XmlEnviado   = @xml,
    DGII_FechaEnvio   = @fecha
WHERE emp_codigo = @emp
  AND sup_codigo = @sup
  AND fac_numero = @fac;", cn)

                cmd.Parameters.Add("@monto", SqlDbType.Decimal).Value = monto
                cmd.Parameters("@monto").Precision = 18
                cmd.Parameters("@monto").Scale = 2

                cmd.Parameters.Add("@xml", SqlDbType.NVarChar).Value = If(xmlText, "")
                cmd.Parameters("@xml").Size = -1

                cmd.Parameters.Add("@fecha", SqlDbType.DateTime2).Value = fechaEnvio

                cmd.Parameters.Add("@emp", SqlDbType.Int).Value = emp
                cmd.Parameters.Add("@sup", SqlDbType.Int).Value = sup
                cmd.Parameters.Add("@fac", SqlDbType.VarChar, 15).Value = facNumero

                cn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub


    ' dbo.Desembolsos PK: emp_codigo, suc_codigo, des_numero
    Private Shared Sub UpdateDesembolso(
    cadena As String,
    emp As Integer,
    suc As Integer,
    desNumero As Integer,
    monto As Decimal,
    xmlText As String,
    fechaEnvio As DateTime
)
        Using cn As New SqlConnection(cadena)
            Using cmd As New SqlCommand("
UPDATE dbo.Desembolsos
SET DGII_MontoEnviado = @monto,
    DGII_XmlEnviado   = @xml,
    DGII_FechaEnvio   = @fecha
WHERE emp_codigo = @emp
  AND suc_codigo = @suc
  AND des_numero = @des;", cn)

                cmd.Parameters.Add("@monto", SqlDbType.Decimal).Value = monto
                cmd.Parameters("@monto").Precision = 18
                cmd.Parameters("@monto").Scale = 2

                cmd.Parameters.Add("@xml", SqlDbType.NVarChar).Value = If(xmlText, "")
                cmd.Parameters("@xml").Size = -1

                cmd.Parameters.Add("@fecha", SqlDbType.DateTime2).Value = fechaEnvio

                cmd.Parameters.Add("@emp", SqlDbType.Int).Value = emp
                cmd.Parameters.Add("@suc", SqlDbType.Int).Value = suc
                cmd.Parameters.Add("@des", SqlDbType.Int).Value = desNumero

                cn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' dbo.Factura_RestBar: llaves EMP_CODIGO, SUC_CODIGO, FacturaID, CajeroID, CajaID (flujo RESBAR).
    ''' </summary>
    Private Shared Sub UpdateFacturaRestBar(
        cadena As String,
        emp As Integer,
        suc As Integer,
        facturaId As Integer,
        cajeroId As Integer,
        cajaId As Integer,
        monto As Decimal,
        xmlText As String,
        fechaEnvio As DateTime
    )
        Using cn As New SqlConnection(cadena)
            Using cmd As New SqlCommand("
UPDATE dbo.Factura_RestBar
SET DGII_MontoEnviado = @monto,
    DGII_XmlEnviado   = @xml,
    DGII_FechaEnvio   = @fecha
WHERE EMP_CODIGO = @emp
  AND SUC_CODIGO = @suc
  AND FacturaID  = @fac
  AND CajeroID   = @cajero
  AND CajaID     = @caja;", cn)

                cmd.Parameters.Add("@monto", SqlDbType.Decimal).Value = monto
                cmd.Parameters("@monto").Precision = 18
                cmd.Parameters("@monto").Scale = 2

                cmd.Parameters.Add("@xml", SqlDbType.NVarChar).Value = If(xmlText, "")
                cmd.Parameters("@xml").Size = -1

                cmd.Parameters.Add("@fecha", SqlDbType.DateTime).Value = fechaEnvio

                cmd.Parameters.Add("@emp", SqlDbType.Int).Value = emp
                cmd.Parameters.Add("@suc", SqlDbType.Int).Value = suc
                cmd.Parameters.Add("@fac", SqlDbType.Int).Value = facturaId
                cmd.Parameters.Add("@cajero", SqlDbType.Int).Value = cajeroId
                cmd.Parameters.Add("@caja", SqlDbType.Int).Value = cajaId

                cn.Open()
                Dim filas As Integer = cmd.ExecuteNonQuery()
                If filas = 0 Then
                    Helper.RegistrarLogCliente($"[UpdateFacturaRestBar] 0 filas. PK EMP={emp} SUC={suc} FacturaID={facturaId} Cajero={cajeroId} Caja={cajaId} monto={monto}")
                    LogDetallado("UpdateFacturaRestBar: 0 filas actualizadas. PK EMP=" & emp & " SUC=" & suc & " FacturaID=" & facturaId & " Cajero=" & cajeroId & " Caja=" & cajaId)
                Else
                    Helper.RegistrarLogCliente($"[UpdateFacturaRestBar] OK {filas} fila(s) FacturaID={facturaId} Cajero={cajeroId} Caja={cajaId} monto={monto}")
                End If
            End Using
        End Using
    End Sub

    Private Shared Sub UpdateMontosTicket(
    cadena As String,
    usuCodigo As Integer,
    fecha As DateTime,
    caja As Integer,
    ticket As Integer,
    monto As Decimal,
    xmlText As String,
    fechaEnvio As DateTime
)

       Helper.RegistrarLogCliente($"[UpdateMontosTicket]")
            

        Using cn As New SqlConnection(cadena)
            Using cmd As New SqlCommand("
UPDATE dbo.Montos_Ticket
SET DGII_MontoEnviado = @monto,
    DGII_XmlEnviado   = @xml,
    DGII_FechaEnvio   = @fechaEnvio
WHERE usu_codigo = @usu 
  AND caja       = @caja
  AND ticket     = @ticket;", cn)

                cmd.Parameters.Add("@monto", SqlDbType.Decimal).Value = monto
                cmd.Parameters("@monto").Precision = 18
                cmd.Parameters("@monto").Scale = 2

                cmd.Parameters.Add("@xml", SqlDbType.NVarChar).Value = If(xmlText, "")
                cmd.Parameters("@xml").Size = -1

                ' Si tu columna la creas DATETIME, cambia a SqlDbType.DateTime
                cmd.Parameters.Add("@fechaEnvio", SqlDbType.DateTime).Value = fechaEnvio

                cmd.Parameters.Add("@usu", SqlDbType.Int).Value = usuCodigo
               
                cmd.Parameters.Add("@caja", SqlDbType.Int).Value = caja
                cmd.Parameters.Add("@ticket", SqlDbType.Int).Value = ticket

                cn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub


    Public Function EnviarNotaDebito(emp As String,
                                             suc As String,
                                                sup As String,
                                             facNumero As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()
            Dim encfUsar = If(eNCF, "").Trim()
            Dim empNd As Integer, sucNd As Integer, numNd As Integer
            If Integer.TryParse(emp, empNd) AndAlso Integer.TryParse(suc, sucNd) AndAlso Integer.TryParse(facNumero, numNd) Then
                Dim docNd = Helper.EncfDocumentoRef.NotaDebito(empNd, sucNd, numNd)
                Dim jsonYaNd = IntentarReusarEnvioExistenteDgii(
                    docNd, If(String.IsNullOrWhiteSpace(tipoECF), "33", tipoECF))
                If Not String.IsNullOrWhiteSpace(jsonYaNd) Then Return MsgYaEnviadoNoReenviar
                If String.IsNullOrWhiteSpace(encfUsar) Then
                    encfUsar = EncFYaAsignado(Helper.ObtenerEstadoEncf(cadenaConexion, docNd))
                End If
                If String.IsNullOrWhiteSpace(encfUsar) Then
                    encfUsar = LlamarAsignarSecuenciaNotaDebitoDGII(cadenaConexion, empNd, sucNd, facNumero)
                End If
            End If
            If String.IsNullOrWhiteSpace(encfUsar) Then
                Return "Error: no hay eNCF para la nota de débito " & facNumero
            End If
            glbncfEnvia = encfUsar
            ProcesoEnvioFacturaE33(RncCliente, encfUsar, If(String.IsNullOrWhiteSpace(tipoECF), "33", tipoECF), emp, suc, "", facNumero, empRnc)
            Return $"OK: Factura {facNumero} tipo {tipoECF} lista para envÃ­o desde empresa {emp}, sucursal {suc}."
        Catch ex As Exception
            Return "Error: " & ex.Message
        End Try

    End Function

    Public Function EnviarPagosExterior(emp As String,
                                             suc As String,
                                                sup As String,
                                             facNumero As String,
                                             empRnc As String, eNCF As String, RncCliente As String,
                                              Optional facForma As String = "",
                                             Optional tfaCodigo As String = "",
                                             Optional tipoECF As String = "") As String
        Try
            CargarConfiguracion()
            Seguridad.ObtenerToken()

            Dim empPe As Integer, sucPe As Integer, supPe As Integer
            If Integer.TryParse(emp, empPe) AndAlso Integer.TryParse(suc, sucPe) AndAlso Integer.TryParse(sup, supPe) Then
                Dim jsonYaPe = IntentarReusarEnvioExistenteDgii(
                    Helper.EncfDocumentoRef.Compra(empPe, sucPe, supPe, facNumero), "47")
                If Not String.IsNullOrWhiteSpace(jsonYaPe) Then Return MsgYaEnviadoNoReenviar
            End If

            Dim nuevoENCF As String = LlamarAsignarSecuenciaComprasDGII(cadenaConexion,
                                                     Convert.ToInt32(emp),
                                                     Convert.ToInt32(suc),
                                                     Convert.ToInt32(sup),
                                                     facNumero,
                                                     47)
            If String.IsNullOrWhiteSpace(nuevoENCF) Then
                Return "ERROR: No se pudo asignar eNCF tipo 47. Ejecute docs/SP_AsignarSecuenciaFacturasProvDGII.sql y verifique SecuenciaDGII tipo 47 para la empresa."
            End If
            glbncfEnvia = nuevoENCF

            Dim tipoEnvio As String = "47"
            Dim r As String = ProcesoEnvioFacturaE47(RncCliente, nuevoENCF, tipoEnvio, emp, suc, facNumero, empRnc, sup)
            Return r
        Catch ex As Exception
            Return "ERROR: " & ex.Message
        End Try

    End Function

    Public Function ProbarConexion() As String
        Return "ConexiÃ³n exitosa desde COM"
    End Function

    ''' <summary>
    ''' Selecciona certificado, connectionString y RNC emisor según el RNC de la empresa.
    ''' Llamar una vez al iniciar el DashA (Delphi): ConfigurarPorRnc(RNC_de_la_empresa).
    ''' config.ini: sección [rnc_XXXXX] o [XXXXX] por cada cliente; [general] para URLs comunes.
    ''' </summary>
    Public Function ConfigurarPorRnc(rnc As String) As String
        Try
            Dim n = RutaHelper.NormalizarRnc(rnc)
            If n = "" Then Return "Error: RNC vacío o inválido."

            If Not String.Equals(n, rncConfigActual, StringComparison.Ordinal) Then
                rncConfigActual = n
                GlobalVariables.token = ""
                configCargada = False
                cadenaConexion = ""
            End If

            CargarConfiguracion()

            If String.IsNullOrWhiteSpace(GlobalVariables.pathCertificado) Then
                Return "Error: pathCertificado no configurado para RNC " & n & "."
            End If

            Dim pathCert = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            If Not File.Exists(pathCert) Then
                Return "Error: certificado no encontrado: " & pathCert
            End If

            Return "OK"
        Catch ex As Exception
            configCargada = False
            cadenaConexion = ""
            Return "Error: " & ex.Message
        End Try
    End Function

    ''' <summary>
    ''' Consulta el directorio DGII por RNC del cliente/comprador.
    ''' Usa Parametros.Usa_FacturacionElectronica: si FE activo, certificado propio en config.ini;
    ''' si no, certificado del dueño del sistema en config.ini [sistema] (carpeta certificados junto a la DLL).
    ''' </summary>
    ''' <param name="rncConsulta">RNC a buscar en el directorio (comprador/cliente).</param>
    ''' <param name="urlBaseDgii">URL base DGII (ej. https://ecf.dgii.gov.do/CerteCF/). Vacío = config.ini, Parametros o producción.</param>
    ''' <param name="rncEmisorCertificado">RNC emisor para leer certificado de config.ini [rnc_XXX] (opcional).</param>
    ''' <param name="pathCertificado">Ruta .p12 relativa a la DLL o absoluta (opcional si hay config.ini o token).</param>
    ''' <param name="passCertificado">Clave del certificado (opcional).</param>
    ''' <param name="tokenDgii">Token JWT vigente; si viene vacío se obtiene con el certificado.</param>
    ''' <param name="emp_codigo">Código empresa DashA para leer Parametros (opcional si está en config.ini).</param>
    ''' <returns>JSON: ok, encontrado, nombre, rnc, urlRecepcion, urlAceptacion, urlOpcional, message, httpStatus, rawResponse</returns>
    Public Function ConsultarDirectorioPorRnc(
        rncConsulta As String,
        urlBaseDgii As String,
        Optional rncEmisorCertificado As String = "",
        Optional pathCertificado As String = "",
        Optional passCertificado As String = "",
        Optional tokenDgii As String = "",
        Optional emp_codigo As String = ""
    ) As String
        Try
            AsegurarConfiguracionConsultaDirectorio(rncEmisorCertificado)
            Dim emp = ResolverEmpCodigoConsulta(emp_codigo, rncEmisorCertificado)
            Dim conn = ObtenerCadenaConexionConsulta(rncEmisorCertificado)

            Dim urlBase = RutaHelper.NormalizarUrlBaseDgii(urlBaseDgii)
            If urlBase = "" Then urlBase = ResolverUrlBaseDgii("")
            If urlBase = "" AndAlso conn <> "" AndAlso emp <> "" Then
                urlBase = RutaHelper.NormalizarUrlBaseDgii(Helper.ObtenerUrlFacturacionElectronica(conn, emp))
            End If
            If urlBase = "" Then
                Return Helper.ConstruirJsonDirectorioDgii(False, New Helper.DirectorioDgiiConsulta With {
                    .Mensaje = "Indique urlBaseDgii, url_base en config.ini o URL_FacturacionElectronica en Parametros."
                })
            End If

            Dim token = If(tokenDgii, "").Trim()
            If token = "" AndAlso Not String.IsNullOrWhiteSpace(GlobalVariables.token) Then
                token = GlobalVariables.token.Trim()
            End If

            If token = "" Then
                Dim pathCert As String = ""
                Dim passCert As String = ""
                Dim usaFe As Boolean = False
                If conn <> "" AndAlso emp <> "" Then
                    usaFe = Helper.ObtenerUsaFacturacionElectronica(conn, emp)
                End If

                If Not ResolverCertificadoConsultaDirectorio(
                    usaFe,
                    rncEmisorCertificado,
                    pathCertificado,
                    passCertificado,
                    pathCert,
                    passCert) Then
                    Dim msgCert = If(usaFe,
                        "Se requiere tokenDgii o certificado FE de la empresa (config.ini [rnc_XXX], ConfigurarPorRnc o pathCertificado + passCertificado).",
                        "Se requiere tokenDgii o certificado del dueño del sistema en config.ini [sistema] (pathCertificado + passCertificado junto a la DLL).")
                    Return Helper.ConstruirJsonDirectorioDgii(False, New Helper.DirectorioDgiiConsulta With {
                        .Mensaje = msgCert
                    })
                End If

                token = Seguridad.ObtenerTokenDgii(urlBase, pathCert, passCert)
                If token = "" Then
                    Dim detToken = If(String.IsNullOrWhiteSpace(Seguridad.UltimoErrorToken),
                        "Verifique certificado, clave y urlBaseDgii.",
                        Seguridad.UltimoErrorToken)
                    Return Helper.ConstruirJsonDirectorioDgii(False, New Helper.DirectorioDgiiConsulta With {
                        .Mensaje = "No se pudo obtener token DGII. " & detToken
                    })
                End If
            End If

            Dim consulta = Helper.ConsultarDirectorioPorRncHttp(urlBase, token, rncConsulta)
            Return Helper.ConstruirJsonDirectorioDgii(consulta.Encontrado, consulta)
        Catch ex As Exception
            Return Helper.ConstruirJsonDirectorioDgii(False, New Helper.DirectorioDgiiConsulta With {
                .Mensaje = ex.Message
            })
        End Try
    End Function

    Private Shared Function ResolverUrlBaseDgii(urlBaseParam As String) As String
        Dim u = RutaHelper.NormalizarUrlBaseDgii(urlBaseParam)
        If u <> "" Then Return u

        Try
            Dim rutaIni = RutaHelper.ObtenerRutaConfigIni()
            If File.Exists(rutaIni) Then
                Dim ini As New IniReader(rutaIni)
                u = RutaHelper.NormalizarUrlBaseDgii(ini.Read("url_base", "general"))
                If u <> "" Then Return u
            End If
        Catch
        End Try

        Return RutaHelper.NormalizarUrlBaseDgii(RutaHelper.UrlBaseDgiiProduccion)
    End Function

    Private Shared Function ResolverCertificadoDgii(
        rncEmisor As String,
        pathCertParam As String,
        passCertParam As String,
        ByRef pathCertFull As String,
        ByRef passCert As String
    ) As Boolean
        pathCertFull = ""
        passCert = ""

        If Not String.IsNullOrWhiteSpace(pathCertParam) Then
            pathCertFull = If(Path.IsPathRooted(pathCertParam.Trim()),
                              pathCertParam.Trim(),
                              ObtenerRutaArchivo(pathCertParam.Trim()))
            passCert = If(passCertParam, "").Trim()
            Return passCert <> "" AndAlso File.Exists(pathCertFull)
        End If

        Try
            Dim rutaIni = RutaHelper.ObtenerRutaConfigIni()
            If File.Exists(rutaIni) Then
                Dim ini As New IniReader(rutaIni)
                Dim seccion = "general"
                Dim r = RutaHelper.NormalizarRnc(rncEmisor)
                If r <> "" Then
                    Dim secPrefijo = "rnc_" & r
                    If TieneSeccionEmpresa(ini, secPrefijo) Then
                        seccion = secPrefijo
                    ElseIf TieneSeccionEmpresa(ini, r) Then
                        seccion = r
                    End If
                End If

                Dim pathIni = LeerClaveIni(ini, "pathCertificado", seccion)
                Dim passIni = LeerClaveIni(ini, "passCertificado", seccion)
                If pathIni <> "" AndAlso passIni <> "" Then
                    pathCertFull = ObtenerRutaArchivo(pathIni)
                    passCert = passIni
                    Return File.Exists(pathCertFull)
                End If
            End If
        Catch
        End Try

        If Not String.IsNullOrWhiteSpace(GlobalVariables.pathCertificado) AndAlso
           Not String.IsNullOrWhiteSpace(GlobalVariables.passCertificado) Then
            pathCertFull = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            passCert = GlobalVariables.passCertificado
            Return File.Exists(pathCertFull)
        End If

        Return False
    End Function

    Private Shared Sub AsegurarConfiguracionConsultaDirectorio(rncEmisor As String)
        If configCargada AndAlso Not String.IsNullOrEmpty(cadenaConexion) Then Return
        Try
            Dim rnc = RutaHelper.NormalizarRnc(rncEmisor)
            If rnc <> "" Then
                AplicarConfiguracionDesdeIni(rnc)
                Return
            End If
            CargarConfiguracion()
        Catch
        End Try
    End Sub

    Private Shared Function ObtenerCadenaConexionConsulta(rncEmisor As String) As String
        If Not String.IsNullOrWhiteSpace(cadenaConexion) Then Return cadenaConexion
        Try
            Dim rutaIni = RutaHelper.ObtenerRutaConfigIni()
            If Not File.Exists(rutaIni) Then Return ""
            Dim ini As New IniReader(rutaIni)
            Dim seccion = ResolverSeccionConfigRnc(ini, rncEmisor)
            Return LeerClaveIni(ini, "connectionString", seccion)
        Catch
            Return ""
        End Try
    End Function

    Private Shared Function ResolverEmpCodigoConsulta(empParam As String, rncEmisor As String) As String
        Dim emp = If(empParam, "").Trim()
        If emp <> "" Then Return emp
        Try
            Dim rutaIni = RutaHelper.ObtenerRutaConfigIni()
            If Not File.Exists(rutaIni) Then Return ""
            Dim ini As New IniReader(rutaIni)
            Dim seccion = ResolverSeccionConfigRnc(ini, rncEmisor)
            emp = LeerClaveIni(ini, "emp_codigo", seccion)
            If emp <> "" Then Return emp
            If Not String.IsNullOrWhiteSpace(rncConfigActual) Then
                seccion = ResolverSeccionConfigRnc(ini, rncConfigActual)
                Return LeerClaveIni(ini, "emp_codigo", seccion)
            End If
        Catch
        End Try
        Return ""
    End Function

    Private Shared Function ResolverCertificadoConsultaDirectorio(
        usaFacturacionElectronica As Boolean,
        rncEmisor As String,
        pathCertParam As String,
        passCertParam As String,
        ByRef pathCertFull As String,
        ByRef passCert As String
    ) As Boolean
        If usaFacturacionElectronica Then
            Return ResolverCertificadoDgii(rncEmisor, pathCertParam, passCertParam, pathCertFull, passCert)
        End If
        Return ResolverCertificadoSistemaConfigIni(pathCertParam, passCertParam, pathCertFull, passCert)
    End Function

    ''' <summary>
    ''' Certificado del dueño del sistema (config.ini [sistema], misma carpeta certificados que la DLL).
    ''' </summary>
    Private Shared Function ResolverCertificadoSistemaConfigIni(
        pathCertParam As String,
        passCertParam As String,
        ByRef pathCertFull As String,
        ByRef passCert As String
    ) As Boolean
        pathCertFull = ""
        passCert = ""

        If Not String.IsNullOrWhiteSpace(pathCertParam) Then
            pathCertFull = If(Path.IsPathRooted(pathCertParam.Trim()),
                              pathCertParam.Trim(),
                              ObtenerRutaArchivo(pathCertParam.Trim()))
            passCert = If(passCertParam, "").Trim()
            Return passCert <> "" AndAlso File.Exists(pathCertFull)
        End If

        Try
            Dim rutaIni = RutaHelper.ObtenerRutaConfigIni()
            If Not File.Exists(rutaIni) Then Return False

            Dim ini As New IniReader(rutaIni)
            Dim pathIni = LeerClaveIni(ini, "pathCertificado", "sistema")
            Dim passIni = LeerClaveIni(ini, "passCertificado", "sistema")
            If pathIni <> "" AndAlso passIni <> "" Then
                pathCertFull = ObtenerRutaArchivo(pathIni)
                passCert = passIni
                Return File.Exists(pathCertFull)
            End If
        Catch
        End Try

        Return False
    End Function

    Private Shared Sub CargarConfiguracion()
        If configCargada AndAlso Not String.IsNullOrEmpty(cadenaConexion) Then Return
        AplicarConfiguracionDesdeIni(rncConfigActual)
    End Sub

    Private Shared Sub AplicarConfiguracionDesdeIni(rnc As String)
        Dim rutaIni = RutaHelper.ObtenerRutaConfigIni()
        If Not File.Exists(rutaIni) Then
            Throw New FileNotFoundException("No se encontró config.ini junto a la DLL.", rutaIni)
        End If

        Dim ini As New IniReader(rutaIni)
        GlobalVariables.url_base = (ini.Read("url_base", "general") & "").Trim()
        GlobalVariables.urlfc_base = (ini.Read("urlfc_base", "general") & "").Trim()

        Dim seccion = ResolverSeccionConfigRnc(ini, rnc)
        cadenaConexion = LeerClaveIni(ini, "connectionString", seccion)
        GlobalVariables.pathCertificado = LeerClaveIni(ini, "pathCertificado", seccion)
        GlobalVariables.passCertificado = LeerClaveIni(ini, "passCertificado", seccion)

        Dim empRncIni = LeerClaveIni(ini, "emp_rnc", seccion)
        Dim rncNorm = RutaHelper.NormalizarRnc(rnc)
        GlobalVariables.emp_rnc = If(empRncIni <> "", empRncIni, rncNorm)

        If String.IsNullOrWhiteSpace(cadenaConexion) Then
            Throw New ApplicationException(
                "connectionString no configurado en config.ini" &
                If(rncNorm <> "", " para RNC " & rncNorm & " (sección [" & seccion & "])", " ([general])") & ".")
        End If

        configCargada = True
    End Sub

    Private Shared Function ResolverSeccionConfigRnc(ini As IniReader, rnc As String) As String
        Dim r = RutaHelper.NormalizarRnc(rnc)
        If r = "" Then Return "general"

        Dim secPrefijo = "rnc_" & r
        If TieneSeccionEmpresa(ini, secPrefijo) Then Return secPrefijo
        If TieneSeccionEmpresa(ini, r) Then Return r

        Return "general"
    End Function

    Private Shared Function TieneSeccionEmpresa(ini As IniReader, seccion As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(ini.Read("connectionString", seccion)) OrElse
               Not String.IsNullOrWhiteSpace(ini.Read("pathCertificado", seccion))
    End Function

    Private Shared Function LeerClaveIni(ini As IniReader, clave As String, seccion As String) As String
        Dim v = (ini.Read(clave, seccion) & "").Trim()
        If v <> "" OrElse String.Equals(seccion, "general", StringComparison.OrdinalIgnoreCase) Then Return v
        Return (ini.Read(clave, "general") & "").Trim()
    End Function

    Private Shared Sub ProcesarRespuestaAPI(
    RncCliente As String, TipoeCF As String, cadenaConexion As String,
    emp_codigo As String, suc_codigo As String, respuestaConsulta As String,
    facNumero As String, signedXml As XmlDocument, codigoseguridad As String,
    fac_forma As String, tfa_codigo As String, sup_codigo As String, fechaFirma As DateTime, isNotaCredito As Boolean)

        Try
            Dim parsedDgii = Helper.ParsearRespuestaDgii(respuestaConsulta)
            Dim estado As String = parsedDgii.Estado
            Dim trackId As String = If(parsedDgii.TrackId, "")
            Dim encf As String = If(parsedDgii.Encf, "")
            Dim fechaRecepcion As String = DateTime.Now.ToString("M/d/yyyy h:mm:ss tt")
            Dim codigoMensaje As String = If(parsedDgii.CodigoMensaje, "")
            Dim mensajeError As String = If(parsedDgii.Mensaje, "")
            Dim secuenciautilizada As Boolean = parsedDgii.SecuenciaUtilizada

            Try
                Dim root = JObject.Parse(respuestaConsulta)
                Dim payload As JToken = root
                If root.SelectToken("body") IsNot Nothing Then payload = root.SelectToken("body")
                If payload.SelectToken("fechaRecepcion") IsNot Nothing Then fechaRecepcion = CStr(payload.SelectToken("fechaRecepcion"))
            Catch
            End Try

            ' ---------- XML crudo para bitÃ¡cora ----------
            Dim xmlText As String = ""
            Try
                Using sw As New StringWriter()
                    Using xw As New XmlTextWriter(sw)
                        signedXml.WriteTo(xw)
                    End Using
                    xmlText = sw.ToString()
                End Using
            Catch
            End Try

            ' ====== DETECCIÃ“N DE SECUENCIA USADA ======
            Dim forzarSecuenciaUsada As Boolean = False
            If ContainsSecuenciaYaUsada(mensajeError) Then
                forzarSecuenciaUsada = True
            ElseIf Not String.IsNullOrWhiteSpace(codigoMensaje) AndAlso codigoMensaje.Trim() = "1209" Then
                forzarSecuenciaUsada = True
            End If
            If forzarSecuenciaUsada Then
                secuenciautilizada = True
            End If

            ' ---------- Fecha recepciÃ³n ----------
            Dim fechaRec As DateTime
            If Not DateTime.TryParseExact(
            fechaRecepcion,
            "M/d/yyyy h:mm:ss tt",
            Globalization.CultureInfo.InvariantCulture,
            Globalization.DateTimeStyles.None,
            fechaRec) Then
                fechaRec = DateTime.Now
            End If

            ' ---------- SIEMPRE: bitÃ¡cora ----------
            Helper.InsertarBitacoraDGII(
            cadenaConexion, emp_codigo, facNumero, estado, trackId,
            codigoMensaje, mensajeError, xmlText, fechaRec, "", "",
            Helper.ResolverTipoBitacoraDgii(TipoeCF, isNotaCredito)
        )
            ' ---------- ActualizaciÃ³n de BD: Aceptado o Rechazado ----------
            Dim aceptado As Integer = If(Helper.EsEstadoAceptadoDgii(estado), 1, 0)
            Helper.AjustarPersistenciaPendiente(aceptado, secuenciautilizada, estado)

            Select Case TipoeCF
                Case "31", "32", "44", "45", "46"
                    If fac_forma.Trim().Length > 0 Then
                        Dim p As SqlParameter() = {
                        New SqlParameter("@emp_codigo", emp_codigo),
                        New SqlParameter("@suc_codigo", suc_codigo),
                        New SqlParameter("@fac_numero", facNumero),
                        New SqlParameter("@eNCF", encf),
                        New SqlParameter("@fac_forma", fac_forma),
                        New SqlParameter("@tfa_codigo", tfa_codigo),
                        New SqlParameter("@codigoseguridad", codigoseguridad),
                        New SqlParameter("@fechafirma", fechaFirma),
                        New SqlParameter("@aceptado", aceptado),
                        New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                    }
                        Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGI", p)
                    End If

                Case "41", "47" ' Compras: ProvFacturas

                    Dim p As SqlParameter() = {
                    New SqlParameter("@emp_codigo", emp_codigo),
                    New SqlParameter("@suc_codigo", suc_codigo),
                    New SqlParameter("@fac_numero", facNumero.ToString()),
                    New SqlParameter("@eNCF", encf),
                    New SqlParameter("@sup_codigo", sup_codigo),
                    New SqlParameter("@codigoseguridad", codigoseguridad),
                    New SqlParameter("@fechafirma", fechaFirma),
                    New SqlParameter("@aceptado", aceptado),
                    New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                }
                    Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGICOMPRAS", p)

                Case "34" ' Devoluciones
                    If isNotaCredito Then
                        Dim p As SqlParameter() = {
                           New SqlParameter("@emp_codigo", emp_codigo),
                           New SqlParameter("@suc_codigo", suc_codigo),
                           New SqlParameter("@fac_numero", facNumero),
                           New SqlParameter("@eNCF", encf),
                           New SqlParameter("@codigoseguridad", codigoseguridad),
                           New SqlParameter("@fechafirma", fechaFirma),
                           New SqlParameter("@aceptado", aceptado),
                           New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                        }
                        Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGINC", p)
                    Else
                        Dim p As SqlParameter() = {
                           New SqlParameter("@emp_codigo", emp_codigo),
                           New SqlParameter("@suc_codigo", suc_codigo),
                           New SqlParameter("@fac_numero", facNumero),
                           New SqlParameter("@eNCF", encf),
                           New SqlParameter("@codigoseguridad", codigoseguridad),
                           New SqlParameter("@fechafirma", fechaFirma),
                           New SqlParameter("@aceptado", aceptado),
                           New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                       }
                        Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGIDEV", p)
                    End If


                Case "43" ' Desembolsos (si tu SP no acepta @aceptado, solo actualiza si fue aceptado)

                    Dim p As SqlParameter() = {
                        New SqlParameter("@emp_codigo", emp_codigo),
                        New SqlParameter("@suc_codigo", suc_codigo),
                        New SqlParameter("@des_numero", facNumero),
                        New SqlParameter("@eNCF", encf),
                        New SqlParameter("@codigoseguridad", codigoseguridad),
                        New SqlParameter("@fechafirma", fechaFirma),
                        New SqlParameter("@aceptado", aceptado),
                        New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                    }
                    Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGIDESEMBOLSOS", p)


                Case "33" ' Nota de dÃ©bito (si tu SP no acepta @aceptado, solo cuando es aceptado)
                    If aceptado = 1 Then
                        Dim p As SqlParameter() = {
                        New SqlParameter("@emp_codigo", emp_codigo),
                        New SqlParameter("@suc_codigo", suc_codigo),
                        New SqlParameter("@fac_numero", facNumero),
                        New SqlParameter("@eNCF", encf),
                        New SqlParameter("@codigoseguridad", codigoseguridad),
                        New SqlParameter("@fechafirma", fechaFirma)
                    }
                        Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGINOTADEBITO", p)
                    End If

                Case Else
                    ' Por defecto trata como ventas (SP_UPDATEDATOSDGI con @aceptado)
                    Dim p As SqlParameter() = {
                    New SqlParameter("@emp_codigo", emp_codigo),
                    New SqlParameter("@suc_codigo", suc_codigo),
                    New SqlParameter("@fac_numero", facNumero),
                    New SqlParameter("@eNCF", encf),
                    New SqlParameter("@fac_forma", fac_forma),
                    New SqlParameter("@tfa_codigo", tfa_codigo),
                    New SqlParameter("@codigoseguridad", codigoseguridad),
                    New SqlParameter("@fechafirma", fechaFirma),
                    New SqlParameter("@aceptado", aceptado),
                    New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                }
                    Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGI", p)
            End Select

            If aceptado = 1 Then
                GuardarXmlYTotalEnTablaSegunTipo(
                        cadenaConexion:=cadenaConexion,
                        tipoECF:=TipoeCF,
                        emp_codigo:=emp_codigo,
                        suc_codigo:=suc_codigo,
                        numeroDoc:=facNumero,
                        xmlText:=xmlText,
                        signedXml:=signedXml,
                        fechaEnvio:=fechaRec,
                        isNotaCredito:=isNotaCredito,
                        fac_forma:=fac_forma,
                        tfa_codigo:=tfa_codigo
                    )
            End If


            ' ---------- EnvÃ­o al cliente SOLO si fue aceptado ----------
            If aceptado = 1 AndAlso RncCliente.Trim().Length > 0 Then
                Try
                    Dim resCli = EnviarFacturaClienteSiTieneUrlAsync(
                    RncCliente, GlobalVariables.token, signedXml, facNumero
                ).GetAwaiter().GetResult()
                    Helper.RegistrarLogCliente($"Factura {facNumero} enviada al cliente {RncCliente}. Resultado: {resCli}")
                Catch ex As Exception
                    Helper.RegistrarLogCliente($"Error enviando a cliente {RncCliente}: {ex.Message}")
                End Try
            End If

        Catch ex As Exception
            Helper.RegistrarLogCliente("ProcesarRespuestaAPI EX: " & ex.ToString())
            'Try
            '    Helper.InsertarBitacoraDGII(cadenaConexion, emp_codigo, facNumero, "EXCEPCION", "", "", ex.Message, "", DateTime.Now)
            'Catch
            'End Try
        End Try
    End Sub

    ''' <summary>
    ''' Parsea la respuesta JSON de consulta de estado DGII y actualiza solo la BD (AceptadoDGII, Error_DGII, bitÃ¡cora).
    ''' No guarda XML ni envÃ­a al cliente. Usado por ReconsultarEstadoFacturaDGII.
    ''' </summary>
    Private Shared Function ProcesarRespuestaAPISoloEstado(
            cadenaConexion As String,
            emp_codigo As String, suc_codigo As String, facNumero As String,
            fac_forma As String, tfa_codigo As String, TipoeCF As String,
            respuestaConsulta As String, sup_codigo As String, isNotaCredito As Boolean) As String
        Dim parsedDgii = Helper.ParsearRespuestaDgii(respuestaConsulta)
        Dim estado As String = parsedDgii.Estado
        Dim trackId As String = If(parsedDgii.TrackId, "")
        Dim encf As String = If(parsedDgii.Encf, "")
        Dim fechaRecepcion As String = DateTime.Now.ToString("M/d/yyyy h:mm:ss tt")
        Dim codigoMensaje As String = If(parsedDgii.CodigoMensaje, "")
        Dim mensajeError As String = If(parsedDgii.Mensaje, "")
        Dim secuenciautilizada As Boolean = parsedDgii.SecuenciaUtilizada
        Try
            Try
                Dim root = JObject.Parse(respuestaConsulta)
                Dim payload As JToken = root
                If root.SelectToken("body") IsNot Nothing Then payload = root.SelectToken("body")
                If payload.SelectToken("fechaRecepcion") IsNot Nothing Then fechaRecepcion = CStr(payload.SelectToken("fechaRecepcion"))
            Catch
            End Try

            Dim forzarSecuenciaUsada As Boolean = False
            If ContainsSecuenciaYaUsada(mensajeError) Then forzarSecuenciaUsada = True
            If Not String.IsNullOrWhiteSpace(codigoMensaje) AndAlso codigoMensaje.Trim() = "1209" Then forzarSecuenciaUsada = True
            If forzarSecuenciaUsada Then secuenciautilizada = True

            Dim fechaRec As DateTime
            If Not DateTime.TryParseExact(fechaRecepcion, "M/d/yyyy h:mm:ss tt", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, fechaRec) Then
                fechaRec = DateTime.Now
            End If

            Helper.InsertarBitacoraDGII(cadenaConexion, emp_codigo, facNumero, estado, trackId, codigoMensaje, mensajeError, "", fechaRec, "", "",
                Helper.ResolverTipoBitacoraDgii(TipoeCF, isNotaCredito))

            Dim aceptado As Integer = If(Helper.EsEstadoAceptadoDgii(estado), 1, 0)
            Helper.AjustarPersistenciaPendiente(aceptado, secuenciautilizada, estado)

            Dim codigoseguridadRecons As String = ""
            Dim fechaFirmaRecons As DateTime = DateTime.Now

            Select Case TipoeCF
                Case "31", "32", "44", "45", "46"
                    If fac_forma.Trim().Length > 0 Then
                        Dim p As SqlParameter() = {
                            New SqlParameter("@emp_codigo", emp_codigo),
                            New SqlParameter("@suc_codigo", suc_codigo),
                            New SqlParameter("@fac_numero", facNumero),
                            New SqlParameter("@eNCF", encf),
                            New SqlParameter("@fac_forma", fac_forma),
                            New SqlParameter("@tfa_codigo", tfa_codigo),
                            New SqlParameter("@codigoseguridad", codigoseguridadRecons),
                            New SqlParameter("@fechafirma", fechaFirmaRecons),
                            New SqlParameter("@aceptado", aceptado),
                            New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                        }
                        Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGI", p)
                    End If
                Case "41", "47"
                    Dim p As SqlParameter() = {
                        New SqlParameter("@emp_codigo", emp_codigo),
                        New SqlParameter("@suc_codigo", suc_codigo),
                        New SqlParameter("@fac_numero", facNumero),
                        New SqlParameter("@eNCF", encf),
                        New SqlParameter("@sup_codigo", sup_codigo),
                        New SqlParameter("@codigoseguridad", codigoseguridadRecons),
                        New SqlParameter("@fechafirma", fechaFirmaRecons),
                        New SqlParameter("@aceptado", aceptado),
                        New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                    }
                    Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGICOMPRAS", p)
                Case "34"
                    If isNotaCredito Then
                        Dim p As SqlParameter() = {
                            New SqlParameter("@emp_codigo", emp_codigo),
                            New SqlParameter("@suc_codigo", suc_codigo),
                            New SqlParameter("@fac_numero", facNumero),
                            New SqlParameter("@eNCF", encf),
                            New SqlParameter("@codigoseguridad", codigoseguridadRecons),
                            New SqlParameter("@fechafirma", fechaFirmaRecons),
                            New SqlParameter("@aceptado", aceptado),
                            New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                        }
                        Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGINC", p)
                    Else
                        Dim p As SqlParameter() = {
                            New SqlParameter("@emp_codigo", emp_codigo),
                            New SqlParameter("@suc_codigo", suc_codigo),
                            New SqlParameter("@fac_numero", facNumero),
                            New SqlParameter("@eNCF", encf),
                            New SqlParameter("@codigoseguridad", codigoseguridadRecons),
                            New SqlParameter("@fechafirma", fechaFirmaRecons),
                            New SqlParameter("@aceptado", aceptado),
                            New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                        }
                        Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGIDEV", p)
                    End If
                Case "43"
                    Dim p As SqlParameter() = {
                        New SqlParameter("@emp_codigo", emp_codigo),
                        New SqlParameter("@suc_codigo", suc_codigo),
                        New SqlParameter("@des_numero", facNumero),
                        New SqlParameter("@eNCF", encf),
                        New SqlParameter("@codigoseguridad", codigoseguridadRecons),
                        New SqlParameter("@fechafirma", fechaFirmaRecons),
                        New SqlParameter("@aceptado", aceptado),
                        New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                    }
                    Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGIDESEMBOLSOS", p)
                Case "33"
                    If aceptado = 1 Then
                        Dim p As SqlParameter() = {
                            New SqlParameter("@emp_codigo", emp_codigo),
                            New SqlParameter("@suc_codigo", suc_codigo),
                            New SqlParameter("@fac_numero", facNumero),
                            New SqlParameter("@eNCF", encf),
                            New SqlParameter("@codigoseguridad", codigoseguridadRecons),
                            New SqlParameter("@fechafirma", fechaFirmaRecons)
                        }
                        Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGINOTADEBITO", p)
                    End If
                Case Else
                    Dim p As SqlParameter() = {
                        New SqlParameter("@emp_codigo", emp_codigo),
                        New SqlParameter("@suc_codigo", suc_codigo),
                        New SqlParameter("@fac_numero", facNumero),
                        New SqlParameter("@eNCF", encf),
                        New SqlParameter("@fac_forma", fac_forma),
                        New SqlParameter("@tfa_codigo", tfa_codigo),
                        New SqlParameter("@codigoseguridad", codigoseguridadRecons),
                        New SqlParameter("@fechafirma", fechaFirmaRecons),
                        New SqlParameter("@aceptado", aceptado),
                        New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                    }
                    Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGI", p)
            End Select
            Return Helper.ConstruirJsonRespuestaOperacion(parsedDgii, respuestaConsulta)
        Catch ex As Exception
            Helper.RegistrarLogCliente("ProcesarRespuestaAPISoloEstado EX: " & ex.ToString())
            Return Helper.ConstruirJsonRespuestaOperacion(
                New Helper.DgiiEstadoRespuesta With {.Estado = "Error", .Mensaje = ex.Message, .Aceptado = False},
                ex.ToString())
        End Try
    End Function

    ''' <summary>
    ''' Igual que <see cref="ProcesarRespuestaAPISoloEstado"/> pero persiste en Montos_Ticket o Factura_RestBar
    ''' (SP_UPDATEDATOSDGIPOS / SP_UPDATEDATOSDGIRESBAR). Necesario porque la reconsulta por trackId no usa tablas Facturas.
    ''' </summary>
    Private Shared Function ProcesarRespuestaAPISoloEstadoTicket(
            cadenaConexion As String,
            emp_codigo As String,
            suc_codigo As String,
            ticket As String,
            usu_codigo As String,
            caja As String,
            respuestaConsulta As String,
            esRestBar As Boolean) As String
        Dim parsedDgii = Helper.ParsearRespuestaDgii(respuestaConsulta)
        Dim estado As String = parsedDgii.Estado
        Dim trackId As String = If(parsedDgii.TrackId, "")
        Dim encf As String = If(parsedDgii.Encf, "")
        Dim fechaRecepcion As String = DateTime.Now.ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture)
        Dim codigoMensaje As String = If(parsedDgii.CodigoMensaje, "")
        Dim mensajeError As String = If(parsedDgii.Mensaje, "")
        Dim secuenciautilizada As Boolean = parsedDgii.SecuenciaUtilizada
        Try
            Try
                Dim root = JObject.Parse(respuestaConsulta)
                Dim payload As JToken = root
                If root.SelectToken("body") IsNot Nothing Then payload = root.SelectToken("body")
                If payload.SelectToken("fechaRecepcion") IsNot Nothing Then fechaRecepcion = CStr(payload.SelectToken("fechaRecepcion"))
            Catch
            End Try

            Dim forzarSecuenciaUsada As Boolean = False
            If ContainsSecuenciaYaUsada(mensajeError) Then forzarSecuenciaUsada = True
            If Not String.IsNullOrWhiteSpace(codigoMensaje) AndAlso codigoMensaje.Trim() = "1209" Then forzarSecuenciaUsada = True
            If forzarSecuenciaUsada Then secuenciautilizada = True

            Dim fechaRec As DateTime
            If Not DateTime.TryParseExact(fechaRecepcion, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture, DateTimeStyles.None, fechaRec) Then
                fechaRec = DateTime.Now
            End If

            Helper.InsertarBitacoraDGII(cadenaConexion, emp_codigo, ticket, estado, trackId, codigoMensaje, mensajeError, "", fechaRec, usu_codigo, caja,
                Helper.ResolverTipoBitacoraTicket(esRestBar))

            Dim aceptado As Integer = If(Helper.EsEstadoAceptadoDgii(estado), 1, 0)
            Helper.AjustarPersistenciaPendiente(aceptado, secuenciautilizada, estado)

            Dim spName As String = If(esRestBar, "SP_UPDATEDATOSDGIRESBAR", "SP_UPDATEDATOSDGIPOS")
            Dim codigoseguridadRecons As String = ""
            Dim fechaFirmaRecons As DateTime = DateTime.Now
            Dim p As SqlParameter() = {
                New SqlParameter("@emp_codigo", emp_codigo),
                New SqlParameter("@suc_codigo", suc_codigo),
                New SqlParameter("@ticket", ticket),
                New SqlParameter("@eNCF", encf),
                New SqlParameter("@usu_codigo", usu_codigo),
                New SqlParameter("@caja", caja),
                New SqlParameter("@codigoseguridad", codigoseguridadRecons),
                New SqlParameter("@fechafirma", fechaFirmaRecons),
                New SqlParameter("@aceptado", aceptado),
                New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
            }
            Helper.RegistrarLogCliente($"[Reconsulta ticket] dbo.{spName} emp={emp_codigo} suc={suc_codigo} ticket={ticket} usu={usu_codigo} caja={caja} aceptado={aceptado} esRestBar={esRestBar} estado={estado}")
            Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, spName, p)
            Return Helper.ConstruirJsonRespuestaOperacion(parsedDgii, respuestaConsulta)
        Catch ex As Exception
            Helper.RegistrarLogCliente("ProcesarRespuestaAPISoloEstadoTicket EX: " & ex.ToString())
            Return Helper.ConstruirJsonRespuestaOperacion(
                New Helper.DgiiEstadoRespuesta With {.Estado = "Error", .Mensaje = ex.Message, .Aceptado = False},
                ex.ToString())
        End Try
    End Function

    Private Shared Sub ProcesarRespuestaAPIPOS(
        RncCliente As String, TipoeCF As String, cadenaConexion As String,
    emp_codigo As String, suc_codigo As String, respuestaConsulta As String,
    ticket As String, signedXml As XmlDocument, codigoseguridad As String,
    usu_codigo As String, caja As String, sup_codigo As String, fechaFirma As DateTime)

        Try
            Dim spUpdateDatosTicket As String = If(isResBar, "SP_UPDATEDATOSDGIRESBAR", "SP_UPDATEDATOSDGIPOS")

            ' ---------- Parseo robusto ----------
            Dim estado As String = "Desconocido"
            Dim trackId As String = ""
            Dim encf As String = ""
            Dim fechaRecepcion As String = DateTime.Now.ToString("M/d/yyyy h:mm:ss tt")
            Dim codigoMensaje As String = ""
            Dim mensajeError As String = ""
            Dim secuenciautilizada As Boolean

            Try
                Dim root = Newtonsoft.Json.Linq.JObject.Parse(respuestaConsulta)
                Dim payload As Newtonsoft.Json.Linq.JToken = root
                If root.SelectToken("body") IsNot Nothing Then payload = root.SelectToken("body")

                If payload.SelectToken("estado") IsNot Nothing Then estado = CStr(payload.SelectToken("estado"))
                If payload.SelectToken("trackId") IsNot Nothing Then trackId = CStr(payload.SelectToken("trackId"))
                If payload.SelectToken("encf") IsNot Nothing Then encf = CStr(payload.SelectToken("encf"))
                If payload.SelectToken("fechaRecepcion") IsNot Nothing Then fechaRecepcion = CStr(payload.SelectToken("fechaRecepcion"))
                If payload.SelectToken("secuenciaUtilizada") IsNot Nothing Then
                    secuenciautilizada = CBool(payload.SelectToken("secuenciaUtilizada"))
                End If
                Dim jt = payload.SelectToken("mensajes")
                If jt IsNot Nothing Then
                    If jt.Type = JTokenType.Array Then
                        Dim partes As New List(Of String)
                        For Each m In jt
                            Dim cod = (If(m("codigo"), "")).ToString()
                            Dim val = (If(m("valor"), m.ToString())).ToString()
                            If Not String.IsNullOrWhiteSpace(cod) Then
                                partes.Add($"[{cod}] {val}")
                            Else
                                partes.Add(val)
                            End If
                        Next
                        mensajeError = String.Join(" | ", partes)
                    Else
                        mensajeError = jt.ToString()
                    End If
                End If

                Dim cm = payload.SelectToken("codigo")
                If cm IsNot Nothing Then codigoMensaje = cm.ToString()
            Catch
                ' Si no era JSON, guarda el texto crudo como estado/mensaje
                estado = "No procesado"
                mensajeError = respuestaConsulta
            End Try

            ' ---------- XML crudo para bitÃ¡cora ----------
            Dim xmlText As String = ""
            Try
                Using sw As New StringWriter()
                    Using xw As New XmlTextWriter(sw)
                        signedXml.WriteTo(xw)
                    End Using
                    xmlText = sw.ToString()
                End Using
            Catch
            End Try

            ' ====== DETECCIÃ“N DE SECUENCIA USADA ======
            Dim forzarSecuenciaUsada As Boolean = False
            If ContainsSecuenciaYaUsada(mensajeError) Then
                forzarSecuenciaUsada = True
            ElseIf Not String.IsNullOrWhiteSpace(codigoMensaje) AndAlso codigoMensaje.Trim() = "1209" Then
                forzarSecuenciaUsada = True
            End If
            If forzarSecuenciaUsada Then
                secuenciautilizada = True
            End If

            ' ---------- Fecha recepciÃ³n ----------
            Dim fechaRec As DateTime
            If Not DateTime.TryParseExact(
            fechaRecepcion,
            "M/d/yyyy h:mm:ss tt",
            Globalization.CultureInfo.InvariantCulture,
            Globalization.DateTimeStyles.None,
            fechaRec) Then
                fechaRec = DateTime.Now
            End If

            ' ---------- SIEMPRE: bitÃ¡cora ----------
            Helper.InsertarBitacoraDGII(
            cadenaConexion, emp_codigo, ticket, estado, trackId,
            codigoMensaje, mensajeError, xmlText, fechaRec, usu_codigo, caja,
            Helper.ResolverTipoBitacoraTicket(isResBar)
        )

            ' ---------- ActualizaciÃ³n de BD: Aceptado o Rechazado ----------
            Dim aceptado As Integer = If(Helper.EsEstadoAceptadoDgii(estado), 1, 0)
            Helper.AjustarPersistenciaPendiente(aceptado, secuenciautilizada, estado)

            Helper.RegistrarLogCliente("Entro al ProcesarRespuestaAPIPOS " + aceptado.ToString())

            ' Montos_Ticket / Factura_RestBar: actualizar siempre que haya respuesta de consulta DGII,
            ' para cualquier TipoeCF (31, 32, 33, 34, 41, 45, 46, 47, etc.). El SP filtra por bitÃ¡cora.
            Helper.RegistrarLogCliente($"[SP POS] INICIO dbo.{spUpdateDatosTicket} emp={emp_codigo} suc={suc_codigo} ticket={ticket} usu={usu_codigo} caja={caja} aceptado={aceptado} TipoeCF={TipoeCF} isResBar={isResBar} estadoDGII={estado}")
            Try
                Dim p As SqlParameter() = {
                    New SqlParameter("@emp_codigo", emp_codigo),
                    New SqlParameter("@suc_codigo", suc_codigo),
                    New SqlParameter("@ticket", ticket),
                    New SqlParameter("@eNCF", encf),
                    New SqlParameter("@usu_codigo", usu_codigo),
                    New SqlParameter("@caja", caja),
                    New SqlParameter("@codigoseguridad", codigoseguridad),
                    New SqlParameter("@fechafirma", fechaFirma),
                    New SqlParameter("@aceptado", aceptado),
                    New SqlParameter("@secuenciaUtilizada", secuenciautilizada)
                }
                Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, spUpdateDatosTicket, p)
                Helper.RegistrarLogCliente($"[SP POS] OK dbo.{spUpdateDatosTicket} ticket={ticket}")
            Catch ex As Exception
                Helper.RegistrarLogCliente($"[SP POS] ERROR dbo.{spUpdateDatosTicket} ticket={ticket}: {ex.Message}")
                LogDetallado("[SP POS] ERROR dbo." & spUpdateDatosTicket & " " & ex.ToString())
                Throw
            End Try

            ' ---------- EnvÃ­o al cliente SOLO si fue aceptado ----------
            If aceptado = 1 AndAlso RncCliente.Trim().Length > 0 Then
                Try
                    Dim resCli = EnviarFacturaClienteSiTieneUrlAsync(
                    RncCliente, GlobalVariables.token, signedXml, ticket
                ).GetAwaiter().GetResult()
                    Helper.RegistrarLogCliente($"Factura {ticket} enviada al cliente {RncCliente}. Resultado: {resCli}")
                Catch ex As Exception
                    Helper.RegistrarLogCliente($"Error enviando a cliente {RncCliente}: {ex.Message}")
                End Try
            End If

            ' ---------- Guardar monto + XML en BD (DGII_MontoEnviado / Xml / Fecha) ----------
            ' Misma idea que versiÃ³n administrativa + resumen RFCE: persistir siempre que haya respuesta con XML firmado,
            ' aunque DGII no marque aceptado. RestBar -> Factura_RestBar; POS -> Montos_Ticket.
            If isResBar Then
                Try
                    Dim monto As Decimal = ExtraerMontoTotalDesdeXml(signedXml)
                    Dim idTicket As Integer
                    Dim idCajero As Integer
                    Dim idCaja As Integer
                    Dim idEmp As Integer
                    Dim idSuc As Integer
                    If Not Integer.TryParse((ticket & "").Trim(), idTicket) OrElse
                       Not Integer.TryParse((usu_codigo & "").Trim(), idCajero) OrElse
                       Not Integer.TryParse((caja & "").Trim(), idCaja) OrElse
                       Not Integer.TryParse((emp_codigo & "").Trim(), idEmp) OrElse
                       Not Integer.TryParse((suc_codigo & "").Trim(), idSuc) Then
                        LogDetallado("UpdateFacturaRestBar omitido: parÃ¡metro numÃ©rico invÃ¡lido (ticket/usu_codigo/caja/emp/suc). ticket=" & ticket & " usu=" & usu_codigo & " caja=" & caja)
                    Else
                        UpdateFacturaRestBar(
                            cadenaConexion,
                            idEmp,
                            idSuc,
                            idTicket,
                            idCajero,
                            idCaja,
                            monto,
                            xmlText,
                            fechaRec)
                    End If
                Catch ex As Exception
                    LogDetallado("GuardarXmlYTotal (Factura_RestBar / ProcesarRespuestaAPIPOS) EX: " & ex.Message)
                End Try
            Else
                Try
                    Dim monto As Decimal = ExtraerMontoTotalDesdeXml(signedXml)
                    UpdateMontosTicket(
                        cadena:=cadenaConexion,
                        usuCodigo:=Convert.ToInt32(usu_codigo),
                        fecha:=fechaFirma,
                        caja:=Convert.ToInt32(caja),
                        ticket:=Convert.ToInt32(ticket),
                        monto:=monto,
                        xmlText:=xmlText,
                        fechaEnvio:=fechaRec)
                Catch ex As Exception
                    LogDetallado("GuardarXmlYTotal (Montos_Ticket / ProcesarRespuestaAPIPOS) EX: " & ex.Message)
                End Try
            End If

        Catch ex As Exception
            Helper.RegistrarLogCliente("ProcesarRespuestaAPI EX: " & ex.ToString())
            'Try
            '    Helper.InsertarBitacoraDGII(cadenaConexion, emp_codigo, facNumero, "EXCEPCION", "", "", ex.Message, "", DateTime.Now)
            'Catch
            'End Try
        End Try
    End Sub

    Private Shared Function NormalizeForCompare(input As String) As String
        If String.IsNullOrEmpty(input) Then Return ""
        ' Quita tildes y pasa a lower invariant
        Dim formD = input.Normalize(NormalizationForm.FormD)
        Dim sb As New StringBuilder(formD.Length)
        For Each ch In formD
            If CharUnicodeInfo.GetUnicodeCategory(ch) <> UnicodeCategory.NonSpacingMark Then
                sb.Append(ch)
            End If
        Next
        Return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant()
    End Function

    Private Shared Function ContainsSecuenciaYaUsada(msg As String) As Boolean
        Dim n = NormalizeForCompare(msg)
        ' Frase clave sin tildes
        If n.Contains("este numero de secuencia ya ha sido utilizado") Then Return True
        ' Tambien si viene el codigo 1209 en el texto
        If n.Contains("[1209]") OrElse n.Contains(" 1209") OrElse n.Contains("1209 ") Then Return True
        Return False
    End Function


    Private Shared Sub ProbarSPDirecto(cadena As String, emp As Integer, suc As Integer, tipo As Integer, forma As String, numero As Integer)
        Using cn As New SqlConnection(cadena)
            Using cmd As New SqlCommand("AsignarSecuenciaFacturasDGII", cn)
                cmd.CommandType = CommandType.StoredProcedure
                cmd.Parameters.Add("@empresa", SqlDbType.Int).Value = emp
                cmd.Parameters.Add("@sucursal", SqlDbType.Int).Value = suc
                cmd.Parameters.Add("@tipo", SqlDbType.Int).Value = tipo
                cmd.Parameters.Add("@forma", SqlDbType.VarChar, 1).Value = forma
                cmd.Parameters.Add("@numero", SqlDbType.Int).Value = numero
                Dim pOut = cmd.Parameters.Add("@eNCF", SqlDbType.NVarChar, 50)
                pOut.Direction = ParameterDirection.Output

                cn.Open()
                cmd.ExecuteNonQuery()
                Helper.RegistrarLogCliente("PRUEBA DIRECTA @eNCF=" & If(pOut.Value Is DBNull.Value, "(NULL)", CStr(pOut.Value)))
            End Using
        End Using
    End Sub


    Private Shared Function LlamarAsignarSecuenciaDGII(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    tipo As Integer,
    forma As String,
    numero As Integer
) As String

        ' Asegura forma de 1 caracter sin espacios
        Dim forma1 As String = If(forma, "").Trim()
        If forma1.Length > 1 Then forma1 = forma1.Substring(0, 1)
        ValidarMontoFacturaMayorQueCero(cadenaConexion, emp_codigo, suc_codigo, tipo, forma1, numero)

        Dim estadoEncfPrevio As Helper.DocumentoEncfEstado =
            Helper.ObtenerEstadoEncfFactura(cadenaConexion, emp_codigo, forma1, tipo, numero, suc_codigo)
        Dim encfPrevFac = EncFYaAsignado(estadoEncfPrevio)
        If encfPrevFac <> "" Then
            Helper.RegistrarLogCliente("[DGII] Factura ya tiene eNCF=" & encfPrevFac & "; se reutiliza.")
            Return encfPrevFac
        End If

        Try
            Using cn As New SqlClient.SqlConnection(cadenaConexion)
                Using cmd As New SqlClient.SqlCommand("dbo.AsignarSecuenciaFacturasDGII", cn)
                    cmd.CommandType = CommandType.StoredProcedure
                    cmd.CommandTimeout = SqlCommandTimeoutSegundos

                    ' === ENTRADAS (ajusta tipos/longitudes a los del SP) ===
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@empresa", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = emp_codigo
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@sucursal", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = suc_codigo
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@tipo", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = tipo
                })
                    ' Si en el SP es CHAR(1), usa Char y Size = 1
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@forma", SqlDbType.Char, 1) With {
                    .Direction = ParameterDirection.Input, .Value = forma1
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@numero", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = numero
                })

                    ' === OUTPUT ===
                    Dim pOut As New SqlClient.SqlParameter("@eNCF", SqlDbType.VarChar, 50)
                    pOut.Direction = ParameterDirection.Output
                    pOut.Value = DBNull.Value
                    cmd.Parameters.Add(pOut)

                    ' === RETURN VALUE (opcional pero Ãºtil para diagnÃ³sticos) ===
                    Dim pRet As New SqlClient.SqlParameter("@RETURN_VALUE", SqlDbType.Int)
                    pRet.Direction = ParameterDirection.ReturnValue
                    cmd.Parameters.Add(pRet)

                    ' LOG de entrada
                    Helper.RegistrarLogCliente($"[SP] @empresa={emp_codigo} @sucursal={suc_codigo} @tipo={tipo} @forma='{forma1}' @numero={numero}")

                    cn.Open()
                    cmd.ExecuteNonQuery()

                    Dim encfGenerado As String = If(cmd.Parameters("@eNCF").Value Is DBNull.Value, "", CStr(cmd.Parameters("@eNCF").Value))
                    Dim ret As Integer = If(cmd.Parameters("@RETURN_VALUE").Value Is DBNull.Value, -999, CInt(cmd.Parameters("@RETURN_VALUE").Value))

                    Helper.RegistrarLogCliente($"[SP OUT] @eNCF='{encfGenerado}'  RETURN_VALUE={ret}")

                    If String.IsNullOrWhiteSpace(encfGenerado) Then
                        Throw New Exception($"El SP no devolviÃ³ eNCF. RETURN_VALUE={ret}. Revisa que los parÃ¡metros coincidan y que la factura exista.")
                    End If

                    RegistrarBitacoraEncfTrasAsignacion(
                        cadenaConexion,
                        Helper.EncfDocumentoRef.Factura(emp_codigo, suc_codigo, forma1, tipo, numero),
                        encfGenerado,
                        estadoEncfPrevio,
                        "DGII_ASIGNACION")

                    Return encfGenerado
                End Using
            End Using

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error en LlamarAsignarSecuenciaDGII: " & ex.Message)
            Return ""
        End Try
    End Function



    Private Shared Function LlamarAsignarSecuenciaDGIIPOS(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    usu_codigo As String,
    caja As String,
    numero As Integer
) As String

        ' Asegura forma de 1 caracter sin espacios
        Dim usuId As Integer = 0
        Dim cajaId As Integer = 0
        Integer.TryParse(If(usu_codigo, "").Trim(), usuId)
        Integer.TryParse(If(caja, "").Trim(), cajaId)
        ValidarMontoPosMayorQueCero(cadenaConexion, emp_codigo, suc_codigo, usuId, cajaId, numero)
        Dim docPos = Helper.EncfDocumentoRef.PosTicket(emp_codigo, suc_codigo, usuId, cajaId, numero)
        Dim estadoEncfPrevioPos = Helper.ObtenerEstadoEncf(cadenaConexion, docPos)
        Dim encfPrevPos = EncFYaAsignado(estadoEncfPrevioPos)
        If encfPrevPos <> "" Then
            Helper.RegistrarLogCliente("[DGII] POS ya tiene eNCF=" & encfPrevPos & "; se reutiliza.")
            Return encfPrevPos
        End If

        Try
            Using cn As New SqlClient.SqlConnection(cadenaConexion)
                Using cmd As New SqlClient.SqlCommand("dbo.AsignarSecuenciaFacturasDGIIPOS", cn)
                    cmd.CommandType = CommandType.StoredProcedure
                    cmd.CommandTimeout = SqlCommandTimeoutSegundos

                    ' === ENTRADAS (ajusta tipos/longitudes a los del SP) ===
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@empresa", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = emp_codigo
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@sucursal", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = suc_codigo
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@usu_codigo", SqlDbType.VarChar, 50) With {
                    .Direction = ParameterDirection.Input, .Value = usu_codigo
                })
                    ' Si en el SP es CHAR(1), usa Char y Size = 1
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@caja", SqlDbType.VarChar, 50) With {
                    .Direction = ParameterDirection.Input, .Value = caja
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@numero", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = numero
                })

                    ' === OUTPUT ===
                    Dim pOut As New SqlClient.SqlParameter("@eNCF", SqlDbType.VarChar, 50)
                    pOut.Direction = ParameterDirection.Output
                    pOut.Value = DBNull.Value
                    cmd.Parameters.Add(pOut)

                    ' === RETURN VALUE (opcional pero Ãºtil para diagnÃ³sticos) ===
                    Dim pRet As New SqlClient.SqlParameter("@RETURN_VALUE", SqlDbType.Int)
                    pRet.Direction = ParameterDirection.ReturnValue
                    cmd.Parameters.Add(pRet)

                    ' LOG de entrada
                    Helper.RegistrarLogCliente($"[SP POS] @empresa={emp_codigo} @sucursal={suc_codigo} @usu_codigo={usu_codigo} @ticket={numero} @caja={caja}")

                    cn.Open()
                    cmd.ExecuteNonQuery()

                    Dim encfGenerado As String = If(cmd.Parameters("@eNCF").Value Is DBNull.Value, "", CStr(cmd.Parameters("@eNCF").Value))
                    Dim ret As Integer = If(cmd.Parameters("@RETURN_VALUE").Value Is DBNull.Value, -999, CInt(cmd.Parameters("@RETURN_VALUE").Value))

                    Helper.RegistrarLogCliente($"[SP OUT] @eNCF='{encfGenerado}'  RETURN_VALUE={ret}")

                    If String.IsNullOrWhiteSpace(encfGenerado) Then
                        Throw New Exception($"El SP no devolviÃ³ eNCF. RETURN_VALUE={ret}. Revisa que los parÃ¡metros coincidan y que la factura exista.")
                    End If

                    RegistrarBitacoraEncfTrasAsignacion(cadenaConexion, docPos, encfGenerado, estadoEncfPrevioPos, "DGII_ASIGNACION_POS")

                    Return encfGenerado
                End Using
            End Using

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error en LlamarAsignarSecuenciaDGII: " & ex.Message)
            Return ""
        End Try
    End Function

    ''' <summary>Misma firma que <see cref="LlamarAsignarSecuenciaDGIIPOS"/>; ejecuta <c>dbo.AsignarSecuenciaFacturasDGIIPOSRESBAR</c>.</summary>
    Private Shared Function LlamarAsignarSecuenciaDGIIPOSResBar(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    usu_codigo As String,
    caja As String,
    numero As Integer
) As String

        Dim usuIdRb As Integer = 0
        Dim cajaIdRb As Integer = 0
        Integer.TryParse(If(usu_codigo, "").Trim(), usuIdRb)
        Integer.TryParse(If(caja, "").Trim(), cajaIdRb)
        ValidarMontoResBarMayorQueCero(cadenaConexion, emp_codigo, suc_codigo, usuIdRb, cajaIdRb, numero)
        Dim docRb = Helper.EncfDocumentoRef.ResBar(emp_codigo, suc_codigo, usuIdRb, cajaIdRb, numero)
        Dim estadoEncfPrevioRb = Helper.ObtenerEstadoEncf(cadenaConexion, docRb)
        Dim encfPrevRb = EncFYaAsignado(estadoEncfPrevioRb)
        If encfPrevRb <> "" Then
            Helper.RegistrarLogCliente("[DGII] ResBar ya tiene eNCF=" & encfPrevRb & "; se reutiliza.")
            Return encfPrevRb
        End If

        Try
            Using cn As New SqlClient.SqlConnection(cadenaConexion)
                Using cmd As New SqlClient.SqlCommand("dbo.AsignarSecuenciaFacturasDGIIPOSRESBAR", cn)
                    cmd.CommandType = CommandType.StoredProcedure
                    cmd.CommandTimeout = SqlCommandTimeoutSegundos

                    cmd.Parameters.Add(New SqlClient.SqlParameter("@empresa", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = emp_codigo
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@sucursal", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = suc_codigo
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@usu_codigo", SqlDbType.VarChar, 50) With {
                    .Direction = ParameterDirection.Input, .Value = usu_codigo
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@caja", SqlDbType.VarChar, 50) With {
                    .Direction = ParameterDirection.Input, .Value = caja
                })
                    cmd.Parameters.Add(New SqlClient.SqlParameter("@numero", SqlDbType.Int) With {
                    .Direction = ParameterDirection.Input, .Value = numero
                })

                    Dim pOut As New SqlClient.SqlParameter("@eNCF", SqlDbType.VarChar, 50)
                    pOut.Direction = ParameterDirection.Output
                    pOut.Value = DBNull.Value
                    cmd.Parameters.Add(pOut)

                    Dim pRet As New SqlClient.SqlParameter("@RETURN_VALUE", SqlDbType.Int)
                    pRet.Direction = ParameterDirection.ReturnValue
                    cmd.Parameters.Add(pRet)

                    Helper.RegistrarLogCliente($"[SP RESBAR] @empresa={emp_codigo} @sucursal={suc_codigo} @usu_codigo={usu_codigo} @FacturaID={numero} @caja={caja}")

                    cn.Open()
                    cmd.ExecuteNonQuery()

                    Dim encfGenerado As String = If(cmd.Parameters("@eNCF").Value Is DBNull.Value, "", CStr(cmd.Parameters("@eNCF").Value))
                    Dim ret As Integer = If(cmd.Parameters("@RETURN_VALUE").Value Is DBNull.Value, -999, CInt(cmd.Parameters("@RETURN_VALUE").Value))

                    Helper.RegistrarLogCliente($"[SP RESBAR OUT] @eNCF='{encfGenerado}'  RETURN_VALUE={ret}")

                    If String.IsNullOrWhiteSpace(encfGenerado) Then
                        Throw New Exception($"El SP RESBAR no devolviÃ³ eNCF. RETURN_VALUE={ret}. Revisa parÃ¡metros y que el documento exista en tablas RESBAR.")
                    End If

                    RegistrarBitacoraEncfTrasAsignacion(cadenaConexion, docRb, encfGenerado, estadoEncfPrevioRb, "DGII_ASIGNACION_RESBAR")

                    Return encfGenerado
                End Using
            End Using

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error en LlamarAsignarSecuenciaDGIIPOSResBar: " & ex.Message)
            Return ""
        End Try
    End Function

    Private Shared Sub ValidarMontoFacturaMayorQueCero(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    tipo As Integer,
    forma As String,
    numero As Integer)
        Const sql As String =
"SELECT TOP (1) fac_total
FROM dbo.Facturas
WHERE emp_codigo=@emp
  AND suc_codigo=@suc
  AND tfa_codigo=@tipo
  AND fac_forma=@forma
  AND fac_numero=@num;"
        Dim monto = ObtenerMontoDocumento(cadenaConexion, sql,
            New SqlParameter("@emp", SqlDbType.Int) With {.Value = emp_codigo},
            New SqlParameter("@suc", SqlDbType.Int) With {.Value = suc_codigo},
            New SqlParameter("@tipo", SqlDbType.Int) With {.Value = tipo},
            New SqlParameter("@forma", SqlDbType.Char, 1) With {.Value = forma},
            New SqlParameter("@num", SqlDbType.Int) With {.Value = numero})

        If Not monto.HasValue Then
            Throw New ApplicationException($"No se encontró la factura {numero} para validar el monto antes de asignar eNCF.")
        End If
        If monto.Value <= 0D Then
            Throw New ApplicationException($"No se puede asignar eNCF a la factura {numero} porque su monto total es {monto.Value.ToString("F2", CultureInfo.InvariantCulture)}.")
        End If
    End Sub

    Private Shared Sub ValidarMontoPosMayorQueCero(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    usu_codigo As Integer,
    caja As Integer,
    numero As Integer)
        Const sql As String =
"SELECT TOP (1) total
FROM dbo.Montos_Ticket
WHERE emp_codigo=@emp
  AND suc_codigo=@suc
  AND usu_codigo=@usu
  AND caja=@caja
  AND ticket=@num;"
        Dim monto = ObtenerMontoDocumento(cadenaConexion, sql,
            New SqlParameter("@emp", SqlDbType.Int) With {.Value = emp_codigo},
            New SqlParameter("@suc", SqlDbType.Int) With {.Value = suc_codigo},
            New SqlParameter("@usu", SqlDbType.Int) With {.Value = usu_codigo},
            New SqlParameter("@caja", SqlDbType.Int) With {.Value = caja},
            New SqlParameter("@num", SqlDbType.Int) With {.Value = numero})

        If Not monto.HasValue Then
            Throw New ApplicationException($"No se encontró el ticket POS {numero} para validar el monto antes de asignar eNCF.")
        End If
        If monto.Value <= 0D Then
            Throw New ApplicationException($"No se puede asignar eNCF al ticket POS {numero} porque su monto total es {monto.Value.ToString("F2", CultureInfo.InvariantCulture)}.")
        End If
    End Sub

    Private Shared Sub ValidarMontoResBarMayorQueCero(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    usu_codigo As Integer,
    caja As Integer,
    numero As Integer)
        Const sql As String =
"SELECT TOP (1) Total
FROM dbo.Factura_RestBar
WHERE EMP_CODIGO=@emp
  AND SUC_CODIGO=@suc
  AND CajeroID=@usu
  AND CajaID=@caja
  AND FacturaID=@num;"
        Dim monto = ObtenerMontoDocumento(cadenaConexion, sql,
            New SqlParameter("@emp", SqlDbType.Int) With {.Value = emp_codigo},
            New SqlParameter("@suc", SqlDbType.Int) With {.Value = suc_codigo},
            New SqlParameter("@usu", SqlDbType.Int) With {.Value = usu_codigo},
            New SqlParameter("@caja", SqlDbType.Int) With {.Value = caja},
            New SqlParameter("@num", SqlDbType.Int) With {.Value = numero})

        If Not monto.HasValue Then
            Throw New ApplicationException($"No se encontró la factura RestBar {numero} para validar el monto antes de asignar eNCF.")
        End If
        If monto.Value <= 0D Then
            Throw New ApplicationException($"No se puede asignar eNCF a la factura RestBar {numero} porque su monto total es {monto.Value.ToString("F2", CultureInfo.InvariantCulture)}.")
        End If
    End Sub

    Private Shared Function ObtenerMontoDocumento(
    cadenaConexion As String,
    sql As String,
    ParamArray parametros() As SqlParameter) As Decimal?
        Using cn As New SqlConnection(cadenaConexion)
            Using cmd As New SqlCommand(sql, cn)
                cmd.CommandTimeout = SqlCommandTimeoutSegundos
                If parametros IsNot Nothing AndAlso parametros.Length > 0 Then
                    cmd.Parameters.AddRange(parametros)
                End If

                cn.Open()
                Dim value = cmd.ExecuteScalar()
                Return SafeDec(value)
            End Using
        End Using
    End Function


    Private Shared Function LlamarAsignarSecuenciaComprasDGII(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    sup_codigo As Integer,
    numero As String,
    Optional tipoeCF As Integer = 0
) As String
        Dim docCompra = Helper.EncfDocumentoRef.Compra(emp_codigo, suc_codigo, sup_codigo, numero)
        Dim estadoCompra = Helper.ObtenerEstadoEncf(cadenaConexion, docCompra)
        Dim encfPrevCompra = EncFYaAsignado(estadoCompra)
        If encfPrevCompra <> "" Then
            Helper.RegistrarLogCliente("[DGII] Compra ya tiene eNCF=" & encfPrevCompra & "; se reutiliza.")
            Return encfPrevCompra
        End If
        Try
            Dim p As New List(Of SqlParameter) From {
                New SqlParameter("@empresa", emp_codigo),
                New SqlParameter("@proveedor", sup_codigo),
                New SqlParameter("@numero", numero),
                New SqlParameter("@eNCF", SqlDbType.VarChar, 50) With {.Direction = ParameterDirection.Output},
                New SqlParameter("@sucursal", suc_codigo)
            }
            If tipoeCF > 0 Then
                p.Add(New SqlParameter("@tipoECF", tipoeCF))
            Else
                p.Add(New SqlParameter("@tipoECF", DBNull.Value))
            End If

            Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "AsignarSecuenciaFacturasProvDGII", p.ToArray())

            Dim encfGenerado As String = If(p(3).Value, "").ToString()

            If String.IsNullOrWhiteSpace(encfGenerado) Then
                Dim detalleTipo = If(tipoeCF > 0, " (tipo e-CF " & tipoeCF & ")", "")
                Throw New Exception("El SP no devolvió un eNCF válido o no encontró la compra" & detalleTipo & ". Verifique ProvFacturas, SecuenciaDGII y docs/SP_AsignarSecuenciaFacturasProvDGII.sql.")
            End If

            RegistrarBitacoraEncfTrasAsignacion(cadenaConexion, docCompra, encfGenerado, estadoCompra, "DGII_ASIGNACION_COMPRA")
            Return encfGenerado

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error en AsignarSecuenciaFacturasProvDGII: " & ex.Message)
            Return ""
        End Try
    End Function

    ''' <summary>Determina si la compra debe enviarse como e-CF 47 (pago al exterior).</summary>
    Private Shared Function EsCompraPagoExterior(
        cadenaConexion As String,
        emp_codigo As String,
        sup_codigo As String,
        fac_numero As String,
        Optional tipoEcfParam As String = "") As Boolean

        Dim tipoParam = If(tipoEcfParam, "").Trim()
        If tipoParam = "47" Then Return True
        If tipoParam = "41" Then Return False

        Try
            Using cn As New SqlConnection(cadenaConexion)
                Using cmd As New SqlCommand(
                    "SELECT TOP (1) TipoeCF, prov_rnc FROM dbo.vwComprasXML WHERE emp_codigo=@emp AND sup_codigo=@sup AND fac_numero=@fac",
                    cn)
                    cmd.Parameters.Add("@emp", SqlDbType.Int).Value = Convert.ToInt32(emp_codigo)
                    cmd.Parameters.Add("@sup", SqlDbType.Int).Value = Convert.ToInt32(sup_codigo)
                    cmd.Parameters.Add("@fac", SqlDbType.VarChar, 15).Value = If(fac_numero, "").Trim()
                    cn.Open()
                    Using r = cmd.ExecuteReader()
                        If Not r.Read() Then Return False

                        Dim tipoVista = Helper.SafeStr(r, "TipoeCF")
                        If tipoVista = "47" Then Return True
                        If tipoVista = "41" Then Return False

                        Dim rncProveedor = Helper.SafeStr(r, "prov_rnc")
                        Dim soloDigitos As New StringBuilder()
                        For Each c As Char In rncProveedor
                            If Char.IsDigit(c) Then soloDigitos.Append(c)
                        Next
                        Dim rncDigitos = soloDigitos.ToString()
                        If rncDigitos = "" Then Return True
                        If rncDigitos.Length = 9 OrElse rncDigitos.Length = 11 Then Return False
                        Return True
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Helper.RegistrarLogCliente("EsCompraPagoExterior EX: " & ex.Message)
            Return False
        End Try
    End Function

    Private Shared Function LlamarAsignarSecuenciaDesembolsosDGII(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    sup_codigo As Integer,
    numero As String
) As String
        Dim numDes As Integer
        Integer.TryParse(If(numero, "").Trim(), numDes)
        Dim docDes = Helper.EncfDocumentoRef.Desembolso(emp_codigo, suc_codigo, numDes)
        Dim estadoDes = Helper.ObtenerEstadoEncf(cadenaConexion, docDes)
        Dim encfPrevDes = EncFYaAsignado(estadoDes)
        If encfPrevDes <> "" Then
            Helper.RegistrarLogCliente("[DGII] Desembolso ya tiene eNCF=" & encfPrevDes & "; se reutiliza.")
            Return encfPrevDes
        End If
        Try
            Dim p As SqlParameter() = {
            New SqlParameter("@empresa", emp_codigo),
            New SqlParameter("@sucursal", suc_codigo),
            New SqlParameter("@numero", numero),
            New SqlParameter("@eNCF", SqlDbType.VarChar, 50) With {.Direction = ParameterDirection.Output}
        }

            Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "AsignarSecuenciaFacturasDesembolsosDGII", p)

            Dim encfGenerado As String = If(p(3).Value, "").ToString()

            If String.IsNullOrWhiteSpace(encfGenerado) Then
                Throw New Exception("El SP no devolviÃ³ un eNCF vÃ¡lido o no encontrÃ³ la factura.")
            End If

            RegistrarBitacoraEncfTrasAsignacion(cadenaConexion, docDes, encfGenerado, estadoDes, "DGII_ASIGNACION_DESEMBOLSO")
            Return encfGenerado

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error en AsignarSecuenciaFacturasDesembolsosDGII: " & ex.Message)
            Return ""
        End Try
    End Function



    Private Shared Function LlamarAsignarSecuenciaDevolucionDGII(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    numero As String
) As String
        Dim numDev As Integer
        Integer.TryParse(If(numero, "").Trim(), numDev)
        Dim docDev = Helper.EncfDocumentoRef.Devolucion(emp_codigo, suc_codigo, numDev)
        Dim estadoDev = Helper.ObtenerEstadoEncf(cadenaConexion, docDev)
        Try
            ' 1) Si ya tiene eNCF en BD, reutilizarlo
            Dim encfExistente = ObtenerEncfDevolucionExistente(cadenaConexion, emp_codigo, suc_codigo, numDev)
            If Not String.IsNullOrWhiteSpace(encfExistente) Then
                Helper.RegistrarLogCliente("[LUGANIS] Devolución ya tiene eNCF=" & encfExistente)
                Return encfExistente.Trim()
            End If

            Dim p As SqlParameter() = {
                New SqlParameter("@empresa", SqlDbType.Int) With {.Value = emp_codigo},
                New SqlParameter("@sucursal", SqlDbType.Int) With {.Value = suc_codigo},
                New SqlParameter("@numero", SqlDbType.Int) With {.Value = numDev},
                New SqlParameter("@eNCF", SqlDbType.VarChar, 50) With {
                    .Direction = ParameterDirection.Output,
                    .Value = DBNull.Value
                }
            }

            Try
                Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "AsignarSecuenciaDevolucionDGII", p)
            Catch exSp As Exception
                Helper.RegistrarLogCliente("AsignarSecuenciaDevolucionDGII falló: " & exSp.Message & " — se intenta asignación directa tipo 34.")
            End Try

            Dim encfGeneradoObj As Object = p(3).Value
            Dim encfGenerado As String = If(encfGeneradoObj Is Nothing OrElse encfGeneradoObj Is DBNull.Value, "", encfGeneradoObj.ToString().Trim())

            If String.IsNullOrWhiteSpace(encfGenerado) Then
                encfGenerado = AsignarSecuenciaDevolucionDirecta(cadenaConexion, emp_codigo, suc_codigo, numDev)
            End If

            If String.IsNullOrWhiteSpace(encfGenerado) Then
                Throw New Exception("El SP no devolvió un eNCF válido o no encontró la devolución " & numero & ".")
            End If

            RegistrarBitacoraEncfTrasAsignacion(cadenaConexion, docDev, encfGenerado, estadoDev, "DGII_ASIGNACION_DEVOLUCION")
            Return encfGenerado

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error en LlamarAsignarSecuenciaDevolucionDGII: " & ex.Message)
            Return ""
        End Try
    End Function

    Private Shared Function ObtenerEncfDevolucionExistente(
        cadenaConexion As String,
        emp_codigo As Integer,
        suc_codigo As Integer,
        numero As Integer
    ) As String
        Const sql As String =
"SELECT TOP (1) NULLIF(LTRIM(RTRIM(eNCF)), '') AS eNCF
 FROM dbo.Devolucion WITH (NOLOCK)
 WHERE emp_codigo = @emp AND suc_codigo = @suc AND DEV_NUMERO = @num
   AND ISNULL(dev_status,'') <> 'ANU';"
        Try
            Using conn As New SqlConnection(cadenaConexion)
                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.Add("@emp", SqlDbType.Int).Value = emp_codigo
                    cmd.Parameters.Add("@suc", SqlDbType.Int).Value = suc_codigo
                    cmd.Parameters.Add("@num", SqlDbType.Int).Value = numero
                    conn.Open()
                    Dim o = cmd.ExecuteScalar()
                    If o Is Nothing OrElse o Is DBNull.Value Then Return ""
                    Return o.ToString().Trim()
                End Using
            End Using
        Catch ex As Exception
            Helper.RegistrarLogCliente("ObtenerEncfDevolucionExistente: " & ex.Message)
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' Asigna eNCF tipo 34 a Devolucion vía fn_obtenerSecuenciaDGI (fallback si el SP falla o no existe).
    ''' </summary>
    Private Shared Function AsignarSecuenciaDevolucionDirecta(
        cadenaConexion As String,
        emp_codigo As Integer,
        suc_codigo As Integer,
        numero As Integer
    ) As String
        Const sql As String =
"SET NOCOUNT ON;
DECLARE @nuevo VARCHAR(50);
SET @nuevo = dbo.fn_obtenerSecuenciaDGI(@emp, 34);
IF @nuevo IS NULL OR LTRIM(RTRIM(@nuevo)) = ''
BEGIN
  SELECT CAST(NULL AS VARCHAR(50));
  RETURN;
END;
UPDATE dbo.Devolucion
SET eNCF = @nuevo
WHERE emp_codigo = @emp AND suc_codigo = @suc AND DEV_NUMERO = @num
  AND ISNULL(dev_status,'') <> 'ANU'
  AND (eNCF IS NULL OR LTRIM(RTRIM(eNCF)) = '');
IF @@ROWCOUNT = 0
BEGIN
  SELECT TOP (1) NULLIF(LTRIM(RTRIM(eNCF)),'') FROM dbo.Devolucion
  WHERE emp_codigo = @emp AND suc_codigo = @suc AND DEV_NUMERO = @num AND ISNULL(dev_status,'') <> 'ANU';
  RETURN;
END;
UPDATE dbo.SecuenciaDGII
SET Ultima_secuencia_DGII = Ultima_secuencia_DGII + 1
WHERE emp_codigo = @emp AND Tipo = 34;
SELECT @nuevo;"
        Try
            Using conn As New SqlConnection(cadenaConexion)
                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.Add("@emp", SqlDbType.Int).Value = emp_codigo
                    cmd.Parameters.Add("@suc", SqlDbType.Int).Value = suc_codigo
                    cmd.Parameters.Add("@num", SqlDbType.Int).Value = numero
                    conn.Open()
                    Dim o = cmd.ExecuteScalar()
                    Dim encf = If(o Is Nothing OrElse o Is DBNull.Value, "", o.ToString().Trim())
                    Helper.RegistrarLogCliente("[LUGANIS] AsignarSecuenciaDevolucionDirecta => " & If(encf, "(vacío)"))
                    Return encf
                End Using
            End Using
        Catch ex As Exception
            Helper.RegistrarLogCliente("AsignarSecuenciaDevolucionDirecta ERROR: " & ex.Message)
            Return ""
        End Try
    End Function

    Private Shared Function LlamarAsignarSecuenciaNotaCreditoDGII(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    numero As String
) As String
        Dim numNc As Integer
        Integer.TryParse(If(numero, "").Trim(), numNc)
        Dim docNc = Helper.EncfDocumentoRef.NotaCredito(emp_codigo, suc_codigo, numNc)
        Dim estadoNc = Helper.ObtenerEstadoEncf(cadenaConexion, docNc)
        Dim encfPrevNc = EncFYaAsignado(estadoNc)
        If encfPrevNc <> "" Then
            Helper.RegistrarLogCliente("[DGII] Nota de crédito ya tiene eNCF=" & encfPrevNc & "; se reutiliza.")
            Return encfPrevNc
        End If
        Try
            Dim p As SqlParameter() = {
            New SqlParameter("@empresa", emp_codigo),
            New SqlParameter("@sucursal", suc_codigo),
            New SqlParameter("@numero", numero),
            New SqlParameter("@eNCF", SqlDbType.VarChar, 50) With {
                .Direction = ParameterDirection.Output,
                .Value = DBNull.Value
            }
        }

            Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "AsignarSecuenciaNotaCreditoDGII", p)

            Dim encfGeneradoObj As Object = p(3).Value
            Dim encfGenerado As String = If(encfGeneradoObj Is Nothing OrElse encfGeneradoObj Is DBNull.Value, "", encfGeneradoObj.ToString().Trim())

            If String.IsNullOrWhiteSpace(encfGenerado) Then
                Throw New Exception("El SP no devolviÃ³ un eNCF vÃ¡lido o no encontrÃ³ la factura.")
            End If

            RegistrarBitacoraEncfTrasAsignacion(cadenaConexion, docNc, encfGenerado, estadoNc, "DGII_ASIGNACION_NOTACREDITO")
            Return encfGenerado

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error en AsignarSecuenciaNotaCreditoDGII: " & ex.Message)
            Return ""
        End Try
    End Function

    Private Shared Function LlamarAsignarSecuenciaNotaDebitoDGII(
    cadenaConexion As String,
    emp_codigo As Integer,
    suc_codigo As Integer,
    numero As String
) As String
        Dim numNd As Integer
        Integer.TryParse(If(numero, "").Trim(), numNd)
        Dim docNd = Helper.EncfDocumentoRef.NotaDebito(emp_codigo, suc_codigo, numNd)
        Dim estadoNd = Helper.ObtenerEstadoEncf(cadenaConexion, docNd)
        Dim encfPrevNd = EncFYaAsignado(estadoNd)
        If encfPrevNd <> "" Then
            Helper.RegistrarLogCliente("[DGII] Nota de débito ya tiene eNCF=" & encfPrevNd & "; se reutiliza.")
            Return encfPrevNd
        End If
        Try
            Dim p As SqlParameter() = {
                New SqlParameter("@empresa", emp_codigo),
                New SqlParameter("@sucursal", suc_codigo),
                New SqlParameter("@numero", numero),
                New SqlParameter("@eNCF", SqlDbType.VarChar, 50) With {
                    .Direction = ParameterDirection.Output,
                    .Value = DBNull.Value
                }
            }

            Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "AsignarSecuenciaNotaDebitoDGII", p)

            Dim encfGeneradoObj As Object = p(3).Value
            Dim encfGenerado As String = If(encfGeneradoObj Is Nothing OrElse encfGeneradoObj Is DBNull.Value, "", encfGeneradoObj.ToString().Trim())

            If String.IsNullOrWhiteSpace(encfGenerado) Then
                Throw New Exception("El SP no devolvió un eNCF válido o no encontró la nota de débito.")
            End If

            RegistrarBitacoraEncfTrasAsignacion(cadenaConexion, docNd, encfGenerado, estadoNd, "DGII_ASIGNACION_NOTADEBITO")
            Return encfGenerado

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error en AsignarSecuenciaNotaDebitoDGII: " & ex.Message)
            Return ""
        End Try
    End Function



    ' Puedes poner esto dentro de una clase (Public Class FacturaService) o en un Module.
    Public Class EstadoFacturaResponse
        Public Property Status As HttpStatusCode
        Public Property Body As String
    End Class

    Private Shared Function ConsultarEstadoFacturaHttp(trackId As String, token As String) As EstadoFacturaResponse
        Dim urlConsulta As String = GlobalVariables.url_base & "consultaresultado/api/consultas/estado?trackid=" & trackId
        Dim request As HttpWebRequest = CType(WebRequest.Create(urlConsulta), HttpWebRequest)
        request.Method = "GET"
        request.Headers.Add("Authorization", "Bearer " & token)

        Dim result As New EstadoFacturaResponse()

        Try
            Using response As HttpWebResponse = CType(request.GetResponse(), HttpWebResponse)
                Using reader As New StreamReader(response.GetResponseStream())
                    result.Status = response.StatusCode
                    result.Body = reader.ReadToEnd()
                    Return result
                End Using
            End Using
        Catch ex As WebException
            Dim resp = TryCast(ex.Response, HttpWebResponse)
            If resp IsNot Nothing Then
                Using reader As New StreamReader(resp.GetResponseStream())
                    result.Status = resp.StatusCode
                    result.Body = reader.ReadToEnd()
                    Return result
                End Using
            End If
            Throw New Exception("Error al consultar el estado de la factura: " & ex.Message)
        End Try
    End Function

    Private Shared Function ExtraerEstado(json As String) As String
        Dim s As String = If(json, "").ToUpperInvariant()
        If s.Contains("EN_PROCESO") Then Return "EN_PROCESO"
        If s.Contains("ACEPTADO") Then Return "ACEPTADO"
        If s.Contains("APROBADO") Then Return "ACEPTADO" ' por si viene asÃ­
        If s.Contains("RECHAZADO") Then Return "RECHAZADO"
        If s.Contains("RECHAZADA") Then Return "RECHAZADO"
        Return ""
    End Function

    Private Shared Function EsEstadoFinal(estado As String) As Boolean
        estado = If(estado, "").ToUpperInvariant()
        Return (estado = "ACEPTADO" OrElse estado = "RECHAZADO")
    End Function

    Public Shared Function ConsultarEstadoConReintentos(trackId As String,
                                                    token As String,
                                                    Optional maxIntentos As Integer = 8,
                                                    Optional esperaInicialMs As Integer = 1500,
                                                    Optional esperaMaxMs As Integer = 15000) As String
        Dim intento As Integer = 0
        Dim espera As Integer = esperaInicialMs

        Do
            Dim resp As EstadoFacturaResponse = ConsultarEstadoFacturaHttp(trackId, token)

            ' 202 = Accepted -> normalmente "en proceso"
            Dim estado As String = If(resp.Status = HttpStatusCode.Accepted, "EN_PROCESO", ExtraerEstado(resp.Body))

            If EsEstadoFinal(estado) OrElse CInt(resp.Status) >= 400 Then
                ' Finalizamos devolviendo el Ãºltimo cuerpo
                Return resp.Body
            End If

            ' AÃºn en proceso -> backoff y reintento
            intento += 1
            If intento >= maxIntentos Then
                Return resp.Body ' devolvemos lo Ãºltimo (seguirÃ¡ "en proceso")
            End If

            Thread.Sleep(espera)
            espera = Math.Min(espera * 2, esperaMaxMs)
        Loop
    End Function
    Private Shared Function ConsultarEstadoFactura(ByVal trackId As String, ByVal token As String) As String
        ' Usar concatenaciÃ³n para formar la URL correctamente
        Dim urlConsulta As String = GlobalVariables.url_base & "consultaresultado/api/consultas/estado?trackid=" & trackId
        Dim request As HttpWebRequest = CType(WebRequest.Create(urlConsulta), HttpWebRequest)
        request.Method = "GET"
        request.Headers.Add("Authorization", "Bearer " & token)

        Try
            Using response As HttpWebResponse = CType(request.GetResponse(), HttpWebResponse)
                Using reader As New StreamReader(response.GetResponseStream())
                    Dim responseBody As String = reader.ReadToEnd()
                    Return responseBody
                End Using
            End Using
        Catch ex As Exception
            Throw New Exception("Error al consultar el estado de la factura: " & ex.Message)
        End Try
    End Function
    Private Shared Function GenerarXMLE46(eNCF As String, cadenaConexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String,
                               ByVal fac_forma As String, ByVal tfa_codigo As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()

        ' Nodo raÃ­z del XML
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")

        ' Nodo Encabezado
        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)

        ' Variables para almacenar los valores del encabezado
        Dim montoGravado As String = ""
        Dim montoGravado1 As String = ""
        Dim montoGravado2 As String = ""
        Dim itbisTotal As String = ""
        Dim totalFactura As String = ""
        Dim valorpagar As String = ""
        Dim montopagado As String = ""
        Dim montoexento As String = ""
        Dim unidadMedida As String = ""
        Dim precioUnitarioItem As String = ""
        Dim ultimaLineaDetalle As String = "1" ' Variable para la Ãºltima lÃ­nea del detalle
        Dim cantidadItem As String = ""
        Dim itbis As String = ""
        Dim itbis18 As String = ""
        Dim itbis16 As String = ""
        Dim itbisTotal1 As String = ""
        Dim itbisTotal2 As String = ""
        Dim montoItem As String = ""
        Dim montoTotal As String = ""
        Dim cantidadReferencia As String = ""
        Dim UnidadReferencia As String = ""
        Dim gradosalcohol As String = ""
        Dim preciounitarioreferencia As String = ""

        Dim DecMontoImpuestoAdicional As Decimal
        Dim DecmontoTotal As Decimal
        Dim DecGravado As Decimal
        Dim DecExento As Decimal
        Dim DecItbis As Decimal
        Dim DecItbisDGI As Decimal
        Dim dtSecuencia As New DataTable

        ' Consultar para obtener la informaciÃ³n de Factura
        Dim consultaFactura As String = ConsultaFacturaXmlStdSp(emp_codigo, suc_codigo, facNumero, fac_forma, tfa_codigo)
        Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)
        If facturaReader.Read() Then
            ' AÃ±adir los nodos dentro del Encabezado
            Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

            ' IdDoc
            Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
            encabezadoNode.AppendChild(idDocNode)
            Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeCF").ToString())
            glbTipoeCF = facturaReader("TipoeCF").ToString()

            'Buscamos la secuencia a usar eNCF
            'Dim consultaSecuencia = " select [dbo].[fn_obtenerSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as secuencia ")
            'Dim secuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaSecuencia)
            If eNCF.Trim.Length > 0 Then
                glbncfEnvia = eNCF
                Helper.AddElement(xmlDoc, idDocNode, "eNCF", glbncfEnvia)

                Dim consultaFechaSecuencia = " select [dbo].[fn_obtenerFechaSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as Fecha_vence ")
                Dim FechasecuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaFechaSecuencia)

                If FechasecuenciaReader.Read() Then

                    Dim fechaVencimiento As String = FechasecuenciaReader("Fecha_vence").ToString()
                    If Not String.IsNullOrEmpty(fechaVencimiento) Then
                        Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fechaVencimiento)
                    End If

                    Helper.AddElement(xmlDoc, idDocNode, "IndicadorEnvioDiferido", "1")
                    Helper.AddElement(xmlDoc, idDocNode, "TipoIngresos", "01")
                    Helper.AddElement(xmlDoc, idDocNode, "TipoPago", facturaReader("TipoPago").ToString())

                    ' Verificar si el campo "condicion_pago" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim terminoPago As String = facturaReader("condicion_pago").ToString()

                    Dim fechaLimitePago As String = facturaReader("FechaLimitePago").ToString()
                    If Not String.IsNullOrEmpty(fechaLimitePago) Then
                        Dim fechaFormateada As String = DateTime.Parse(fechaLimitePago).ToString("dd-MM-yyyy")
                        Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", fechaFormateada)
                    End If

                    If Not String.IsNullOrEmpty(terminoPago) Then
                        Helper.AddElement(xmlDoc, idDocNode, "TerminoPago", terminoPago)
                    End If

                    ' Nodo TablaFormasPago
                    Dim consultaFormaPago = " SELECT * FROM vwFormasPagoXML WHERE fac_numero=" & facNumero & " AND fac_forma='" & fac_forma & "' AND tfa_codigo='" & tfa_codigo & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                    Dim formaPagoReader = EjecutarConsultaReader(cadenaConexion, consultaFormaPago)
                    If formaPagoReader.Read() Then
                        Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
                        idDocNode.AppendChild(tablaFormasPagoNode)

                        ' AÃ±adir las formas de pago
                        Do
                            Dim formaNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
                            montopagado = Decimal.Parse(formaPagoReader("MontoPago").ToString()).ToString("F2")

                            tablaFormasPagoNode.AppendChild(formaNode)
                            Helper.AddElement(xmlDoc, formaNode, "FormaPago", formaPagoReader("FormaPago").ToString())
                            Helper.AddElement(xmlDoc, formaNode, "MontoPago", montopagado)

                        Loop While formaPagoReader.Read()
                    End If
                    formaPagoReader.Close()

                    'Helper.AddElement(xmlDoc, idDocNode, "TotalPaginas", "1")

                    ' Nodo Emisor
                    Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
                    encabezadoNode.AppendChild(emisorNode)
                    Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
                    Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
                    Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
                    Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())
                    ' Verificar si el campo "Municipio" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim municipio As String = facturaReader("Municipio").ToString()

                    If Not String.IsNullOrEmpty(municipio) Then
                        Helper.AddElement(xmlDoc, emisorNode, "Municipio", municipio)
                    End If

                    ' Verificar si el campo "Provincia" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim provincia As String = facturaReader("Provincia").ToString()

                    If Not String.IsNullOrEmpty(provincia) Then
                        Helper.AddElement(xmlDoc, emisorNode, "Provincia", provincia)
                    End If

                    Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode,
                        facturaReader("emp_telefono").ToString(),
                        facturaReader("emp_telefono2").ToString())

                    ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim correoEmisor As String = facturaReader("emp_email").ToString()
                    If Not String.IsNullOrEmpty(correoEmisor) Then
                        Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
                    End If

                    'Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email").ToString())

                    ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim WebSite As String = facturaReader("emp_web").ToString()

                    If Not String.IsNullOrEmpty(WebSite) Then
                        Helper.AddElement(xmlDoc, emisorNode, "WebSite", WebSite)
                    End If

                    ' Verificar si el campo "CodigoVendedor" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim codigoVendedor As String = facturaReader("CodigoVendedor").ToString()

                    If Not String.IsNullOrEmpty(codigoVendedor) Then
                        Helper.AddElement(xmlDoc, emisorNode, "CodigoVendedor", codigoVendedor)
                    End If

                    ' Verificar si el campo "NumeroFacturaInterna" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim numeroFacturaInterna As String = facturaReader("NumeroFacturaInterna").ToString()

                    If Not String.IsNullOrEmpty(numeroFacturaInterna) Then
                        Helper.AddElement(xmlDoc, emisorNode, "NumeroFacturaInterna", numeroFacturaInterna)
                    End If

                    ' Verificar si el campo "ZonaVenta" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim zonaVenta As String = facturaReader("ZonaVenta").ToString()

                    If Not String.IsNullOrEmpty(zonaVenta) Then
                        Helper.AddElement(xmlDoc, emisorNode, "ZonaVenta", zonaVenta)
                    End If

                    Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

                    ' Nodo Comprador
                    Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
                    encabezadoNode.AppendChild(compradorNode)
                    Helper.AddElement(xmlDoc, compradorNode, "RNCComprador", facturaReader("cli_rnc").ToString())
                    Helper.AddElement(xmlDoc, compradorNode, "RazonSocialComprador", facturaReader("cli_nombre").ToString())

                    ' Verificar si el campo "ContactoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim contactoComprador As String = facturaReader("cli_contacto").ToString()

                    If Not String.IsNullOrEmpty(contactoComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "ContactoComprador", contactoComprador)
                    End If

                    ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim correoComprador As String = facturaReader("cli_email").ToString()

                    If Not String.IsNullOrEmpty(correoComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "CorreoComprador", correoComprador)
                    End If

                    ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim direccionComprador As String = facturaReader("cli_direccion").ToString()

                    If Not String.IsNullOrEmpty(direccionComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "DireccionComprador", direccionComprador)
                    End If

                    Dim municipioComprador As String = facturaReader("MunicipioComprador").ToString()

                    If Not String.IsNullOrEmpty(municipioComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "MunicipioComprador", municipioComprador)
                    End If

                    Dim provinciaComprador As String = facturaReader("ProvinciaComprador").ToString()

                    If Not String.IsNullOrEmpty(provinciaComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "ProvinciaComprador", provinciaComprador)
                    End If

                    Dim fechaEntrega As String = facturaReader("FechaEntrega").ToString()

                    If Not String.IsNullOrEmpty(fechaEntrega) Then
                        Helper.AddElement(xmlDoc, compradorNode, "FechaEntrega", fechaEntrega)
                    End If

                    Dim fechaOrdenCompra As String = facturaReader("FechaOrdenCompra").ToString()

                    If Not String.IsNullOrEmpty(fechaOrdenCompra) Then
                        Helper.AddElement(xmlDoc, compradorNode, "FechaOrdenCompra", fechaOrdenCompra)
                    End If

                    Dim numeroOrdenCompra As String = facturaReader("NumeroOrdenCompra").ToString()

                    If Not String.IsNullOrEmpty(numeroOrdenCompra) Then
                        Helper.AddElement(xmlDoc, compradorNode, "NumeroOrdenCompra", numeroOrdenCompra)
                    End If

                    Dim codigoInternoComprador As String = facturaReader("CodigoInternoComprador").ToString()

                    If Not String.IsNullOrEmpty(codigoInternoComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "CodigoInternoComprador", codigoInternoComprador)
                    End If

                    Dim numeroContenedor As String = facturaReader("numeroContenedor").ToString()

                    If Not String.IsNullOrEmpty(numeroContenedor) Then
                        Helper.AddElement(xmlDoc, compradorNode, "NumeroContenedor", numeroContenedor)
                    End If

                    Dim numeroReferencia As String = facturaReader("numeroReferencia").ToString()

                    If Not String.IsNullOrEmpty(numeroReferencia) Then
                        Helper.AddElement(xmlDoc, compradorNode, "NumeroReferencia", numeroReferencia)
                    End If

                    ' Nodo InformacionesAdicionales
                    Dim consultaInformacionesAdicionales = " SELECT * FROM InformacionesAdicionales WHERE fac_numero=" & facNumero
                    Dim infoAdicionalReader = EjecutarConsultaReader(cadenaConexion, consultaInformacionesAdicionales)

                    If infoAdicionalReader.Read() Then
                        Dim infoAdicionalNode As XmlElement = xmlDoc.CreateElement("InformacionesAdicionales")
                        encabezadoNode.AppendChild(infoAdicionalNode)

                        Helper.AddElement(xmlDoc, infoAdicionalNode, "PesoBruto", Decimal.Parse(CStr(infoAdicionalReader("PesoBruto"))).ToString("F2"))
                        Helper.AddElement(xmlDoc, infoAdicionalNode, "PesoNeto", Decimal.Parse(CStr(infoAdicionalReader("PesoNeto"))).ToString("F2"))
                        Helper.AddElement(xmlDoc, infoAdicionalNode, "UnidadPesoBruto", infoAdicionalReader("UnidadPesoBruto").ToString())
                        Helper.AddElement(xmlDoc, infoAdicionalNode, "UnidadPesoNeto", infoAdicionalReader("UnidadPesoNeto").ToString())
                        Helper.AddElement(xmlDoc, infoAdicionalNode, "CantidadBulto", Decimal.Parse(CStr(infoAdicionalReader("CantidadBulto"))).ToString("F2"))
                        Helper.AddElement(xmlDoc, infoAdicionalNode, "UnidadBulto", infoAdicionalReader("UnidadBulto").ToString())
                        Helper.AddElement(xmlDoc, infoAdicionalNode, "VolumenBulto", Decimal.Parse(CStr(infoAdicionalReader("VolumenBulto"))).ToString("F2"))
                        Helper.AddElement(xmlDoc, infoAdicionalNode, "UnidadVolumen", infoAdicionalReader("UnidadVolumen").ToString())
                    End If
                    infoAdicionalReader.Close()

                    ' Nodo Transporte
                    Dim consultatransporteReader = " SELECT * FROM Transporte WHERE fac_numero=" & facNumero
                    Dim transporteReader = EjecutarConsultaReader(cadenaConexion, consultatransporteReader)

                    If transporteReader.Read() Then
                        Dim transporteNode As XmlElement = xmlDoc.CreateElement("Transporte")
                        encabezadoNode.AppendChild(transporteNode)
                        Helper.AddElement(xmlDoc, transporteNode, "NumeroAlbaran", transporteReader("NumeroAlbaran").ToString())
                    End If
                    transporteReader.Close()

                    ' Nodo Totales
                    ''Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
                    encabezadoNode.AppendChild(totalesNode)

                    ' Guardar los valores en variables antes de cerrar el reader
                    '' DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis_dgi").ToString())
                    DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis").ToString())
                    DecItbis = Decimal.Parse(facturaReader("fac_itbis").ToString())
                    itbisTotal = Decimal.Parse(facturaReader("fac_itbis").ToString()).ToString("F2")
                    itbisTotal1 = Decimal.Parse(facturaReader("det_totalitbis_1").ToString()).ToString("F2")
                    itbisTotal2 = Decimal.Parse(facturaReader("det_totalitbis_2").ToString()).ToString("F2")

                    itbis18 = Decimal.Parse(facturaReader("det_itbis_1").ToString()).ToString("F0")
                    itbis16 = Decimal.Parse(facturaReader("det_itbis_2").ToString()).ToString("F0")

                    totalFactura = Decimal.Parse(facturaReader("fac_total").ToString()).ToString("F2")
                    DecGravado = If(IsDBNull(facturaReader("monto_grabado")), 0D, Convert.ToDecimal(facturaReader("monto_grabado")))

                    montoGravado = DecGravado.ToString("F2")
                    montoGravado1 = If(IsDBNull(facturaReader("monto_grabado1")), 0D, Convert.ToDecimal(facturaReader("monto_grabado1"))).ToString("F2")
                    montoGravado2 = If(IsDBNull(facturaReader("monto_grabado2")), 0D, Convert.ToDecimal(facturaReader("monto_grabado2"))).ToString("F2")


                    If montoGravado <> "0" And montoGravado <> "0.00" Then
                        Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoTotal", totalFactura)
                    End If

                    Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI3", totalFactura)
                    ' Verificar si el monto exento no es igual a 0.00 antes de agregar el elementoltw1
                    Helper.AddElement(xmlDoc, totalesNode, "ITBIS3", "0")
                    Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS", "0.00")
                    Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS3", "0.00")

                    ' Agregar la secciÃ³n de Impuestos Adicionales solo si totalesNode ha sido creado
                    ' RESTBAR: no existe Impuestos_adicionales_dgi para este flujo

                    If totalesNode IsNot Nothing AndAlso Not isResBar Then
                        Dim consultaimpuestoReader = " SELECT * FROM Impuestos_adicionales_dgi WHERE fac_numero=" & facNumero
                        Dim impuestoReader = EjecutarConsultaReader(cadenaConexion, consultaimpuestoReader)

                        ' Solo agregar MontoImpuestoAdicional si hay registros en Impuestos_adicionales_dgi
                        If impuestoReader.HasRows Then
                            ' Leer el primer registro para obtener el MontoImpuestoAdicional
                            If impuestoReader.Read() Then
                                ' Agregar MontoImpuestoAdicional antes del nodo ImpuestosAdicionales
                                DecMontoImpuestoAdicional = Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional")))
                                If DecMontoImpuestoAdicional.ToString("F2") <> "0.00" Then
                                    Helper.AddElement(xmlDoc, totalesNode, "MontoImpuestoAdicional", Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional"))).ToString("F2"))
                                End If
                            End If

                            ' Nodo ImpuestosAdicionales
                            Dim impuestosAdicionalesNode As XmlElement = xmlDoc.CreateElement("ImpuestosAdicionales")
                            totalesNode.AppendChild(impuestosAdicionalesNode)

                            ' Leer cada fila y aÃ±adir los impuestos adicionales
                            Do
                                ' Para el conjunto de columnas que terminan en _1
                                If Not IsDBNull(impuestoReader("TipoImpuesto_1")) Then
                                    Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode1)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", impuestoReader("TipoImpuesto_1").ToString())
                                    Dim tasaImpuestoAdicional1 As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_1").ToString())

                                    ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                    If tasaImpuestoAdicional1 = Math.Floor(tasaImpuestoAdicional1) Then
                                        ' No tiene decimales, mostramos solo el entero
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F0"))
                                    Else
                                        ' Tiene decimales, mostramos con dos decimales
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F2"))
                                    End If

                                    ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                    If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1").ToString()).ToString("F2"))
                                    End If

                                    ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                    If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1").ToString()).ToString("F2"))
                                    End If

                                    ' Verificar y agregar OtrosImpuestosAdicionales
                                    If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_1")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_1").ToString()).ToString("F2"))
                                    End If
                                End If

                                ' Para el conjunto de columnas que terminan en _2
                                If Not IsDBNull(impuestoReader("TipoImpuesto_2")) Then
                                    Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode2)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", impuestoReader("TipoImpuesto_2").ToString())

                                    Dim tasaImpuestoAdicional As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_2").ToString())

                                    ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                    If tasaImpuestoAdicional = Math.Floor(tasaImpuestoAdicional) Then
                                        ' No tiene decimales, mostramos solo el entero
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F0"))
                                    Else
                                        ' Tiene decimales, mostramos con dos decimales
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F2"))
                                    End If

                                    ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                    If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2").ToString()).ToString("F2"))
                                    End If

                                    ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                    If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2").ToString()).ToString("F2"))
                                    End If

                                    ' Verificar y agregar OtrosImpuestosAdicionales
                                    If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_2")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_2").ToString()).ToString("F2"))
                                    End If
                                End If
                            Loop While impuestoReader.Read()
                        End If
                        impuestoReader.Close()
                    End If

                    Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", totalFactura)

                End If
                facturaReader.Close()

                ' Nodo DetallesItems
                Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
                root.AppendChild(detallesItemsNode)

                ' Consulta para obtener la informaciÃ³n de Detalle de Factura
                Dim consultaDetalle = ConsultaDetalleFacturaXml(emp_codigo, suc_codigo, fac_forma, tfa_codigo, facNumero)
                Dim detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalle)

                While detalleReader.Read()
                    ' Nodo Item dentro de DetallesItems
                    Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
                    detallesItemsNode.AppendChild(itemNode)

                    ' AÃ±adir los detalles del item
                    Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", detalleReader("NumeroLinea").ToString())
                    Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", "3")
                    'Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", detalleReader("IndicadorFacturacion").ToString())
                    Helper.AddElement(xmlDoc, itemNode, "NombreItem", detalleReader("NombreItem").ToString())
                    Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", detalleReader("IndicadorBienoServicio").ToString())

                    cantidadItem = Decimal.Parse(detalleReader("CantidadItem").ToString()).ToString("F2")
                    unidadMedida = Decimal.Parse(detalleReader("UnidadMedida").ToString()).ToString("F0")

                    gradosalcohol = Decimal.Parse(detalleReader("gradosalcohol").ToString()).ToString("F2")
                    preciounitarioreferencia = Decimal.Parse(detalleReader("preciounitarioreferencia").ToString()).ToString("F2")

                    Helper.AddElement(xmlDoc, itemNode, "CantidadItem", cantidadItem)
                    Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", unidadMedida)

                    If (Not String.IsNullOrEmpty(CStr(detalleReader("cantidadreferencia")))) And (Decimal.Parse(detalleReader("cantidadreferencia").ToString()) <> CDec("0")) Then
                        cantidadReferencia = Decimal.Parse(detalleReader("cantidadreferencia").ToString()).ToString("F0")
                        If (Not String.IsNullOrEmpty(cantidadReferencia)) And (cantidadReferencia <> "0") Then
                            Helper.AddElement(xmlDoc, itemNode, "CantidadReferencia", cantidadReferencia)
                        End If
                    End If

                    If (Not String.IsNullOrEmpty(CStr(detalleReader("unidadreferencia")))) And (Decimal.Parse(detalleReader("unidadreferencia").ToString()) <> CDec("0")) Then
                        UnidadReferencia = Decimal.Parse(detalleReader("unidadreferencia").ToString()).ToString("F0")
                        If (Not String.IsNullOrEmpty(UnidadReferencia)) And (UnidadReferencia <> "0") Then
                            Helper.AddElement(xmlDoc, itemNode, "UnidadReferencia", UnidadReferencia)
                        End If
                    End If

                    ' AÃ±adir la tabla de Subcantidad
                    If Not IsDBNull(detalleReader("Subcantidad")) And Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                        If Not detalleReader("Subcantidad").ToString() = "0.00" And Not detalleReader("Subcantidad").ToString() = "" And (Not detalleReader("CodigoSubcantidad").ToString() = "") Then
                            Dim tablaSubcantidadNode As XmlElement = xmlDoc.CreateElement("TablaSubcantidad")
                            itemNode.AppendChild(tablaSubcantidadNode)

                            Dim subcantidadNode As XmlElement = xmlDoc.CreateElement("SubcantidadItem")
                            tablaSubcantidadNode.AppendChild(subcantidadNode)

                            If Not IsDBNull(detalleReader("Subcantidad")) Then
                                Helper.AddElement(xmlDoc, subcantidadNode, "Subcantidad", Decimal.Parse(detalleReader("Subcantidad").ToString()).ToString("F3"))
                            End If

                            If Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                                Helper.AddElement(xmlDoc, subcantidadNode, "CodigoSubcantidad", detalleReader("CodigoSubcantidad").ToString())
                            End If
                        End If
                    End If


                    If (Not String.IsNullOrEmpty(gradosalcohol)) And (gradosalcohol <> "0.00") Then
                        Helper.AddElement(xmlDoc, itemNode, "GradosAlcohol", gradosalcohol)
                    End If

                    If (Not String.IsNullOrEmpty(preciounitarioreferencia)) And (preciounitarioreferencia <> "0.00") Then
                        Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioReferencia", preciounitarioreferencia)
                    End If

                    precioUnitarioItem = Decimal.Parse(detalleReader("PrecioUnitarioItem").ToString()).ToString("F2")
                    Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", precioUnitarioItem)

                    ' AÃ±adir la tabla de impuestos adicionales (TipoImpuesto)
                    If Not IsDBNull(detalleReader("TipoImpuesto1")) And Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                        If Not detalleReader("TipoImpuesto1").ToString() = "" And Not detalleReader("TipoImpuesto2").ToString() = "" Then
                            Dim tablaImpuestoAdicionalNode As XmlElement = xmlDoc.CreateElement("TablaImpuestoAdicional")
                            itemNode.AppendChild(tablaImpuestoAdicionalNode)

                            If Not IsDBNull(detalleReader("TipoImpuesto1")) Then
                                Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode1)
                                Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", detalleReader("TipoImpuesto1").ToString())
                            End If

                            If Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                                Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode2)
                                Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", detalleReader("TipoImpuesto2").ToString())
                            End If
                        End If
                    End If

                    ' Verificar si el DescuentoMonto no es igual a 0.0000 antes de agregar el elemento
                    '  Dim descuentoMonto As String = If(DBNull.Value.Equals(detalleReader("DescuentoMonto")), "0.0000", detalleReader("DescuentoMonto").ToString())
                    Dim descuentoRaw As String = If(detalleReader.IsDBNull(detalleReader.GetOrdinal("DescuentoMonto")), "", detalleReader("DescuentoMonto").ToString())
                    Dim descuentoNorm As String = NormalizeDecimalString(descuentoRaw) ' <-- SIEMPRE "F2" con punto

                    Dim montoDecimal As Decimal

                    ' Verificamos si al menos uno de los subdescuentos es vÃ¡lido antes de crear la tabla

                    ' ===== Validadores =====
                    Dim tipo1 As String = If(IsDBNull(detalleReader("TipoSubDescuento_1")), "", detalleReader("TipoSubDescuento_1").ToString().Trim())
                    Dim tipo2 As String = If(IsDBNull(detalleReader("TipoSubDescuento_2")), "", detalleReader("TipoSubDescuento_2").ToString().Trim())

                    Dim m1 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_1"))
                    Dim m2 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_2"))

                    Dim tipo1Valido As Boolean = (tipo1 = "$" OrElse tipo1 = "%")
                    Dim tipo2Valido As Boolean = (tipo2 = "$" OrElse tipo2 = "%")

                    Dim monto1Valido As Boolean = (m1.HasValue AndAlso m1.Value > 0D)
                    Dim monto2Valido As Boolean = (m2.HasValue AndAlso m2.Value > 0D)

                    Dim tieneSubDescuento1 As Boolean = (tipo1Valido AndAlso monto1Valido)
                    Dim tieneSubDescuento2 As Boolean = (tipo2Valido AndAlso monto2Valido)

                    If descuentoNorm <> "0.00" Then
                        Helper.AddElement(xmlDoc, itemNode, "DescuentoMonto", descuentoNorm)

                        ' Si al menos uno de los subdescuentos es REAL y vÃ¡lido, agregamos la estructura
                        If tieneSubDescuento1 OrElse tieneSubDescuento2 Then
                            ' Crear TablaSubDescuento solo si hay datos vÃ¡lidos
                            Dim subDescuentoNode As XmlElement = xmlDoc.CreateElement("TablaSubDescuento")
                            itemNode.AppendChild(subDescuentoNode)

                            ' Agregar SubDescuento_1
                            If tieneSubDescuento1 Then
                                Dim subDescuentoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                subDescuentoNode.AppendChild(subDescuentoDetailNode1)

                                Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "TipoSubDescuento", tipo1)
                                Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "MontoSubDescuento",
                          m1.Value.ToString("F2", CultureInfo.InvariantCulture))
                            End If

                            ' Agregar SubDescuento_2
                            If tieneSubDescuento2 Then
                                Dim subDescuentoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                subDescuentoNode.AppendChild(subDescuentoDetailNode2)

                                Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "TipoSubDescuento", tipo2)
                                Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "MontoSubDescuento",
                          m2.Value.ToString("F2", CultureInfo.InvariantCulture))
                            End If
                        End If
                    End If

                    ' --- Nueva SecciÃ³n para RecargoMonto y TablaSubRecargo ---
                    Dim recargoMonto As String = ""
                    If Not IsDBNull(detalleReader("RecargoMonto")) Then
                        recargoMonto = detalleReader("RecargoMonto").ToString()
                    End If

                    If Not recargoMonto = "0.00" AndAlso Not String.IsNullOrEmpty(recargoMonto) Then
                        ' Agregar elemento RecargoMonto formateado a dos decimales
                        If Decimal.TryParse(recargoMonto, NumberStyles.Any, CultureInfo.InvariantCulture, montoDecimal) Then
                            Helper.AddElement(xmlDoc, itemNode, "RecargoMonto", montoDecimal.ToString("F2", CultureInfo.InvariantCulture))
                        End If

                        ' Validar la existencia de subrecargos (por ejemplo, dos conjuntos)
                        Dim tieneSubRecargo1 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_1")) AndAlso
                                       Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_1").ToString())
                        Dim tieneSubRecargo2 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_2")) AndAlso
                                       Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_2").ToString())

                        If tieneSubRecargo1 OrElse tieneSubRecargo2 Then
                            Dim tablaSubRecargoNode As XmlElement = xmlDoc.CreateElement("TablaSubRecargo")
                            itemNode.AppendChild(tablaSubRecargoNode)

                            ' Agregar SubRecargo 1
                            If tieneSubRecargo1 Then
                                Dim subRecargoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                                tablaSubRecargoNode.AppendChild(subRecargoDetailNode1)
                                Helper.AddElement(xmlDoc, subRecargoDetailNode1, "TipoSubRecargo", detalleReader("TipoSubRecargo_1").ToString())

                                ' Elemento opcional: SubRecargoPorcentaje
                                If Not IsDBNull(detalleReader("SubRecargoPorcentaje_1")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_1").ToString()) Then
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode1, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_1").ToString())
                                End If

                                ' Elemento opcional: MontoSubRecargo
                                If Not IsDBNull(detalleReader("MontoSubRecargo_1")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_1").ToString()) Then
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode1, "MontoSubRecargo", detalleReader("MontoSubRecargo_1").ToString())
                                End If
                            End If

                            ' Agregar SubRecargo 2
                            If tieneSubRecargo2 Then
                                Dim subRecargoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                                tablaSubRecargoNode.AppendChild(subRecargoDetailNode2)
                                Helper.AddElement(xmlDoc, subRecargoDetailNode2, "TipoSubRecargo", detalleReader("TipoSubRecargo_2").ToString())

                                If Not IsDBNull(detalleReader("SubRecargoPorcentaje_2")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_2").ToString()) Then
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode2, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_2").ToString())
                                End If

                                If Not IsDBNull(detalleReader("MontoSubRecargo_2")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_2").ToString()) Then
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode2, "MontoSubRecargo", detalleReader("MontoSubRecargo_2").ToString())
                                End If
                            End If
                        End If
                    End If

                    ' AÃ±adir MontoItem
                    montoItem = Decimal.Parse(detalleReader("MontoItem").ToString()).ToString("F2")
                    Helper.AddElement(xmlDoc, itemNode, "MontoItem", montoItem)

                    ' Actualizar la Ãºltima lÃ­nea del detalle
                    ultimaLineaDetalle = detalleReader("NumeroLinea").ToString()
                End While
                detalleReader.Close()

                ' AÃ±ade esto dentro de la funciÃ³n GenerarXML, justo antes de agregar la FechaHoraFirma

                ' Consulta la tabla DescuentooRecargo
                Dim consultaDescuentooRecargo = " SELECT * FROM DescuentooRecargo WHERE fac_numero=" & facNumero
                Dim descuentoRecargoReader = EjecutarConsultaReader(cadenaConexion, consultaDescuentooRecargo)

                ' Verifica si hay descuentos o recargos asociados
                If descuentoRecargoReader.HasRows Then
                    ' Crea el nodo DescuentosORecargos
                    Dim descuentosORecargosNode As XmlElement = xmlDoc.CreateElement("DescuentosORecargos")
                    root.AppendChild(descuentosORecargosNode)

                    ' Itera sobre los registros de la tabla
                    Do While descuentoRecargoReader.Read()
                        ' Crea el nodo DescuentoORecargo para cada registro
                        Dim descuentoORecargoNode As XmlElement = xmlDoc.CreateElement("DescuentoORecargo")
                        descuentosORecargosNode.AppendChild(descuentoORecargoNode)

                        ' AÃ±ade los detalles del descuento o recargo
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "NumeroLinea", descuentoRecargoReader("NumeroLineaDoR").ToString())
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoAjuste", descuentoRecargoReader("TipoAjuste").ToString())

                        '' Opcional: Verifica si el campo estÃ¡ presente antes de agregarlo
                        'If Not IsDBNull(descuentoRecargoReader("IndicadorNorma1007")) Then
                        '    Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorNorma1007", descuentoRecargoReader("IndicadorNorma1007").ToString())
                        'End If

                        If Not IsDBNull(descuentoRecargoReader("DescripcionDescuentooRecargo")) Then
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "DescripcionDescuentooRecargo", descuentoRecargoReader("DescripcionDescuentooRecargo").ToString())
                        End If

                        If Not IsDBNull(descuentoRecargoReader("TipoValor")) Then
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoValor", descuentoRecargoReader("TipoValor").ToString())
                        End If

                        If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargo")) Then
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargo", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargo").ToString()).ToString("F2"))
                        End If

                        'If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargoOtraMoneda")) Then
                        '    Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargoOtraMoneda", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargoOtraMoneda").ToString()).ToString("F2"))
                        'End If

                        If Not IsDBNull(descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo")) Then
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorFacturacionDescuentooRecargo", descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo").ToString())
                        End If
                    Loop
                End If
                descuentoRecargoReader.Close()

                ' Nodo FechaHoraFirma fuera del Encabezado y despuÃ©s de Paginacion
                Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
                root.AppendChild(fechaHoraFirmaNode)

                ' Obtener la fecha y hora actual en el formato requerido y agregarla al nodo
                Dim fechaHoraFirma As String = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
                fechaHoraFirmaNode.InnerText = fechaHoraFirma
            End If
        End If
        ' Retorna el documento XML generado
        Return xmlDoc
    End Function


    ' Devuelve el nÃºmero normalizado como string "F2" con punto decimal (p.ej. "1234.50")
    ' Soporta "1,234.5", "1.234,5", "1234,5", "1234.50", "  1 234,50 "â€¦
    Private Shared Function NormalizeDecimalString(input As String, Optional fallback As String = "0.00") As String
        If String.IsNullOrWhiteSpace(input) Then Return fallback
        Dim s = input.Trim()

        ' HeurÃ­stica: detecta el separador decimal por el Ãºltimo punto/coma
        Dim lastDot = s.LastIndexOf("."c)
        Dim lastComma = s.LastIndexOf(","c)
        If lastComma > lastDot Then
            ' Se asume coma decimal: quitar puntos (miles) y cambiar coma->punto
            s = s.Replace(".", "")
            s = s.Replace(",", ".")
        Else
            ' Se asume punto decimal: quitar comas (miles)
            s = s.Replace(",", "")
        End If

        Dim d As Decimal
        If Decimal.TryParse(s, NumberStyles.AllowLeadingSign Or NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, d) Then
            Return d.ToString("F2", CultureInfo.InvariantCulture)
        End If
        Return fallback
    End Function

    ' Verdadero si la cadena representa 0 (tras normalizar)
    Private Shared Function IsZeroString(input As String) As Boolean
        Dim norm = NormalizeDecimalString(input, "0.00")
        Return norm = "0.00"
    End Function

    Private Shared Function GenerarXMLE44(eNCF As String, cadenaConexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String,
                               ByVal fac_forma As String, ByVal tfa_codigo As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()

        ' Nodo raÃ­z del XML
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")

        ' Nodo Encabezado
        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)

        ' Variables para almacenar los valores del encabezado
        Dim montoGravado As String = ""
        Dim montoGravado1 As String = ""
        Dim montoGravado2 As String = ""
        Dim itbisTotal As String = ""
        Dim totalFactura As String = ""
        Dim valorpagar As String = ""
        Dim montopagado As String = ""
        Dim montoexento As String = ""
        Dim unidadMedida As String = ""
        Dim precioUnitarioItem As String = ""
        Dim ultimaLineaDetalle As String = "1" ' Variable para la Ãºltima lÃ­nea del detalle
        Dim cantidadItem As String = ""
        Dim itbis As String = ""
        Dim itbis18 As String = ""
        Dim itbis16 As String = ""
        Dim itbisTotal1 As String = ""
        Dim itbisTotal2 As String = ""
        Dim montoItem As String = ""
        Dim montoTotal As String = ""
        Dim cantidadReferencia As String = ""
        Dim UnidadReferencia As String = ""
        Dim gradosalcohol As String = ""
        Dim preciounitarioreferencia As String = ""

        Dim DecMontoImpuestoAdicional As Decimal
        Dim DecmontoTotal As Decimal
        Dim DecGravado As Decimal
        Dim DecExento As Decimal
        Dim DecItbis As Decimal
        Dim DecItbisDGI As Decimal
        Dim dtSecuencia As New DataTable

        ' Consultar para obtener la informaciÃ³n de Factura
        Dim consultaFactura As String = ConsultaFacturaXmlStdSp(emp_codigo, suc_codigo, facNumero, fac_forma, tfa_codigo)
        Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)
        If facturaReader.Read() Then
            ' AÃ±adir los nodos dentro del Encabezado
            Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

            ' IdDoc
            Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
            encabezadoNode.AppendChild(idDocNode)
            Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeCF").ToString())
            glbTipoeCF = facturaReader("TipoeCF").ToString()

            'Buscamos la secuencia a usar eNCF
            'Dim consultaSecuencia = " select [dbo].[fn_obtenerSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as secuencia ")
            'Dim secuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaSecuencia)
            If eNCF.Trim.Length > 0 Then
                glbncfEnvia = eNCF
                Helper.AddElement(xmlDoc, idDocNode, "eNCF", glbncfEnvia)

                Dim consultaFechaSecuencia = " select [dbo].[fn_obtenerFechaSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as Fecha_vence ")
                Dim FechasecuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaFechaSecuencia)
                If FechasecuenciaReader.Read() Then

                    Dim fechaVencimiento As String = FechasecuenciaReader("Fecha_vence").ToString()
                    If Not String.IsNullOrEmpty(fechaVencimiento) Then
                        Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fechaVencimiento)
                    End If

                    Helper.AddElement(xmlDoc, idDocNode, "IndicadorEnvioDiferido", "1")
                    Helper.AddElement(xmlDoc, idDocNode, "IndicadorServicioTodoIncluido", "1")

                    Helper.AddElement(xmlDoc, idDocNode, "TipoIngresos", "01")
                    Helper.AddElement(xmlDoc, idDocNode, "TipoPago", facturaReader("TipoPago").ToString())

                    ' Verificar si el campo "condicion_pago" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim terminoPago As String = facturaReader("condicion_pago").ToString()

                    Dim fechaLimitePago As String = facturaReader("FechaLimitePago").ToString()
                    If Not String.IsNullOrEmpty(fechaLimitePago) Then
                        Dim fechaFormateada As String = DateTime.Parse(fechaLimitePago).ToString("dd-MM-yyyy")
                        Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", fechaFormateada)
                    End If

                    If Not String.IsNullOrEmpty(terminoPago) Then
                        Helper.AddElement(xmlDoc, idDocNode, "TerminoPago", terminoPago)
                    End If

                    ' Nodo TablaFormasPago
                    Dim consultaFormaPago = " SELECT * FROM vwFormasPagoXML WHERE fac_numero=" & facNumero & " AND fac_forma='" & fac_forma & "' AND tfa_codigo='" & tfa_codigo & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                    Dim formaPagoReader = EjecutarConsultaReader(cadenaConexion, consultaFormaPago)

                    If formaPagoReader.Read() Then
                        Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
                        idDocNode.AppendChild(tablaFormasPagoNode)

                        ' AÃ±adir las formas de pago
                        Do
                            Dim formaNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
                            montopagado = Decimal.Parse(formaPagoReader("MontoPago").ToString()).ToString("F2")

                            tablaFormasPagoNode.AppendChild(formaNode)
                            Helper.AddElement(xmlDoc, formaNode, "FormaPago", formaPagoReader("FormaPago").ToString())
                            Helper.AddElement(xmlDoc, formaNode, "MontoPago", montopagado)

                        Loop While formaPagoReader.Read()
                    End If
                    formaPagoReader.Close()

                    'Helper.AddElement(xmlDoc, idDocNode, "TotalPaginas", "1")

                    ' Nodo Emisor
                    Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
                    encabezadoNode.AppendChild(emisorNode)
                    Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
                    Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
                    Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
                    Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())
                    ' Verificar si el campo "Municipio" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim municipio As String = facturaReader("Municipio").ToString()

                    If Not String.IsNullOrEmpty(municipio) Then
                        Helper.AddElement(xmlDoc, emisorNode, "Municipio", municipio)
                    End If

                    ' Verificar si el campo "Provincia" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim provincia As String = facturaReader("Provincia").ToString()

                    If Not String.IsNullOrEmpty(provincia) Then
                        Helper.AddElement(xmlDoc, emisorNode, "Provincia", provincia)
                    End If

                    Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode,
                        facturaReader("emp_telefono").ToString(),
                        facturaReader("emp_telefono2").ToString())

                    ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim correoEmisor As String = facturaReader("emp_email").ToString()
                    If Not String.IsNullOrEmpty(correoEmisor) Then
                        Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
                    End If

                    'Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email").ToString())

                    ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim WebSite As String = facturaReader("emp_web").ToString()

                    If Not String.IsNullOrEmpty(WebSite) Then
                        Helper.AddElement(xmlDoc, emisorNode, "WebSite", WebSite)
                    End If

                    ' Verificar si el campo "CodigoVendedor" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim codigoVendedor As String = facturaReader("CodigoVendedor").ToString()

                    If Not String.IsNullOrEmpty(codigoVendedor) Then
                        Helper.AddElement(xmlDoc, emisorNode, "CodigoVendedor", codigoVendedor)
                    End If

                    ' Verificar si el campo "NumeroFacturaInterna" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim numeroFacturaInterna As String = facturaReader("NumeroFacturaInterna").ToString()

                    If Not String.IsNullOrEmpty(numeroFacturaInterna) Then
                        Helper.AddElement(xmlDoc, emisorNode, "NumeroFacturaInterna", numeroFacturaInterna)
                    End If

                    ' Verificar si el campo "ZonaVenta" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim zonaVenta As String = facturaReader("ZonaVenta").ToString()

                    If Not String.IsNullOrEmpty(zonaVenta) Then
                        Helper.AddElement(xmlDoc, emisorNode, "ZonaVenta", zonaVenta)
                    End If

                    Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

                    ' Nodo Comprador
                    Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
                    encabezadoNode.AppendChild(compradorNode)
                    Helper.AddElement(xmlDoc, compradorNode, "RNCComprador", facturaReader("cli_rnc").ToString())
                    Helper.AddElement(xmlDoc, compradorNode, "RazonSocialComprador", facturaReader("cli_nombre").ToString())

                    ' Verificar si el campo "ContactoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim contactoComprador As String = facturaReader("cli_contacto").ToString()

                    If Not String.IsNullOrEmpty(contactoComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "ContactoComprador", contactoComprador)
                    End If

                    ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim correoComprador As String = facturaReader("cli_email").ToString()

                    If Not String.IsNullOrEmpty(correoComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "CorreoComprador", correoComprador)
                    End If

                    ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                    Dim direccionComprador As String = facturaReader("cli_direccion").ToString()

                    If Not String.IsNullOrEmpty(direccionComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "DireccionComprador", direccionComprador)
                    End If

                    Dim municipioComprador As String = facturaReader("MunicipioComprador").ToString()

                    If Not String.IsNullOrEmpty(municipioComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "MunicipioComprador", municipioComprador)
                    End If

                    Dim provinciaComprador As String = facturaReader("ProvinciaComprador").ToString()

                    If Not String.IsNullOrEmpty(provinciaComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "ProvinciaComprador", provinciaComprador)
                    End If

                    Dim fechaEntrega As String = facturaReader("FechaEntrega").ToString()

                    If Not String.IsNullOrEmpty(fechaEntrega) Then
                        Helper.AddElement(xmlDoc, compradorNode, "FechaEntrega", fechaEntrega)
                    End If

                    Dim fechaOrdenCompra As String = facturaReader("FechaOrdenCompra").ToString()

                    If Not String.IsNullOrEmpty(fechaOrdenCompra) Then
                        Helper.AddElement(xmlDoc, compradorNode, "FechaOrdenCompra", fechaOrdenCompra)
                    End If

                    Dim numeroOrdenCompra As String = facturaReader("NumeroOrdenCompra").ToString()

                    If Not String.IsNullOrEmpty(numeroOrdenCompra) Then
                        Helper.AddElement(xmlDoc, compradorNode, "NumeroOrdenCompra", numeroOrdenCompra)
                    End If

                    Dim codigoInternoComprador As String = facturaReader("CodigoInternoComprador").ToString()

                    If Not String.IsNullOrEmpty(codigoInternoComprador) Then
                        Helper.AddElement(xmlDoc, compradorNode, "CodigoInternoComprador", codigoInternoComprador)
                    End If

                    Dim numeroContenedor As String = facturaReader("numeroContenedor").ToString()

                    If Not String.IsNullOrEmpty(numeroContenedor) Then
                        Helper.AddElement(xmlDoc, compradorNode, "NumeroContenedor", numeroContenedor)
                    End If

                    Dim numeroReferencia As String = facturaReader("numeroReferencia").ToString()

                    If Not String.IsNullOrEmpty(numeroReferencia) Then
                        Helper.AddElement(xmlDoc, compradorNode, "NumeroReferencia", numeroReferencia)
                    End If


                    ' Nodo Totales
                    ''Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
                    encabezadoNode.AppendChild(totalesNode)

                    ' Guardar los valores en variables antes de cerrar el reader
                    '' DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis_dgi").ToString())
                    DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis").ToString())
                    DecItbis = Decimal.Parse(facturaReader("fac_itbis").ToString())
                    itbisTotal = Decimal.Parse(facturaReader("fac_itbis").ToString()).ToString("F2")
                    itbisTotal1 = Decimal.Parse(facturaReader("det_totalitbis_1").ToString()).ToString("F2")
                    itbisTotal2 = Decimal.Parse(facturaReader("det_totalitbis_2").ToString()).ToString("F2")

                    itbis18 = Decimal.Parse(facturaReader("det_itbis_1").ToString()).ToString("F0")
                    itbis16 = Decimal.Parse(facturaReader("det_itbis_2").ToString()).ToString("F0")

                    totalFactura = Decimal.Parse(facturaReader("fac_total").ToString()).ToString("F2")
                    valorpagar = Decimal.Parse(facturaReader("ValorPagar").ToString()).ToString("F2")

                    Helper.AddElement(xmlDoc, totalesNode, "MontoExento", totalFactura)

                    ' Agregar la secciÃ³n de Impuestos Adicionales solo si totalesNode ha sido creado
                    ' RESTBAR: no existe Impuestos_adicionales_dgi para este flujo

                    If totalesNode IsNot Nothing AndAlso Not isResBar Then
                        Dim consultaImpuesto = " SELECT * FROM Impuestos_adicionales_dgi WHERE fac_numero=" & facNumero
                        Dim impuestoReader = EjecutarConsultaReader(cadenaConexion, consultaImpuesto)

                        ' Solo agregar MontoImpuestoAdicional si hay registros en Impuestos_adicionales_dgi
                        If impuestoReader.HasRows Then
                            ' Leer el primer registro para obtener el MontoImpuestoAdicional
                            If impuestoReader.Read() Then
                                ' Agregar MontoImpuestoAdicional antes del nodo ImpuestosAdicionales
                                DecMontoImpuestoAdicional = Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional")))
                                If DecMontoImpuestoAdicional.ToString("F2") <> "0.00" Then
                                    Helper.AddElement(xmlDoc, totalesNode, "MontoImpuestoAdicional", Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional"))).ToString("F2"))
                                End If
                            End If

                            ' Nodo ImpuestosAdicionales
                            Dim impuestosAdicionalesNode As XmlElement = xmlDoc.CreateElement("ImpuestosAdicionales")
                            totalesNode.AppendChild(impuestosAdicionalesNode)

                            ' Leer cada fila y aÃ±adir los impuestos adicionales
                            Do
                                ' Para el conjunto de columnas que terminan en _1
                                If Not IsDBNull(impuestoReader("TipoImpuesto_1")) Then
                                    Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode1)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", impuestoReader("TipoImpuesto_1").ToString())
                                    Dim tasaImpuestoAdicional1 As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_1").ToString())

                                    ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                    If tasaImpuestoAdicional1 = Math.Floor(tasaImpuestoAdicional1) Then
                                        ' No tiene decimales, mostramos solo el entero
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F0"))
                                    Else
                                        ' Tiene decimales, mostramos con dos decimales
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F2"))
                                    End If

                                    ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                    If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1").ToString()).ToString("F2"))
                                    End If

                                    ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                    If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1").ToString()).ToString("F2"))
                                    End If

                                    ' Verificar y agregar OtrosImpuestosAdicionales
                                    If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_1")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_1").ToString()).ToString("F2"))
                                    End If
                                End If

                                ' Para el conjunto de columnas que terminan en _2
                                If Not IsDBNull(impuestoReader("TipoImpuesto_2")) Then
                                    Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode2)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", impuestoReader("TipoImpuesto_2").ToString())

                                    Dim tasaImpuestoAdicional As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_2").ToString())

                                    ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                    If tasaImpuestoAdicional = Math.Floor(tasaImpuestoAdicional) Then
                                        ' No tiene decimales, mostramos solo el entero
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F0"))
                                    Else
                                        ' Tiene decimales, mostramos con dos decimales
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F2"))
                                    End If

                                    ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                    If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2").ToString()).ToString("F2"))
                                    End If

                                    ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                    If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2").ToString()).ToString("F2"))
                                    End If

                                    ' Verificar y agregar OtrosImpuestosAdicionales
                                    If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_2")) Then
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_2").ToString()).ToString("F2"))
                                    End If
                                End If
                            Loop While impuestoReader.Read()
                        End If
                        impuestoReader.Close()
                    End If

                    If (DecItbisDGI > 0) Then
                        DecmontoTotal = DecGravado + DecExento + DecItbisDGI + DecMontoImpuestoAdicional
                    Else
                        DecmontoTotal = DecGravado + DecExento + DecItbis + DecMontoImpuestoAdicional
                    End If

                    'Dim montoTotalStr As String = (Math.Floor(DecmontoTotal * 100) / 100).ToString("0.00")
                    Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", totalFactura)
                    If (Not String.IsNullOrEmpty(valorpagar)) And (valorpagar <> "0.00") Then
                        Helper.AddElement(xmlDoc, totalesNode, "ValorPagar", valorpagar)
                    End If


                End If
                facturaReader.Close()

                ' Nodo DetallesItems
                Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
                root.AppendChild(detallesItemsNode)

                ' Consulta para obtener la informaciÃ³n de Detalle de Factura
                Dim consultaDetalle = ConsultaDetalleFacturaXml(emp_codigo, suc_codigo, fac_forma, tfa_codigo, facNumero)
                Dim detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalle)

                While detalleReader.Read()
                    ' Nodo Item dentro de DetallesItems
                    Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
                    detallesItemsNode.AppendChild(itemNode)

                    ' AÃ±adir los detalles del item
                    Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", detalleReader("NumeroLinea").ToString())
                    Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", "4")
                    Helper.AddElement(xmlDoc, itemNode, "NombreItem", detalleReader("NombreItem").ToString())
                    Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", detalleReader("IndicadorBienoServicio").ToString())

                    cantidadItem = Decimal.Parse(detalleReader("CantidadItem").ToString()).ToString("F2")
                    unidadMedida = Decimal.Parse(detalleReader("UnidadMedida").ToString()).ToString("F0")

                    gradosalcohol = Decimal.Parse(detalleReader("gradosalcohol").ToString()).ToString("F2")
                    preciounitarioreferencia = Decimal.Parse(detalleReader("preciounitarioreferencia").ToString()).ToString("F2")

                    Helper.AddElement(xmlDoc, itemNode, "CantidadItem", cantidadItem)
                    Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", unidadMedida)

                    If (Not String.IsNullOrEmpty(CStr(detalleReader("cantidadreferencia")))) And (Decimal.Parse(detalleReader("cantidadreferencia").ToString()) <> CDec("0")) Then
                        cantidadReferencia = Decimal.Parse(detalleReader("cantidadreferencia").ToString()).ToString("F0")
                        If (Not String.IsNullOrEmpty(cantidadReferencia)) And (cantidadReferencia <> "0") Then
                            Helper.AddElement(xmlDoc, itemNode, "CantidadReferencia", cantidadReferencia)
                        End If
                    End If

                    If (Not String.IsNullOrEmpty(CStr(detalleReader("unidadreferencia")))) And (Decimal.Parse(detalleReader("unidadreferencia").ToString()) <> CDec("0")) Then
                        UnidadReferencia = Decimal.Parse(detalleReader("unidadreferencia").ToString()).ToString("F0")
                        If (Not String.IsNullOrEmpty(UnidadReferencia)) And (UnidadReferencia <> "0") Then
                            Helper.AddElement(xmlDoc, itemNode, "UnidadReferencia", UnidadReferencia)
                        End If
                    End If

                    ' AÃ±adir la tabla de Subcantidad
                    If Not IsDBNull(detalleReader("Subcantidad")) And Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                        If Not detalleReader("Subcantidad").ToString() = "0.00" And Not detalleReader("Subcantidad").ToString() = "" And (Not detalleReader("CodigoSubcantidad").ToString() = "") Then
                            Dim tablaSubcantidadNode As XmlElement = xmlDoc.CreateElement("TablaSubcantidad")
                            itemNode.AppendChild(tablaSubcantidadNode)

                            Dim subcantidadNode As XmlElement = xmlDoc.CreateElement("SubcantidadItem")
                            tablaSubcantidadNode.AppendChild(subcantidadNode)

                            If Not IsDBNull(detalleReader("Subcantidad")) Then
                                Helper.AddElement(xmlDoc, subcantidadNode, "Subcantidad", Decimal.Parse(detalleReader("Subcantidad").ToString()).ToString("F3"))
                            End If

                            If Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                                Helper.AddElement(xmlDoc, subcantidadNode, "CodigoSubcantidad", detalleReader("CodigoSubcantidad").ToString())
                            End If
                        End If
                    End If


                    If (Not String.IsNullOrEmpty(gradosalcohol)) And (gradosalcohol <> "0.00") Then
                        Helper.AddElement(xmlDoc, itemNode, "GradosAlcohol", gradosalcohol)
                    End If

                    If (Not String.IsNullOrEmpty(preciounitarioreferencia)) And (preciounitarioreferencia <> "0.00") Then
                        Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioReferencia", preciounitarioreferencia)
                    End If

                    precioUnitarioItem = Decimal.Parse(detalleReader("PrecioUnitarioItem").ToString()).ToString("F2")
                    Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", precioUnitarioItem)

                    ' AÃ±adir la tabla de impuestos adicionales (TipoImpuesto)
                    If Not IsDBNull(detalleReader("TipoImpuesto1")) And Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                        If Not detalleReader("TipoImpuesto1").ToString() = "" And Not detalleReader("TipoImpuesto2").ToString() = "" Then
                            Dim tablaImpuestoAdicionalNode As XmlElement = xmlDoc.CreateElement("TablaImpuestoAdicional")
                            itemNode.AppendChild(tablaImpuestoAdicionalNode)

                            If Not IsDBNull(detalleReader("TipoImpuesto1")) Then
                                Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode1)
                                Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", detalleReader("TipoImpuesto1").ToString())
                            End If

                            If Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                                Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode2)
                                Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", detalleReader("TipoImpuesto2").ToString())
                            End If
                        End If
                    End If
                    ' Verificar si el DescuentoMonto no es igual a 0.0000 antes de agregar el elemento
                    '  Dim descuentoMonto As String = If(DBNull.Value.Equals(detalleReader("DescuentoMonto")), "0.0000", detalleReader("DescuentoMonto").ToString())
                    Dim descuentoRaw As String = If(detalleReader.IsDBNull(detalleReader.GetOrdinal("DescuentoMonto")), "", detalleReader("DescuentoMonto").ToString())
                    Dim descuentoNorm As String = NormalizeDecimalString(descuentoRaw) ' <-- SIEMPRE "F2" con punto

                    Dim montoDecimal As Decimal

                    ' Verificamos si al menos uno de los subdescuentos es vÃ¡lido antes de crear la tabla

                    ' ===== Validadores =====
                    Dim tipo1 As String = If(IsDBNull(detalleReader("TipoSubDescuento_1")), "", detalleReader("TipoSubDescuento_1").ToString().Trim())
                    Dim tipo2 As String = If(IsDBNull(detalleReader("TipoSubDescuento_2")), "", detalleReader("TipoSubDescuento_2").ToString().Trim())

                    Dim m1 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_1"))
                    Dim m2 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_2"))

                    Dim tipo1Valido As Boolean = (tipo1 = "$" OrElse tipo1 = "%")
                    Dim tipo2Valido As Boolean = (tipo2 = "$" OrElse tipo2 = "%")

                    Dim monto1Valido As Boolean = (m1.HasValue AndAlso m1.Value > 0D)
                    Dim monto2Valido As Boolean = (m2.HasValue AndAlso m2.Value > 0D)

                    Dim tieneSubDescuento1 As Boolean = (tipo1Valido AndAlso monto1Valido)
                    Dim tieneSubDescuento2 As Boolean = (tipo2Valido AndAlso monto2Valido)

                    If descuentoNorm <> "0.00" Then
                        Helper.AddElement(xmlDoc, itemNode, "DescuentoMonto", descuentoNorm)

                        ' Si al menos uno de los subdescuentos es REAL y vÃ¡lido, agregamos la estructura
                        If tieneSubDescuento1 OrElse tieneSubDescuento2 Then
                            ' Crear TablaSubDescuento solo si hay datos vÃ¡lidos
                            Dim subDescuentoNode As XmlElement = xmlDoc.CreateElement("TablaSubDescuento")
                            itemNode.AppendChild(subDescuentoNode)

                            ' Agregar SubDescuento_1
                            If tieneSubDescuento1 Then
                                Dim subDescuentoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                subDescuentoNode.AppendChild(subDescuentoDetailNode1)

                                Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "TipoSubDescuento", tipo1)
                                Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "MontoSubDescuento",
                          m1.Value.ToString("F2", CultureInfo.InvariantCulture))
                            End If

                            ' Agregar SubDescuento_2
                            If tieneSubDescuento2 Then
                                Dim subDescuentoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                subDescuentoNode.AppendChild(subDescuentoDetailNode2)

                                Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "TipoSubDescuento", tipo2)
                                Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "MontoSubDescuento",
                          m2.Value.ToString("F2", CultureInfo.InvariantCulture))
                            End If
                        End If
                    End If

                    ' --- Nueva SecciÃ³n para RecargoMonto y TablaSubRecargo ---
                    Dim recargoMonto As String = ""
                    If Not IsDBNull(detalleReader("RecargoMonto")) Then
                        recargoMonto = detalleReader("RecargoMonto").ToString()
                    End If

                    If Not recargoMonto = "0.00" AndAlso Not String.IsNullOrEmpty(recargoMonto) Then
                        ' Agregar elemento RecargoMonto formateado a dos decimales
                        If Decimal.TryParse(recargoMonto, NumberStyles.Any, CultureInfo.InvariantCulture, montoDecimal) Then
                            Helper.AddElement(xmlDoc, itemNode, "RecargoMonto", montoDecimal.ToString("F2", CultureInfo.InvariantCulture))
                        End If

                        ' Validar la existencia de subrecargos (por ejemplo, dos conjuntos)
                        Dim tieneSubRecargo1 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_1")) AndAlso
                                       Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_1").ToString())
                        Dim tieneSubRecargo2 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_2")) AndAlso
                                       Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_2").ToString())

                        If tieneSubRecargo1 OrElse tieneSubRecargo2 Then
                            Dim tablaSubRecargoNode As XmlElement = xmlDoc.CreateElement("TablaSubRecargo")
                            itemNode.AppendChild(tablaSubRecargoNode)

                            ' Agregar SubRecargo 1
                            If tieneSubRecargo1 Then
                                Dim subRecargoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                                tablaSubRecargoNode.AppendChild(subRecargoDetailNode1)
                                Helper.AddElement(xmlDoc, subRecargoDetailNode1, "TipoSubRecargo", detalleReader("TipoSubRecargo_1").ToString())

                                ' Elemento opcional: SubRecargoPorcentaje
                                If Not IsDBNull(detalleReader("SubRecargoPorcentaje_1")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_1").ToString()) Then
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode1, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_1").ToString())
                                End If

                                ' Elemento opcional: MontoSubRecargo
                                If Not IsDBNull(detalleReader("MontoSubRecargo_1")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_1").ToString()) Then
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode1, "MontoSubRecargo", detalleReader("MontoSubRecargo_1").ToString())
                                End If
                            End If

                            ' Agregar SubRecargo 2
                            If tieneSubRecargo2 Then
                                Dim subRecargoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                                tablaSubRecargoNode.AppendChild(subRecargoDetailNode2)
                                Helper.AddElement(xmlDoc, subRecargoDetailNode2, "TipoSubRecargo", detalleReader("TipoSubRecargo_2").ToString())

                                If Not IsDBNull(detalleReader("SubRecargoPorcentaje_2")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_2").ToString()) Then
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode2, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_2").ToString())
                                End If

                                If Not IsDBNull(detalleReader("MontoSubRecargo_2")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_2").ToString()) Then
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode2, "MontoSubRecargo", detalleReader("MontoSubRecargo_2").ToString())
                                End If
                            End If
                        End If
                    End If

                    ' AÃ±adir MontoItem
                    montoItem = Decimal.Parse(detalleReader("MontoItem").ToString()).ToString("F2")
                    Helper.AddElement(xmlDoc, itemNode, "MontoItem", montoItem)

                    ' Actualizar la Ãºltima lÃ­nea del detalle
                    ultimaLineaDetalle = detalleReader("NumeroLinea").ToString()
                End While
                detalleReader.Close()

                ' AÃ±ade esto dentro de la funciÃ³n GenerarXML, justo antes de agregar la FechaHoraFirma

                ' Consulta la tabla DescuentooRecargo
                Dim consultaDescuentooRecargo = " SELECT * FROM DescuentooRecargo WHERE fac_numero=" & facNumero
                Dim descuentoRecargoReader = EjecutarConsultaReader(cadenaConexion, consultaDescuentooRecargo)

                ' Verifica si hay descuentos o recargos asociados
                If descuentoRecargoReader.HasRows Then
                    ' Crea el nodo DescuentosORecargos
                    Dim descuentosORecargosNode As XmlElement = xmlDoc.CreateElement("DescuentosORecargos")
                    root.AppendChild(descuentosORecargosNode)

                    ' Itera sobre los registros de la tabla
                    Do While descuentoRecargoReader.Read()
                        ' Crea el nodo DescuentoORecargo para cada registro
                        Dim descuentoORecargoNode As XmlElement = xmlDoc.CreateElement("DescuentoORecargo")
                        descuentosORecargosNode.AppendChild(descuentoORecargoNode)

                        ' AÃ±ade los detalles del descuento o recargo
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "NumeroLinea", descuentoRecargoReader("NumeroLineaDoR").ToString())
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoAjuste", descuentoRecargoReader("TipoAjuste").ToString())

                        '' Opcional: Verifica si el campo estÃ¡ presente antes de agregarlo
                        'If Not IsDBNull(descuentoRecargoReader("IndicadorNorma1007")) Then
                        '    Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorNorma1007", descuentoRecargoReader("IndicadorNorma1007").ToString())
                        'End If

                        If Not IsDBNull(descuentoRecargoReader("DescripcionDescuentooRecargo")) Then
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "DescripcionDescuentooRecargo", descuentoRecargoReader("DescripcionDescuentooRecargo").ToString())
                        End If

                        If Not IsDBNull(descuentoRecargoReader("TipoValor")) Then
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoValor", descuentoRecargoReader("TipoValor").ToString())
                        End If

                        If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargo")) Then
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargo", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargo").ToString()).ToString("F2"))
                        End If

                        'If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargoOtraMoneda")) Then
                        '    Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargoOtraMoneda", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargoOtraMoneda").ToString()).ToString("F2"))
                        'End If

                        If Not IsDBNull(descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo")) Then
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorFacturacionDescuentooRecargo", descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo").ToString())
                        End If
                    Loop
                End If
                descuentoRecargoReader.Close()

                ' Nodo FechaHoraFirma fuera del Encabezado y despuÃ©s de Paginacion
                Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
                root.AppendChild(fechaHoraFirmaNode)

                ' Obtener la fecha y hora actual en el formato requerido y agregarla al nodo
                Dim fechaHoraFirma As String = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
                fechaHoraFirmaNode.InnerText = fechaHoraFirma
            End If
        End If
        ' Retorna el documento XML generado
        Return xmlDoc
    End Function
    Private Shared Function GenerarXMLE45(eNCF As String, cadenaConexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String,
                               ByVal fac_forma As String, ByVal tfa_codigo As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()
        Dim eNcfNorm As String = If(eNCF, "").Trim()
        Dim prefijo As String = If(eNcfNorm.Length >= 3, eNcfNorm.Substring(0, 3), eNcfNorm)

        ' Nodo raÃ­z del XML
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")

        ' Nodo Encabezado
        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)

        ' Variables para almacenar los valores del encabezado
        Dim montoGravado As String = ""
        Dim montoGravado1 As String = ""
        Dim montoGravado2 As String = ""
        Dim itbisTotal As String = ""
        Dim totalFactura As String = ""
        Dim valorpagar As String = ""
        Dim montopagado As String = ""
        Dim montoexento As String = ""
        Dim unidadMedida As String = ""
        Dim precioUnitarioItem As String = ""
        Dim ultimaLineaDetalle As String = "1" ' Variable para la Ãºltima lÃ­nea del detalle
        Dim cantidadItem As String = ""
        Dim itbis As String = ""
        Dim itbis18 As String = ""
        Dim itbis16 As String = ""
        Dim itbisTotal1 As String = ""
        Dim itbisTotal2 As String = ""
        Dim montoItem As String = ""
        Dim montoTotal As String = ""
        Dim cantidadReferencia As String = ""
        Dim UnidadReferencia As String = ""
        Dim gradosalcohol As String = ""
        Dim preciounitarioreferencia As String = ""

        Dim DecMontoImpuestoAdicional As Decimal
        Dim DecmontoTotal As Decimal
        Dim DecGravado As Decimal
        Dim DecExento As Decimal
        Dim DecItbis As Decimal
        Dim DecItbisDGI As Decimal
        Dim dtSecuencia As New DataTable

        Try


            ' Consultar para obtener la informaciÃ³n de Factura
            Dim consultaFactura As String = ConsultaFacturaXmlStdSp(emp_codigo, suc_codigo, facNumero, fac_forma, tfa_codigo)
            Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)
            If facturaReader.Read() Then
                ' AÃ±adir los nodos dentro del Encabezado
                Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

                ' IdDoc
                Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
                encabezadoNode.AppendChild(idDocNode)
                Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeCF").ToString())
                glbTipoeCF = facturaReader("TipoeCF").ToString()

                'Buscamos la secuencia a usar eNCF
                'Dim consultaSecuencia = " select [dbo].[fn_obtenerSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as secuencia ")
                'Dim secuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaSecuencia)
                If eNCF.Trim.Length > 0 Then
                    glbncfEnvia = eNCF
                    Helper.AddElement(xmlDoc, idDocNode, "eNCF", glbncfEnvia)

                    Dim emp As Integer = If(Integer.TryParse(emp_codigo, Nothing), CInt(emp_codigo), 0)
                    Dim tipo As Integer = If(Integer.TryParse(facturaReader("TipoeCF").ToString(), Nothing), CInt(facturaReader("TipoeCF")), 0)

                    Dim consultaFechaSecuencia As String = "SELECT [dbo].[fn_obtenerFechaSecuenciaDGI](" & emp & "," & tipo & ") AS Fecha_vence"

                    Dim FechasecuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaFechaSecuencia)

                    If FechasecuenciaReader.Read() Then

                        Dim fechaVencimiento As String = FechasecuenciaReader("Fecha_vence").ToString()
                        If Not String.IsNullOrEmpty(fechaVencimiento) Then
                            Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fechaVencimiento)
                        End If

                        'Helper.AddElement(xmlDoc, idDocNode, "IndicadorEnvioDiferido", "1")

                        Dim indicadorMonto As String = If(IsDBNull(facturaReader("IndicadorMontoGravado")), "", facturaReader("IndicadorMontoGravado").ToString())

                        If (prefijo <> "E44") Then
                            If Not String.IsNullOrEmpty(indicadorMonto) Then
                                Helper.AddElement(xmlDoc, idDocNode, "IndicadorMontoGravado", indicadorMonto)
                            End If
                        End If

                        'Helper.AddElement(xmlDoc, idDocNode, "IndicadorServicioTodoIncluido", "1")

                        Helper.AddElement(xmlDoc, idDocNode, "TipoIngresos", "01")
                        Helper.AddElement(xmlDoc, idDocNode, "TipoPago", facturaReader("TipoPago").ToString())

                        ' Verificar si el campo "condicion_pago" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim terminoPago As String = facturaReader("condicion_pago").ToString()

                        Dim fechaLimite As DateTime
                        If Helper.TryGetDate(facturaReader, "FechaLimitePago", fechaLimite) Then
                            Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", fechaLimite.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                        End If

                        If Not String.IsNullOrEmpty(terminoPago) Then
                            Helper.AddElement(xmlDoc, idDocNode, "TerminoPago", terminoPago)
                        End If

                        ' Nodo TablaFormasPago
                        Dim consultaFormaPago = " SELECT * FROM vwFormasPagoXML WHERE fac_numero=" & facNumero & " AND fac_forma='" & fac_forma & "' AND tfa_codigo='" & tfa_codigo & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                        Dim formaPagoReader = EjecutarConsultaReader(cadenaConexion, consultaFormaPago)
                        If formaPagoReader.Read() Then
                            Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
                            idDocNode.AppendChild(tablaFormasPagoNode)

                            ' AÃ±adir las formas de pago
                            Do
                                Dim formaNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
                                montopagado = Decimal.Parse(formaPagoReader("MontoPago").ToString()).ToString("F2")

                                tablaFormasPagoNode.AppendChild(formaNode)
                                Helper.AddElement(xmlDoc, formaNode, "FormaPago", formaPagoReader("FormaPago").ToString())
                                Helper.AddElement(xmlDoc, formaNode, "MontoPago", montopagado)

                            Loop While formaPagoReader.Read()
                        End If
                        formaPagoReader.Close()

                        'Helper.AddElement(xmlDoc, idDocNode, "TotalPaginas", "1")

                        ' Nodo Emisor
                        Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
                        encabezadoNode.AppendChild(emisorNode)
                        Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())
                        ' Verificar si el campo "Municipio" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim municipio As String = facturaReader("Municipio").ToString()

                        If Not String.IsNullOrEmpty(municipio) Then
                            Helper.AddElement(xmlDoc, emisorNode, "Municipio", municipio)
                        End If

                        ' Verificar si el campo "Provincia" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim provincia As String = facturaReader("Provincia").ToString()

                        If Not String.IsNullOrEmpty(provincia) Then
                            Helper.AddElement(xmlDoc, emisorNode, "Provincia", provincia)
                        End If

                        Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode,
                            facturaReader("emp_telefono").ToString(),
                            facturaReader("emp_telefono2").ToString())

                        ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim correoEmisor As String = facturaReader("emp_email").ToString()
                        If Not String.IsNullOrEmpty(correoEmisor) Then
                            Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
                        End If
                        'Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email").ToString())

                        ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim WebSite As String = facturaReader("emp_web").ToString()

                        If Not String.IsNullOrEmpty(WebSite) Then
                            Helper.AddElement(xmlDoc, emisorNode, "WebSite", WebSite)
                        End If

                        ' Verificar si el campo "CodigoVendedor" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim codigoVendedor As String = facturaReader("CodigoVendedor").ToString()

                        If Not String.IsNullOrEmpty(codigoVendedor) Then
                            Helper.AddElement(xmlDoc, emisorNode, "CodigoVendedor", codigoVendedor)
                        End If

                        ' Verificar si el campo "NumeroFacturaInterna" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim numeroFacturaInterna As String = facturaReader("NumeroFacturaInterna").ToString()

                        If Not String.IsNullOrEmpty(numeroFacturaInterna) Then
                            Helper.AddElement(xmlDoc, emisorNode, "NumeroFacturaInterna", numeroFacturaInterna)
                        End If

                        ' Verificar si el campo "ZonaVenta" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim zonaVenta As String = facturaReader("ZonaVenta").ToString()

                        If Not String.IsNullOrEmpty(zonaVenta) Then
                            Helper.AddElement(xmlDoc, emisorNode, "ZonaVenta", zonaVenta)
                        End If

                        Dim fechaEmi As DateTime
                        If Helper.TryGetDate(facturaReader, "fac_fecha", fechaEmi) Then
                            Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", fechaEmi.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                        End If
                        ' Nodo Comprador
                        Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
                        encabezadoNode.AppendChild(compradorNode)
                        Helper.AddElement(xmlDoc, compradorNode, "RNCComprador", facturaReader("cli_rnc").ToString())
                        Helper.AddElement(xmlDoc, compradorNode, "RazonSocialComprador", facturaReader("cli_nombre").ToString())

                        ' Verificar si el campo "ContactoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim contactoComprador As String = facturaReader("cli_contacto").ToString()

                        If Not String.IsNullOrEmpty(contactoComprador) Then
                            Helper.AddElement(xmlDoc, compradorNode, "ContactoComprador", contactoComprador)
                        End If

                        ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim correoComprador As String = facturaReader("cli_email").ToString()

                        If Not String.IsNullOrEmpty(correoComprador) Then
                            Helper.AddElement(xmlDoc, compradorNode, "CorreoComprador", correoComprador)
                        End If

                        ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim direccionComprador As String = facturaReader("cli_direccion").ToString()

                        If Not String.IsNullOrEmpty(direccionComprador) Then
                            Helper.AddElement(xmlDoc, compradorNode, "DireccionComprador", direccionComprador)
                        End If

                        Dim municipioComprador As String = facturaReader("MunicipioComprador").ToString()

                        If Not String.IsNullOrEmpty(municipioComprador) Then
                            Helper.AddElement(xmlDoc, compradorNode, "MunicipioComprador", municipioComprador)
                        End If

                        Dim provinciaComprador As String = facturaReader("ProvinciaComprador").ToString()

                        If Not String.IsNullOrEmpty(provinciaComprador) Then
                            Helper.AddElement(xmlDoc, compradorNode, "ProvinciaComprador", provinciaComprador)
                        End If

                        Dim fTmp As DateTime
                        If Helper.TryGetDate(facturaReader, "FechaEntrega", fTmp) Then
                            Helper.AddElement(xmlDoc, compradorNode, "FechaEntrega", fTmp.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                        End If

                        If Helper.TryGetDate(facturaReader, "FechaOrdenCompra", fTmp) Then
                            Helper.AddElement(xmlDoc, compradorNode, "FechaOrdenCompra", fTmp.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                        End If

                        Dim numeroOrdenCompra As String = facturaReader("NumeroOrdenCompra").ToString()

                        If Not String.IsNullOrEmpty(numeroOrdenCompra) Then
                            Helper.AddElement(xmlDoc, compradorNode, "NumeroOrdenCompra", numeroOrdenCompra)
                        End If

                        Dim codigoInternoComprador As String = facturaReader("CodigoInternoComprador").ToString()

                        If Not String.IsNullOrEmpty(codigoInternoComprador) Then
                            Helper.AddElement(xmlDoc, compradorNode, "CodigoInternoComprador", codigoInternoComprador)
                        End If

                        Dim numeroContenedor As String = facturaReader("numeroContenedor").ToString()

                        If Not String.IsNullOrEmpty(numeroContenedor) Then
                            Helper.AddElement(xmlDoc, compradorNode, "NumeroContenedor", numeroContenedor)
                        End If

                        Dim numeroReferencia As String = facturaReader("numeroReferencia").ToString()

                        If Not String.IsNullOrEmpty(numeroReferencia) Then
                            Helper.AddElement(xmlDoc, compradorNode, "NumeroReferencia", numeroReferencia)
                        End If

                        ' Nodo Totales
                        ''Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
                        encabezadoNode.AppendChild(totalesNode)

                        ' Guardar los valores en variables antes de cerrar el reader
                        '' DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis_dgi").ToString())
                        DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis").ToString())
                        DecItbis = Decimal.Parse(facturaReader("fac_itbis").ToString())
                        itbisTotal = Decimal.Parse(facturaReader("fac_itbis").ToString()).ToString("F2")
                        itbisTotal1 = Decimal.Parse(facturaReader("det_totalitbis_1").ToString()).ToString("F2")
                        itbisTotal2 = Decimal.Parse(facturaReader("det_totalitbis_2").ToString()).ToString("F2")

                        itbis18 = Decimal.Parse(facturaReader("det_itbis_1").ToString()).ToString("F0")
                        itbis16 = Decimal.Parse(facturaReader("det_itbis_2").ToString()).ToString("F0")

                        totalFactura = Decimal.Parse(facturaReader("fac_total").ToString()).ToString("F2")
                        valorpagar = Decimal.Parse(facturaReader("ValorPagar").ToString()).ToString("F2")
                        DecGravado = If(IsDBNull(facturaReader("monto_grabado")), 0D, Convert.ToDecimal(facturaReader("monto_grabado")))

                        montoGravado = DecGravado.ToString("F2")

                        montoGravado1 = If(IsDBNull(facturaReader("monto_grabado1")), 0D, Convert.ToDecimal(facturaReader("monto_grabado1"))).ToString("F2")
                        montoGravado2 = If(IsDBNull(facturaReader("monto_grabado2")), 0D, Convert.ToDecimal(facturaReader("monto_grabado2"))).ToString("F2")


                        If montoGravado <> "0" And montoGravado <> "0.00" Then
                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoTotal", montoGravado)
                        End If


                        If (Not String.IsNullOrEmpty(montoGravado1)) And (Not montoGravado1 = "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI1", montoGravado1)
                        End If

                        If (Not String.IsNullOrEmpty(montoGravado2)) And (Not montoGravado2 = "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI2", montoGravado2)
                        End If

                        ' Verificar si el monto exento no es igual a 0.00 antes de agregar el elementoltw1
                        DecExento = Decimal.Parse(facturaReader("monto_exento").ToString())

                        If (Not String.IsNullOrEmpty(DecExento.ToString("F2"))) And (Not DecExento.ToString("F2") = "0.00") Then
                            montoexento = DecExento.ToString("F2")
                            Helper.AddElement(xmlDoc, totalesNode, "MontoExento", montoexento)
                        End If

                        '' ITBIS1/ITBIS2: si hay TotalITBISn o MontoGravadoIn, incluir tasa (evita omitir ITBIS2 cuando det_itbis_2=0)
                        If (Not String.IsNullOrEmpty(itbisTotal1)) AndAlso itbisTotal1 <> "0.00" Then
                            Dim tasaItbis1 As String = If(String.IsNullOrEmpty(itbis18) OrElse itbis18 = "0", "18", itbis18)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", tasaItbis1)
                        ElseIf (Not String.IsNullOrEmpty(montoGravado1)) AndAlso montoGravado1 <> "0.00" Then
                            Dim tasaItbis1b As String = If(String.IsNullOrEmpty(itbis18) OrElse itbis18 = "0", "18", itbis18)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", tasaItbis1b)
                        End If
                        If (Not String.IsNullOrEmpty(itbisTotal2)) AndAlso itbisTotal2 <> "0.00" Then
                            Dim tasaItbis2 As String = If(String.IsNullOrEmpty(itbis16) OrElse itbis16 = "0", "16", itbis16)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS2", tasaItbis2)
                        ElseIf (Not String.IsNullOrEmpty(montoGravado2)) AndAlso montoGravado2 <> "0.00" Then
                            Dim tasaItbis2b As String = If(String.IsNullOrEmpty(itbis16) OrElse itbis16 = "0", "16", itbis16)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS2", tasaItbis2b)
                        End If

                        If (Not String.IsNullOrEmpty(itbisTotal)) And (itbisTotal <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS", itbisTotal)
                        End If

                        If (Not String.IsNullOrEmpty(itbisTotal1)) And (itbisTotal1 <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS1", itbisTotal1)
                        End If

                        If (Not String.IsNullOrEmpty(itbisTotal2)) And (itbisTotal2 <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS2", itbisTotal2)
                        End If

                        ' Agregar la secciÃ³n de Impuestos Adicionales solo si totalesNode ha sido creado
                        ' RESTBAR: no existe Impuestos_adicionales_dgi para este flujo

                        If totalesNode IsNot Nothing AndAlso Not isResBar Then
                            Dim consultaImpuesto = " SELECT * FROM Impuestos_adicionales_dgi WHERE fac_numero=" & facNumero
                            Dim impuestoReader = EjecutarConsultaReader(cadenaConexion, consultaImpuesto)

                            ' Solo agregar MontoImpuestoAdicional si hay registros en Impuestos_adicionales_dgi
                            If impuestoReader.HasRows Then
                                ' Leer el primer registro para obtener el MontoImpuestoAdicional
                                If impuestoReader.Read() Then
                                    ' Agregar MontoImpuestoAdicional antes del nodo ImpuestosAdicionales
                                    DecMontoImpuestoAdicional = Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional")))
                                    If DecMontoImpuestoAdicional.ToString("F2") <> "0.00" Then
                                        Helper.AddElement(xmlDoc, totalesNode, "MontoImpuestoAdicional", Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional"))).ToString("F2"))
                                    End If
                                End If

                                ' Nodo ImpuestosAdicionales
                                Dim impuestosAdicionalesNode As XmlElement = xmlDoc.CreateElement("ImpuestosAdicionales")
                                totalesNode.AppendChild(impuestosAdicionalesNode)

                                ' Leer cada fila y aÃ±adir los impuestos adicionales
                                Do
                                    ' Para el conjunto de columnas que terminan en _1
                                    If Not IsDBNull(impuestoReader("TipoImpuesto_1")) Then
                                        Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                        impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode1)
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", impuestoReader("TipoImpuesto_1").ToString())
                                        Dim tasaImpuestoAdicional1 As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_1").ToString())

                                        ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                        If tasaImpuestoAdicional1 = Math.Floor(tasaImpuestoAdicional1) Then
                                            ' No tiene decimales, mostramos solo el entero
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F0"))
                                        Else
                                            ' Tiene decimales, mostramos con dos decimales
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar OtrosImpuestosAdicionales
                                        If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_1").ToString()).ToString("F2"))
                                        End If
                                    End If

                                    ' Para el conjunto de columnas que terminan en _2
                                    If Not IsDBNull(impuestoReader("TipoImpuesto_2")) Then
                                        Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                        impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode2)
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", impuestoReader("TipoImpuesto_2").ToString())

                                        Dim tasaImpuestoAdicional As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_2").ToString())

                                        ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                        If tasaImpuestoAdicional = Math.Floor(tasaImpuestoAdicional) Then
                                            ' No tiene decimales, mostramos solo el entero
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F0"))
                                        Else
                                            ' Tiene decimales, mostramos con dos decimales
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar OtrosImpuestosAdicionales
                                        If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_2").ToString()).ToString("F2"))
                                        End If
                                    End If
                                Loop While impuestoReader.Read()
                            End If
                            impuestoReader.Close()
                        End If

                        If (DecItbisDGI > 0) Then
                            DecmontoTotal = DecGravado + DecExento + DecItbisDGI + DecMontoImpuestoAdicional
                        Else
                            DecmontoTotal = DecGravado + DecExento + DecItbis + DecMontoImpuestoAdicional
                        End If

                        Dim montoTotalStr As String = (Math.Floor(CDec(totalFactura) * 100) / 100).ToString("0.00")
                        Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", montoTotalStr)

                        If (Not String.IsNullOrEmpty(valorpagar)) And (valorpagar <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "ValorPagar", valorpagar)
                        End If
                    End If
                    facturaReader.Close()

                    ' Nodo DetallesItems
                    Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
                    root.AppendChild(detallesItemsNode)

                    ' Consulta para obtener la informaciÃ³n de Detalle de Factura
                    Dim consultaDetalle = ConsultaDetalleFacturaXml(emp_codigo, suc_codigo, fac_forma, tfa_codigo, facNumero)
                    Dim detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalle)

                    While detalleReader.Read()
                        ' Nodo Item dentro de DetallesItems
                        Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
                        detallesItemsNode.AppendChild(itemNode)

                        ' AÃ±adir los detalles del item
                        Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", detalleReader("NumeroLinea").ToString())
                        Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", detalleReader("IndicadorFacturacion").ToString())
                        Helper.AddElement(xmlDoc, itemNode, "NombreItem", detalleReader("NombreItem").ToString())
                        Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", detalleReader("IndicadorBienoServicio").ToString())

                        cantidadItem = Decimal.Parse(detalleReader("CantidadItem").ToString()).ToString("F2")
                        unidadMedida = Decimal.Parse(detalleReader("UnidadMedida").ToString()).ToString("F0")

                        gradosalcohol = Decimal.Parse(detalleReader("gradosalcohol").ToString()).ToString("F2")
                        preciounitarioreferencia = Decimal.Parse(detalleReader("preciounitarioreferencia").ToString()).ToString("F2")

                        Helper.AddElement(xmlDoc, itemNode, "CantidadItem", cantidadItem)
                        Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", unidadMedida)

                        If (Not String.IsNullOrEmpty(CStr(detalleReader("cantidadreferencia")))) And (Decimal.Parse(detalleReader("cantidadreferencia").ToString()) <> CDec("0")) Then
                            cantidadReferencia = Decimal.Parse(detalleReader("cantidadreferencia").ToString()).ToString("F0")
                            If (Not String.IsNullOrEmpty(cantidadReferencia)) And (cantidadReferencia <> "0") Then
                                Helper.AddElement(xmlDoc, itemNode, "CantidadReferencia", cantidadReferencia)
                            End If
                        End If

                        If (Not String.IsNullOrEmpty(CStr(detalleReader("unidadreferencia")))) And (Decimal.Parse(detalleReader("unidadreferencia").ToString()) <> CDec("0")) Then
                            UnidadReferencia = Decimal.Parse(detalleReader("unidadreferencia").ToString()).ToString("F0")
                            If (Not String.IsNullOrEmpty(UnidadReferencia)) And (UnidadReferencia <> "0") Then
                                Helper.AddElement(xmlDoc, itemNode, "UnidadReferencia", UnidadReferencia)
                            End If
                        End If

                        ' AÃ±adir la tabla de Subcantidad
                        If Not IsDBNull(detalleReader("Subcantidad")) And Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                            If Not detalleReader("Subcantidad").ToString() = "0.00" And Not detalleReader("Subcantidad").ToString() = "" And (Not detalleReader("CodigoSubcantidad").ToString() = "") Then
                                Dim tablaSubcantidadNode As XmlElement = xmlDoc.CreateElement("TablaSubcantidad")
                                itemNode.AppendChild(tablaSubcantidadNode)

                                Dim subcantidadNode As XmlElement = xmlDoc.CreateElement("SubcantidadItem")
                                tablaSubcantidadNode.AppendChild(subcantidadNode)

                                If Not IsDBNull(detalleReader("Subcantidad")) Then
                                    Helper.AddElement(xmlDoc, subcantidadNode, "Subcantidad", Decimal.Parse(detalleReader("Subcantidad").ToString()).ToString("F3"))
                                End If

                                If Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                                    Helper.AddElement(xmlDoc, subcantidadNode, "CodigoSubcantidad", detalleReader("CodigoSubcantidad").ToString())
                                End If
                            End If
                        End If


                        If (Not String.IsNullOrEmpty(gradosalcohol)) And (gradosalcohol <> "0.00") Then
                            Helper.AddElement(xmlDoc, itemNode, "GradosAlcohol", gradosalcohol)
                        End If

                        If (Not String.IsNullOrEmpty(preciounitarioreferencia)) And (preciounitarioreferencia <> "0.00") Then
                            Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioReferencia", preciounitarioreferencia)
                        End If

                        precioUnitarioItem = Decimal.Parse(detalleReader("PrecioUnitarioItem").ToString()).ToString("F2")
                        Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", precioUnitarioItem)

                        ' AÃ±adir la tabla de impuestos adicionales (TipoImpuesto)
                        If Not IsDBNull(detalleReader("TipoImpuesto1")) And Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                            If Not detalleReader("TipoImpuesto1").ToString() = "" And Not detalleReader("TipoImpuesto2").ToString() = "" Then
                                Dim tablaImpuestoAdicionalNode As XmlElement = xmlDoc.CreateElement("TablaImpuestoAdicional")
                                itemNode.AppendChild(tablaImpuestoAdicionalNode)

                                If Not IsDBNull(detalleReader("TipoImpuesto1")) Then
                                    Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode1)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", detalleReader("TipoImpuesto1").ToString())
                                End If

                                If Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                                    Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode2)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", detalleReader("TipoImpuesto2").ToString())
                                End If
                            End If
                        End If

                        ' Verificar si el DescuentoMonto no es igual a 0.0000 antes de agregar el elemento
                        '  Dim descuentoMonto As String = If(DBNull.Value.Equals(detalleReader("DescuentoMonto")), "0.0000", detalleReader("DescuentoMonto").ToString())
                        Dim descuentoRaw As String = If(detalleReader.IsDBNull(detalleReader.GetOrdinal("DescuentoMonto")), "", detalleReader("DescuentoMonto").ToString())
                        Dim descuentoNorm As String = NormalizeDecimalString(descuentoRaw) ' <-- SIEMPRE "F2" con punto

                        Dim montoDecimal As Decimal

                        ' Verificamos si al menos uno de los subdescuentos es vÃ¡lido antes de crear la tabla

                        ' ===== Validadores =====
                        Dim tipo1 As String = If(IsDBNull(detalleReader("TipoSubDescuento_1")), "", detalleReader("TipoSubDescuento_1").ToString().Trim())
                        Dim tipo2 As String = If(IsDBNull(detalleReader("TipoSubDescuento_2")), "", detalleReader("TipoSubDescuento_2").ToString().Trim())

                        Dim m1 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_1"))
                        Dim m2 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_2"))

                        Dim tipo1Valido As Boolean = (tipo1 = "$" OrElse tipo1 = "%")
                        Dim tipo2Valido As Boolean = (tipo2 = "$" OrElse tipo2 = "%")

                        Dim monto1Valido As Boolean = (m1.HasValue AndAlso m1.Value > 0D)
                        Dim monto2Valido As Boolean = (m2.HasValue AndAlso m2.Value > 0D)

                        Dim tieneSubDescuento1 As Boolean = (tipo1Valido AndAlso monto1Valido)
                        Dim tieneSubDescuento2 As Boolean = (tipo2Valido AndAlso monto2Valido)

                        If descuentoNorm <> "0.00" Then
                            Helper.AddElement(xmlDoc, itemNode, "DescuentoMonto", descuentoNorm)

                            ' Si al menos uno de los subdescuentos es REAL y vÃ¡lido, agregamos la estructura
                            If tieneSubDescuento1 OrElse tieneSubDescuento2 Then
                                ' Crear TablaSubDescuento solo si hay datos vÃ¡lidos
                                Dim subDescuentoNode As XmlElement = xmlDoc.CreateElement("TablaSubDescuento")
                                itemNode.AppendChild(subDescuentoNode)

                                ' Agregar SubDescuento_1
                                If tieneSubDescuento1 Then
                                    Dim subDescuentoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                    subDescuentoNode.AppendChild(subDescuentoDetailNode1)

                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "TipoSubDescuento", tipo1)
                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "MontoSubDescuento",
                          m1.Value.ToString("F2", CultureInfo.InvariantCulture))
                                End If

                                ' Agregar SubDescuento_2
                                If tieneSubDescuento2 Then
                                    Dim subDescuentoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                    subDescuentoNode.AppendChild(subDescuentoDetailNode2)

                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "TipoSubDescuento", tipo2)
                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "MontoSubDescuento",
                          m2.Value.ToString("F2", CultureInfo.InvariantCulture))
                                End If
                            End If
                        End If

                        ' --- Nueva SecciÃ³n para RecargoMonto y TablaSubRecargo ---
                        Dim recargoMonto As String = ""
                        If Not IsDBNull(detalleReader("RecargoMonto")) Then
                            recargoMonto = detalleReader("RecargoMonto").ToString()
                        End If

                        If Not recargoMonto = "0.00" AndAlso Not String.IsNullOrEmpty(recargoMonto) Then
                            ' Agregar elemento RecargoMonto formateado a dos decimales
                            If Decimal.TryParse(recargoMonto, NumberStyles.Any, CultureInfo.InvariantCulture, montoDecimal) Then
                                Helper.AddElement(xmlDoc, itemNode, "RecargoMonto", montoDecimal.ToString("F2", CultureInfo.InvariantCulture))
                            End If

                            ' Validar la existencia de subrecargos (por ejemplo, dos conjuntos)
                            Dim tieneSubRecargo1 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_1")) AndAlso
                                           Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_1").ToString())
                            Dim tieneSubRecargo2 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_2")) AndAlso
                                           Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_2").ToString())

                            If tieneSubRecargo1 OrElse tieneSubRecargo2 Then
                                Dim tablaSubRecargoNode As XmlElement = xmlDoc.CreateElement("TablaSubRecargo")
                                itemNode.AppendChild(tablaSubRecargoNode)

                                ' Agregar SubRecargo 1
                                If tieneSubRecargo1 Then
                                    Dim subRecargoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                                    tablaSubRecargoNode.AppendChild(subRecargoDetailNode1)
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode1, "TipoSubRecargo", detalleReader("TipoSubRecargo_1").ToString())

                                    ' Elemento opcional: SubRecargoPorcentaje
                                    If Not IsDBNull(detalleReader("SubRecargoPorcentaje_1")) AndAlso
                                        Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_1").ToString()) Then
                                        Helper.AddElement(xmlDoc, subRecargoDetailNode1, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_1").ToString())
                                    End If

                                    ' Elemento opcional: MontoSubRecargo
                                    If Not IsDBNull(detalleReader("MontoSubRecargo_1")) AndAlso
                                        Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_1").ToString()) Then
                                        Helper.AddElement(xmlDoc, subRecargoDetailNode1, "MontoSubRecargo", detalleReader("MontoSubRecargo_1").ToString())
                                    End If
                                End If

                                ' Agregar SubRecargo 2
                                If tieneSubRecargo2 Then
                                    Dim subRecargoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                                    tablaSubRecargoNode.AppendChild(subRecargoDetailNode2)
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode2, "TipoSubRecargo", detalleReader("TipoSubRecargo_2").ToString())

                                    If Not IsDBNull(detalleReader("SubRecargoPorcentaje_2")) AndAlso
                                        Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_2").ToString()) Then
                                        Helper.AddElement(xmlDoc, subRecargoDetailNode2, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_2").ToString())
                                    End If

                                    If Not IsDBNull(detalleReader("MontoSubRecargo_2")) AndAlso
                                        Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_2").ToString()) Then
                                        Helper.AddElement(xmlDoc, subRecargoDetailNode2, "MontoSubRecargo", detalleReader("MontoSubRecargo_2").ToString())
                                    End If
                                End If
                            End If
                        End If

                        ' AÃ±adir MontoItem
                        montoItem = Decimal.Parse(detalleReader("MontoItem").ToString()).ToString("F2")
                        Helper.AddElement(xmlDoc, itemNode, "MontoItem", montoItem)

                        ' Actualizar la Ãºltima lÃ­nea del detalle
                        ultimaLineaDetalle = detalleReader("NumeroLinea").ToString()
                    End While
                    detalleReader.Close()

                    ' AÃ±ade esto dentro de la funciÃ³n GenerarXML, justo antes de agregar la FechaHoraFirma

                    ' Consulta la tabla DescuentooRecargo
                    Dim consultaDescuentooRecargo = " SELECT * FROM DescuentooRecargo WHERE fac_numero=" & facNumero
                    Dim descuentoRecargoReader = EjecutarConsultaReader(cadenaConexion, consultaDescuentooRecargo)

                    ' Verifica si hay descuentos o recargos asociados
                    If descuentoRecargoReader.HasRows Then
                        ' Crea el nodo DescuentosORecargos
                        Dim descuentosORecargosNode As XmlElement = xmlDoc.CreateElement("DescuentosORecargos")
                        root.AppendChild(descuentosORecargosNode)

                        ' Itera sobre los registros de la tabla
                        Do While descuentoRecargoReader.Read()
                            ' Crea el nodo DescuentoORecargo para cada registro
                            Dim descuentoORecargoNode As XmlElement = xmlDoc.CreateElement("DescuentoORecargo")
                            descuentosORecargosNode.AppendChild(descuentoORecargoNode)

                            ' AÃ±ade los detalles del descuento o recargo
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "NumeroLinea", descuentoRecargoReader("NumeroLineaDoR").ToString())
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoAjuste", descuentoRecargoReader("TipoAjuste").ToString())

                            '' Opcional: Verifica si el campo estÃ¡ presente antes de agregarlo
                            'If Not IsDBNull(descuentoRecargoReader("IndicadorNorma1007")) Then
                            '    Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorNorma1007", descuentoRecargoReader("IndicadorNorma1007").ToString())
                            'End If

                            If Not IsDBNull(descuentoRecargoReader("DescripcionDescuentooRecargo")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "DescripcionDescuentooRecargo", descuentoRecargoReader("DescripcionDescuentooRecargo").ToString())
                            End If

                            If Not IsDBNull(descuentoRecargoReader("TipoValor")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoValor", descuentoRecargoReader("TipoValor").ToString())
                            End If

                            If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargo")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargo", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargo").ToString()).ToString("F2"))
                            End If

                            'If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargoOtraMoneda")) Then
                            '    Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargoOtraMoneda", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargoOtraMoneda").ToString()).ToString("F2"))
                            'End If

                            If Not IsDBNull(descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorFacturacionDescuentooRecargo", descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo").ToString())
                            End If
                        Loop
                    End If
                    descuentoRecargoReader.Close()

                    ' Nodo FechaHoraFirma fuera del Encabezado y despuÃ©s de Paginacion
                    Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
                    root.AppendChild(fechaHoraFirmaNode)

                    ' Obtener la fecha y hora actual en el formato requerido y agregarla al nodo
                    Dim fechaHoraFirma As String = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
                    fechaHoraFirmaNode.InnerText = fechaHoraFirma
                End If
            End If

        Catch ex As Exception
            Throw New ApplicationException("Error en el proceso de envÃ­o de la factura: " & ex.Message)
        End Try


        ' Retorna el documento XML generado
        Return xmlDoc
    End Function

    Private Shared Function GenerarXML(eNCF As String, cadenaConexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String,
                               ByVal fac_forma As String, ByVal tfa_codigo As String, usu_codigo As String, caja As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()
        ' eNCF debe tener al menos 3 caracteres para Substring(0,3); NCF vacÃ­o/corto desde BD o Delphi provoca ArgumentOutOfRangeException (length).
        Dim eNcfNorm As String = If(eNCF, "").Trim()
        Dim prefijo As String = If(eNcfNorm.Length >= 3, eNcfNorm.Substring(0, 3), eNcfNorm)

        ' Nodo raÃ­z del XML
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")

        ' Nodo Encabezado
        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)

        ' Variables para almacenar los valores del encabezado
        Dim montoGravado As String = ""
        Dim montoGravado1 As String = ""
        Dim montoGravado2 As String = ""
        Dim itbisTotal As String = ""
        Dim totalFactura As String = ""
        Dim valorpagar As String = ""
        Dim montopagado As String = ""
        Dim montoexento As String = ""
        Dim unidadMedida As String = ""
        Dim precioUnitarioItem As String = ""
        Dim ultimaLineaDetalle As String = "1" ' Variable para la Ãºltima lÃ­nea del detalle
        Dim cantidadItem As String = ""
        Dim itbis As String = ""
        Dim itbis18 As String = ""
        Dim itbis16 As String = ""
        Dim itbisTotal1 As String = ""
        Dim itbisTotal2 As String = ""
        Dim montoItem As String = ""
        Dim montoTotal As String = ""
        Dim cantidadReferencia As String = ""
        Dim UnidadReferencia As String = ""
        Dim gradosalcohol As String = ""
        Dim preciounitarioreferencia As String = ""

        Dim DecMontoImpuestoAdicional As Decimal
        Dim DecmontoTotal As Decimal
        Dim DecGravado As Decimal
        Dim DecExento As Decimal
        Dim DecItbis As Decimal
        Dim DecItbisDGI As Decimal
        Dim DecMontoDescuento As Decimal
        'Dim eNCF As String = ""
        Dim consultaFactura As String
        Dim detalleFacForma As String = If(fac_forma, "").Trim()
        Dim detalleTfaCodigo As String = If(tfa_codigo, "").Trim()
        Dim dtSecuencia As New DataTable
        Try
            Helper.RegistrarLogCliente("Entro al GenerarXML ")
            ' Consultar para obtener la informaciÃ³n de Factura
            If isResBar Then
                consultaFactura = ConsultaFacturaXmlResBarSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)
            ElseIf isPOS Then
                consultaFactura = ConsultaFacturaXmlPosSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)

            Else
                consultaFactura = ConsultaFacturaXmlStdSp(emp_codigo, suc_codigo, facNumero, fac_forma, tfa_codigo)

            End If

            Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)

            If facturaReader.Read() Then
                ' AÃ±adir los nodos dentro del Encabezado
                Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

                ' IdDoc
                Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
                encabezadoNode.AppendChild(idDocNode)
                Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeCF").ToString())
                glbTipoeCF = facturaReader("TipoeCF").ToString()

                ' fac_descuento: leer aquÃ­ porque Totales queda dentro de FechasecuenciaReader.Read;
                ' si fn_obtenerFechaSecuenciaDGI no devuelve fila, antes DecMontoDescuento quedaba en 0 y no se generaba DERE (POS/RESBAR).
                Try
                    Dim ordFacDesc As Integer = facturaReader.GetOrdinal("fac_descuento")
                    DecMontoDescuento = If(facturaReader.IsDBNull(ordFacDesc), 0D, Convert.ToDecimal(facturaReader(ordFacDesc)))
                Catch
                    DecMontoDescuento = 0D
                End Try

                If isPOS OrElse isResBar Then
                    Try
                        Dim ordFf0 As Integer = facturaReader.GetOrdinal("fac_forma")
                        If Not facturaReader.IsDBNull(ordFf0) Then
                            Dim sff0 As String = facturaReader.GetValue(ordFf0).ToString()
                            If Not String.IsNullOrWhiteSpace(sff0) Then detalleFacForma = sff0.Trim()
                        End If
                    Catch
                    End Try
                    Try
                        Dim ordTf0 As Integer = facturaReader.GetOrdinal("tfa_codigo")
                        If Not facturaReader.IsDBNull(ordTf0) Then
                            detalleTfaCodigo = facturaReader.GetValue(ordTf0).ToString().Trim()
                        End If
                    Catch
                    End Try
                End If

                'Buscamos la secuencia a usar eNCF
                '  Dim consultaSecuencia = " select [dbo].[fn_obtenerSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as secuencia ")
                'Dim secuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaSecuencia)

                If eNcfNorm.Length > 0 Then
                    glbncfEnvia = eNcfNorm ' secuenciaReader("secuencia").ToString()
                    Helper.AddElement(xmlDoc, idDocNode, "eNCF", glbncfEnvia)

                    Dim consultaFechaSecuencia = " select [dbo].[fn_obtenerFechaSecuenciaDGI]('" & emp_codigo & "','" & facturaReader("TipoeCF").ToString() & "') as Fecha_vence"
                    Dim FechasecuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaFechaSecuencia)
                    Dim consultaFormaPago As String

                    If FechasecuenciaReader.Read() Then

                        Dim fven? As DateTime = SafeDate(FechasecuenciaReader("Fecha_vence"), "Fecha_vence")
                        If fven.HasValue Then
                            Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fven.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                        End If

                        Helper.RegistrarLogCliente("Entro al FechaVencimientoSecuencia ")

                        'Dim fechaVencimiento As String = FechasecuenciaReader("Fecha_vence").ToString()

                        'If Not String.IsNullOrEmpty(fechaVencimiento) Then
                        '    Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fechaVencimiento)
                        'End If

                        Helper.AddElement(xmlDoc, idDocNode, "IndicadorEnvioDiferido", "1")

                        Dim indicadorMonto As String = If(IsDBNull(facturaReader("IndicadorMontoGravado")), "", facturaReader("IndicadorMontoGravado").ToString())

                        If (prefijo <> "E44") Then
                            If Not String.IsNullOrEmpty(indicadorMonto) Then
                                Helper.AddElement(xmlDoc, idDocNode, "IndicadorMontoGravado", indicadorMonto)
                            End If
                        End If

                        Helper.AddElement(xmlDoc, idDocNode, "IndicadorServicioTodoIncluido", "1")

                        Helper.AddElement(xmlDoc, idDocNode, "TipoIngresos", "01")
                        Helper.AddElement(xmlDoc, idDocNode, "TipoPago", facturaReader("TipoPago").ToString())

                        ' Verificar si el campo "condicion_pago" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim terminoPago As String = facturaReader("condicion_pago").ToString()

                        Helper.RegistrarLogCliente("Entro al condicion_pago ")

                        ' Fecha lÃ­mite de pago
                        Dim f? As DateTime = SafeDate(facturaReader("FechaLimitePago"), "FechaLimitePago")
                        If f.HasValue Then
                            Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", f.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                        End If
                        Helper.RegistrarLogCliente("Entro al FechaLimitePago ")
                        'Dim fechaLimitePago As String = facturaReader("FechaLimitePago").ToString()

                        'If Not String.IsNullOrEmpty(fechaLimitePago) Then
                        '    Dim fechaFormateada As String = DateTime.Parse(fechaLimitePago).ToString("dd-MM-yyyy")
                        '    Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", fechaFormateada)
                        'End If

                        If Not String.IsNullOrEmpty(terminoPago) Then
                            Helper.AddElement(xmlDoc, idDocNode, "TerminoPago", terminoPago)
                        End If

                        ' Nodo TablaFormasPago
                        If isResBar Then
                            consultaFormaPago = " SELECT * FROM vwFormasPagoXMLRESBAR WHERE ticket=" & facNumero & " AND usu_codigo='" & usu_codigo & "' AND caja='" & caja & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                        ElseIf isPOS Then
                            consultaFormaPago = " SELECT * FROM vwFormasPagoXMLPOS WHERE ticket=" & facNumero & " AND usu_codigo='" & usu_codigo & "' AND caja='" & caja & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                        Else
                            consultaFormaPago = " SELECT * FROM vwFormasPagoXML WHERE fac_numero=" & facNumero & " AND fac_forma='" & fac_forma & "' AND tfa_codigo='" & tfa_codigo & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                        End If


                        Dim formaPagoReader = EjecutarConsultaReader(cadenaConexion, consultaFormaPago)
                        Helper.RegistrarLogCliente("Entro al formaPagoReader ")
                        If formaPagoReader.Read() Then
                            Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
                            idDocNode.AppendChild(tablaFormasPagoNode)

                            ' AÃ±adir las formas de pago
                            Do
                                Dim formaNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
                                montopagado = Decimal.Parse(formaPagoReader("MontoPago").ToString()).ToString("F2")

                                tablaFormasPagoNode.AppendChild(formaNode)
                                Helper.AddElement(xmlDoc, formaNode, "FormaPago", formaPagoReader("FormaPago").ToString())
                                Helper.AddElement(xmlDoc, formaNode, "MontoPago", montopagado)

                            Loop While formaPagoReader.Read()
                        End If
                        formaPagoReader.Close()

                        'Helper.AddElement(xmlDoc, idDocNode, "TotalPaginas", "1")
                        Helper.RegistrarLogCliente("Fin al formaPagoReader ")
                        ' Nodo Emisor
                        Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
                        encabezadoNode.AppendChild(emisorNode)
                        Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())
                        ' Verificar si el campo "Municipio" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim municipio As String = facturaReader("Municipio").ToString()

                        If Not String.IsNullOrEmpty(municipio) Then
                            Helper.AddElement(xmlDoc, emisorNode, "Municipio", municipio)
                        End If
                        Helper.RegistrarLogCliente("Entro al Municipio ")

                        ' Verificar si el campo "Provincia" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim provincia As String = facturaReader("Provincia").ToString()

                        If Not String.IsNullOrEmpty(provincia) Then
                            Helper.AddElement(xmlDoc, emisorNode, "Provincia", provincia)
                        End If

                        Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode,
                            facturaReader("emp_telefono").ToString(),
                            facturaReader("emp_telefono2").ToString())
                        Helper.RegistrarLogCliente("Entro al TelefonoEmisor ")

                        ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim correoEmisor As String = facturaReader("emp_email").ToString()
                        If Not String.IsNullOrEmpty(correoEmisor) Then
                            Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
                        End If

                        Helper.RegistrarLogCliente("Entro al CorreoEmisor ")
                        'Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email").ToString())

                        ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim WebSite As String = facturaReader("emp_web").ToString()

                        If Not String.IsNullOrEmpty(WebSite) Then
                            Helper.AddElement(xmlDoc, emisorNode, "WebSite", WebSite)
                        End If
                        Helper.RegistrarLogCliente("Entro al WebSite ")
                        ' Verificar si el campo "CodigoVendedor" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim codigoVendedor As String = facturaReader("CodigoVendedor").ToString()

                        If Not String.IsNullOrEmpty(codigoVendedor) Then
                            Helper.AddElement(xmlDoc, emisorNode, "CodigoVendedor", codigoVendedor)
                        End If
                        Helper.RegistrarLogCliente("Entro al CodigoVendedor ")
                        ' Verificar si el campo "NumeroFacturaInterna" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim numeroFacturaInterna As String = facturaReader("NumeroFacturaInterna").ToString()

                        If Not String.IsNullOrEmpty(numeroFacturaInterna) Then
                            Helper.AddElement(xmlDoc, emisorNode, "NumeroFacturaInterna", numeroFacturaInterna)
                        End If
                        Helper.RegistrarLogCliente("Entro al NumeroFacturaInterna ")
                        ' Verificar si el campo "ZonaVenta" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim zonaVenta As String = facturaReader("ZonaVenta").ToString()

                        If Not String.IsNullOrEmpty(zonaVenta) Then
                            Helper.AddElement(xmlDoc, emisorNode, "ZonaVenta", zonaVenta)
                        End If
                        Helper.RegistrarLogCliente("Entro al ZonaVenta ")

                        If Not facturaReader.IsDBNull(facturaReader.GetOrdinal("fac_fecha")) Then
                            Dim fEmi? As DateTime = SafeDate(facturaReader("fac_fecha"), "fac_fecha")
                            If fEmi.HasValue Then
                                Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", fEmi.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                            End If

                        End If
                        Helper.RegistrarLogCliente("Entro al fac_fecha ")
                        'Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

                        Dim tipo As String = facturaReader("TipoeCF").ToString().Trim()
                        Dim total As Decimal = 0D
                        Decimal.TryParse(CStr(facturaReader("fac_total")), NumberStyles.Any, CultureInfo.InvariantCulture, total)
                        Helper.RegistrarLogCliente("Entro al fac_total ")
                        Dim requiereComprador As Boolean = (tipo = "31" Or tipo = "45" Or tipo = "44" Or total > 250000D)
                        If requiereComprador Then

                            ' Nodo Comprador
                            Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
                            encabezadoNode.AppendChild(compradorNode)
                            Helper.AddElement(xmlDoc, compradorNode, "RNCComprador", facturaReader("cli_rnc").ToString())
                            Helper.AddElement(xmlDoc, compradorNode, "RazonSocialComprador", facturaReader("cli_nombre").ToString())

                            Helper.RegistrarLogCliente("Entro al RazonSocialComprador ")

                            ' Verificar si el campo "ContactoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                            Dim contactoComprador As String = facturaReader("cli_contacto").ToString()

                            If Not String.IsNullOrEmpty(contactoComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "ContactoComprador", contactoComprador)
                            End If
                            Helper.RegistrarLogCliente("Entro al ContactoComprador ")
                            ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                            Dim correoComprador As String = facturaReader("cli_email").ToString()

                            If Not String.IsNullOrEmpty(correoComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "CorreoComprador", correoComprador)
                            End If

                            ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                            Dim direccionComprador As String = facturaReader("cli_direccion").ToString()

                            If Not String.IsNullOrEmpty(direccionComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "DireccionComprador", direccionComprador)
                            End If

                            Dim municipioComprador As String = facturaReader("MunicipioComprador").ToString()

                            If Not String.IsNullOrEmpty(municipioComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "MunicipioComprador", municipioComprador)
                            End If

                            Dim provinciaComprador As String = facturaReader("ProvinciaComprador").ToString()

                            If Not String.IsNullOrEmpty(provinciaComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "ProvinciaComprador", provinciaComprador)
                            End If

                            Dim fechaEntrega As String = facturaReader("FechaEntrega").ToString()

                            If Not String.IsNullOrEmpty(fechaEntrega) Then
                                Helper.AddElement(xmlDoc, compradorNode, "FechaEntrega", fechaEntrega)
                            End If

                            Dim fechaOrdenCompra As String = facturaReader("FechaOrdenCompra").ToString()

                            If Not String.IsNullOrEmpty(fechaOrdenCompra) Then
                                Helper.AddElement(xmlDoc, compradorNode, "FechaOrdenCompra", fechaOrdenCompra)
                            End If

                            Dim numeroOrdenCompra As String = facturaReader("NumeroOrdenCompra").ToString()

                            If Not String.IsNullOrEmpty(numeroOrdenCompra) Then
                                Helper.AddElement(xmlDoc, compradorNode, "NumeroOrdenCompra", numeroOrdenCompra)
                            End If

                            Dim codigoInternoComprador As String = facturaReader("CodigoInternoComprador").ToString()

                            If Not String.IsNullOrEmpty(codigoInternoComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "CodigoInternoComprador", codigoInternoComprador)
                            End If

                            Dim numeroContenedor As String = facturaReader("numeroContenedor").ToString()

                            If Not String.IsNullOrEmpty(numeroContenedor) Then
                                Helper.AddElement(xmlDoc, compradorNode, "NumeroContenedor", numeroContenedor)
                            End If

                            Dim numeroReferencia As String = facturaReader("numeroReferencia").ToString()

                            If Not String.IsNullOrEmpty(numeroReferencia) Then
                                Helper.AddElement(xmlDoc, compradorNode, "NumeroReferencia", numeroReferencia)
                            End If
                        End If

                        ' Nodo Totales
                        ''Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
                        encabezadoNode.AppendChild(totalesNode)

                        ' Guardar los valores en variables antes de cerrar el reader
                        '' DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis_dgi").ToString())

                        DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis").ToString())

                        DecItbis = Decimal.Parse(facturaReader("fac_itbis").ToString())
                        itbisTotal = If(IsDBNull(facturaReader("fac_itbis")) OrElse facturaReader("fac_itbis").ToString() = "", "0.00", Decimal.Parse(facturaReader("fac_itbis").ToString()).ToString("F2"))
                        itbisTotal1 = If(IsDBNull(facturaReader("det_totalitbis_1")) OrElse facturaReader("det_totalitbis_1").ToString() = "", "0.00", Decimal.Parse(facturaReader("det_totalitbis_1").ToString()).ToString("F2"))
                        itbisTotal2 = If(IsDBNull(facturaReader("det_totalitbis_2")) OrElse facturaReader("det_totalitbis_2").ToString() = "", "0.00", Decimal.Parse(facturaReader("det_totalitbis_2").ToString()).ToString("F2"))
                        Helper.RegistrarLogCliente("Entro al itbisTotal2 ")
                        itbis18 = Decimal.Parse(facturaReader("det_itbis_1").ToString()).ToString("F0")
                        Helper.RegistrarLogCliente("Entro al det_itbis_1 ")
                        itbis16 = Decimal.Parse(facturaReader("det_itbis_2").ToString()).ToString("F0")
                        Helper.RegistrarLogCliente("Entro al det_itbis_2 ")
                        totalFactura = Decimal.Parse(facturaReader("fac_total").ToString()).ToString("F2")
                        Helper.RegistrarLogCliente("Entro al fac_total ")
                        valorpagar = Decimal.Parse(facturaReader("ValorPagar").ToString()).ToString("F2")
                        Helper.RegistrarLogCliente("Entro al ValorPagar ")
                        DecGravado = If(IsDBNull(facturaReader("monto_grabado")), 0D, Convert.ToDecimal(facturaReader("monto_grabado")))
                        Helper.RegistrarLogCliente("Entro al monto_grabado ")
                        DecMontoDescuento = If(IsDBNull(facturaReader("fac_descuento")), 0D, Convert.ToDecimal(facturaReader("fac_descuento")))
                        Helper.RegistrarLogCliente("Entro al fac_descuento ")
                        montoGravado = DecGravado.ToString("F2")

                        montoGravado1 = If(IsDBNull(facturaReader("monto_grabado1")), 0D, Convert.ToDecimal(facturaReader("monto_grabado1"))).ToString("F2")

                        montoGravado2 = If(IsDBNull(facturaReader("monto_grabado2")), 0D, Convert.ToDecimal(facturaReader("monto_grabado2"))).ToString("F2")


                        If montoGravado <> "0" And montoGravado <> "0.00" Then
                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoTotal", montoGravado)
                        End If

                        If (Not String.IsNullOrEmpty(montoGravado1)) And (Not montoGravado1 = "0.00") Then

                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI1", montoGravado1)
                        End If

                        If (Not String.IsNullOrEmpty(montoGravado2)) And (Not montoGravado2 = "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI2", montoGravado2)
                        End If
                        ' Verificar si el monto exento no es igual a 0.00 antes de agregar el elementoltw1
                        DecExento = Decimal.Parse(facturaReader("monto_exento").ToString())

                        If (Not String.IsNullOrEmpty(DecExento.ToString("F2"))) And (Not DecExento.ToString("F2") = "0.00") Then
                            montoexento = DecExento.ToString("F2")
                            Helper.AddElement(xmlDoc, totalesNode, "MontoExento", montoexento)
                        End If
                        '' ITBIS1/ITBIS2: alineado a totales estÃ¡ndar (no depender de DecItbis ni de det_itbis_*=0)
                        If (Not String.IsNullOrEmpty(itbisTotal1)) AndAlso itbisTotal1 <> "0.00" Then
                            Dim tasaItbis1 As String = If(String.IsNullOrEmpty(itbis18) OrElse itbis18 = "0", "18", itbis18)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", tasaItbis1)
                        ElseIf (Not String.IsNullOrEmpty(montoGravado1)) AndAlso montoGravado1 <> "0.00" Then
                            Dim tasaItbis1b As String = If(String.IsNullOrEmpty(itbis18) OrElse itbis18 = "0", "18", itbis18)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", tasaItbis1b)
                        End If
                        If (Not String.IsNullOrEmpty(itbisTotal2)) AndAlso itbisTotal2 <> "0.00" Then
                            Dim tasaItbis2 As String = If(String.IsNullOrEmpty(itbis16) OrElse itbis16 = "0", "16", itbis16)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS2", tasaItbis2)
                        ElseIf (Not String.IsNullOrEmpty(montoGravado2)) AndAlso montoGravado2 <> "0.00" Then
                            Dim tasaItbis2b As String = If(String.IsNullOrEmpty(itbis16) OrElse itbis16 = "0", "16", itbis16)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS2", tasaItbis2b)
                        End If

                        If (Not String.IsNullOrEmpty(itbisTotal)) And (itbisTotal <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS", itbisTotal)
                        End If
                        Helper.RegistrarLogCliente("Entro al TotalITBIS ")
                        If (Not String.IsNullOrEmpty(itbisTotal1)) And (itbisTotal1 <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS1", itbisTotal1)
                        End If

                        If (Not String.IsNullOrEmpty(itbisTotal2)) And (itbisTotal2 <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS2", itbisTotal2)
                        End If

                        ' Agregar la secciÃ³n de Impuestos Adicionales solo si totalesNode ha sido creado
                        ' RESTBAR: no existe Impuestos_adicionales_dgi para este flujo

                        If totalesNode IsNot Nothing AndAlso Not isResBar Then
                            Dim consultaImpuesto = " SELECT * FROM Impuestos_adicionales_dgi WHERE fac_numero=" & facNumero
                            Dim impuestoReader = EjecutarConsultaReader(cadenaConexion, consultaImpuesto)

                            ' Solo agregar MontoImpuestoAdicional si hay registros en Impuestos_adicionales_dgi
                            If impuestoReader.HasRows Then
                                ' Leer el primer registro para obtener el MontoImpuestoAdicional
                                If impuestoReader.Read() Then
                                    ' Agregar MontoImpuestoAdicional antes del nodo ImpuestosAdicionales
                                    DecMontoImpuestoAdicional = Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional")))
                                    If DecMontoImpuestoAdicional.ToString("F2") <> "0.00" Then
                                        Helper.AddElement(xmlDoc, totalesNode, "MontoImpuestoAdicional", Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional"))).ToString("F2"))
                                    End If
                                End If

                                ' Nodo ImpuestosAdicionales
                                Dim impuestosAdicionalesNode As XmlElement = xmlDoc.CreateElement("ImpuestosAdicionales")
                                totalesNode.AppendChild(impuestosAdicionalesNode)

                                ' Leer cada fila y aÃ±adir los impuestos adicionales
                                Do
                                    ' Para el conjunto de columnas que terminan en _1
                                    If Not IsDBNull(impuestoReader("TipoImpuesto_1")) Then
                                        Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                        impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode1)
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", impuestoReader("TipoImpuesto_1").ToString())
                                        Dim tasaImpuestoAdicional1 As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_1").ToString())

                                        ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                        If tasaImpuestoAdicional1 = Math.Floor(tasaImpuestoAdicional1) Then
                                            ' No tiene decimales, mostramos solo el entero
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F0"))
                                        Else
                                            ' Tiene decimales, mostramos con dos decimales
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar OtrosImpuestosAdicionales
                                        If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_1").ToString()).ToString("F2"))
                                        End If
                                    End If

                                    ' Para el conjunto de columnas que terminan en _2
                                    If Not IsDBNull(impuestoReader("TipoImpuesto_2")) Then
                                        Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                        impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode2)
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", impuestoReader("TipoImpuesto_2").ToString())

                                        Dim tasaImpuestoAdicional As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_2").ToString())

                                        ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                        If tasaImpuestoAdicional = Math.Floor(tasaImpuestoAdicional) Then
                                            ' No tiene decimales, mostramos solo el entero
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F0"))
                                        Else
                                            ' Tiene decimales, mostramos con dos decimales
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar OtrosImpuestosAdicionales
                                        If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_2").ToString()).ToString("F2"))
                                        End If
                                    End If
                                Loop While impuestoReader.Read()
                            End If
                            impuestoReader.Close()
                        End If

                        If (DecItbisDGI > 0) Then
                            DecmontoTotal = DecGravado + DecExento + DecItbisDGI + DecMontoImpuestoAdicional
                        Else
                            DecmontoTotal = DecGravado + DecExento + DecItbis + DecMontoImpuestoAdicional
                        End If

                        Dim montoTotalStr As String = (Math.Floor(CDec(totalFactura) * 100) / 100).ToString("0.00")
                        Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", montoTotalStr)

                        If (Not String.IsNullOrEmpty(valorpagar)) And (valorpagar <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "ValorPagar", valorpagar)
                        End If
                    End If
                    facturaReader.Close()

                    ' Nodo DetallesItems
                    Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
                    Dim consultaDetalle As String
                    root.AppendChild(detallesItemsNode)

                    ' Detalle: SP estÃ¡ndar. POS/RESBAR (p. ej. resumen RFCE con ticket): si no hay forma/tfa o el SP no devuelve lÃ­neas, usar vista de detalle del ticket.
                    Dim uEsc As String = (usu_codigo & "").Replace("'", "''")
                    Dim cjEsc As String = (caja & "").Replace("'", "''")
                    Dim detalleReader As SqlDataReader
                    If (isPOS OrElse isResBar) AndAlso (String.IsNullOrWhiteSpace(detalleFacForma) OrElse String.IsNullOrWhiteSpace(detalleTfaCodigo)) Then
                        If isResBar Then
                            consultaDetalle = ConsultaFacturasDetalleXmlResBarSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)
                        Else
                            consultaDetalle = ConsultaFacturasDetalleXmlPosSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)
                        End If
                        detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalle)
                    Else
                        consultaDetalle = ConsultaDetalleFacturaXml(emp_codigo, suc_codigo, detalleFacForma, detalleTfaCodigo, facNumero)
                        detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalle)
                        If (isPOS OrElse isResBar) AndAlso Not detalleReader.HasRows Then
                            detalleReader.Close()
                            If isResBar Then
                                consultaDetalle = ConsultaFacturasDetalleXmlResBarSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)
                            Else
                                consultaDetalle = ConsultaFacturasDetalleXmlPosSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)
                            End If
                            detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalle)
                        End If
                    End If

                    While detalleReader.Read()
                        ' Nodo Item dentro de DetallesItems
                        Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
                        detallesItemsNode.AppendChild(itemNode)

                        ' AÃ±adir los detalles del item
                        Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", detalleReader("NumeroLinea").ToString())
                        If (prefijo <> "E44") Then
                            Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", detalleReader("IndicadorFacturacion").ToString())
                        Else
                            Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", "4")
                        End If

                        'Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", detalleReader("IndicadorFacturacion").ToString())
                        Helper.AddElement(xmlDoc, itemNode, "NombreItem", detalleReader("NombreItem").ToString())
                        Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", detalleReader("IndicadorBienoServicio").ToString())

                        cantidadItem = Decimal.Parse(detalleReader("CantidadItem").ToString()).ToString("F2")
                        unidadMedida = Decimal.Parse(detalleReader("UnidadMedida").ToString()).ToString("F0")

                        gradosalcohol = Decimal.Parse(detalleReader("gradosalcohol").ToString()).ToString("F2")
                        preciounitarioreferencia = Decimal.Parse(detalleReader("preciounitarioreferencia").ToString()).ToString("F2")

                        Helper.AddElement(xmlDoc, itemNode, "CantidadItem", cantidadItem)
                        Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", unidadMedida)

                        If (Not String.IsNullOrEmpty(CStr(detalleReader("cantidadreferencia")))) And (Decimal.Parse(detalleReader("cantidadreferencia").ToString()) <> CDec("0")) Then
                            cantidadReferencia = Decimal.Parse(detalleReader("cantidadreferencia").ToString()).ToString("F0")
                            If (Not String.IsNullOrEmpty(cantidadReferencia)) And (cantidadReferencia <> "0") Then
                                Helper.AddElement(xmlDoc, itemNode, "CantidadReferencia", cantidadReferencia)
                            End If
                        End If

                        If (Not String.IsNullOrEmpty(CStr(detalleReader("unidadreferencia")))) And (Decimal.Parse(detalleReader("unidadreferencia").ToString()) <> CDec("0")) Then
                            UnidadReferencia = Decimal.Parse(detalleReader("unidadreferencia").ToString()).ToString("F0")
                            If (Not String.IsNullOrEmpty(UnidadReferencia)) And (UnidadReferencia <> "0") Then
                                Helper.AddElement(xmlDoc, itemNode, "UnidadReferencia", UnidadReferencia)
                            End If
                        End If

                        ' AÃ±adir la tabla de Subcantidad
                        If Not IsDBNull(detalleReader("Subcantidad")) And Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                            If Not detalleReader("Subcantidad").ToString() = "0.00" And Not detalleReader("Subcantidad").ToString() = "" And (Not detalleReader("CodigoSubcantidad").ToString() = "") Then
                                Dim tablaSubcantidadNode As XmlElement = xmlDoc.CreateElement("TablaSubcantidad")
                                itemNode.AppendChild(tablaSubcantidadNode)

                                Dim subcantidadNode As XmlElement = xmlDoc.CreateElement("SubcantidadItem")
                                tablaSubcantidadNode.AppendChild(subcantidadNode)

                                If Not IsDBNull(detalleReader("Subcantidad")) Then
                                    Helper.AddElement(xmlDoc, subcantidadNode, "Subcantidad", Decimal.Parse(detalleReader("Subcantidad").ToString()).ToString("F3"))
                                End If

                                If Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                                    Helper.AddElement(xmlDoc, subcantidadNode, "CodigoSubcantidad", detalleReader("CodigoSubcantidad").ToString())
                                End If
                            End If
                        End If


                        If (Not String.IsNullOrEmpty(gradosalcohol)) And (gradosalcohol <> "0.00") Then
                            Helper.AddElement(xmlDoc, itemNode, "GradosAlcohol", gradosalcohol)
                        End If

                        If (Not String.IsNullOrEmpty(preciounitarioreferencia)) And (preciounitarioreferencia <> "0.00") Then
                            Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioReferencia", preciounitarioreferencia)
                        End If

                        precioUnitarioItem = Decimal.Parse(detalleReader("PrecioUnitarioItem").ToString()).ToString("F2")
                        Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", precioUnitarioItem)

                        ' AÃ±adir la tabla de impuestos adicionales (TipoImpuesto)
                        If Not IsDBNull(detalleReader("TipoImpuesto1")) And Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                            If Not detalleReader("TipoImpuesto1").ToString() = "" And Not detalleReader("TipoImpuesto2").ToString() = "" Then
                                Dim tablaImpuestoAdicionalNode As XmlElement = xmlDoc.CreateElement("TablaImpuestoAdicional")
                                itemNode.AppendChild(tablaImpuestoAdicionalNode)

                                If Not IsDBNull(detalleReader("TipoImpuesto1")) Then
                                    Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode1)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", detalleReader("TipoImpuesto1").ToString())
                                End If

                                If Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                                    Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode2)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", detalleReader("TipoImpuesto2").ToString())
                                End If
                            End If
                        End If

                        ' Verificar si el DescuentoMonto no es igual a 0.0000 antes de agregar el elemento
                        '  Dim descuentoMonto As String = If(DBNull.Value.Equals(detalleReader("DescuentoMonto")), "0.0000", detalleReader("DescuentoMonto").ToString())
                        Dim descuentoRaw As String = If(detalleReader.IsDBNull(detalleReader.GetOrdinal("DescuentoMonto")), "", detalleReader("DescuentoMonto").ToString())
                        Dim descuentoNorm As String = NormalizeDecimalString(descuentoRaw) ' <-- SIEMPRE "F2" con punto

                        Dim montoDecimal As Decimal

                        ' Verificamos si al menos uno de los subdescuentos es vÃ¡lido antes de crear la tabla

                        ' ===== Validadores =====
                        Dim tipo1 As String = If(IsDBNull(detalleReader("TipoSubDescuento_1")), "", detalleReader("TipoSubDescuento_1").ToString().Trim())
                        Dim tipo2 As String = If(IsDBNull(detalleReader("TipoSubDescuento_2")), "", detalleReader("TipoSubDescuento_2").ToString().Trim())

                        Dim m1 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_1"))
                        Dim m2 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_2"))

                        Dim tipo1Valido As Boolean = (tipo1 = "$" OrElse tipo1 = "%")
                        Dim tipo2Valido As Boolean = (tipo2 = "$" OrElse tipo2 = "%")

                        Dim monto1Valido As Boolean = (m1.HasValue AndAlso m1.Value > 0D)
                        Dim monto2Valido As Boolean = (m2.HasValue AndAlso m2.Value > 0D)

                        Dim tieneSubDescuento1 As Boolean = (tipo1Valido AndAlso monto1Valido)
                        Dim tieneSubDescuento2 As Boolean = (tipo2Valido AndAlso monto2Valido)

                        If descuentoNorm <> "0.00" Then
                            Helper.AddElement(xmlDoc, itemNode, "DescuentoMonto", descuentoNorm)

                            ' Si al menos uno de los subdescuentos es REAL y vÃ¡lido, agregamos la estructura
                            If tieneSubDescuento1 OrElse tieneSubDescuento2 Then
                                ' Crear TablaSubDescuento solo si hay datos vÃ¡lidos
                                Dim subDescuentoNode As XmlElement = xmlDoc.CreateElement("TablaSubDescuento")
                                itemNode.AppendChild(subDescuentoNode)

                                ' Agregar SubDescuento_1
                                If tieneSubDescuento1 Then
                                    Dim subDescuentoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                    subDescuentoNode.AppendChild(subDescuentoDetailNode1)

                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "TipoSubDescuento", tipo1)
                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "MontoSubDescuento",
                          m1.Value.ToString("F2", CultureInfo.InvariantCulture))
                                End If

                                ' Agregar SubDescuento_2
                                If tieneSubDescuento2 Then
                                    Dim subDescuentoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                    subDescuentoNode.AppendChild(subDescuentoDetailNode2)

                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "TipoSubDescuento", tipo2)
                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "MontoSubDescuento",
                          m2.Value.ToString("F2", CultureInfo.InvariantCulture))
                                End If
                            End If
                        End If

                        ' --- Nueva SecciÃ³n para RecargoMonto y TablaSubRecargo ---
                        Dim recargoMonto As String = ""
                        If Not IsDBNull(detalleReader("RecargoMonto")) Then
                            recargoMonto = detalleReader("RecargoMonto").ToString()
                        End If

                        If Not recargoMonto = "0.00" AndAlso Not String.IsNullOrEmpty(recargoMonto) AndAlso Not recargoMonto = "0" Then
                            ' Agregar elemento RecargoMonto formateado a dos decimales
                            Helper.AddElement(xmlDoc, itemNode, "RecargoMonto", recargoMonto)

                            Dim tablaSubRecargoNode As XmlElement = xmlDoc.CreateElement("TablaSubRecargo")
                            itemNode.AppendChild(tablaSubRecargoNode)

                            Dim subRecargoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                            tablaSubRecargoNode.AppendChild(subRecargoDetailNode1)
                            Helper.AddElement(xmlDoc, subRecargoDetailNode1, "TipoSubRecargo", "$")
                            Helper.AddElement(xmlDoc, subRecargoDetailNode1, "MontoSubRecargo", recargoMonto)


                            '' Validar la existencia de subrecargos (por ejemplo, dos conjuntos)
                            'Dim tieneSubRecargo1 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_1")) AndAlso
                            '           Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_1").ToString())
                            'Dim tieneSubRecargo2 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_2")) AndAlso
                            '           Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_2").ToString())

                            'If tieneSubRecargo1 OrElse tieneSubRecargo2 Then
                            '    Dim tablaSubRecargoNode As XmlElement = xmlDoc.CreateElement("TablaSubRecargo")
                            '    itemNode.AppendChild(tablaSubRecargoNode)

                            '    ' Agregar SubRecargo 1
                            '    If tieneSubRecargo1 Then
                            '        Dim subRecargoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                            '        tablaSubRecargoNode.AppendChild(subRecargoDetailNode1)
                            '        Helper.AddElement(xmlDoc, subRecargoDetailNode1, "TipoSubRecargo", detalleReader("TipoSubRecargo_1").ToString())

                            '        ' Elemento opcional: SubRecargoPorcentaje
                            '        If Not IsDBNull(detalleReader("SubRecargoPorcentaje_1")) AndAlso
                            '        Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_1").ToString()) Then
                            '            Helper.AddElement(xmlDoc, subRecargoDetailNode1, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_1").ToString())
                            '        End If

                            '        ' Elemento opcional: MontoSubRecargo
                            '        If Not IsDBNull(detalleReader("MontoSubRecargo_1")) AndAlso
                            '        Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_1").ToString()) Then
                            '            Helper.AddElement(xmlDoc, subRecargoDetailNode1, "MontoSubRecargo", detalleReader("MontoSubRecargo_1").ToString())
                            '        End If
                            '    End If

                            '    ' Agregar SubRecargo 2
                            '    If tieneSubRecargo2 Then
                            '        Dim subRecargoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                            '        tablaSubRecargoNode.AppendChild(subRecargoDetailNode2)
                            '        Helper.AddElement(xmlDoc, subRecargoDetailNode2, "TipoSubRecargo", detalleReader("TipoSubRecargo_2").ToString())

                            '        If Not IsDBNull(detalleReader("SubRecargoPorcentaje_2")) AndAlso
                            '        Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_2").ToString()) Then
                            '            Helper.AddElement(xmlDoc, subRecargoDetailNode2, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_2").ToString())
                            '        End If

                            '        If Not IsDBNull(detalleReader("MontoSubRecargo_2")) AndAlso
                            '        Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_2").ToString()) Then
                            '            Helper.AddElement(xmlDoc, subRecargoDetailNode2, "MontoSubRecargo", detalleReader("MontoSubRecargo_2").ToString())
                            '        End If
                            '    End If
                            'End If


                        End If

                        ' AÃ±adir MontoItem
                        montoItem = Decimal.Parse(detalleReader("MontoItem").ToString()).ToString("F2")
                        Helper.AddElement(xmlDoc, itemNode, "MontoItem", montoItem)

                        ' Actualizar la Ãºltima lÃ­nea del detalle
                        ultimaLineaDetalle = detalleReader("NumeroLinea").ToString()
                    End While
                    detalleReader.Close()

                    ' Descuento desde vista POS/RESBAR (fac_descuento). Incluye detecciÃ³n por consulta por si isPOS/isResBar no reflejara el flujo real.
                    Dim descuentoDesdeVistaPosRes As Boolean = isPOS OrElse isResBar OrElse
                        consultaFactura.IndexOf("vwFacturasXMLPOS", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                        consultaFactura.IndexOf("spFacturasXMLPOSGet", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                        consultaFactura.IndexOf("vwFacturasXMLRESBAR", StringComparison.OrdinalIgnoreCase) >= 0
                    If descuentoDesdeVistaPosRes AndAlso DecMontoDescuento > 0D Then
                        Dim descuentosORecargosNode As XmlElement = xmlDoc.CreateElement("DescuentosORecargos")
                        root.AppendChild(descuentosORecargosNode)
                        Dim descuentoORecargoNode As XmlElement = xmlDoc.CreateElement("DescuentoORecargo")
                        descuentosORecargosNode.AppendChild(descuentoORecargoNode)

                        Dim montoDr As String = DecMontoDescuento.ToString("F2", CultureInfo.InvariantCulture)
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "NumeroLinea", "1")
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoAjuste", "D")
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "DescripcionDescuentooRecargo", "Descuento")
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoValor", "$")
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "ValorDescuentooRecargo", montoDr)
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargo", montoDr)
                        Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorFacturacionDescuentooRecargo", "4")
                    Else
                        Dim consultaDescuentooRecargo = " SELECT * FROM DescuentooRecargo WHERE fac_numero=" & facNumero & " AND fac_forma='" & detalleFacForma.Replace("'", "''") & "' AND emp_codigo=" & emp_codigo & " AND tfa_codigo=" & detalleTfaCodigo & " AND suc_codigo=" & suc_codigo

                        Dim descuentoRecargoReader = EjecutarConsultaReader(cadenaConexion, consultaDescuentooRecargo)

                        If descuentoRecargoReader.HasRows Then
                            Dim descuentosORecargosNode As XmlElement = xmlDoc.CreateElement("DescuentosORecargos")
                            root.AppendChild(descuentosORecargosNode)

                            Do While descuentoRecargoReader.Read()
                                Dim descuentoORecargoNode As XmlElement = xmlDoc.CreateElement("DescuentoORecargo")
                                descuentosORecargosNode.AppendChild(descuentoORecargoNode)

                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "NumeroLinea", descuentoRecargoReader("NumeroLineaDoR").ToString())
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoAjuste", descuentoRecargoReader("TipoAjuste").ToString())

                                If Not IsDBNull(descuentoRecargoReader("DescripcionDescuentooRecargo")) Then
                                    Helper.AddElement(xmlDoc, descuentoORecargoNode, "DescripcionDescuentooRecargo", descuentoRecargoReader("DescripcionDescuentooRecargo").ToString())
                                End If

                                If Not IsDBNull(descuentoRecargoReader("TipoValor")) Then
                                    Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoValor", descuentoRecargoReader("TipoValor").ToString())
                                End If
                                If Not IsDBNull(descuentoRecargoReader("ValorDescuentooRecargo")) Then
                                    Helper.AddElement(xmlDoc, descuentoORecargoNode, "ValorDescuentooRecargo", Decimal.Parse(descuentoRecargoReader("ValorDescuentooRecargo").ToString()).ToString("F2"))
                                End If
                                If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargo")) Then
                                    Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargo", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargo").ToString()).ToString("F2"))
                                End If

                                If Not IsDBNull(descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo")) Then
                                    Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorFacturacionDescuentooRecargo", descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo").ToString())
                                End If
                            Loop
                        End If
                        descuentoRecargoReader.Close()
                    End If
                    ' Nodo FechaHoraFirma fuera del Encabezado y despuÃ©s de Paginacion
                    Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
                    root.AppendChild(fechaHoraFirmaNode)

                    ' Obtener la fecha y hora actual en el formato requerido y agregarla al nodo
                    Dim fechaHoraFirma As String = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
                    fechaHoraFirmaNode.InnerText = fechaHoraFirma
                End If
            End If
        Catch ex As Exception
            Throw New ApplicationException("Error en el proceso de envÃ­o de la factura: " & ex.Message)
        End Try
        ' Retorna el documento XML generado
        Return xmlDoc
    End Function

    Private Shared Function GenerarXMLE45Test(eNCF As String, cadenaConexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String,
                               ByVal fac_forma As String, ByVal tfa_codigo As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()
        Dim eNcfNorm As String = If(eNCF, "").Trim()
        Dim prefijo As String = If(eNcfNorm.Length >= 3, eNcfNorm.Substring(0, 3), eNcfNorm)

        ' Nodo raÃ­z del XML
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")

        ' Nodo Encabezado
        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)

        ' Variables para almacenar los valores del encabezado
        Dim montoGravado As String = ""
        Dim montoGravado1 As String = ""
        Dim montoGravado2 As String = ""
        Dim itbisTotal As String = ""
        Dim totalFactura As String = ""
        Dim valorpagar As String = ""
        Dim montopagado As String = ""
        Dim montoexento As String = ""
        Dim unidadMedida As String = ""
        Dim precioUnitarioItem As String = ""
        Dim ultimaLineaDetalle As String = "1" ' Variable para la Ãºltima lÃ­nea del detalle
        Dim cantidadItem As String = ""
        Dim itbis As String = ""
        Dim itbis18 As String = ""
        Dim itbis16 As String = ""
        Dim itbisTotal1 As String = ""
        Dim itbisTotal2 As String = ""
        Dim montoItem As String = ""
        Dim montoTotal As String = ""
        Dim cantidadReferencia As String = ""
        Dim UnidadReferencia As String = ""
        Dim gradosalcohol As String = ""
        Dim preciounitarioreferencia As String = ""

        Dim DecMontoImpuestoAdicional As Decimal
        Dim DecmontoTotal As Decimal
        Dim DecGravado As Decimal
        Dim DecExento As Decimal
        Dim DecItbis As Decimal
        Dim DecItbisDGI As Decimal
        Dim DecMontoDescuento As Decimal
        'Dim eNCF As String = ""
        Dim dtSecuencia As New DataTable
        Try

            ' Consultar para obtener la informaciÃ³n de Factura
            Dim consultaFactura As String = ConsultaFacturaXmlStdSp(emp_codigo, suc_codigo, facNumero, fac_forma, tfa_codigo)
            Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)

            If facturaReader.Read() Then
                ' AÃ±adir los nodos dentro del Encabezado
                Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

                ' IdDoc
                Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
                encabezadoNode.AppendChild(idDocNode)
                Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeCF").ToString())
                glbTipoeCF = facturaReader("TipoeCF").ToString()

                'Buscamos la secuencia a usar eNCF
                '  Dim consultaSecuencia = " select [dbo].[fn_obtenerSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as secuencia ")
                'Dim secuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaSecuencia)

                If eNCF.Trim.Length() > 0 Then
                    glbncfEnvia = eNCF ' secuenciaReader("secuencia").ToString()
                    Helper.AddElement(xmlDoc, idDocNode, "eNCF", glbncfEnvia)

                    Dim consultaFechaSecuencia = " select [dbo].[fn_obtenerFechaSecuenciaDGI]('" & emp_codigo & "','" & facturaReader("TipoeCF").ToString() & "') as Fecha_vence"
                    Dim FechasecuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaFechaSecuencia)

                    If FechasecuenciaReader.Read() Then

                        Dim fven? As DateTime = SafeDate(FechasecuenciaReader("Fecha_vence"), "Fecha_vence")
                        If fven.HasValue Then
                            Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fven.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                        End If

                        Helper.AddElement(xmlDoc, idDocNode, "IndicadorEnvioDiferido", "1")

                        Dim indicadorMonto As String = If(IsDBNull(facturaReader("IndicadorMontoGravado")), "", facturaReader("IndicadorMontoGravado").ToString())

                        If (prefijo <> "E44") Then
                            If Not String.IsNullOrEmpty(indicadorMonto) Then
                                Helper.AddElement(xmlDoc, idDocNode, "IndicadorMontoGravado", indicadorMonto)
                            End If
                        End If

                        Helper.AddElement(xmlDoc, idDocNode, "IndicadorServicioTodoIncluido", "1")

                        Helper.AddElement(xmlDoc, idDocNode, "TipoIngresos", "01")
                        Helper.AddElement(xmlDoc, idDocNode, "TipoPago", facturaReader("TipoPago").ToString())

                        ' Verificar si el campo "condicion_pago" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim terminoPago As String = facturaReader("condicion_pago").ToString()
                        Dim fechaLimitePago As String = facturaReader("FechaLimitePago").ToString()

                        If Not String.IsNullOrEmpty(fechaLimitePago) Then
                            Dim fechaFormateada As String = DateTime.Parse(fechaLimitePago).ToString("dd-MM-yyyy")
                            Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", fechaFormateada)
                        End If

                        If Not String.IsNullOrEmpty(terminoPago) Then
                            Helper.AddElement(xmlDoc, idDocNode, "TerminoPago", terminoPago)
                        End If


                        ' Nodo TablaFormasPago
                        Dim consultaFormaPago = " SELECT * FROM vwFormasPagoXML WHERE fac_numero=" & facNumero & " AND fac_forma='" & fac_forma & "' AND tfa_codigo='" & tfa_codigo & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                        Dim formaPagoReader = EjecutarConsultaReader(cadenaConexion, consultaFormaPago)

                        If formaPagoReader.Read() Then
                            Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
                            idDocNode.AppendChild(tablaFormasPagoNode)

                            ' AÃ±adir las formas de pago
                            Do
                                Dim formaNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
                                montopagado = Decimal.Parse(formaPagoReader("MontoPago").ToString()).ToString("F2")

                                tablaFormasPagoNode.AppendChild(formaNode)
                                Helper.AddElement(xmlDoc, formaNode, "FormaPago", formaPagoReader("FormaPago").ToString())
                                Helper.AddElement(xmlDoc, formaNode, "MontoPago", montopagado)

                            Loop While formaPagoReader.Read()
                        End If
                        formaPagoReader.Close()

                        'Helper.AddElement(xmlDoc, idDocNode, "TotalPaginas", "1")

                        ' Nodo Emisor
                        Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
                        encabezadoNode.AppendChild(emisorNode)
                        Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
                        Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())
                        ' Verificar si el campo "Municipio" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim municipio As String = facturaReader("Municipio").ToString()

                        If Not String.IsNullOrEmpty(municipio) Then
                            Helper.AddElement(xmlDoc, emisorNode, "Municipio", municipio)
                        End If

                        ' Verificar si el campo "Provincia" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim provincia As String = facturaReader("Provincia").ToString()

                        If Not String.IsNullOrEmpty(provincia) Then
                            Helper.AddElement(xmlDoc, emisorNode, "Provincia", provincia)
                        End If

                        Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode,
                            facturaReader("emp_telefono").ToString(),
                            facturaReader("emp_telefono2").ToString())

                        ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim correoEmisor As String = facturaReader("emp_email").ToString()
                        If Not String.IsNullOrEmpty(correoEmisor) Then
                            Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
                        End If

                        'Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email").ToString())

                        ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim WebSite As String = facturaReader("emp_web").ToString()

                        If Not String.IsNullOrEmpty(WebSite) Then
                            Helper.AddElement(xmlDoc, emisorNode, "WebSite", WebSite)
                        End If

                        ' Verificar si el campo "CodigoVendedor" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim codigoVendedor As String = facturaReader("CodigoVendedor").ToString()

                        If Not String.IsNullOrEmpty(codigoVendedor) Then
                            Helper.AddElement(xmlDoc, emisorNode, "CodigoVendedor", codigoVendedor)
                        End If

                        ' Verificar si el campo "NumeroFacturaInterna" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim numeroFacturaInterna As String = facturaReader("NumeroFacturaInterna").ToString()

                        If Not String.IsNullOrEmpty(numeroFacturaInterna) Then
                            Helper.AddElement(xmlDoc, emisorNode, "NumeroFacturaInterna", numeroFacturaInterna)
                        End If

                        ' Verificar si el campo "ZonaVenta" no es nulo o vacÃ­o antes de agregar el elemento
                        Dim zonaVenta As String = facturaReader("ZonaVenta").ToString()

                        If Not String.IsNullOrEmpty(zonaVenta) Then
                            Helper.AddElement(xmlDoc, emisorNode, "ZonaVenta", zonaVenta)
                        End If

                        Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

                        Dim tipo As String = facturaReader("TipoeCF").ToString().Trim()
                        Dim total As Decimal = 0D
                        Decimal.TryParse(CStr(facturaReader("fac_total")), NumberStyles.Any, CultureInfo.InvariantCulture, total)

                        Dim requiereComprador As Boolean = (tipo = "31" Or total > 250000D)
                        If requiereComprador Then

                            ' Nodo Comprador
                            Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
                            encabezadoNode.AppendChild(compradorNode)
                            Helper.AddElement(xmlDoc, compradorNode, "RNCComprador", facturaReader("cli_rnc").ToString())
                            Helper.AddElement(xmlDoc, compradorNode, "RazonSocialComprador", facturaReader("cli_nombre").ToString())

                            ' Verificar si el campo "ContactoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                            Dim contactoComprador As String = facturaReader("cli_contacto").ToString()

                            If Not String.IsNullOrEmpty(contactoComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "ContactoComprador", contactoComprador)
                            End If

                            ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                            Dim correoComprador As String = facturaReader("cli_email").ToString()

                            If Not String.IsNullOrEmpty(correoComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "CorreoComprador", correoComprador)
                            End If

                            ' Verificar si el campo "CorreoComprador" no es nulo o vacÃ­o antes de agregar el elemento
                            Dim direccionComprador As String = facturaReader("cli_direccion").ToString()

                            If Not String.IsNullOrEmpty(direccionComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "DireccionComprador", direccionComprador)
                            End If

                            Dim municipioComprador As String = facturaReader("MunicipioComprador").ToString()

                            If Not String.IsNullOrEmpty(municipioComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "MunicipioComprador", municipioComprador)
                            End If

                            Dim provinciaComprador As String = facturaReader("ProvinciaComprador").ToString()

                            If Not String.IsNullOrEmpty(provinciaComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "ProvinciaComprador", provinciaComprador)
                            End If

                            Dim fechaEntrega As String = facturaReader("FechaEntrega").ToString()

                            If Not String.IsNullOrEmpty(fechaEntrega) Then
                                Helper.AddElement(xmlDoc, compradorNode, "FechaEntrega", fechaEntrega)
                            End If

                            Dim fechaOrdenCompra As String = facturaReader("FechaOrdenCompra").ToString()

                            If Not String.IsNullOrEmpty(fechaOrdenCompra) Then
                                Helper.AddElement(xmlDoc, compradorNode, "FechaOrdenCompra", fechaOrdenCompra)
                            End If

                            Dim numeroOrdenCompra As String = facturaReader("NumeroOrdenCompra").ToString()

                            If Not String.IsNullOrEmpty(numeroOrdenCompra) Then
                                Helper.AddElement(xmlDoc, compradorNode, "NumeroOrdenCompra", numeroOrdenCompra)
                            End If

                            Dim codigoInternoComprador As String = facturaReader("CodigoInternoComprador").ToString()

                            If Not String.IsNullOrEmpty(codigoInternoComprador) Then
                                Helper.AddElement(xmlDoc, compradorNode, "CodigoInternoComprador", codigoInternoComprador)
                            End If

                            Dim numeroContenedor As String = facturaReader("numeroContenedor").ToString()

                            If Not String.IsNullOrEmpty(numeroContenedor) Then
                                Helper.AddElement(xmlDoc, compradorNode, "NumeroContenedor", numeroContenedor)
                            End If

                            Dim numeroReferencia As String = facturaReader("numeroReferencia").ToString()

                            If Not String.IsNullOrEmpty(numeroReferencia) Then
                                Helper.AddElement(xmlDoc, compradorNode, "NumeroReferencia", numeroReferencia)
                            End If
                        End If

                        ' Nodo Totales
                        ''Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
                        encabezadoNode.AppendChild(totalesNode)

                        ' Guardar los valores en variables antes de cerrar el reader
                        '' DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis_dgi").ToString())

                        DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis").ToString())

                        DecItbis = Decimal.Parse(facturaReader("fac_itbis").ToString())
                        itbisTotal = If(IsDBNull(facturaReader("fac_itbis")) OrElse facturaReader("fac_itbis").ToString() = "", "0.00", Decimal.Parse(facturaReader("fac_itbis").ToString()).ToString("F2"))
                        itbisTotal1 = If(IsDBNull(facturaReader("det_totalitbis_1")) OrElse facturaReader("det_totalitbis_1").ToString() = "", "0.00", Decimal.Parse(facturaReader("det_totalitbis_1").ToString()).ToString("F2"))
                        itbisTotal2 = If(IsDBNull(facturaReader("det_totalitbis_2")) OrElse facturaReader("det_totalitbis_2").ToString() = "", "0.00", Decimal.Parse(facturaReader("det_totalitbis_2").ToString()).ToString("F2"))

                        itbis18 = Decimal.Parse(facturaReader("det_itbis_1").ToString()).ToString("F0")
                        itbis16 = Decimal.Parse(facturaReader("det_itbis_2").ToString()).ToString("F0")
                        totalFactura = Decimal.Parse(facturaReader("fac_total").ToString()).ToString("F2")
                        valorpagar = Decimal.Parse(facturaReader("ValorPagar").ToString()).ToString("F2")
                        DecGravado = If(IsDBNull(facturaReader("monto_grabado")), 0D, Convert.ToDecimal(facturaReader("monto_grabado")))
                        DecMontoDescuento = If(IsDBNull(facturaReader("fac_descuento")), 0D, Convert.ToDecimal(facturaReader("fac_descuento")))
                        montoGravado = DecGravado.ToString("F2")

                        montoGravado1 = If(IsDBNull(facturaReader("monto_grabado1")), 0D, Convert.ToDecimal(facturaReader("monto_grabado1"))).ToString("F2")

                        montoGravado2 = If(IsDBNull(facturaReader("monto_grabado2")), 0D, Convert.ToDecimal(facturaReader("monto_grabado2"))).ToString("F2")

                        If montoGravado <> "0" And montoGravado <> "0.00" Then
                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoTotal", montoGravado)
                        End If

                        If (Not String.IsNullOrEmpty(montoGravado1)) And (Not montoGravado1 = "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI1", montoGravado1)
                        End If

                        If (Not String.IsNullOrEmpty(montoGravado2)) And (Not montoGravado2 = "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI2", montoGravado2)
                        End If

                        ' Verificar si el monto exento no es igual a 0.00 antes de agregar el elementoltw1
                        DecExento = Decimal.Parse(facturaReader("monto_exento").ToString())

                        If (Not String.IsNullOrEmpty(DecExento.ToString("F2"))) And (Not DecExento.ToString("F2") = "0.00") Then
                            montoexento = DecExento.ToString("F2")
                            Helper.AddElement(xmlDoc, totalesNode, "MontoExento", montoexento)
                        End If

                        '' ITBIS1/ITBIS2: si hay TotalITBISn o MontoGravadoIn, incluir tasa (evita omitir ITBIS2 cuando det_itbis_2=0)
                        If (Not String.IsNullOrEmpty(itbisTotal1)) AndAlso itbisTotal1 <> "0.00" Then
                            Dim tasaItbis1 As String = If(String.IsNullOrEmpty(itbis18) OrElse itbis18 = "0", "18", itbis18)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", tasaItbis1)
                        ElseIf (Not String.IsNullOrEmpty(montoGravado1)) AndAlso montoGravado1 <> "0.00" Then
                            Dim tasaItbis1b As String = If(String.IsNullOrEmpty(itbis18) OrElse itbis18 = "0", "18", itbis18)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", tasaItbis1b)
                        End If
                        If (Not String.IsNullOrEmpty(itbisTotal2)) AndAlso itbisTotal2 <> "0.00" Then
                            Dim tasaItbis2 As String = If(String.IsNullOrEmpty(itbis16) OrElse itbis16 = "0", "16", itbis16)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS2", tasaItbis2)
                        ElseIf (Not String.IsNullOrEmpty(montoGravado2)) AndAlso montoGravado2 <> "0.00" Then
                            Dim tasaItbis2b As String = If(String.IsNullOrEmpty(itbis16) OrElse itbis16 = "0", "16", itbis16)
                            Helper.AddElement(xmlDoc, totalesNode, "ITBIS2", tasaItbis2b)
                        End If

                        If (Not String.IsNullOrEmpty(itbisTotal)) And (itbisTotal <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS", itbisTotal)
                        End If

                        If (Not String.IsNullOrEmpty(itbisTotal1)) And (itbisTotal1 <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS1", itbisTotal1)
                        End If

                        If (Not String.IsNullOrEmpty(itbisTotal2)) And (itbisTotal2 <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS2", itbisTotal2)
                        End If

                        ' Agregar la secciÃ³n de Impuestos Adicionales solo si totalesNode ha sido creado
                        ' RESTBAR: no existe Impuestos_adicionales_dgi para este flujo

                        If totalesNode IsNot Nothing AndAlso Not isResBar Then
                            Dim consultaImpuesto = " SELECT * FROM Impuestos_adicionales_dgi WHERE fac_numero=" & facNumero
                            Dim impuestoReader = EjecutarConsultaReader(cadenaConexion, consultaImpuesto)

                            ' Solo agregar MontoImpuestoAdicional si hay registros en Impuestos_adicionales_dgi
                            If impuestoReader.HasRows Then
                                ' Leer el primer registro para obtener el MontoImpuestoAdicional
                                If impuestoReader.Read() Then
                                    ' Agregar MontoImpuestoAdicional antes del nodo ImpuestosAdicionales
                                    DecMontoImpuestoAdicional = Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional")))
                                    If DecMontoImpuestoAdicional.ToString("F2") <> "0.00" Then
                                        Helper.AddElement(xmlDoc, totalesNode, "MontoImpuestoAdicional", Decimal.Parse(CStr(impuestoReader("MontoImpuestoAdicional"))).ToString("F2"))
                                    End If
                                End If

                                ' Nodo ImpuestosAdicionales
                                Dim impuestosAdicionalesNode As XmlElement = xmlDoc.CreateElement("ImpuestosAdicionales")
                                totalesNode.AppendChild(impuestosAdicionalesNode)

                                ' Leer cada fila y aÃ±adir los impuestos adicionales
                                Do
                                    ' Para el conjunto de columnas que terminan en _1
                                    If Not IsDBNull(impuestoReader("TipoImpuesto_1")) Then
                                        Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                        impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode1)
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", impuestoReader("TipoImpuesto_1").ToString())
                                        Dim tasaImpuestoAdicional1 As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_1").ToString())

                                        ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                        If tasaImpuestoAdicional1 = Math.Floor(tasaImpuestoAdicional1) Then
                                            ' No tiene decimales, mostramos solo el entero
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F0"))
                                        Else
                                            ' Tiene decimales, mostramos con dos decimales
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TasaImpuestoAdicional", tasaImpuestoAdicional1.ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_1").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_1").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar OtrosImpuestosAdicionales
                                        If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_1")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_1").ToString()).ToString("F2"))
                                        End If
                                    End If

                                    ' Para el conjunto de columnas que terminan en _2
                                    If Not IsDBNull(impuestoReader("TipoImpuesto_2")) Then
                                        Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                        impuestosAdicionalesNode.AppendChild(impuestoAdicionalNode2)
                                        Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", impuestoReader("TipoImpuesto_2").ToString())

                                        Dim tasaImpuestoAdicional As Decimal = Decimal.Parse(impuestoReader("TasaImpuestoAdicional_2").ToString())

                                        ' Si la parte decimal es 0, mostramos solo el entero; de lo contrario, mostramos dos decimales
                                        If tasaImpuestoAdicional = Math.Floor(tasaImpuestoAdicional) Then
                                            ' No tiene decimales, mostramos solo el entero
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F0"))
                                        Else
                                            ' Tiene decimales, mostramos con dos decimales
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TasaImpuestoAdicional", tasaImpuestoAdicional.ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoEspecifico
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoEspecifico", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoEspecifico_2").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar MontoImpuestoSelectivoConsumoAdvalorem
                                        If Not IsDBNull(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "MontoImpuestoSelectivoConsumoAdvalorem", Decimal.Parse(impuestoReader("MontoImpuestoSelectivoConsumoAdvalorem_2").ToString()).ToString("F2"))
                                        End If

                                        ' Verificar y agregar OtrosImpuestosAdicionales
                                        If Not IsDBNull(impuestoReader("OtrosImpuestosAdicionales_2")) Then
                                            Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "OtrosImpuestosAdicionales", Decimal.Parse(impuestoReader("OtrosImpuestosAdicionales_2").ToString()).ToString("F2"))
                                        End If
                                    End If
                                Loop While impuestoReader.Read()
                            End If
                            impuestoReader.Close()
                        End If

                        If (DecItbisDGI > 0) Then
                            DecmontoTotal = DecGravado + DecExento + DecItbisDGI + DecMontoImpuestoAdicional
                        Else
                            DecmontoTotal = DecGravado + DecExento + DecItbis + DecMontoImpuestoAdicional
                        End If

                        Dim montoTotalStr As String = (Math.Floor(CDec(totalFactura) * 100) / 100).ToString("0.00")
                        Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", montoTotalStr)

                        If (Not String.IsNullOrEmpty(valorpagar)) And (valorpagar <> "0.00") Then
                            Helper.AddElement(xmlDoc, totalesNode, "ValorPagar", valorpagar)
                        End If
                    End If
                    facturaReader.Close()

                    ' Nodo DetallesItems
                    Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
                    root.AppendChild(detallesItemsNode)

                    ' Consulta para obtener la informaciÃ³n de Detalle de Factura
                    Dim consultaDetalle = ConsultaDetalleFacturaXml(emp_codigo, suc_codigo, fac_forma, tfa_codigo, facNumero)
                    Dim detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalle)

                    While detalleReader.Read()
                        ' Nodo Item dentro de DetallesItems
                        Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
                        detallesItemsNode.AppendChild(itemNode)

                        ' AÃ±adir los detalles del item
                        Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", detalleReader("NumeroLinea").ToString())
                        Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", detalleReader("IndicadorFacturacion").ToString())
                        Helper.AddElement(xmlDoc, itemNode, "NombreItem", detalleReader("NombreItem").ToString())
                        Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", detalleReader("IndicadorBienoServicio").ToString())

                        cantidadItem = Decimal.Parse(detalleReader("CantidadItem").ToString()).ToString("F2")
                        unidadMedida = Decimal.Parse(detalleReader("UnidadMedida").ToString()).ToString("F0")

                        gradosalcohol = Decimal.Parse(detalleReader("gradosalcohol").ToString()).ToString("F2")
                        preciounitarioreferencia = Decimal.Parse(detalleReader("preciounitarioreferencia").ToString()).ToString("F2")

                        Helper.AddElement(xmlDoc, itemNode, "CantidadItem", cantidadItem)
                        Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", unidadMedida)

                        If (Not String.IsNullOrEmpty(CStr(detalleReader("cantidadreferencia")))) And (Decimal.Parse(detalleReader("cantidadreferencia").ToString()) <> CDec("0")) Then
                            cantidadReferencia = Decimal.Parse(detalleReader("cantidadreferencia").ToString()).ToString("F0")
                            If (Not String.IsNullOrEmpty(cantidadReferencia)) And (cantidadReferencia <> "0") Then
                                Helper.AddElement(xmlDoc, itemNode, "CantidadReferencia", cantidadReferencia)
                            End If
                        End If

                        If (Not String.IsNullOrEmpty(CStr(detalleReader("unidadreferencia")))) And (Decimal.Parse(detalleReader("unidadreferencia").ToString()) <> CDec("0")) Then
                            UnidadReferencia = Decimal.Parse(detalleReader("unidadreferencia").ToString()).ToString("F0")
                            If (Not String.IsNullOrEmpty(UnidadReferencia)) And (UnidadReferencia <> "0") Then
                                Helper.AddElement(xmlDoc, itemNode, "UnidadReferencia", UnidadReferencia)
                            End If
                        End If

                        ' AÃ±adir la tabla de Subcantidad
                        If Not IsDBNull(detalleReader("Subcantidad")) And Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                            If Not detalleReader("Subcantidad").ToString() = "0.00" And Not detalleReader("Subcantidad").ToString() = "" And (Not detalleReader("CodigoSubcantidad").ToString() = "") Then
                                Dim tablaSubcantidadNode As XmlElement = xmlDoc.CreateElement("TablaSubcantidad")
                                itemNode.AppendChild(tablaSubcantidadNode)

                                Dim subcantidadNode As XmlElement = xmlDoc.CreateElement("SubcantidadItem")
                                tablaSubcantidadNode.AppendChild(subcantidadNode)

                                If Not IsDBNull(detalleReader("Subcantidad")) Then
                                    Helper.AddElement(xmlDoc, subcantidadNode, "Subcantidad", Decimal.Parse(detalleReader("Subcantidad").ToString()).ToString("F3"))
                                End If

                                If Not IsDBNull(detalleReader("CodigoSubcantidad")) Then
                                    Helper.AddElement(xmlDoc, subcantidadNode, "CodigoSubcantidad", detalleReader("CodigoSubcantidad").ToString())
                                End If
                            End If
                        End If


                        If (Not String.IsNullOrEmpty(gradosalcohol)) And (gradosalcohol <> "0.00") Then
                            Helper.AddElement(xmlDoc, itemNode, "GradosAlcohol", gradosalcohol)
                        End If

                        If (Not String.IsNullOrEmpty(preciounitarioreferencia)) And (preciounitarioreferencia <> "0.00") Then
                            Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioReferencia", preciounitarioreferencia)
                        End If

                        precioUnitarioItem = Decimal.Parse(detalleReader("PrecioUnitarioItem").ToString()).ToString("F2")
                        Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", precioUnitarioItem)

                        ' AÃ±adir la tabla de impuestos adicionales (TipoImpuesto)
                        If Not IsDBNull(detalleReader("TipoImpuesto1")) And Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                            If Not detalleReader("TipoImpuesto1").ToString() = "" And Not detalleReader("TipoImpuesto2").ToString() = "" Then
                                Dim tablaImpuestoAdicionalNode As XmlElement = xmlDoc.CreateElement("TablaImpuestoAdicional")
                                itemNode.AppendChild(tablaImpuestoAdicionalNode)

                                If Not IsDBNull(detalleReader("TipoImpuesto1")) Then
                                    Dim impuestoAdicionalNode1 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode1)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode1, "TipoImpuesto", detalleReader("TipoImpuesto1").ToString())
                                End If

                                If Not IsDBNull(detalleReader("TipoImpuesto2")) Then
                                    Dim impuestoAdicionalNode2 As XmlElement = xmlDoc.CreateElement("ImpuestoAdicional")
                                    tablaImpuestoAdicionalNode.AppendChild(impuestoAdicionalNode2)
                                    Helper.AddElement(xmlDoc, impuestoAdicionalNode2, "TipoImpuesto", detalleReader("TipoImpuesto2").ToString())
                                End If
                            End If
                        End If

                        ' Verificar si el DescuentoMonto no es igual a 0.0000 antes de agregar el elemento
                        '  Dim descuentoMonto As String = If(DBNull.Value.Equals(detalleReader("DescuentoMonto")), "0.0000", detalleReader("DescuentoMonto").ToString())

                        Dim montoDecimal As Decimal

                        ' Verificar si el DescuentoMonto no es igual a 0.0000 antes de agregar el elemento
                        '  Dim descuentoMonto As String = If(DBNull.Value.Equals(detalleReader("DescuentoMonto")), "0.0000", detalleReader("DescuentoMonto").ToString())
                        Dim descuentoRaw As String = If(detalleReader.IsDBNull(detalleReader.GetOrdinal("DescuentoMonto")), "", detalleReader("DescuentoMonto").ToString())
                        Dim descuentoNorm As String = NormalizeDecimalString(descuentoRaw) ' <-- SIEMPRE "F2" con punto

                        ' Verificamos si al menos uno de los subdescuentos es vÃ¡lido antes de crear la tabla

                        ' ===== Validadores =====
                        Dim tipo1 As String = If(IsDBNull(detalleReader("TipoSubDescuento_1")), "", detalleReader("TipoSubDescuento_1").ToString().Trim())
                        Dim tipo2 As String = If(IsDBNull(detalleReader("TipoSubDescuento_2")), "", detalleReader("TipoSubDescuento_2").ToString().Trim())

                        Dim m1 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_1"))
                        Dim m2 As Decimal? = SafeDec(detalleReader("MontoSubdescuento_2"))

                        Dim tipo1Valido As Boolean = (tipo1 = "$" OrElse tipo1 = "%")
                        Dim tipo2Valido As Boolean = (tipo2 = "$" OrElse tipo2 = "%")

                        Dim monto1Valido As Boolean = (m1.HasValue AndAlso m1.Value > 0D)
                        Dim monto2Valido As Boolean = (m2.HasValue AndAlso m2.Value > 0D)

                        Dim tieneSubDescuento1 As Boolean = (tipo1Valido AndAlso monto1Valido)
                        Dim tieneSubDescuento2 As Boolean = (tipo2Valido AndAlso monto2Valido)

                        If descuentoNorm <> "0.00" Then
                            Helper.AddElement(xmlDoc, itemNode, "DescuentoMonto", descuentoNorm)

                            ' Si al menos uno de los subdescuentos es REAL y vÃ¡lido, agregamos la estructura
                            If tieneSubDescuento1 OrElse tieneSubDescuento2 Then
                                ' Crear TablaSubDescuento solo si hay datos vÃ¡lidos
                                Dim subDescuentoNode As XmlElement = xmlDoc.CreateElement("TablaSubDescuento")
                                itemNode.AppendChild(subDescuentoNode)

                                ' Agregar SubDescuento_1
                                If tieneSubDescuento1 Then
                                    Dim subDescuentoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                    subDescuentoNode.AppendChild(subDescuentoDetailNode1)

                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "TipoSubDescuento", tipo1)
                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode1, "MontoSubDescuento",
                          m1.Value.ToString("F2", CultureInfo.InvariantCulture))
                                End If

                                ' Agregar SubDescuento_2
                                If tieneSubDescuento2 Then
                                    Dim subDescuentoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubDescuento")
                                    subDescuentoNode.AppendChild(subDescuentoDetailNode2)

                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "TipoSubDescuento", tipo2)
                                    Helper.AddElement(xmlDoc, subDescuentoDetailNode2, "MontoSubDescuento",
                          m2.Value.ToString("F2", CultureInfo.InvariantCulture))
                                End If
                            End If
                        End If

                        ' --- Nueva SecciÃ³n para RecargoMonto y TablaSubRecargo ---
                        Dim recargoMonto As String = ""
                        If Not IsDBNull(detalleReader("RecargoMonto")) Then
                            recargoMonto = detalleReader("RecargoMonto").ToString()
                        End If

                        If Not recargoMonto = "0.00" AndAlso Not String.IsNullOrEmpty(recargoMonto) Then
                            ' Agregar elemento RecargoMonto formateado a dos decimales
                            If Decimal.TryParse(recargoMonto, NumberStyles.Any, CultureInfo.InvariantCulture, montoDecimal) Then
                                Helper.AddElement(xmlDoc, itemNode, "RecargoMonto", montoDecimal.ToString("F2", CultureInfo.InvariantCulture))
                            End If

                            ' Validar la existencia de subrecargos (por ejemplo, dos conjuntos)
                            Dim tieneSubRecargo1 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_1")) AndAlso
                                       Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_1").ToString())
                            Dim tieneSubRecargo2 As Boolean = Not IsDBNull(detalleReader("TipoSubRecargo_2")) AndAlso
                                       Not String.IsNullOrEmpty(detalleReader("TipoSubRecargo_2").ToString())

                            If tieneSubRecargo1 OrElse tieneSubRecargo2 Then
                                Dim tablaSubRecargoNode As XmlElement = xmlDoc.CreateElement("TablaSubRecargo")
                                itemNode.AppendChild(tablaSubRecargoNode)

                                ' Agregar SubRecargo 1
                                If tieneSubRecargo1 Then
                                    Dim subRecargoDetailNode1 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                                    tablaSubRecargoNode.AppendChild(subRecargoDetailNode1)
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode1, "TipoSubRecargo", detalleReader("TipoSubRecargo_1").ToString())

                                    ' Elemento opcional: SubRecargoPorcentaje
                                    If Not IsDBNull(detalleReader("SubRecargoPorcentaje_1")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_1").ToString()) Then
                                        Helper.AddElement(xmlDoc, subRecargoDetailNode1, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_1").ToString())
                                    End If

                                    ' Elemento opcional: MontoSubRecargo
                                    If Not IsDBNull(detalleReader("MontoSubRecargo_1")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_1").ToString()) Then
                                        Helper.AddElement(xmlDoc, subRecargoDetailNode1, "MontoSubRecargo", detalleReader("MontoSubRecargo_1").ToString())
                                    End If
                                End If

                                ' Agregar SubRecargo 2
                                If tieneSubRecargo2 Then
                                    Dim subRecargoDetailNode2 As XmlElement = xmlDoc.CreateElement("SubRecargo")
                                    tablaSubRecargoNode.AppendChild(subRecargoDetailNode2)
                                    Helper.AddElement(xmlDoc, subRecargoDetailNode2, "TipoSubRecargo", detalleReader("TipoSubRecargo_2").ToString())

                                    If Not IsDBNull(detalleReader("SubRecargoPorcentaje_2")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("SubRecargoPorcentaje_2").ToString()) Then
                                        Helper.AddElement(xmlDoc, subRecargoDetailNode2, "SubRecargoPorcentaje", detalleReader("SubRecargoPorcentaje_2").ToString())
                                    End If

                                    If Not IsDBNull(detalleReader("MontoSubRecargo_2")) AndAlso
                                    Not String.IsNullOrEmpty(detalleReader("MontoSubRecargo_2").ToString()) Then
                                        Helper.AddElement(xmlDoc, subRecargoDetailNode2, "MontoSubRecargo", detalleReader("MontoSubRecargo_2").ToString())
                                    End If
                                End If
                            End If
                        End If

                        ' AÃ±adir MontoItem
                        montoItem = Decimal.Parse(detalleReader("MontoItem").ToString()).ToString("F2")
                        Helper.AddElement(xmlDoc, itemNode, "MontoItem", montoItem)

                        ' Actualizar la Ãºltima lÃ­nea del detalle
                        ultimaLineaDetalle = detalleReader("NumeroLinea").ToString()
                    End While
                    detalleReader.Close()

                    Dim consultaDescuentooRecargo = " SELECT * FROM DescuentooRecargo WHERE fac_numero=" & facNumero & " and fac_forma='" & fac_forma & "' and emp_codigo=" & emp_codigo & " and tfa_codigo=" & tfa_codigo & " and suc_codigo=" & suc_codigo
                    Dim descuentoRecargoReader = EjecutarConsultaReader(cadenaConexion, consultaDescuentooRecargo)

                    ' Verifica si hay descuentos o recargos asociados
                    If descuentoRecargoReader.HasRows Then
                        ' Crea el nodo DescuentosORecargos
                        Dim descuentosORecargosNode As XmlElement = xmlDoc.CreateElement("DescuentosORecargos")
                        root.AppendChild(descuentosORecargosNode)

                        ' Itera sobre los registros de la tabla
                        Do While descuentoRecargoReader.Read()
                            ' Crea el nodo DescuentoORecargo para cada registro
                            Dim descuentoORecargoNode As XmlElement = xmlDoc.CreateElement("DescuentoORecargo")
                            descuentosORecargosNode.AppendChild(descuentoORecargoNode)

                            ' AÃ±ade los detalles del descuento o recargo
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "NumeroLinea", descuentoRecargoReader("NumeroLineaDoR").ToString())
                            Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoAjuste", descuentoRecargoReader("TipoAjuste").ToString())

                            '' Opcional: Verifica si el campo estÃ¡ presente antes de agregarlo
                            'If Not IsDBNull(descuentoRecargoReader("IndicadorNorma1007")) Then
                            '    Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorNorma1007", descuentoRecargoReader("IndicadorNorma1007").ToString())
                            'End If

                            If Not IsDBNull(descuentoRecargoReader("DescripcionDescuentooRecargo")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "DescripcionDescuentooRecargo", descuentoRecargoReader("DescripcionDescuentooRecargo").ToString())
                            End If

                            If Not IsDBNull(descuentoRecargoReader("TipoValor")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "TipoValor", descuentoRecargoReader("TipoValor").ToString())
                            End If
                            If Not IsDBNull(descuentoRecargoReader("ValorDescuentooRecargo")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "ValorDescuentooRecargo", Decimal.Parse(descuentoRecargoReader("ValorDescuentooRecargo").ToString()).ToString("F2"))
                            End If
                            If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargo")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargo", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargo").ToString()).ToString("F2"))
                            End If

                            'If Not IsDBNull(descuentoRecargoReader("MontoDescuentooRecargoOtraMoneda")) Then
                            '    Helper.AddElement(xmlDoc, descuentoORecargoNode, "MontoDescuentooRecargoOtraMoneda", Decimal.Parse(descuentoRecargoReader("MontoDescuentooRecargoOtraMoneda").ToString()).ToString("F2"))
                            'End If

                            If Not IsDBNull(descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo")) Then
                                Helper.AddElement(xmlDoc, descuentoORecargoNode, "IndicadorFacturacionDescuentooRecargo", descuentoRecargoReader("IndicadorFacturacionDescuentooRecargo").ToString())
                            End If
                        Loop
                    End If
                    descuentoRecargoReader.Close()
                    ' Nodo FechaHoraFirma fuera del Encabezado y despuÃ©s de Paginacion
                    Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
                    root.AppendChild(fechaHoraFirmaNode)

                    ' Obtener la fecha y hora actual en el formato requerido y agregarla al nodo
                    Dim fechaHoraFirma As String = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
                    fechaHoraFirmaNode.InnerText = fechaHoraFirma
                End If
            End If
        Catch ex As Exception
            Throw New ApplicationException("Error en el proceso de envÃ­o de la factura: " & ex.Message)
        End Try
        ' Retorna el documento XML generado
        Return xmlDoc
    End Function

    ' Helper reutilizable
    Private Shared Function SafeDec(val As Object) As Decimal?
        If val Is Nothing OrElse val Is DBNull.Value Then Return Nothing
        If TypeOf val Is Decimal Then Return DirectCast(val, Decimal)
        If TypeOf val Is Double Then Return CDec(DirectCast(val, Double))
        Dim d As Decimal
        ' Intenta con Invariant y, si falla, con cultura local
        If Decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, d) _
       OrElse Decimal.TryParse(val.ToString(), NumberStyles.Any, New CultureInfo("es-DO"), d) Then
            Return d
        End If
        Return Nothing
    End Function

    Private Shared Function ObtenerFechaFirmaDesdeXml(xmlDoc As XmlDocument) As String
        Dim nodoFechaFirma As XmlNode = xmlDoc.SelectSingleNode("//FechaHoraFirma")
        If nodoFechaFirma IsNot Nothing Then
            Return nodoFechaFirma.InnerText.Trim()
        Else
            Return "Fecha de firma no encontrada"
        End If
    End Function

    ''' <summary>Intenta interpretar el texto de FechaHoraFirma del e-CF (varias culturas/formatos).</summary>
    Private Shared Function TryParseFechaHoraFirmaECF(fechaTexto As String, ByRef fecha As DateTime) As Boolean
        Dim s As String = If(fechaTexto, "").Trim()
        If s.Length = 0 Then Return False
        If String.Equals(s, "Fecha de firma no encontrada", StringComparison.OrdinalIgnoreCase) Then Return False

        Dim formats As String() = {
            "dd-MM-yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss",
            "dd-MM-yyyy H:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss"
        }
        For Each fmt In formats
            If DateTime.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, fecha) Then Return True
            If DateTime.TryParseExact(s, fmt, New CultureInfo("es-DO"), DateTimeStyles.None, fecha) Then Return True
        Next
        If DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, fecha) Then Return True
        If DateTime.TryParse(s, New CultureInfo("es-DO"), DateTimeStyles.None, fecha) Then Return True
        Return False
    End Function

    ''' <summary>Fecha de firma del XML extendido firmado; si falta o el formato no coincide, usa Now y deja rastro en log.</summary>
    Private Shared Function ObtenerFechaFirmaDesdeXmlComoDateTime(xmlDoc As XmlDocument) As DateTime
        Dim txt As String = ObtenerFechaFirmaDesdeXml(xmlDoc)
        Dim fecha As DateTime
        If TryParseFechaHoraFirmaECF(txt, fecha) Then Return fecha
        LogDetallado("FechaHoraFirma ausente o no reconocida; se usa DateTime.Now. Valor=[" & txt & "]")
        Return DateTime.Now
    End Function

    Private Shared Function ObtenerCodigoSeguridadDesdeXml(xmlDoc As XmlDocument) As String
        Dim nsmgr As New XmlNamespaceManager(xmlDoc.NameTable)
        nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#")

        Dim signatureValueNode As XmlNode = xmlDoc.SelectSingleNode("//ds:SignatureValue", nsmgr)
        If signatureValueNode Is Nothing Then
            Return "Firma no encontrada"
        End If

        Dim hash As String = signatureValueNode.InnerText.Trim()
        If hash.Length >= 6 Then
            Return hash.Substring(0, 6)
        Else
            Return "Hash incompleto"
        End If
    End Function
    Private Shared Function EjecutarConsultaReader(cadenaConexion As String, consulta As String) As SqlDataReader
        Dim qLog As String = If(consulta, "")
        Try
            Helper.RegistrarLogCliente("[SQL Reader] " & qLog)
        Catch
        End Try

        Dim conn As New SqlConnection(cadenaConexion)
        Dim cmd As New SqlCommand(consulta, conn)
        cmd.CommandTimeout = SqlCommandTimeoutSegundos

        Try
            conn.Open()
            Return cmd.ExecuteReader(CommandBehavior.CloseConnection)
        Catch ex As SqlException
            Dim qPreview As String = SqlQueryPreviewParaLog(qLog)
            Try
                Helper.RegistrarLogCliente("[SQL Reader ERROR] Number=" & ex.Number.ToString() & " " & ex.Message & " | " & qPreview)
            Catch
            End Try
            Throw New ApplicationException(
                "Consulta que fallÃ³ (SQL " & ex.Number.ToString() & "): " & ex.Message & Environment.NewLine & qPreview, ex)
        Catch ex As Exception
            Dim qPreview As String = SqlQueryPreviewParaLog(qLog)
            Try
                Helper.RegistrarLogCliente("[SQL Reader ERROR] " & ex.Message & " | " & qPreview)
            Catch
            End Try
            Throw New ApplicationException(
                "Consulta que fallÃ³: " & ex.Message & Environment.NewLine & qPreview, ex)
        End Try
    End Function

    ''' <summary>Recorta la consulta para mensajes de error y log (evita strings enormes).</summary>
    Private Shared Function SqlQueryPreviewParaLog(consulta As String) As String
        Dim s As String = If(consulta, "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        Const maxLen As Integer = 800
        If s.Length <= maxLen Then Return s
        Return s.Substring(0, maxLen) & "..."
    End Function

    ''' <summary>Encabezado XML flujo POS: ejecuta <c>dbo.spFacturasXMLPOSGet</c> (misma proyecciÃ³n que <c>vwFacturasXMLPOS</c>, filtrada por ticket/cajero/caja).</summary>
    Private Shared Function ConsultaFacturaXmlPosSp(emp_codigo As String, suc_codigo As String, facNumero As String, usu_codigo As String, caja As String) As String
        Dim uEsc As String = If(usu_codigo, "").Trim().Replace("'", "''")
        Dim cjEsc As String = If(caja, "").Trim().Replace("'", "''")
        Dim empI As Integer = Convert.ToInt32(emp_codigo, CultureInfo.InvariantCulture)
        Dim sucI As Integer = Convert.ToInt32(suc_codigo, CultureInfo.InvariantCulture)
        Dim ticketI As Integer = Convert.ToInt32(facNumero, CultureInfo.InvariantCulture)
        Return "EXEC dbo.spFacturasXMLPOSGet @emp_codigo=" & empI.ToString(CultureInfo.InvariantCulture) &
            ",@suc_codigo=" & sucI.ToString(CultureInfo.InvariantCulture) &
            ",@fac_numero=" & ticketI.ToString(CultureInfo.InvariantCulture) &
            ",@usu_codigo=N'" & uEsc & "'" &
            ",@caja=N'" & cjEsc & "'"
    End Function

    ''' <summary>Detalle XML flujo POS: ejecuta <c>dbo.spFacturasDetalleXMLPOSGet</c> (misma proyecciÃ³n que <c>vwFacturasDetalleXMLPOS</c>).</summary>
    Private Shared Function ConsultaFacturasDetalleXmlPosSp(emp_codigo As String, suc_codigo As String, facNumero As String, usu_codigo As String, caja As String) As String
        Dim uEsc As String = If(usu_codigo, "").Trim().Replace("'", "''")
        Dim cjEsc As String = If(caja, "").Trim().Replace("'", "''")
        Dim empI As Integer = Convert.ToInt32(emp_codigo, CultureInfo.InvariantCulture)
        Dim sucI As Integer = Convert.ToInt32(suc_codigo, CultureInfo.InvariantCulture)
        Dim ticketI As Integer = Convert.ToInt32(facNumero, CultureInfo.InvariantCulture)
        Return "EXEC dbo.spFacturasDetalleXMLPOSGet @emp_codigo=" & empI.ToString(CultureInfo.InvariantCulture) &
            ",@suc_codigo=" & sucI.ToString(CultureInfo.InvariantCulture) &
            ",@fac_numero=" & ticketI.ToString(CultureInfo.InvariantCulture) &
            ",@usu_codigo=N'" & uEsc & "'" &
            ",@caja=N'" & cjEsc & "'"
    End Function

    ''' <summary>Encabezado XML flujo RESBAR: ejecuta <c>dbo.spFacturasXMLRESBARGet</c> (equivalente filtrado por ticket/cajero/caja).</summary>
    Private Shared Function ConsultaFacturaXmlResBarSp(emp_codigo As String, suc_codigo As String, facNumero As String, usu_codigo As String, caja As String) As String
        Dim uEsc As String = If(usu_codigo, "").Trim().Replace("'", "''")
        Dim cjEsc As String = If(caja, "").Trim().Replace("'", "''")
        Dim empI As Integer = Convert.ToInt32(emp_codigo, CultureInfo.InvariantCulture)
        Dim sucI As Integer = Convert.ToInt32(suc_codigo, CultureInfo.InvariantCulture)
        Dim ticketI As Integer = Convert.ToInt32(facNumero, CultureInfo.InvariantCulture)
        Return "EXEC dbo.spFacturasXMLRESBARGet @emp_codigo=" & empI.ToString(CultureInfo.InvariantCulture) &
            ",@suc_codigo=" & sucI.ToString(CultureInfo.InvariantCulture) &
            ",@fac_numero=" & ticketI.ToString(CultureInfo.InvariantCulture) &
            ",@usu_codigo=N'" & uEsc & "'" &
            ",@caja=N'" & cjEsc & "'"
    End Function

    ''' <summary>Detalle XML flujo RESBAR: ejecuta <c>dbo.spFacturasDetalleXMLRESBARGet</c> (equivalente filtrado por ticket/cajero/caja).</summary>
    Private Shared Function ConsultaFacturasDetalleXmlResBarSp(emp_codigo As String, suc_codigo As String, facNumero As String, usu_codigo As String, caja As String) As String
        Dim uEsc As String = If(usu_codigo, "").Trim().Replace("'", "''")
        Dim cjEsc As String = If(caja, "").Trim().Replace("'", "''")
        Dim empI As Integer = Convert.ToInt32(emp_codigo, CultureInfo.InvariantCulture)
        Dim sucI As Integer = Convert.ToInt32(suc_codigo, CultureInfo.InvariantCulture)
        Dim ticketI As Integer = Convert.ToInt32(facNumero, CultureInfo.InvariantCulture)
        Return "EXEC dbo.spFacturasDetalleXMLRESBARGet @emp_codigo=" & empI.ToString(CultureInfo.InvariantCulture) &
            ",@suc_codigo=" & sucI.ToString(CultureInfo.InvariantCulture) &
            ",@fac_numero=" & ticketI.ToString(CultureInfo.InvariantCulture) &
            ",@usu_codigo=N'" & uEsc & "'" &
            ",@caja=N'" & cjEsc & "'"
    End Function

    ''' <summary>Encabezado XML facturaciÃ³n administrativa: ejecuta <c>dbo.spFacturasXMLGet</c> (misma proyecciÃ³n que <c>vwFacturasXML</c>).</summary>
    Private Shared Function ConsultaFacturaXmlStdSp(emp_codigo As String, suc_codigo As String, facNumero As String, fac_forma As String, tfa_codigo As String) As String
        Dim formaEsc As String = If(fac_forma, "").Trim().Replace("'", "''")
        Dim tfaEsc As String = If(tfa_codigo, "").Trim().Replace("'", "''")
        Dim empI As Integer = Convert.ToInt32(emp_codigo, CultureInfo.InvariantCulture)
        Dim sucI As Integer = Convert.ToInt32(suc_codigo, CultureInfo.InvariantCulture)
        Dim facI As Integer = Convert.ToInt32(facNumero, CultureInfo.InvariantCulture)
        Return "EXEC dbo.spFacturasXMLGet @emp_codigo=" & empI.ToString(CultureInfo.InvariantCulture) &
            ",@suc_codigo=" & sucI.ToString(CultureInfo.InvariantCulture) &
            ",@fac_numero=" & facI.ToString(CultureInfo.InvariantCulture) &
            ",@fac_forma=N'" & formaEsc & "'" &
            ",@tfa_codigo=N'" & tfaEsc & "'"
    End Function

    ''' <summary>
    ''' Detalle XML para una factura:
    ''' - Flujo administrativo y POS: SP dbo.GetFacturaDetalleXML (comportamiento original).
    ''' - Flujo RestBar: SP dbo.spFacturasDetalleXMLRESBARGet.
    ''' </summary>
    Private Shared Function ConsultaDetalleFacturaXml(emp_codigo As String, suc_codigo As String, fac_forma As String, tfa_codigo As String, facNumero As String, Optional usu_codigo As String = "", Optional caja As String = "") As String
        Dim forma1 As String = If(fac_forma, "").Trim()
        If forma1.Length > 1 Then forma1 = forma1.Substring(0, 1)
        Dim formaEsc As String = forma1.Replace("'", "''")
        Dim tfaEsc As String = (If(tfa_codigo, "")).Trim().Replace("'", "''")
        Dim uEsc As String = If(usu_codigo, "").Trim().Replace("'", "''")
        Dim cjEsc As String = If(caja, "").Trim().Replace("'", "''")

        If isResBar Then
            Return ConsultaFacturasDetalleXmlResBarSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)
        End If

        Return "EXEC dbo.GetFacturaDetalleXML @emp_codigo=" & emp_codigo & ",@suc_codigo=" & suc_codigo &
            ",@fac_forma=N'" & formaEsc & "',@tfa_codigo=N'" & tfaEsc & "',@fac_numero=" & facNumero
    End Function
    Private Shared Function EnviarXMLFacturaFirmado(ByVal signedXml As XmlDocument,
                                                ByVal token As String,
                                                cfEnvia As String,
                                                emp_rnc As String) As String
        Dim url As String = GlobalVariables.url_base & "recepcion/api/facturaselectronicas"

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls
        ServicePointManager.Expect100Continue = False

        Try
            Using client As New HttpClient()
                ' Aceptamos JSON (lo que DGII deberÃ­a devolver)
                client.DefaultRequestHeaders.Accept.Clear()
                client.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", token)

                ' XML a bytes (memoria)
                Dim bytes As Byte()
                Using ms As New MemoryStream()
                    signedXml.Save(ms)
                    bytes = ms.ToArray()
                End Using

                Dim part As New ByteArrayContent(bytes)
                part.Headers.ContentType = New MediaTypeHeaderValue("application/xml")
                Dim fileName As String = emp_rnc & cfEnvia & ".xml"

                Using form As New MultipartFormDataContent()
                    form.Add(part, "xml", fileName)

                    Dim resp = client.PostAsync(url, form).GetAwaiter().GetResult()
                    Dim body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()

                    ' Log completo
                    GuardarLogEnvioFactura(url, "HTTP " & CInt(resp.StatusCode) & " " & resp.ReasonPhrase & " | " & body)

                    ' 1) Si no es success, devolvemos error con detalle
                    If Not resp.IsSuccessStatusCode Then
                        ' DetecciÃ³n rÃ¡pida de error HTML tipo "MY NS HOME PAGE"
                        Dim bodyTrim = body.Trim().ToLowerInvariant()

                        If bodyTrim.StartsWith("<html") OrElse
                       bodyTrim.Contains("404 not found") OrElse
                       bodyTrim.Contains("my ns home page") Then

                            Return "ERROR: La URL de recepciÃ³n no responde como servicio ECF. " &
                               "Probable URL incorrecta o cambiada: " & url & " | BODY: " & body
                        End If

                        Return "ERROR: HTTP " & CInt(resp.StatusCode) & " " & resp.ReasonPhrase & " | " & body
                    End If

                    ' 2) Intentar parsear JSON (respuesta correcta de DGII)
                    Try
                        Dim jo = JObject.Parse(body)
                        Dim trackId = CStr(jo.SelectToken("trackId"))

                        If String.IsNullOrWhiteSpace(trackId) Then
                            Return "ERROR: respuesta sin trackId. Body: " & body
                        End If

                        Return trackId
                    Catch
                        ' 3) No fue JSON, revisar si quizÃ¡s es HTML
                        Dim bodyTrim = body.Trim().ToLowerInvariant()

                        If bodyTrim.StartsWith("<html") OrElse
                       bodyTrim.Contains("404 not found") OrElse
                       bodyTrim.Contains("my ns home page") Then

                            Return "ERROR: La URL de recepciÃ³n parece apuntar a una pÃ¡gina HTML (no al servicio web ECF). " &
                               "Revise la URL base: " & GlobalVariables.url_base & " | BODY: " & body
                        End If

                        Return "ERROR: respuesta no JSON. Body: " & body
                    End Try
                End Using
            End Using
        Catch ex As Exception
            Return "ERROR: " & ex.Message
        End Try
    End Function


    Private Shared Sub GuardarLogEnvioFactura(url As String, xml As String)
        Try
            Dim rutaLog As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "envios_factura.log")
            Dim linea As String = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | URL: {url} | XML: {xml}"
            File.AppendAllText(rutaLog, linea & Environment.NewLine)
        Catch
            ' Evitar que un error de log interrumpa el flujo
        End Try
    End Sub

    Private Shared Async Function EnviarFacturaClienteSiTieneUrlAsync(rnc As String, token As String, signedXml As XmlDocument, cfEnvia As String) As Task(Of String)
        Try
            ' Paso 1: Consultar el directorio del cliente
            GlobalVariables.urlClienteRecepcion = Await ObtenerDirectorioPorRNCAsync(rnc, token)

            ' Paso 2: Validar que se recibiÃ³ una URL vÃ¡lida
            If Not String.IsNullOrEmpty(GlobalVariables.urlClienteRecepcion) Then
                ' Asegura que la URL termine bien
                Dim urlDestino As String = GlobalVariables.urlClienteRecepcion.TrimEnd("/"c) & "/fe/recepcion/api/ecf"

                ' Enviar usando la URL del cliente con la ruta /fe/recepcion/api/ecf
                Return EnviarXMLFacturaFirmadoAUrl(signedXml, token, cfEnvia, rnc, urlDestino)
            Else
                Return "Cliente no tiene una URL de recepciÃ³n definida."
            End If

        Catch ex As Exception
            Return "Error al enviar factura al cliente: " & ex.Message
        End Try
    End Function
    Private Shared Function EnviarXMLFacturaFirmadoAUrl(ByVal signedXml As XmlDocument, ByVal token As String, cfEnvia As String, emp_rnc As String, ByVal url As String) As String
        Dim xmlFilePath As String = ObtenerRutaArchivo($"{emp_rnc}{cfEnvia}.xml")

        Try
            Using client As New HttpClient()
                client.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", token)

                Dim boundary As String = "----WebKitFormBoundary" & DateTime.Now.Ticks.ToString("x")
                Dim content As New MultipartFormDataContent(boundary)

                Dim fileStream As New FileStream(xmlFilePath, FileMode.Open, FileAccess.Read)
                Dim streamContent As New StreamContent(fileStream)
                streamContent.Headers.ContentType = New MediaTypeHeaderValue("text/xml")
                content.Add(streamContent, "xml", emp_rnc + cfEnvia + ".xml")

                Dim response = client.PostAsync(url, content).Result

                If response.IsSuccessStatusCode Then
                    Dim responseBody As String = response.Content.ReadAsStringAsync().Result
                    Dim jsonResponse As JObject = JObject.Parse(responseBody)
                    Return jsonResponse("trackId")?.ToString()
                Else
                    Dim errorResponse As String = response.Content.ReadAsStringAsync().Result
                    Dim jsonResponse As JObject = JObject.Parse(errorResponse)
                    Return "Error: " & jsonResponse("error").ToString() & " - Mensaje: " & jsonResponse("mensaje").ToString()
                End If
            End Using
        Catch ex As Exception
            Return "Error al enviar el XML firmado: " & ex.Message
        End Try
    End Function
    Private Shared Function FirmarXMLFactura(ByVal xmlFactura As XmlDocument, ByVal pathCert As String, ByVal passCert As String, ncfEnvia As String, emp_rnc As String) As XmlDocument
        Try
            ' Cargar el certificado
            Dim cert As New X509Certificate2(pathCert, passCert, X509KeyStorageFlags.Exportable)
            Dim exportedKeyMaterial = cert.PrivateKey.ToXmlString(True)
            Dim key As New RSACryptoServiceProvider(New CspParameters(24))
            key.PersistKeyInCsp = False
            key.FromXmlString(exportedKeyMaterial)

            ' Crear firma digital
            Dim signedXml As New SignedXml(xmlFactura)
            signedXml.SigningKey = key
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256"

            ' Crear la referencia y transformaciones
            Dim reference As New Reference()
            reference.Uri = ""
            reference.AddTransform(New XmlDsigEnvelopedSignatureTransform())
            reference.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256"
            signedXml.AddReference(reference)

            ' Agregar la informaciÃ³n de la clave
            Dim keyInfo As New KeyInfo()
            keyInfo.AddClause(New KeyInfoX509Data(cert))
            signedXml.KeyInfo = keyInfo

            ' Firmar el XML
            signedXml.ComputeSignature()
            Dim xmlDigitalSignature As XmlElement = signedXml.GetXml()

            ' Insertar la firma en el documento original
            xmlFactura.DocumentElement.AppendChild(xmlFactura.ImportNode(xmlDigitalSignature, True))

            ' Guardar el XML firmado en una carpeta con permisos de escritura
            'Dim appDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            'Dim folderPath As String = Path.Combine(appDataPath, "DashaDGII")
            'If Not Directory.Exists(folderPath) Then
            '    Directory.CreateDirectory(folderPath)
            'End If
            'Dim xmlFilePath As String = Path.Combine(folderPath, emp_rnc + ncfEnvia & ".xml")
            Dim xmlFilePath As String = ObtenerRutaArchivo($"{emp_rnc}{ncfEnvia}.xml")

            xmlFactura.Save(xmlFilePath)

            Return xmlFactura
        Catch ex As Exception
            Throw New Exception("Error al firmar el XML de la factura: " & ex.Message)
        End Try
    End Function
    Private Shared Function FirmarXMLFacturaExtendida(ByVal xmlFactura As XmlDocument, ByVal pathCert As String, ByVal passCert As String, ncfEnvia As String, emp_rnc As String) As XmlDocument
        Try
            ' Cargar el certificado
            Dim cert As New X509Certificate2(pathCert, passCert, X509KeyStorageFlags.Exportable)
            Dim exportedKeyMaterial = cert.PrivateKey.ToXmlString(True)
            Dim key As New RSACryptoServiceProvider(New CspParameters(24))
            key.PersistKeyInCsp = False
            key.FromXmlString(exportedKeyMaterial)

            ' Crear firma digital
            Dim signedXml As New SignedXml(xmlFactura)
            signedXml.SigningKey = key
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256"

            ' Crear la referencia y transformaciones
            Dim reference As New Reference()
            reference.Uri = ""
            reference.AddTransform(New XmlDsigEnvelopedSignatureTransform())
            reference.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256"
            signedXml.AddReference(reference)

            ' Agregar la informaciÃ³n de la clave
            Dim keyInfo As New KeyInfo()
            keyInfo.AddClause(New KeyInfoX509Data(cert))
            signedXml.KeyInfo = keyInfo

            ' Firmar el XML
            signedXml.ComputeSignature()
            Dim xmlDigitalSignature As XmlElement = signedXml.GetXml()

            ' Insertar la firma en el documento original
            xmlFactura.DocumentElement.AppendChild(xmlFactura.ImportNode(xmlDigitalSignature, True))

            ' Guardar el XML firmado en una carpeta con permisos seguros (AppData)
            'Dim appDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            'Dim carpetaDestino As String = Path.Combine(appDataPath, "DashaDGII\FacturasExtendidas")
            Dim carpetaDestino As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FacturasExtendidas")

            If Not Directory.Exists(carpetaDestino) Then
                Directory.CreateDirectory(carpetaDestino)
            End If

            Dim xmlFilePath As String = Path.Combine(carpetaDestino, emp_rnc + ncfEnvia & ".xml")
            xmlFactura.Save(xmlFilePath)

            Return xmlFactura
        Catch ex As Exception
            Throw New Exception("Error al firmar el XML de la factura: " & ex.Message)
        End Try
    End Function
    Private Shared Function ProcesoEnvioFacturaResumen(RncCliente As String, eNCF As String,
                                                   emp_codigo As String, suc_codigo As String,
                                                   facNumero As String, emp_rnc As String,
                                                   fac_forma As String, tfa_codigo As String,
                                                   TipoeCF As String) As String
        Dim paso As String = "Inicio"
        Try
            Dim xmlFacturaExtendido As XmlDocument
            Dim xmlFacturaResumen As XmlDocument

            paso = "GenerarXML extendido"
            xmlFacturaExtendido = GenerarXML(eNCF, cadenaConexion, facNumero, emp_codigo, suc_codigo, fac_forma, tfa_codigo, "", "")
            GuardarXml(xmlFacturaExtendido, "01-extendido.xml")

            paso = "Firmar XML extendido"
            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado
            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFacturaExtendida(xmlFacturaExtendido, pathCert, passCert, glbncfEnvia, emp_rnc)
            GuardarXml(xmlFacturaFirmada, "02-extendido-firmado.xml")

            paso = "Extraer datos firma"
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim encfGenerado As String = ObtenerENCFDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)

            paso = "Generar XML resumen"
            xmlFacturaResumen = GenerarXMLResumen(cadenaConexion, facNumero, emp_codigo, suc_codigo, fac_forma, tfa_codigo, codigoSeguridad, encfGenerado)
            GuardarXml(xmlFacturaResumen, "03-resumen.xml")

            paso = "Firmar XML resumen"
            Dim xmlFacturaFirmadaResumen As XmlDocument = FirmarXMLFactura(xmlFacturaResumen, pathCert, passCert, glbncfEnvia, emp_rnc)
            GuardarXml(xmlFacturaFirmadaResumen, "04-resumen-firmado.xml")

            paso = "Obtener token"
            Dim token As String = GlobalVariables.token
            If String.IsNullOrWhiteSpace(token) Then
                Dim detTok = If(String.IsNullOrWhiteSpace(Seguridad.UltimoErrorToken), "(sin detalle; revise LogsCliente [TOKEN])", Seguridad.UltimoErrorToken)
                Return "[ERROR] Paso: " & paso & ". Token vacío. " & detTok
            End If

            paso = "Enviar resumen"
            Dim respuestaJson As String = EnviarXMLFacturaFirmadoResumen(xmlFacturaFirmadaResumen, token, glbncfEnvia, emp_rnc)
            ' respuestaJson es lo que devuelve la DGII: {"codigo":1,"estado":"Aceptado",...}

            ' Procesar/guardar en tu BD con la respuesta JSON real
            paso = "ProcesarRespuestaAPIResumen"
            ProcesarRespuestaAPIResumen(RncCliente, encfGenerado, emp_codigo, suc_codigo, respuestaJson, facNumero, xmlFacturaFirmada, codigoSeguridad, fac_forma, tfa_codigo, "", fechaFirma, TipoeCF)

            ' Devolver al llamador SOLO el estado para mensajes rÃ¡pidos
            Try
                Dim jo = JObject.Parse(respuestaJson)
                Dim estado As String = If(jo("estado") IsNot Nothing, jo("estado").ToString(), "")
                If estado <> "" Then Return estado
            Catch
                ' no era JSON; devolvemos el texto crudo
            End Try
            Return respuestaJson
        Catch ex As Exception
            Dim msg As String = $"[ERROR] Paso: {paso}. {ex.Message}"
            LogDetallado(msg & Environment.NewLine & ex.ToString())
            Return msg
        End Try
    End Function

    Private Shared Function ProcesoEnvioFacturaResumenPOS(RncCliente As String, eNCF As String,
                                                   emp_codigo As String, suc_codigo As String,
                                                   ticket As String, emp_rnc As String,
                                                   usu_codigo As String, caja As String,
                                                   TipoeCF As String,
                                                   Optional esResBarFlow As Boolean = False) As String
        Dim paso As String = "Inicio"
        Try
            Dim xmlFacturaExtendido As XmlDocument
            Dim xmlFacturaResumen As XmlDocument

            paso = "GenerarXML extendido"
            xmlFacturaExtendido = GenerarXML(eNCF, cadenaConexion, ticket, emp_codigo, suc_codigo, "", "", usu_codigo, caja)
            GuardarXml(xmlFacturaExtendido, "01-extendido.xml")

            paso = "Firmar XML extendido"
            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado
            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFacturaExtendida(xmlFacturaExtendido, pathCert, passCert, glbncfEnvia, emp_rnc)
            GuardarXml(xmlFacturaFirmada, "02-extendido-firmado.xml")

            paso = "Extraer datos firma"
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim encfGenerado As String = ObtenerENCFDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)

            paso = "Generar XML resumen"
            xmlFacturaResumen = GenerarXMLResumenPOS(cadenaConexion, ticket, emp_codigo, suc_codigo, usu_codigo, caja, codigoSeguridad, encfGenerado)
            GuardarXml(xmlFacturaResumen, "03-resumen.xml")

            paso = "Firmar XML resumen"
            Dim xmlFacturaFirmadaResumen As XmlDocument = FirmarXMLFactura(xmlFacturaResumen, pathCert, passCert, glbncfEnvia, emp_rnc)
            GuardarXml(xmlFacturaFirmadaResumen, "04-resumen-firmado.xml")

            paso = "Obtener token"
            Dim token As String = GlobalVariables.token
            If String.IsNullOrWhiteSpace(token) Then
                Dim detTok = If(String.IsNullOrWhiteSpace(Seguridad.UltimoErrorToken), "(sin detalle; revise LogsCliente [TOKEN])", Seguridad.UltimoErrorToken)
                Return "[ERROR] Paso: " & paso & ". Token vacío. " & detTok
            End If

            paso = "Enviar resumen"
            Dim respuestaJson As String = EnviarXMLFacturaFirmadoResumen(xmlFacturaFirmadaResumen, token, glbncfEnvia, emp_rnc)
            ' respuestaJson es lo que devuelve la DGII: {"codigo":1,"estado":"Aceptado",...}

            ' Procesar/guardar en tu BD con la respuesta JSON real.
            ' signedXml = RFCE resumen firmado (el enviado a DGII), no el XML extendido 31/32.
            paso = "ProcesarRespuestaAPIResumen"
            ProcesarRespuestaAPIResumenPOS(RncCliente, encfGenerado, emp_codigo, suc_codigo, respuestaJson, ticket, xmlFacturaFirmadaResumen, codigoSeguridad, usu_codigo, caja, "", fechaFirma, esResBarFlow)

            ' Devolver al llamador SOLO el estado para mensajes rÃ¡pidos
            Try
                Dim jo = JObject.Parse(respuestaJson)
                Dim estado As String = If(jo("estado") IsNot Nothing, jo("estado").ToString(), "")
                If estado <> "" Then Return estado
            Catch
                ' no era JSON; devolvemos el texto crudo
            End Try
            Return respuestaJson
        Catch ex As Exception
            Dim msg As String = $"[ERROR] Paso: {paso}. {ex.Message}"
            LogDetallado(msg & Environment.NewLine & ex.ToString())
            Return msg
        End Try
    End Function


    ' ===== Utilidades de logging =====
    Private Shared Sub LogDetallado(msg As String)
        Try
            Dim dir As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                         "DRTechnology\FE\logs")
            Directory.CreateDirectory(dir)
            Dim file As String = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd") & ".log")
            System.IO.File.AppendAllText(file, DateTime.Now.ToString("HH:mm:ss") & " " & msg & Environment.NewLine)

        Catch
            ' evitar que falle el flujo por el log
        End Try
    End Sub
    Private Shared Sub GuardarXml(doc As XmlDocument, nombre As String)
        Try
            Dim dir As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trace")
            Directory.CreateDirectory(dir)
            doc.Save(Path.Combine(dir, nombre))
        Catch
        End Try
    End Sub
    Private Shared Function GuardarTexto(contenido As String, nombre As String) As String
        Dim base1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trace")
        Dim base2 = "C:\DRFE-logs\trace"
        Dim path1 = Path.Combine(base1, nombre)
        Dim path2 = Path.Combine(base2, nombre)
        Try : Directory.CreateDirectory(base1) : File.WriteAllText(path1, contenido, Encoding.UTF8) : Catch : End Try
        Try : Directory.CreateDirectory(base2) : File.WriteAllText(path2, contenido, Encoding.UTF8) : Catch : End Try
        Return path1
    End Function
    Private Shared Function ObtenerENCFDesdeXml(xmlDoc As XmlDocument) As String
        Dim nodoENCF As XmlNode = xmlDoc.SelectSingleNode("//eNCF")
        If nodoENCF IsNot Nothing Then
            Return nodoENCF.InnerText.Trim()
        Else
            Return "eNCF no encontrado"
        End If
    End Function
    Private Shared Function PostXmlCrudo(url As String, xmlFilePath As String, token As String) As String
        ' TLS moderno y sin 100-Continue
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls
        ServicePointManager.Expect100Continue = False
        ServicePointManager.CheckCertificateRevocationList = False ' (a veces el CRL hace que cierre)

        Dim xml As String = File.ReadAllText(xmlFilePath, Encoding.UTF8)
        Dim bytes As Byte() = Encoding.UTF8.GetBytes(xml)

        Dim req = CType(WebRequest.Create(url), HttpWebRequest)
        req.Method = "POST"
        req.ProtocolVersion = HttpVersion.Version10 ' HTTP/1.0
        req.KeepAlive = False                       ' Connection: close
        req.SendChunked = False
        req.ContentType = "application/xml; charset=utf-8"
        req.Accept = "application/json"
        req.ContentLength = bytes.Length
        req.Headers(HttpRequestHeader.Authorization) = "Bearer " & token

        ' Si sospechas del proxy, prueba primero SIN proxy:
        ' req.Proxy = Nothing

        Using s = req.GetRequestStream()
            s.Write(bytes, 0, bytes.Length)
        End Using

        Try
            Using resp = CType(req.GetResponse(), HttpWebResponse)
                Using r As New StreamReader(resp.GetResponseStream(), Encoding.UTF8)
                    Return r.ReadToEnd() ' <-- aquÃ­ deberÃ­as ver el JSON con trackId
                End Using
            End Using
        Catch ex As WebException
            Dim body As String = ""
            If ex.Response IsNot Nothing Then
                Using r As New StreamReader(ex.Response.GetResponseStream())
                    body = r.ReadToEnd()
                End Using
            End If
            Throw New Exception("HTTP error: " & ex.Message & vbCrLf & body)
        End Try
    End Function
    Private Shared Function EnviarXMLFacturaFirmadoResumen(
    ByVal signedXml As XmlDocument,
    ByVal token As String,
    cfEnvia As String,
    emp_rnc As String) As String

        Dim url As String = GlobalVariables.urlfc_base & "recepcionfc/api/recepcion/ecf"
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls
        ServicePointManager.Expect100Continue = False

        Try
            Using client As New HttpClient()
                client.DefaultRequestHeaders.Accept.Clear()
                client.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", token)

                Dim xmlBytes As Byte()
                Using ms As New MemoryStream()
                    signedXml.Save(ms)
                    xmlBytes = ms.ToArray()
                End Using

                Using content As New MultipartFormDataContent()
                    Dim fileContent As New ByteArrayContent(xmlBytes)
                    fileContent.Headers.ContentType = New MediaTypeHeaderValue("application/xml")
                    Dim fileName As String = emp_rnc & cfEnvia & ".xml"
                    content.Add(fileContent, "xml", fileName)

                    Dim resp = client.PostAsync(url, content).GetAwaiter().GetResult()
                    Dim body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()

                    GuardarTexto($"URL: {url}{Environment.NewLine}" &
                             $"HTTP {(CInt(resp.StatusCode))} {resp.ReasonPhrase}{Environment.NewLine}" &
                             "Body:" & Environment.NewLine & body,
                             "envio-resumen-http.txt")

                    ' SIEMPRE devolver el body (JSON de la DGII), incluso si hay error HTTP
                    If Not resp.IsSuccessStatusCode Then
                        ' opcional: incluir cÃ³digo HTTP en el JSON para depurar
                        Return $"{{""httpStatus"":{CInt(resp.StatusCode)},""body"":{body}}}"
                    End If

                    Return body
                End Using
            End Using
        Catch ex As Exception
            GuardarTexto("TRANSPORTE/EX: " & Environment.NewLine & ex.ToString(), "envio-resumen-exception.txt")
            ' devolver mensaje JSON-like para no romper al consumidor
            Return $"{{""error"":""{ex.Message.Replace("""", "'")}""}}"
        End Try
    End Function

    '    Private Shared Function EnviarXMLFacturaFirmadoResumen(ByVal signedXml As XmlDocument,
    '                                                       ByVal token As String,
    '                                                       cfEnvia As String,
    '                                                       emp_rnc As String) As String
    '        Dim url As String = GlobalVariables.url_base & "recepcionfc/api/recepcion/ecf"
    '        Dim carpetaDestino As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FacturasExtendidas")
    '        '' Dim xmlFilePath As String = Path.Combine(carpetaDestino, emp_rnc & cfEnvia & ".xml")
    '        Dim xmlFilePath As String = ObtenerRutaArchivo($"{emp_rnc}{cfEnvia}.xml")

    '        ' TLS (por si el host lo exige)
    '        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls

    '        Try
    '            Directory.CreateDirectory(carpetaDestino)

    '            ' ===== Log pre-envÃ­o =====
    '            Dim pre = "POST " & url & Environment.NewLine &
    '          "Archivo: " & xmlFilePath & " (existe=" & File.Exists(xmlFilePath) &
    '          ", len=" & If(File.Exists(xmlFilePath), New FileInfo(xmlFilePath).Length, 0) & ")"
    '            GuardarTexto(pre, "00-ANTES_DE_ENVIAR.txt")

    '            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls
    '            ServicePointManager.Expect100Continue = False

    '            Dim bytes As Byte() = File.ReadAllBytes(xmlFilePath) ' fija Content-Length

    '            Dim handler As New HttpClientHandler With {
    '  .AutomaticDecompression = DecompressionMethods.None, ' por si acaso
    '  .UseProxy = True,
    '  .Proxy = WebRequest.DefaultWebProxy,
    '  .UseDefaultCredentials = True
    '}

    '            Using client As New HttpClient(handler)
    '                client.Timeout = TimeSpan.FromSeconds(60)
    '                client.DefaultRequestHeaders.Accept.Clear()
    '                client.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
    '                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", token)

    '                Dim fileContent As New ByteArrayContent(bytes)
    '                fileContent.Headers.ContentType = New MediaTypeHeaderValue("application/xml")
    '                fileContent.Headers.ContentDisposition = New ContentDispositionHeaderValue("form-data") With {
    '    .Name = "xml",                          ' <-- cambia a "archivo"/"file" si tu API lo exige
    '    .FileName = Path.GetFileName(xmlFilePath)
    '  }

    '                Using content As New MultipartFormDataContent()
    '                    content.Add(fileContent)

    '                    Dim req As New HttpRequestMessage(HttpMethod.Post, url)
    '                    req.Version = New Version(1, 0)         ' HTTP/1.0
    '                    req.Headers.ConnectionClose = True      ' Connection: close
    '                    req.Headers.ExpectContinue = False
    '                    req.Content = content

    '                    Dim resp = client.SendAsync(req).GetAwaiter().GetResult()
    '                    Dim body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    '                    GuardarTexto($"HTTP {CInt(resp.StatusCode)} {resp.ReasonPhrase}{Environment.NewLine}{body}",
    '                 If(resp.IsSuccessStatusCode, "10-DESPUES_STATUSLINE-OK.txt", "10-DESPUES_STATUSLINE-ERROR.txt"))


    '                    If Not resp.IsSuccessStatusCode Then Throw New Exception($"HTTP {(CInt(resp.StatusCode))} {resp.ReasonPhrase}. Body: {body}")

    '                    Dim trackId = ExtraerTrackIdSeguro(body)
    '                    If String.IsNullOrWhiteSpace(trackId) Then Throw New Exception("Sin trackId. Body: " & body)
    '                    Return trackId
    '                End Using
    '            End Using

    '        Catch ex As Exception
    '            ' TambiÃ©n lo dejamos en log
    '            GuardarTexto("EXCEPCIÃ“N FINAL: " & Environment.NewLine & ex.ToString(), "envio-resumen-exception.txt")
    '            Throw New Exception("Error al enviar el XML de la factura firmada: " & ex.Message)
    '        End Try
    '    End Function
    Private Shared Function ExtraerTrackIdSeguro(body As String) As String
        If String.IsNullOrWhiteSpace(body) Then Return ""
        Try
            Dim jo = JObject.Parse(body)
            Dim v = CStr(jo.SelectToken("trackId"))
            If String.IsNullOrWhiteSpace(v) Then v = CStr(jo.SelectToken("trackID"))
            If String.IsNullOrWhiteSpace(v) Then v = CStr(jo.SelectToken("idSeguimiento"))
            If Not String.IsNullOrWhiteSpace(v) Then Return v
        Catch
        End Try
        Dim m = System.Text.RegularExpressions.Regex.Match(body, """trackId""\s*:\s*""([^""]+)""", Text.RegularExpressions.RegexOptions.IgnoreCase)
        If m.Success Then Return m.Groups(1).Value
        m = System.Text.RegularExpressions.Regex.Match(body, """trackID""\s*:\s*""([^""]+)""", Text.RegularExpressions.RegexOptions.IgnoreCase)
        If m.Success Then Return m.Groups(1).Value
        Return ""
    End Function
    Private Shared Function ValidarXmlContraXsd(xmlDoc As XmlDocument, xsdPath As String) As Boolean
        Dim settings As New XmlReaderSettings()
        settings.Schemas.Add(Nothing, xsdPath)
        settings.ValidationType = ValidationType.Schema

        Dim errores As New List(Of String)

        AddHandler settings.ValidationEventHandler,
            Sub(sender As Object, e As ValidationEventArgs)
                errores.Add($"[{e.Severity}] {e.Message}")
            End Sub

        Try
            Using reader As XmlReader = XmlReader.Create(New XmlNodeReader(xmlDoc), settings)
                While reader.Read()
                End While
            End Using

            If errores.Count > 0 Then
                Dim errorText = String.Join(vbCrLf, errores)
                Throw New ApplicationException("Errores de validaciÃ³n XML contra XSD:" & vbCrLf & errorText)
                Return False
            End If

            Return True
        Catch ex As Exception
            Throw New ApplicationException("Error validando XML contra XSD: " & ex.Message)

            Return False
        End Try
    End Function
    Private Shared Sub ProcesarRespuestaAPIResumen(
    RncCliente As String, encfgenerado As String,
    emp_codigo As String, suc_codigo As String,
    resestado As String, facNumero As String,
    signedXml As XmlDocument, codigoseguridad As String,
    fac_forma As String, tfa_codigo As String,
    sup_codigo As String, fechaFirma As DateTime, TipoeCF As String)

        Try
            Dim estado As String = "Desconocido"
            Dim encf As String = If(encfgenerado, String.Empty)
            Dim codigoMensaje As String = ""
            Dim mensajeError As String = ""
            Dim secuenciaUtilizada As Boolean? = Nothing

            ' ------------------ Parseo robusto ------------------
            Try
                Dim root = Newtonsoft.Json.Linq.JObject.Parse(resestado)

                ' Si viene con httpStatus/body, bajamos a body
                Dim payload As Newtonsoft.Json.Linq.JToken = root
                If root.SelectToken("body") IsNot Nothing Then
                    payload = root.SelectToken("body")
                End If

                ' Extraer campos estÃ¡ndar
                If payload.SelectToken("estado") IsNot Nothing Then
                    estado = CStr(payload.SelectToken("estado"))
                End If
                If payload.SelectToken("encf") IsNot Nothing Then
                    encf = CStr(payload.SelectToken("encf"))
                End If
                If payload.SelectToken("codigo") IsNot Nothing Then
                    codigoMensaje = CStr(payload.SelectToken("codigo"))
                End If
                If payload.SelectToken("secuenciaUtilizada") IsNot Nothing Then
                    secuenciaUtilizada = CBool(payload.SelectToken("secuenciaUtilizada"))
                End If

                ' Mensajes puede ser arreglo u objeto/str
                Dim jt = payload.SelectToken("mensajes")
                If jt IsNot Nothing Then
                    If jt.Type = Newtonsoft.Json.Linq.JTokenType.Array Then
                        Dim partes As New List(Of String)
                        For Each m In jt
                            Dim cod = (If(m("codigo"), "")).ToString()
                            Dim val = (If(m("valor"), m.ToString())).ToString()
                            If Not String.IsNullOrWhiteSpace(cod) Then
                                partes.Add($"[{cod}] {val}")
                            Else
                                partes.Add(val)
                            End If
                        Next
                        mensajeError = String.Join(" | ", partes)
                    Else
                        mensajeError = jt.ToString()
                    End If
                End If
            Catch
                ' Si no era JSON vÃ¡lido, dejamos el texto crudo como mensaje
                mensajeError = resestado
            End Try
            ' ----------------------------------------------------

            Dim seqResumen As Boolean = If(secuenciaUtilizada.HasValue, secuenciaUtilizada.Value, False)
            Dim aceptadoResumen As Integer = If(Helper.EsEstadoAceptadoDgii(estado), 1, 0)
            Helper.AjustarPersistenciaPendiente(aceptadoResumen, seqResumen, estado)
            secuenciaUtilizada = seqResumen

            ' -------- BitÃ¡cora: siempre registrar --------
            ' Usa el estado parseado y el listado de mensajes
            Helper.InsertarBitacoraDGII(
            cadenaConexion,
            emp_codigo,
            facNumero,
            estado,                 ' Estado: Aceptado/Rechazado/Desconocido
            "",                     ' detalle extra si lo usas
            If(String.IsNullOrEmpty(codigoMensaje), "", codigoMensaje),
            mensajeError,
            encf,                   ' guarda el eNCF si vino
            DateTime.Now, "", "",
            Helper.ResolverTipoBitacoraDgii(TipoeCF, False)
        )

            ' -------- ActualizaciÃ³n de datos locales --------
            If aceptadoResumen = 1 Then
                ' Aceptado => actualiza como ya lo hacÃ­as
                Dim parametros As SqlParameter() = {
                New SqlParameter("@emp_codigo", emp_codigo),
                New SqlParameter("@suc_codigo", suc_codigo),
                New SqlParameter("@fac_numero", facNumero),
                New SqlParameter("@eNCF", encf),
                New SqlParameter("@fac_forma", fac_forma),
                New SqlParameter("@tfa_codigo", tfa_codigo),
                New SqlParameter("@codigoseguridad", codigoseguridad),
                New SqlParameter("@fechafirma", fechaFirma),
                New SqlParameter("@aceptado", 1),
                New SqlParameter("@secuenciaUtilizada", secuenciaUtilizada)}


                Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGI", parametros)

                ' Enviar al cliente si aplica (sin async/await)
                If RncCliente.Trim().Length > 0 Then
                    Try
                        Dim res As String = EnviarFacturaClienteSiTieneUrlAsync(
                        RncCliente, GlobalVariables.token, signedXml, facNumero
                    ).GetAwaiter().GetResult()
                        Helper.RegistrarLogCliente($"Factura {facNumero} enviada al cliente {RncCliente}. Resultado: {res}")
                    Catch ex As Exception
                        Helper.RegistrarLogCliente($"Error al enviar factura {facNumero} al cliente {RncCliente}: {ex.Message}")
                    End Try
                End If

            Else
                ' Rechazado / Desconocido => marca como enviado/no aceptado y conserva eNCF si la API lo devolviÃ³
                ' Si ya tienes un SP para rechazos, Ãºsalo aquÃ­.
                ' Ejemplo genÃ©rico:
                Dim parametros As SqlParameter() = {
                New SqlParameter("@emp_codigo", emp_codigo),
                New SqlParameter("@suc_codigo", suc_codigo),
                New SqlParameter("@fac_numero", facNumero),
                New SqlParameter("@eNCF", encf),
                New SqlParameter("@fac_forma", fac_forma),
                New SqlParameter("@tfa_codigo", tfa_codigo),
                New SqlParameter("@codigoseguridad", codigoseguridad),
                New SqlParameter("@fechafirma", fechaFirma),
                New SqlParameter("@aceptado", 0),
                New SqlParameter("@secuenciaUtilizada", secuenciaUtilizada)
            }
                ' Cambia el nombre del SP por el tuyo (ej. SP_UPDATEDATOSDGI)
                Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, "SP_UPDATEDATOSDGI", parametros)
            End If

            ' âœ… Guardar XML + Monto (solo si fue aceptado)
            Try
                Dim xmlText As String = If(signedXml IsNot Nothing, signedXml.OuterXml, "")
                GuardarXmlYTotalEnTablaSegunTipo(
                        cadenaConexion:=cadenaConexion,
                        tipoECF:=TipoeCF,
                        emp_codigo:=emp_codigo,
                        suc_codigo:=suc_codigo,
                        numeroDoc:=facNumero,
                        xmlText:=xmlText,
                        signedXml:=signedXml,
                        fechaEnvio:=DateTime.Now,
                        isNotaCredito:=False,
                        fac_forma:=fac_forma,
                        tfa_codigo:=tfa_codigo
                    )
            Catch ex As Exception
                LogDetallado("GuardarXmlYTotal (Resumen) EX: " & ex.Message)
            End Try

        Catch ex As Exception
            ' Nunca dejes que salga una excepciÃ³n a Delphi
            LogDetallado("ProcesarRespuestaAPIResumen EX: " & ex.ToString())
            Try
                Helper.InsertarBitacoraDGII(
                cadenaConexion, emp_codigo, facNumero,
                "EXCEPCION", "", "", ex.Message, "", DateTime.Now, "", "",
                Helper.ResolverTipoBitacoraDgii(TipoeCF, False)
            )
            Catch
                ' swallow
            End Try
        End Try
    End Sub

    Private Shared Sub ProcesarRespuestaAPIResumenPOS(
    RncCliente As String, encfgenerado As String,
    emp_codigo As String, suc_codigo As String,
    resestado As String, ticket As String,
    signedXml As XmlDocument, codigoseguridad As String,
    usu_codigo As String, caja As String,
    sup_codigo As String, fechaFirma As DateTime,
    esResBarFlow As Boolean)

        Try
            Dim spUpdateDatosTicket As String = If(esResBarFlow, "SP_UPDATEDATOSDGIRESBAR", "SP_UPDATEDATOSDGIPOS")
            Dim estado As String = "Desconocido"
            Dim encf As String = If(encfgenerado, String.Empty)
            Dim codigoMensaje As String = ""
            Dim mensajeError As String = ""
            Dim secuenciaUtilizada As Boolean? = Nothing
            Dim aceptado As Integer = 0

            ' ------------------ Parseo robusto ------------------
            Try
                Dim root = Newtonsoft.Json.Linq.JObject.Parse(resestado)

                ' Si viene con httpStatus/body, bajamos a body
                Dim payload As Newtonsoft.Json.Linq.JToken = root
                If root.SelectToken("body") IsNot Nothing Then
                    payload = root.SelectToken("body")
                End If

                ' Extraer campos estÃ¡ndar
                If payload.SelectToken("estado") IsNot Nothing Then
                    estado = CStr(payload.SelectToken("estado"))
                End If
                If payload.SelectToken("encf") IsNot Nothing Then
                    encf = CStr(payload.SelectToken("encf"))
                End If
                If payload.SelectToken("codigo") IsNot Nothing Then
                    codigoMensaje = CStr(payload.SelectToken("codigo"))
                End If
                If payload.SelectToken("secuenciaUtilizada") IsNot Nothing Then
                    secuenciaUtilizada = CBool(payload.SelectToken("secuenciaUtilizada"))
                End If

                ' Mensajes puede ser arreglo u objeto/str
                Dim jt = payload.SelectToken("mensajes")
                If jt IsNot Nothing Then
                    If jt.Type = Newtonsoft.Json.Linq.JTokenType.Array Then
                        Dim partes As New List(Of String)
                        For Each m In jt
                            Dim cod = (If(m("codigo"), "")).ToString()
                            Dim val = (If(m("valor"), m.ToString())).ToString()
                            If Not String.IsNullOrWhiteSpace(cod) Then
                                partes.Add($"[{cod}] {val}")
                            Else
                                partes.Add(val)
                            End If
                        Next
                        mensajeError = String.Join(" | ", partes)
                    Else
                        mensajeError = jt.ToString()
                    End If
                End If
            Catch
                ' Si no era JSON vÃ¡lido, dejamos el texto crudo como mensaje
                mensajeError = resestado
            End Try
            ' ----------------------------------------------------

            Dim seqResumenPos As Boolean = If(secuenciaUtilizada.HasValue, secuenciaUtilizada.Value, False)
            aceptado = If(Helper.EsEstadoAceptadoDgii(estado), 1, 0)
            Helper.AjustarPersistenciaPendiente(aceptado, seqResumenPos, estado)
            secuenciaUtilizada = seqResumenPos

            ' -------- BitÃ¡cora: siempre registrar --------
            ' Usa el estado parseado y el listado de mensajes
            Helper.InsertarBitacoraDGII(
            cadenaConexion,
            emp_codigo,
            ticket,
            estado,                 ' Estado: Aceptado/Rechazado/Desconocido
            "",                     ' detalle extra si lo usas
            If(String.IsNullOrEmpty(codigoMensaje), "", codigoMensaje),
            mensajeError,
            encf,                   ' guarda el eNCF si vino
            DateTime.Now, usu_codigo, caja,
            Helper.ResolverTipoBitacoraTicket(esResBarFlow)
        )

            ' âœ… Monto/XML: Factura_RestBar o Montos_Ticket siempre (como resumen administrativo / Facturas.DGII_MontoEnviado).
            ' esResBarFlow explÃ­cito: no depender del Shared isResBar (Finally de EnviarFacturaResumenResBar puede interferir en COM).
            If esResBarFlow Then
                Try
                    Helper.RegistrarLogCliente($"[Entro a esResBarFlow Resumen] INICIO ticket={ticket} ")

                    Dim xmlTextRes As String = If(signedXml IsNot Nothing, signedXml.OuterXml, "")
                    Dim monto As Decimal = ExtraerMontoTotalDesdeXml(signedXml)
                    Helper.RegistrarLogCliente($"[UpdateFacturaRestBar Resumen] INICIO ticket={ticket} monto={monto} xmlLen={xmlTextRes.Length} aceptado={aceptado}")
                    Dim idTicketR As Integer
                    Dim idCajeroR As Integer
                    Dim idCajaR As Integer
                    Dim idEmpR As Integer
                    Dim idSucR As Integer
                    If Not Integer.TryParse((ticket & "").Trim(), idTicketR) OrElse
                       Not Integer.TryParse((usu_codigo & "").Trim(), idCajeroR) OrElse
                       Not Integer.TryParse((caja & "").Trim(), idCajaR) OrElse
                       Not Integer.TryParse((emp_codigo & "").Trim(), idEmpR) OrElse
                       Not Integer.TryParse((suc_codigo & "").Trim(), idSucR) Then
                        Helper.RegistrarLogCliente("[UpdateFacturaRestBar Resumen] OMITIDO: ids numÃ©ricos invÃ¡lidos ticket=" & ticket & " usu=" & usu_codigo & " caja=" & caja)
                        LogDetallado("UpdateFacturaRestBar (Resumen POS) omitido: parÃ¡metro numÃ©rico invÃ¡lido. ticket=" & ticket & " usu=" & usu_codigo & " caja=" & caja)
                    Else
                        UpdateFacturaRestBar(
                            cadenaConexion,
                            idEmpR,
                            idSucR,
                            idTicketR,
                            idCajeroR,
                            idCajaR,
                            monto,
                            xmlTextRes,
                            DateTime.Now)
                    End If
                Catch ex As Exception
                    Helper.RegistrarLogCliente("[UpdateFacturaRestBar Resumen] ERROR: " & ex.Message)
                    LogDetallado("GuardarXmlYTotal (Factura_RestBar Resumen POS) EX: " & ex.Message)
                End Try
            Else
                Try
                  Helper.RegistrarLogCliente($"[Entro a no es restbar] INICIO ticket={esResBarFlow} ")

                    Dim xmlTextRes As String = If(signedXml IsNot Nothing, signedXml.OuterXml, "")
                    Dim monto As Decimal = ExtraerMontoTotalDesdeXml(signedXml)
                    UpdateMontosTicket(
                        cadena:=cadenaConexion,
                        usuCodigo:=Convert.ToInt32(usu_codigo),
                        fecha:=fechaFirma,
                        caja:=Convert.ToInt32(caja),
                        ticket:=Convert.ToInt32(ticket),
                        monto:=monto,
                        xmlText:=xmlTextRes,
                        fechaEnvio:=fechaFirma)
                Catch ex As Exception
                    LogDetallado("GuardarXmlYTotal (Montos_Ticket Resumen POS) EX: " & ex.Message)
                End Try
            End If



            ' -------- ActualizaciÃ³n de datos locales --------
            If String.Equals(estado, "Aceptado", StringComparison.OrdinalIgnoreCase) Or
                String.Equals(estado, "Aceptado Condicional", StringComparison.OrdinalIgnoreCase) Then

                aceptado = 1
                Dim parametros As SqlParameter() = {
                New SqlParameter("@emp_codigo", emp_codigo),
                New SqlParameter("@suc_codigo", suc_codigo),
                New SqlParameter("@ticket", ticket),
                New SqlParameter("@eNCF", encf),
                New SqlParameter("@usu_codigo", usu_codigo),
                New SqlParameter("@caja", caja),
                New SqlParameter("@codigoseguridad", codigoseguridad),
                New SqlParameter("@fechafirma", fechaFirma),
                New SqlParameter("@aceptado", 1),
                New SqlParameter("@secuenciaUtilizada", secuenciaUtilizada)}

                Helper.RegistrarLogCliente("Entro al ticket " + ticket)
                Helper.RegistrarLogCliente("Entro al codigoseguridad " + codigoseguridad)
                Helper.RegistrarLogCliente("Entro al usu_codigo " + usu_codigo)
                Helper.RegistrarLogCliente("Entro al fechaFirma " + fechaFirma.ToString())

                Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, spUpdateDatosTicket, parametros)
                Helper.RegistrarLogCliente($"[SP Resumen POS] OK dbo.{spUpdateDatosTicket} rama=ACEPTADO ticket={ticket}")

                ' Enviar al cliente si aplica (sin async/await)
                If RncCliente.Trim().Length > 0 Then
                    Try
                        Dim res As String = EnviarFacturaClienteSiTieneUrlAsync(
                        RncCliente, GlobalVariables.token, signedXml, ticket
                    ).GetAwaiter().GetResult()
                        Helper.RegistrarLogCliente($"Factura {ticket} enviada al cliente {RncCliente}. Resultado: {res}")
                    Catch ex As Exception
                        Helper.RegistrarLogCliente($"Error al enviar factura {ticket} al cliente {RncCliente}: {ex.Message}")
                    End Try
                End If

            Else
                ' Rechazado / Desconocido => marca como enviado/no aceptado y conserva eNCF si la API lo devolviÃ³
                ' Si ya tienes un SP para rechazos, Ãºsalo aquÃ­.
                ' Ejemplo genÃ©rico:
                Dim parametros As SqlParameter() = {
                New SqlParameter("@emp_codigo", emp_codigo),
                New SqlParameter("@suc_codigo", suc_codigo),
                New SqlParameter("@ticket", ticket),
                New SqlParameter("@eNCF", encf),
                New SqlParameter("@usu_codigo", usu_codigo),
                New SqlParameter("@caja", caja),
                New SqlParameter("@codigoseguridad", codigoseguridad),
                New SqlParameter("@fechafirma", fechaFirma),
                New SqlParameter("@aceptado", 0),
                New SqlParameter("@secuenciaUtilizada", secuenciaUtilizada)
            }

                Helper.RegistrarLogCliente($"[SP Resumen POS] INICIO dbo.{spUpdateDatosTicket} rama=RECHAZO/OTRO estado={estado} ticket={ticket} emp={emp_codigo} esResBarFlow={esResBarFlow}")
                Try
                    Helper.EjecutarProcedimientoAlmacenado(cadenaConexion, spUpdateDatosTicket, parametros)
                    Helper.RegistrarLogCliente($"[SP Resumen POS] OK dbo.{spUpdateDatosTicket} rama=RECHAZO/OTRO ticket={ticket}")
                Catch ex As Exception
                    Helper.RegistrarLogCliente($"[SP Resumen POS] ERROR dbo.{spUpdateDatosTicket} rama=RECHAZO/OTRO ticket={ticket}: {ex.Message}")
                    LogDetallado("[SP Resumen POS] ERROR dbo." & spUpdateDatosTicket & " " & ex.ToString())
                    Throw
                End Try
            End If


        Catch ex As Exception
            ' Nunca dejes que salga una excepciÃ³n a Delphi
            LogDetallado("ProcesarRespuestaAPIResumen EX: " & ex.ToString())
            Try
                Helper.InsertarBitacoraDGII(
                cadenaConexion, emp_codigo, ticket,
                "EXCEPCION", "", "", ex.Message, "", DateTime.Now, usu_codigo, caja,
                Helper.ResolverTipoBitacoraTicket(esResBarFlow)
            )
            Catch
                ' swallow
            End Try
        End Try
    End Sub

    Public Shared Function GenerarXMLResumenPOS(cadenaConexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String,
                               ByVal usu_codigo As String, ByVal caja As String, codigoSeguridad As String, encfgenerado As String) As XmlDocument
        Dim montoGravado As String = ""
        Dim montoGravado1 As String = ""
        Dim montoGravado2 As String = ""
        Dim itbisTotal As String = ""
        Dim totalFactura As String = ""
        Dim valorpagar As String = ""
        Dim montopagado As String = ""
        Dim montoexento As String = ""
        Dim unidadMedida As String = ""
        Dim precioUnitarioItem As String = ""
        Dim ultimaLineaDetalle As String = "1" ' Variable para la Ãºltima lÃ­nea del detalle
        Dim cantidadItem As String = ""
        Dim itbisTotal1 As String = ""
        Dim itbisTotal2 As String = ""
        Dim montoItem As String = ""
        Dim montoTotal As String = ""

        Dim DecmontoTotal As Decimal
        Dim DecGravado As Decimal
        Dim DecExento As Decimal
        Dim DecItbis As Decimal
        Dim DecItbisDGI As Decimal

        Try
            Dim xmlDoc As New XmlDocument()
            xmlDoc.PreserveWhitespace = True

            Dim xmlDeclaration As XmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", Nothing)
            xmlDoc.AppendChild(xmlDeclaration)

            Dim root As XmlElement = xmlDoc.CreateElement("RFCE")
            xmlDoc.AppendChild(root)

            Dim encabezado As XmlElement = xmlDoc.CreateElement("Encabezado")
            root.AppendChild(encabezado)

            Dim consultaFactura As String
            If isResBar Then
                consultaFactura = ConsultaFacturaXmlResBarSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)
            Else
                consultaFactura = ConsultaFacturaXmlPosSp(emp_codigo, suc_codigo, facNumero, usu_codigo, caja)
            End If
            Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)

            If facturaReader.Read() Then
                encabezado.AppendChild(Helper.CreateElement(xmlDoc, "Version", "1.0"))

                ' ID DOC
                Dim idDoc As XmlElement = xmlDoc.CreateElement("IdDoc")
                encabezado.AppendChild(idDoc)

                Helper.AddIfValid(xmlDoc, idDoc, facturaReader, "TipoeCF")
                idDoc.AppendChild(Helper.CreateElement(xmlDoc, "eNCF", encfgenerado))
                idDoc.AppendChild(Helper.CreateElement(xmlDoc, "TipoIngresos", "01"))
                Helper.AddIfValid(xmlDoc, idDoc, facturaReader, "TipoPago")

                ' Nodo TablaFormasPago
                Dim consultaFormaPago As String
                If isResBar Then
                    consultaFormaPago = " SELECT * FROM vwFormasPagoXMLRESBAR WHERE ticket=" & facNumero & " AND usu_codigo='" & usu_codigo & "' AND caja='" & caja & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                Else
                    consultaFormaPago = " SELECT * FROM vwFormasPagoXMLPOS WHERE ticket=" & facNumero & " AND usu_codigo='" & usu_codigo & "' AND caja='" & caja & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                End If
                Dim formaPagoReader = EjecutarConsultaReader(cadenaConexion, consultaFormaPago)
                If formaPagoReader.Read() Then
                    Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
                    idDoc.AppendChild(tablaFormasPagoNode)

                    ' AÃ±adir las formas de pago
                    Do
                        Dim formaNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
                        montopagado = Decimal.Parse(formaPagoReader("MontoPago").ToString()).ToString("F2")

                        tablaFormasPagoNode.AppendChild(formaNode)
                        Helper.AddElement(xmlDoc, formaNode, "FormaPago", formaPagoReader("FormaPago").ToString())
                        Helper.AddElement(xmlDoc, formaNode, "MontoPago", montopagado)

                    Loop While formaPagoReader.Read()
                End If
                formaPagoReader.Close()

                ' EMISOR
                Dim emisor As XmlElement = xmlDoc.CreateElement("Emisor")
                encabezado.AppendChild(emisor)

                Helper.AddElement(xmlDoc, emisor, "RNCEmisor", facturaReader("emp_rnc").ToString())
                Helper.AddElement(xmlDoc, emisor, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())

                Dim f? As DateTime = SafeDate(facturaReader("fac_fecha"), "fac_fecha")
                If f.HasValue Then
                    Helper.AddElement(xmlDoc, emisor, "FechaEmision", f.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                End If

                'Helper.AddElement(xmlDoc, emisor, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

                ' COMPRADOR
                Dim comprador As XmlElement = xmlDoc.CreateElement("Comprador")
                encabezado.AppendChild(comprador)
                Helper.AddElement(xmlDoc, comprador, "RNCComprador", facturaReader("cli_rnc").ToString())
                Helper.AddElement(xmlDoc, comprador, "RazonSocialComprador", facturaReader("cli_nombre").ToString())

                ' TOTALES
                Dim totales As XmlElement = xmlDoc.CreateElement("Totales")
                encabezado.AppendChild(totales)


                ' Guardar los valores en variables antes de cerrar el reader
                '' DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis_dgi").ToString())
                DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis").ToString())
                DecItbis = Decimal.Parse(facturaReader("fac_itbis").ToString())
                itbisTotal = Decimal.Parse(facturaReader("fac_itbis").ToString()).ToString("F2")
                itbisTotal1 = Decimal.Parse(facturaReader("det_totalitbis_1").ToString()).ToString("F2")
                itbisTotal2 = Decimal.Parse(facturaReader("det_totalitbis_2").ToString()).ToString("F2")

                totalFactura = Decimal.Parse(facturaReader("fac_total").ToString()).ToString("F2")
                valorpagar = Decimal.Parse(facturaReader("ValorPagar").ToString()).ToString("F2")
                DecGravado = If(IsDBNull(facturaReader("monto_grabado")), 0D, Convert.ToDecimal(facturaReader("monto_grabado")))

                montoGravado = DecGravado.ToString("F2")

                montoGravado1 = If(IsDBNull(facturaReader("monto_grabado1")), 0D, Convert.ToDecimal(facturaReader("monto_grabado1"))).ToString("F2")
                montoGravado2 = If(IsDBNull(facturaReader("monto_grabado2")), 0D, Convert.ToDecimal(facturaReader("monto_grabado2"))).ToString("F2")

                If montoGravado <> "0" And montoGravado <> "0.00" Then
                    Helper.AddElement(xmlDoc, totales, "MontoGravadoTotal", montoGravado)
                End If

                If (Not String.IsNullOrEmpty(montoGravado1)) And (Not montoGravado1 = "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "MontoGravadoI1", montoGravado1)
                End If

                If (Not String.IsNullOrEmpty(montoGravado2)) And (Not montoGravado2 = "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "MontoGravadoI2", montoGravado2)
                End If

                ' Verificar si el monto exento no es igual a 0.00 antes de agregar el elementoltw1
                DecExento = Decimal.Parse(facturaReader("monto_exento").ToString())

                If (Not String.IsNullOrEmpty(DecExento.ToString("F2"))) And (Not DecExento.ToString("F2") = "0.00") Then
                    montoexento = DecExento.ToString("F2")
                    Helper.AddElement(xmlDoc, totales, "MontoExento", montoexento)
                End If

                ' RFCE: no incluir nodos ITBIS1/ITBIS2 (tasas); solo montos TotalITBIS* en Totales.

                If (Not String.IsNullOrEmpty(itbisTotal)) And (itbisTotal <> "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "TotalITBIS", itbisTotal)
                End If

                If (Not String.IsNullOrEmpty(itbisTotal1)) And (itbisTotal1 <> "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "TotalITBIS1", itbisTotal1)
                End If

                If (Not String.IsNullOrEmpty(itbisTotal2)) And (itbisTotal2 <> "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "TotalITBIS2", itbisTotal2)
                End If

                If (DecItbisDGI > 0) Then
                    DecmontoTotal = DecGravado + DecExento + DecItbisDGI
                Else
                    DecmontoTotal = DecGravado + DecExento + DecItbis
                End If

                Dim montoTotalStr As String = (Math.Floor(CDec(totalFactura) * 100) / 100).ToString("0.00")
                Helper.AddElement(xmlDoc, totales, "MontoTotal", montoTotalStr)

                ' CÃ³digo seguridad (placeholder)
                encabezado.AppendChild(Helper.CreateElement(xmlDoc, "CodigoSeguridadeCF", codigoSeguridad))

            End If

            Return xmlDoc

        Catch ex As Exception
            Throw New Exception("Error generando XML del resumen: " & ex.Message)
        End Try
    End Function

    Public Shared Function GenerarXMLResumen(cadenaConexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String,
                               ByVal fac_forma As String, ByVal tfa_codigo As String, codigoSeguridad As String, encfgenerado As String) As XmlDocument
        Dim montoGravado As String = ""
        Dim montoGravado1 As String = ""
        Dim montoGravado2 As String = ""
        Dim itbisTotal As String = ""
        Dim totalFactura As String = ""
        Dim valorpagar As String = ""
        Dim montopagado As String = ""
        Dim montoexento As String = ""
        Dim unidadMedida As String = ""
        Dim precioUnitarioItem As String = ""
        Dim ultimaLineaDetalle As String = "1" ' Variable para la Ãºltima lÃ­nea del detalle
        Dim cantidadItem As String = ""
        Dim itbisTotal1 As String = ""
        Dim itbisTotal2 As String = ""
        Dim montoItem As String = ""
        Dim montoTotal As String = ""

        Dim DecmontoTotal As Decimal
        Dim DecGravado As Decimal
        Dim DecExento As Decimal
        Dim DecItbis As Decimal
        Dim DecItbisDGI As Decimal

        Try
            Dim xmlDoc As New XmlDocument()
            xmlDoc.PreserveWhitespace = True

            Dim xmlDeclaration As XmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", Nothing)
            xmlDoc.AppendChild(xmlDeclaration)

            Dim root As XmlElement = xmlDoc.CreateElement("RFCE")
            xmlDoc.AppendChild(root)

            Dim encabezado As XmlElement = xmlDoc.CreateElement("Encabezado")
            root.AppendChild(encabezado)

            Dim consultaFactura As String = ConsultaFacturaXmlStdSp(emp_codigo, suc_codigo, facNumero, fac_forma, tfa_codigo)
            Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)

            If facturaReader.Read() Then
                encabezado.AppendChild(Helper.CreateElement(xmlDoc, "Version", "1.0"))

                ' ID DOC
                Dim idDoc As XmlElement = xmlDoc.CreateElement("IdDoc")
                encabezado.AppendChild(idDoc)

                Helper.AddIfValid(xmlDoc, idDoc, facturaReader, "TipoeCF")
                idDoc.AppendChild(Helper.CreateElement(xmlDoc, "eNCF", encfgenerado))
                idDoc.AppendChild(Helper.CreateElement(xmlDoc, "TipoIngresos", "01"))
                Helper.AddIfValid(xmlDoc, idDoc, facturaReader, "TipoPago")

                ' Nodo TablaFormasPago
                Dim consultaFormaPago = " SELECT * FROM vwFormasPagoXML WHERE fac_numero=" & facNumero & " AND fac_forma='" & fac_forma & "' AND tfa_codigo='" & tfa_codigo & "' AND emp_codigo='" & emp_codigo & "' AND suc_codigo='" & suc_codigo & "'"
                Dim formaPagoReader = EjecutarConsultaReader(cadenaConexion, consultaFormaPago)
                If formaPagoReader.Read() Then
                    Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
                    idDoc.AppendChild(tablaFormasPagoNode)

                    ' AÃ±adir las formas de pago
                    Do
                        Dim formaNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
                        montopagado = Decimal.Parse(formaPagoReader("MontoPago").ToString()).ToString("F2")

                        tablaFormasPagoNode.AppendChild(formaNode)
                        Helper.AddElement(xmlDoc, formaNode, "FormaPago", formaPagoReader("FormaPago").ToString())
                        Helper.AddElement(xmlDoc, formaNode, "MontoPago", montopagado)

                    Loop While formaPagoReader.Read()
                End If
                formaPagoReader.Close()

                ' EMISOR
                Dim emisor As XmlElement = xmlDoc.CreateElement("Emisor")
                encabezado.AppendChild(emisor)

                Helper.AddElement(xmlDoc, emisor, "RNCEmisor", facturaReader("emp_rnc").ToString())
                Helper.AddElement(xmlDoc, emisor, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())

                Dim f? As DateTime = SafeDate(facturaReader("fac_fecha"), "fac_fecha")
                If f.HasValue Then
                    Helper.AddElement(xmlDoc, emisor, "FechaEmision", f.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                End If

                'Helper.AddElement(xmlDoc, emisor, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

                ' COMPRADOR
                Dim comprador As XmlElement = xmlDoc.CreateElement("Comprador")
                encabezado.AppendChild(comprador)
                Helper.AddElement(xmlDoc, comprador, "RNCComprador", facturaReader("cli_rnc").ToString())
                Helper.AddElement(xmlDoc, comprador, "RazonSocialComprador", facturaReader("cli_nombre").ToString())

                ' TOTALES
                Dim totales As XmlElement = xmlDoc.CreateElement("Totales")
                encabezado.AppendChild(totales)


                ' Guardar los valores en variables antes de cerrar el reader
                '' DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis_dgi").ToString())
                DecItbisDGI = Decimal.Parse(facturaReader("fac_itbis").ToString())
                DecItbis = Decimal.Parse(facturaReader("fac_itbis").ToString())
                itbisTotal = Decimal.Parse(facturaReader("fac_itbis").ToString()).ToString("F2")
                itbisTotal1 = Decimal.Parse(facturaReader("det_totalitbis_1").ToString()).ToString("F2")
                itbisTotal2 = Decimal.Parse(facturaReader("det_totalitbis_2").ToString()).ToString("F2")

                totalFactura = Decimal.Parse(facturaReader("fac_total").ToString()).ToString("F2")
                valorpagar = Decimal.Parse(facturaReader("ValorPagar").ToString()).ToString("F2")
                DecGravado = If(IsDBNull(facturaReader("monto_grabado")), 0D, Convert.ToDecimal(facturaReader("monto_grabado")))

                montoGravado = DecGravado.ToString("F2")

                montoGravado1 = If(IsDBNull(facturaReader("monto_grabado1")), 0D, Convert.ToDecimal(facturaReader("monto_grabado1"))).ToString("F2")
                montoGravado2 = If(IsDBNull(facturaReader("monto_grabado2")), 0D, Convert.ToDecimal(facturaReader("monto_grabado2"))).ToString("F2")

                If montoGravado <> "0" And montoGravado <> "0.00" Then
                    Helper.AddElement(xmlDoc, totales, "MontoGravadoTotal", montoGravado)
                End If

                If (Not String.IsNullOrEmpty(montoGravado1)) And (Not montoGravado1 = "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "MontoGravadoI1", montoGravado1)
                End If

                If (Not String.IsNullOrEmpty(montoGravado2)) And (Not montoGravado2 = "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "MontoGravadoI2", montoGravado2)
                End If

                ' Verificar si el monto exento no es igual a 0.00 antes de agregar el elementoltw1
                DecExento = Decimal.Parse(facturaReader("monto_exento").ToString())

                If (Not String.IsNullOrEmpty(DecExento.ToString("F2"))) And (Not DecExento.ToString("F2") = "0.00") Then
                    montoexento = DecExento.ToString("F2")
                    Helper.AddElement(xmlDoc, totales, "MontoExento", montoexento)
                End If

                ' RFCE: no incluir nodos ITBIS1/ITBIS2 (tasas); solo montos TotalITBIS* en Totales.

                If (Not String.IsNullOrEmpty(itbisTotal)) And (itbisTotal <> "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "TotalITBIS", itbisTotal)
                End If

                If (Not String.IsNullOrEmpty(itbisTotal1)) And (itbisTotal1 <> "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "TotalITBIS1", itbisTotal1)
                End If

                If (Not String.IsNullOrEmpty(itbisTotal2)) And (itbisTotal2 <> "0.00") Then
                    Helper.AddElement(xmlDoc, totales, "TotalITBIS2", itbisTotal2)
                End If

                If (DecItbisDGI > 0) Then
                    DecmontoTotal = DecGravado + DecExento + DecItbisDGI
                Else
                    DecmontoTotal = DecGravado + DecExento + DecItbis
                End If

                Dim montoTotalStr As String = (Math.Floor(CDec(totalFactura) * 100) / 100).ToString("0.00")
                Helper.AddElement(xmlDoc, totales, "MontoTotal", montoTotalStr)

                ' CÃ³digo seguridad (placeholder)
                encabezado.AppendChild(Helper.CreateElement(xmlDoc, "CodigoSeguridadeCF", codigoSeguridad))

            End If

            Return xmlDoc

        Catch ex As Exception
            Throw New Exception("Error generando XML del resumen: " & ex.Message)
        End Try
    End Function
    Private Shared Function ProcesoEnvioFacturaE41(
    RncCliente As String, eNCF As String, tipoECF As String,
    emp_codigo As String, suc_codigo As String, sup_codigo As String,
    facNumero As String, emp_rnc As String) As String

        Try
            Dim xmlFactura As XmlDocument = GenerarXMLE41(eNCF, cadenaConexion, facNumero, emp_codigo, sup_codigo)
            glbncfEnvia = eNCF
            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado

            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFactura(xmlFactura, pathCert, passCert, glbncfEnvia, emp_rnc)

            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)

            Dim token As String = GlobalVariables.token
            If String.IsNullOrWhiteSpace(token) Then
                Dim detTok = If(String.IsNullOrWhiteSpace(Seguridad.UltimoErrorToken), "(sin detalle; revise LogsCliente [TOKEN])", Seguridad.UltimoErrorToken)
                Return "ERROR: token vacio. " & detTok
            End If

            Dim trackIdOError As String = EnviarXMLFacturaFirmado(xmlFacturaFirmada, token, glbncfEnvia, emp_rnc)
            If trackIdOError.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) Then
                Return trackIdOError
            End If

            Dim respuesta As String = ConsultarEstadoConReintentos(trackIdOError, token)
            Try
                ProcesarRespuestaAPI(RncCliente, tipoECF, cadenaConexion, emp_codigo, suc_codigo,
                                     respuesta, facNumero, xmlFacturaFirmada, codigoSeguridad, "", "",
                                     sup_codigo, fechaFirma, False)

            Catch ex As Exception
                Helper.RegistrarLogCliente("ProcesarRespuestaAPI EX: " & ex.ToString())
            End Try

            Return respuesta                     ' o solo el estado, como prefieras
        Catch ex As Exception
            Helper.RegistrarLogCliente("ProcesoEnvioE41 EX: " & ex.ToString())
            Return "ERROR: " & ex.Message
        End Try
    End Function

    Private Shared Function GenerarXMLE41(eNCF As String, ByVal cadenaconexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal sup_codigo As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)
        Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

        ' Crear nodos
        Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
        encabezadoNode.AppendChild(idDocNode)

        Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
        encabezadoNode.AppendChild(emisorNode)

        Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
        encabezadoNode.AppendChild(compradorNode)

        Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
        encabezadoNode.AppendChild(totalesNode)

        Dim consultaFactura As String = "SELECT * FROM vwComprasXML WHERE fac_numero='" & facNumero & "' AND sup_codigo='" & sup_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        Dim facturaReader = EjecutarConsultaReader(cadenaconexion, consultaFactura)

        If facturaReader.Read() Then
            ' IdDoc en orden
            Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeCF").ToString())
            glbTipoeCF = facturaReader("TipoeCF").ToString()
            Helper.AddElement(xmlDoc, idDocNode, "eNCF", eNCF)
            'Dim consultaSecuencia = " select [dbo].[fn_obtenerSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as secuencia ")
            'Dim secuenciaReader = EjecutarConsultaReader(cadenaconexion, consultaSecuencia)

            Dim consultaFechaSecuencia = " select [dbo].[fn_obtenerFechaSecuenciaDGI]('" & emp_codigo & "','" & facturaReader("TipoeCF").ToString() & "') as Fecha_vence"
            Dim FechasecuenciaReader = EjecutarConsultaReader(cadenaconexion, consultaFechaSecuencia)

            Dim fechaSecuenciaAgregada As Boolean = False
            If FechasecuenciaReader.Read() Then
                Dim fven? As DateTime = SafeDate(FechasecuenciaReader("Fecha_vence"), "Fecha_vence")
                If fven.HasValue Then
                    Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fven.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                    fechaSecuenciaAgregada = True
                End If
            End If
            FechasecuenciaReader.Close()
            If Not fechaSecuenciaAgregada Then
                Throw New ApplicationException("Falta FechaVencimientoSecuencia para e-CF " & facturaReader("TipoeCF").ToString() & ".")
            End If

            Helper.AddOptionalElement(xmlDoc, idDocNode, "IndicadorMontoGravado", facturaReader("IndicadorMontoGravado"))
            Helper.AddElement(xmlDoc, idDocNode, "TipoPago", facturaReader("tipo_pago").ToString())

            Dim fechaLimitePago As String = facturaReader("fac_vence").ToString()
            If Not String.IsNullOrEmpty(fechaLimitePago) Then
                Dim fechaFormateada As String = DateTime.Parse(fechaLimitePago).ToString("dd-MM-yyyy")
                Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", fechaFormateada)
            End If

            Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
            idDocNode.AppendChild(tablaFormasPagoNode)
            Dim formaDePagoNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
            tablaFormasPagoNode.AppendChild(formaDePagoNode)
            Helper.AddElement(xmlDoc, formaDePagoNode, "FormaPago", facturaReader("FormaPago").ToString())
            Helper.AddElement(xmlDoc, formaDePagoNode, "MontoPago", Decimal.Parse(facturaReader("ValorPagar").ToString()).ToString("F2"))

            ' Emisor
            Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())

            Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode, facturaReader("emp_telefono").ToString())

            ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
            Dim correoEmisor As String = facturaReader("emp_email").ToString()
            If Not String.IsNullOrEmpty(correoEmisor) Then
                Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
            End If

            'Helper.AddOptionalElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email"))

            Helper.AddOptionalElement(xmlDoc, emisorNode, "WebSite", facturaReader("emp_web"))
            Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

            ' Comprador
            Helper.AddOptionalElement(xmlDoc, compradorNode, "RNCComprador", facturaReader("prov_rnc"))
            Helper.AddOptionalElement(xmlDoc, compradorNode, "RazonSocialComprador", facturaReader("prov_nombre"))
            Helper.AddOptionalElement(xmlDoc, compradorNode, "CorreoComprador", facturaReader("sup_email"))
            Helper.AddOptionalElement(xmlDoc, compradorNode, "DireccionComprador", facturaReader("sup_direccion"))

            ' Totales
            Dim montoGravadoTotal As String = If(IsDBNull(facturaReader("fac_grabado")), "0", facturaReader("fac_grabado").ToString())
            If montoGravadoTotal <> "0.00" Then
                Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoTotal", Decimal.Parse(montoGravadoTotal).ToString("F2"))
            End If

            Dim MontoGravadoI1 As String = If(IsDBNull(facturaReader("fac_grabado")), "0", facturaReader("fac_grabado").ToString())
            If MontoGravadoI1 <> "0.00" Then
                Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI1", Decimal.Parse(MontoGravadoI1).ToString("F2"))
            End If

            Dim montoExento As String = If(IsDBNull(facturaReader("fac_exento")), "0", facturaReader("fac_exento").ToString())
            If montoExento <> "0" AndAlso montoExento <> "0.00" Then
                Helper.AddElement(xmlDoc, totalesNode, "MontoExento", Decimal.Parse(montoExento).ToString("F2"))
            End If
            Dim totalItbis As String = If(IsDBNull(facturaReader("fac_itbis")), "0", facturaReader("fac_itbis").ToString())
            ' Verificar si itbis18 tiene un valor antes de agregar el nodo
            If (Not String.IsNullOrEmpty(totalItbis)) And (totalItbis <> "0") And (totalItbis <> "0.00") Then
                Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", "18")
                '' Dim totalItbis As String = If(IsDBNull(facturaReader("fac_itbis")), "0", facturaReader("fac_itbis").ToString())
                Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS", Decimal.Parse(totalItbis).ToString("F2"))
                Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS1", Decimal.Parse(totalItbis).ToString("F2"))

            End If


            Dim montoTotal As Decimal = Convert.ToDecimal(montoGravadoTotal) + Convert.ToDecimal(montoExento) + Convert.ToDecimal(totalItbis)
            Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", montoTotal.ToString("F2"))
            Dim valorPagar As String = If(IsDBNull(facturaReader("ValorPagar")), "0", facturaReader("ValorPagar").ToString())
            Helper.AddElement(xmlDoc, totalesNode, "ValorPagar", Decimal.Parse(valorPagar).ToString("F2"))

            Dim fac_retencion As String = If(IsDBNull(facturaReader("fac_retencion")), "0", facturaReader("fac_retencion").ToString())
            Helper.AddElement(xmlDoc, totalesNode, "TotalITBISRetenido", Decimal.Parse(fac_retencion).ToString("F2"))

            Dim fac_retencion_isr As String = If(IsDBNull(facturaReader("fac_retencion_isr")), "0", facturaReader("fac_retencion_isr").ToString())
            Helper.AddElement(xmlDoc, totalesNode, "TotalISRRetencion", Decimal.Parse(fac_retencion_isr).ToString("F2"))


        End If
        facturaReader.Close()

        ' DetallesItems
        Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
        root.AppendChild(detallesItemsNode)

        Dim consultadetalleFactura As String = "SELECT * FROM vwComprasDetalleXML WHERE fac_numero='" & facNumero & "' AND sup_codigo='" & sup_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        Dim detalleReader = EjecutarConsultaReader(cadenaconexion, consultadetalleFactura)
        Dim totalItems As Integer = 0
        Dim itemsList As New List(Of Dictionary(Of String, Object))

        While detalleReader.Read()
            totalItems += 1
            Dim itemData As New Dictionary(Of String, Object)
            For i As Integer = 0 To detalleReader.FieldCount - 1
                itemData(detalleReader.GetName(i)) = detalleReader.GetValue(i)
            Next
            itemsList.Add(itemData)
        End While
        detalleReader.Close()

        For Each item In itemsList
            Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
            detallesItemsNode.AppendChild(itemNode)

            Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", item("NumeroLinea").ToString())
            Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", item("IndicadorFacturacion").ToString())

            Dim retencionITBIS As Decimal = 0
            Dim retencionISR As Decimal = 0

            If item.ContainsKey("fac_retencion") AndAlso Not IsDBNull(item("fac_retencion")) Then
                retencionITBIS = Decimal.Parse(item("fac_retencion").ToString()) / totalItems
            End If
            If item.ContainsKey("fac_retencion_isr") AndAlso Not IsDBNull(item("fac_retencion_isr")) Then
                retencionISR = Decimal.Parse(item("fac_retencion_isr").ToString()) / totalItems
            End If

            'If retencionITBIS > 0 OrElse retencionISR > 0 Then

            Dim retencionNode As XmlElement = xmlDoc.CreateElement("Retencion")
            itemNode.AppendChild(retencionNode)

            If item.ContainsKey("IndicadorAgenteRetencionoPercepcion") AndAlso Not String.IsNullOrEmpty(item("IndicadorAgenteRetencionoPercepcion").ToString()) Then
                Helper.AddElement(xmlDoc, retencionNode, "IndicadorAgenteRetencionoPercepcion", item("IndicadorAgenteRetencionoPercepcion").ToString())
            End If

            Helper.AddElement(xmlDoc, retencionNode, "MontoITBISRetenido", retencionITBIS.ToString("F2"))
            Helper.AddElement(xmlDoc, retencionNode, "MontoISRRetenido", retencionISR.ToString("F2"))
            ''If retencionITBIS > 0 Then
            '        AddElement(xmlDoc, retencionNode, "MontoITBISRetenido", retencionITBIS.ToString("F2"))
            '    End If
            'If retencionISR > 0 Then
            '    AddElement(xmlDoc, retencionNode, "MontoISRRetenido", retencionISR.ToString("F2"))
            'End If

            Helper.AddElement(xmlDoc, itemNode, "NombreItem", item("NombreItem").ToString())
            Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", item("IndicadorBienoServicio").ToString())
            Helper.AddElement(xmlDoc, itemNode, "CantidadItem", Decimal.Parse(item("CantidadItem").ToString()).ToString("F2"))
            Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", item("UnidadMedida").ToString())
            Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", Decimal.Parse(item("PrecioUnitarioItem").ToString()).ToString("F2"))
            Helper.AddElement(xmlDoc, itemNode, "MontoItem", Decimal.Parse(item("MontoItem").ToString()).ToString("F2"))
            'End If
        Next

        Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
        root.AppendChild(fechaHoraFirmaNode)
        fechaHoraFirmaNode.InnerText = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")

        Return xmlDoc
    End Function
    Private Shared Function ProcesoEnvioFacturaE43(RncCliente As String, ByVal eNCF As String, ByVal tipoECF As String, ByVal emp_codigo As String, ByVal suc_codigo As String, ByVal facNumero As String, emp_rnc As String) As String
        Try
            glbncfEnvia = eNCF

            ' Paso 1: Obtener el XML de la factura 
            Dim xmlFactura As XmlDocument = GenerarXMLE43(eNCF, cadenaConexion, facNumero, emp_codigo, suc_codigo) ' MÃ©todo que obtendrÃ¡ el XML de la factura de la BD

            ' Paso 2: Firmar el XML de la factura
            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado

            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFactura(xmlFactura, pathCert, passCert, glbncfEnvia, emp_rnc)
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)

            ' Paso 3: Obtener el token 
            Dim token As String = GlobalVariables.token

            ' Paso 4: Enviar el XML firmado
            Dim trackId As String = EnviarXMLFacturaFirmado(xmlFacturaFirmada, token, glbncfEnvia, emp_rnc)

            ' Paso 5: Consultar el estado de la factura
            Dim respuestaConsulta As String = ConsultarEstadoConReintentos(trackId, token)

            Try
                ProcesarRespuestaAPI(RncCliente, tipoECF, cadenaConexion, emp_codigo, suc_codigo, respuestaConsulta, facNumero, xmlFacturaFirmada, codigoSeguridad, "", "", "", fechaFirma, False)
            Catch ex As Exception
                Helper.RegistrarLogCliente("ProcesarRespuestaAPI EX: " & ex.ToString())
            End Try

            Dim parseada = Helper.ParsearRespuestaDgii(respuestaConsulta)
            Return Helper.ConstruirJsonRespuestaOperacion(parseada, respuestaConsulta)

        Catch ex As Exception
            Throw New ApplicationException("Error en el proceso de envÃ­o de la factura: " & ex.Message)
        End Try
    End Function
    Private Shared Function GenerarXMLE43(eNCF As String, cadenaConexion As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)
        Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

        ' Crear nodos
        Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
        encabezadoNode.AppendChild(idDocNode)

        Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
        encabezadoNode.AppendChild(emisorNode)

        'Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
        'encabezadoNode.AppendChild(compradorNode)

        Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
        encabezadoNode.AppendChild(totalesNode)

        Dim consultaFactura As String = "SELECT * FROM vwDesembolsoXML WHERE des_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)

        If facturaReader.Read() Then
            ' IdDoc en orden
            Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeNCF").ToString())
            glbTipoeCF = facturaReader("TipoeNCF").ToString()

            Helper.AddElement(xmlDoc, idDocNode, "eNCF", eNCF)
            Dim consultaFechaSecuencia = " select [dbo].[fn_obtenerFechaSecuenciaDGI]('" & emp_codigo & "','" & facturaReader("TipoeNCF").ToString() & "') as Fecha_vence"
            Dim FechasecuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaFechaSecuencia)

            If FechasecuenciaReader.Read() Then

                Dim fven? As DateTime = SafeDate(FechasecuenciaReader("Fecha_vence"), "Fecha_vence")
                If fven.HasValue Then
                    Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fven.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                End If
            End If

            ' Emisor
            Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())

            Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode, facturaReader("emp_telefono").ToString())

            'Helper.AddOptionalElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email"))

            ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
            Dim correoEmisor As String = facturaReader("emp_email").ToString()
            If Not String.IsNullOrEmpty(correoEmisor) Then
                Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
            End If

            Helper.AddOptionalElement(xmlDoc, emisorNode, "WebSite", facturaReader("emp_web"))
            Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("des_fecha")).ToString("dd-MM-yyyy"))

            ' Totales
            Dim montoTotal As String = If(IsDBNull(facturaReader("des_monto")), "0", facturaReader("des_monto").ToString())

            Dim montoExento As String = If(IsDBNull(facturaReader("des_monto")), "0", facturaReader("des_monto").ToString())
            If montoExento <> "0" AndAlso montoExento <> "0.00" Then
                Helper.AddElement(xmlDoc, totalesNode, "MontoExento", Decimal.Parse(montoExento).ToString("F2"))
            End If

            Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", montoTotal)

        End If
        facturaReader.Close()

        ' DetallesItems
        Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
        root.AppendChild(detallesItemsNode)

        Dim consultaDetalle = " SELECT * FROM vwDesembolsoDetalleXML WHERE DES_numero=" & facNumero & " AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        Dim detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalle)

        Dim totalItems As Integer = 0
        Dim itemsList As New List(Of Dictionary(Of String, Object))

        While detalleReader.Read()
            totalItems += 1
            Dim itemData As New Dictionary(Of String, Object)
            For i As Integer = 0 To detalleReader.FieldCount - 1
                itemData(detalleReader.GetName(i)) = detalleReader.GetValue(i)
            Next
            itemsList.Add(itemData)
        End While
        detalleReader.Close()

        For Each item In itemsList
            Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
            detallesItemsNode.AppendChild(itemNode)

            Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", item("NumeroLinea").ToString())
            Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", item("IndicadorFacturacion").ToString())
            Helper.AddElement(xmlDoc, itemNode, "NombreItem", item("NombreItem").ToString())
            Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", item("IndicadorBienoServicio").ToString())
            Helper.AddElement(xmlDoc, itemNode, "CantidadItem", Decimal.Parse(item("CantidadItem").ToString()).ToString("F2"))
            Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", item("UnidadMedida").ToString())
            Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", Decimal.Parse(item("PrecioUnitarioItem").ToString()).ToString("F2"))
            Helper.AddElement(xmlDoc, itemNode, "MontoItem", Decimal.Parse(item("MontoItem").ToString()).ToString("F2"))
            'End If
        Next

        Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
        root.AppendChild(fechaHoraFirmaNode)
        fechaHoraFirmaNode.InnerText = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")

        Return xmlDoc
    End Function
    Private Shared Function ProcesoEnvioFacturaE34(RncCliente As String, ByVal eNCF As String, ByVal tipoECF As String, ByVal emp_codigo As String, ByVal suc_codigo As String, ByVal sup_codigo As String, ByVal facNumero As String, emp_rnc As String, caja As String, usu_codigo As String, isNotaCredito As Boolean) As String
        Try
            glbncfEnvia = eNCF
            Helper.RegistrarLogCliente("GenerarXMLE34 " + eNCF)
            ' Paso 1: Obtener el XML de la factura 
            Dim xmlFactura As XmlDocument = GenerarXMLE34(eNCF, facNumero, emp_codigo, suc_codigo, isNotaCredito) ' MÃ©todo que obtendrÃ¡ el XML de la factura de la BD

            Helper.RegistrarLogCliente("GenerarXMLE34Fin " + facNumero)

            ' Paso 2: Firmar el XML de la factura
            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado

            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFactura(xmlFactura, pathCert, passCert, glbncfEnvia, emp_rnc)
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)
            ' Paso 3: Obtener el token 
            Dim token As String = GlobalVariables.token

            ' Paso 4: Enviar el XML firmado
            Dim trackId As String = EnviarXMLFacturaFirmado(xmlFacturaFirmada, token, glbncfEnvia, emp_rnc)

            Dim respuestaConsulta As String = ConsultarEstadoConReintentos(trackId, token)
            Try
                ProcesarRespuestaAPI(RncCliente, tipoECF, cadenaConexion, emp_codigo, suc_codigo, respuestaConsulta, facNumero, xmlFacturaFirmada, codigoSeguridad, "", "", sup_codigo, fechaFirma, isNotaCredito)

            Catch ex As Exception
                Helper.RegistrarLogCliente("ProcesarRespuestaAPI EX: " & ex.ToString())
            End Try

            Dim parseada = Helper.ParsearRespuestaDgii(respuestaConsulta)
            Return Helper.ConstruirJsonRespuestaOperacion(parseada, respuestaConsulta)
        Catch ex As Exception
            Throw New ApplicationException("Error en el proceso de envÃ­o de la factura: " & ex.Message)

        End Try

    End Function


    Private Shared Function GenerarXMLE34(ByVal eNCF As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String, isNotaCredito As Boolean) As XmlDocument
        Dim xmlDoc As New XmlDocument()
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)
        Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

        ' IdDoc
        Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
        encabezadoNode.AppendChild(idDocNode)

        Dim consultaFactura As String
        Helper.RegistrarLogCliente("isNotaCredito " + isNotaCredito.ToString)
        If isResBar Then
            consultaFactura = "SELECT * FROM vwDevolucionesRESBARXML WHERE dev_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        ElseIf isPOS Then
            consultaFactura = "SELECT * FROM vwDevolucionesPOSXML WHERE dev_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        Else
            If isNotaCredito Then
                consultaFactura = "SELECT * FROM vwNotaCreditoXML WHERE dev_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"

            Else
                consultaFactura = "SELECT * FROM vwDevolucionesXML WHERE dev_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"

            End If

        End If
        Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)
        Helper.RegistrarLogCliente("facturaReader " + isNotaCredito.ToString)

        If facturaReader.Read() Then
            Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeCF").ToString())
            glbTipoeCF = facturaReader("TipoeCF").ToString()

            Helper.AddElement(xmlDoc, idDocNode, "eNCF", eNCF)

            ' =======================
            ' INDICADORNOTACREDITO
            ' a) 0 si fecha e-CF afectado <= 30 dÃ­as
            ' b) 1 si fecha e-CF afectado > 30 dÃ­as
            ' =======================
            Dim indicadorNotaCredito As String = "0"

            Try
                ' Fecha de emisiÃ³n de ESTA nota (E34)
                Dim fechaEmisionNota As DateTime = Convert.ToDateTime(facturaReader("fac_fecha"))

                ' Fecha de emisiÃ³n del e-CF afectado (viene en FechaNCFModificado)
                If Not facturaReader.IsDBNull(facturaReader.GetOrdinal("FechaNCFModificado")) Then
                    Dim fechaECFAfectado As DateTime = Convert.ToDateTime(facturaReader("FechaNCFModificado"))

                    Dim diasDiferencia As Integer = CInt((fechaEmisionNota.Date - fechaECFAfectado.Date).TotalDays)

                    If diasDiferencia > 30 Then
                        indicadorNotaCredito = "1"
                    Else
                        indicadorNotaCredito = "0"
                    End If

                    Helper.RegistrarLogCliente("IndicadorNotaCredito calculado. Dias diferencia=" &
                                           diasDiferencia.ToString() &
                                           " Valor=" & indicadorNotaCredito)
                Else
                    ' Si no tenemos FechaNCFModificado, por seguridad lo dejamos en 0
                    Helper.RegistrarLogCliente("FechaNCFModificado NULL, IndicadorNotaCredito=0 por defecto")
                End If

            Catch ex As Exception
                ' En caso de error, dejarlo en 0 y loguear
                indicadorNotaCredito = "0"
                Helper.RegistrarLogCliente("Error calculando IndicadorNotaCredito: " & ex.Message)
            End Try

            Helper.AddElement(xmlDoc, idDocNode, "IndicadorNotaCredito", indicadorNotaCredito)
            ' ======================= FIN CAMBIO =======================


            Dim indicadorMonto As String = If(IsDBNull(facturaReader("IndicadorMontoGravado")), "", facturaReader("IndicadorMontoGravado").ToString())

            If Not String.IsNullOrEmpty(indicadorMonto) AndAlso indicadorMonto <> "0" Then
                Helper.AddElement(xmlDoc, idDocNode, "IndicadorMontoGravado", indicadorMonto)
            End If
            'AddOptionalElement(xmlDoc, idDocNode, "IndicadorMontoGravado", facturaReader("IndicadorMontoGravado"))
            Helper.AddElement(xmlDoc, idDocNode, "TipoIngresos", "01")
            Helper.AddElement(xmlDoc, idDocNode, "TipoPago", facturaReader("TipoPago").ToString())

            ' Fecha lÃ­mite de pago
            Dim f? As DateTime = SafeDate(facturaReader("FechaLimitePago"), "FechaLimitePago")
            If f.HasValue Then
                Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", f.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
            End If
            Helper.RegistrarLogCliente("FechaLimitePago " + isPOS.ToString)
            ' Emisor
            Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
            encabezadoNode.AppendChild(emisorNode)
            Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())
            'AddOptionalElement(xmlDoc, emisorNode, "Municipio", facturaReader("municipio"))
            'AddOptionalElement(xmlDoc, emisorNode, "Provincia", facturaReader("provincia"))

            Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode, facturaReader("emp_telefono").ToString())

            ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
            Dim correoEmisor As String = facturaReader("emp_email").ToString()
            If Not String.IsNullOrEmpty(correoEmisor) Then
                Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
            End If
            Helper.RegistrarLogCliente("CorreoEmisor " + isPOS.ToString)
            'Helper.AddOptionalElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email"))

            Helper.AddOptionalElement(xmlDoc, emisorNode, "WebSite", facturaReader("emp_web"))
            Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

            ' Comprador (solo si existe RNC)
            Dim rnc As String = ""
            If Not facturaReader.IsDBNull(facturaReader.GetOrdinal("prov_rnc")) Then
                rnc = Convert.ToString(facturaReader("prov_rnc")).Trim()
            End If

            If rnc <> "" Then
                Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
                encabezadoNode.AppendChild(compradorNode)

                ' Ya que rnc lo tenemos saneado, lo usamos directo:
                Helper.AddOptionalElement(xmlDoc, compradorNode, "RNCComprador", rnc)
                Helper.AddOptionalElement(xmlDoc, compradorNode, "RazonSocialComprador", facturaReader("prov_nombre"))

                Dim correoComprador As String = facturaReader("cli_email").ToString()
                If Not String.IsNullOrEmpty(correoComprador) Then
                    Helper.AddOptionalElement(xmlDoc, compradorNode, "CorreoComprador", facturaReader("cli_email"))
                End If

                Dim direccionComprador As String = facturaReader("cli_direccion").ToString().Trim()
                If Not String.IsNullOrEmpty(direccionComprador) Then
                    Helper.AddOptionalElement(xmlDoc, compradorNode, "DireccionComprador", facturaReader("cli_direccion"))
                End If

            End If
            Helper.RegistrarLogCliente("prov_rnc " + isPOS.ToString)

            ' Totales
            Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
            Dim montoexento = Convert.ToDecimal(facturaReader("fac_exento"))
            Dim grabado = Convert.ToDecimal(facturaReader("fac_grabado"))
            Dim devtotal = Convert.ToDecimal(facturaReader("dev_total"))
            Dim itbis = Convert.ToDecimal(facturaReader("fac_itbis"))

            Dim itbis1 = Convert.ToDecimal(facturaReader("TotalITBIS1"))
            Dim itbis2 = Convert.ToDecimal(facturaReader("TotalITBIS2"))

            Dim grabado1 = Convert.ToDecimal(facturaReader("TotalGrabado1"))
            Dim grabado2 = Convert.ToDecimal(facturaReader("TotalGrabado2"))

            encabezadoNode.AppendChild(totalesNode)

            If grabado > 0 Then
                ' Siempre va el total general
                Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoTotal", grabado.ToString())

                ' --- Grupo ITBIS 18 ---
                If grabado1 > 0 Then
                    Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI1", grabado1.ToString())
                End If

                If grabado2 > 0 Then
                    Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI2", grabado2.ToString())
                End If
            End If

            If (montoexento > 0) Then
                Helper.AddElement(xmlDoc, totalesNode, "MontoExento", montoexento.ToString())
            End If

            If grabado1 > 0 Then
                Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", "18")
            End If

            ' --- Grupo ITBIS 16 ---
            If grabado2 > 0 Then
                Helper.AddElement(xmlDoc, totalesNode, "ITBIS2", "16")
            End If

            Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS", itbis.ToString())

            If grabado1 > 0 Then
                Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS1", itbis1.ToString())
            End If

            ' --- Grupo ITBIS 16 ---
            If grabado2 > 0 Then
                Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS2", itbis2.ToString())
            End If



            Dim total As Decimal = Convert.ToDecimal(facturaReader("dev_total"))
            ''            Dim total As Decimal = Convert.ToDecimal(facturaReader("fac_grabado")) + montoexento + Convert.ToDecimal(facturaReader("fac_itbis"))

            Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", total.ToString("F2"))

            Helper.AddElement(xmlDoc, totalesNode, "MontoPeriodo", devtotal.ToString())
            Helper.AddElement(xmlDoc, totalesNode, "ValorPagar", devtotal.ToString())

        End If

        Dim NCFModificado As String = facturaReader("eNCFModifica").ToString()
        Dim RazonModificacion As String = facturaReader("RazonModificacion").ToString()
        Dim FechaNCFModificado As String = facturaReader("FechaNCFModificado").ToString()
        Dim CodigoModificacion As String = facturaReader("CodigoModificacion").ToString()
        Helper.RegistrarLogCliente("eNCFModifica " + isPOS.ToString)
        facturaReader.Close()

        ' DetallesItems
        Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
        root.AppendChild(detallesItemsNode)

        Dim consultaDetalleFactura As String

        If isNotaCredito Then
            consultaDetalleFactura = "SELECT * FROM vwNotaCreditoDetalleXML WHERE dev_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        ElseIf isResBar Then
            consultaDetalleFactura = "SELECT * FROM vwDevolucionesDetalleXMLRESBAR WHERE dev_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        Else
            consultaDetalleFactura = "SELECT * FROM vwDevolucionesDetalleXML WHERE dev_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        End If


        Dim detalleReader = EjecutarConsultaReader(cadenaConexion, consultaDetalleFactura)
        Helper.RegistrarLogCliente("vwDevolucionesDetalleXML " + isPOS.ToString)

        While detalleReader.Read()
            Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
            detallesItemsNode.AppendChild(itemNode)
            Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", detalleReader("NumeroLinea").ToString())
            Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", detalleReader("IndicadorFacturacion").ToString())
            Helper.AddElement(xmlDoc, itemNode, "NombreItem", detalleReader("NombreItem").ToString())
            Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", detalleReader("IndicadorBienoServicio").ToString())
            Helper.AddElement(xmlDoc, itemNode, "CantidadItem", Decimal.Parse(detalleReader("CantidadItem").ToString()).ToString("F2"))
            Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", detalleReader("UnidadMedida").ToString())
            Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", Decimal.Parse(detalleReader("PrecioUnitarioItem").ToString()).ToString("F2"))
            Helper.AddElement(xmlDoc, itemNode, "MontoItem", Decimal.Parse(detalleReader("MontoItem").ToString()).ToString("F2"))
        End While
        detalleReader.Close()

        ' InformacionesAdicionales (si aplica)
        If Not IsDBNull(RazonModificacion) Or Not IsDBNull(FechaNCFModificado) Then
            Dim infoAdicionalNode As XmlElement = xmlDoc.CreateElement("InformacionReferencia")
            root.AppendChild(infoAdicionalNode)
            Helper.AddElement(xmlDoc, infoAdicionalNode, "NCFModificado", NCFModificado)
            Helper.AddElement(xmlDoc, infoAdicionalNode, "FechaNCFModificado", Convert.ToDateTime(FechaNCFModificado).ToString("dd-MM-yyyy"))
            Helper.AddElement(xmlDoc, infoAdicionalNode, "CodigoModificacion", CodigoModificacion)
            Helper.AddElement(xmlDoc, infoAdicionalNode, "RazonModificacion", RazonModificacion)
        End If

        ' FechaHoraFirma (aunque no se firme todavÃ­a)
        Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
        root.AppendChild(fechaHoraFirmaNode)
        fechaHoraFirmaNode.InnerText = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")

        Return xmlDoc
    End Function
    Private Shared Sub ProcesoEnvioFacturaE33(RncCliente As String, ByVal eNCF As String, ByVal tipoECF As String, ByVal emp_codigo As String, ByVal suc_codigo As String, ByVal sup_codigo As String, ByVal facNumero As String, emp_rnc As String)
        Try
            ' Paso 1: Obtener el XML de la factura 
            Dim xmlFactura As XmlDocument = GenerarXMLE33(eNCF, facNumero, emp_codigo, suc_codigo) ' MÃ©todo que obtendrÃ¡ el XML de la factura de la BD

            ' Paso 2: Firmar el XML de la factura
            Dim pathCert As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado

            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFactura(xmlFactura, pathCert, passCert, glbncfEnvia, emp_rnc)
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)
            ' Paso 3: Obtener el token 
            Dim token As String = GlobalVariables.token

            ' Paso 4: Enviar el XML firmado
            Dim trackId As String = EnviarXMLFacturaFirmado(xmlFacturaFirmada, token, glbncfEnvia, emp_rnc)

            ' Paso 5: Consultar el estado de la factura
            Dim respuestaConsulta As String = ConsultarEstadoFactura(trackId, token)
            ProcesarRespuestaAPI(RncCliente, tipoECF, cadenaConexion, emp_codigo, suc_codigo, respuestaConsulta, facNumero, xmlFacturaFirmada, codigoSeguridad, "", "", sup_codigo, fechaFirma, False)


        Catch ex As Exception
            Throw New ApplicationException("Error en el proceso de envÃ­o de la factura: " & ex.Message)

        End Try
    End Sub
    Private Shared Function GenerarXMLE33(ByVal eNCF As String, ByVal facNumero As String, ByVal emp_codigo As String, ByVal suc_codigo As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)
        Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

        ' IdDoc
        Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
        encabezadoNode.AppendChild(idDocNode)

        Dim consultafactura As String = "SELECT * FROM vwNotasDebitoXML WHERE nde_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultafactura)

        If facturaReader.Read() Then
            Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", facturaReader("TipoeCF").ToString())
            glbTipoeCF = facturaReader("TipoeCF").ToString()

            Dim consultaSecuencia = " select [dbo].[fn_obtenerSecuenciaDGI](" & Integer.Parse(emp_codigo) & "," & Integer.Parse(facturaReader("TipoeCF").ToString() & ") as secuencia ")
            Dim secuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaSecuencia)
            If secuenciaReader.Read() Then
                glbncfEnvia = eNCF 'secuenciaReader("secuencia").ToString()
                Helper.AddElement(xmlDoc, idDocNode, "eNCF", glbncfEnvia)
                Helper.AddOptionalElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", "31-12-2025")
            End If
            secuenciaReader.Close()

            Helper.AddElement(xmlDoc, idDocNode, "TipoIngresos", "01")
            Helper.AddElement(xmlDoc, idDocNode, "TipoPago", facturaReader("TipoPago").ToString())

            ' Emisor
            Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
            encabezadoNode.AppendChild(emisorNode)
            Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
            Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())

            Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode, facturaReader("emp_telefono").ToString())

            ' Verificar si el campo "WebSite" no es nulo o vacÃ­o antes de agregar el elemento
            Dim correoEmisor As String = facturaReader("emp_email").ToString()
            If Not String.IsNullOrEmpty(correoEmisor) Then
                Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
            End If

            'Helper.AddOptionalElement(xmlDoc, emisorNode, "CorreoEmisor", facturaReader("emp_email"))

            Helper.AddOptionalElement(xmlDoc, emisorNode, "WebSite", facturaReader("emp_web"))
            Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy"))

            ' Comprador
            Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
            encabezadoNode.AppendChild(compradorNode)
            Helper.AddOptionalElement(xmlDoc, compradorNode, "RNCComprador", facturaReader("prov_rnc"))
            Helper.AddOptionalElement(xmlDoc, compradorNode, "RazonSocialComprador", facturaReader("prov_nombre"))
            Helper.AddOptionalElement(xmlDoc, compradorNode, "CorreoComprador", facturaReader("cli_email"))
            Helper.AddOptionalElement(xmlDoc, compradorNode, "DireccionComprador", facturaReader("cli_direccion"))

            ' Totales
            Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
            Dim montoexento = Convert.ToDecimal(facturaReader("fac_exento"))
            Dim grabado = Convert.ToDecimal(facturaReader("nde_abono"))
            Dim devtotal = Convert.ToDecimal(facturaReader("nde_monto"))
            Dim itbis = Convert.ToDecimal(facturaReader("nde_itbis"))

            encabezadoNode.AppendChild(totalesNode)

            If (grabado > 0) Then
                Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoTotal", grabado.ToString())
                Helper.AddElement(xmlDoc, totalesNode, "MontoGravadoI1", grabado.ToString())
                Helper.AddElement(xmlDoc, totalesNode, "ITBIS1", "18")
                Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS", itbis.ToString())
                Helper.AddElement(xmlDoc, totalesNode, "TotalITBIS1", itbis.ToString())

            End If

            If (montoexento > 0) Then
                Helper.AddElement(xmlDoc, totalesNode, "MontoExento", montoexento.ToString())
            End If

            Dim total As Decimal = Convert.ToDecimal(facturaReader("nde_monto"))
            Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", total.ToString("F2"))

            'AddElement(xmlDoc, totalesNode, "MontoPeriodo", devtotal.ToString())
            'AddElement(xmlDoc, totalesNode, "ValorPagar", devtotal.ToString())

        End If

        Dim NCFModificado As String = facturaReader("eNCFModifica").ToString()
        Dim RazonModificacion As String = facturaReader("RazonModificacion").ToString()
        Dim FechaNCFModificado As String = facturaReader("FechaNCFModificado").ToString()
        Dim CodigoModificacion As String = facturaReader("CodigoModificacion").ToString()

        facturaReader.Close()

        ' DetallesItems
        Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
        root.AppendChild(detallesItemsNode)

        Dim consultadetalle As String = "SELECT * FROM vwNotasDebitoDetalleXML WHERE nde_numero='" & facNumero & "' AND suc_codigo='" & suc_codigo & "' AND emp_codigo='" & emp_codigo & "'"
        Dim detalleReader = EjecutarConsultaReader(cadenaConexion, consultadetalle)

        While detalleReader.Read()
            Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
            detallesItemsNode.AppendChild(itemNode)
            Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", detalleReader("NumeroLinea").ToString())
            Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", detalleReader("IndicadorFacturacion").ToString())
            Helper.AddElement(xmlDoc, itemNode, "NombreItem", detalleReader("NombreItem").ToString())
            Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", detalleReader("IndicadorBienoServicio").ToString())
            Helper.AddElement(xmlDoc, itemNode, "CantidadItem", Decimal.Parse(detalleReader("CantidadItem").ToString()).ToString("F2"))
            Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", detalleReader("UnidadMedida").ToString())
            Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem", Decimal.Parse(detalleReader("PrecioUnitarioItem").ToString()).ToString("F2"))
            Helper.AddElement(xmlDoc, itemNode, "MontoItem", Decimal.Parse(detalleReader("MontoItem").ToString()).ToString("F2"))
        End While
        detalleReader.Close()

        ' InformacionesAdicionales (si aplica)
        If Not IsDBNull(RazonModificacion) Or Not IsDBNull(FechaNCFModificado) Then
            Dim infoAdicionalNode As XmlElement = xmlDoc.CreateElement("InformacionReferencia")
            root.AppendChild(infoAdicionalNode)
            Helper.AddElement(xmlDoc, infoAdicionalNode, "NCFModificado", NCFModificado)
            Helper.AddElement(xmlDoc, infoAdicionalNode, "FechaNCFModificado", Convert.ToDateTime(FechaNCFModificado).ToString("dd-MM-yyyy"))
            Helper.AddElement(xmlDoc, infoAdicionalNode, "CodigoModificacion", CodigoModificacion)
            Helper.AddElement(xmlDoc, infoAdicionalNode, "RazonModificacion", RazonModificacion)
        End If

        ' FechaHoraFirma (aunque no se firme todavÃ­a)
        Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
        root.AppendChild(fechaHoraFirmaNode)
        fechaHoraFirmaNode.InnerText = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")

        Return xmlDoc
    End Function
    Private Shared Function ProcesoEnvioFacturaE47(
    RncCliente As String, eNCF As String, tipoECF As String,
    emp_codigo As String, suc_codigo As String, facNumero As String,
    emp_rnc As String, sup_codigo As String) As String
        Try
            Dim xmlFactura As XmlDocument = GenerarXMLE47(eNCF, cadenaConexion, facNumero, emp_codigo, sup_codigo)
            glbncfEnvia = eNCF

            Dim pathCert As String = ObtenerRutaArchivo(GlobalVariables.pathCertificado)
            Dim passCert As String = GlobalVariables.passCertificado

            Dim xmlFacturaFirmada As XmlDocument = FirmarXMLFactura(xmlFactura, pathCert, passCert, glbncfEnvia, emp_rnc)
            Dim codigoSeguridad As String = ObtenerCodigoSeguridadDesdeXml(xmlFacturaFirmada)
            Dim fechaFirma As DateTime = ObtenerFechaFirmaDesdeXmlComoDateTime(xmlFacturaFirmada)

            Dim token As String = GlobalVariables.token
            If String.IsNullOrWhiteSpace(token) Then
                Dim detTok = If(String.IsNullOrWhiteSpace(Seguridad.UltimoErrorToken), "(sin detalle; revise LogsCliente [TOKEN])", Seguridad.UltimoErrorToken)
                Return "ERROR: token vacio. " & detTok
            End If

            Dim trackIdOError As String = EnviarXMLFacturaFirmado(xmlFacturaFirmada, token, glbncfEnvia, emp_rnc)
            If trackIdOError.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) Then
                Return trackIdOError
            End If

            Dim respuestaConsulta As String = ConsultarEstadoConReintentos(trackIdOError, token)
            Try
                ProcesarRespuestaAPI(RncCliente, tipoECF, cadenaConexion, emp_codigo, suc_codigo,
                                     respuestaConsulta, facNumero, xmlFacturaFirmada, codigoSeguridad, "", "",
                                     sup_codigo, fechaFirma, False)
            Catch ex As Exception
                Helper.RegistrarLogCliente("ProcesarRespuestaAPI E47 EX: " & ex.ToString())
            End Try

            Return respuestaConsulta
        Catch ex As Exception
            Helper.RegistrarLogCliente("ProcesoEnvioE47 EX: " & ex.ToString())
            Return "ERROR: " & ex.Message
        End Try
    End Function

    ''' <summary>e-CF 47 (pagos al exterior): encabezado y detalle desde <c>vwComprasXML</c> / <c>vwComprasDetalleXML</c> (ProvFacturas).</summary>
    Private Shared Function GenerarXMLE47(eNCF As String, cadenaConexion As String, facNumero As String, emp_codigo As String, sup_codigo As String) As XmlDocument
        Dim xmlDoc As New XmlDocument()
        Dim root As XmlElement = xmlDoc.CreateElement("ECF")
        xmlDoc.AppendChild(root)

        Dim encabezadoNode As XmlElement = xmlDoc.CreateElement("Encabezado")
        root.AppendChild(encabezadoNode)
        Helper.AddElement(xmlDoc, encabezadoNode, "Version", "1.0")

        Dim idDocNode As XmlElement = xmlDoc.CreateElement("IdDoc")
        encabezadoNode.AppendChild(idDocNode)

        Dim facEsc As String = (If(facNumero, "")).Trim().Replace("'", "''")
        Dim supEsc As String = (If(sup_codigo, "")).Trim().Replace("'", "''")
        Dim empEsc As String = (If(emp_codigo, "")).Trim().Replace("'", "''")
        Dim consultaFactura As String =
            "SELECT * FROM vwComprasXML WHERE fac_numero='" & facEsc & "' AND sup_codigo='" & supEsc & "' AND emp_codigo='" & empEsc & "'"
        Dim facturaReader = EjecutarConsultaReader(cadenaConexion, consultaFactura)

        If Not facturaReader.Read() Then
            facturaReader.Close()
            Throw New ApplicationException("No se encontró la compra en vwComprasXML (fac=" & facNumero & ", sup=" & sup_codigo & ", emp=" & emp_codigo & ").")
        End If

        Dim tipoeCF As String = "47"

        Helper.AddElement(xmlDoc, idDocNode, "TipoeCF", tipoeCF)
        glbTipoeCF = tipoeCF
        Helper.AddElement(xmlDoc, idDocNode, "eNCF", eNCF)
        glbncfEnvia = eNCF

        Dim fechaSecuenciaAgregada As Boolean = False
        Dim consultaFechaSecuencia = " select [dbo].[fn_obtenerFechaSecuenciaDGI]('" & emp_codigo & "','" & tipoeCF & "') as Fecha_vence"
        Dim FechasecuenciaReader = EjecutarConsultaReader(cadenaConexion, consultaFechaSecuencia)
        If FechasecuenciaReader.Read() Then
            Dim fven? As DateTime = SafeDate(FechasecuenciaReader("Fecha_vence"), "Fecha_vence")
            If fven.HasValue Then
                Helper.AddElement(xmlDoc, idDocNode, "FechaVencimientoSecuencia", fven.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
                fechaSecuenciaAgregada = True
            End If
        End If
        FechasecuenciaReader.Close()
        If Not fechaSecuenciaAgregada Then
            Throw New ApplicationException("Falta FechaVencimientoSecuencia: fn_obtenerFechaSecuenciaDGI no devolvió fecha para tipo e-CF " & tipoeCF & ".")
        End If

        Helper.AddIfValid(xmlDoc, idDocNode, facturaReader, "tipo_pago", "TipoPago")

        Dim fechaLimitePago As String = Helper.SafeStr(facturaReader, "fac_vence")
        If Not String.IsNullOrEmpty(fechaLimitePago) Then
            Helper.AddElement(xmlDoc, idDocNode, "FechaLimitePago", DateTime.Parse(fechaLimitePago).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))
        End If

        Helper.AddIfValid(xmlDoc, idDocNode, facturaReader, "condicion_pago", "TerminoPago")

        Dim formaPago As String = Helper.SafeStr(facturaReader, "FormaPago")
        Dim valorPagoForma As Decimal = 0D
        Dim tieneMontoPago As Boolean = Helper.TryGetDec(facturaReader, "ValorPagar", valorPagoForma)
        If Not String.IsNullOrWhiteSpace(formaPago) AndAlso tieneMontoPago Then
            Dim tablaFormasPagoNode As XmlElement = xmlDoc.CreateElement("TablaFormasPago")
            idDocNode.AppendChild(tablaFormasPagoNode)
            Dim formaDePagoNode As XmlElement = xmlDoc.CreateElement("FormaDePago")
            tablaFormasPagoNode.AppendChild(formaDePagoNode)
            Helper.AddElement(xmlDoc, formaDePagoNode, "FormaPago", formaPago)
            Helper.AddElement(xmlDoc, formaDePagoNode, "MontoPago", valorPagoForma.ToString("F2", CultureInfo.InvariantCulture))
        End If

        Helper.AddIfValid(xmlDoc, idDocNode, facturaReader, "TipoCuentaPago")
        Helper.AddIfValid(xmlDoc, idDocNode, facturaReader, "NumeroCuentaPago")
        Helper.AddIfValid(xmlDoc, idDocNode, facturaReader, "BancoPago")
        Helper.AddIfValid(xmlDoc, idDocNode, facturaReader, "FechaDesde")
        Helper.AddIfValid(xmlDoc, idDocNode, facturaReader, "FechaHasta")
        Helper.AddIfValid(xmlDoc, idDocNode, facturaReader, "TotalPaginas")

        Helper.EliminarCamposIdDocNoPermitidosE47(idDocNode)

        Dim emisorNode As XmlElement = xmlDoc.CreateElement("Emisor")
        encabezadoNode.AppendChild(emisorNode)
        Helper.AddElement(xmlDoc, emisorNode, "RNCEmisor", facturaReader("emp_rnc").ToString())
        Helper.AddElement(xmlDoc, emisorNode, "RazonSocialEmisor", facturaReader("emp_nombre").ToString())
        Helper.AddElement(xmlDoc, emisorNode, "NombreComercial", facturaReader("emp_nombre").ToString())
        Helper.AddElement(xmlDoc, emisorNode, "DireccionEmisor", facturaReader("emp_direccion").ToString())
        Helper.AppendTablaTelefonoEmisorIfAny(xmlDoc, emisorNode, facturaReader("emp_telefono").ToString())

        Dim correoEmisor As String = Helper.SafeStr(facturaReader, "emp_email")
        If Not String.IsNullOrEmpty(correoEmisor) Then
            Helper.AddElement(xmlDoc, emisorNode, "CorreoEmisor", correoEmisor)
        End If

        Helper.AddIfValid(xmlDoc, emisorNode, facturaReader, "emp_web", "WebSite")
        Helper.AddIfValid(xmlDoc, emisorNode, facturaReader, "NumeroFacturaInterna")
        Helper.AddElement(xmlDoc, emisorNode, "FechaEmision", Convert.ToDateTime(facturaReader("fac_fecha")).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture))

        Dim compradorNode As XmlElement = xmlDoc.CreateElement("Comprador")
        encabezadoNode.AppendChild(compradorNode)

        Dim identificadorExtranjero As String = Helper.SafeStr(facturaReader, "IdentificadorExtranjero")
        If String.IsNullOrWhiteSpace(identificadorExtranjero) Then
            identificadorExtranjero = Helper.SafeStr(facturaReader, "prov_rnc")
        End If
        If String.IsNullOrWhiteSpace(identificadorExtranjero) Then
            identificadorExtranjero = Helper.SafeStr(facturaReader, "prov_cedula")
        End If
        Helper.AddOptionalElement(xmlDoc, compradorNode, "IdentificadorExtranjero", identificadorExtranjero)

        Dim razonSocialComprador As String = Helper.SafeStr(facturaReader, "prov_nombre")
        If String.IsNullOrWhiteSpace(razonSocialComprador) Then
            razonSocialComprador = Helper.SafeStr(facturaReader, "sup_nombre")
        End If
        Helper.AddOptionalElement(xmlDoc, compradorNode, "RazonSocialComprador", razonSocialComprador)

        Dim paisDestino As String = Helper.SafeStr(facturaReader, "PaisDestino")
        If String.IsNullOrWhiteSpace(paisDestino) Then
            paisDestino = Helper.SafeStr(facturaReader, "prov_pais")
        End If
        If String.IsNullOrWhiteSpace(paisDestino) Then
            paisDestino = Helper.SafeStr(facturaReader, "pais_destino")
        End If
        If Not String.IsNullOrWhiteSpace(paisDestino) Then
            Dim transporteNode As XmlElement = xmlDoc.CreateElement("Transporte")
            encabezadoNode.AppendChild(transporteNode)
            Helper.AddElement(xmlDoc, transporteNode, "PaisDestino", paisDestino)
        End If

        Dim totalesNode As XmlElement = xmlDoc.CreateElement("Totales")
        encabezadoNode.AppendChild(totalesNode)

        Dim montoGravado As String = Helper.SafeStr(facturaReader, "fac_grabado")
        Dim montoExentoVista As String = Helper.SafeStr(facturaReader, "fac_exento")
        If montoExentoVista = "" Then montoExentoVista = Helper.SafeStr(facturaReader, "monto_exento")
        Dim totalItbis As String = Helper.SafeStr(facturaReader, "fac_itbis")
        Dim montoTotal As Decimal = 0D
        If Not Helper.TryGetDec(facturaReader, "fac_total", montoTotal) Then
            Dim gravadoDec As Decimal = 0D
            Dim exentoDec As Decimal = 0D
            Dim itbisDec As Decimal = 0D
            Decimal.TryParse(montoGravado, NumberStyles.Any, CultureInfo.InvariantCulture, gravadoDec)
            Decimal.TryParse(montoExentoVista, NumberStyles.Any, CultureInfo.InvariantCulture, exentoDec)
            Decimal.TryParse(totalItbis, NumberStyles.Any, CultureInfo.InvariantCulture, itbisDec)
            montoTotal = gravadoDec + exentoDec + itbisDec
        End If
        ' E47: DGII trata el monto como exento; no usar desglose gravado/ITBIS.
        Helper.AddElement(xmlDoc, totalesNode, "MontoExento", montoTotal.ToString("F2", CultureInfo.InvariantCulture))
        Helper.AddElement(xmlDoc, totalesNode, "MontoTotal", montoTotal.ToString("F2", CultureInfo.InvariantCulture))

        Helper.AddIfValid(xmlDoc, totalesNode, facturaReader, "MontoPeriodo")
        Helper.AddIfValid(xmlDoc, totalesNode, facturaReader, "SaldoAnterior")
        Helper.AddIfValid(xmlDoc, totalesNode, facturaReader, "MontoAvancePago")

        Dim valorPagar As String = Helper.SafeStr(facturaReader, "ValorPagar")
        Helper.AddOptionalElement(xmlDoc, totalesNode, "ValorPagar", valorPagar)

        Dim totalIsr As String = Helper.SafeStr(facturaReader, "TotalISRRetencion")
        If totalIsr = "" Then totalIsr = Helper.SafeStr(facturaReader, "fac_retencion_isr")
        Helper.AddOptionalElement(xmlDoc, totalesNode, "TotalISRRetencion", totalIsr)

        Dim tipoMoneda As String = Helper.SafeStr(facturaReader, "TipoMoneda")
        Dim tipoCambio As String = Helper.SafeStr(facturaReader, "TipoCambio")
        Dim montoExentoOtraMoneda As String = Helper.SafeStr(facturaReader, "MontoExentoOtraMoneda")
        Dim montoTotalOtraMoneda As String = Helper.SafeStr(facturaReader, "MontoTotalOtraMoneda")
        If Not String.IsNullOrWhiteSpace(tipoMoneda) OrElse
           Not String.IsNullOrWhiteSpace(tipoCambio) OrElse
           Not String.IsNullOrWhiteSpace(montoExentoOtraMoneda) OrElse
           Not String.IsNullOrWhiteSpace(montoTotalOtraMoneda) Then
            Dim otraMonedaNode As XmlElement = xmlDoc.CreateElement("OtraMoneda")
            encabezadoNode.AppendChild(otraMonedaNode)
            Helper.AddOptionalElement(xmlDoc, otraMonedaNode, "TipoMoneda", tipoMoneda)
            Helper.AddOptionalElement(xmlDoc, otraMonedaNode, "TipoCambio", tipoCambio)
            Helper.AddOptionalElement(xmlDoc, otraMonedaNode, "MontoExentoOtraMoneda", montoExentoOtraMoneda)
            Helper.AddOptionalElement(xmlDoc, otraMonedaNode, "MontoTotalOtraMoneda", montoTotalOtraMoneda)
        End If

        facturaReader.Close()

        Dim detallesItemsNode As XmlElement = xmlDoc.CreateElement("DetallesItems")
        root.AppendChild(detallesItemsNode)

        Dim consultadetalleFactura As String =
            "SELECT * FROM vwComprasDetalleXML WHERE fac_numero='" & facEsc & "' AND sup_codigo='" & supEsc & "' AND emp_codigo='" & empEsc & "'"
        Dim detalleReader = EjecutarConsultaReader(cadenaConexion, consultadetalleFactura)
        Dim itemsList As New List(Of Dictionary(Of String, Object))
        While detalleReader.Read()
            Dim itemData As New Dictionary(Of String, Object)
            For i As Integer = 0 To detalleReader.FieldCount - 1
                itemData(detalleReader.GetName(i)) = detalleReader.GetValue(i)
            Next
            itemsList.Add(itemData)
        End While
        detalleReader.Close()

        If itemsList.Count = 0 Then
            Throw New ApplicationException("La compra no tiene líneas en vwComprasDetalleXML (fac=" & facNumero & ").")
        End If

        Dim totalItems As Integer = itemsList.Count
        For Each item In itemsList
            Dim itemNode As XmlElement = xmlDoc.CreateElement("Item")
            detallesItemsNode.AppendChild(itemNode)

            Helper.AddElement(xmlDoc, itemNode, "NumeroLinea", item("NumeroLinea").ToString())
            ' E47 (pagos al exterior): DGII solo acepta indicador exento (4).
            Helper.AddElement(xmlDoc, itemNode, "IndicadorFacturacion", "4")

            Dim retencionIsrLinea As Decimal = 0D
            If item.ContainsKey("fac_retencion_isr") AndAlso Not IsDBNull(item("fac_retencion_isr")) Then
                retencionIsrLinea = Decimal.Parse(item("fac_retencion_isr").ToString(), CultureInfo.InvariantCulture) / totalItems
            End If

            Dim retencionNode As XmlElement = xmlDoc.CreateElement("Retencion")
            itemNode.AppendChild(retencionNode)
            Dim indicadorRet As String = "1"
            If item.ContainsKey("IndicadorAgenteRetencionoPercepcion") AndAlso Not IsDBNull(item("IndicadorAgenteRetencionoPercepcion")) Then
                Dim tmpInd = item("IndicadorAgenteRetencionoPercepcion").ToString().Trim()
                If tmpInd <> "" Then indicadorRet = tmpInd
            End If
            Helper.AddElement(xmlDoc, retencionNode, "IndicadorAgenteRetencionoPercepcion", indicadorRet)
            Helper.AddElement(xmlDoc, retencionNode, "MontoISRRetenido", retencionIsrLinea.ToString("F2", CultureInfo.InvariantCulture))

            Helper.AddElement(xmlDoc, itemNode, "NombreItem", item("NombreItem").ToString())
            ' E47 (pagos al exterior): DGII solo acepta servicio (2).
            Helper.AddElement(xmlDoc, itemNode, "IndicadorBienoServicio", "2")
            If item.ContainsKey("DescripcionItem") AndAlso Not IsDBNull(item("DescripcionItem")) Then
                Dim descItem As String = item("DescripcionItem").ToString().Trim()
                If descItem <> "" Then Helper.AddElement(xmlDoc, itemNode, "DescripcionItem", descItem)
            End If

            Helper.AddElement(xmlDoc, itemNode, "CantidadItem",
                Decimal.Parse(item("CantidadItem").ToString(), CultureInfo.InvariantCulture).ToString("F2", CultureInfo.InvariantCulture))
            If item.ContainsKey("UnidadMedida") AndAlso Not IsDBNull(item("UnidadMedida")) Then
                Dim unidad As String = item("UnidadMedida").ToString().Trim()
                If unidad <> "" Then Helper.AddElement(xmlDoc, itemNode, "UnidadMedida", unidad)
            End If
            Helper.AddElement(xmlDoc, itemNode, "PrecioUnitarioItem",
                Decimal.Parse(item("PrecioUnitarioItem").ToString(), CultureInfo.InvariantCulture).ToString("F2", CultureInfo.InvariantCulture))
            Helper.AddElement(xmlDoc, itemNode, "MontoItem",
                Decimal.Parse(item("MontoItem").ToString(), CultureInfo.InvariantCulture).ToString("F2", CultureInfo.InvariantCulture))
        Next

        Dim fechaHoraFirmaNode As XmlElement = xmlDoc.CreateElement("FechaHoraFirma")
        root.AppendChild(fechaHoraFirmaNode)
        fechaHoraFirmaNode.InnerText = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture)

        Helper.ValidarXmlE47(xmlDoc)
        Return xmlDoc
    End Function
    Private Shared Async Function ObtenerDirectorioPorRNCAsync(rnc As String, token As String) As Task(Of String)
        Dim consulta = Helper.ConsultarDirectorioPorRncHttp(GlobalVariables.url_base, token, rnc)
        If consulta.Encontrado AndAlso Not String.IsNullOrWhiteSpace(consulta.UrlRecepcion) Then
            Return consulta.UrlRecepcion
        End If
        Return Await Task.FromResult("")
    End Function


    Private Shared ReadOnly DateFormats As String() = {
    "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy",
    "dd-MM-yyyy", "MM-dd-yyyy", "yyyyMMdd", "ddMMyyyy",
    "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "MM/dd/yyyy HH:mm:ss"
}

    Private Shared Function TryParseAnyDate(s As String, ByRef dt As DateTime) As Boolean
        If String.IsNullOrWhiteSpace(s) Then Return False
        Return DateTime.TryParseExact(
        s.Trim(),
        DateFormats,
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        dt
    )
    End Function

    Private Shared Function SafeDate(value As Object, campo As String) As Nullable(Of DateTime)
        Try
            If value Is Nothing OrElse Convert.IsDBNull(value) Then Return Nothing

            Dim dt As DateTime
            ' Intenta convertir directamente si ya es DateTime
            If TypeOf value Is DateTime Then
                Return CType(value, DateTime)
            End If

            ' Si es string, intenta con formato ISO primero
            Dim s As String = value.ToString().Trim()
            If DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, dt) Then
                Return dt
            End If

            ' Ãšltimo recurso: parse normal (puede fallar segÃºn regiÃ³n)
            If DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, dt) Then
                Return dt
            End If

            ' Si llega aquÃ­, loguea la fecha cruda
            Helper.RegistrarLogCliente("Fecha invÃ¡lida en campo [" & campo & "]: '" & s & "'")
            Return Nothing

        Catch ex As Exception
            Helper.RegistrarLogCliente("Error parseando fecha en campo [" & campo & "]: " & ex.Message)
            Return Nothing
        End Try
    End Function


End Class


