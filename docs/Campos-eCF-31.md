## Archivo TXT – Tipo e-CF 31

Resumen estructurado de los campos para el archivo TXT de **Factura de Crédito Fiscal Electrónica (Tipo e-CF 31)** según la documentación de LUGANIS.  
Este archivo es solo una transcripción de apoyo para el desarrollo; la fuente de verdad sigue siendo el documento original.

### Códigos de obligatoriedad

- **1**: Dato obligatorio  
- **2**: Dato condicional  
- **3**: Dato opcional  

---

### SECCIÓN: IDENTIFICACIÓN DEL DOCUMENTO (`IDOC`)

| Posición | Campo                           | Largo | Tipo     | Oblig. | Tabla / Notas                        |
|---------|----------------------------------|-------|----------|--------|--------------------------------------|
| 1       | `<TipoeCF>`                      | 2     | NUM      | 1      | Tabla 1. Tipo_eCF                    |
| 2       | `<eNCF>`                         | 13    | ALFA NUM | 1      |                                      |
| 3       | `<FechaVencimientoSecuencia>`   | 10    | ALFA NUM | 1      |                                      |
| 4       | `<IndicadorEnvioDiferido>`      | 1     | NUM      | 2      | Tabla 3. Indicador_Monto_Gravado     |
| 5       | `<IndicadorMontoGravado>`       | 1     | NUM      | 2      | Tabla 3. Indicador_Monto_Gravado     |
| 6       | `<TipoIngresos>`                | 2     | NUM      | 1      | Tabla 4. Tipo_Ingresos               |
| 7       | `<TipoPago>`                    | 1     | NUM      | 1      | Tabla 5. Tipo_Pago                   |
| 8       | `<FechaLimitePago>`             | 10    | ALFA NUM | 2      |                                      |
| 9       | `<TerminoPago>`                 | 15    | ALFA NUM | 3      |                                      |
| 10      | `<TipoCuentaPago>`              | 2     | ALFA     | 3      | Tabla 7. Tipo_Cuenta_Pago            |
| 11      | `<NumeroCuentaPago>`            | 28    | ALFA NUM | 3      |                                      |
| 12      | `<BancoPago>`                   | 75    | ALFA NUM | 3      |                                      |
| 13      | `<FechaDesde>`                  | 10    | ALFA NUM | 3      |                                      |
| 14      | `<FechaHasta>`                  | 10    | ALFA NUM | 3      |                                      |
| 15      | `<FechaEmision>`                | 10    | ALFA NUM | 1      |                                      |

> **Nota**: La documentación original puede incluir más campos en esta sección; aquí se han priorizado los visibles en las capturas compartidas.

---

### SECCIÓN: DATOS DEL EMISOR (`EMIS`)

| Posición | Campo                    | Largo | Tipo     | Oblig. | Tabla / Notas                   |
|---------|---------------------------|-------|----------|--------|---------------------------------|
| 1       | `<RNCEmisor>`             | 9 u 11| NUM      | 1      |                                 |
| 2       | `<RazonSocialEmisor>`     | 150   | ALFA NUM | 1      |                                 |
| 3       | `<NombreComercial>`       | 150   | ALFA NUM | 3      |                                 |
| 4       | `<Sucursal>`              | 20    | ALFA NUM | 3      |                                 |
| 5       | `<DireccionEmisor>`       | 100   | ALFA NUM | 1      |                                 |
| 6       | `<Municipio>`             | 6     | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 7       | `<Provincia>`             | 6     | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 8       | `<TelefonoEmisor>`        | 12    | ALFA NUM | 3      |                                 |
| 9       | `<CorreoEmisor>`          | 80    | ALFA NUM | 3      |                                 |
| 10      | `<WebSite>`               | 50    | ALFA NUM | 3      |                                 |
| 11      | `<ActividadEconomica>`    | 100   | ALFA NUM | 3      |                                 |
| 12      | `<CodigoVendedor>`        | 60    | ALFA NUM | 3      |                                 |
| 13      | `<NumeroFacturaInterna>`  | 20    | ALFA NUM | 3      |                                 |
| 14      | `<NumeroPedidoInterno>`   | 20    | NUM      | 3      |                                 |
| 15      | `<ZonaVenta>`             | 20    | ALFA NUM | 3      |                                 |
| 16      | `<RutaVenta>`             | 20    | ALFA NUM | 3      |                                 |
| 17      | `<InformacionAdicionalEmisor>` | 250 | ALFA NUM | 3   |                                 |

---

### SECCIÓN: DATOS DEL RECEPTOR / COMPRADOR (`COMP`)

| Posición | Campo                         | Largo | Tipo     | Oblig. | Tabla / Notas                   |
|---------|--------------------------------|-------|----------|--------|---------------------------------|
| 1       | `<RNCComprador>`               | 9 u 11| NUM      | 1      |                                 |
| 2       | `<RazonSocialComprador>`       | 150   | ALFA NUM | 1      |                                 |
| 3       | `<ContactoComprador>`          | 80    | ALFA NUM | 3      |                                 |
| 4       | `<CorreoComprador>`            | 80    | ALFA NUM | 3      |                                 |
| 5       | `<DireccionComprador>`         | 100   | ALFA NUM | 3      |                                 |
| 6       | `<MunicipioComprador>`         | 6     | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 7       | `<ProvinciaComprador>`         | 6     | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 8       | `<FechaEntrega>`               | 10    | ALFA NUM | 3      |                                 |
| 9       | `<ContactoEntrega>`            | 80    | ALFA NUM | 3      |                                 |
| 10      | `<DireccionEntrega>`           | 100   | ALFA NUM | 3      |                                 |
| 11      | `<TelefonoAdicional>`          | 12    | ALFA NUM | 3      |                                 |
| 12      | `<FechaOrdenCompra>`           | 10    | ALFA NUM | 3      |                                 |
| 13      | `<NumeroOrdenCompra>`          | 20    | ALFA NUM | 3      |                                 |
| 14      | `<CodigoInternoComprador>`     | 20    | ALFA NUM | 3      |                                 |
| 15      | `<ResponsablePago>`            | 80    | ALFA NUM | 3      |                                 |
| 16      | `<Informacionadicionalcomprador>` | 150 | ALFA NUM | 3    |                                 |

---

### SECCIÓN: INFORMACIONES ADICIONALES (`INFA`)

| Posición | Campo                         | Largo | Tipo     | Oblig. | Tabla / Notas          |
|---------|--------------------------------|-------|----------|--------|------------------------|
| 1       | `<FechaEmbarque>`             | 10    | ALFA NUM | 3      |                        |
| 2       | `<NumeroEmbarque>`            | 25    | ALFA NUM | 3      |                        |
| 3       | `<NumeroContenedor>`          | 100   | ALFA NUM | 3      |                        |
| 4       | `<NumeroReferencia>`          | 20    | NUM      | 3      |                        |
| 5       | `<PesoBruto>`                 | 18    | NUM      | 3      | Tabla 9. Unidad_Medida |
| 6       | `<PesoNeto>`                  | 18    | NUM      | 3      | Tabla 9. Unidad_Medida |
| 7       | `<UnidadPesoBruto>`           | 2     | NUM      | 3      | Tabla 9. Unidad_Medida |
| 8       | `<UnidadPesoNeto>`            | 2     | NUM      | 3      | Tabla 9. Unidad_Medida |
| 9       | `<CantidadBulto>`             | 18    | NUM      | 3      | Tabla 9. Unidad_Medida |
| 10      | `<UnidadBulto>`               | 2     | NUM      | 3      | Tabla 9. Unidad_Medida |
| 11      | `<VolumenBulto>`              | 18    | NUM      | 3      | Tabla 9. Unidad_Medida |
| 12      | `<UnidadVolumen>`             | 2     | NUM      | 3      | Tabla 9. Unidad_Medida |

---

### SECCIÓN: INFORMACIONES DEL TRANSPORTE (`TRAN`)

| Posición | Campo                | Largo | Tipo     | Oblig. | Notas |
|---------|-----------------------|-------|----------|--------|-------|
| 1       | `<Conductor>`         | 20    | ALFA NUM | 3      |       |
| 2       | `<DocumentoTransporte>` | 20  | NUM      | 3      |       |
| 3       | `<Ficha>`             | 20    | NUM      | 3      |       |
| 4       | `<Placa>`             | 7     | ALFA NUM | 3      |       |
| 5       | `<RutaTransporte>`    | 20    | ALFA NUM | 3      |       |
| 6       | `<ZonaTransporte>`    | 20    | ALFA NUM | 3      |       |
| 7       | `<NumeroAlbaran>`     | 20    | ALFA NUM | 3      |       |

---

### SECCIÓN: INFORMACIONES SOBRE OTRAS MONEDAS (`OTMN`)

| Posición | Campo        | Largo | Tipo | Oblig. | Tabla / Notas        |
|---------|--------------|-------|------|--------|----------------------|
| 1       | `<TipoMoneda>` | 3   | ALFA | 2      | Tabla 12. Tipo_Moneda|
| 2       | `<TipoCambio>` | 7   | NUM  | 2      |                      |

---

### SECCIÓN: DETALLE DE LOS BIENES O SERVICIOS FACTURADOS (`ITEM`)

> Nota: Esta sección se repite por cada línea de detalle. A continuación se listan los campos principales visibles en la documentación.

| Posición | Campo                              | Largo | Tipo     | Oblig. | Tabla / Notas                    |
|---------|-------------------------------------|-------|----------|--------|----------------------------------|
| 1       | `<NumeroLinea>`                    | 5     | NUM      | 1      |                                  |
| 2       | `<TipoCodigo\|CodigoItem>`         | 14    | ALFA NUM | 3      |                                  |
| 3       | `<IndicadorFacturacion>`           | 1     | NUM      | 1      | Tabla 13. Indicador_Facturación  |
| 4       | `<IndicadorAgenteRetencionPercepcion>` | 1 | NUM | 2      |                                  |
| 5       | `<MontoITBISRetenido>`             | 18    | NUM      | 2      |                                  |
| 6       | `<MontoISRRetenido>`               | 18    | NUM      | 2      |                                  |
| 7       | `<NombreItem>`                     | 80    | ALFA NUM | 1      |                                  |
| 8       | `<IndicadorBienoServicio>`         | 1     | NUM      | 1      | Tabla 14. Indicador_Bien_Servicio|
| 9       | `<DescripcionItem>`                | 1000  | ALFA NUM | 3      |                                  |
| 10      | `<CantidadItem>`                   | 18    | NUM      | 1      |                                  |
| 11      | `<UnidadMedida>`                   | 2     | NUM      | 3      | Tabla 9. Unidad_Medida           |
| 12      | `<CantidadReferencia>`             | 18    | NUM      | 2      | Tabla 9. Unidad_Medida           |
| 13      | `<UnidadReferencia>`               | 2     | NUM      | 2      | Tabla 9. Unidad_Medida           |
| 14      | `<Subcantidad\|CodigoSubcantidad>` | 19    | NUM      | 2      |                                  |
| 15      | `<GradosAlcohol>`                  | 2     | NUM      | 2      | Tabla 9. Unidad_Medida           |
| 16      | `<PrecioUnitarioReferencia>`       | 20    | NUM      | 2      |                                  |
| 17      | `<FechaElaboracion>`               | 10    | ALFA NUM | 3      |                                  |
| 18      | `<FechaVencimientoItem>`           | 10    | ALFA NUM | 3      |                                  |
| 19      | `<PrecioUnitarioItem>`             | 20    | NUM      | 1      |                                  |
| 20      | `<DescuentoMonto>`                 | 18    | NUM      | 2      |                                  |
| 21      | `<TipoSubDescuento\|SubDescuentoPorcentaje\|MontoSubDescuento>` | 18 | ALFA | 2 |                                  |
| 22      | `<RecargoMonto>`                   | 18    | NUM      | 2      |                                  |
| 23      | `<TipoSubRecargo\|SubRecargoPorcentaje\|MontoSubRecargo>` | 18 | ALFA | 2 |                                  |
| 24      | `<TipoImpuesto>`                   | 2     | NUM      | 2      | Tabla 10. Tipos_Impuestos_Adicionales |
| 25      | `<PrecioOtraMoneda>`               | 20    | NUM      | 3      |                                  |
| 26      | `<DescuentoOtraMoneda>`            | 18    | NUM      | 3      |                                  |
| 27      | `<RecargoOtraMoneda>`              | 18    | NUM      | 3      |                                  |
| 28      | `<MontoItemOtraMoneda>`            | 18    | NUM      | 3      |                                  |
| 29      | `<MontoItem>`                      | 18    | NUM      | 1      |                                  |

---

### SECCIÓN: FORMAS DE PAGO (`FPAG`)

| Posición | Campo        | Largo | Tipo | Oblig. | Tabla / Notas          |
|---------|--------------|-------|------|--------|------------------------|
| 1       | `<FormaPago>` | 2    | NUM  | 3      | Tabla 6. Formas_Pago   |
| 2       | `<MontoPago>` | 18   | NUM  | 2      |                        |

---

### SECCIÓN: DESCUENTOS Y RECARGOS (`DERE`)

| Posición | Campo                            | Largo | Tipo | Oblig. | Tabla / Notas                                   |
|---------|-----------------------------------|-------|------|--------|-------------------------------------------------|
| 1       | `<NumeroLinea>`                  | 2     | NUM  | 2      |                                                 |
| 2       | `<TipoAjuste>`                   | 1     | ALFA | 2      |                                                 |
| 3       | `<IndicadorNorma1007>`           | 1     | NUM  | 3      |                                                 |
| 4       | `<DescripcionDescuentoRecargo>`  | 45    | ALFA | 3      |                                                 |
| 5       | `<TipoValor>`                    | 1     | ALFA | 2      |                                                 |
| 6       | `<ValorDescuentoRecargo>`        | 5     | NUM  | 2      |                                                 |
| 7       | `<MontoDescuentoRecargo>`        | 18    | NUM  | 2      |                                                 |
| 8       | `<MontoDescuentoRecargoOtraMoneda>` | 18 | NUM | 3      |                                                 |
| 9       | `<IndicadorFacturacionDescuentoRecargo>` | 1 | NUM | 2 | Tabla 17. Indicador_facturación_Descuento_Recargo |

---

### Notas finales

- Este `.md` sirve como guía rápida dentro del proyecto para implementar:
  - `LuganisXmlReader.Parse` (mapeo XML → `DocumentoLuganisDto`).
  - `LuganisTxtBuilder.BuildTxtTipo31` (construcción exacta del TXT tipo 31).
- Algunos campos pueden requerir confirmación cruzada con el documento original (por ejemplo, combinaciones como `<TipoCodigo\|CodigoItem>`); los dejaremos marcados en código con `TODO` cuando la implementación requiera reglas adicionales.

