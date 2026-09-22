## Archivo TXT – Tipo e-CF 45

Comprobante Gubernamental Electrónico (Tipo e‑CF 45).  
Resumen de campos para el archivo TXT según la pestaña **"Campos e‑CF 45"** de la matriz de LUGANIS.  
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
| 5       | `<IndicadorMontoGravado>`    | 1         | NUM      | 2      | Tabla 3. Indicador_Monto_Gravado |
| 6       | `<TipoIngresos>`             | 2         | NUM      | 1      | Tabla 4. Tipo_Ingresos        |
| 7       | `<TipoPago>`                 | 1         | NUM      | 1      | Tabla 5. Tipo_Pago            |
| 8       | `<FechaLimitePago>`          | 10        | ALFA NUM | 2      |                               |
| 9       | `<TerminoPago>`              | 15        | ALFA NUM | 3      |                               |
| 10      | `<TipoCuentaPago>`           | 2         | ALFA     | 3      | Tabla 7. Tipo_Cuenta_Pago     |
| 11      | `<NumeroCuentaPago>`         | 28        | ALFA NUM | 3      |                               |
| 12      | `<BancoPago>`                | 75        | ALFA NUM | 3      |                               |
| 13      | `<FechaDesde>`               | 10        | ALFA NUM | 3      |                               |
| 14      | `<FechaHasta>`               | 10        | ALFA NUM | 3      |                               |
| 15      | `<FechaEmision>`             | 10        | ALFA NUM | 1      |                               |

---

### SECCIÓN: DATOS DEL EMISOR (`EMIS`)

| Posición | Campo                         | Largo máx | Tipo     | Oblig. | Tabla / Notas                    |
|---------|-------------------------------|-----------|----------|--------|----------------------------------|
| 1       | `<RNCEmisor>`                 | 9 u 11    | NUM      | 1      |                                  |
| 2       | `<RazonSocialEmisor>`         | 150       | ALFA NUM | 1      |                                  |
| 3       | `<NombreComercial>`           | 150       | ALFA NUM | 3      |                                  |
| 4       | `<Sucursal>`                  | 20        | ALFA NUM | 3      |                                  |
| 5       | `<DireccionEmisor>`           | 100       | ALFA NUM | 1      |                                  |
| 6       | `<Municipio>`                 | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios  |
| 7       | `<Provincia>`                 | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios  |
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
| 1       | `<RNCComprador>`                 | 9 u 11    | NUM      | 1      |                                 |
| 2       | `<RazonSocialComprador>`         | 150       | ALFA NUM | 1      |                                 |
| 3       | `<ContactoComprador>`            | 80        | ALFA NUM | 3      |                                 |
| 4       | `<CorreoComprador>`              | 80        | ALFA NUM | 3      |                                 |
| 5       | `<DireccionComprador>`           | 100       | ALFA NUM | 3      |                                 |
| 6       | `<MunicipioComprador>`           | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios  |
| 7       | `<ProvinciaComprador>`           | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios  |
| 8       | `<FechaEntrega>`                 | 10        | ALFA NUM | 3      |                                 |
| 9       | `<ContactoEntrega>`              | 100       | ALFA NUM | 3      |                                 |
| 10      | `<DireccionEntrega>`             | 100       | ALFA NUM | 3      |                                 |
| 11      | `<TelefonoAdicional>`            | 12        | ALFA NUM | 3      |                                 |
| 12      | `<FechaOrdenCompra>`             | 10        | ALFA NUM | 3      |                                 |
| 13      | `<NumeroOrdenCompra>`            | 20        | ALFA NUM | 3      |                                 |
| 14      | `<CodigoInternoComprador>`       | 20        | ALFA NUM | 3      |                                 |
| 15      | `<ResponsablePago>`              | 20        | ALFA     | 3      |                                 |
| 16      | `<Informacionadicionalcomprador>`| 150       | ALFA NUM | 3      |                                 |

---

### SECCIÓN: INFORMACIONES ADICIONALES (`INFA`)

| Posición | Campo               | Largo máx | Tipo     | Oblig. | Tabla / Notas        |
|---------|----------------------|-----------|----------|--------|----------------------|
| 1       | `<FechaEmbarque>`    | 10        | ALFA NUM | 3      |                      |
| 2       | `<NumeroEmbarque>`   | 25        | ALFA NUM | 3      |                      |
| 3       | `<NumeroContenedor>` | 100       | ALFA NUM | 3      |                      |
| 4       | `<NumeroReferencia>` | 20        | NUM      | 3      |                      |
| 5       | `<PesoBruto>`        | 18        | NUM      | 3      |                      |
| 6       | `<PesoNeto>`         | 18        | NUM      | 3      |                      |
| 7       | `<UnidadPesoBruto>`  | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |
| 8       | `<UnidadPesoNeto>`   | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |
| 9       | `<CantidadBulto>`    | 18        | NUM      | 3      |                      |
| 10      | `<UnidadBulto>`      | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |
| 11      | `<VolumenBulto>`     | 18        | NUM      | 3      |                      |
| 12      | `<UnidadVolumen>`    | 2         | NUM      | 3      | Tabla 9. Unidad_Medida |

---

### SECCIÓN: INFORMACIONES DEL TRANSPORTE (`TRAN`)

| Posición | Campo                 | Largo máx | Tipo     | Oblig. | Tabla / Notas |
|---------|------------------------|-----------|----------|--------|---------------|
| 1       | `<Conductor>`          | 20        | ALFA NUM | 3      |               |
| 2       | `<DocumentoTransporte>`| 20        | NUM      | 3      |               |
| 3       | `<Ficha>`              | 10        | ALFA NUM | 3      |               |
| 4       | `<Placa>`              | 7         | ALFA NUM | 3      |               |
| 5       | `<RutaTransporte>`     | 20        | ALFA NUM | 3      |               |
| 6       | `<ZonaTransporte>`     | 20        | ALFA NUM | 3      |               |
| 7       | `<NumeroAlbaran>`      | 20        | ALFA NUM | 3      |               |

---

### SECCIÓN: INFORMACIONES SOBRE OTRAS MONEDAS (`OTMN`)

| Posición | Campo        | Largo máx | Tipo | Oblig. | Tabla / Notas         |
|---------|--------------|-----------|------|--------|-----------------------|
| 1       | `<TipoMoneda>` | 3       | ALFA | 2      | Tabla 12. Tipo_Moneda |
| 2       | `<TipoCambio>` | 7       | NUM  | 2      |                       |

---

### SECCIÓN: DETALLE DE LOS BIENES O SERVICIOS FACTURADOS (`ITEM`)

| Posición | Campo                                                      | Largo máx | Tipo     | Oblig. | Tabla / Notas                         |
|---------|-------------------------------------------------------------|-----------|----------|--------|---------------------------------------|
| 1       | `<NumeroLinea>`                                            | 5         | NUM      | 1      |                                       |
| 2       | `<TipoCodigo\|CodigoItem>`                                 | 14        | ALFA NUM | 3      |                                       |
| 3       | `<IndicadorFacturacion>`                                   | 1         | NUM      | 1      | Tabla 13. Indicador_Facturación       |
| 4       | `<NombreItem>`                                             | 80        | ALFA NUM | 1      |                                       |
| 5       | `<IndicadorBienoServicio>`                                 | 1         | NUM      | 1      | Tabla 14. Indicador_Bien_Servicio     |
| 6       | `<DescripcionItem>`                                        | 1000      | ALFA NUM | 3      |                                       |
| 7       | `<CantidadItem>`                                           | 18        | NUM      | 1      |                                       |
| 8       | `<UnidadMedida>`                                           | 2         | NUM      | 3      | Tabla 9. Unidad_Medida                |
| 9       | `<CantidadReferencia>`                                     | 18        | NUM      | 2      |                                       |
| 10      | `<UnidadReferencia>`                                       | 2         | NUM      | 2      | Tabla 9. Unidad_Medida                |
| 11      | `<Subcantidad\|CodigoSubcantidad>`                         | 19        | NUM      | 2      |                                       |
| 12      | `<GradosAlcohol>`                                          | 5         | NUM      | 2      |                                       |
| 13      | `<PrecioUnitarioReferencia>`                               | 18        | NUM      | 2      |                                       |
| 14      | `<FechaElaboracion>`                                       | 10        | ALFA NUM | 3      |                                       |
| 15      | `<FechaVencimientoItem>`                                   | 10        | ALFA NUM | 3      |                                       |
| 16      | `<PrecioUnitarioItem>`                                     | 20        | NUM      | 1      |                                       |
| 17      | `<DescuentoMonto>`                                         | 18        | NUM      | 2      |                                       |
| 18      | `<TipoSubDescuento\|SubDescuentoPorcentaje\|MontoSubDescuento>` | 1/5/18 | ALFA/NUM | 2      |                                       |
| 19      | `<RecargoMonto>`                                           | 18        | NUM      | 2      |                                       |
| 20      | `<TipoSubRecargo\|SubRecargoPorcentaje\|MontoSubRecargo>`  | 1/5/18    | ALFA/NUM | 2      |                                       |
| 21      | `<TipoImpuesto>`                                           | 3         | NUM      | 2      | Tabla 10. Tipos_Impuestos_Adicionales |
| 22      | `<PrecioOtraMoneda>`                                       | 20        | NUM      | 2      |                                       |
| 23      | `<DescuentoOtraMoneda>`                                    | 18        | NUM      | 3      |                                       |
| 24      | `<RecargoOtraMoneda>`                                      | 18        | NUM      | 3      |                                       |
| 25      | `<MontoItemOtraMoneda>`                                    | 18        | NUM      | 2      |                                       |
| 26      | `<MontoItem>`                                              | 18        | NUM      | 1      |                                       |

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
| 3       | `<IndicadorNorma1007>`              | 1         | NUM  | 3      |                                                 |
| 4       | `<DescripcionDescuentooRecargo>`    | 45        | ALFA | 3      |                                                 |
| 5       | `<TipoValor>`                       | 1         | ALFA | 2      |                                                 |
| 6       | `<ValorDescuentooRecargo>`          | 5         | NUM  | 2      |                                                 |
| 7       | `<MontoDescuentooRecargo>`          | 18        | NUM  | 2      |                                                 |
| 8       | `<MontoDescuentooRecargoOtraMoneda>`| 18        | NUM  | 3      |                                                 |
| 9       | `<IndicadorFacturacionDescuentooRecargo>` | 1  | NUM  | 2      | Tabla 17. Indicador_facturación_Descuento_Recargo |

---

### Notas para implementación

- Esta hoja servirá como referencia para:
  - `LuganisXmlReader.Parse` para **tipo 45** (comprobante gubernamental).
  - `LuganisTxtBuilder.BuildTxtTipo45`.
- El tipo 45 incluye campos específicos como `CantidadReferencia`, `UnidadReferencia`, `Subcantidad`, `GradosAlcohol`, `PrecioUnitarioReferencia` e `IndicadorNorma1007` (DERE).
- Cualquier diferencia futura se debe validar siempre contra el documento oficial actualizado de LUGANIS.
