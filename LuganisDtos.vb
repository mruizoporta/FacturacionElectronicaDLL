Imports System
Imports System.Collections.Generic

' Modelos de dominio internos para la integración con LUGANIS.
' Estos DTOs NO afectan el contrato público COM existente y se usan solo dentro de la DLL.

Public Class DocumentoLuganisDto
    Public Property TipoECF As String
    Public Property ENcf As String

    ' IDOC
    ''' Tipo 31/32/33: FechaVencimientoSecuencia. Tipo 34: no se usa; usar IndicadorNotaCredito.
    Public Property FechaVencimientoSecuencia As String
    ''' <summary>Solo tipo 34. Tabla 2: 0 si e-CF afectado <= 30 días; 1 si > 30 días.</summary>
    Public Property IndicadorNotaCredito As String
    Public Property IndicadorEnvioDiferido As String
    Public Property IndicadorMontoGravado As String
    Public Property TipoIngresos As String
    Public Property TipoPago As String
    Public Property FechaLimitePago As String
    Public Property TerminoPago As String
    Public Property TipoCuentaPago As String
    Public Property NumeroCuentaPago As String
    Public Property BancoPago As String
    Public Property FechaDesde As String
    Public Property FechaHasta As String
    Public Property FechaEmision As String

    ' EMIS
    Public Property RncEmisor As String
    Public Property RazonSocialEmisor As String
    Public Property NombreComercial As String
    Public Property Sucursal As String
    Public Property DireccionEmisor As String
    Public Property Municipio As String
    Public Property Provincia As String
    Public Property TelefonosEmisor As List(Of String)
    Public Property CorreoEmisor As String
    Public Property WebSite As String
    Public Property ActividadEconomica As String
    Public Property CodigoVendedor As String
    Public Property NumeroFacturaInterna As String
    Public Property NumeroPedidoInterno As String
    Public Property ZonaVenta As String
    Public Property RutaVenta As String
    Public Property InformacionAdicionalEmisor As String

    ' COMP (tipo 34 tiene IdentificadorExtranjero entre RNC y RazonSocial)
    Public Property RncComprador As String
    Public Property IdentificadorExtranjero As String
    Public Property RazonSocialComprador As String
    Public Property ContactoComprador As String
    Public Property CorreoComprador As String
    Public Property DireccionComprador As String
    Public Property MunicipioComprador As String
    Public Property ProvinciaComprador As String
    Public Property FechaEntrega As String
    Public Property ContactoEntrega As String
    Public Property DireccionEntrega As String
    Public Property TelefonoAdicional As String
    Public Property FechaOrdenCompra As String
    Public Property NumeroOrdenCompra As String
    Public Property CodigoInternoComprador As String
    Public Property ResponsablePago As String
    Public Property InformacionAdicionalComprador As String

    ' OTMN
    Public Property TipoMoneda As String
    Public Property TipoCambio As String

    ' Referencias (legacy)
    Public Property NombreEmisor As String
    Public Property NombreReceptor As String
    Public Property NumeroInterno As String
    Public Property MontoGravado As Decimal?
    Public Property MontoExento As Decimal?
    Public Property Itbis As Decimal?
    Public Property TotalFactura As Decimal?
    Public Property Moneda As String

    Public Property Items As List(Of ItemLuganisDto)
    Public Property Pagos As List(Of PagoLuganisDto)
    Public Property DescuentosRecargos As List(Of DescuentoRecargoDto)
    Public Property Referencias As List(Of ReferenciaDto)

    ' INFR (Información de Referencia) - obligatorio para tipo 34 (Nota Crédito/Devolución)
    Public Property NCFModificado As String
    Public Property FechaNCFModificado As String
    Public Property CodigoModificacion As String
    Public Property RazonModificacion As String
    Public Property RNCOtroContribuyente As String

    Public Sub New()
        Items = New List(Of ItemLuganisDto)()
        Pagos = New List(Of PagoLuganisDto)()
        DescuentosRecargos = New List(Of DescuentoRecargoDto)()
        Referencias = New List(Of ReferenciaDto)()
        TelefonosEmisor = New List(Of String)()
    End Sub
End Class

Public Class ItemLuganisDto
    Public Property NumeroLinea As Integer?
    Public Property TipoCodigo As String
    Public Property CodigoItem As String
    Public Property IndicadorFacturacion As String
    Public Property IndicadorAgenteRetencionPercepcion As String
    Public Property MontoITBISRetenido As Decimal?
    Public Property MontoISRRetenido As Decimal?
    Public Property NombreItem As String
    Public Property IndicadorBienoServicio As String
    Public Property Descripcion As String
    Public Property Cantidad As Decimal?
    Public Property UnidadMedida As String
    Public Property CantidadReferencia As Decimal?
    Public Property UnidadReferencia As String
    Public Property Subcantidad As String
    Public Property CodigoSubcantidad As String
    Public Property GradosAlcohol As Decimal?
    Public Property PrecioUnitarioReferencia As Decimal?
    Public Property FechaElaboracion As String
    Public Property FechaVencimientoItem As String
    ' Solo tipo 34: posiciones 19-22 del ITEM (antes de PrecioUnitario)
    Public Property PesoNetoKilogramo As Decimal?
    Public Property PesoNetoMineria As Decimal?
    Public Property TipoAfiliacion As String
    Public Property Liquidacion As String
    Public Property PrecioUnitario As Decimal?
    Public Property DescuentoMonto As Decimal?
    Public Property TipoSubDescuento As String
    Public Property SubDescuentoPorcentaje As Decimal?
    Public Property MontoSubDescuento As Decimal?
    Public Property RecargoMonto As Decimal?
    Public Property TipoSubRecargo As String
    Public Property SubRecargoPorcentaje As Decimal?
    Public Property MontoSubRecargo As Decimal?
    Public Property TipoImpuesto As String
    Public Property PrecioOtraMoneda As Decimal?
    Public Property DescuentoOtraMoneda As Decimal?
    Public Property RecargoOtraMoneda As Decimal?
    Public Property MontoItemOtraMoneda As Decimal?
    Public Property MontoLinea As Decimal?
End Class

Public Class PagoLuganisDto
    Public Property FormaPago As String
    Public Property MontoPago As Decimal?
End Class

Public Class DescuentoRecargoDto
    Public Property NumeroLinea As Integer?
    Public Property TipoAjuste As String
    ''' <summary>DERE pos 3 según doc (opcional). Tabla Indicador_Norma_1007.</summary>
    Public Property IndicadorNorma1007 As String
    Public Property Descripcion As String
    Public Property TipoValor As String
    Public Property ValorPorcentajeOMonto As Decimal?
    Public Property MontoDescuentoRecargo As Decimal?
    Public Property MontoOtraMoneda As Decimal?
    Public Property IndicadorFacturacion As String
End Class

Public Class ReferenciaDto
    ' Referencias a otros documentos (por ejemplo, NC, ND, factura origen, etc.)
    Public Property TipoReferencia As String
    Public Property ENcfReferencia As String
    Public Property NumeroInternoReferencia As String
    Public Property FechaReferencia As DateTime?

    ' TODO: completar según "Catálogo de tablas" y reglas por tipo de e-CF.
End Class

Public Class LuganisLoginRequest
    ' Datos necesarios para el login en /authentication-service/auth/login/COMPANY.
    Public Property BaseUrl As String
    Public Property CompanyCode As String
    Public Property Username As String
    Public Property Password As String

    ' Metadatos adicionales enviados en el login o trazabilidad,
    ' según lo definido por LUGANIS.
    Public Property AppVersion As String
    Public Property Os As String
    Public Property DeviceId As String
    Public Property Latitude As String
    Public Property Longitude As String
    Public Property ProviderIpAddress As String

    ' TODO: ajustar a la estructura exacta esperada por LUGANIS (body JSON, campos, etc.).
End Class

Public Class LuganisLoginResponse
    Public Property Success As Boolean
    Public Property Token As String
    Public Property RawResponse As String
    Public Property ErrorMessage As String
    Public Property HttpStatusCode As Integer
End Class

Public Class LuganisSendRequest
    Public Property BaseUrl As String
    Public Property Token As String
    Public Property DeviceId As String
    Public Property FileName As String
    Public Property FileContentBase64 As String
End Class

Public Class LuganisSendResponse
    ' Respuesta cruda del envío a /parser-service/send.
    Public Property Success As Boolean
    Public Property HttpStatusCode As Integer
    Public Property RawResponse As String
    Public Property ErrorMessage As String

    ' Identificador de tracking que pueda devolver LUGANIS
    ' para hacer consultas posteriores, si aplica.
    Public Property TrackId As String

    ' TODO: mapear campos específicos devueltos por LUGANIS (códigos, descripciones, IDs, etc.).
End Class

''' <summary>
''' Respuesta de la consulta de estado por trackId (GET /parser-service/read/trackId/...).
''' Incluye status "Aceptado", "Rechazado", "Aceptado Condicional", etc.
''' </summary>
Public Class LuganisStatusResponse
    Public Property Success As Boolean
    Public Property HttpStatusCode As Integer
    Public Property RawResponse As String
    Public Property ErrorMessage As String
    ''' <summary>Valor devuelto por LUGANIS: "Aceptado", "Rechazado", "Aceptado Condicional", "En proceso", etc.</summary>
    Public Property Status As String
    Public Property ResponseMessage As String
    Public Property ResponseCode As String
End Class

Public Class LuganisQrRequest
    Public Property BaseUrl As String
    Public Property Token As String
    Public Property DeviceId As String
    Public Property RncEmisor As String
    Public Property ENcf As String
End Class

Public Class LuganisQrResponse
    Public Property Success As Boolean
    Public Property HttpStatusCode As Integer
    Public Property QrCode As String
    Public Property RawResponse As String
    Public Property ErrorMessage As String
End Class

Public Class LuganisResult
    ' Resultado de alto nivel que se devolverá al código que llama (y, eventualmente, a Delphi).
    Public Property Success As Boolean
    Public Property Codigo As String
    Public Property Mensaje As String
    Public Property FileName As String
    Public Property RawResponse As String

    ' Contenido TXT generado (opcional, para debug o para guardar en disco).
    Public Property TxtContent As String

    ' Identificador de tracking que devuelva LUGANIS (opcional).
    Public Property TrackId As String

    ''' <summary>True = Aceptado/Aceptado Condicional por LUGANIS; False = Rechazado u otro; Nothing = no consultado o desconocido.</summary>
    Public Property AceptadoPorLuganis As Boolean?

    ''' <summary>Estado devuelto por GET read/trackId (Aceptado, Rechazado, etc.).</summary>
    Public Property EstadoLuganis As String

    ' Fabric helpers para éxito / error
    Public Shared Function Ok(fileName As String, rawResponse As String, Optional txtContent As String = Nothing, Optional trackId As String = Nothing, Optional aceptadoPorLuganis As Boolean? = Nothing) As LuganisResult
        Return New LuganisResult With {
            .Success = True,
            .Codigo = "OK",
            .Mensaje = "Envio a LUGANIS realizado correctamente.",
            .FileName = fileName,
            .RawResponse = rawResponse,
            .TxtContent = txtContent,
            .TrackId = trackId,
            .AceptadoPorLuganis = aceptadoPorLuganis
        }
    End Function

    Public Shared Function Fail(codigo As String, mensaje As String, Optional rawResponse As String = Nothing, Optional trackId As String = Nothing, Optional aceptadoPorLuganis As Boolean? = False) As LuganisResult
        Return New LuganisResult With {
            .Success = False,
            .Codigo = If(String.IsNullOrEmpty(codigo), "ERROR", codigo),
            .Mensaje = mensaje,
            .FileName = Nothing,
            .RawResponse = rawResponse,
            .TrackId = trackId,
            .AceptadoPorLuganis = aceptadoPorLuganis
        }
    End Function
End Class

