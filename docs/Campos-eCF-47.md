## Archivo TXT – Tipo e-CF 47

Comprobante de Retenciones e Informaciones Complementarias Electrónico (Tipo e‑CF 47).  
Resumen de campos para el archivo TXT según la pestaña **"Campos e‑CF 47"** de la matriz de LUGANIS.  
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
| 4       | `<TipoPago>`                 | 1         | NUM      | 3      | Tabla 5. Tipo_Pago            |
| 5       | `<FechaLimitePago>`          | 10        | ALFA NUM | 3      |                               |
| 6       | `<TerminoPago>`              | 15        | ALFA NUM | 3      |                               |
| 7       | `<TipoCuentaPago>`           | 2         | ALFA     | 3      | Tabla 7. Tipo_Cuenta_Pago     |
| 8       | `<NumeroCuentaPago>`         | 28        | ALFA NUM | 3      |                               |
| 9       | `<BancoPago>`                | 75        | ALFA NUM | 3      |                               |
| 10      | `<FechaDesde>`               | 10        | ALFA NUM | 3      |                               |
| 11      | `<FechaHasta>`               | 10        | ALFA NUM | 3      |                               |
| 12      | `<FechaEmision>`             | 10        | ALFA NUM | 1      |                               |

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
| 12      | `<NumeroFacturaInterna>`      | 20        | ALFA NUM | 3      |                                  |
| 13      | `<NumeroPedidoInterno>`       | 20        | NUM      | 3      |                                  |
| 14      | `<InformacionAdicionalEmisor>`| 250       | ALFA NUM | 3      |                                  |

---

### SECCIÓN: DATOS DEL RECEPTOR / COMPRADOR (`COMP`)

| Posición | Campo                    | Largo máx | Tipo     | Oblig. | Tabla / Notas |
|---------|---------------------------|-----------|----------|--------|---------------|
| 1       | `<IdentificadorExtranjero>` | 20      | ALFA NUM | 3      |               |
| 2       | `<RazonSocialComprador>`    | 150     | ALFA NUM | 3      |               |

---

### SECCIÓN: INFORMACIONES SOBRE OTRAS MONEDAS (`OTMN`)

| Posición | Campo        | Largo máx | Tipo | Oblig. | Tabla / Notas         |
|---------|--------------|-----------|------|--------|-----------------------|
| 1       | `<TipoMoneda>` | 3       | ALFA | 2      | Tabla 12. Tipo_Moneda |
| 2       | `<TipoCambio>` | 7       | NUM  | 2      |                       |

---

### SECCIÓN: DETALLE DE LOS BIENES O SERVICIOS FACTURADOS (`ITEM`)

| Posición | Campo                                            | Largo máx | Tipo     | Oblig. | Tabla / Notas                         |
|---------|---------------------------------------------------|-----------|----------|--------|---------------------------------------|
| 1       | `<NumeroLinea>`                                  | 5         | NUM      | 1      |                                       |
| 2       | `<TipoCodigo\|CodigoItem>`                       | 14        | ALFA NUM | 3      |                                       |
| 3       | `<IndicadorFacturacion>`                         | 1         | NUM      | 3      | Tabla 13. Indicador_Facturación       |
| 4       | `<IndicadorAgenteRetencionoPercepcion>`         | 1         | NUM      | 1      |                                       |
| 5       | `<MontoISRRetenido>`                             | 18        | NUM      | 1      |                                       |
| 6       | `<NombreItem>`                                   | 80        | ALFA NUM | 1      |                                       |
| 7       | `<IndicadorBienoServicio>`                       | 1         | NUM      | 1      | Tabla 14. Indicador_Bien_Servicio     |
| 8       | `<DescripcionItem>`                              | 1000      | ALFA NUM | 3      |                                       |
| 9       | `<CantidadItem>`                                 | 18        | NUM      | 1      |                                       |
| 10      | `<UnidadMedida>`                                 | 2         | NUM      | 3      | Tabla 9. Unidad_Medida                |
| 11      | `<PrecioUnitarioItem>`                           | 20        | NUM      | 1      |                                       |
| 12      | `<PrecioOtraMoneda>`                             | 20        | NUM      | 2      |                                       |
| 13      | `<DescuentoOtraMoneda>`                          | 18        | NUM      | 3      |                                       |
| 14      | `<RecargoOtraMoneda>`                            | 18        | NUM      | 3      |                                       |
| 15      | `<MontoItemOtraMoneda>`                          | 18        | NUM      | 2      |                                       |
| 16      | `<MontoItem>`                                    | 18        | NUM      | 1      |                                       |

---

### SECCIÓN: FORMAS DE PAGO (`FPAG`)

| Posición | Campo        | Largo máx | Tipo | Oblig. | Tabla / Notas        |
|---------|--------------|-----------|------|--------|----------------------|
| 1       | `<FormaPago>` | 2        | NUM  | 3      | Tabla 6. Formas_Pago |
| 2       | `<MontoPago>` | 18       | NUM  | 2      |                      |

---

### Notas para implementación

- Esta hoja servirá como referencia para:
  - `LuganisXmlReader.Parse` para **tipo 47**.
  - `LuganisTxtBuilder.BuildTxtTipo47`.
- Este tipo se centra en **retenciones**: usar con atención los campos `IndicadorAgenteRetencionoPercepcion` y `MontoISRRetenido`.
- Cualquier diferencia futura se debe validar siempre contra el documento oficial actualizado de LUGANIS.

