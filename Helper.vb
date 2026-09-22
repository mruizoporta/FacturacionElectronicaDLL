Imports System.Collections.Generic
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml
Imports System.Xml.Schema
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class Helper
    Public Shared Sub EjecutarProcedimientoAlmacenado(cadenaConexion As String, nombreProcedimiento As String, parametros As SqlParameter())
        Using conn As New SqlConnection(cadenaConexion)
            Using cmd As New SqlCommand(nombreProcedimiento, conn)
                cmd.CommandType = CommandType.StoredProcedure
                If parametros IsNot Nothing Then
                    cmd.Parameters.AddRange(parametros)
                End If
                conn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Public Const TipoEncfFactura As String = "FACTURA"
    Public Const TipoEncfCompra As String = "COMPRA"
    Public Const TipoEncfDesembolso As String = "DESEMBOLSO"
    Public Const TipoEncfDevolucion As String = "DEVOLUCION"
    Public Const TipoEncfNotaCredito As String = "NOTACREDITO"
    Public Const TipoEncfNotaDebito As String = "NOTADEBITO"
    Public Const TipoEncfPos As String = "POS"
    Public Const TipoEncfResBar As String = "RESBAR"

    ''' <summary>Valores de columna tipo en dbo.BitacoraFacturacionElectronicaDGII (tabla de origen).</summary>
    Public Const BitacoraTipoFacturas As String = "FACTURAS"
    Public Const BitacoraTipoProvFacturas As String = "PROV_FACTURAS"
    Public Const BitacoraTipoDesembolsos As String = "DESEMBOLSOS"
    Public Const BitacoraTipoDevolucion As String = "DEVOLUCION"
    Public Const BitacoraTipoNotaCredito As String = "NOTA_CREDITO"
    Public Const BitacoraTipoNotasDebito As String = "NOTAS_DEBITO"
    Public Const BitacoraTipoMontosTicket As String = "MONTOS_TICKET"
    Public Const BitacoraTipoRestBar As String = "RESTBAR"
    Public Const XsdE47FileName As String = "e-CF 47 v.1.0.xsd"

    ''' <summary>Estado DGII/Luganis antes de asignar otro eNCF.</summary>
    Public Class DocumentoEncfEstado
        Public Property ENCF As String
        Public Property ErrorDGII As Boolean?
        Public Property AceptadoDGII As Boolean?
        Public Property ErrorLuganis As Boolean?
        Public Property AceptadoLuganis As Boolean?
        Public Property EnviadoDGII As Boolean?
        Public Property SecuenciaUtilizadaDGI As Boolean?
        Public Property TrackIdLuganis As String
    End Class

    ''' <summary>Alias de compatibilidad.</summary>
    Public Class FacturaEncfEstado
        Inherits DocumentoEncfEstado
    End Class

    ''' <summary>Identificador de documento para bitácora eNCF.</summary>
    Public Class EncfDocumentoRef
        Public Property TipoDocumento As String
        Public Property EmpCodigo As Integer
        Public Property SucCodigo As Integer
        Public Property FacForma As String
        Public Property TfaCodigo As Integer?
        Public Property DocNumero As Integer?
        Public Property DocNumeroStr As String
        Public Property SupCodigo As Integer?
        Public Property UsuCodigo As Integer?
        Public Property CajaCodigo As Integer?

        Public ReadOnly Property DocClave As String
            Get
                Select Case TipoDocumento.ToUpperInvariant()
                    Case TipoEncfFactura
                        Return $"FACTURA|{EmpCodigo}|{SucCodigo}|{NormalizarForma(FacForma)}|{If(TfaCodigo, 0)}|{If(DocNumero, 0)}"
                    Case TipoEncfCompra
                        Dim num = If(Not String.IsNullOrWhiteSpace(DocNumeroStr), DocNumeroStr.Trim(), If(DocNumero.HasValue, DocNumero.Value.ToString(), "0"))
                        Return $"COMPRA|{EmpCodigo}|{SucCodigo}|{If(SupCodigo, 0)}|{num}"
                    Case TipoEncfDesembolso
                        Return $"DESEMBOLSO|{EmpCodigo}|{SucCodigo}|{If(DocNumero, 0)}"
                    Case TipoEncfDevolucion
                        Return $"DEVOLUCION|{EmpCodigo}|{SucCodigo}|{If(DocNumero, 0)}"
                    Case TipoEncfNotaCredito
                        Return $"NOTACREDITO|{EmpCodigo}|{SucCodigo}|{If(DocNumero, 0)}"
                    Case TipoEncfNotaDebito
                        Return $"NOTADEBITO|{EmpCodigo}|{SucCodigo}|{If(DocNumero, 0)}"
                    Case TipoEncfPos
                        Return $"POS|{EmpCodigo}|{SucCodigo}|{If(UsuCodigo, 0)}|{If(CajaCodigo, 0)}|{If(DocNumero, 0)}"
                    Case TipoEncfResBar
                        Return $"RESBAR|{EmpCodigo}|{SucCodigo}|{If(UsuCodigo, 0)}|{If(CajaCodigo, 0)}|{If(DocNumero, 0)}"
                    Case Else
                        Return $"{TipoDocumento}|{EmpCodigo}|{SucCodigo}"
                End Select
            End Get
        End Property

        Public Shared Function Factura(emp As Integer, suc As Integer, forma As String, tfa As Integer, numero As Integer) As EncfDocumentoRef
            Return New EncfDocumentoRef With {
                .TipoDocumento = TipoEncfFactura,
                .EmpCodigo = emp,
                .SucCodigo = suc,
                .FacForma = forma,
                .TfaCodigo = tfa,
                .DocNumero = numero
            }
        End Function

        Public Shared Function Compra(emp As Integer, suc As Integer, sup As Integer, numero As String) As EncfDocumentoRef
            Dim n As Integer
            Dim ref As New EncfDocumentoRef With {
                .TipoDocumento = TipoEncfCompra,
                .EmpCodigo = emp,
                .SucCodigo = suc,
                .SupCodigo = sup,
                .DocNumeroStr = If(numero, "").Trim()
            }
            If Integer.TryParse(ref.DocNumeroStr, n) Then ref.DocNumero = n
            Return ref
        End Function

        Public Shared Function Desembolso(emp As Integer, suc As Integer, numero As Integer) As EncfDocumentoRef
            Return New EncfDocumentoRef With {.TipoDocumento = TipoEncfDesembolso, .EmpCodigo = emp, .SucCodigo = suc, .DocNumero = numero}
        End Function

        Public Shared Function Devolucion(emp As Integer, suc As Integer, numero As Integer) As EncfDocumentoRef
            Return New EncfDocumentoRef With {.TipoDocumento = TipoEncfDevolucion, .EmpCodigo = emp, .SucCodigo = suc, .DocNumero = numero}
        End Function

        Public Shared Function NotaCredito(emp As Integer, suc As Integer, numero As Integer) As EncfDocumentoRef
            Return New EncfDocumentoRef With {.TipoDocumento = TipoEncfNotaCredito, .EmpCodigo = emp, .SucCodigo = suc, .DocNumero = numero}
        End Function

        Public Shared Function NotaDebito(emp As Integer, suc As Integer, numero As Integer) As EncfDocumentoRef
            Return New EncfDocumentoRef With {.TipoDocumento = TipoEncfNotaDebito, .EmpCodigo = emp, .SucCodigo = suc, .DocNumero = numero}
        End Function

        Public Shared Function PosTicket(emp As Integer, suc As Integer, usu As Integer, caja As Integer, ticket As Integer) As EncfDocumentoRef
            Return New EncfDocumentoRef With {
                .TipoDocumento = TipoEncfPos,
                .EmpCodigo = emp,
                .SucCodigo = suc,
                .UsuCodigo = usu,
                .CajaCodigo = caja,
                .DocNumero = ticket
            }
        End Function

        Public Shared Function ResBar(emp As Integer, suc As Integer, usu As Integer, caja As Integer, facturaId As Integer) As EncfDocumentoRef
            Return New EncfDocumentoRef With {
                .TipoDocumento = TipoEncfResBar,
                .EmpCodigo = emp,
                .SucCodigo = suc,
                .UsuCodigo = usu,
                .CajaCodigo = caja,
                .DocNumero = facturaId
            }
        End Function
    End Class

    ''' <summary>tipo en BitacoraFacturacionElectronicaDGII según TipoeCF DGII.</summary>
    Public Shared Function ResolverTipoBitacoraDgii(tipoeCF As String, Optional isNotaCredito As Boolean = False) As String
        Select Case If(tipoeCF, "").Trim()
            Case "41", "47"
                Return BitacoraTipoProvFacturas
            Case "34"
                Return If(isNotaCredito, BitacoraTipoNotaCredito, BitacoraTipoDevolucion)
            Case "43"
                Return BitacoraTipoDesembolsos
            Case "33"
                Return BitacoraTipoNotasDebito
            Case Else
                Return BitacoraTipoFacturas
        End Select
    End Function

    Public Shared Function ResolverTipoBitacoraTicket(esRestBar As Boolean) As String
        Return If(esRestBar, BitacoraTipoRestBar, BitacoraTipoMontosTicket)
    End Function

    Private Shared Function NormalizarForma(forma As String) As String
        Dim f = If(forma, "").Trim()
        If f.Length = 0 Then Return "A"
        If f.Length > 1 Then Return f.Substring(0, 1)
        Return f
    End Function

    Public Shared Function ObtenerEstadoEncfFactura(
        cadenaConexion As String,
        empCodigo As Integer,
        facForma As String,
        tfaCodigo As Integer,
        facNumero As Integer,
        sucCodigo As Integer
    ) As DocumentoEncfEstado
        Return ObtenerEstadoEncf(cadenaConexion, EncfDocumentoRef.Factura(empCodigo, sucCodigo, facForma, tfaCodigo, facNumero))
    End Function

    Public Shared Function ObtenerEstadoEncf(cadenaConexion As String, doc As EncfDocumentoRef) As DocumentoEncfEstado
        Dim resultado As New DocumentoEncfEstado()
        If doc Is Nothing OrElse String.IsNullOrWhiteSpace(cadenaConexion) Then Return resultado

        Dim sql As String = Nothing
        Select Case doc.TipoDocumento.ToUpperInvariant()
            Case TipoEncfFactura
                sql =
"SELECT TOP (1) eNCF, Error_DGII, AceptadoDGII, Error_Luganis, AceptadoLuganis, Enviado_DGII, secuenciaUtilizadaDGI, trackIdLuganis
FROM dbo.Facturas
WHERE emp_codigo=@emp AND suc_codigo=@suc AND fac_forma=@forma AND tfa_codigo=@tfa AND fac_numero=@num;"
            Case TipoEncfCompra
                sql =
"SELECT TOP (1) eNCF, Error_DGII, AceptadoDGII, Error_Luganis, AceptadoLuganis, Enviado_DGII, secuenciaUtilizadaDGI, trackIdLuganis
FROM dbo.ProvFacturas
WHERE emp_codigo=@emp AND suc_codigo=@suc AND sup_codigo=@sup AND fac_numero=@numStr;"
            Case TipoEncfDesembolso
                sql =
"SELECT TOP (1) eNCF, Error_DGII, AceptadoDGII, Error_Luganis, AceptadoLuganis, Enviado_DGII, secuenciaUtilizadaDGI, trackIdLuganis
FROM dbo.Desembolsos
WHERE emp_codigo=@emp AND suc_codigo=@suc AND des_numero=@num;"
            Case TipoEncfDevolucion
                sql =
"SELECT TOP (1) eNCF, Error_DGII, AceptadoDGII, Error_Luganis, AceptadoLuganis, Enviado_DGII, secuenciaUtilizadaDGI, trackIdLuganis
FROM dbo.Devolucion
WHERE emp_codigo=@emp AND suc_codigo=@suc AND dev_numero=@num;"
            Case TipoEncfNotaCredito
                sql =
"SELECT TOP (1) eNCF, Error_DGII, AceptadoDGII, Error_Luganis, AceptadoLuganis, Enviado_DGII, secuenciaUtilizadaDGI, trackIdLuganis
FROM dbo.NotasCredito
WHERE emp_codigo=@emp AND suc_codigo=@suc AND ncr_numero=@num;"
            Case TipoEncfNotaDebito
                sql =
"SELECT TOP (1) eNCF, Error_DGII, AceptadoDGII, Error_Luganis, AceptadoLuganis, Enviado_DGII, secuenciaUtilizadaDGI, trackIdLuganis
FROM dbo.NotasDebito
WHERE emp_codigo=@emp AND suc_codigo=@suc AND nde_numero=@num;"
            Case TipoEncfPos
                sql =
"SELECT TOP (1) eNCF, Error_DGII, AceptadoDGII, Error_Luganis, AceptadoLuganis, Enviado_DGII, secuenciaUtilizadaDGI, trackIdLuganis
FROM dbo.Montos_Ticket
WHERE emp_codigo=@emp AND suc_codigo=@suc AND usu_codigo=@usu AND caja=@caja AND ticket=@num;"
            Case TipoEncfResBar
                sql =
"SELECT TOP (1) eNCF, Error_DGII, AceptadoDGII, Error_Luganis, AceptadoLuganis, Enviado_DGII, secuenciaUtilizadaDGI, trackIdLuganis
FROM dbo.Factura_RestBar
WHERE EMP_CODIGO=@emp AND SUC_CODIGO=@suc AND CajeroID=@usu AND CajaID=@caja AND FacturaID=@num;"
            Case Else
                Return resultado
        End Select

        Try
            Using conn As New SqlConnection(cadenaConexion)
                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.Add("@emp", SqlDbType.Int).Value = doc.EmpCodigo
                    cmd.Parameters.Add("@suc", SqlDbType.Int).Value = doc.SucCodigo
                    If doc.TipoDocumento.Equals(TipoEncfFactura, StringComparison.OrdinalIgnoreCase) Then
                        cmd.Parameters.Add("@forma", SqlDbType.Char, 1).Value = NormalizarForma(doc.FacForma)
                        cmd.Parameters.Add("@tfa", SqlDbType.Int).Value = If(doc.TfaCodigo, 0)
                    End If
                    If doc.TipoDocumento.Equals(TipoEncfCompra, StringComparison.OrdinalIgnoreCase) Then
                        cmd.Parameters.Add("@sup", SqlDbType.Int).Value = If(doc.SupCodigo, 0)
                        cmd.Parameters.Add("@numStr", SqlDbType.VarChar, 30).Value =
                            If(String.IsNullOrWhiteSpace(doc.DocNumeroStr), If(doc.DocNumero.HasValue, doc.DocNumero.Value.ToString(), "0"), doc.DocNumeroStr)
                    End If
                    If doc.TipoDocumento.Equals(TipoEncfPos, StringComparison.OrdinalIgnoreCase) OrElse
                       doc.TipoDocumento.Equals(TipoEncfResBar, StringComparison.OrdinalIgnoreCase) Then
                        cmd.Parameters.Add("@usu", SqlDbType.Int).Value = If(doc.UsuCodigo, 0)
                        cmd.Parameters.Add("@caja", SqlDbType.Int).Value = If(doc.CajaCodigo, 0)
                    End If
                    If Not doc.TipoDocumento.Equals(TipoEncfCompra, StringComparison.OrdinalIgnoreCase) Then
                        cmd.Parameters.Add("@num", SqlDbType.Int).Value = If(doc.DocNumero, 0)
                    End If
                    conn.Open()
                    Using r = cmd.ExecuteReader()
                        If Not r.Read() Then Return resultado
                        CargarEstadoDesdeReader(r, resultado)
                    End Using
                End Using
            End Using
        Catch ex As Exception
            RegistrarLogCliente($"ObtenerEstadoEncf ({doc.TipoDocumento}): " & ex.Message)
        End Try
        Return resultado
    End Function

    Private Shared Sub CargarEstadoDesdeReader(r As IDataRecord, resultado As DocumentoEncfEstado)
        resultado.ENCF = SafeStr(r, "eNCF")
        resultado.ErrorDGII = ReadNullableBool(r, "Error_DGII")
        resultado.AceptadoDGII = ReadNullableBool(r, "AceptadoDGII")
        resultado.ErrorLuganis = ReadNullableBool(r, "Error_Luganis")
        resultado.AceptadoLuganis = ReadNullableBool(r, "AceptadoLuganis")
        resultado.EnviadoDGII = ReadNullableBool(r, "Enviado_DGII")
        resultado.SecuenciaUtilizadaDGI = ReadNullableBool(r, "secuenciaUtilizadaDGI")
        resultado.TrackIdLuganis = SafeStr(r, "trackIdLuganis")
    End Sub

    Private Shared Function ReadNullableBool(r As IDataRecord, col As String) As Boolean?
        Try
            Dim i = r.GetOrdinal(col)
            If r.IsDBNull(i) Then Return Nothing
            Return Convert.ToBoolean(r.GetValue(i))
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function NullableBoolToDb(v As Boolean?) As Object
        If Not v.HasValue Then Return DBNull.Value
        Return If(v.Value, CObj(1), CObj(0))
    End Function

    ''' <summary>Inserta en dbo.Documentos_eNCF_Bitacora si el eNCF_nuevo no existe para ese documento.</summary>
    Public Shared Function InsertarBitacoraEncf(
        cadenaConexion As String,
        doc As EncfDocumentoRef,
        encfNuevo As String,
        Optional encfAnterior As String = Nothing,
        Optional origen As String = "DLL",
        Optional motivoRechazo As String = Nothing,
        Optional estadoAnterior As DocumentoEncfEstado = Nothing
    ) As Boolean
        If doc Is Nothing Then Return False
        Dim encfNue = If(encfNuevo, "").Trim()
        If String.IsNullOrWhiteSpace(cadenaConexion) OrElse encfNue = "" Then Return False

        Dim encfAnt = If(encfAnterior, "").Trim()
        If encfAnt = "" AndAlso estadoAnterior IsNot Nothing Then
            encfAnt = If(estadoAnterior.ENCF, "").Trim()
        End If

        Dim tipoEvento = If(encfAnt = "", "ASIGNACION", "REASIGNACION")
        Dim rechazado = EvaluarRechazoEncfAnterior(estadoAnterior, encfAnt)
        If String.Equals(If(origen, "").Trim(), "LUGANIS", StringComparison.OrdinalIgnoreCase) Then
            ' Distinto de ASIGNACION (DGII_ASIGNACION) para que sí inserte fila Luganis
            tipoEvento = If(rechazado OrElse Not String.IsNullOrWhiteSpace(motivoRechazo), "RECHAZO", "ENVIO")
        End If

        If rechazado AndAlso String.IsNullOrWhiteSpace(motivoRechazo) Then
            motivoRechazo = ObtenerUltimoMensajeErrorDgii(cadenaConexion, doc)
        End If

        Const sqlInsert As String =
"INSERT INTO dbo.Documentos_eNCF_Bitacora
(
    tipo_documento, doc_clave, emp_codigo, suc_codigo,
    fac_forma, tfa_codigo, doc_numero, doc_numero_str, sup_codigo, usu_codigo, caja_codigo,
    secuencia_encf, eNCF_anterior, eNCF_nuevo, tipo_evento, origen,
    Error_DGII_anterior, AceptadoDGII_anterior, Error_Luganis_anterior, AceptadoLuganis_anterior,
    Enviado_DGII_anterior, secuenciaUtilizada_anterior, trackIdLuganis_anterior,
    rechazado, motivo_rechazo, usuario_bd, aplicacion, host_cliente
)
SELECT
    @tipo, @clave, @emp, @suc,
    @forma, @tfa, @doc_num, @doc_str, @sup, @usu, @caja,
    ISNULL((SELECT MAX(b.secuencia_encf) FROM dbo.Documentos_eNCF_Bitacora b
            WHERE b.tipo_documento = @tipo AND b.doc_clave = @clave), 0) + 1,
    NULLIF(@encf_ant, ''), @encf_nue, @tipo_evento, @origen,
    @err_dgii, @acep_dgii, @err_lug, @acep_lug, @env_dgii, @seq_util, @track_lug,
    @rechazado, @motivo, SUSER_SNAME(), APP_NAME(), HOST_NAME()
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Documentos_eNCF_Bitacora x
    WHERE x.tipo_documento = @tipo AND x.doc_clave = @clave AND x.eNCF_nuevo = @encf_nue
);"

        Const sqlUpdateLuganis As String =
"UPDATE dbo.Documentos_eNCF_Bitacora
SET tipo_evento = @tipo_evento,
    origen = @origen,
    rechazado = @rechazado,
    motivo_rechazo = COALESCE(@motivo, motivo_rechazo),
    Error_Luganis_anterior = COALESCE(@err_lug, Error_Luganis_anterior),
    AceptadoLuganis_anterior = COALESCE(@acep_lug, AceptadoLuganis_anterior)
WHERE tipo_documento = @tipo AND doc_clave = @clave AND eNCF_nuevo = @encf_nue;"

        Try
            Using conn As New SqlConnection(cadenaConexion)
                conn.Open()
                Using cmd As New SqlCommand(sqlInsert, conn)
                    cmd.Parameters.Add("@tipo", SqlDbType.VarChar, 30).Value = doc.TipoDocumento.ToUpperInvariant()
                    cmd.Parameters.Add("@clave", SqlDbType.VarChar, 120).Value = doc.DocClave
                    cmd.Parameters.Add("@emp", SqlDbType.Int).Value = doc.EmpCodigo
                    cmd.Parameters.Add("@suc", SqlDbType.Int).Value = doc.SucCodigo
                    cmd.Parameters.Add("@forma", SqlDbType.Char, 1).Value =
                        If(doc.TipoDocumento.Equals(TipoEncfFactura, StringComparison.OrdinalIgnoreCase),
                           CObj(NormalizarForma(doc.FacForma)), DBNull.Value)
                    cmd.Parameters.Add("@tfa", SqlDbType.Int).Value = If(doc.TfaCodigo.HasValue, CObj(doc.TfaCodigo.Value), DBNull.Value)
                    cmd.Parameters.Add("@doc_num", SqlDbType.Int).Value = If(doc.DocNumero.HasValue, CObj(doc.DocNumero.Value), DBNull.Value)
                    cmd.Parameters.Add("@doc_str", SqlDbType.VarChar, 30).Value =
                        If(String.IsNullOrWhiteSpace(doc.DocNumeroStr), DBNull.Value, CObj(doc.DocNumeroStr.Trim()))
                    cmd.Parameters.Add("@sup", SqlDbType.Int).Value = If(doc.SupCodigo.HasValue, CObj(doc.SupCodigo.Value), DBNull.Value)
                    cmd.Parameters.Add("@usu", SqlDbType.Int).Value = If(doc.UsuCodigo.HasValue, CObj(doc.UsuCodigo.Value), DBNull.Value)
                    cmd.Parameters.Add("@caja", SqlDbType.Int).Value = If(doc.CajaCodigo.HasValue, CObj(doc.CajaCodigo.Value), DBNull.Value)
                    cmd.Parameters.Add("@encf_ant", SqlDbType.VarChar, 100).Value = If(encfAnt = "", CObj(DBNull.Value), CObj(encfAnt))
                    cmd.Parameters.Add("@encf_nue", SqlDbType.VarChar, 100).Value = encfNue
                    cmd.Parameters.Add("@tipo_evento", SqlDbType.VarChar, 20).Value = tipoEvento
                    cmd.Parameters.Add("@origen", SqlDbType.VarChar, 30).Value = If(String.IsNullOrWhiteSpace(origen), CObj(DBNull.Value), CObj(origen.Trim()))
                    cmd.Parameters.Add("@err_dgii", SqlDbType.Bit).Value = NullableBoolToDb(If(estadoAnterior Is Nothing, Nothing, estadoAnterior.ErrorDGII))
                    cmd.Parameters.Add("@acep_dgii", SqlDbType.Bit).Value = NullableBoolToDb(If(estadoAnterior Is Nothing, Nothing, estadoAnterior.AceptadoDGII))
                    cmd.Parameters.Add("@err_lug", SqlDbType.Bit).Value = NullableBoolToDb(If(estadoAnterior Is Nothing, Nothing, estadoAnterior.ErrorLuganis))
                    cmd.Parameters.Add("@acep_lug", SqlDbType.Bit).Value = NullableBoolToDb(If(estadoAnterior Is Nothing, Nothing, estadoAnterior.AceptadoLuganis))
                    cmd.Parameters.Add("@env_dgii", SqlDbType.Bit).Value = NullableBoolToDb(If(estadoAnterior Is Nothing, Nothing, estadoAnterior.EnviadoDGII))
                    cmd.Parameters.Add("@seq_util", SqlDbType.Bit).Value = NullableBoolToDb(If(estadoAnterior Is Nothing, Nothing, estadoAnterior.SecuenciaUtilizadaDGI))
                    cmd.Parameters.Add("@track_lug", SqlDbType.VarChar, 200).Value =
                        If(String.IsNullOrWhiteSpace(estadoAnterior?.TrackIdLuganis), CObj(DBNull.Value), CObj(estadoAnterior.TrackIdLuganis))
                    cmd.Parameters.Add("@rechazado", SqlDbType.Bit).Value = If(rechazado, 1, 0)
                    cmd.Parameters.Add("@motivo", SqlDbType.NVarChar, -1).Value =
                        If(String.IsNullOrWhiteSpace(motivoRechazo), CObj(DBNull.Value), CObj(motivoRechazo.Trim()))
                    Dim filas As Integer = 0
                    Try
                        filas = cmd.ExecuteNonQuery()
                    Catch exDup As Exception
                        ' UX_Documentos_eNCF_Bitacora_Clave_ENCF: si el INSERT choca, caer a UPDATE Luganis
                        If String.Equals(If(origen, "").Trim(), "LUGANIS", StringComparison.OrdinalIgnoreCase) AndAlso
                           (exDup.Message.IndexOf("UX_Documentos_eNCF_Bitacora", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                            exDup.Message.IndexOf("duplicate key", StringComparison.OrdinalIgnoreCase) >= 0) Then
                            filas = 0
                            RegistrarLogCliente($"InsertarBitacoraEncf: duplicado eNCF, se actualizará fila existente. {exDup.Message}")
                        Else
                            Throw
                        End If
                    End Try
                    If filas > 0 Then
                        RegistrarLogCliente($"Bitácora eNCF [{doc.TipoDocumento}]: {tipoEvento} {encfNue} clave={doc.DocClave} origen={origen}")
                        Return True
                    End If

                    ' Índice único UX_..._Clave_ENCF: si ya existe el eNCF (p.ej. DGII_ASIGNACION),
                    ' actualizar la fila con el resultado LUGANIS (motivo / rechazo).
                    If String.Equals(If(origen, "").Trim(), "LUGANIS", StringComparison.OrdinalIgnoreCase) Then
                        Using cmdUpd As New SqlCommand(sqlUpdateLuganis, conn)
                            cmdUpd.Parameters.Add("@tipo", SqlDbType.VarChar, 30).Value = doc.TipoDocumento.ToUpperInvariant()
                            cmdUpd.Parameters.Add("@clave", SqlDbType.VarChar, 120).Value = doc.DocClave
                            cmdUpd.Parameters.Add("@encf_nue", SqlDbType.VarChar, 100).Value = encfNue
                            cmdUpd.Parameters.Add("@tipo_evento", SqlDbType.VarChar, 20).Value = tipoEvento
                            cmdUpd.Parameters.Add("@origen", SqlDbType.VarChar, 30).Value = "LUGANIS"
                            cmdUpd.Parameters.Add("@rechazado", SqlDbType.Bit).Value = If(rechazado OrElse Not String.IsNullOrWhiteSpace(motivoRechazo), 1, 0)
                            cmdUpd.Parameters.Add("@motivo", SqlDbType.NVarChar, -1).Value =
                                If(String.IsNullOrWhiteSpace(motivoRechazo), CObj(DBNull.Value), CObj(motivoRechazo.Trim()))
                            cmdUpd.Parameters.Add("@err_lug", SqlDbType.Bit).Value = NullableBoolToDb(If(estadoAnterior Is Nothing, Nothing, estadoAnterior.ErrorLuganis))
                            cmdUpd.Parameters.Add("@acep_lug", SqlDbType.Bit).Value = NullableBoolToDb(If(estadoAnterior Is Nothing, Nothing, estadoAnterior.AceptadoLuganis))
                            Dim filasUpd = cmdUpd.ExecuteNonQuery()
                            If filasUpd > 0 Then
                                RegistrarLogCliente($"Bitácora eNCF [{doc.TipoDocumento}]: UPDATE {tipoEvento} {encfNue} clave={doc.DocClave} origen=LUGANIS motivo={If(motivoRechazo, "")}")
                                Return True
                            End If
                        End Using
                    End If
                    Return False
                End Using
            End Using
        Catch ex As Exception
            RegistrarLogCliente($"InsertarBitacoraEncf ({doc.TipoDocumento}): " & ex.Message)
            Return False
        End Try
    End Function

    Public Shared Function InsertarBitacoraEncfFactura(
        cadenaConexion As String,
        empCodigo As Integer,
        facForma As String,
        tfaCodigo As Integer,
        facNumero As Integer,
        sucCodigo As Integer,
        encfNuevo As String,
        Optional encfAnterior As String = Nothing,
        Optional origen As String = "DLL",
        Optional motivoRechazo As String = Nothing,
        Optional estadoAnterior As DocumentoEncfEstado = Nothing
    ) As Boolean
        Dim doc = EncfDocumentoRef.Factura(empCodigo, sucCodigo, facForma, tfaCodigo, facNumero)
        Return InsertarBitacoraEncf(cadenaConexion, doc, encfNuevo, encfAnterior, origen, motivoRechazo, estadoAnterior)
    End Function

    Private Shared Function EvaluarRechazoEncfAnterior(estado As DocumentoEncfEstado, encfAnt As String) As Boolean
        If estado Is Nothing Then Return False
        If encfAnt = "" AndAlso String.IsNullOrWhiteSpace(estado.ENCF) Then Return False
        Return (estado.ErrorDGII.HasValue AndAlso estado.ErrorDGII.Value) OrElse
               (estado.ErrorLuganis.HasValue AndAlso estado.ErrorLuganis.Value) OrElse
               (estado.AceptadoDGII.HasValue AndAlso Not estado.AceptadoDGII.Value) OrElse
               (estado.AceptadoLuganis.HasValue AndAlso Not estado.AceptadoLuganis.Value)
    End Function

    Public Shared Function ObtenerUltimoMensajeErrorDgii(cadenaConexion As String, doc As EncfDocumentoRef) As String
        If doc Is Nothing Then Return Nothing
        Dim sql As String
        Dim tipoBitacora = ResolverTipoBitacoraTicket(doc.TipoDocumento.Equals(TipoEncfResBar, StringComparison.OrdinalIgnoreCase))
        If doc.TipoDocumento.Equals(TipoEncfPos, StringComparison.OrdinalIgnoreCase) OrElse
           doc.TipoDocumento.Equals(TipoEncfResBar, StringComparison.OrdinalIgnoreCase) Then
            sql =
"SELECT TOP (1) b.MensajeError FROM dbo.BitacoraFacturacionElectronicaDGII b
WHERE b.emp_codigo = @emp AND b.fac_numero = @fac AND b.usu_codigo = @usu AND b.caja = @caja
  AND (b.tipo = @tipo_bit OR b.tipo IS NULL)
  AND b.MensajeError IS NOT NULL AND LTRIM(RTRIM(b.MensajeError)) <> ''
ORDER BY b.Fecha_Envio DESC;"
        Else
            sql =
"SELECT TOP (1) b.MensajeError FROM dbo.BitacoraFacturacionElectronicaDGII b
WHERE b.emp_codigo = @emp
  AND LTRIM(RTRIM(CONVERT(VARCHAR(30), b.fac_numero))) = LTRIM(RTRIM(@fac))
  AND b.MensajeError IS NOT NULL AND LTRIM(RTRIM(b.MensajeError)) <> ''
ORDER BY b.Fecha_Envio DESC;"
        End If
        Try
            Using conn As New SqlConnection(cadenaConexion)
                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.Add("@emp", SqlDbType.Int).Value = doc.EmpCodigo
                    Dim numTxt = If(Not String.IsNullOrWhiteSpace(doc.DocNumeroStr), doc.DocNumeroStr,
                                    If(doc.DocNumero.HasValue, doc.DocNumero.Value.ToString(), "0"))
                    cmd.Parameters.Add("@fac", SqlDbType.VarChar, 30).Value = numTxt
                    If doc.TipoDocumento.Equals(TipoEncfPos, StringComparison.OrdinalIgnoreCase) OrElse
                       doc.TipoDocumento.Equals(TipoEncfResBar, StringComparison.OrdinalIgnoreCase) Then
                        cmd.Parameters.Add("@tipo_bit", SqlDbType.VarChar, 30).Value = tipoBitacora
                        cmd.Parameters.Add("@usu", SqlDbType.VarChar, 50).Value = If(doc.UsuCodigo, 0).ToString()
                        cmd.Parameters.Add("@caja", SqlDbType.VarChar, 50).Value = If(doc.CajaCodigo, 0).ToString()
                    End If
                    conn.Open()
                    Dim o = cmd.ExecuteScalar()
                    If o Is Nothing OrElse o Is DBNull.Value Then Return Nothing
                    Return o.ToString().Trim()
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>Resultado parseado de la consulta de estado DGII.</summary>
    Public Class DgiiEstadoRespuesta
        Public Property Estado As String = "Desconocido"
        Public Property Mensaje As String
        Public Property CodigoMensaje As String
        Public Property TrackId As String
        Public Property Encf As String
        Public Property SecuenciaUtilizada As Boolean
        Public Property Aceptado As Boolean

        Public Function MensajeParaUsuario() As String
            If Aceptado Then
                If Not String.IsNullOrWhiteSpace(Mensaje) Then Return Mensaje
                Return "Aceptado por la DGII"
            End If
            Dim titulo = If(String.IsNullOrWhiteSpace(Estado), "Rechazado", Estado)
            If String.IsNullOrWhiteSpace(Mensaje) Then Return titulo
            Return titulo & ": " & Mensaje
        End Function
    End Class

    ''' <summary>Parsea JSON de consulta DGII (estado, mensajes, encf).</summary>
    Public Shared Function ParsearRespuestaDgii(respuestaConsulta As String) As DgiiEstadoRespuesta
        Dim r As New DgiiEstadoRespuesta()
        If String.IsNullOrWhiteSpace(respuestaConsulta) Then
            r.Estado = "No procesado"
            r.Mensaje = "Sin respuesta de la DGII"
            Return r
        End If
        Try
            Dim root = JObject.Parse(respuestaConsulta)
            Dim payload As JToken = root
            If root.SelectToken("body") IsNot Nothing Then payload = root.SelectToken("body")

            If payload.SelectToken("estado") IsNot Nothing Then r.Estado = CStr(payload.SelectToken("estado"))
            If payload.SelectToken("trackId") IsNot Nothing Then r.TrackId = CStr(payload.SelectToken("trackId"))
            If payload.SelectToken("encf") IsNot Nothing Then r.Encf = CStr(payload.SelectToken("encf"))
            If payload.SelectToken("secuenciaUtilizada") IsNot Nothing Then
                r.SecuenciaUtilizada = CBool(payload.SelectToken("secuenciaUtilizada"))
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
                    r.Mensaje = String.Join(" | ", partes)
                Else
                    r.Mensaje = jt.ToString()
                End If
            End If

            Dim cm = payload.SelectToken("codigo")
            If cm IsNot Nothing Then r.CodigoMensaje = cm.ToString()
        Catch
            r.Estado = "No procesado"
            r.Mensaje = respuestaConsulta
        End Try

        r.Aceptado =
            String.Equals(r.Estado, "Aceptado", StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(r.Estado, "Aceptado Condicional", StringComparison.OrdinalIgnoreCase)
        If r.Estado = "No procesado" OrElse r.Estado = "" OrElse r.Estado = "Desconocido" Then
            r.Aceptado = False
        End If
        Return r
    End Function

    ''' <summary>JSON para Delphi: ok, message (error legible), estado, codigo, encf, trackId.</summary>
    Public Shared Function ConstruirJsonRespuestaOperacion(
        parseada As DgiiEstadoRespuesta,
        Optional rawResponse As String = Nothing
    ) As String
        If parseada Is Nothing Then parseada = New DgiiEstadoRespuesta()
        Dim payload = New With {
            .ok = parseada.Aceptado,
            .estado = parseada.Estado,
            .message = parseada.MensajeParaUsuario(),
            .mensaje = parseada.Mensaje,
            .codigo = parseada.CodigoMensaje,
            .trackId = parseada.TrackId,
            .encf = parseada.Encf,
            .secuenciaUtilizada = parseada.SecuenciaUtilizada,
            .rawResponse = rawResponse
        }
        Return JsonConvert.SerializeObject(payload)
    End Function

    ''' <summary>Lee Parametros.Usa_FacturacionElectronica para la empresa (DashA).</summary>
    Public Shared Function ObtenerUsaFacturacionElectronica(cadenaConexion As String, emp_codigo As String) As Boolean
        If String.IsNullOrWhiteSpace(cadenaConexion) OrElse String.IsNullOrWhiteSpace(emp_codigo) Then Return False
        Const sql As String = "SELECT Usa_FacturacionElectronica FROM dbo.Parametros WHERE emp_codigo = @emp"
        Try
            Using conn As New SqlConnection(cadenaConexion)
                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.Add("@emp", SqlDbType.Int).Value = Convert.ToInt32(emp_codigo.Trim())
                    conn.Open()
                    Dim o = cmd.ExecuteScalar()
                    Return InterpretarFlagSiNo(o)
                End Using
            End Using
        Catch
            Return False
        End Try
    End Function

    ''' <summary>Lee Parametros.URL_FacturacionElectronica (ambiente DGII del DashA).</summary>
    Public Shared Function ObtenerUrlFacturacionElectronica(cadenaConexion As String, emp_codigo As String) As String
        If String.IsNullOrWhiteSpace(cadenaConexion) OrElse String.IsNullOrWhiteSpace(emp_codigo) Then Return ""
        Const sql As String = "SELECT URL_FacturacionElectronica FROM dbo.Parametros WHERE emp_codigo = @emp"
        Try
            Using conn As New SqlConnection(cadenaConexion)
                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.Add("@emp", SqlDbType.Int).Value = Convert.ToInt32(emp_codigo.Trim())
                    conn.Open()
                    Dim o = cmd.ExecuteScalar()
                    If o Is Nothing OrElse o Is DBNull.Value Then Return ""
                    Return o.ToString().Trim()
                End Using
            End Using
        Catch
            Return ""
        End Try
    End Function

    Private Shared Function InterpretarFlagSiNo(valor As Object) As Boolean
        If valor Is Nothing OrElse valor Is DBNull.Value Then Return False
        If TypeOf valor Is Boolean Then Return CBool(valor)
        Dim s = valor.ToString().Trim()
        If s = "" Then Return False
        If String.Equals(s, "0", StringComparison.OrdinalIgnoreCase) Then Return False
        If String.Equals(s, "false", StringComparison.OrdinalIgnoreCase) Then Return False
        If String.Equals(s, "N", StringComparison.OrdinalIgnoreCase) Then Return False
        If String.Equals(s, "NO", StringComparison.OrdinalIgnoreCase) Then Return False
        Return s = "1" OrElse
               String.Equals(s, "true", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(s, "S", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(s, "SI", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>Último motivo en Documentos_eNCF_Bitacora (rechazo o motivo_rechazo).</summary>
    Public Shared Function ObtenerMotivoRechazoEncfBitacora(cadenaConexion As String, doc As EncfDocumentoRef) As String
        If doc Is Nothing OrElse String.IsNullOrWhiteSpace(cadenaConexion) Then Return Nothing
        Const sql As String =
"SELECT TOP (1) motivo_rechazo
FROM dbo.Documentos_eNCF_Bitacora
WHERE tipo_documento = @tipo AND doc_clave = @clave
  AND motivo_rechazo IS NOT NULL AND LTRIM(RTRIM(motivo_rechazo)) <> ''
ORDER BY secuencia_encf DESC, fecha_registro DESC;"
        Try
            Using conn As New SqlConnection(cadenaConexion)
                Using cmd As New SqlCommand(sql, conn)
                    cmd.Parameters.Add("@tipo", SqlDbType.VarChar, 30).Value = doc.TipoDocumento.ToUpperInvariant()
                    cmd.Parameters.Add("@clave", SqlDbType.VarChar, 120).Value = doc.DocClave
                    conn.Open()
                    Dim o = cmd.ExecuteScalar()
                    If o Is Nothing OrElse o Is DBNull.Value Then Return Nothing
                    Return o.ToString().Trim()
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    Public Shared Sub InsertarBitacoraDGII(cadenaConexion As String, emp_codigo As String, fac_numero As String, estado As String,
                                        trackingID As String, codigoError As String, mensajeError As String,
                                        xmlFirmado As String, fechaEnvio As DateTime, usu_codigo As String, caja As String,
                                        tipo As String)
        Using conn As New SqlConnection(cadenaConexion)
            Using cmd As New SqlCommand("INSERT INTO BitacoraFacturacionElectronicaDGII (emp_codigo, fac_numero, Estado, trackingID, CodigoError, MensajeError, XML_Firmado, Fecha_Envio, usu_codigo, caja, tipo) " &
                                    "VALUES (@emp_codigo, @fac_numero, @Estado, @trackingID, @CodigoError, @MensajeError, @XML_Firmado, @Fecha_Envio, @usu_codigo, @caja, @tipo)", conn)

                cmd.Parameters.AddWithValue("@emp_codigo", emp_codigo)
                cmd.Parameters.AddWithValue("@fac_numero", fac_numero)
                cmd.Parameters.AddWithValue("@Estado", estado)
                cmd.Parameters.AddWithValue("@trackingID", trackingID)
                cmd.Parameters.AddWithValue("@CodigoError", codigoError)
                cmd.Parameters.AddWithValue("@MensajeError", mensajeError)
                cmd.Parameters.AddWithValue("@XML_Firmado", xmlFirmado)
                cmd.Parameters.AddWithValue("@Fecha_Envio", fechaEnvio)
                cmd.Parameters.AddWithValue("@usu_codigo", usu_codigo)
                cmd.Parameters.AddWithValue("@caja", caja)
                cmd.Parameters.Add("@tipo", SqlDbType.VarChar, 30).Value =
                    If(String.IsNullOrWhiteSpace(tipo), CObj(DBNull.Value), CObj(tipo.Trim()))

                conn.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Public Shared Sub AddElement(doc As XmlDocument, parent As XmlElement, name As String, value As String)
        Dim elem As XmlElement = doc.CreateElement(name)
        elem.InnerText = value
        parent.AppendChild(elem)
    End Sub
    Public Shared Sub AddOptionalElement(doc As XmlDocument, parent As XmlElement, name As String, value As Object)
        If value IsNot DBNull.Value AndAlso Not String.IsNullOrEmpty(value.ToString()) Then
            AddElement(doc, parent, name, value.ToString())
        End If
    End Sub

    Private Shared ReadOnly TelefonoXsdRegex As New Regex("^\d{3}-\d{3}-\d{4}$", RegexOptions.Compiled)

    ''' <summary>Normaliza un teléfono al formato XSD DGII: 809-999-9999.</summary>
    Public Shared Function NormalizarTelefonoDgii(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then Return ""

        Dim trimmed = raw.Trim()
        If TelefonoXsdRegex.IsMatch(trimmed) Then Return trimmed

        Dim digits As New StringBuilder()
        For Each c As Char In trimmed
            If Char.IsDigit(c) Then digits.Append(c)
        Next

        Dim d = digits.ToString()
        If d.Length = 11 AndAlso d.StartsWith("1", StringComparison.Ordinal) Then
            d = d.Substring(1)
        End If

        If d.Length = 10 Then
            Return d.Substring(0, 3) & "-" & d.Substring(3, 3) & "-" & d.Substring(6, 4)
        End If

        Return ""
    End Function

    ''' <summary>Divide cadenas con varios teléfonos y devuelve hasta 3 válidos para el XSD.</summary>
    Public Shared Function ExtraerTelefonosEmisorValidos(ParamArray valoresTelefono As String()) As List(Of String)
        Dim resultado As New List(Of String)()
        If valoresTelefono Is Nothing Then Return resultado

        For Each raw In valoresTelefono
            If raw Is Nothing Then Continue For
            Dim partes = raw.Split(New Char() {"/"c, ";"c, "|"c, ","c, ChrW(10), ChrW(13)}, StringSplitOptions.RemoveEmptyEntries)
            For Each parte In partes
                Dim tel = NormalizarTelefonoDgii(parte)
                If tel = "" Then Continue For
                If resultado.Exists(Function(x) String.Equals(x, tel, StringComparison.Ordinal)) Then Continue For
                resultado.Add(tel)
                If resultado.Count >= 3 Then Return resultado
            Next
        Next

        Return resultado
    End Function

    ''' <summary>
    ''' Solo agrega TablaTelefonoEmisor si hay al menos un teléfono no vacío (evita nodo vacío o solo etiqueta de cierre en serialización).
    ''' </summary>
    Public Shared Sub AppendTablaTelefonoEmisorIfAny(doc As XmlDocument, emisorNode As XmlElement, ParamArray valoresTelefono As String())
        Dim lista = ExtraerTelefonosEmisorValidos(valoresTelefono)
        If lista.Count = 0 Then Return

        Dim tabla As XmlElement = doc.CreateElement("TablaTelefonoEmisor")
        emisorNode.AppendChild(tabla)
        For Each t In lista
            AddElement(doc, tabla, "TelefonoEmisor", t)
        Next
    End Sub

    Public Shared Sub AddIfValid(doc As XmlDocument, parent As XmlElement, reader As SqlDataReader, field As String, Optional elementName As String = Nothing)
        Try
            If Not reader.IsDBNull(reader.GetOrdinal(field)) Then
                Dim value = reader(field).ToString()
                Dim name = If(String.IsNullOrEmpty(elementName), field, elementName)
                parent.AppendChild(CreateElement(doc, name, value))
            End If
        Catch ex As Exception
            ' Silenciar errores individuales si el campo no existe
        End Try
    End Sub

    Public Shared Function CreateElement(doc As XmlDocument, name As String, value As String) As XmlElement
        Dim elem As XmlElement = doc.CreateElement(name)
        elem.InnerText = value
        Return elem
    End Function

    Public Shared Sub RegistrarLogCliente(mensaje As String)
        Try
            Dim carpetaLogs As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogsCliente")
            If Not Directory.Exists(carpetaLogs) Then
                Directory.CreateDirectory(carpetaLogs)
            End If

            Dim logPath As String = Path.Combine(carpetaLogs, $"envio_cliente_{DateTime.Now:yyyyMMdd}.log")
            Dim linea As String = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {mensaje}"

            Using writer As New StreamWriter(logPath, True)
                writer.WriteLine(linea)
            End Using
        Catch ex As Exception
            ' Silenciar error en log para evitar que afecte el flujo principal
        End Try
    End Sub


    ' Devuelve "" si la columna no existe, es DBNull o Nothing
    Public Shared Function SafeStr(r As IDataRecord, col As String) As String
        If r Is Nothing OrElse String.IsNullOrWhiteSpace(col) Then Return ""
        Try
            Dim i = r.GetOrdinal(col)
            If r.IsDBNull(i) Then Return ""
            Return Convert.ToString(r.GetValue(i)).Trim()
        Catch ex As IndexOutOfRangeException
            Return ""
        End Try
    End Function

    ' Intenta leer Decimal de forma segura (acepta coma o punto). Devuelve False si no se pudo.
    Public Shared Function TryGetDec(r As IDataRecord, col As String, ByRef value As Decimal) As Boolean
        value = 0D
        Dim s = SafeStr(r, col)
        If s = "" Then Return False
        ' normaliza coma → punto
        s = s.Replace(","c, "."c)
        Return Decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, value)
    End Function

    ' Intenta leer DateTime aceptando varios formatos comunes y valores nulos
    Public Shared Function TryGetDate(r As IDataRecord, col As String, ByRef dt As DateTime) As Boolean
        dt = Date.MinValue
        Dim s = SafeStr(r, col)
        If s = "" Then Return False
        ' formatos que suelen venir de SQL y UIs
        Dim fmts = {
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "dd-MM-yyyy",
        "dd-MM-yyyy HH:mm:ss",
        "dd/MM/yyyy",
        "dd/MM/yyyy HH:mm:ss",
        "yyyyMMdd",
        "yyyyMMdd HHmmss",
        "s",            ' ISO-8601 sortable
        "o"             ' ISO-8601 round-trip
    }
        Return DateTime.TryParseExact(
        s,
        fmts,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeLocal Or DateTimeStyles.AllowWhiteSpaces,
        dt)
    End Function

    Public Class DirectorioDgiiConsulta
        Public Property Encontrado As Boolean
        Public Property Nombre As String
        Public Property Rnc As String
        Public Property UrlRecepcion As String
        Public Property UrlAceptacion As String
        Public Property UrlOpcional As String
        Public Property Mensaje As String
        Public Property HttpStatus As Integer
        Public Property RawResponse As String
    End Class

  ''' <summary>GET consultadirectorio DGII por RNC (síncrono).</summary>
    Public Shared Function ConsultarDirectorioPorRncHttp(urlBase As String, token As String, rnc As String) As DirectorioDgiiConsulta
        Dim res As New DirectorioDgiiConsulta With {
            .Encontrado = False,
            .Mensaje = "Sin respuesta"
        }

        Dim rncNorm = RutaHelper.NormalizarRnc(rnc)
        If rncNorm = "" Then
            res.Mensaje = "RNC vacío o inválido"
            Return res
        End If

        Dim base = RutaHelper.NormalizarUrlBaseDgii(urlBase)
        If base = "" Then
            res.Mensaje = "url_base DGII no configurada"
            Return res
        End If

        If String.IsNullOrWhiteSpace(token) Then
            res.Mensaje = "Token DGII requerido"
            Return res
        End If

        Dim url = base & "consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC=" & Uri.EscapeDataString(rncNorm)

        Try
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
            Dim request As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
            request.Method = "GET"
            request.Accept = "application/json"
            request.Headers.Add("Authorization", "Bearer " & token.Trim())

            Using response As HttpWebResponse = CType(request.GetResponse(), HttpWebResponse)
                res.HttpStatus = CInt(response.StatusCode)
                Using reader As New StreamReader(response.GetResponseStream())
                    res.RawResponse = reader.ReadToEnd()
                End Using

                If response.StatusCode = HttpStatusCode.NoContent Then
                    res.Mensaje = "RNC no registrado como emisor electrónico en el directorio DGII"
                    Return res
                End If

                ParsearJsonDirectorioDgii(res)
            End Using
        Catch ex As WebException
            res.HttpStatus = 0
            If ex.Response IsNot Nothing Then
                res.HttpStatus = CInt(CType(ex.Response, HttpWebResponse).StatusCode)
                Using reader As New StreamReader(ex.Response.GetResponseStream())
                    res.RawResponse = reader.ReadToEnd()
                End Using
                If res.HttpStatus = 401 Then
                    res.Mensaje = "Token DGII inválido o expirado"
                ElseIf res.HttpStatus = 204 Then
                    res.Mensaje = "RNC no registrado como emisor electrónico en el directorio DGII"
                Else
                    res.Mensaje = "Error HTTP " & res.HttpStatus.ToString()
                End If
            Else
                res.Mensaje = ex.Message
            End If
        Catch ex As Exception
            res.Mensaje = ex.Message
        End Try

        Return res
    End Function

    Private Shared Sub ParsearJsonDirectorioDgii(res As DirectorioDgiiConsulta)
        If String.IsNullOrWhiteSpace(res.RawResponse) Then
            res.Mensaje = "Respuesta vacía de la DGII"
            Return
        End If

        Try
            Dim token As JToken = JToken.Parse(res.RawResponse)
            Dim item As JObject = Nothing

            If token.Type = JTokenType.Array Then
                Dim arr = CType(token, JArray)
                If arr.Count = 0 Then
                    res.Mensaje = "RNC no registrado como emisor electrónico en el directorio DGII"
                    Return
                End If
                item = CType(arr(0), JObject)
            ElseIf token.Type = JTokenType.Object Then
                item = CType(token, JObject)
            End If

            If item Is Nothing Then
                res.Mensaje = "Formato de respuesta no reconocido"
                Return
            End If

            res.Nombre = If(item("nombre"), "").ToString().Trim()
            res.Rnc = If(item("rnc"), "").ToString().Trim()
            res.UrlRecepcion = If(item("urlRecepcion"), "").ToString().Trim()
            res.UrlAceptacion = If(item("urlAceptacion"), "").ToString().Trim()
            res.UrlOpcional = If(item("urlOpcional"), "").ToString().Trim()

            If res.Nombre <> "" OrElse res.UrlRecepcion <> "" Then
                res.Encontrado = True
                res.Mensaje = res.Nombre
            Else
                res.Mensaje = "RNC no registrado como emisor electrónico en el directorio DGII"
            End If
        Catch ex As Exception
            res.Mensaje = "Error al interpretar respuesta DGII: " & ex.Message
        End Try
    End Sub

    Public Shared Function ConstruirJsonDirectorioDgii(ok As Boolean, consulta As DirectorioDgiiConsulta) As String
        If consulta Is Nothing Then consulta = New DirectorioDgiiConsulta()
        Dim payload = New With {
            .ok = ok,
            .encontrado = consulta.Encontrado,
            .nombre = consulta.Nombre,
            .rnc = consulta.Rnc,
            .urlRecepcion = consulta.UrlRecepcion,
            .urlAceptacion = consulta.UrlAceptacion,
            .urlOpcional = consulta.UrlOpcional,
            .message = consulta.Mensaje,
            .httpStatus = consulta.HttpStatus,
            .rawResponse = consulta.RawResponse
        }
        Return JsonConvert.SerializeObject(payload)
    End Function

    ''' <summary>Quita nodos de IdDoc que existen en otros e-CF (p. ej. E41) pero no en el XSD 47.</summary>
    Public Shared Sub EliminarCamposIdDocNoPermitidosE47(idDocNode As XmlElement)
        If idDocNode Is Nothing Then Return
        Dim prohibidos As String() = {
            "IndicadorMontoGravado",
            "IndicadorEnvioDiferido",
            "TipoIngresos",
            "IndicadorNotaCredito"
        }
        For Each nombre In prohibidos
            Dim nodo = idDocNode.SelectSingleNode(nombre)
            If nodo IsNot Nothing Then idDocNode.RemoveChild(nodo)
        Next
    End Sub

    ''' <summary>Valida estructura mínima y XSD del e-CF 47 antes de firmar.</summary>
    Public Shared Sub ValidarXmlE47(xmlDoc As XmlDocument)
        ValidarCamposObligatoriosE47(xmlDoc)
        ValidarXmlE47ContraXsd(xmlDoc)
    End Sub

    Private Shared Sub ValidarCamposObligatoriosE47(xmlDoc As XmlDocument)
        If xmlDoc Is Nothing OrElse xmlDoc.DocumentElement Is Nothing Then
            Throw New ApplicationException("XML E47 vacío.")
        End If

        Dim errores As New List(Of String)()

        If xmlDoc.SelectSingleNode("//Encabezado/IdDoc/TipoeCF") Is Nothing Then errores.Add("Falta IdDoc/TipoeCF")
        If xmlDoc.SelectSingleNode("//Encabezado/IdDoc/eNCF") Is Nothing Then errores.Add("Falta IdDoc/eNCF")
        If xmlDoc.SelectSingleNode("//Encabezado/IdDoc/FechaVencimientoSecuencia") Is Nothing Then errores.Add("Falta IdDoc/FechaVencimientoSecuencia")
        If xmlDoc.SelectSingleNode("//Encabezado/Totales/MontoTotal") Is Nothing Then errores.Add("Falta Totales/MontoTotal")
        If xmlDoc.SelectSingleNode("//Encabezado/Emisor/RNCEmisor") Is Nothing Then errores.Add("Falta Emisor/RNCEmisor")
        If xmlDoc.SelectSingleNode("//Encabezado/Emisor/FechaEmision") Is Nothing Then errores.Add("Falta Emisor/FechaEmision")

        Dim items = xmlDoc.SelectNodes("//DetallesItems/Item")
        If items Is Nothing OrElse items.Count = 0 Then
            errores.Add("DetallesItems debe incluir al menos un Item")
        Else
            For Each itemNode As XmlNode In items
                Dim item = CType(itemNode, XmlElement)
                If item.SelectSingleNode("NumeroLinea") Is Nothing Then errores.Add("Item sin NumeroLinea")
                If item.SelectSingleNode("IndicadorFacturacion") Is Nothing Then
                    errores.Add("Item sin IndicadorFacturacion")
                ElseIf item.SelectSingleNode("IndicadorFacturacion").InnerText.Trim() <> "4" Then
                    errores.Add("E47 requiere IndicadorFacturacion=4 (exento)")
                End If
                If item.SelectSingleNode("Retencion/IndicadorAgenteRetencionoPercepcion") Is Nothing Then errores.Add("Item sin Retencion/IndicadorAgenteRetencionoPercepcion")
                If item.SelectSingleNode("Retencion/MontoISRRetenido") Is Nothing Then errores.Add("Item sin Retencion/MontoISRRetenido")
                If item.SelectSingleNode("NombreItem") Is Nothing Then errores.Add("Item sin NombreItem")
                If item.SelectSingleNode("IndicadorBienoServicio") Is Nothing Then
                    errores.Add("Item sin IndicadorBienoServicio")
                ElseIf item.SelectSingleNode("IndicadorBienoServicio").InnerText.Trim() <> "2" Then
                    errores.Add("E47 requiere IndicadorBienoServicio=2 (servicio)")
                End If
                If item.SelectSingleNode("CantidadItem") Is Nothing Then errores.Add("Item sin CantidadItem")
                If item.SelectSingleNode("PrecioUnitarioItem") Is Nothing Then errores.Add("Item sin PrecioUnitarioItem")
                If item.SelectSingleNode("MontoItem") Is Nothing Then errores.Add("Item sin MontoItem")
            Next
        End If

        If xmlDoc.SelectSingleNode("//FechaHoraFirma") Is Nothing Then errores.Add("Falta FechaHoraFirma")

        If errores.Count > 0 Then
            Throw New ApplicationException("XML E47 incompleto: " & String.Join("; ", errores.Distinct()))
        End If
    End Sub

    Private Shared Sub ValidarXmlE47ContraXsd(xmlDoc As XmlDocument)
        Dim xsdPath = Path.Combine(RutaHelper.ObtenerRutaBaseDLL(), XsdE47FileName)
        If Not File.Exists(xsdPath) Then
            RegistrarLogCliente("Validación XSD E47 omitida: no se encontró " & xsdPath)
            Return
        End If

        Dim docValidacion = CType(xmlDoc.Clone(), XmlDocument)
        Dim root = docValidacion.DocumentElement
        If root IsNot Nothing AndAlso root.SelectSingleNode("Signature") Is Nothing Then
            Dim placeholderFirma = docValidacion.CreateElement("Signature")
            root.AppendChild(placeholderFirma)
        End If

        Dim schemas As New XmlSchemaSet()
        Using reader = XmlReader.Create(xsdPath)
            schemas.Add(Nothing, reader)
        End Using

        docValidacion.Schemas = schemas

        Dim erroresXsd As New List(Of String)()
        Dim handler As ValidationEventHandler =
            Sub(sender, e)
                erroresXsd.Add(e.Message)
            End Sub

        docValidacion.Validate(handler)

        If erroresXsd.Count > 0 Then
            Throw New ApplicationException("XML E47 no cumple XSD: " & String.Join("; ", erroresXsd.Distinct()))
        End If
    End Sub

End Class
