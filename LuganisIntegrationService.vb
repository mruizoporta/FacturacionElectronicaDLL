Imports System
Imports System.Collections.Generic
Imports System.Text
Imports System.Threading.Tasks
Imports System.Xml

' Responsabilidad: orquestar todo el flujo de integración con LUGANIS
' a partir del XML de e-CF generado por esta misma DLL.
' NO modifica el comportamiento actual con la DGII.

Friend Class LuganisIntegrationService
    ' Helper para logging (siempre escribe pasos críticos; logDebug añade detalle extra)
    Private Shared Sub LogLuganis(msg As String, Optional logDebug As Boolean = True)
        ' Pasos con prefijo numérico (4.x) siempre van al log para diagnosticar envíos
        ' aunque Delphi llame con logDebug=False.
        Helper.RegistrarLogCliente("[LUGANIS] " & msg)
    End Sub

    ''' <summary>
    ''' Flujo de alto nivel:
    ''' 1. Convertir el XML a DocumentoLuganisDto (LuganisXmlReader).
    ''' 2. Construir el TXT según tipo de e-CF (LuganisTxtBuilder).
    ''' 3. Codificar TXT en UTF-8 sin BOM y luego en Base64.
    ''' 4. Hacer login en LUGANIS (LuganisApiClient.LoginAsync).
    ''' 5. Enviar el archivo (LuganisApiClient.SendAsync).
    ''' 6. Devolver un LuganisResult claro para el consumidor (y, más adelante, para Delphi).
    ''' </summary>
    ''' <param name="xmlDoc">XML de e-CF ya generado por los métodos existentes.</param>
    ''' <param name="tipoECF">Tipo de e-CF (31, 32, 33, 34, 41, 43, 44, 45, 46, 47, etc.).</param>
    ''' <param name="rncEmisor">RNC del emisor, utilizado para formar el filename.</param>
    ''' <param name="eNCF">Secuencia e-CF, utilizada para formar el filename y trazabilidad.</param>
    ''' <param name="loginRequest">Parámetros de login/configuración para LUGANIS.</param>
    ''' <param name="logDebug">Si True, registra cada paso en LogsCliente para depuración.</param>
    Friend Shared Async Function EnviarAsync(
        xmlDoc As XmlDocument,
        tipoECF As String,
        rncEmisor As String,
        eNCF As String,
        loginRequest As LuganisLoginRequest,
        Optional logDebug As Boolean = False
    ) As Task(Of LuganisResult)

        If xmlDoc Is Nothing Then Throw New ArgumentNullException(NameOf(xmlDoc))
        If String.IsNullOrWhiteSpace(tipoECF) Then Throw New ArgumentException("tipoECF es obligatorio.", NameOf(tipoECF))
        If String.IsNullOrWhiteSpace(rncEmisor) Then Throw New ArgumentException("rncEmisor es obligatorio.", NameOf(rncEmisor))
        If loginRequest Is Nothing Then Throw New ArgumentNullException(NameOf(loginRequest))

        Try
            LogLuganis("4.1 Parse XML → DocumentoLuganisDto", logDebug)
            Dim documento As DocumentoLuganisDto = LuganisXmlReader.Parse(xmlDoc, tipoECF)
            ' eNCF del XML manda si el parámetro viene vacío; si ambos existen y difieren, usar el del parámetro (asignado en BD)
            Dim encfFinal As String = If(String.IsNullOrWhiteSpace(eNCF), If(documento.ENcf, "").Trim(), eNCF.Trim())
            If String.IsNullOrWhiteSpace(encfFinal) Then
                Return LuganisResult.Fail("ENCF_VACIO", "No hay eNCF para armar el filename/TXT. No se envía a LUGANIS (evita error 3006).")
            End If
            If Not String.IsNullOrWhiteSpace(documento.ENcf) AndAlso
               Not String.Equals(documento.ENcf.Trim(), encfFinal, StringComparison.OrdinalIgnoreCase) Then
                LogLuganis("WARN: eNCF XML='" & documento.ENcf & "' ≠ eNCF asignado='" & encfFinal & "'; se usa el asignado.", True)
            End If
            documento.ENcf = encfFinal
            documento.RncEmisor = rncEmisor

            LogLuganis("4.2 Build TXT (LuganisTxtBuilder)", logDebug)
            Dim txtContenido As String = LuganisTxtBuilder.BuildTxt(documento)

            LogLuganis("4.3 Codificar TXT → Base64 (UTF-8 sin BOM)", logDebug)
            Dim utf8SinBom As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False)
            Dim bytesTxt As Byte() = utf8SinBom.GetBytes(txtContenido)
            Dim base64Content As String = Convert.ToBase64String(bytesTxt)

            LogLuganis("4.4 Login LUGANIS (POST auth/login)...", logDebug)
            Dim loginResp As LuganisLoginResponse = Await LuganisApiClient.LoginAsync(loginRequest).ConfigureAwait(False)
            LogLuganis("4.4 Login resultado: Success=" & loginResp.Success & " HTTP=" & loginResp.HttpStatusCode, logDebug)
            If Not loginResp.Success OrElse String.IsNullOrWhiteSpace(loginResp.Token) Then
                Return LuganisResult.Fail("LOGIN_FAILED", $"No se pudo autenticar en LUGANIS: {loginResp.ErrorMessage}", loginResp.RawResponse)
            End If

            Dim fileNameFinal As String = ConstruirNombreArchivo(rncEmisor, tipoECF, encfFinal)
            LogLuganis("4.5 Preparar Send (filename=" & fileNameFinal & " eNCF=" & encfFinal & ")", logDebug)
            Dim sendRequest As New LuganisSendRequest With {
                .BaseUrl = loginRequest.BaseUrl,
                .Token = loginResp.Token,
                .DeviceId = loginRequest.DeviceId,
                .FileName = fileNameFinal,
                .FileContentBase64 = base64Content
            }

            LogLuganis("4.6 Enviar TXT (POST parser-service/send)...", logDebug)
            Dim sendResp As LuganisSendResponse = Await LuganisApiClient.SendAsync(sendRequest).ConfigureAwait(False)
            LogLuganis("4.6 Send resultado: Success=" & sendResp.Success & " TrackId=" & If(String.IsNullOrEmpty(sendResp.TrackId), "(nulo)", sendResp.TrackId) & " HTTP=" & sendResp.HttpStatusCode, logDebug)
            If Not sendResp.Success Then
                LogLuganis("4.6 ERROR SEND: " & If(sendResp.ErrorMessage, "") & " | Body=" & TruncarLog(sendResp.RawResponse), True)
                Return LuganisResult.Fail("SEND_FAILED", $"Error al enviar archivo a LUGANIS: {sendResp.ErrorMessage}", sendResp.RawResponse, sendResp.TrackId)
            End If

            ' 4.7 Consultar estado por trackId (o por documento si no hay trackId).
            ' "Pendiente" NO es rechazo: se reintenta hasta Aceptado/Rechazado.
            Dim aceptadoPorLuganis As Boolean? = Nothing
            Dim statusResp As LuganisStatusResponse = Nothing
            If Not String.IsNullOrWhiteSpace(sendResp.TrackId) Then
                Try
                    Const maxIntentosEstado As Integer = 10
                    Const esperaMsEstado As Integer = 2000
                    For intento As Integer = 1 To maxIntentosEstado
                        LogLuganis($"4.7 Consultando estado por trackId (intento {intento}/{maxIntentosEstado})...", logDebug)
                        statusResp = Await LuganisApiClient.GetStatusByTrackIdAsync(
                            loginRequest.BaseUrl,
                            loginResp.Token,
                            loginRequest.DeviceId,
                            sendResp.TrackId
                        ).ConfigureAwait(False)

                        Dim clasif = ClasificarEstadoLuganis(statusResp)
                        aceptadoPorLuganis = clasif.Aceptado
                        LogLuganis("4.7 Estado LUGANIS: " & If(statusResp?.Status, "(sin)") &
                                   " code=" & If(statusResp?.ResponseCode, "") &
                                   " msg=" & If(statusResp?.ResponseMessage, "") &
                                   " => " & clasif.Resumen, True)

                        If clasif.EsFinal Then
                            If clasif.Aceptado.HasValue AndAlso Not clasif.Aceptado.Value Then
                                LogLuganis("4.7 RECHAZO trackId: status=" & If(statusResp?.Status, "") &
                                           " | responseCode=" & If(statusResp?.ResponseCode, "") &
                                           " | responseMessage=" & If(statusResp?.ResponseMessage, "") &
                                           " | Body=" & TruncarLog(statusResp?.RawResponse), True)
                            End If
                            Exit For
                        End If

                        LogLuganis("4.7 Estado pendiente; esperando " & esperaMsEstado & " ms...", True)
                        Await Task.Delay(esperaMsEstado).ConfigureAwait(False)
                    Next
                    If Not aceptadoPorLuganis.HasValue Then
                        LogLuganis("4.7 Quedó PENDIENTE tras reintentos (no se marca rechazo). TrackId=" & sendResp.TrackId &
                                   " Body=" & TruncarLog(statusResp?.RawResponse), True)
                    End If
                Catch exStatus As Exception
                    LogLuganis("4.7 Excepción al consultar estado: " & exStatus.ToString(), True)
                End Try
            Else
                LogLuganis("4.7 Sin trackId; consultando STATUS por documento (RNC/eNCF)...", logDebug)
                LogLuganis("4.7 Send Body (sin trackId)=" & TruncarLog(sendResp.RawResponse), True)
                Try
                    Await Task.Delay(1500).ConfigureAwait(False)
                    statusResp = Await LuganisApiClient.GetStatusByDocumentAsync(
                        loginRequest.BaseUrl,
                        loginResp.Token,
                        loginRequest.DeviceId,
                        rncEmisor,
                        encfFinal
                    ).ConfigureAwait(False)
                    Dim clasifDoc = ClasificarEstadoLuganis(statusResp)
                    aceptadoPorLuganis = clasifDoc.Aceptado
                    LogLuganis("4.7 Estado por documento: " & If(statusResp?.Status, "(sin)") &
                               " => " & clasifDoc.Resumen & " Body=" & TruncarLog(statusResp?.RawResponse), True)
                    If clasifDoc.EsFinal AndAlso clasifDoc.Aceptado.HasValue AndAlso Not clasifDoc.Aceptado.Value Then
                        LogLuganis("4.7 RECHAZO documento: " & clasifDoc.Resumen & " | Body=" & TruncarLog(statusResp?.RawResponse), True)
                    End If
                Catch exDoc As Exception
                    LogLuganis("4.7 Excepción STATUS documento: " & exDoc.ToString(), True)
                End Try
            End If

            LogLuganis("4.8 Flujo LUGANIS completado OK", logDebug)
            Dim res = LuganisResult.Ok(sendRequest.FileName, sendResp.RawResponse, txtContenido, sendResp.TrackId, aceptadoPorLuganis)
            If statusResp IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(statusResp.Status) Then
                res.EstadoLuganis = statusResp.Status.Trim()
            End If
            If aceptadoPorLuganis.HasValue AndAlso Not aceptadoPorLuganis.Value Then
                res.Codigo = If(String.IsNullOrWhiteSpace(statusResp?.ResponseCode), "RECHAZADO", statusResp.ResponseCode)
                Dim msgRechazo = ConstruirMotivoRechazo(statusResp)
                res.Mensaje = msgRechazo
                LogLuganis("4.8 NO ACEPTADO: " & msgRechazo, True)
            ElseIf Not aceptadoPorLuganis.HasValue Then
                res.Codigo = "PENDIENTE"
                Dim motivoPendiente =
                    "Documento en estado Pendiente/en proceso (aún no Aceptado ni Rechazado). TrackId=" &
                    If(String.IsNullOrWhiteSpace(sendResp.TrackId), "(nulo)", sendResp.TrackId) &
                    " | STATUS=" & If(statusResp?.Status, "(sin status)") &
                    " | StatusBody=" & TruncarLog(statusResp?.RawResponse)
                res.Mensaje = motivoPendiente
                If statusResp IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(statusResp.RawResponse) Then
                    res.RawResponse = statusResp.RawResponse
                End If
                LogLuganis("4.8 PENDIENTE (sin aceptación final): " & motivoPendiente, True)
            End If
            Return res

        Catch ex As Exception
            Helper.RegistrarLogCliente("[LUGANIS] EXCEPCION en EnviarAsync: " & ex.ToString())
            Return LuganisResult.Fail("EXCEPTION", ex.Message)
        End Try
    End Function

    Private Structure ClasificacionEstadoLuganis
        Public Aceptado As Boolean?
        Public EsFinal As Boolean
        Public Resumen As String
    End Structure

    ''' <summary>
    ''' Aceptado / Aceptado Condicional => True (final).
    ''' Rechazado => False (final).
    ''' Pendiente / En Proceso / vacío => Nothing (no final; reintentar).
    ''' </summary>
    Private Shared Function ClasificarEstadoLuganis(statusResp As LuganisStatusResponse) As ClasificacionEstadoLuganis
        Dim r As New ClasificacionEstadoLuganis With {
            .Aceptado = Nothing,
            .EsFinal = False,
            .Resumen = "SIN_ESTADO"
        }
        If statusResp Is Nothing OrElse Not statusResp.Success Then
            r.Resumen = "SIN_RESPUESTA"
            Return r
        End If

        Dim est = If(statusResp.Status, "").Trim()
        Dim code = If(statusResp.ResponseCode, "").Trim()
        Dim msg = If(statusResp.ResponseMessage, "").Trim()

        If String.Equals(est, "Pendiente", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(est, "En Proceso", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(est, "Enviado", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(est, "Procesando", StringComparison.OrdinalIgnoreCase) OrElse
           String.IsNullOrWhiteSpace(est) Then
            r.Aceptado = Nothing
            r.EsFinal = False
            r.Resumen = "PENDIENTE (AceptadoPorLuganis=null)"
            Return r
        End If

        If String.Equals(est, "Aceptado", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(est, "Aceptado Condicional", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(code, "1", StringComparison.OrdinalIgnoreCase) Then
            r.Aceptado = True
            r.EsFinal = True
            r.Resumen = "ACEPTADO"
            Return r
        End If

        If String.Equals(est, "Rechazado", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(est, "Rejected", StringComparison.OrdinalIgnoreCase) OrElse
           msg.IndexOf("Rechaz", StringComparison.OrdinalIgnoreCase) >= 0 Then
            r.Aceptado = False
            r.EsFinal = True
            r.Resumen = "RECHAZADO"
            Return r
        End If

        ' Estado desconocido: no marcar rechazo automático
        r.Aceptado = Nothing
        r.EsFinal = False
        r.Resumen = "DESCONOCIDO:" & est
        Return r
    End Function

    Private Shared Function TruncarLog(texto As String, Optional maxLen As Integer = 1200) As String
        If String.IsNullOrWhiteSpace(texto) Then Return "(vacío)"
        Dim t = texto.Trim()
        If t.Length <= maxLen Then Return t
        Return t.Substring(0, maxLen) & "...(truncado)"
    End Function

    Private Shared Function ConstruirMotivoRechazo(statusResp As LuganisStatusResponse) As String
        If statusResp Is Nothing Then Return "Documento rechazado por LUGANIS (sin detalle de STATUS)."
        Dim partes As New List(Of String)
        If Not String.IsNullOrWhiteSpace(statusResp.Status) Then partes.Add("status=" & statusResp.Status.Trim())
        If Not String.IsNullOrWhiteSpace(statusResp.ResponseCode) Then partes.Add("code=" & statusResp.ResponseCode.Trim())
        If Not String.IsNullOrWhiteSpace(statusResp.ResponseMessage) Then partes.Add("message=" & statusResp.ResponseMessage.Trim())
        If Not String.IsNullOrWhiteSpace(statusResp.ErrorMessage) Then partes.Add("error=" & statusResp.ErrorMessage.Trim())
        If partes.Count = 0 Then
            Return "Documento rechazado por LUGANIS. Body=" & TruncarLog(statusResp.RawResponse)
        End If
        Return String.Join(" | ", partes) & " | Body=" & TruncarLog(statusResp.RawResponse)
    End Function

    ''' <summary>
    ''' Construye el nombre de archivo en el formato RNCE99SECUENCIAL.txt
    ''' Ejemplo: 132944372E310000002238.txt
    ''' Si eNCF ya tiene formato E310000002238, se usa directamente.
    ''' </summary>
    Private Shared Function ConstruirNombreArchivo(rncEmisor As String, tipoECF As String, eNCF As String) As String
        If String.IsNullOrWhiteSpace(rncEmisor) Then Throw New ArgumentException("rncEmisor es obligatorio.", NameOf(rncEmisor))
        If String.IsNullOrWhiteSpace(tipoECF) Then Throw New ArgumentException("tipoECF es obligatorio.", NameOf(tipoECF))

        Dim rncLimpio As String = New String(rncEmisor.Where(Function(c) Char.IsDigit(c)).ToArray())
        Dim eNCFLimpio As String = If(eNCF, String.Empty).Trim()

        If String.IsNullOrWhiteSpace(eNCFLimpio) Then
            Throw New ArgumentException("eNCF es obligatorio para el filename LUGANIS (evita inventar E340000000001).", NameOf(eNCF))
        End If

        If eNCFLimpio.StartsWith("E", StringComparison.OrdinalIgnoreCase) OrElse eNCFLimpio.Length >= 3 Then
            Return rncLimpio & eNCFLimpio & ".txt"
        End If

        Dim prefijoTipo As String = "E" & tipoECF.Trim()
        Dim numeros As String = New String(eNCFLimpio.Where(Function(c) Char.IsDigit(c)).ToArray())
        Dim secuencial As String = If(numeros.Length > 0, numeros.PadLeft(10, "0"c), "0000000001")
        If secuencial.Length > 10 Then secuencial = secuencial.Substring(secuencial.Length - 10)
        Return rncLimpio & prefijoTipo & secuencial & ".txt"
    End Function

End Class

