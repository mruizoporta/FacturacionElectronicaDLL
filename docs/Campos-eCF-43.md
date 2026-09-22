## Archivo TXT – Tipo e-CF 43

Comprobante de Gasto Menor Electrónico (Tipo e‑CF 43).  
Resumen de campos para el archivo TXT según la pestaña **“Campos e‑CF 43”** de la matriz de LUGANIS.  
La referencia oficial sigue siendo el documento original.

### Códigos de obligatoriedad

- **1**: Dato obligatorio  
- **2**: Dato condicional  
- **3**: Dato opcional  

---

### SECCIÓN: IDENTIFICACIÓN DEL DOCUMENTO (`IDOC`)

| Posición | Campo                         | Largo máx | Tipo     | Oblig. | Tabla / Notas         |
|---------|--------------------------------|-----------|----------|--------|-----------------------|
| 1       | `<TipoeCF>`                    | 2         | NUM      | 1      | Tabla 1. Tipo_eCF     |
| 2       | `<eNCF>`                       | 13        | ALFA NUM | 1      |                       |
| 3       | `<FechaVencimientoSecuencia>`  | 10        | ALFA NUM | 1      |                       |
| 4       | `<TipoPago>`                   | 1         | NUM      | 3      | Tabla 5. Tipo_Pago    |
| 5       | `<FechaEmision>`               | 10        | ALFA NUM | 1      |                       |

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
| 12      | `<NumeroFacturaInterna>`  | 20        | ALFA NUM | 3      |                                 |
| 13      | `<NumeroPedidoInterno>`   | 20        | ALFA NUM | 3      |                                 |
| 14      | `<InformacionAdicionalEmisor>` | 250  | ALFA NUM | 3      |                                 |

---

### SECCIÓN: DATOS DEL RECEPTOR / COMPRADOR (`COMP`)

| Posición | Campo                          | Largo máx | Tipo     | Oblig. | Tabla / Notas                   |
|---------|---------------------------------|-----------|----------|--------|---------------------------------|
| 1       | `<RNCComprador>`                | 9 u 11    | NUM      | 1      |                                 |
| 2       | `<RazonSocialComprador>`        | 150       | ALFA NUM | 1      |                                 |
| 3       | `<ContactoComprador>`           | 80        | ALFA NUM | 3      |                                 |
| 4       | `<CorreoComprador>`             | 80        | ALFA NUM | 3      |                                 |
| 5       | `<DireccionComprador>`          | 100       | ALFA NUM | 3      |                                 |
| 6       | `<MunicipioComprador>`          | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 7       | `<ProvinciaComprador>`          | 6         | NUM      | 3      | Tabla 8. Provincias_Municipios |
| 8       | `<CodigoInternoComprador>`      | 20        | ALFA NUM | 3      |                                 |
| 9       | `<ResponsablePago>`             | 20        | ALFA     | 3      |                                 |
| 10      | `<Informacionadicionalcomprador>` | 150     | ALFA NUM | 3      |                                 |

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
| 4       | `<NombreItem>`                     | 80        | ALFA NUM | 1      |                                  |
| 5       | `<IndicadorBienoServicio>`         | 1         | NUM      | 1      | Tabla 14. Indicador_Bien_Servicio|
| 6       | `<DescripcionItem>`                | 1000      | ALFA NUM | 3      |                                  |
| 7       | `<CantidadItem>`                   | 18        | NUM      | 1      |                                  |
| 8       | `<UnidadMedida>`                   | 2         | NUM      | 3      | Tabla 9. Unidad_Medida           |
| 9       | `<PrecioUnitarioItem>`             | 20        | NUM      | 1      |                                  |
| 10      | `<PrecioOtraMoneda>`               | 20        | NUM      | 2      |                                  |
| 11      | `<DescuentoOtraMoneda>`            | 18        | NUM      | 3      |                                  |
| 12      | `<RecargoOtraMoneda>`              | 18        | NUM      | 3      |                                  |
| 13      | `<MontoItemOtraMoneda>`            | 18        | NUM      | 3      |                                  |
| 14      | `<MontoItem>`                      | 18        | NUM      | 1      |                                  |

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
| 3       | `<DescripcionDescuentoRecargo>`  | 45        | ALFA | 3      |                                                 |
| 4       | `<TipoValor>`                    | 1         | ALFA | 2      |                                                 |
| 5       | `<ValorDescuentoRecargo>`        | 5         | NUM  | 2      |                                                 |
| 6       | `<MontoDescuentoRecargo>`        | 18        | NUM  | 2      |                                                 |
| 7       | `<MontoDescuentoRecargoOtraMoneda>` | 18     | NUM  | 3      |                                                 |
| 8       | `<IndicadorFacturacionDescuentoRecargo>` | 1 | NUM | 2 | Tabla 17. Indicador_facturación_Descuento_Recargo |

---

### Notas para implementación

- Esta hoja se usará como guía para:
  - `LuganisXmlReader.Parse` en **tipo 43** (gasto menor).
  - `LuganisTxtBuilder.BuildTxtTipo43`.
- No se muestra una sección de referencia (`INFR`) en las capturas para este tipo; cualquier campo adicional de referencia deberá verificarse en el documento completo.

