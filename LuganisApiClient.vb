Imports System
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports Newtonsoft.Json
Imports System.Threading.Tasks

' Responsabilidad: encapsular las llamadas HTTP hacia los servicios de LUGANIS:
' - Login: POST /authentication-service/auth/login/COMPANY
' - Envío: POST /parser-service/send

Friend Class LuganisApiClient

    Private Shared ReadOnly _httpClient As HttpClient = CrearHttpClient()

    Private Shared Function CrearHttpClient() As HttpClient
        Dim c As New HttpClient()
        ' Evita que el envío quede colgado indefinidamente contra staging/red
        c.Timeout = TimeSpan.FromSeconds(60)
        Return c
    End Function

    ''' <summary>
    ''' Realiza el login contra LUGANIS utilizando los datos de LuganisLoginRequest.
    ''' </summary>
    Friend Shared Async Function LoginAsync(request As LuganisLoginRequest) As Task(Of LuganisLoginResponse)
        If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))

        Dim response As New LuganisLoginResponse()

        Try
            Dim baseUrl = If(request.BaseUrl, String.Empty).TrimEnd("/"c)
            Dim url = baseUrl & "/authentication-service/auth/login/" & request.CompanyCode

            Dim bodyObject = New With {
                .identifierValue = request.Username,
                .authenticationValue = request.Password,
                .deviceInfo = New With {
                    .appVersion = request.AppVersion,
                    .os = request.Os,
                    .deviceId = request.DeviceId,
                    .latitude = request.Latitude,
                    .longitude = request.Longitude,
                    .providerIpAddress = request.ProviderIpAddress
                }
            }

            Dim bodyJson = JsonConvert.SerializeObject(bodyObject)
            Dim content = New StringContent(bodyJson, Encoding.UTF8, "application/json")

            Dim httpResponse = Await _httpClient.PostAsync(url, content).ConfigureAwait(False)
            response.HttpStatusCode = CInt(httpResponse.StatusCode)
            response.RawResponse = Await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(False)
            Helper.RegistrarLogCliente($"[LUGANIS] Login HTTP={response.HttpStatusCode} url={url}")

            If httpResponse.IsSuccessStatusCode Then
                Dim token As String = Nothing
                Try
                    Dim jo = Newtonsoft.Json.Linq.JObject.Parse(response.RawResponse)
                    Dim data = jo("data")
                    If data IsNot Nothing Then
                        Dim tokenObj = data("token")
                        If tokenObj IsNot Nothing AndAlso tokenObj("accessToken") IsNot Nothing Then
                            token = CStr(tokenObj("accessToken"))
                        End If
                    End If
                    If String.IsNullOrWhiteSpace(token) AndAlso jo("token") IsNot Nothing Then
                        token = CStr(jo("token"))
                    End If
                    If String.IsNullOrWhiteSpace(token) AndAlso jo("accessToken") IsNot Nothing Then
                        token = CStr(jo("accessToken"))
                    End If
                Catch
                    token = Nothing
                End Try

                If Not String.IsNullOrWhiteSpace(token) Then
                    response.Success = True
                    response.Token = token
                    response.ErrorMessage = Nothing
                Else
                    response.Success = False
                    response.ErrorMessage = "Login en LUGANIS respondió 200 pero no se encontró el token en la respuesta."
                End If
            Else
                response.Success = False
                response.ErrorMessage = $"Error HTTP en login LUGANIS: {(CInt(httpResponse.StatusCode))} - {httpResponse.ReasonPhrase}"
            End If
        Catch ex As Exception
            ' Devolver el detalle completo de la excepción para poder diagnosticar problemas de red/TLS/proxy desde Delphi.
            response.Success = False
            response.ErrorMessage = ex.ToString()
            response.RawResponse = Nothing
            Helper.RegistrarLogCliente("[LUGANIS] Login EXCEPCION: " & ex.ToString())
        End Try

        Return response
    End Function

    ''' <summary>
    ''' Envía el archivo TXT (en Base64) a LUGANIS usando POST /parser-service/send.
    ''' </summary>
    Friend Shared Async Function SendAsync(request As LuganisSendRequest) As Task(Of LuganisSendResponse)
        If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))

        Dim result As New LuganisSendResponse()

        Try
            Dim baseUrl = If(request.BaseUrl, String.Empty).TrimEnd("/"c)
            Dim url = baseUrl & "/parser-service/send"

            Dim bodyObject = New With {
                .filename = request.FileName,
                .filecontent = request.FileContentBase64
            }

            Dim bodyJson = JsonConvert.SerializeObject(bodyObject)
            Dim content = New StringContent(bodyJson, Encoding.UTF8, "application/json")

            Dim req As New HttpRequestMessage(HttpMethod.Post, url) With {
                .Content = content
            }
            req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", request.Token)
            If Not String.IsNullOrWhiteSpace(request.DeviceId) Then
                req.Headers.TryAddWithoutValidation("device-id", request.DeviceId)
            End If

            Dim httpResponse = Await _httpClient.SendAsync(req).ConfigureAwait(False)
            result.HttpStatusCode = CInt(httpResponse.StatusCode)
            result.RawResponse = Await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(False)

            If httpResponse.IsSuccessStatusCode Then
                Dim bizCode As String = Nothing
                Dim bizMsg As String = Nothing
                Try
                    Dim jo = Newtonsoft.Json.Linq.JObject.Parse(result.RawResponse)
                    ' status.code "0" = éxito de negocio; cualquier otro (1006, 2007...) es rechazo
                    Dim st = jo.SelectToken("status")
                    If st IsNot Nothing Then
                        If st("code") IsNot Nothing Then bizCode = st("code").ToString().Trim()
                        If st("message") IsNot Nothing Then bizMsg = st("message").ToString().Trim()
                    End If
                    If Not String.IsNullOrWhiteSpace(bizCode) AndAlso bizCode <> "0" Then
                        result.Success = False
                        result.ErrorMessage = $"LUGANIS rechazó el envío (code={bizCode}): {If(bizMsg, "sin mensaje")}"
                        Helper.RegistrarLogCliente("[LUGANIS] Send RECHAZADO: " & result.ErrorMessage & " Body=" & If(result.RawResponse, ""))
                        Return result
                    End If

                    ' Acepta data.trackId, trackId raíz o cualquier trackId
                    Dim tok = jo.SelectToken("data.trackId")
                    If tok Is Nothing OrElse tok.Type = Newtonsoft.Json.Linq.JTokenType.Null Then tok = jo.SelectToken("trackId")
                    If tok Is Nothing OrElse tok.Type = Newtonsoft.Json.Linq.JTokenType.Null Then tok = jo.SelectToken("$..trackId")
                    If tok IsNot Nothing AndAlso tok.Type <> Newtonsoft.Json.Linq.JTokenType.Null Then
                        result.TrackId = tok.ToString()
                    End If
                Catch
                End Try

                result.Success = True
                If String.IsNullOrWhiteSpace(result.TrackId) Then
                    Dim body = If(result.RawResponse, "")
                    If body.Length > 800 Then body = body.Substring(0, 800) & "..."
                    Helper.RegistrarLogCliente("[LUGANIS] Send HTTP 200 pero sin trackId. Body=" & body)
                End If
            Else
                result.Success = False
                result.ErrorMessage = $"Error HTTP al enviar archivo a LUGANIS: {(CInt(httpResponse.StatusCode))} - {httpResponse.ReasonPhrase}"
            End If
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = ex.Message
            result.RawResponse = Nothing
        End Try

        Return result
    End Function

    ''' <summary>
    ''' Obtiene la URL del código QR desde LUGANIS (GET /client-service/download/QR/...).
    ''' </summary>
    Friend Shared Async Function DownloadQrAsync(request As LuganisQrRequest) As Task(Of LuganisQrResponse)
        If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))

        Dim result As New LuganisQrResponse()

        Try
            Dim baseUrl = If(request.BaseUrl, String.Empty).TrimEnd("/"c)
            Dim rncLimpio As String = New String(If(request.RncEmisor, "").Where(Function(c) Char.IsDigit(c)).ToArray())
            Dim encfLimpio As String = If(request.ENcf, String.Empty).Trim()
            If encfLimpio.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) Then
                encfLimpio = encfLimpio.Substring(0, encfLimpio.Length - 4)
            End If

            If String.IsNullOrEmpty(rncLimpio) OrElse String.IsNullOrEmpty(encfLimpio) Then
                result.Success = False
                result.ErrorMessage = "RncEmisor y eNCF son obligatorios para obtener el QR."
                Return result
            End If

            Dim clientId As String = "RNC-" & rncLimpio
            Dim url = baseUrl & "/client-service/download/QR/" & Uri.EscapeDataString(clientId) & "/" & Uri.EscapeDataString(encfLimpio)

            Dim req As New HttpRequestMessage(HttpMethod.Get, url)
            req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", request.Token)
            If Not String.IsNullOrWhiteSpace(request.DeviceId) Then
                req.Headers.TryAddWithoutValidation("device-id", request.DeviceId)
            End If

            Dim httpResponse = Await _httpClient.SendAsync(req).ConfigureAwait(False)
            result.HttpStatusCode = CInt(httpResponse.StatusCode)
            result.RawResponse = Await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(False)

            If httpResponse.IsSuccessStatusCode Then
                result.Success = True
                Try
                    Dim jo = Newtonsoft.Json.Linq.JObject.Parse(result.RawResponse)
                    Dim tok = jo.SelectToken("data.detail.qrCode")
                    If tok Is Nothing OrElse tok.Type = Newtonsoft.Json.Linq.JTokenType.Null Then tok = jo.SelectToken("data.qrCode")
                    If tok Is Nothing OrElse tok.Type = Newtonsoft.Json.Linq.JTokenType.Null Then tok = jo.SelectToken("qrCode")
                    If tok Is Nothing OrElse tok.Type = Newtonsoft.Json.Linq.JTokenType.Null Then tok = jo.SelectToken("$..qrCode")
                    If tok IsNot Nothing AndAlso tok.Type <> Newtonsoft.Json.Linq.JTokenType.Null Then
                        result.QrCode = tok.ToString()
                    End If
                Catch
                End Try
                If String.IsNullOrWhiteSpace(result.QrCode) Then
                    Dim body = If(result.RawResponse, "")
                    If body.Length > 800 Then body = body.Substring(0, 800) & "..."
                    Helper.RegistrarLogCliente($"[LUGANIS] QR HTTP={result.HttpStatusCode} sin qrCode. url={url} Body={body}")
                    result.Success = False
                    result.ErrorMessage = "Respuesta QR sin campo qrCode (documento quizá aún no procesado)."
                End If
            Else
                result.Success = False
                result.ErrorMessage = $"Error HTTP al obtener QR de LUGANIS: {result.HttpStatusCode} - {httpResponse.ReasonPhrase}"
                Helper.RegistrarLogCliente($"[LUGANIS] QR HTTP={result.HttpStatusCode} url={url} Body={If(result.RawResponse, "")}")
            End If
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = ex.Message
            result.RawResponse = Nothing
            Helper.RegistrarLogCliente("[LUGANIS] QR EXCEPCION: " & ex.ToString())
        End Try

        Return result
    End Function

    ''' <summary>
    ''' Consulta el estado del documento por trackId (GET /parser-service/read/trackId/{{TRACK-ID}}).
    ''' La respuesta puede incluir status "Aceptado", "Rechazado", "Aceptado Condicional", etc.
    ''' </summary>
    Friend Shared Async Function GetStatusByTrackIdAsync(
        baseUrl As String,
        token As String,
        deviceId As String,
        trackId As String
    ) As Task(Of LuganisStatusResponse)
        Dim result As New LuganisStatusResponse()
        If String.IsNullOrWhiteSpace(baseUrl) OrElse String.IsNullOrWhiteSpace(trackId) Then
            result.Success = False
            result.ErrorMessage = "baseUrl y trackId son obligatorios."
            Return result
        End If

        Try
            Dim urlBase = (If(baseUrl, String.Empty)).TrimEnd("/"c)
            Dim url = urlBase & "/parser-service/read/trackId/" & Uri.EscapeDataString(trackId.Trim())

            Dim req As New HttpRequestMessage(HttpMethod.Get, url)
            req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", token)
            If Not String.IsNullOrWhiteSpace(deviceId) Then
                req.Headers.TryAddWithoutValidation("device-id", deviceId)
            End If

            Dim httpResponse = Await _httpClient.SendAsync(req).ConfigureAwait(False)
            result.HttpStatusCode = CInt(httpResponse.StatusCode)
            result.RawResponse = Await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(False)
            Helper.RegistrarLogCliente($"[LUGANIS] STATUS trackId HTTP={result.HttpStatusCode} trackId={trackId}")

            If httpResponse.IsSuccessStatusCode Then
                result.Success = True
                Try
                    Dim jo = Newtonsoft.Json.Linq.JObject.Parse(result.RawResponse)
                    Dim data = jo("data")
                    If data IsNot Nothing Then
                        ' Preferir data.status (Aceptado/Pendiente/Rechazado) sobre status raíz (objeto code/message)
                        If data("status") IsNot Nothing AndAlso data("status").Type = Newtonsoft.Json.Linq.JTokenType.String Then
                            result.Status = CStr(data("status")).Trim()
                        End If
                        Dim detail = data("detail")
                        If detail IsNot Nothing Then
                            If detail("responseMessage") IsNot Nothing Then
                                result.ResponseMessage = CStr(detail("responseMessage")).Trim()
                            End If
                            If detail("responseCode") IsNot Nothing Then result.ResponseCode = CStr(detail("responseCode")).Trim()
                            If detail("messages") IsNot Nothing AndAlso String.IsNullOrWhiteSpace(result.ResponseMessage) Then
                                result.ResponseMessage = detail("messages").ToString()
                            End If
                            If detail("errorMessage") IsNot Nothing AndAlso String.IsNullOrWhiteSpace(result.ResponseMessage) Then
                                result.ResponseMessage = CStr(detail("errorMessage")).Trim()
                            End If
                        End If
                    End If
                    If String.IsNullOrWhiteSpace(result.Status) AndAlso jo("status") IsNot Nothing AndAlso jo("status").Type = Newtonsoft.Json.Linq.JTokenType.String Then
                        result.Status = CStr(jo("status")).Trim()
                    End If
                    If jo("responseMessage") IsNot Nothing AndAlso String.IsNullOrWhiteSpace(result.ResponseMessage) Then
                        result.ResponseMessage = CStr(jo("responseMessage")).Trim()
                    End If
                    If jo("code") IsNot Nothing AndAlso String.IsNullOrWhiteSpace(result.ResponseCode) Then
                        result.ResponseCode = CStr(jo("code")).Trim()
                    End If
                    If jo("message") IsNot Nothing AndAlso String.IsNullOrWhiteSpace(result.ResponseMessage) Then
                        result.ResponseMessage = CStr(jo("message")).Trim()
                    End If
                    If String.IsNullOrWhiteSpace(result.Status) AndAlso Not String.IsNullOrWhiteSpace(result.ResponseMessage) Then
                        result.Status = result.ResponseMessage
                    End If
                    ' Log completo de STATUS para diagnóstico
                    Dim bodyLog = If(result.RawResponse, "")
                    If bodyLog.Length > 1200 Then bodyLog = bodyLog.Substring(0, 1200) & "..."
                    Helper.RegistrarLogCliente($"[LUGANIS] STATUS trackId detalle: status={If(result.Status, "")} code={If(result.ResponseCode, "")} msg={If(result.ResponseMessage, "")} Body={bodyLog}")
                Catch exParse As Exception
                    Helper.RegistrarLogCliente("[LUGANIS] STATUS trackId parse error: " & exParse.Message & " Body=" & If(result.RawResponse, ""))
                End Try
            Else
                result.Success = False
                result.ErrorMessage = $"Error HTTP al consultar estado: {result.HttpStatusCode} - {httpResponse.ReasonPhrase}"
                Helper.RegistrarLogCliente($"[LUGANIS] STATUS trackId ERROR: {result.ErrorMessage} Body={If(result.RawResponse, "")}")
            End If
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = ex.Message
            result.RawResponse = Nothing
            Helper.RegistrarLogCliente("[LUGANIS] STATUS trackId EXCEPCION: " & ex.ToString())
        End Try

        Return result
    End Function

    ''' <summary>
    ''' Consulta estado por documento (GET /client-service/download/STATUS/RNC-{{rnc}}/{{eNCF}}).
    ''' Útil cuando el send no devolvió trackId.
    ''' </summary>
    Friend Shared Async Function GetStatusByDocumentAsync(
        baseUrl As String,
        token As String,
        deviceId As String,
        rncEmisor As String,
        eNCF As String
    ) As Task(Of LuganisStatusResponse)
        Dim result As New LuganisStatusResponse()
        Try
            Dim rncLimpio As String = New String(If(rncEmisor, "").Where(Function(c) Char.IsDigit(c)).ToArray())
            Dim encfLimpio As String = If(eNCF, String.Empty).Trim()
            If encfLimpio.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) Then
                encfLimpio = encfLimpio.Substring(0, encfLimpio.Length - 4)
            End If
            If String.IsNullOrWhiteSpace(baseUrl) OrElse String.IsNullOrEmpty(rncLimpio) OrElse String.IsNullOrEmpty(encfLimpio) Then
                result.Success = False
                result.ErrorMessage = "baseUrl, RNC y eNCF son obligatorios para STATUS."
                Return result
            End If

            Dim clientId As String = "RNC-" & rncLimpio
            Dim url = (If(baseUrl, String.Empty)).TrimEnd("/"c) &
                       "/client-service/download/STATUS/" & Uri.EscapeDataString(clientId) & "/" & Uri.EscapeDataString(encfLimpio)

            Dim req As New HttpRequestMessage(HttpMethod.Get, url)
            req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", token)
            If Not String.IsNullOrWhiteSpace(deviceId) Then
                req.Headers.TryAddWithoutValidation("device-id", deviceId)
            End If

            Dim httpResponse = Await _httpClient.SendAsync(req).ConfigureAwait(False)
            result.HttpStatusCode = CInt(httpResponse.StatusCode)
            result.RawResponse = Await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(False)
            Helper.RegistrarLogCliente($"[LUGANIS] STATUS doc HTTP={result.HttpStatusCode} url={url}")

            If Not httpResponse.IsSuccessStatusCode Then
                result.Success = False
                result.ErrorMessage = $"Error HTTP STATUS: {result.HttpStatusCode}"
                Return result
            End If

            Try
                Dim jo = Newtonsoft.Json.Linq.JObject.Parse(result.RawResponse)
                Dim st = jo.SelectToken("status")
                Dim bizCode As String = Nothing
                Dim bizMsg As String = Nothing
                If st IsNot Nothing Then
                    If st.Type = Newtonsoft.Json.Linq.JTokenType.Object Then
                        If st("code") IsNot Nothing Then bizCode = st("code").ToString().Trim()
                        If st("message") IsNot Nothing Then bizMsg = st("message").ToString().Trim()
                    ElseIf st.Type = Newtonsoft.Json.Linq.JTokenType.String Then
                        result.Status = CStr(st).Trim()
                    End If
                End If
                If Not String.IsNullOrWhiteSpace(bizCode) AndAlso bizCode <> "0" AndAlso bizCode <> "8" Then
                    ' 8 = éxito en STATUS por documento según docs; 0 = éxito genérico
                    result.Success = False
                    result.ResponseCode = bizCode
                    result.ResponseMessage = bizMsg
                    result.Status = If(bizMsg, "Error")
                    result.ErrorMessage = $"LUGANIS STATUS error (code={bizCode}): {If(bizMsg, "sin mensaje")}"
                    Helper.RegistrarLogCliente("[LUGANIS] STATUS doc RECHAZADO: " & result.ErrorMessage)
                    Return result
                End If

                result.Success = True
                ' Formato doc: data[0].detail.responseMessage = "Aceptado"
                Dim detail = jo.SelectToken("data[0].detail")
                If detail Is Nothing Then detail = jo.SelectToken("data.detail")
                If detail IsNot Nothing Then
                    If detail("responseMessage") IsNot Nothing Then result.ResponseMessage = CStr(detail("responseMessage")).Trim()
                    If detail("responseCode") IsNot Nothing Then result.ResponseCode = CStr(detail("responseCode")).Trim()
                    If Not String.IsNullOrWhiteSpace(result.ResponseMessage) Then result.Status = result.ResponseMessage
                End If
                If String.IsNullOrWhiteSpace(result.Status) AndAlso Not String.IsNullOrWhiteSpace(bizMsg) Then
                    result.Status = bizMsg
                End If
            Catch exParse As Exception
                result.Success = False
                result.ErrorMessage = exParse.Message
                Helper.RegistrarLogCliente("[LUGANIS] STATUS parse: " & exParse.Message)
            End Try
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = ex.Message
            Helper.RegistrarLogCliente("[LUGANIS] STATUS EXCEPCION: " & ex.ToString())
        End Try
        Return result
    End Function

End Class

