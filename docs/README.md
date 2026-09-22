# Documentación LUGANIS – Facturación Electrónica

Documentación de referencia para la integración con la Plataforma Facturación Electrónica RD (LUGANIS CORP). Fuente: Manual de integración usuario, versión 03, 04/03/2025.

---

## Índice de documentos

### Formato TXT y reglas

| Archivo | Contenido |
|---------|-----------|
| [Formato-TXT-Luganis.md](Formato-TXT-Luganis.md) | Separador de campos, estructura, nombre de archivo, codificación UTF-8 sin BOM |
| [Reglas-Tolerancia-Redondeo.md](Reglas-Tolerancia-Redondeo.md) | Tolerancia por transacción, tolerancia global, reglas de redondeo |

### API (integración online)

| Archivo | Contenido |
|---------|-----------|
| [API-Luganis.md](API-Luganis.md) | Login, RefreshToken, Logout, Envío TXT, Consulta trackId, Consulta STATUS, Descarga PDF/XML/QR |

### Integración en lote (referencia, sin implementar)

| Archivo | Contenido |
|---------|-----------|
| [Integracion-Lote-Luganis.md](Integracion-Lote-Luganis.md) | AWS S3, estructura de directorios, envío y consulta en lote |

### Recomendaciones

| Archivo | Contenido |
|---------|-----------|
| [Recomendaciones-Luganis.md](Recomendaciones-Luganis.md) | Uso de token, secciones vacías, codificación, URLs de pruebas |

### Campos por tipo de e-CF

| Archivo | Tipo | Descripción |
|---------|------|-------------|
| [Campos-eCF-31.md](Campos-eCF-31.md) | 31 | Factura de Crédito Fiscal |
| [Campos-eCF-32.md](Campos-eCF-32.md) | 32 | Factura de Consumo |
| [Campos-eCF-33.md](Campos-eCF-33.md) | 33 | Nota de Débito |
| [Campos-eCF-34.md](Campos-eCF-34.md) | 34 | Nota de Crédito |
| [Campos-eCF-41.md](Campos-eCF-41.md) | 41 | Comprobante de Compra |
| [Campos-eCF-43.md](Campos-eCF-43.md) | 43 | Comprobante de Gasto Menor |
| [Campos-eCF-44.md](Campos-eCF-44.md) | 44 | Comprobante Regímenes Especiales |
| [Campos-eCF-45.md](Campos-eCF-45.md) | 45 | Comprobante Gubernamental |
| [Campos-eCF-46.md](Campos-eCF-46.md) | 46 | Comprobante Exportaciones |
| [Campos-eCF-47.md](Campos-eCF-47.md) | 47 | Comprobante Retenciones |
