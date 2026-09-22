Imports System
Imports System.Globalization
Imports System.Text

' Responsabilidad: construir la trama TXT que LUGANIS espera,
' a partir del modelo neutral DocumentoLuganisDto.
' La estructura del TXT depende del tipo de e-CF.
' Formato: pipe-separado, una línea por sección, secciones vacías excluidas.

Friend Class LuganisTxtBuilder

    Private Shared ReadOnly Inv As CultureInfo = CultureInfo.InvariantCulture

    Friend Shared Function BuildTxt(documento As DocumentoLuganisDto) As String
        If documento Is Nothing Then Throw New ArgumentNullException(NameOf(documento))
        If String.IsNullOrWhiteSpace(documento.TipoECF) Then
            Throw New ArgumentException("TipoECF es obligatorio para construir el TXT de LUGANIS.", NameOf(documento))
        End If

        Select Case documento.TipoECF.Trim()
            Case "31" : Return BuildTxtTipo31(documento)
            Case "32" : Return BuildTxtTipo32(documento)
            Case "33" : Return BuildTxtTipo33(documento)
            Case "34" : Return BuildTxtTipo34(documento)
            Case "41" : Return BuildTxtTipo41(documento)
            Case "43" : Return BuildTxtTipo43(documento)
            Case "44" : Return BuildTxtTipo44(documento)
            Case "45" : Return BuildTxtTipo45(documento)
            Case "46" : Return BuildTxtTipo46(documento)
            Case "47" : Return BuildTxtTipo47(documento)
            Case Else : Throw New NotSupportedException($"Tipo de e-CF no soportado para LUGANIS: {documento.TipoECF}")
        End Select
    End Function

    Private Shared Function Lin(ParamArray vals() As String) As String
        If vals Is Nothing OrElse vals.Length = 0 Then Return String.Empty
        Dim sb As New StringBuilder()
        For i As Integer = 0 To vals.Length - 1
            If i > 0 Then sb.Append("|"c)
            sb.Append(If(vals(i), String.Empty))
        Next
        Return sb.ToString()
    End Function

    Private Shared Function D2(v As Decimal?) As String
        If Not v.HasValue Then Return String.Empty
        Return v.Value.ToString("F2", Inv)
    End Function

    Private Shared Function BuildTxtTipo31(documento As DocumentoLuganisDto) As String
        Dim sb As New StringBuilder()

        ' IDOC
        sb.AppendLine(Lin("IDOC",
            documento.TipoECF,
            documento.ENcf,
            documento.FechaVencimientoSecuencia,
            documento.IndicadorEnvioDiferido,
            documento.IndicadorMontoGravado,
            documento.TipoIngresos,
            documento.TipoPago,
            documento.FechaLimitePago,
            documento.TerminoPago,
            documento.TipoCuentaPago,
            documento.NumeroCuentaPago,
            documento.BancoPago,
            documento.FechaDesde,
            documento.FechaHasta,
            documento.FechaEmision))

        ' EMIS
        Dim telef As String = TelefonosEmisor(documento)
        sb.AppendLine(Lin("EMIS",
            documento.RncEmisor,
            documento.RazonSocialEmisor,
            documento.NombreComercial,
            documento.Sucursal,
            documento.DireccionEmisor,
            documento.Municipio,
            documento.Provincia,
            telef,
            documento.CorreoEmisor,
            documento.WebSite,
            documento.ActividadEconomica,
            documento.CodigoVendedor,
            documento.NumeroFacturaInterna,
            documento.NumeroPedidoInterno,
            documento.ZonaVenta,
            documento.RutaVenta,
            documento.InformacionAdicionalEmisor))

        ' COMP
        sb.AppendLine(Lin("COMP",
            documento.RncComprador,
            documento.RazonSocialComprador,
            documento.ContactoComprador,
            documento.CorreoComprador,
            documento.DireccionComprador,
            documento.MunicipioComprador,
            documento.ProvinciaComprador,
            documento.FechaEntrega,
            documento.ContactoEntrega,
            documento.DireccionEntrega,
            documento.TelefonoAdicional,
            documento.FechaOrdenCompra,
            documento.NumeroOrdenCompra,
            documento.CodigoInternoComprador,
            documento.ResponsablePago,
            documento.InformacionAdicionalComprador))

        ' OTMN (solo si hay otras monedas)
        If Not String.IsNullOrWhiteSpace(documento.TipoMoneda) OrElse Not String.IsNullOrWhiteSpace(documento.TipoCambio) Then
            sb.AppendLine(Lin("OTMN", documento.TipoMoneda, documento.TipoCambio))
        End If

        ' ITEM
        For Each it In documento.Items
            sb.AppendLine(ItemLine31(it))
        Next

        ' FPAG
        For Each p In documento.Pagos
            sb.AppendLine(Lin("FPAG", p.FormaPago, D2(p.MontoPago)))
        Next

        ' DERE
        For Each dr In documento.DescuentosRecargos
            sb.AppendLine(Lin("DERE",
                If(dr.NumeroLinea.HasValue, dr.NumeroLinea.Value.ToString(), String.Empty),
                dr.TipoAjuste,
                If(dr.IndicadorNorma1007, String.Empty),
                dr.Descripcion,
                dr.TipoValor,
                D2(dr.ValorPorcentajeOMonto),
                D2(dr.MontoDescuentoRecargo),
                D2(dr.MontoOtraMoneda),
                dr.IndicadorFacturacion))
        Next

        Return sb.ToString().TrimEnd(ChrW(13), ChrW(10))
    End Function

    Private Shared Function TelefonosEmisor(documento As DocumentoLuganisDto) As String
        If documento.TelefonosEmisor Is Nothing OrElse documento.TelefonosEmisor.Count = 0 Then Return String.Empty
        Return "[" & String.Join(";", documento.TelefonosEmisor) & "]"
    End Function

    Private Shared Function ItemLine31(itm As ItemLuganisDto) As String
        Dim tipoCodigoItem As String = TipoCodigoYItem(itm)
        Dim subDescuento As String = FormatItemSubDescuento(itm)
        Dim subRecargo As String = FormatItemSubRecargo(itm)
        Return Lin("ITEM",
            If(itm.NumeroLinea.HasValue, itm.NumeroLinea.Value.ToString(), String.Empty),
            tipoCodigoItem,
            itm.IndicadorFacturacion,
            itm.IndicadorAgenteRetencionPercepcion,
            D2(itm.MontoITBISRetenido),
            D2(itm.MontoISRRetenido),
            itm.NombreItem,
            itm.IndicadorBienoServicio,
            itm.Descripcion,
            D2(itm.Cantidad),
            itm.UnidadMedida,
            D2(itm.CantidadReferencia),
            itm.UnidadReferencia,
            Subcantidad(itm),
            D2(itm.GradosAlcohol),
            D2(itm.PrecioUnitarioReferencia),
            itm.FechaElaboracion,
            itm.FechaVencimientoItem,
            D2(itm.PrecioUnitario),
            D2(itm.DescuentoMonto),
            subDescuento,
            D2(itm.RecargoMonto),
            subRecargo,
            itm.TipoImpuesto,
            D2(itm.PrecioOtraMoneda),
            D2(itm.DescuentoOtraMoneda),
            D2(itm.RecargoOtraMoneda),
            D2(itm.MontoItemOtraMoneda),
            D2(itm.MontoLinea))
    End Function

    Private Shared Function TipoCodigoYItem(it As ItemLuganisDto) As String
        If String.IsNullOrWhiteSpace(it.TipoCodigo) AndAlso String.IsNullOrWhiteSpace(it.CodigoItem) Then Return String.Empty
        If String.IsNullOrWhiteSpace(it.CodigoItem) Then Return it.TipoCodigo
        If String.IsNullOrWhiteSpace(it.TipoCodigo) Then Return it.CodigoItem
        Return "[" & it.TipoCodigo & "|" & it.CodigoItem & "]"
    End Function

    Private Shared Function Subcantidad(it As ItemLuganisDto) As String
        If String.IsNullOrWhiteSpace(it.Subcantidad) AndAlso String.IsNullOrWhiteSpace(it.CodigoSubcantidad) Then Return String.Empty
        If String.IsNullOrWhiteSpace(it.CodigoSubcantidad) Then Return it.Subcantidad
        If String.IsNullOrWhiteSpace(it.Subcantidad) Then Return it.CodigoSubcantidad
        Return "[" & it.Subcantidad & "|" & it.CodigoSubcantidad & "]"
    End Function

    Private Shared Function FormatItemSubDescuento(it As ItemLuganisDto) As String
        If Not it.MontoSubDescuento.HasValue AndAlso Not it.SubDescuentoPorcentaje.HasValue Then Return String.Empty
        Dim p As String = D2(it.SubDescuentoPorcentaje)
        Dim m As String = D2(it.MontoSubDescuento)
        If String.IsNullOrEmpty(it.TipoSubDescuento) AndAlso String.IsNullOrEmpty(p) AndAlso String.IsNullOrEmpty(m) Then Return String.Empty
        Return "[" & If(it.TipoSubDescuento, String.Empty) & "|" & p & "|" & m & "]"
    End Function

    Private Shared Function FormatItemSubRecargo(it As ItemLuganisDto) As String
        If Not it.MontoSubRecargo.HasValue AndAlso Not it.SubRecargoPorcentaje.HasValue Then Return String.Empty
        Dim p As String = D2(it.SubRecargoPorcentaje)
        Dim m As String = D2(it.MontoSubRecargo)
        If String.IsNullOrEmpty(it.TipoSubRecargo) AndAlso String.IsNullOrEmpty(p) AndAlso String.IsNullOrEmpty(m) Then Return String.Empty
        Return "[" & If(it.TipoSubRecargo, String.Empty) & "|" & p & "|" & m & "]"
    End Function

    ''' <summary>
    ''' Tipo 32 – Factura de Consumo.
    ''' Estructura según Campos-eCF-32.md:
    ''' - IDOC sin FechaVencimientoSecuencia (empieza en IndicadorEnvioDiferido).
    ''' - COMP con IdentificadorExtranjero.
    ''' - ITEM con 30 columnas (incluye PesoNetoKilogramo, PesoNetoMineria, TipoAfiliacion, Liquidacion).
    ''' </summary>
    Private Shared Function BuildTxtTipo32(documento As DocumentoLuganisDto) As String
        Dim sb As New StringBuilder()

        ' IDOC tipo 32 (14 campos)
        sb.AppendLine(Lin("IDOC",
            documento.TipoECF,
            documento.ENcf,
            documento.IndicadorEnvioDiferido,
            documento.IndicadorMontoGravado,
            documento.TipoIngresos,
            documento.TipoPago,
            documento.FechaLimitePago,
            documento.TerminoPago,
            documento.TipoCuentaPago,
            documento.NumeroCuentaPago,
            documento.BancoPago,
            documento.FechaDesde,
            documento.FechaHasta,
            documento.FechaEmision))

        ' EMIS (igual que tipo 31)
        Dim telef As String = TelefonosEmisor(documento)
        sb.AppendLine(Lin("EMIS",
            documento.RncEmisor,
            documento.RazonSocialEmisor,
            documento.NombreComercial,
            documento.Sucursal,
            documento.DireccionEmisor,
            documento.Municipio,
            documento.Provincia,
            telef,
            documento.CorreoEmisor,
            documento.WebSite,
            documento.ActividadEconomica,
            documento.CodigoVendedor,
            documento.NumeroFacturaInterna,
            documento.NumeroPedidoInterno,
            documento.ZonaVenta,
            documento.RutaVenta,
            documento.InformacionAdicionalEmisor))

        ' COMP tipo 32: incluye IdentificadorExtranjero
        sb.AppendLine(Lin("COMP",
            documento.RncComprador,
            If(documento.IdentificadorExtranjero, String.Empty),
            documento.RazonSocialComprador,
            documento.ContactoComprador,
            documento.CorreoComprador,
            documento.DireccionComprador,
            documento.MunicipioComprador,
            documento.ProvinciaComprador,
            documento.FechaEntrega,
            documento.ContactoEntrega,
            documento.DireccionEntrega,
            documento.TelefonoAdicional,
            documento.FechaOrdenCompra,
            documento.NumeroOrdenCompra,
            documento.CodigoInternoComprador,
            documento.ResponsablePago,
            documento.InformacionAdicionalComprador))

        ' OTMN (solo si hay otras monedas)
        If Not String.IsNullOrWhiteSpace(documento.TipoMoneda) OrElse Not String.IsNullOrWhiteSpace(documento.TipoCambio) Then
            sb.AppendLine(Lin("OTMN", documento.TipoMoneda, documento.TipoCambio))
        End If

        ' ITEM tipo 32: 30 columnas
        For Each it In documento.Items
            sb.AppendLine(ItemLine32(it))
        Next

        ' FPAG
        For Each p In documento.Pagos
            sb.AppendLine(Lin("FPAG", p.FormaPago, D2(p.MontoPago)))
        Next

        ' DERE (9 columnas según doc, IndicadorNorma1007 en pos 3)
        For Each dr In documento.DescuentosRecargos
            sb.AppendLine(Lin("DERE",
                If(dr.NumeroLinea.HasValue, dr.NumeroLinea.Value.ToString(), String.Empty),
                dr.TipoAjuste,
                If(dr.IndicadorNorma1007, String.Empty),
                dr.Descripcion,
                dr.TipoValor,
                D2(dr.ValorPorcentajeOMonto),
                D2(dr.MontoDescuentoRecargo),
                D2(dr.MontoOtraMoneda),
                dr.IndicadorFacturacion))
        Next

        Return sb.ToString().TrimEnd(ChrW(13), ChrW(10))
    End Function

    ''' <summary>
    ''' ITEM para tipo 32 – 30 columnas según Campos-eCF-32.md.
    ''' </summary>
    Private Shared Function ItemLine32(itm As ItemLuganisDto) As String
        Dim tipoCodigoItem As String = TipoCodigoYItem(itm)
        Dim subDescuento As String = FormatItemSubDescuento(itm)
        Dim subRecargo As String = FormatItemSubRecargo(itm)
        Return Lin("ITEM",
            If(itm.NumeroLinea.HasValue, itm.NumeroLinea.Value.ToString(), String.Empty),  ' 1 NumeroLinea
            tipoCodigoItem,                                                               ' 2 TipoCodigo|CodigoItem
            itm.IndicadorFacturacion,                                                    ' 3 IndicadorFacturacion
            itm.NombreItem,                                                              ' 4 NombreItem
            itm.IndicadorBienoServicio,                                                  ' 5 IndicadorBienoServicio
            itm.Descripcion,                                                             ' 6 DescripcionItem
            D2(itm.Cantidad),                                                            ' 7 CantidadItem
            itm.UnidadMedida,                                                            ' 8 UnidadMedida
            D2(itm.CantidadReferencia),                                                  ' 9 CantidadReferencia
            itm.UnidadReferencia,                                                        ' 10 UnidadReferencia
            Subcantidad(itm),                                                            ' 11 Subcantidad|CodigoSubcantidad
            D2(itm.GradosAlcohol),                                                       ' 12 GradosAlcohol
            D2(itm.PrecioUnitarioReferencia),                                            ' 13 PrecioUnitarioReferencia
            itm.FechaElaboracion,                                                        ' 14 FechaElaboracion
            itm.FechaVencimientoItem,                                                    ' 15 FechaVencimientoItem
            D2(itm.PesoNetoKilogramo),                                                   ' 16 PesoNetoKilogramo
            D2(itm.PesoNetoMineria),                                                     ' 17 PesoNetoMineria
            If(itm.TipoAfiliacion, String.Empty),                                        ' 18 TipoAfiliacion
            If(itm.Liquidacion, String.Empty),                                           ' 19 Liquidacion
            D2(itm.PrecioUnitario),                                                      ' 20 PrecioUnitarioItem
            D2(itm.DescuentoMonto),                                                      ' 21 DescuentoMonto
            subDescuento,                                                                ' 22 TipoSubDescuento|SubDescuentoPorcentaje|MontoSubDescuento
            D2(itm.RecargoMonto),                                                        ' 23 RecargoMonto
            subRecargo,                                                                  ' 24 TipoSubRecargo|SubRecargoPorcentaje|MontoSubRecargo
            itm.TipoImpuesto,                                                            ' 25 TipoImpuesto
            D2(itm.PrecioOtraMoneda),                                                    ' 26 PrecioOtraMoneda
            D2(itm.DescuentoOtraMoneda),                                                 ' 27 DescuentoOtraMoneda
            D2(itm.RecargoOtraMoneda),                                                   ' 28 RecargoOtraMoneda
            D2(itm.MontoItemOtraMoneda),                                                 ' 29 MontoItemOtraMoneda
            D2(itm.MontoLinea))                                                          ' 30 MontoItem
    End Function

    Private Shared Function BuildTxtTipo33(documento As DocumentoLuganisDto) As String
        Return BuildTxtTipo31(documento)
    End Function

    ''' <summary>Tipo 34: Nota de Crédito / Devolución. IDOC según LUGANIS: solo 11 campos (sin TerminoPago, TipoCuentaPago, NumeroCuentaPago, BancoPago).
    ''' IndicadorNotaCredito solo 0 o 1 (Tabla 2). TipoPago 1/2/3 (Tabla 5).</summary>
    Private Shared Function BuildTxtTipo34(documento As DocumentoLuganisDto) As String
        Dim sb As New StringBuilder()

        ' IndicadorNotaCredito: LUGANIS solo acepta 0 o 1 (Tabla 2). Cualquier otro valor (ej. "2") se normaliza a "0"
        Dim indNotaCredito As String = If(String.IsNullOrWhiteSpace(documento.IndicadorNotaCredito), "0", documento.IndicadorNotaCredito.Trim())
        If indNotaCredito <> "1" Then indNotaCredito = "0"
        ' TipoPago: LUGANIS Tabla 5 solo acepta 1=Contado, 2=Crédito, 3=Gratuito
        Dim tipoPago As String = If(String.IsNullOrWhiteSpace(documento.TipoPago), "1", documento.TipoPago.Trim())
        If tipoPago <> "2" AndAlso tipoPago <> "3" Then tipoPago = "1"

        ' IDOC tipo 34 – 11 campos según especificación LUGANIS (sin TerminoPago, TipoCuentaPago, NumeroCuentaPago, BancoPago)
        sb.AppendLine(Lin("IDOC",
            documento.TipoECF,
            documento.ENcf,
            indNotaCredito,
            documento.IndicadorEnvioDiferido,
            documento.IndicadorMontoGravado,
            documento.TipoIngresos,
            tipoPago,
            documento.FechaLimitePago,
            documento.FechaDesde,
            documento.FechaHasta,
            documento.FechaEmision))

        ' EMIS (igual que tipo 31)
        Dim telef As String = TelefonosEmisor(documento)
        sb.AppendLine(Lin("EMIS",
            documento.RncEmisor,
            documento.RazonSocialEmisor,
            documento.NombreComercial,
            documento.Sucursal,
            documento.DireccionEmisor,
            documento.Municipio,
            documento.Provincia,
            telef,
            documento.CorreoEmisor,
            documento.WebSite,
            documento.ActividadEconomica,
            documento.CodigoVendedor,
            documento.NumeroFacturaInterna,
            documento.NumeroPedidoInterno,
            documento.ZonaVenta,
            documento.RutaVenta,
            documento.InformacionAdicionalEmisor))

        ' COMP tipo 34: IdentificadorExtranjero entre RNC y RazonSocial (17 columnas)
        sb.AppendLine(Lin("COMP",
            documento.RncComprador,
            If(documento.IdentificadorExtranjero, String.Empty),
            documento.RazonSocialComprador,
            documento.ContactoComprador,
            documento.CorreoComprador,
            documento.DireccionComprador,
            documento.MunicipioComprador,
            documento.ProvinciaComprador,
            documento.FechaEntrega,
            documento.ContactoEntrega,
            documento.DireccionEntrega,
            documento.TelefonoAdicional,
            documento.FechaOrdenCompra,
            documento.NumeroOrdenCompra,
            documento.CodigoInternoComprador,
            documento.ResponsablePago,
            documento.InformacionAdicionalComprador))

        ' OTMN
        If Not String.IsNullOrWhiteSpace(documento.TipoMoneda) OrElse Not String.IsNullOrWhiteSpace(documento.TipoCambio) Then
            sb.AppendLine(Lin("OTMN", documento.TipoMoneda, documento.TipoCambio))
        End If

        ' ITEM tipo 34: 33 columnas (4 más que tipo 31: PesoNetoKilogramo, PesoNetoMineria, TipoAfiliacion, Liquidacion)
        For Each it In documento.Items
            sb.AppendLine(ItemLine34(it))
        Next

        ' FPAG
        For Each p In documento.Pagos
            sb.AppendLine(Lin("FPAG", p.FormaPago, D2(p.MontoPago)))
        Next

        ' DERE tipo 34: 9 columnas según Campos-eCF-34 (IndicadorNorma1007 en pos 3)
        For Each dr In documento.DescuentosRecargos
            sb.AppendLine(Lin("DERE",
                If(dr.NumeroLinea.HasValue, dr.NumeroLinea.Value.ToString(), String.Empty),
                dr.TipoAjuste,
                If(dr.IndicadorNorma1007, String.Empty),
                dr.Descripcion,
                dr.TipoValor,
                D2(dr.ValorPorcentajeOMonto),
                D2(dr.MontoDescuentoRecargo),
                D2(dr.MontoOtraMoneda),
                dr.IndicadorFacturacion))
        Next

        ' INFR (obligatorio tipo 34: comprobante original al que se hace la devolución)
        sb.AppendLine(Lin("INFR",
            If(String.IsNullOrWhiteSpace(documento.NCFModificado), String.Empty, documento.NCFModificado),
            If(documento.RNCOtroContribuyente, String.Empty),
            If(String.IsNullOrWhiteSpace(documento.FechaNCFModificado), String.Empty, documento.FechaNCFModificado),
            If(String.IsNullOrWhiteSpace(documento.CodigoModificacion), String.Empty, documento.CodigoModificacion),
            If(documento.RazonModificacion, String.Empty)))

        Return sb.ToString().TrimEnd(ChrW(13), ChrW(10))
    End Function

    ''' <summary>ITEM línea para tipo 34: 33 columnas (incluye PesoNetoKilogramo, PesoNetoMineria, TipoAfiliacion, Liquidacion).</summary>
    Private Shared Function ItemLine34(itm As ItemLuganisDto) As String
        Dim tipoCodigoItem As String = TipoCodigoYItem(itm)
        Dim subDescuento As String = FormatItemSubDescuento(itm)
        Dim subRecargo As String = FormatItemSubRecargo(itm)
        Return Lin("ITEM",
            If(itm.NumeroLinea.HasValue, itm.NumeroLinea.Value.ToString(), String.Empty),
            tipoCodigoItem,
            itm.IndicadorFacturacion,
            itm.IndicadorAgenteRetencionPercepcion,
            D2(itm.MontoITBISRetenido),
            D2(itm.MontoISRRetenido),
            itm.NombreItem,
            itm.IndicadorBienoServicio,
            itm.Descripcion,
            D2(itm.Cantidad),
            itm.UnidadMedida,
            D2(itm.CantidadReferencia),
            itm.UnidadReferencia,
            Subcantidad(itm),
            D2(itm.GradosAlcohol),
            D2(itm.PrecioUnitarioReferencia),
            itm.FechaElaboracion,
            itm.FechaVencimientoItem,
            D2(itm.PesoNetoKilogramo),
            D2(itm.PesoNetoMineria),
            If(itm.TipoAfiliacion, String.Empty),
            If(itm.Liquidacion, String.Empty),
            D2(itm.PrecioUnitario),
            D2(itm.DescuentoMonto),
            subDescuento,
            D2(itm.RecargoMonto),
            subRecargo,
            itm.TipoImpuesto,
            D2(itm.PrecioOtraMoneda),
            D2(itm.DescuentoOtraMoneda),
            D2(itm.RecargoOtraMoneda),
            D2(itm.MontoItemOtraMoneda),
            D2(itm.MontoLinea))
    End Function

    ''' <summary>
    ''' Tipo 41 – Comprobante de Compra Electrónico.
    ''' Estructura según Campos-eCF-41.md:
    ''' - IDOC: 11 campos (TipoeCF, eNCF, FechaVencimientoSecuencia, IndicadorMontoGravado, TipoPago, FechaLimitePago, TerminoPago, TipoCuentaPago, NumeroCuentaPago, BancoPago, FechaEmision).
    ''' - EMIS: proveedor (14 campos, sin ZonaVenta/RutaVenta).
    ''' - COMP: empresa compradora (10 campos).
    ''' - ITEM: 24 columnas.
    ''' - FPAG: 2 columnas.
    ''' - DERE: 8 columnas (sin IndicadorNorma1007).
    ''' </summary>
    Private Shared Function BuildTxtTipo41(documento As DocumentoLuganisDto) As String
        Dim sb As New StringBuilder()

        ' IDOC tipo 41 – 11 campos
        sb.AppendLine(Lin("IDOC",
            documento.TipoECF,                ' 1 TipoeCF
            documento.ENcf,                   ' 2 eNCF
            documento.FechaVencimientoSecuencia, ' 3 FechaVencimientoSecuencia
            documento.IndicadorMontoGravado,  ' 4 IndicadorMontoGravado
            documento.TipoPago,               ' 5 TipoPago
            documento.FechaLimitePago,        ' 6 FechaLimitePago
            documento.TerminoPago,            ' 7 TerminoPago
            documento.TipoCuentaPago,         ' 8 TipoCuentaPago
            documento.NumeroCuentaPago,       ' 9 NumeroCuentaPago
            documento.BancoPago,              '10 BancoPago
            documento.FechaEmision))          '11 FechaEmision

        ' EMIS – proveedor emisor (14 campos, sin ZonaVenta/RutaVenta)
        Dim telef As String = TelefonosEmisor(documento)
        sb.AppendLine(Lin("EMIS",
            documento.RncEmisor,
            documento.RazonSocialEmisor,
            documento.NombreComercial,
            documento.Sucursal,
            documento.DireccionEmisor,
            documento.Municipio,
            documento.Provincia,
            telef,
            documento.CorreoEmisor,
            documento.WebSite,
            documento.ActividadEconomica,
            documento.NumeroFacturaInterna,
            documento.NumeroPedidoInterno,
            documento.InformacionAdicionalEmisor))

        ' COMP – empresa compradora (10 campos)
        sb.AppendLine(Lin("COMP",
            documento.RncComprador,
            documento.RazonSocialComprador,
            documento.ContactoComprador,
            documento.CorreoComprador,
            documento.DireccionComprador,
            documento.MunicipioComprador,
            documento.ProvinciaComprador,
            documento.CodigoInternoComprador,
            documento.ResponsablePago,
            documento.InformacionAdicionalComprador))

        ' OTMN (si aplica)
        If Not String.IsNullOrWhiteSpace(documento.TipoMoneda) OrElse Not String.IsNullOrWhiteSpace(documento.TipoCambio) Then
            sb.AppendLine(Lin("OTMN", documento.TipoMoneda, documento.TipoCambio))
        End If

        ' ITEM – 24 columnas según Campos-eCF-41.md
        For Each it In documento.Items
            sb.AppendLine(ItemLine41(it))
        Next

        ' FPAG – 2 columnas
        For Each p In documento.Pagos
            sb.AppendLine(Lin("FPAG", p.FormaPago, D2(p.MontoPago)))
        Next

        ' DERE – 8 columnas (sin IndicadorNorma1007)
        For Each dr In documento.DescuentosRecargos
            sb.AppendLine(Lin("DERE",
                If(dr.NumeroLinea.HasValue, dr.NumeroLinea.Value.ToString(), String.Empty),
                dr.TipoAjuste,
                dr.Descripcion,
                dr.TipoValor,
                D2(dr.ValorPorcentajeOMonto),
                D2(dr.MontoDescuentoRecargo),
                D2(dr.MontoOtraMoneda),
                dr.IndicadorFacturacion))
        Next

        Return sb.ToString().TrimEnd(ChrW(13), ChrW(10))
    End Function

    ''' <summary>ITEM línea para tipo 41: 24 columnas según Campos-eCF-41.md.</summary>
    Private Shared Function ItemLine41(itm As ItemLuganisDto) As String
        Dim tipoCodigoItem As String = TipoCodigoYItem(itm)
        Dim subDescuento As String = FormatItemSubDescuento(itm)
        Dim subRecargo As String = FormatItemSubRecargo(itm)
        ' Ajuste temporal: si el indicador de agente de retención/percepción viene vacío, usar "2"
        Dim indRet As String = If(itm.IndicadorAgenteRetencionPercepcion, String.Empty)
        If String.IsNullOrWhiteSpace(indRet) Then indRet = "2"
        Return Lin("ITEM",
            If(itm.NumeroLinea.HasValue, itm.NumeroLinea.Value.ToString(), String.Empty),  ' 1 NumeroLinea
            tipoCodigoItem,                            ' 2 TipoCodigo|CodigoItem
            itm.IndicadorFacturacion,                  ' 3 IndicadorFacturacion
            indRet,                                    ' 4 IndicadorAgenteRetencionoPercepcion
            D2(itm.MontoITBISRetenido),                ' 5 MontoITBISRetenido
            D2(itm.MontoISRRetenido),                  ' 6 MontoISRRetenido
            itm.NombreItem,                            ' 7 NombreItem
            itm.IndicadorBienoServicio,                ' 8 IndicadorBienoServicio
            itm.Descripcion,                           ' 9 DescripcionItem
            D2(itm.Cantidad),                          '10 CantidadItem
            itm.UnidadMedida,                          '11 UnidadMedida
            itm.FechaElaboracion,                      '12 FechaElaboracion
            itm.FechaVencimientoItem,                  '13 FechaVencimientoItem
            D2(itm.PrecioUnitario),                    '14 PrecioUnitarioItem
            D2(itm.DescuentoMonto),                    '15 DescuentoMonto
            subDescuento,                              '16 TipoSubDescuento|SubDescuentoPorcentaje|MontoSubDescuento
            D2(itm.RecargoMonto),                      '17 RecargoMonto
            subRecargo,                                '18 TipoSubRecargo|SubRecargoPorcentaje|MontoSubRecargo
            D2(itm.PrecioOtraMoneda),                  '19 PrecioOtraMoneda
            D2(itm.DescuentoOtraMoneda),               '20 DescuentoOtraMoneda
            D2(itm.RecargoOtraMoneda),                 '21 RecargoOtraMoneda
            D2(itm.MontoItemOtraMoneda),               '22 MontoItemOtraMoneda
            D2(itm.MontoLinea))                        '23 MontoItem
    End Function

    Private Shared Function BuildTxtTipo43(documento As DocumentoLuganisDto) As String
        Return BuildTxtTipo31(documento)
    End Function

    Private Shared Function BuildTxtTipo44(documento As DocumentoLuganisDto) As String
        Return BuildTxtTipo31(documento)
    End Function

    ''' <summary>
    ''' Tipo 45 – Comprobante Gubernamental Electrónico.
    ''' Estructura según Campos-eCF-45.md:
    ''' - IDOC: igual que tipo 31 (15 campos con FechaDesde/FechaHasta).
    ''' - EMIS / COMP / INFA / TRAN / OTMN: mismas secciones base.
    ''' - ITEM: 26 columnas específicas.
    ''' - DERE: 9 columnas con IndicadorNorma1007 en posición 3.
    ''' </summary>
    Private Shared Function BuildTxtTipo45(documento As DocumentoLuganisDto) As String
        Dim sb As New StringBuilder()

        ' IDOC tipo 45 – 15 campos (mismo layout que 31)
        sb.AppendLine(Lin("IDOC",
            documento.TipoECF,
            documento.ENcf,
            documento.FechaVencimientoSecuencia,
            documento.IndicadorEnvioDiferido,
            documento.IndicadorMontoGravado,
            documento.TipoIngresos,
            documento.TipoPago,
            documento.FechaLimitePago,
            documento.TerminoPago,
            documento.TipoCuentaPago,
            documento.NumeroCuentaPago,
            documento.BancoPago,
            documento.FechaDesde,
            documento.FechaHasta,
            documento.FechaEmision))

        ' EMIS (igual que tipo 31)
        Dim telef As String = TelefonosEmisor(documento)
        sb.AppendLine(Lin("EMIS",
            documento.RncEmisor,
            documento.RazonSocialEmisor,
            documento.NombreComercial,
            documento.Sucursal,
            documento.DireccionEmisor,
            documento.Municipio,
            documento.Provincia,
            telef,
            documento.CorreoEmisor,
            documento.WebSite,
            documento.ActividadEconomica,
            documento.CodigoVendedor,
            documento.NumeroFacturaInterna,
            documento.NumeroPedidoInterno,
            documento.ZonaVenta,
            documento.RutaVenta,
            documento.InformacionAdicionalEmisor))

        ' COMP (igual que tipo 31)
        sb.AppendLine(Lin("COMP",
            documento.RncComprador,
            documento.RazonSocialComprador,
            documento.ContactoComprador,
            documento.CorreoComprador,
            documento.DireccionComprador,
            documento.MunicipioComprador,
            documento.ProvinciaComprador,
            documento.FechaEntrega,
            documento.ContactoEntrega,
            documento.DireccionEntrega,
            documento.TelefonoAdicional,
            documento.FechaOrdenCompra,
            documento.NumeroOrdenCompra,
            documento.CodigoInternoComprador,
            documento.ResponsablePago,
            documento.InformacionAdicionalComprador))

        ' INFA
        ' (la lectura desde XML ya llena estos campos en DocumentoLuganisDto si existen)
        ' De momento no generamos INFA explícito porque la mayoría de 45 no lo usan; se puede habilitar si lo necesitas.

        ' TRAN
        ' (similar a INFA, solo se incluiría si hay datos; omitido aquí para no romper tramas existentes)

        ' OTMN (solo si hay otras monedas)
        If Not String.IsNullOrWhiteSpace(documento.TipoMoneda) OrElse Not String.IsNullOrWhiteSpace(documento.TipoCambio) Then
            sb.AppendLine(Lin("OTMN", documento.TipoMoneda, documento.TipoCambio))
        End If

        ' ITEM tipo 45 – 26 columnas
        For Each it In documento.Items
            sb.AppendLine(ItemLine45(it))
        Next

        ' FPAG
        For Each p In documento.Pagos
            sb.AppendLine(Lin("FPAG", p.FormaPago, D2(p.MontoPago)))
        Next

        ' DERE tipo 45 – 9 columnas con IndicadorNorma1007
        For Each dr In documento.DescuentosRecargos
            sb.AppendLine(Lin("DERE",
                If(dr.NumeroLinea.HasValue, dr.NumeroLinea.Value.ToString(), String.Empty),
                dr.TipoAjuste,
                If(dr.IndicadorNorma1007, String.Empty),
                dr.Descripcion,
                dr.TipoValor,
                D2(dr.ValorPorcentajeOMonto),
                D2(dr.MontoDescuentoRecargo),
                D2(dr.MontoOtraMoneda),
                dr.IndicadorFacturacion))
        Next

        Return sb.ToString().TrimEnd(ChrW(13), ChrW(10))
    End Function

    ''' <summary>ITEM línea para tipo 45: 26 columnas según Campos-eCF-45.md.</summary>
    Private Shared Function ItemLine45(itm As ItemLuganisDto) As String
        Dim tipoCodigoItem As String = TipoCodigoYItem(itm)
        Dim subDescuento As String = FormatItemSubDescuento(itm)
        Dim subRecargo As String = FormatItemSubRecargo(itm)
        Return Lin("ITEM",
            If(itm.NumeroLinea.HasValue, itm.NumeroLinea.Value.ToString(), String.Empty),  ' 1 NumeroLinea
            tipoCodigoItem,                            ' 2 TipoCodigo|CodigoItem
            itm.IndicadorFacturacion,                  ' 3 IndicadorFacturacion
            itm.NombreItem,                            ' 4 NombreItem
            itm.IndicadorBienoServicio,                ' 5 IndicadorBienoServicio
            itm.Descripcion,                           ' 6 DescripcionItem
            D2(itm.Cantidad),                          ' 7 CantidadItem
            itm.UnidadMedida,                          ' 8 UnidadMedida
            D2(itm.CantidadReferencia),                ' 9 CantidadReferencia
            itm.UnidadReferencia,                      '10 UnidadReferencia
            Subcantidad(itm),                          '11 Subcantidad|CodigoSubcantidad
            D2(itm.GradosAlcohol),                     '12 GradosAlcohol
            D2(itm.PrecioUnitarioReferencia),          '13 PrecioUnitarioReferencia
            itm.FechaElaboracion,                      '14 FechaElaboracion
            itm.FechaVencimientoItem,                  '15 FechaVencimientoItem
            D2(itm.PrecioUnitario),                    '16 PrecioUnitarioItem
            D2(itm.DescuentoMonto),                    '17 DescuentoMonto
            subDescuento,                              '18 TipoSubDescuento|SubDescuentoPorcentaje|MontoSubDescuento
            D2(itm.RecargoMonto),                      '19 RecargoMonto
            subRecargo,                                '20 TipoSubRecargo|SubRecargoPorcentaje|MontoSubRecargo
            itm.TipoImpuesto,                          '21 TipoImpuesto
            D2(itm.PrecioOtraMoneda),                  '22 PrecioOtraMoneda
            D2(itm.DescuentoOtraMoneda),               '23 DescuentoOtraMoneda
            D2(itm.RecargoOtraMoneda),                 '24 RecargoOtraMoneda
            D2(itm.MontoItemOtraMoneda),               '25 MontoItemOtraMoneda
            D2(itm.MontoLinea))                        '26 MontoItem
    End Function

    Private Shared Function BuildTxtTipo46(documento As DocumentoLuganisDto) As String
        Return BuildTxtTipo31(documento)
    End Function

    Private Shared Function BuildTxtTipo47(documento As DocumentoLuganisDto) As String
        Dim sb As New StringBuilder()

        sb.AppendLine(Lin("IDOC",
            documento.TipoECF,
            documento.ENcf,
            documento.FechaVencimientoSecuencia,
            documento.TipoPago,
            documento.FechaLimitePago,
            documento.TerminoPago,
            documento.TipoCuentaPago,
            documento.NumeroCuentaPago,
            documento.BancoPago,
            documento.FechaDesde,
            documento.FechaHasta,
            documento.FechaEmision))

        Dim telef As String = TelefonosEmisor(documento)
        sb.AppendLine(Lin("EMIS",
            documento.RncEmisor,
            documento.RazonSocialEmisor,
            documento.NombreComercial,
            documento.Sucursal,
            documento.DireccionEmisor,
            documento.Municipio,
            documento.Provincia,
            telef,
            documento.CorreoEmisor,
            documento.WebSite,
            documento.ActividadEconomica,
            documento.NumeroFacturaInterna,
            documento.NumeroPedidoInterno,
            documento.InformacionAdicionalEmisor))

        sb.AppendLine(Lin("COMP",
            documento.IdentificadorExtranjero,
            documento.RazonSocialComprador))

        If Not String.IsNullOrWhiteSpace(documento.TipoMoneda) OrElse Not String.IsNullOrWhiteSpace(documento.TipoCambio) Then
            sb.AppendLine(Lin("OTMN", documento.TipoMoneda, documento.TipoCambio))
        End If

        For Each it In documento.Items
            sb.AppendLine(ItemLine47(it))
        Next

        For Each p In documento.Pagos
            sb.AppendLine(Lin("FPAG", p.FormaPago, D2(p.MontoPago)))
        Next

        Return sb.ToString().TrimEnd(ChrW(13), ChrW(10))
    End Function

    Private Shared Function ItemLine47(itm As ItemLuganisDto) As String
        Dim tipoCodigoItem As String = TipoCodigoYItem(itm)
        Return Lin("ITEM",
            If(itm.NumeroLinea.HasValue, itm.NumeroLinea.Value.ToString(), String.Empty),
            tipoCodigoItem,
            itm.IndicadorFacturacion,
            itm.IndicadorAgenteRetencionPercepcion,
            D2(itm.MontoISRRetenido),
            itm.NombreItem,
            itm.IndicadorBienoServicio,
            itm.Descripcion,
            D2(itm.Cantidad),
            itm.UnidadMedida,
            D2(itm.PrecioUnitario),
            D2(itm.PrecioOtraMoneda),
            D2(itm.DescuentoOtraMoneda),
            D2(itm.RecargoOtraMoneda),
            D2(itm.MontoItemOtraMoneda),
            D2(itm.MontoLinea))
    End Function
End Class

