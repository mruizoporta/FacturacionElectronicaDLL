# Formato del archivo TXT – LUGANIS

Referencia extraída del Manual de integración usuario Plataforma Facturación Electrónica RD (LUGANIS CORP, versión 03, 04/03/2025).

---

## 1. Separador de campos

- El carácter separador de campos es el **pipe** `|`.
- Los campos van concatenados en secuencia, separados por `|`.

---

## 2. Campos con repeticiones o datos unificados

Algunos campos pueden tener varios valores. Se indican entre **corchetes** `[ ]` y los valores se separan con **punto y coma** `;`.

### Campos únicos repetibles

| Campo | Ejemplo |
|-------|---------|
| `[TelefonoEmisor]` | `[809-999-9999]` o `[809-999-9999;829-999-9999]` |
| `[TipoImpuesto]` | Códigos de impuestos separados por `;` |

### Campos separados por pipe pero unificados (pueden repetirse)

| Campo | Ejemplo |
|-------|---------|
| `[TipoCodigo\|CodigoItem]` | `[EAN13\|7467397392256;EAN13\|6958347455288]` |
| `[Subcantidad\|CodigoSubcantidad]` | |
| `[TipoSubDescuento\|SubDescuentoPorcentaje\|MontoSubDescuento]` | |
| `[TipoSubRecargo\|SubRecargoPorcentaje\|MontoSubRecargo]` | |

---

## 3. Estructura del contenido TXT

- Una **línea por sección**.
- Cada línea empieza con el código de la sección, seguido de `|` y los valores de los campos en orden.
- Secciones posibles según tipo de e-CF: IDOC, EMIS, COMP, INFA, TRAN, OTMN, ITEM, FPAG, DERE, INFR.
- Si una sección **no contiene datos**, **no se incluye** en el archivo.

### Ejemplo de trama (tipo 31, simplificado)

```
IDOC|31|E310000002238|31-12-2025||01|1|||CT|960587458960|BANCO DE RESERVAS||03-04-2024
EMIS|132944372|LUGANIS RD, SRL|LUGANIS|Naco|C. Gustavo...|010100|010000|[849-846-7920;829-401-3982]|INFO@LUGANIS.COM|...
COMP|1220174435|...|lucelyn.gonzalez@luganis.com|Av. 27 de Febrero...
ITEM|1|[EAN13|7467397392256;EAN13|6958347455288]|1|||PRODUCTO 111111...|5.00|43|||...|75000.00
FPAG|3|300000.00
FPAG|2|200000.00
FPAG|1|50950.00
```

---

## 4. Nombre del archivo TXT

### Integración Online

Formato: `RNCeNCF.txt`

| Componente | Descripción | Ejemplo |
|------------|-------------|---------|
| RNC | RNC del emisor | 132944372 |
| E | Indicador electrónico | E |
| 99 | Tipo de e-CF (31, 32, 33, 34, 41, 43, 44, 45, 46, 47) | 31 |
| SECUENCIAL | 10 dígitos, rellenados con ceros a la izquierda | 0000002238 |

**Ejemplo:** `132944372E310000002238.txt`

### Integración en Lote

Formato: `RNC_Emisor + Tipo_Comprobante + Fecha_envío (DDMMYYYYHHMMSS)`

**Ejemplo:** `132944372E3116042024153206.txt`

---

## 5. Codificación

| Regla | Valor |
|-------|-------|
| Caracteres | UTF-8 **sin BOM** |
| Envío a API | Codificar el TXT en **Base64** después de UTF-8 sin BOM |

---

## 6. Secciones del TXT (referencia rápida)

| Código | Descripción |
|--------|-------------|
| IDOC | Identificación del Documento |
| EMIS | Datos del Emisor |
| COMP | Datos del Receptor/Comprador |
| INFA | Informaciones Adicionales |
| TRAN | Informaciones del Transporte |
| OTMN | Informaciones sobre Otras Monedas |
| ITEM | Detalle de Bienes o Servicios facturados |
| FPAG | Formas de Pago |
| DERE | Descuentos y Recargos |
| INFR | Información de Referencia (notas débito/crédito) |
