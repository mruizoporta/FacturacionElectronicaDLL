## Archivo TXT – Tipo e-CF 46

Comprobante de Exportaciones Electrónico (Tipo e‑CF 46).  
Resumen de campos para el archivo TXT según la pestaña **"Campos e‑CF 46"** de la matriz de LUGANIS.  
La referencia oficial sigue siendo el documento original.

### Códigos de obligatoriedad

- **1**: Dato obligatorio  
- **2**: Dato condicional  
- **3**: Dato opcional  

---

### SECCIÓN: IDENTIFICACIÓN DEL DOCUMENTO (`IDOC`)

| Posición | Campo                        | Largo máx | Tipo     | Oblig. | Tabla / Notas                 |
|---------|------------------------------|-----------|----------|--------|-------------------------------|
| 1       | `<TipoeCF>`                  | 2         | NUM      | 1      | Tabla 1. Tipo_eCF             |
| 2       | `<eNCF>`                     | 13        | ALFA NUM | 1      |                               |
| 3       | `<FechaVencimientoSecuencia>`| 10        | ALFA NUM | 1      |                               |
| 4       | `<IndicadorEnvioDiferido>`   | 1         | NUM      | 2      |                               |
| 5       | `<TipoIngresos>`             | 2         | NUM      | 1      | Tabla 4. Tipo_Ingresos        |
| 6       | `<TipoPago>`                 | 1         | NUM      | 1      | Tabla 5. Tipo_Pago            |
| 7       | `<FechaLimitePago>`          | 10        | ALFA NUM | 2      |                               |
| 8       | `<TerminoPago>`              | 15        | ALFA NUM | 3      |                               |
| 9       | `<TipoCuentaPago>`           | 2         | ALFA     | 3      | Tabla 7. Tipo_Cuenta_Pago     |
| 10      | `<NumeroCuentaPago>`         | 28        | ALFA NUM | 3      |                               |
| 11      | `<BancoPago>`                | 75        | ALFA NUM | 3      |                               |
| 12      | `<FechaDesde>`               | 10        | ALFA NUM | 3      |                               |
| 13      | `<FechaHasta>`               | 10        | ALFA NUM | 3      |                               |
| 14      | `<FechaEmision>`             | 10        | ALFA NUM | 1      |                               |

---

### SECCIÓN: DATOS DEL EMISOR (`EMIS`)

| Posición | Campo                         | Largo máx | Tipo     | Oblig. | Tabla / Notas                    |
|---------|-------------------------------|-----------|----------|--------|----------------------------------|
| 1       | `<RNCEmisor>`                 | 9 u 11    | NUM      | 1      |                                  |
| 2       | `<RazonSocialEmisor>`         | 150       | ALFA NUM | 1      |                                  |
| 3       | `<NombreComercial>`           | 150       | ALFA NUM | 3      |                                  |
| 4       | `<Sucursal>`                  | 20        | ALFA NUM | 3      |                                  |
| 5       | `<DireccionEmisor>`           | 100       | ALFA NUM | 1      |                                  |
| 6       | `<Municipio>`                 | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios   |
| 7       | `<Provincia>`                 | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios   |
| 8       | `<TelefonoEmisor>`            | 12        | ALFA NUM | 3      |                                  |
| 9       | `<CorreoEmisor>`              | 80        | ALFA NUM | 3      |                                  |
| 10      | `<WebSite>`                   | 50        | ALFA NUM | 3      |                                  |
| 11      | `<ActividadEconomica>`        | 100       | ALFA NUM | 3      |                                  |
| 12      | `<CodigoVendedor>`            | 60        | ALFA NUM | 3      |                                  |
| 13      | `<NumeroFacturaInterna>`      | 20        | ALFA NUM | 3      |                                  |
| 14      | `<NumeroPedidoInterno>`       | 20        | NUM      | 3      |                                  |
| 15      | `<ZonaVenta>`                 | 20        | ALFA NUM | 3      |                                  |
| 16      | `<RutaVenta>`                 | 20        | ALFA NUM | 3      |                                  |
| 17      | `<InformacionAdicionalEmisor>`| 250       | ALFA NUM | 3      |                                  |

---

### SECCIÓN: DATOS DEL RECEPTOR / COMPRADOR (`COMP`)

| Posición | Campo                           | Largo máx | Tipo     | Oblig. | Tabla / Notas                   |
|---------|----------------------------------|-----------|----------|--------|---------------------------------|
| 1       | `<RNCComprador>`                 | 9 u 11    | NUM      | 2      |                                 |
| 2       | `<IdentificadorExtranjero>`      | 20        | ALFA NUM | 2      |                                 |
| 3       | `<RazonSocialComprador>`         | 150       | ALFA NUM | 1      |                                 |
| 4       | `<ContactoComprador>`            | 80        | ALFA NUM | 3      |                                 |
| 5       | `<CorreoComprador>`              | 80        | ALFA NUM | 3      |                                 |
| 6       | `<DireccionComprador>`           | 100       | ALFA NUM | 3      |                                 |
| 7       | `<MunicipioComprador>`           | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios  |
| 8       | `<ProvinciaComprador>`           | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios  |
| 9       | `<PaisComprador>`                | 60        | ALFA     | 3      | *(específico exportaciones)*    |
| 10      | `<FechaEntrega>`                 | 10        | ALFA NUM | 3      |                                 |
| 11      | `<ContactoEntrega>`              | 100       | ALFA NUM | 3      |                                 |
| 12      | `<DireccionEntrega>`             | 100       | ALFA NUM | 3      |                                 |
| 13      | `<TelefonoAdicional>`            | 12        | ALFA NUM | 3      |                                 |
| 14      | `<FechaOrdenCompra>`             | 10        | ALFA NUM | 3      |                                 |
| 15      | `<NumeroOrdenCompra>`            | 20        | ALFA NUM | 3      |                                 |
| 16      | `<CodigoInternoComprador>`       | 20        | ALFA NUM | 3      |                                 |
| 17      | `<ResponsablePago>`              | 20        | ALFA     | 3      |                                 |
| 18      | `<Informacionadicionalcomprador>`| 150       | ALFA NUM | 3      |                                 |

---

### SECCIÓN: INFORMACIONES ADICIONALES (`INFA`)

> Específica para exportaciones: puertos, FOB, CIF, régimen aduanero, etc.

| Posición | Campo                    | Largo máx | Tipo     | Oblig. | Tabla / Notas        |
|---------|---------------------------|-----------|----------|--------|----------------------|
| 1       | `<FechaEmbarque>`         | 10        | ALFA NUM | 3      |                      |
| 2       | `<NumeroEmbarque>`        | 25        | ALFA NUM | 3      |                      |
| 3       | `<NumeroContenedor>`      | 100       | ALFA NUM | 3      |                      |
| 4       | `<NumeroReferencia>`      | 20        | NUM      | 3      |                      |
| 5       | `<NombrePuertoEmbarque>`  | 40        | ALFA NUM | 3      |                      |
| 6       | `<CondicionesEntrega>`    | 3         | ALFA     | 3      |                      |
| 7       | `<TotalFob>`              | 18        | NUM      | 3      |                      |
| 8       | `<Seguro>`                | 18        | NUM      | 3      |                      |
| 9       | `<Flete>`                 | 18        | NUM      | 3      |                      |
| 10      | `<OtrosGastos>`           | 18        | NUM      | 3      |                      |
| 11      | `<TotalCif>`              | 18        | NUM      | 3      |                      |
| 12      | `<RegimenAduanero>`       | 35        | ALFA     | 3      |                      |
| 13      | `<NombrePuertoSalida>`    | 40        | ALFA NUM | 3      |                      |
| 14      | `<NombrePuertoDesembarque>`| 40       | ALFA NUM | 3      |                      |
| 15      | `<PesoBruto>`             | 18        | NUM      | 3      |                      |
| 16      | `<PesoNeto>`              | 18        | NUM      | 3      |                      |
| 17      | `<UnidadPesoBruto>`       | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |
| 18      | `<UnidadPesoNeto>`        | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |
| 19      | `<CantidadBulto>`         | 18        | NUM      | 3      |                      |
| 20      | `<UnidadBulto>`           | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |
| 21      | `<VolumenBulto>`          | 18        | NUM      | 3      |                      |
| 22      | `<UnidadVolumen>`         | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |

---

### SECCIÓN: INFORMACIONES DEL TRANSPORTE (`TRAN`)

> Específica para exportaciones: vía, países, compañía transportista, etc.

| Posición | Campo                                   | Largo máx | Tipo     | Oblig. | Tabla / Notas           |
|---------|------------------------------------------|-----------|----------|--------|-------------------------|
| 1       | `<ViaTransporte>`                        | 2         | NUM      | 3      | Tabla 11. Via_Transporte |
| 2       | `<PaisOrigen>`                           | 60        | ALFA     | 3      |                         |
| 3       | `<DireccionDestino>`                     | 100       | ALFA NUM | 3      |                         |
| 4       | `<PaisDestino>`                          | 60        | ALFA     | 3      |                         |
| 5       | `<RNCIdentificacionCompaniaTransportista>`| 20       | ALFA NUM | 3      |                         |
| 6       | `<NombreCompaniaTransportista>`          | 150       | ALFA NUM | 3      |                         |
| 7       | `<NumeroViaje>`                          | 20        | ALFA NUM | 3      |                         |
| 8       | `<Conductor>`                            | 20        | ALFA NUM | 3      |                         |
| 9       | `<DocumentoTransporte>`                  | 20        | NUM      | 3      |                         |
| 10      | `<Ficha>`                                | 10        | ALFA NUM | 3      |                         |
| 11      | `<Placa>`                                | 7         | ALFA NUM | 3      |                         |
| 12      | `<RutaTransporte>`                       | 20        | ALFA NUM | 3      |                         |
| 13      | `<ZonaTransporte>`                       | 20        | ALFA NUM | 3      |                         |
| 14      | `<NumeroAlbaran>`                        | 20        | ALFA NUM | 3      |                         |

---

### SECCIÓN: INFORMACIONES SOBRE OTRAS MONEDAS (`OTMN`)

| Posición | Campo        | Largo máx | Tipo | Oblig. | Tabla / Notas         |
|---------|--------------|-----------|------|--------|-----------------------|
| 1       | `<TipoMoneda>` | 3       | ALFA | 2      | Tabla 12. Tipo_Moneda |
| 2       | `<TipoCambio>` | 7       | NUM  | 2      |                       |

---

### SECCIÓN: DETALLE DE LOS BIENES O SERVICIOS FACTURADOS (`ITEM`)

> Incluye campos específicos de exportaciones: `PesoNetoKilogramo`, `PesoNetoMineria`, `TipoAfiliacion`, `Liquidacion`.

| Posición | Campo                                                      | Largo máx | Tipo     | Oblig. | Tabla / Notas                         |
|---------|-------------------------------------------------------------|-----------|----------|--------|---------------------------------------|
| 1       | `<NumeroLinea>`                                            | 5         | NUM      | 1      |                                       |
| 2       | `<TipoCodigo\|CodigoItem>`                                 | 14 / 35   | ALFA NUM | 3      |                                       |
| 3       | `<IndicadorFacturacion>`                                   | 1         | NUM      | 1      | Tabla 13. Indicador_Facturación       |
| 4       | `<NombreItem>`                                             | 80        | ALFA NUM | 1      |                                       |
| 5       | `<IndicadorBienoServicio>`                                 | 1         | NUM      | 1      | Tabla 14. Indicador_Bien_Servicio     |
| 6       | `<DescripcionItem>`                                        | 1000      | ALFA NUM | 3      |                                       |
| 7       | `<CantidadItem>`                                           | 18        | NUM      | 1      |                                       |
| 8       | `<UnidadMedida>`                                           | 2         | NUM      | 3      | Tabla 9. Unidad_Medida                |
| 9       | `<FechaElaboracion>`                                       | 10        | ALFA NUM | 3      |                                       |
| 10      | `<FechaVencimientoItem>`                                   | 10        | ALFA NUM | 3      |                                       |
| 11      | `<PesoNetoKilogramo>`                                      | 19        | NUM      | 2      | *(específico exportaciones)*          |
| 12      | `<PesoNetoMineria>`                                        | 19        | NUM      | 2      | *(específico exportaciones)*          |
| 13      | `<TipoAfiliacion>`                                         | 1         | NUM      | 2      | Tabla 15. Tipo_Afiliación             |
| 14      | `<Liquidacion>`                                            | 1         | NUM      | 2      | Tabla 16. Tipo_Liquidación            |
| 15      | `<PrecioUnitarioItem>`                                     | 20        | NUM      | 1      |                                       |
| 16      | `<DescuentoMonto>`                                         | 18        | NUM      | 2      |                                       |
| 17      | `<TipoSubDescuento\|SubDescuentoPorcentaje\|MontoSubDescuento>` | 1/5/18 | ALFA/NUM | 2  |                                       |
| 18      | `<RecargoMonto>`                                           | 18        | NUM      | 2      |                                       |
| 19      | `<TipoSubRecargo\|SubRecargoPorcentaje\|MontoSubRecargo>`  | 1/5/18    | ALFA/NUM | 2      |                                       |
| 20      | `<PrecioOtraMoneda>`                                       | 20        | NUM      | 2      |                                       |
| 21      | `<DescuentoOtraMoneda>`                                    | 18        | NUM      | 3      |                                       |
| 22      | `<RecargoOtraMoneda>`                                      | 18        | NUM      | 3      |                                       |
| 23      | `<MontoItemOtraMoneda>`                                    | 18        | NUM      | 2      |                                       |
| 24      | `<MontoItem>`                                              | 18        | NUM      | 1      |                                       |

---

### SECCIÓN: FORMAS DE PAGO (`FPAG`)

| Posición | Campo        | Largo máx | Tipo | Oblig. | Tabla / Notas        |
|---------|--------------|-----------|------|--------|----------------------|
| 1       | `<FormaPago>` | 2        | NUM  | 3      | Tabla 6. Formas_Pago |
| 2       | `<MontoPago>` | 18       | NUM  | 2      |                      |

---

### SECCIÓN: DESCUENTOS Y RECARGOS (`DERE`)

| Posición | Campo                               | Largo máx | Tipo | Oblig. | Tabla / Notas                                   |
|---------|--------------------------------------|-----------|------|--------|-------------------------------------------------|
| 1       | `<NumeroLinea>`                     | 2         | NUM  | 2      |                                                 |
| 2       | `<TipoAjuste>`                      | 1         | ALFA | 2      |                                                 |
| 3       | `<DescripcionDescuentoRecargo>`     | 45        | ALFA | 3      |                                                 |
| 4       | `<TipoValor>`                       | 1         | ALFA | 2      |                                                 |
| 5       | `<ValorDescuentoRecargo>`           | 5         | NUM  | 2      |                                                 |
| 6       | `<MontoDescuentoRecargo>`           | 18        | NUM  | 2      |                                                 |
| 7       | `<MontoDescuentoRecargoOtraMoneda>` | 18        | NUM  | 3      |                                                 |
| 8       | `<IndicadorFacturacionDescuentoRecargo>` | 1  | NUM  | 2      | Tabla 17. Indicador_facturación_Descuento_Recargo |

---

### Notas para implementación

- Esta hoja servirá como referencia para:
  - `LuganisXmlReader.Parse` para **tipo 46** (comprobante de exportaciones).
  - `LuganisTxtBuilder.BuildTxtTipo46`.
- El tipo 46 incluye secciones específicas de exportación:
  - **INFA:** puertos, condiciones de entrega, FOB/CIF, régimen aduanero.
  - **TRAN:** vía de transporte, países origen/destino, compañía transportista.
  - **COMP:** `PaisComprador`, `RNCComprador`/`IdentificadorExtranjero` condicionales.
  - **ITEM:** `PesoNetoKilogramo`, `PesoNetoMineria`, `TipoAfiliacion`, `Liquidacion`.

