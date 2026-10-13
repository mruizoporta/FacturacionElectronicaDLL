Imports Newtonsoft.Json.Linq
Imports System.Globalization
Imports System.IO
Imports System.Net
Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates
Imports System.Security.Cryptography.Xml
Imports System.Text
Imports System.Xml

Public Class Seguridad

    ''' <summary>Último error detallado al obtener token DGII (para logs y mensajes al cliente).</summary>
    Public Shared Property UltimoErrorToken As String = ""

    Private Shared tokenExpiraUtc As DateTime = DateTime.MinValue
    Private Shared ReadOnly tokenLock As New Object()
    Private Const MinutosMargenToken As Integer = 2
    Private Const MinutosFallbackToken As Integer = 50

    Public Shared Sub InvalidarToken()
        SyncLock tokenLock
            GlobalVariables.token = String.Empty
            tokenExpiraUtc = DateTime.MinValue
        End SyncLock
    End Sub

    Private Shared Function TokenVigente() As Boolean
        If String.IsNullOrWhiteSpace(GlobalVariables.token) Then Return False
        If tokenExpiraUtc = DateTime.MinValue Then Return False
        Return DateTime.UtcNow.AddMinutes(MinutosMargenToken) < tokenExpiraUtc
    End Function

    Private Shared Function CalcularExpiracionUtc(token As String, jsonResp As JObject) As DateTime
        Dim fallback = DateTime.UtcNow.AddMinutes(MinutosFallbackToken)

        If jsonResp IsNot Nothing Then
            For Each nombre In New String() {"expira", "expiracion", "expires", "expire"}
                Dim v = jsonResp(nombre)
                If v Is Nothing Then Continue For
                Dim d As DateTime
                If DateTime.TryParse(v.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, d) OrElse
                   DateTime.TryParse(v.ToString(), d) Then
                    If d.Kind = DateTimeKind.Unspecified Then
                        d = DateTime.SpecifyKind(d, DateTimeKind.Local)
                    End If
                    Return d.ToUniversalTime()
                End If
            Next
        End If

        Try
            Dim parts = If(token, "").Split("."c)
            If parts.Length >= 2 Then
                Dim payload = parts(1).Replace("-"c, "+"c).Replace("_"c, "/"c)
                Select Case payload.Length Mod 4
                    Case 2 : payload &= "=="
                    Case 3 : payload &= "="
                End Select
                Dim json = Encoding.UTF8.GetString(Convert.FromBase64String(payload))
                Dim jo = JObject.Parse(json)
                Dim exp = jo("exp")
                Dim unix As Long
                If exp IsNot Nothing AndAlso Long.TryParse(exp.ToString(), unix) Then
                    Return New DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix)
                End If
            End If
        Catch
        End Try

        Return fallback
    End Function

    Private Shared Sub RegistrarErrorToken(mensaje As String)
        UltimoErrorToken = If(mensaje, "")
        Helper.RegistrarLogCliente(mensaje)
    End Sub

    Private Shared Function DetalleExcepcion(ex As Exception) As String
        If ex Is Nothing Then Return ""
        Dim sb As New StringBuilder()
        sb.Append(ex.GetType().Name).Append(": ").Append(ex.Message)

        Dim we = TryCast(ex, WebException)
        If we IsNot Nothing Then
            sb.Append(" | WebStatus=").Append(we.Status.ToString())
            Dim http = TryCast(we.Response, HttpWebResponse)
            If http IsNot Nothing Then
                sb.Append(" | Http=").Append(CInt(http.StatusCode)).Append(" ").Append(http.StatusDescription)
                Try
                    Using stream = http.GetResponseStream()
                        If stream IsNot Nothing Then
                            Using reader = New StreamReader(stream)
                                Dim body = reader.ReadToEnd()
                                If Not String.IsNullOrWhiteSpace(body) Then
                                    If body.Length > 800 Then body = body.Substring(0, 800) & "..."
                                    sb.Append(" | Body=").Append(body.Replace(vbCr, " ").Replace(vbLf, " "))
                                End If
                            End Using
                        End If
                    End Using
                Catch
                End Try
            ElseIf Not String.IsNullOrWhiteSpace(we.Message) Then
                ' sin Response: DNS, timeout, conexión rechazada, TLS, etc.
            End If
        End If

        Dim inner = ex.InnerException
        Dim n As Integer = 0
        While inner IsNot Nothing AndAlso n < 4
            sb.Append(" | Inner").Append(n + 1).Append("=").
                Append(inner.GetType().Name).Append(": ").Append(inner.Message)
            inner = inner.InnerException
            n += 1
        End While
        Return sb.ToString()
    End Function

    Private Shared Function LeerCuerpoWebException(ex As WebException) As String
        Try
            If ex Is Nothing OrElse ex.Response Is Nothing Then Return ""
            Using stream = ex.Response.GetResponseStream()
                If stream Is Nothing Then Return ""
                Using reader = New StreamReader(stream)
                    Dim body = reader.ReadToEnd()
                    If body IsNot Nothing AndAlso body.Length > 800 Then
                        Return body.Substring(0, 800) & "..."
                    End If
                    Return If(body, "")
                End Using
            End Using
        Catch
            Return ""
        End Try
    End Function

    Private Shared Function DescargarSemilla(urlBase As String) As XmlDocument
        Dim url As String = RutaHelper.NormalizarUrlBaseDgii(urlBase) & "autenticacion/api/Autenticacion/Semilla"
        Try
            Helper.RegistrarLogCliente("[TOKEN] Paso 1/3: descargar semilla | url=" & url)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

            Dim request As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
            request.Method = "GET"
            request.ContentType = "application/xml"
            request.Timeout = 60000
            request.ReadWriteTimeout = 60000
            request.KeepAlive = False
            request.ServicePoint.Expect100Continue = False

            Dim response As HttpWebResponse = CType(request.GetResponse(), HttpWebResponse)

            Using reader As New StreamReader(response.GetResponseStream())
                Dim responseContent As String = reader.ReadToEnd()

                Dim semillaXml As New XmlDocument()
                semillaXml.LoadXml(responseContent)

                If semillaXml IsNot Nothing Then
                    Dim xmlFilePath As String = ObtenerRutaArchivo("semilla_sin_firmar.xml")
                    semillaXml.Save(xmlFilePath)
                End If

                Helper.RegistrarLogCliente("[TOKEN] Semilla OK (Http=" & CInt(response.StatusCode) & ", bytes=" &
                                          If(responseContent, "").Length.ToString() & ")")
                Return semillaXml
            End Using

        Catch ex As WebException
            Dim http = TryCast(ex.Response, HttpWebResponse)
            Dim body = LeerCuerpoWebException(ex)
            Dim msg = "Error al descargar la semilla DGII. url=" & url &
                      " | " & DetalleExcepcion(ex)
            If body <> "" AndAlso msg.IndexOf(body, StringComparison.OrdinalIgnoreCase) < 0 Then
                msg &= " | Body=" & body.Replace(vbCr, " ").Replace(vbLf, " ")
            End If
            If http Is Nothing AndAlso ex.Status = WebExceptionStatus.ConnectFailure Then
                msg &= " | Causa probable: sin acceso a Internet/firewall/DNS hacia ecf.dgii.gov.do"
            ElseIf http Is Nothing AndAlso ex.Status = WebExceptionStatus.TrustFailure Then
                msg &= " | Causa probable: problema TLS/certificado SSL del servidor DGII"
            ElseIf http Is Nothing AndAlso (ex.Status = WebExceptionStatus.Timeout OrElse
                    (ex.Message IsNot Nothing AndAlso ex.Message.IndexOf("failed to respond", StringComparison.OrdinalIgnoreCase) >= 0)) Then
                msg &= " | El PC no recibe respuesta de ecf.dgii.gov.do:443. Abrir HTTPS hacia la DGII en el firewall/antivirus."
            End If
            Throw New Exception(msg, ex)
        Catch ex As Exception
            Throw New Exception("Error al descargar/parsear semilla DGII. url=" & url & " | " & DetalleExcepcion(ex), ex)
        End Try
    End Function

    Public Shared Sub ObtenerToken()
        SyncLock tokenLock
            UltimoErrorToken = ""
            Try
                If TokenVigente() Then
                    Helper.RegistrarLogCliente(
                        "[TOKEN] Reuso token vigente hasta " &
                        tokenExpiraUtc.ToLocalTime().ToString("HH:mm:ss"))
                    Return
                End If

                GlobalVariables.token = String.Empty
                tokenExpiraUtc = DateTime.MinValue

                Dim pathConfigurado = If(GlobalVariables.pathCertificado, "").Trim()
                Dim pathCert As String = ResolverRutaCertificado(pathConfigurado)
                Dim passCert As String = GlobalVariables.passCertificado
                Dim carpetaDll = RutaHelper.ObtenerRutaBaseDLL()
                Dim carpetaCerts = Path.Combine(carpetaDll, "certificados")
                Dim urlBase = If(GlobalVariables.url_base, "")

                Helper.RegistrarLogCliente(
                    "[TOKEN] Inicio ObtenerToken | url_base=" & urlBase &
                    " | pathCertificado(config)=" & pathConfigurado &
                    " | pathCert(resuelto)=" & pathCert &
                    " | passConfigurado=" & If(String.IsNullOrEmpty(passCert), "NO", "SI(len=" & passCert.Length.ToString() & ")") &
                    " | carpetaDLL=" & carpetaDll)

                If pathConfigurado = "" Then
                    RegistrarErrorToken(
                        "No se pudo obtener el token DGII: pathCertificado vacío en config.ini. " &
                        "Coloque el archivo .p12 en: " & carpetaCerts &
                        " y configure pathCertificado=certificados\nombre.p12 (junto a la DLL: " & carpetaDll & ").")
                    Return
                End If

                If Not File.Exists(pathCert) Then
                    RegistrarErrorToken(
                        "No se pudo obtener el token DGII: certificado no encontrado. " &
                        "Buscado en: " & pathCert & ". " &
                        "El .p12 debe estar en la carpeta certificados junto a FacturacionElectronicaDGII.dll (" &
                        carpetaCerts & ") y pathCertificado en config.ini debe apuntar ahí (ej. certificados\mi_cert.p12).")
                    Return
                End If

                Dim token As String = ""
                For intento As Integer = 1 To 3
                    token = ObtenerTokenDgii(urlBase, pathCert, passCert)
                    If token <> "" Then Exit For
                    If intento < 3 Then
                        Helper.RegistrarLogCliente("[TOKEN] Reintento " & (intento + 1).ToString() & "/3 tras fallo al descargar semilla")
                        Threading.Thread.Sleep(2000)
                    End If
                Next
                If token = "" Then
                    Dim detalle = If(String.IsNullOrWhiteSpace(UltimoErrorToken),
                        "sin detalle adicional (revise logs [TOKEN] anteriores)",
                        UltimoErrorToken)
                    RegistrarErrorToken(
                        "No se pudo obtener el token DGII con el certificado: " & pathCert & ". " &
                        "Detalle: " & detalle &
                        " | Verifique: 1) passCertificado, 2) url_base (TesteCF/CerteCF/ecf), 3) .p12 vigente de la DGII. " &
                        "Ubicación: " & carpetaCerts)
                    Return
                End If
                GlobalVariables.token = token
                If tokenExpiraUtc = DateTime.MinValue Then
                    tokenExpiraUtc = CalcularExpiracionUtc(token, Nothing)
                End If
                Helper.RegistrarLogCliente(
                    "[TOKEN] Token DGII obtenido OK (len=" & token.Length.ToString() &
                    ", vale hasta " & tokenExpiraUtc.ToLocalTime().ToString("HH:mm:ss") & ")")
            Catch ex As Exception
                RegistrarErrorToken("Error al obtener el token DGII: " & DetalleExcepcion(ex) &
                    " | Carpeta DLL: " & RutaHelper.ObtenerRutaBaseDLL() &
                    " | Cert configurado: " & If(GlobalVariables.pathCertificado, "") &
                    " | url_base: " & If(GlobalVariables.url_base, ""))
            End Try
        End SyncLock
    End Sub

    ''' <summary>Token JWT DGII sin depender de MessageBox (para consultas desde Delphi).</summary>
    Public Shared Function ObtenerTokenDgii(urlBase As String, pathCertificado As String, passCertificado As String) As String
        UltimoErrorToken = ""
        Dim paso As String = "inicio"
        Dim pathCert As String = ""
        Dim base As String = ""
        Try
            base = RutaHelper.NormalizarUrlBaseDgii(urlBase)
            If base = "" Then
                RegistrarErrorToken("ObtenerTokenDgii: url_base vacía o inválida en config.ini / parámetro.")
                Return ""
            End If

            pathCert = ResolverRutaCertificado(pathCertificado)
            If pathCert = "" OrElse Not File.Exists(pathCert) Then
                RegistrarErrorToken(
                    "ObtenerTokenDgii: certificado no encontrado en '" & pathCert & "'. " &
                    "Debe existir en " & Path.Combine(RutaHelper.ObtenerRutaBaseDLL(), "certificados") & ".")
                Return ""
            End If

            If String.IsNullOrEmpty(passCertificado) Then
                Helper.RegistrarLogCliente("[TOKEN] Aviso: passCertificado vacío; la carga del .p12 puede fallar.")
            End If

            paso = "descargar_semilla"
            Dim semillaXml As XmlDocument = DescargarSemilla(base)

            paso = "firmar_semilla"
            Helper.RegistrarLogCliente("[TOKEN] Paso 2/3: firmar semilla | cert=" & pathCert)
            Dim signedXml As XmlDocument = FirmarXML(semillaXml, pathCert, passCertificado)

            paso = "validar_semilla"
            Helper.RegistrarLogCliente("[TOKEN] Paso 3/3: validar semilla firmada en DGII")
            Dim jsonString As String = EnviarXMLFirmado(base, signedXml)

            paso = "parsear_token"
            Dim jsonObj As JObject = JObject.Parse(jsonString)
            Dim t = jsonObj("token")
            If t Is Nothing Then
                Dim preview = If(jsonString, "")
                If preview.Length > 500 Then preview = preview.Substring(0, 500) & "..."
                RegistrarErrorToken("ObtenerTokenDgii: la DGII no devolvió campo 'token'. Respuesta: " & preview)
                Return ""
            End If
            Dim tok = t.ToString()
            tokenExpiraUtc = CalcularExpiracionUtc(tok, jsonObj)
            Return tok
        Catch ex As Exception
            RegistrarErrorToken("ObtenerTokenDgii EX en paso=" & paso &
                " | cert=" & pathCert &
                " | url=" & base &
                " | " & DetalleExcepcion(ex))
            Return ""
        End Try
    End Function

    Private Shared Function ResolverRutaCertificado(pathCert As String) As String
        Dim p = If(pathCert, "").Trim()
        If p = "" Then Return ""
        If Path.IsPathRooted(p) Then Return p
        Return ObtenerRutaArchivo(p)
    End Function

    Private Shared Function FirmarXML(ByVal xmlDoc As XmlDocument, ByVal pathCert As String, ByVal passCert As String) As XmlDocument
        Dim rutaCompletaCert = ResolverRutaCertificado(pathCert)
        Try
            If Not File.Exists(rutaCompletaCert) Then
                Throw New Exception("El certificado para firma no existe: " & rutaCompletaCert)
            End If

            Dim cert As X509Certificate2 = Nothing
            Try
                cert = New X509Certificate2(rutaCompletaCert, passCert, X509KeyStorageFlags.Exportable)
            Catch ex As CryptographicException
                Throw New Exception(
                    "No se pudo abrir el certificado .p12 (clave incorrecta, archivo dañado o sin permiso). " &
                    "cert=" & rutaCompletaCert & " | " & DetalleExcepcion(ex), ex)
            Catch ex As Exception
                Throw New Exception(
                    "Error cargando certificado .p12: " & rutaCompletaCert & " | " & DetalleExcepcion(ex), ex)
            End Try

            Helper.RegistrarLogCliente(
                "[TOKEN] Certificado cargado | Subject=" & cert.Subject &
                " | NotBefore=" & cert.NotBefore.ToString("yyyy-MM-dd") &
                " | NotAfter=" & cert.NotAfter.ToString("yyyy-MM-dd") &
                " | HasPrivateKey=" & cert.HasPrivateKey.ToString())

            If DateTime.Now > cert.NotAfter Then
                Throw New Exception(
                    "El certificado está VENCIDO. NotAfter=" & cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss") &
                    " | Subject=" & cert.Subject)
            End If
            If DateTime.Now < cert.NotBefore Then
                Throw New Exception(
                    "El certificado aún no es válido (NotBefore=" & cert.NotBefore.ToString("yyyy-MM-dd HH:mm:ss") & ").")
            End If
            If Not cert.HasPrivateKey Then
                Throw New Exception("El certificado no tiene clave privada (HasPrivateKey=False). Use el .p12 completo, no solo el .cer.")
            End If

            Dim exportedKeyMaterial = cert.PrivateKey.ToXmlString(True)
            Dim key = New RSACryptoServiceProvider(New CspParameters(24))
            key.PersistKeyInCsp = False
            key.FromXmlString(exportedKeyMaterial)

            Dim signedXml As New SignedXml(xmlDoc)
            signedXml.SigningKey = key
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256"

            Dim reference As New Reference()
            reference.Uri = ""
            reference.AddTransform(New XmlDsigEnvelopedSignatureTransform())
            reference.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256"
            signedXml.AddReference(reference)

            Dim keyInfo As New KeyInfo()
            keyInfo.AddClause(New KeyInfoX509Data(cert))
            signedXml.KeyInfo = keyInfo

            signedXml.ComputeSignature()
            Dim xmlFirmaDigital As XmlElement = signedXml.GetXml()
            xmlDoc.DocumentElement.AppendChild(xmlDoc.ImportNode(xmlFirmaDigital, True))
            Helper.RegistrarLogCliente("[TOKEN] Semilla firmada OK")
            Return xmlDoc
        Catch ex As Exception
            Throw New Exception("Error firmando semilla | cert=" & rutaCompletaCert & " | " & DetalleExcepcion(ex), ex)
        End Try
    End Function

    Private Shared Function EnviarXMLFirmado(urlBase As String, ByVal signedXml As XmlDocument) As String
        Dim url As String = RutaHelper.NormalizarUrlBaseDgii(urlBase) & "autenticacion/api/autenticacion/validarsemilla"
        Try
            Dim xmlFilePath As String = ObtenerRutaArchivo("semilla_firmada.xml")
            signedXml.Save(xmlFilePath)

            Helper.RegistrarLogCliente("[TOKEN] POST validarsemilla | url=" & url & " | xml=" & xmlFilePath)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

            Dim boundary As String = "----WebKitFormBoundary" & DateTime.Now.Ticks.ToString("x")
            Dim request As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
            request.Method = "POST"
            request.ContentType = "multipart/form-data; boundary=" & boundary
            request.Accept = "application/json"
            request.Timeout = 60000
            request.ReadWriteTimeout = 60000

            Using stream As Stream = request.GetRequestStream()
                Dim header As String = "--" & boundary & vbCrLf &
                                   "Content-Disposition: form-data; name=""xml""; filename=""semilla_firmada.xml""" & vbCrLf &
                                   "Content-Type: text/xml" & vbCrLf & vbCrLf
                stream.Write(Encoding.UTF8.GetBytes(header), 0, header.Length)

                Using fileStream As New FileStream(xmlFilePath, FileMode.Open, FileAccess.Read)
                    fileStream.CopyTo(stream)
                End Using

                Dim footer As String = vbCrLf & "--" & boundary & "--" & vbCrLf
                stream.Write(Encoding.UTF8.GetBytes(footer), 0, footer.Length)
            End Using

            Using response As HttpWebResponse = CType(request.GetResponse(), HttpWebResponse)
                Using reader As New StreamReader(response.GetResponseStream())
                    Dim body = reader.ReadToEnd()
                    Helper.RegistrarLogCliente("[TOKEN] validarsemilla OK | Http=" & CInt(response.StatusCode) &
                                              " | bytes=" & If(body, "").Length.ToString())
                    Return body
                End Using
            End Using

        Catch ex As WebException
            Dim body = LeerCuerpoWebException(ex)
            Dim msg = "Error validando semilla en DGII. url=" & url & " | " & DetalleExcepcion(ex)
            If body <> "" AndAlso msg.IndexOf(body, StringComparison.OrdinalIgnoreCase) < 0 Then
                msg &= " | Body=" & body.Replace(vbCr, " ").Replace(vbLf, " ")
            End If
            Throw New Exception(msg, ex)
        Catch ex As Exception
            Throw New Exception("Error en validarsemilla. url=" & url & " | " & DetalleExcepcion(ex), ex)
        End Try
    End Function

End Class
