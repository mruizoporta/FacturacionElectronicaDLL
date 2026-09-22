Imports System
Imports System.Xml
Imports System.Globalization

' Responsabilidad: leer el XML de e-CF generado por esta misma DLL
' y transformarlo en un modelo neutral DocumentoLuganisDto para
' el consumo de LUGANIS.

Friend Class LuganisXmlReader

    ''' <summary>
    ''' Convierte el XmlDocument de e-CF a un DocumentoLuganisDto neutral.
    ''' NO modifica el XML, solo lo interpreta.
    ''' </summary>
    ''' <param name="xmlDoc">XML del e-CF ya generado por los métodos actuales.</param>
    ''' <param name="tipoECF">Tipo de e-CF (31, 32, 33, 34, 41, 43, 44, 45, 46, 47, etc.).</param>
    Friend Shared Function Parse(xmlDoc As XmlDocument, tipoECF As String) As DocumentoLuganisDto
        If xmlDoc Is Nothing Then Throw New ArgumentNullException(NameOf(xmlDoc))

        Dim result As New DocumentoLuganisDto() With {.TipoECF = If(tipoECF, String.Empty)}

        ' --- IDOC ---
        result.ENcf = V(xmlDoc, "//IdDoc/eNCF")
        result.FechaVencimientoSecuencia = V(xmlDoc, "//IdDoc/FechaVencimientoSecuencia")
        result.IndicadorNotaCredito = V(xmlDoc, "//IdDoc/IndicadorNotaCredito")
        result.IndicadorEnvioDiferido = V(xmlDoc, "//IdDoc/IndicadorEnvioDiferido")
        result.IndicadorMontoGravado = V(xmlDoc, "//IdDoc/IndicadorMontoGravado")
        result.TipoIngresos = V(xmlDoc, "//IdDoc/TipoIngresos")
        result.TipoPago = V(xmlDoc, "//IdDoc/TipoPago")
        result.FechaLimitePago = V(xmlDoc, "//IdDoc/FechaLimitePago")
        result.TerminoPago = V(xmlDoc, "//IdDoc/TerminoPago")
        result.TipoCuentaPago = V(xmlDoc, "//IdDoc/TipoCuentaPago")
        result.NumeroCuentaPago = V(xmlDoc, "//IdDoc/NumeroCuentaPago")
        result.BancoPago = V(xmlDoc, "//IdDoc/BancoPago")
        result.FechaDesde = V(xmlDoc, "//IdDoc/FechaDesde")
        result.FechaHasta = V(xmlDoc, "//IdDoc/FechaHasta")
        result.FechaEmision = V(xmlDoc, "//Emisor/FechaEmision")
        If String.IsNullOrWhiteSpace(result.FechaEmision) Then result.FechaEmision = V(xmlDoc, "//IdDoc/FechaEmision")

        ' --- EMIS ---
        result.RncEmisor = V(xmlDoc, "//Emisor/RNCEmisor")
        result.RazonSocialEmisor = V(xmlDoc, "//Emisor/RazonSocialEmisor")
        result.NombreComercial = V(xmlDoc, "//Emisor/NombreComercial")
        result.Sucursal = V(xmlDoc, "//Emisor/Sucursal")
        result.DireccionEmisor = V(xmlDoc, "//Emisor/DireccionEmisor")
        result.Municipio = V(xmlDoc, "//Emisor/Municipio")
        result.Provincia = V(xmlDoc, "//Emisor/Provincia")
        result.CorreoEmisor = V(xmlDoc, "//Emisor/CorreoEmisor")
        result.WebSite = V(xmlDoc, "//Emisor/WebSite")
        result.ActividadEconomica = V(xmlDoc, "//Emisor/ActividadEconomica")
        result.CodigoVendedor = V(xmlDoc, "//Emisor/CodigoVendedor")
        result.NumeroFacturaInterna = V(xmlDoc, "//Emisor/NumeroFacturaInterna")
        result.NumeroPedidoInterno = V(xmlDoc, "//Emisor/NumeroPedidoInterno")
        result.ZonaVenta = V(xmlDoc, "//Emisor/ZonaVenta")
        result.RutaVenta = V(xmlDoc, "//Emisor/RutaVenta")
        result.InformacionAdicionalEmisor = V(xmlDoc, "//Emisor/InformacionAdicionalEmisor")

        ' Telefonos (repetibles)
        Dim telefonos = xmlDoc.SelectNodes("//Emisor/TablaTelefonoEmisor/TelefonoEmisor")
        If telefonos IsNot Nothing Then
            For Each n As XmlNode In telefonos
                Dim t As String = (If(n?.InnerText, String.Empty)).Trim()
                If Not String.IsNullOrEmpty(t) Then result.TelefonosEmisor.Add(t)
            Next
        End If

        ' --- COMP ---
        result.RncComprador = V(xmlDoc, "//Comprador/RNCComprador")
        result.IdentificadorExtranjero = V(xmlDoc, "//Comprador/IdentificadorExtranjero")
        result.RazonSocialComprador = V(xmlDoc, "//Comprador/RazonSocialComprador")
        result.ContactoComprador = V(xmlDoc, "//Comprador/ContactoComprador")
        result.CorreoComprador = V(xmlDoc, "//Comprador/CorreoComprador")
        result.DireccionComprador = V(xmlDoc, "//Comprador/DireccionComprador")
        result.MunicipioComprador = V(xmlDoc, "//Comprador/MunicipioComprador")
        result.ProvinciaComprador = V(xmlDoc, "//Comprador/ProvinciaComprador")
        result.FechaEntrega = V(xmlDoc, "//Comprador/FechaEntrega")
        result.ContactoEntrega = V(xmlDoc, "//Comprador/ContactoEntrega")
        result.DireccionEntrega = V(xmlDoc, "//Comprador/DireccionEntrega")
        result.TelefonoAdicional = V(xmlDoc, "//Comprador/TelefonoAdicional")
        result.FechaOrdenCompra = V(xmlDoc, "//Comprador/FechaOrdenCompra")
        result.NumeroOrdenCompra = V(xmlDoc, "//Comprador/NumeroOrdenCompra")
        result.CodigoInternoComprador = V(xmlDoc, "//Comprador/CodigoInternoComprador")
        result.ResponsablePago = V(xmlDoc, "//Comprador/ResponsablePago")
        result.InformacionAdicionalComprador = V(xmlDoc, "//Comprador/InformacionAdicionalComprador")
        If String.IsNullOrWhiteSpace(result.InformacionAdicionalComprador) Then
            result.InformacionAdicionalComprador = V(xmlDoc, "//Comprador/Informacionadicionalcomprador")
        End If

        ' --- OTMN (Otras Monedas) ---
        result.TipoMoneda = V(xmlDoc, "//OtrasMonedas/TipoMoneda")
        result.TipoCambio = V(xmlDoc, "//OtrasMonedas/TipoCambio")

        ' --- Totales ---
        result.MontoGravado = DN(xmlDoc, "//Totales/MontoGravadoTotal")
        If Not result.MontoGravado.HasValue Then result.MontoGravado = DN(xmlDoc, "//Totales/MontoGravadoI3")
        result.TotalFactura = DN(xmlDoc, "//Totales/MontoTotal")
        result.Itbis = DN(xmlDoc, "//Totales/TotalITBIS")
        result.MontoExento = DN(xmlDoc, "//Totales/MontoExento")

        ' --- Items (DetallesItems/Item) ---
        Dim itemNodes = xmlDoc.SelectNodes("//DetallesItems/Item")
        If itemNodes Is Nothing Then itemNodes = xmlDoc.SelectNodes("//Item")
        If itemNodes IsNot Nothing Then
            For Each itemNode As XmlNode In itemNodes
                result.Items.Add(ParseItem(itemNode))
            Next
        End If

        ' --- FPAG (Formas de pago) ---
        Dim formaNodes = xmlDoc.SelectNodes("//IdDoc/TablaFormasPago/FormaDePago")
        If formaNodes Is Nothing Then formaNodes = xmlDoc.SelectNodes("//TablaFormasPago/FormaDePago")
        If formaNodes Is Nothing Then formaNodes = xmlDoc.SelectNodes("//FormaDePago")
        If formaNodes IsNot Nothing Then
            For Each n As XmlNode In formaNodes
                Dim pago As New PagoLuganisDto()
                pago.FormaPago = GetNodeValue(n, "FormaPago")
                pago.MontoPago = DN(n, "MontoPago")
                If Not String.IsNullOrWhiteSpace(pago.FormaPago) OrElse pago.MontoPago.HasValue Then
                    result.Pagos.Add(pago)
                End If
            Next
        End If

        ' --- INFR (Información de Referencia) - tipo 34 NC/Devolución ---
        result.NCFModificado = V(xmlDoc, "//InformacionReferencia/NCFModificado")
        result.FechaNCFModificado = V(xmlDoc, "//InformacionReferencia/FechaNCFModificado")
        result.CodigoModificacion = V(xmlDoc, "//InformacionReferencia/CodigoModificacion")
        result.RazonModificacion = V(xmlDoc, "//InformacionReferencia/RazonModificacion")
        result.RNCOtroContribuyente = V(xmlDoc, "//InformacionReferencia/RNCOtroContribuyente")

        ' --- DERE (Descuentos y recargos) ---
        Dim dereNodes = xmlDoc.SelectNodes("//TablaDescuentosRecargos/DescuentoRecargo")
        If dereNodes Is Nothing Then dereNodes = xmlDoc.SelectNodes("//DescuentoRecargo")
        If dereNodes IsNot Nothing Then
            For Each n As XmlNode In dereNodes
                Dim dr As New DescuentoRecargoDto()
                dr.NumeroLinea = ParseNodeInt(n, "NumeroLinea")
                dr.TipoAjuste = GetNodeValue(n, "TipoAjuste")
                dr.IndicadorNorma1007 = GetNodeValue(n, "IndicadorNorma1007")
                dr.Descripcion = GetNodeValue(n, "DescripcionDescuentoRecargo")
                If String.IsNullOrWhiteSpace(dr.Descripcion) Then dr.Descripcion = GetNodeValue(n, "Descripcion")
                dr.TipoValor = GetNodeValue(n, "TipoValor")
                dr.ValorPorcentajeOMonto = DN(n, "ValorDescuentoRecargo")
                If Not dr.ValorPorcentajeOMonto.HasValue Then dr.ValorPorcentajeOMonto = DN(n, "ValorPorcentajeOMonto")
                dr.MontoDescuentoRecargo = DN(n, "MontoDescuentoRecargo")
                dr.MontoOtraMoneda = DN(n, "MontoDescuentoRecargoOtraMoneda")
                dr.IndicadorFacturacion = GetNodeValue(n, "IndicadorFacturacionDescuentoRecargo")
                If Not String.IsNullOrWhiteSpace(dr.TipoAjuste) OrElse dr.MontoDescuentoRecargo.HasValue Then
                    result.DescuentosRecargos.Add(dr)
                End If
            Next
        End If

        Return result
    End Function

    Private Shared Function ParseItem(itemNode As XmlNode) As ItemLuganisDto
        Dim item As New ItemLuganisDto()
        item.NumeroLinea = ParseNodeInt(itemNode, "NumeroLinea")
        item.TipoCodigo = GetNodeValue(itemNode, "TipoCodigo")
        item.CodigoItem = GetNodeValue(itemNode, "CodigoItem")
        item.IndicadorFacturacion = GetNodeValue(itemNode, "IndicadorFacturacion")
        ' En algunos XML el nodo se llama IndicadorAgenteRetencionoPercepcion (con "o").
        item.IndicadorAgenteRetencionPercepcion = GetNodeValue(itemNode, "IndicadorAgenteRetencionoPercepcion")
        If String.IsNullOrWhiteSpace(item.IndicadorAgenteRetencionPercepcion) Then
            item.IndicadorAgenteRetencionPercepcion = GetNodeValue(itemNode, "IndicadorAgenteRetencionPercepcion")
        End If
        ' Log para depurar el valor leído desde el XML (especialmente para tipo 41)
        If item.NumeroLinea.HasValue Then
            Helper.RegistrarLogCliente($"[LUGANIS XML] Item #{item.NumeroLinea.Value} IndicadorAgenteRetencion(o)Percepcion='{item.IndicadorAgenteRetencionPercepcion}'")
        Else
            Helper.RegistrarLogCliente($"[LUGANIS XML] Item sin NumeroLinea IndicadorAgenteRetencion(o)Percepcion='{item.IndicadorAgenteRetencionPercepcion}'")
        End If
        item.MontoITBISRetenido = DN(itemNode, "MontoITBISRetenido")
        item.MontoISRRetenido = DN(itemNode, "MontoISRRetenido")
        item.NombreItem = GetNodeValue(itemNode, "NombreItem")
        item.IndicadorBienoServicio = GetNodeValue(itemNode, "IndicadorBienoServicio")
        item.Descripcion = GetNodeValue(itemNode, "DescripcionItem")
        If String.IsNullOrWhiteSpace(item.Descripcion) Then item.Descripcion = GetNodeValue(itemNode, "Descripcion")
        item.Cantidad = DN(itemNode, "CantidadItem")
        item.UnidadMedida = GetNodeValue(itemNode, "UnidadMedida")
        item.CantidadReferencia = DN(itemNode, "CantidadReferencia")
        item.UnidadReferencia = GetNodeValue(itemNode, "UnidadReferencia")
        item.Subcantidad = GetNodeValue(itemNode, "Subcantidad")
        item.CodigoSubcantidad = GetNodeValue(itemNode, "CodigoSubcantidad")
        item.GradosAlcohol = DN(itemNode, "GradosAlcohol")
        item.PrecioUnitarioReferencia = DN(itemNode, "PrecioUnitarioReferencia")
        item.FechaElaboracion = GetNodeValue(itemNode, "FechaElaboracion")
        item.FechaVencimientoItem = GetNodeValue(itemNode, "FechaVencimientoItem")
        item.PesoNetoKilogramo = DN(itemNode, "PesoNetoKilogramo")
        item.PesoNetoMineria = DN(itemNode, "PesoNetoMineria")
        item.TipoAfiliacion = GetNodeValue(itemNode, "TipoAfiliacion")
        item.Liquidacion = GetNodeValue(itemNode, "Liquidacion")
        item.PrecioUnitario = DN(itemNode, "PrecioUnitarioItem")
        item.DescuentoMonto = DN(itemNode, "DescuentoMonto")
        item.TipoSubDescuento = GetNodeValue(itemNode, "TipoSubDescuento")
        item.SubDescuentoPorcentaje = DN(itemNode, "SubDescuentoPorcentaje")
        item.MontoSubDescuento = DN(itemNode, "MontoSubDescuento")
        item.RecargoMonto = DN(itemNode, "RecargoMonto")
        item.TipoSubRecargo = GetNodeValue(itemNode, "TipoSubRecargo")
        item.SubRecargoPorcentaje = DN(itemNode, "SubRecargoPorcentaje")
        item.MontoSubRecargo = DN(itemNode, "MontoSubRecargo")
        item.TipoImpuesto = GetNodeValue(itemNode, "TipoImpuesto")
        item.PrecioOtraMoneda = DN(itemNode, "PrecioOtraMoneda")
        item.DescuentoOtraMoneda = DN(itemNode, "DescuentoOtraMoneda")
        item.RecargoOtraMoneda = DN(itemNode, "RecargoOtraMoneda")
        item.MontoItemOtraMoneda = DN(itemNode, "MontoItemOtraMoneda")
        item.MontoLinea = DN(itemNode, "MontoItem")
        If item.NumeroLinea.HasValue Then
            Helper.RegistrarLogCliente($"[LUGANIS XML] Item #{item.NumeroLinea.Value} parse OK Nombre='{item.NombreItem}' Monto={If(item.MontoLinea.HasValue, item.MontoLinea.Value.ToString(CultureInfo.InvariantCulture), "")}")
        End If
        Return item
    End Function

    Private Shared Function V(doc As XmlDocument, xpath As String) As String
        If doc Is Nothing OrElse String.IsNullOrWhiteSpace(xpath) Then Return String.Empty
        Try
            Dim n As XmlNode = doc.SelectSingleNode(xpath)
            If n Is Nothing OrElse String.IsNullOrWhiteSpace(n.InnerText) Then Return String.Empty
            Return n.InnerText.Trim()
        Catch
            Return String.Empty
        End Try
    End Function

    Private Shared Function GetNodeValue(parent As XmlNode, localName As String) As String
        If parent Is Nothing OrElse String.IsNullOrWhiteSpace(localName) Then Return String.Empty
        Try
            Dim child As XmlNode = parent.SelectSingleNode(localName)
            If child Is Nothing OrElse String.IsNullOrWhiteSpace(child.InnerText) Then Return String.Empty
            Return child.InnerText.Trim()
        Catch
            Return String.Empty
        End Try
    End Function

    Private Shared Function DN(parent As XmlNode, localName As String) As Decimal?
        Dim s As String = GetNodeValue(parent, localName)
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Dim d As Decimal
        If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, d) Then Return d
        Return Nothing
    End Function

    Private Shared Function DN(doc As XmlDocument, xpath As String) As Decimal?
        Dim s As String = V(doc, xpath)
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Dim d As Decimal
        If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, d) Then Return d
        Return Nothing
    End Function

    Private Shared Function ParseNodeInt(parent As XmlNode, localName As String) As Integer?
        Dim s As String = GetNodeValue(parent, localName)
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Dim i As Integer
        If Integer.TryParse(s, i) Then Return i
        Return Nothing
    End Function
End Class
