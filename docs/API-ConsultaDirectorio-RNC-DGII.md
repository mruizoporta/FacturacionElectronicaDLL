# Consulta Directorio DGII por RNC

Documento para integrar desde **Delphi 7** (u otro cliente) el endpoint que la DLL `FacturacionElectronicaDGII` usa internamente para buscar datos de un contribuyente electrónico por su RNC.

Referencia oficial DGII: *Descripción técnica de facturación electrónica* — servicio **Consulta Directorio de Servicios por RNC**.

---

## 1. Para qué sirve

Dado el **RNC del comprador/receptor**, la DGII devuelve:

| Campo | Uso típico en pantallas Delphi |
|-------|-------------------------------|
| `nombre` | Razón social → autocompletar nombre del cliente |
| `rnc` | Confirmar RNC normalizado |
| `urlRecepcion` | URL del servicio donde el receptor recibe e-CF |
| `urlAceptacion` | URL de aprobación comercial |
| `urlOpcional` | URL de autenticación opcional del receptor |

Solo aplica a contribuyentes **autorizados como emisores electrónicos** en el directorio DGII.

---

## 2. Método COM en la DLL (recomendado para Delphi)

```vb
Public Function ConsultarDirectorioPorRnc(
    rncConsulta As String,
    urlBaseDgii As String,
    Optional rncEmisorCertificado As String = "",
    Optional pathCertificado As String = "",
    Optional passCertificado As String = "",
    Optional tokenDgii As String = "",
    Optional emp_codigo As String = ""
) As String
```

Devuelve **JSON** para parsear en Delphi:

```json
{
  "ok": true,
  "encontrado": true,
  "nombre": "EMPRESA CLIENTE SRL",
  "rnc": "101234567",
  "urlRecepcion": "https://...",
  "urlAceptacion": "https://...",
  "urlOpcional": "",
  "message": "EMPRESA CLIENTE SRL",
  "httpStatus": 200,
  "rawResponse": "{...}"
}
```

| Campo | Significado |
|-------|-------------|
| `ok` / `encontrado` | `true` si el RNC está en el directorio DGII |
| `nombre` | Razón social → autocompletar en pantalla |
| `message` | Texto para el usuario (`nombre` o motivo de no encontrado) |
| `httpStatus` | 200, 204, 401, etc. |

### ¿Cómo sabe la DLL si la empresa usa FE?

Consulta la base de datos del DashA:

```sql
SELECT Usa_FacturacionElectronica FROM dbo.Parametros WHERE emp_codigo = @emp
```

`emp_codigo` lo recibe Delphi en el parámetro opcional, o puede estar en `config.ini` (`emp_codigo` en `[rnc_XXX]`).

| `Usa_FacturacionElectronica` | Certificado para token DGII |
|------------------------------|-------------------------------|
| `1` / `S` / `true` | **Propio** de la empresa: `config.ini` `[rnc_XXX]`, `ConfigurarPorRnc` |
| `0` / `N` / vacío | **Dueño del sistema**: `config.ini` `[sistema]` (misma carpeta `certificados\` junto a la DLL) |

### `config.ini` — sección `[sistema]` (empresas sin FE)

```ini
[sistema]
rnc=131083298
pathCertificado=certificados\dueno_sistema.p12
passCertificado=clave_certificado
```

El `.p12` se coloca en `C:\Dasha\FacturacionElectronica\certificados\` (igual que los certificados de empresas con FE).

### Ejemplo Delphi

```pascal
FacturaService.ConfigurarPorRnc(RNC_Empresa);
Res := FacturaService.ConsultarDirectorioPorRnc(
  RNC_Cliente,
  '',              // vacío: config.ini o Parametros.URL_FacturacionElectronica
  RNC_Emisor,
  '', '', '',
  IntToStr(Emp_Codigo)   // emp_codigo para leer Parametros
);
```

La DLL decide sola qué certificado usar según `Usa_FacturacionElectronica`. Delphi **no** pasa un booleano `feActivo`.

Si `urlBaseDgii` viene vacío: `url_base` en `config.ini` → `Parametros.URL_FacturacionElectronica` → producción (`CerteCF`).

### Uso interno tras envío de factura

Tras aceptación DGII, si `RncCliente <> ''`, la DLL consulta el mismo endpoint y reenvía el XML a `{urlRecepcion}/fe/recepcion/api/ecf`.

---

## 3. URLs y ambientes

La base viene de `config.ini` → clave `url_base` en `[general]`:

| Ambiente | `url_base` típico |
|----------|-------------------|
| Certificación / pruebas | `https://ecf.dgii.gov.do/TesteCF/` |
| Producción | `https://ecf.dgii.gov.do/CerteCF/` |

### Endpoint completo (como en la DLL)

```
GET {url_base}consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC={rnc}
```

**Nota de implementación en la DLL:** el código concatena con una barra extra:

```vb
GlobalVariables.url_base & "/consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC=" & rnc
```

Si `url_base` ya termina en `/` (recomendado), la URL queda con `//` antes de `consultadirectorio`; los servidores DGII lo toleran. En Delphi use una sola barra:

```pascal
BaseURL := 'https://ecf.dgii.gov.do/CerteCF/';  // con barra final
URL := BaseURL + 'consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC=' + RNC;
```

Equivalente oficial DGII:

- Test: `https://ecf.dgii.gov.do/testecf/consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC=...`
- Prod: `https://ecf.dgii.gov.do/ecf/consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC=...`

(`url_base` de DashA ya incluye el segmento `TesteCF/` o `CerteCF/`.)

---

## 4. Autenticación (obligatoria)

El endpoint requiere **token JWT** obtenido con el certificado del **emisor** (mismo flujo que envío de facturas).

### 4.1 Obtener semilla

```
GET {url_base}autenticacion/api/Autenticacion/Semilla
Accept: application/xml
```

Respuesta: XML de semilla (sin token).

### 4.2 Firmar la semilla

Firmar el XML con el certificado `.p12` del emisor (mismo que usa la DLL en `Seguridad.FirmarXML`).

### 4.3 Validar semilla → token

```
POST {url_base}autenticacion/api/autenticacion/validarsemilla
Content-Type: multipart/form-data
Accept: application/json
Campo: xml = semilla_firmada.xml
```

Respuesta JSON:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expira": "2026-05-22T10:30:00",
  "expedido": "2026-05-22T08:30:00"
}
```

Guardar `token` para las consultas siguientes (vida útil limitada; renovar si expira).

### 4.4 Consulta directorio

```
GET {url_consulta}
Authorization: Bearer {token}
Accept: application/json
```

TLS **1.2** obligatorio.

---

## 5. Request — consulta por RNC

| Elemento | Valor |
|----------|--------|
| Método | `GET` |
| Query | `RNC` = RNC del contribuyente a consultar (solo dígitos, 9 u 11) |
| Headers | `Authorization: Bearer {token}` |
| Headers | `Accept: application/json` |

### Normalización del RNC (recomendado)

Igual que `RutaHelper.NormalizarRnc` en la DLL: quitar guiones y espacios; dejar solo dígitos.

```
131-083298-1  →  1310832981  (o 9/11 según el caso)
```

---

## 6. Response

### 6.1 Éxito — HTTP 200

Según documentación DGII, el cuerpo puede ser un **objeto** o un **arreglo con un objeto**. Ejemplo:

```json
{
  "nombre": "EMPRESA CLIENTE SRL",
  "rnc": "101234567",
  "urlRecepcion": "https://receptor.cliente.com.do",
  "urlAceptacion": "https://aprobacion.cliente.com.do",
  "urlOpcional": ""
}
```

O:

```json
[
  {
    "nombre": "EMPRESA CLIENTE SRL",
    "rnc": "101234567",
    "urlRecepcion": "https://receptor.cliente.com.do",
    "urlAceptacion": "https://aprobacion.cliente.com.do",
    "urlOpcional": ""
  }
]
```

**Modelo en la DLL** (`DirectorioDGII.vb`):

```vb
Public Property nombre As String
Public Property rnc As String
Public Property urlRecepcion As String
Public Property urlAceptacion As String
Public Property urlOpcional As String
```

La DLL deserializa un **objeto único** y solo usa `urlRecepcion` para el reenvío al cliente. En Delphi conviene leer también `nombre` para la UI.

### 6.2 Sin datos — HTTP 204 No Content

El contribuyente **no está en el directorio** (no es emisor electrónico o no tiene URLs registradas).

La DLL devuelve cadena vacía `""` y no reenvía el XML al comprador.

### 6.3 Errores

| HTTP | Significado |
|------|-------------|
| 401 | Token inválido o expirado → renovar token |
| 404 | URL mal formada |
| 4xx/5xx | Ver cuerpo de respuesta |

En la DLL, cualquier excepción en la consulta se captura y devuelve `""` (sin propagar error al usuario). **En Delphi se recomienda mostrar el error** en pantallas de captura.

---

## 7. Uso posterior de `urlRecepcion` (envío al comprador)

Si se desea enviar el e-CF firmado al receptor (como hace la DLL tras aceptación):

```
POST {urlRecepcion sin barra final}/fe/recepcion/api/ecf
Authorization: Bearer {token del EMISOR}
Content-Type: multipart/form-data
Campo: xml = {RNCEmisor}{eNCF}.xml
```

Ejemplo de URL final en la DLL:

```vb
urlDestino = urlRecepcion.TrimEnd("/") & "/fe/recepcion/api/ecf"
```

---

## 8. Implementación sugerida en Delphi 7

### 8.1 Dependencias

- **Indy** (`IdHTTP`) o WinHTTP — HTTPS con TLS 1.2.
- Parser JSON: SuperObject, mORMot, o extracción manual si solo necesita `nombre` y `rnc`.

### 8.2 Flujo en pantalla (ej. alta de cliente / factura)

```
1. Usuario ingresa o sale del campo RNC
2. Normalizar RNC (solo dígitos)
3. Si longitud < 9 → no consultar
4. Obtener token DGII (cachear en memoria hasta expirar)
5. GET consulta directorio
6. Si HTTP 200 y hay "nombre" → llenar campo Razón Social
7. Si HTTP 204 → mensaje: "RNC no registrado como emisor electrónico"
8. Opcional: guardar urlRecepcion/urlAceptacion en tabla cliente
```

### 8.3 Pseudocódigo

```pascal
function ConsultarDirectorioDGII(const ARnc, AToken, ABaseURL: string): TDirectorioDGII;
var
  HTTP: TIdHTTP;
  URL, Resp: string;
  RNC: string;
begin
  RNC := SoloDigitos(ARnc);
  URL := ABaseURL;
  if URL[Length(URL)] <> '/' then URL := URL + '/';
  URL := URL + 'consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC=' + RNC;

  HTTP := TIdHTTP.Create(nil);
  try
    HTTP.Request.Accept := 'application/json';
    HTTP.Request.CustomHeaders.Add('Authorization: Bearer ' + AToken);
  // IdSSLIOHandler con TLS 1.2
    Resp := HTTP.Get(URL);
    // Parsear JSON → nombre, rnc, urlRecepcion, urlAceptacion, urlOpcional
    // Si respuesta empieza con '[' tomar primer elemento del array
  finally
    HTTP.Free;
  end;
end;
```

### 8.4 Uso desde Delphi (solo DLL)

```pascal
ResJSON := FacturaService.ConsultarDirectorioPorRnc(
  RNC, UrlDGII, RNC_Emisor, '', '', '', IntToStr(Emp_Codigo));
// Parsear JSON → si encontrado=true, asignar campo Nombre del cliente
```

---

---

## 9. Configuración relacionada (`config.ini`)

```ini
[general]
url_base=https://ecf.dgii.gov.do/CerteCF/
urlfc_base=https://fc.dgii.gov.do/CerteCF/

[sistema]
rnc=131083298
pathCertificado=certificados\dueno_sistema.p12
passCertificado=***

[rnc_131083298]
emp_codigo=1
pathCertificado=certificados\mi_empresa.p12
passCertificado=***
```

- `url_base` → autenticación + **consulta directorio** + consulta estado e-CF.
- `urlfc_base` → resúmenes FC (no usa consulta directorio).
- `[sistema]` → certificado del dueño cuando `Parametros.Usa_FacturacionElectronica = 0`.
- Certificado del **emisor** firma la semilla; el **RNC consultado** es el del cliente/comprador.

---

## 10. Casos de prueba

| RNC consultado | Resultado esperado |
|----------------|-------------------|
| Emisor electrónico activo | 200 + `nombre` + URLs |
| RNC válido no electrónico | 204 o cuerpo vacío |
| RNC inexistente / mal formado | 204 o error 4xx |
| Token expirado | 401 → renovar token |
| Ambiente incorrecto (Test vs Prod) | 204 o error |

---

## 11. Resumen para el otro proyecto

| Tema | Detalle |
|------|---------|
| Endpoint | `GET .../consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC={rnc}` |
| Auth | Bearer token (semilla firmada con certificado del emisor) |
| Campo UI principal | `nombre` (razón social) |
| Sin registro | HTTP 204 → informar al usuario |
| DLL | `ConsultarDirectorioPorRnc` — lee `Parametros.Usa_FacturacionElectronica`; sin FE usa cert `[sistema]` en config.ini |
| RNC sin FE electrónico | `encontrado=false`, HTTP 204 — consulta OK, sin datos |

---

## 12. Referencias en este repositorio

| Archivo | Contenido |
|---------|-----------|
| `FacturaElectronicaService.vb` | `ObtenerDirectorioPorRNCAsync`, `EnviarFacturaClienteSiTieneUrlAsync` |
| `DirectorioDGII.vb` | Modelo de respuesta |
| `Seguridad.vb` | Obtención de token DGII |
| `GlobalVariables.vb` | `url_base`, `token` |
| `docs/config.ini.ejemplo_multi_rnc` | Ejemplo de configuración |

---

*Generado a partir de `FacturacionElectronicaDGII` — integración DashA / Delphi 7.*
