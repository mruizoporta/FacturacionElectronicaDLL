## Archivo TXT – Tipo e-CF 32

Factura de Consumo Electrónica (Tipo e‑CF 32).  
Este archivo resume los campos del TXT según la pestaña **“Campos e‑CF 32”** de la matriz de LUGANIS.  
La fuente de verdad sigue siendo el documento original; esto es una guía de trabajo para el código.

### Códigos de obligatoriedad

- **1**: Dato obligatorio  
- **2**: Dato condicional  
- **3**: Dato opcional  

---

### SECCIÓN: IDENTIFICACIÓN DEL DOCUMENTO (`IDOC`)

| Posición | Campo                         | Largo máx | Tipo     | Oblig. | Tabla / Notas                    |
|---------|--------------------------------|-----------|----------|--------|----------------------------------|
| 1       | `<TipoeCF>`                    | 2         | NUM      | 1      | Tabla 1. Tipo_eCF                |
| 2       | `<eNCF>`                       | 13        | ALFA NUM | 1      |                                  |
| 3       | `<IndicadorEnvioDiferido>`     | 1         | NUM      | 2      |                                  |
| 4       | `<IndicadorMontoGravado>`      | 1         | NUM      | 2      | Tabla 3. Indicador_Monto_Gravado |
| 5       | `<TipoIngresos>`               | 2         | NUM      | 1      | Tabla 4. Tipo_Ingresos           |
| 6       | `<TipoPago>`                   | 1         | NUM      | 1      | Tabla 5. Tipo_Pago               |
| 7       | `<FechaLimitePago>`            | 10        | ALFA NUM | 2      |                                  |
| 8       | `<TerminoPago>`                | 15        | ALFA NUM | 3      |                                  |
| 9       | `<TipoCuentaPago>`             | 2         | ALFA     | 3      | Tabla 7. Tipo_Cuenta_Pago        |
| 10      | `<NumeroCuentaPago>`           | 28        | ALFA NUM | 3      |                                  |
| 11      | `<BancoPago>`                  | 75        | ALFA NUM | 3      |                                  |
| 12      | `<FechaDesde>`                 | 10        | ALFA NUM | 3      |                                  |
| 13      | `<FechaHasta>`                 | 10        | ALFA NUM | 3      |                                  |
| 14      | `<FechaEmision>`               | 10        | ALFA NUM | 1      |                                  |

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
| 14      | `<NumeroPedidoInterno>`   | 20        | ALFA NUM | 3      |                                 |
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

> Mismas estructuras que en tipo 31 (según las capturas).  
> Se listan para tener la referencia rápida.

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

> Igual estructura general que para tipo 31, con los campos destacados en las capturas:

| Posición | Campo                              | Largo máx | Tipo     | Oblig. | Tabla / Notas                    |
|---------|-------------------------------------|-----------|----------|--------|----------------------------------|
| 1       | `<NumeroLinea>`                    | 5         | NUM      | 1      |                                  |
| 2       | `<TipoCodigo\|CodigoItem>`         | 14        | ALFA NUM | 3      |                                  |
| 3       | `<IndicadorFacturacion>`           | 1         | NUM      | 1      | Tabla 13. Indicador_Facturación  |
| 4       | `<NombreItem>`                     | 80        | ALFA NUM | 1      |                                  |
| 5       | `<IndicadorBienoServicio>`         | 1         | NUM      | 1      | Tabla 14. Indicador_Bien_Servicio|
| 6       | `<DescripcionItem>`                | 1000      | ALFA NUM | 3      |                                  |
| 7       | `<CantidadItem>`                   | 18        | NUM      | 1      |                                  |
| 8       | `<UnidadMedida>`                   | 2         | NUM      | 3      | Tabla 9. Unidad_Medida           |
| 9       | `<CantidadReferencia>`             | 18        | NUM      | 2      | Tabla 9. Unidad_Medida           |
| 10      | `<UnidadReferencia>`               | 2         | NUM      | 2      | Tabla 9. Unidad_Medida           |
| 11      | `<Subcantidad\|CodigoSubcantidad>` | 19        | NUM      | 2      |                                  |
| 12      | `<GradosAlcohol>`                  | 2         | NUM      | 2      |                                  |
| 13      | `<PrecioUnitarioReferencia>`       | 20        | NUM      | 2      |                                  |
| 14      | `<FechaElaboracion>`               | 10        | ALFA NUM | 3      |                                  |
| 15      | `<FechaVencimientoItem>`           | 10        | ALFA NUM | 3      |                                  |
| 16      | `<PesoNetoKilogramo>`              | 19        | NUM      | 2      |                                  |
| 17      | `<PesoNetoMineria>`                | 19        | NUM      | 2      |                                  |
| 18      | `<TipoAfiliacion>`                 | 1         | NUM      | 2      | Tabla 15. Tipo_Afiliación        |
| 19      | `<Liquidacion>`                    | 2         | NUM      | 2      | Tabla 16. Tipo_Liquidación       |
| 20      | `<PrecioUnitarioItem>`             | 20        | NUM      | 1      |                                  |
| 21      | `<DescuentoMonto>`                 | 18        | NUM      | 2      |                                  |
| 22      | `<TipoSubDescuento\|SubDescuentoPorcentaje\|MontoSubDescuento>` | 18 | ALFA | 2 |                                  |
| 23      | `<RecargoMonto>`                   | 18        | NUM      | 2      |                                  |
| 24      | `<TipoSubRecargo\|SubRecargoPorcentaje\|MontoSubRecargo>` | 18 | ALFA | 2 |                                  |
| 25      | `<TipoImpuesto>`                   | 2         | NUM      | 2      | Tabla 10. Tipos_Impuestos_Adicionales |
| 26      | `<PrecioOtraMoneda>`               | 20        | NUM      | 3      |                                  |
| 27      | `<DescuentoOtraMoneda>`            | 18        | NUM      | 3      |                                  |
| 28      | `<RecargoOtraMoneda>`              | 18        | NUM      | 3      |                                  |
| 29      | `<MontoItemOtraMoneda>`            | 18        | NUM      | 3      |                                  |
| 30      | `<MontoItem>`                      | 18        | NUM      | 1      |                                  |

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

### Notas para implementación

- Esta hoja sirve como referencia directa para implementar:
  - `LuganisXmlReader.Parse` para **tipo 32**.
  - `LuganisTxtBuilder.BuildTxtTipo32`.
- Algunos campos compuestos (`TipoCodigo\|CodigoItem`, `TipoSubDescuento\|...`) requerirán reglas adicionales que se documentarán con `TODO` en el código donde aplique.

