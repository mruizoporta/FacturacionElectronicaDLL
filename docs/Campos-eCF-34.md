## Archivo TXT – Tipo e-CF 34

Nota de Crédito Electrónica (Tipo e‑CF 34).  
Resumen de campos para el archivo TXT según la pestaña **“Campos e‑CF 34”** de la matriz de LUGANIS.  
La referencia oficial sigue siendo el documento original.

### Códigos de obligatoriedad

- **1**: Dato obligatorio  
- **2**: Dato condicional  
- **3**: Dato opcional  

---

### SECCIÓN: IDENTIFICACIÓN DEL DOCUMENTO (`IDOC`)

**Especificación LUGANIS (oficial): la sección IDOC para tipo 34 solo conlleva 11 campos.** No se incluyen TerminoPago, TipoCuentaPago, NumeroCuentaPago ni BancoPago en el TXT.

**Tabla 2 – Indicador_Nota_Crédito:** solo valores `0` o `1` (0 = fecha eCF modificado ≤ 30 días; 1 = > 30 días).

**Tabla 5 – Tipo_Pago:** solo valores `1` (Contado), `2` (Crédito), `3` (Gratuito).

| Posición | Campo                         | Largo máx | Tipo     | Oblig. | Tabla / Notas                    |
|---------|--------------------------------|-----------|----------|--------|----------------------------------|
| 1       | `<TipoeCF>`                    | 2         | NUM      | 1      | Tabla 1. Tipo_eCF                |
| 2       | `<eNCF>`                       | 13        | ALFA NUM | 1      |                                  |
| 3       | `<IndicadorNotaCredito>`       | 1         | NUM      | 1      | Tabla 2. Solo 0 o 1              |
| 4       | `<IndicadorEnvioDiferido>`     | 1         | NUM      | 2      |                                  |
| 5       | `<IndicadorMontoGravado>`      | 1         | NUM      | 2      | Tabla 3. Indicador_Monto_Gravado |
| 6       | `<TipoIngresos>`               | 2         | NUM      | 1      | Tabla 4. Tipo_Ingresos           |
| 7       | `<TipoPago>`                   | 1         | NUM      | 1      | Tabla 5. Solo 1, 2 o 3           |
| 8       | `<FechaLimitePago>`            | 10        | ALFA NUM | 2      |                                  |
| 9       | `<FechaDesde>`                 | 10        | ALFA NUM | 3      |                                  |
| 10      | `<FechaHasta>`                 | 10        | ALFA NUM | 3      |                                  |
| 11      | `<FechaEmision>`               | 10        | ALFA NUM | 1      |                                  |

---

### SECCIÓN: DATOS DEL EMISOR (`EMIS`)

| Posición | Campo                    | Largo máx | Tipo     | Oblig. | Tabla / Notas                   |
|---------|---------------------------|-----------|----------|--------|---------------------------------|
| 1       | `<RNCEmisor>`             | 9 u 11    | NUM      | 1      |                                 |
| 2       | `<RazonSocialEmisor>`     | 150       | ALFA NUM | 1      |                                 |
| 3       | `<NombreComercial>`       | 150       | ALFA NUM | 3      |                                 |
| 4       | `<Sucursal>`              | 20        | ALFA NUM | 3      |                                 |
| 5       | `<DireccionEmisor>`       | 100       | ALFA NUM | 1      |                                 |
| 6       | `<Municipio>`             | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 7       | `<Provincia>`             | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 8       | `<TelefonoEmisor>`        | 12        | ALFA NUM | 3      |                                 |
| 9       | `<CorreoEmisor>`          | 80        | ALFA NUM | 3      |                                 |
| 10      | `<WebSite>`               | 50        | ALFA NUM | 3      |                                 |
| 11      | `<ActividadEconomica>`    | 100       | ALFA NUM | 3      |                                 |
| 12      | `<CodigoVendedor>`        | 60        | ALFA NUM | 3      |                                 |
| 13      | `<NumeroFacturaInterna>`  | 20        | ALFA NUM | 3      |                                 |
| 14      | `<NumeroPedidoInterno>`   | 20        | NUM      | 3      |                                 |
| 15      | `<ZonaVenta>`             | 20        | ALFA NUM | 3      |                                 |
| 16      | `<RutaVenta>`             | 20        | ALFA NUM | 3      |                                 |
| 17      | `<InformacionAdicionalEmisor>` | 250  | ALFA NUM | 3      |                                 |

---

### SECCIÓN: DATOS DEL RECEPTOR / COMPRADOR (`COMP`)

| Posición | Campo                          | Largo máx | Tipo     | Oblig. | Tabla / Notas                   |
|---------|---------------------------------|-----------|----------|--------|---------------------------------|
| 1       | `<RNCComprador>`                | 9 u 11    | NUM      | 2      |                                 |
| 2       | `<IdentificadorExtranjero>`     | 20        | ALFA NUM | 2      |                                 |
| 3       | `<RazonSocialComprador>`        | 150       | ALFA NUM | 2      |                                 |
| 4       | `<ContactoComprador>`           | 80        | ALFA NUM | 3      |                                 |
| 5       | `<CorreoComprador>`             | 80        | ALFA NUM | 3      |                                 |
| 6       | `<DireccionComprador>`          | 100       | ALFA NUM | 3      |                                 |
| 7       | `<MunicipioComprador>`          | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 8       | `<ProvinciaComprador>`          | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 9       | `<FechaEntrega>`                | 10        | ALFA NUM | 3      |                                 |
| 10      | `<ContactoEntrega>`             | 80        | ALFA NUM | 3      |                                 |
| 11      | `<DireccionEntrega>`            | 100       | ALFA NUM | 3      |                                 |
| 12      | `<TelefonoAdicional>`           | 12        | ALFA NUM | 3      |                                 |
| 13      | `<FechaOrdenCompra>`            | 10        | ALFA NUM | 3      |                                 |
| 14      | `<NumeroOrdenCompra>`           | 20        | ALFA NUM | 3      |                                 |
| 15      | `<CodigoInternoComprador>`      | 20        | ALFA NUM | 3      |                                 |
| 16      | `<ResponsablePago>`             | 20        | ALFA     | 3      |                                 |
| 17      | `<Informacionadicionalcomprador>` | 150     | ALFA NUM | 3      |                                 |

---

### SECCIÓN: INFORMACIONES ADICIONALES (`INFA`)

| Posición | Campo             | Largo máx | Tipo     | Oblig. | Tabla / Notas          |
|---------|--------------------|-----------|----------|--------|------------------------|
| 1       | `<FechaEmbarque>` | 10        | ALFA NUM | 3      |                        |
| 2       | `<NumeroEmbarque>`| 25        | ALFA NUM | 3      |                        |
| 3       | `<NumeroContenedor>` | 100     | ALFA NUM | 3      |                        |
| 4       | `<NumeroReferencia>` | 20      | NUM      | 3      |                        |
| 5       | `<PesoBruto>`     | 18        | NUM      | 3      | Tabla 9. Unidad_Medida |
| 6       | `<PesoNeto>`      | 18        | NUM      | 3      | Tabla 9. Unidad_Medida |
| 7       | `<UnidadPesoBruto>` | 2       | NUM      | 3      | Tabla 9. Unidad_Medida |
| 8       | `<UnidadPesoNeto>` | 2        | NUM      | 3      | Tabla 9. Unidad_Medida |
| 9       | `<CantidadBulto>` | 18        | NUM      | 3      | Tabla 9. Unidad_Medida |
| 10      | `<UnidadBulto>`   | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |
| 11      | `<VolumenBulto>`  | 18        | NUM      | 3      | Tabla 9. Unidad_Medida |
| 12      | `<UnidadVolumen>` | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |

---

### SECCIÓN: INFORMACIONES DEL TRANSPORTE (`TRAN`)

| Posición | Campo                | Largo máx | Tipo     | Oblig. | Notas |
|---------|-----------------------|-----------|----------|--------|-------|
| 1       | `<Conductor>`         | 20        | ALFA NUM | 3      |       |
| 2       | `<DocumentoTransporte>` | 20      | NUM      | 3      |       |
| 3       | `<Ficha>`             | 20        | NUM      | 3      |       |
| 4       | `<Placa>`             | 7         | ALFA NUM | 3      |       |
| 5       | `<RutaTransporte>`    | 20        | ALFA NUM | 3      |       |
| 6       | `<ZonaTransporte>`    | 20        | ALFA NUM | 3      |       |
| 7       | `<NumeroAlbaran>`     | 20        | ALFA NUM | 3      |       |

---

### SECCIÓN: INFORMACIONES SOBRE OTRAS MONEDAS (`OTMN`)

| Posición | Campo        | Largo máx | Tipo | Oblig. | Tabla / Notas        |
|---------|--------------|-----------|------|--------|----------------------|
| 1       | `<TipoMoneda>` | 3       | ALFA | 2      | Tabla 12. Tipo_Moneda|
| 2       | `<TipoCambio>` | 7       | NUM  | 2      |                      |

---

### SECCIÓN: DETALLE DE LOS BIENES O SERVICIOS FACTURADOS (`ITEM`)

| Posición | Campo                              | Largo máx | Tipo     | Oblig. | Tabla / Notas                    |
|---------|-------------------------------------|-----------|----------|--------|----------------------------------|
| 1       | `<NumeroLinea>`                    | 5         | NUM      | 1      |                                  |
| 2       | `<TipoCodigo\|CodigoItem>`         | 14        | ALFA NUM | 3      |                                  |
| 3       | `<IndicadorFacturacion>`           | 1         | NUM      | 1      | Tabla 13. Indicador_Facturación  |
| 4       | `<IndicadorAgenteRetencionoPercepcion>` | 1    | NUM      | 2      |                                  |
| 5       | `<MontoITBISRetenido>`             | 18        | NUM      | 2      |                                  |
| 6       | `<MontoISRRetenido>`               | 18        | NUM      | 2      |                                  |
| 7       | `<NombreItem>`                     | 80        | ALFA NUM | 1      |                                  |
| 8       | `<IndicadorBienoServicio>`         | 1         | NUM      | 1      | Tabla 14. Indicador_Bien_Servicio|
| 9       | `<DescripcionItem>`                | 1000      | ALFA NUM | 3      |                                  |
| 10      | `<CantidadItem>`                   | 18        | NUM      | 1      |                                  |
| 11      | `<UnidadMedida>`                   | 2         | NUM      | 3      | Tabla 9. Unidad_Medida           |
| 12      | `<CantidadReferencia>`             | 18        | NUM      | 2      | Tabla 9. Unidad_Medida           |
| 13      | `<UnidadReferencia>`               | 2         | NUM      | 2      | Tabla 9. Unidad_Medida           |
| 14      | `<Subcantidad\|CodigoSubcantidad>` | 19        | NUM      | 2      |                                  |
| 15      | `<GradosAlcohol>`                  | 2         | NUM      | 2      |                                  |
| 16      | `<PrecioUnitarioReferencia>`       | 20        | NUM      | 2      |                                  |
| 17      | `<FechaElaboracion>`               | 10        | ALFA NUM | 3      |                                  |
| 18      | `<FechaVencimientoItem>`           | 10        | ALFA NUM | 3      |                                  |
| 19      | `<PesoNetoKilogramo>`              | 19        | NUM      | 2      |                                  |
| 20      | `<PesoNetoMineria>`                | 19        | NUM      | 2      |                                  |
| 21      | `<TipoAfiliacion>`                 | 1         | NUM      | 2      | Tabla 15. Tipo_Afiliación        |
| 22      | `<Liquidacion>`                    | 1         | NUM      | 2      | Tabla 16. Tipo_Liquidación       |
| 23      | `<PrecioUnitarioItem>`             | 20        | NUM      | 1      |                                  |
| 24      | `<DescuentoMonto>`                 | 18        | NUM      | 2      |                                  |
| 25      | `<TipoSubDescuento\|SubDescuentoPorcentaje\|MontoSubDescuento>` | 18 | ALFA | 2 |                                  |
| 26      | `<RecargoMonto>`                   | 18        | NUM      | 2      |                                  |
| 27      | `<TipoSubRecargo\|SubRecargoPorcentaje\|MontoSubRecargo>` | 18 | ALFA | 2 |                                  |
| 28      | `<TipoImpuesto>`                   | 2         | NUM      | 2      | Tabla 10. Tipos_Impuestos_Adicionales |
| 29      | `<PrecioOtraMoneda>`               | 20        | NUM      | 3      |                                  |
| 30      | `<DescuentoOtraMoneda>`            | 18        | NUM      | 3      |                                  |
| 31      | `<RecargoOtraMoneda>`              | 18        | NUM      | 3      |                                  |
| 32      | `<MontoItemOtraMoneda>`            | 18        | NUM      | 3      |                                  |
| 33      | `<MontoItem>`                      | 18        | NUM      | 1      |                                  |

---

### SECCIÓN: FORMAS DE PAGO (`FPAG`)

| Posición | Campo        | Largo máx | Tipo | Oblig. | Tabla / Notas          |
|---------|--------------|-----------|------|--------|------------------------|
| 1       | `<FormaPago>` | 2        | NUM  | 3      | Tabla 6. Formas_Pago   |
| 2       | `<MontoPago>` | 18       | NUM  | 2      |                        |

---

### SECCIÓN: DESCUENTOS Y RECARGOS (`DERE`)

| Posición | Campo                            | Largo máx | Tipo | Oblig. | Tabla / Notas                                   |
|---------|-----------------------------------|-----------|------|--------|-------------------------------------------------|
| 1       | `<NumeroLinea>`                  | 2         | NUM  | 2      |                                                 |
| 2       | `<TipoAjuste>`                   | 1         | ALFA | 2      |                                                 |
| 3       | `<IndicadorNorma1007>`           | 1         | NUM  | 3      |                                                 |
| 4       | `<DescripcionDescuentoRecargo>`  | 45        | ALFA | 3      |                                                 |
| 5       | `<TipoValor>`                    | 1         | ALFA | 2      |                                                 |
| 6       | `<ValorDescuentoRecargo>`        | 5         | NUM  | 2      |                                                 |
| 7       | `<MontoDescuentoRecargo>`        | 18        | NUM  | 2      |                                                 |
| 8       | `<MontoDescuentoRecargoOtraMoneda>` | 18     | NUM  | 3      |                                                 |
| 9       | `<IndicadorFacturacionDescuentoRecargo>` | 1 | NUM | 2 | Tabla 17. Indicador_facturación_Descuento_Recargo |

---

### SECCIÓN: INFORMACIÓN DE REFERENCIA (`INFR`)

| Posición | Campo                | Largo máx | Tipo     | Oblig. | Tabla / Notas                |
|---------|-----------------------|-----------|----------|--------|------------------------------|
| 1       | `<NCFModificado>`     | 19 / 11 / 13 | ALFA NUM | 1   |                              |
| 2       | `<RNCOtroContribuyente>` | 9 u 11 | NUM      | 2      |                              |
| 3       | `<FechaNCFModificado>`| 10        | ALFA NUM | 1      |                              |
| 4       | `<CodigoModificacion>`| 1         | NUM      | 1      | Tabla 18. Código_Modificación|
| 5       | `<RazonModificacion>` | 90        | ALFA     | 3      |                              |

---

### Notas para implementación

- Esta hoja será la referencia para:
  - `LuganisXmlReader.Parse` para **tipo 34** (nota de crédito).
  - `LuganisTxtBuilder.BuildTxtTipo34`.
- La sección `INFR` es clave para vincular la nota de crédito con el e‑CF original.

