# Integración Delphi 7 — Consulta Directorio DGII por RNC

Guía para el equipo Delphi sobre cómo llamar **`ConsultarDirectorioPorRnc`** desde la DLL COM `FacturacionElectronicaDGII`.

**Objetivo:** al capturar el RNC de un cliente/comprador, consultar la DGII y autocompletar razón social (y opcionalmente guardar URLs del receptor electrónico).

---

## 1. Resumen rápido

| Tema | Detalle |
|------|---------|
| DLL | `FacturacionElectronicaDGII.dll` (típicamente `C:\Dasha\FacturacionElectronica\`) |
| Clase COM | `FacturacionElectronicaDGII.FacturaElectronicaService` |
| Método | `ConsultarDirectorioPorRnc` |
| Retorno | **Cadena JSON** (`string` / `BSTR`) — siempre parsear en Delphi |
| Autenticación DGII | La DLL obtiene el token sola (certificado `.p12`) |
| ¿Usa FE la empresa? | La DLL lo lee de `Parametros.Usa_FacturacionElectronica` |

---

## 2. Flujo recomendado en DashA

```
1. Al iniciar sesión / cambiar empresa
   → ConfigurarPorRnc(RNC_Empresa_Activa)

2. En pantalla de cliente o factura, al salir del campo RNC
   → Normalizar RNC (solo dígitos, mínimo 9)
   → ConsultarDirectorioPorRnc(RNC_Cliente, ...)

3. Parsear JSON
   → Si encontrado = true → llenar nombre del cliente
   → Si no → mostrar message al usuario
```

---

## 3. Métodos COM a usar

### 3.1 `ConfigurarPorRnc` (una vez por empresa activa)

```pascal
function ConfigurarPorRnc(rnc: WideString): WideString;
```

| Retorno | Significado |
|---------|-------------|
| `'OK'` | Configuración cargada (connectionString + certificado de `[rnc_XXX]`) |
| `'Error: ...'` | RNC inválido, falta `config.ini`, certificado no encontrado, etc. |

**Cuándo llamarlo:** al abrir DashA o al cambiar de empresa (multi-RNC en la misma PC).

```pascal
var
  ResConfig: string;
begin
  ResConfig := FacturaService.ConfigurarPorRnc(RNC_Empresa);
  if Pos('OK', ResConfig) <> 1 then
    ShowMessage('FE: ' + ResConfig);
end;
```

### 3.2 `ConsultarDirectorioPorRnc` (consulta al directorio)

```pascal
function ConsultarDirectorioPorRnc(
  rncConsulta: WideString;
  urlBaseDgii: WideString;
  rncEmisorCertificado: WideString;
  pathCertificado: WideString;
  passCertificado: WideString;
  tokenDgii: WideString;
  emp_codigo: WideString
): WideString;
```

#### Parámetros

| # | Parámetro | Obligatorio | Descripción |
|---|-----------|-------------|-------------|
| 1 | `rncConsulta` | **Sí** | RNC del **cliente/comprador** a buscar en el directorio DGII |
| 2 | `urlBaseDgii` | No | URL base DGII. Vacío = `config.ini` → `Parametros.URL_FacturacionElectronica` → producción |
| 3 | `rncEmisorCertificado` | No | RNC de la empresa activa (para leer su sección en `config.ini`) |
| 4 | `pathCertificado` | No | Override manual del `.p12` (ruta absoluta o relativa a la DLL) |
| 5 | `passCertificado` | No | Clave del certificado (solo si usa override manual) |
| 6 | `tokenDgii` | No | Token JWT ya obtenido. Vacío = la DLL lo pide con el certificado |
| 7 | `emp_codigo` | Recomendado | Código empresa DashA (`Parametros.emp_codigo`) para saber si usa FE |

**Nota:** los parámetros 3–6 pueden ir vacíos (`''`) en el uso normal. La DLL resuelve certificado y URL automáticamente.

#### ¿Cómo elige la DLL el certificado?

Consulta en la BD del DashA:

```sql
SELECT Usa_FacturacionElectronica FROM dbo.Parametros WHERE emp_codigo = @emp
```

| `Usa_FacturacionElectronica` | Certificado usado |
|------------------------------|-------------------|
| `1`, `S`, `SI`, `true` | Certificado **de la empresa** → `config.ini` `[rnc_XXX]` |
| `0`, `N`, `NO`, vacío | Certificado del **dueño del sistema** → `config.ini` `[sistema]` |

Ambos `.p12` van en `certificados\` junto a la DLL (misma carpeta).

---

## 4. Qué retorna la API (JSON)

El método devuelve **siempre un string JSON**. No lanza excepciones a Delphi; los errores vienen dentro del JSON en `message`.

### 4.1 Estructura del JSON

```json
{
  "ok": true,
  "encontrado": true,
  "nombre": "EMPRESA CLIENTE SRL",
  "rnc": "101234567",
  "urlRecepcion": "https://receptor.cliente.com.do",
  "urlAceptacion": "https://aprobacion.cliente.com.do",
  "urlOpcional": "",
  "message": "EMPRESA CLIENTE SRL",
  "httpStatus": 200,
  "rawResponse": "{\"nombre\":\"EMPRESA CLIENTE SRL\",...}"
}
```

### 4.2 Campos del JSON

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `ok` | boolean | `true` si la operación fue exitosa **y** el RNC está en el directorio |
| `encontrado` | boolean | Igual que `ok` en la práctica: `true` si DGII devolvió datos del contribuyente |
| `nombre` | string | Razón social → **autocompletar campo nombre del cliente** |
| `rnc` | string | RNC normalizado devuelto por DGII |
| `urlRecepcion` | string | URL donde el receptor recibe e-CF (uso avanzado / envío al comprador) |
| `urlAceptacion` | string | URL de aprobación comercial del receptor |
| `urlOpcional` | string | URL opcional de autenticación del receptor |
| `message` | string | Texto para mostrar al usuario: `nombre` si encontró, o motivo del error |
| `httpStatus` | number | Código HTTP de la DGII: `200`, `204`, `401`, `0` si error local |
| `rawResponse` | string | JSON crudo de la DGII (depuración) |

### 4.3 Escenarios de respuesta

#### A) Cliente encontrado en el directorio DGII

```json
{
  "ok": true,
  "encontrado": true,
  "nombre": "ACME SRL",
  "rnc": "131234567",
  "urlRecepcion": "https://...",
  "urlAceptacion": "https://...",
  "urlOpcional": "",
  "message": "ACME SRL",
  "httpStatus": 200,
  "rawResponse": "..."
}
```

**Acción Delphi:** asignar `nombre` al campo razón social del cliente.

---

#### B) RNC válido pero NO es emisor electrónico (HTTP 204)

```json
{
  "ok": false,
  "encontrado": false,
  "nombre": "",
  "rnc": "",
  "urlRecepcion": "",
  "urlAceptacion": "",
  "urlOpcional": "",
  "message": "RNC no registrado como emisor electrónico en el directorio DGII",
  "httpStatus": 204,
  "rawResponse": ""
}
```

**Acción Delphi:** informar al usuario; el cliente puede seguir capturándose manualmente.

---

#### C) Token inválido o expirado (HTTP 401)

```json
{
  "ok": false,
  "encontrado": false,
  "message": "Token DGII inválido o expirado",
  "httpStatus": 401,
  "rawResponse": "..."
}
```

**Acción Delphi:** mostrar `message`; la DLL intentará renovar token en la siguiente llamada si no se pasó `tokenDgii`.

---

#### D) Error de configuración / certificado (antes de llamar DGII)

```json
{
  "ok": false,
  "encontrado": false,
  "nombre": "",
  "rnc": "",
  "urlRecepcion": "",
  "urlAceptacion": "",
  "urlOpcional": "",
  "message": "Se requiere tokenDgii o certificado del dueño del sistema en config.ini [sistema] (pathCertificado + passCertificado junto a la DLL).",
  "httpStatus": 0,
  "rawResponse": ""
}
```

**Acción Delphi:** mostrar `message` al administrador; revisar `config.ini` y carpeta `certificados\`.

---

#### E) RNC vacío o inválido

```json
{
  "ok": false,
  "encontrado": false,
  "message": "RNC vacío o inválido",
  "httpStatus": 0,
  "rawResponse": ""
}
```

---

## 5. Ejemplo de llamada en Delphi 7

### 5.1 Crear el objeto COM

```pascal
uses ComObj;

var
  FacturaService: OleVariant;
begin
  FacturaService := CreateOleObject('FacturacionElectronicaDGII.FacturaElectronicaService');
```

> Si ya tienen una unit/wrapper propia para la DLL, usen el mismo patrón que `EnviarFacturaElectronica`.

### 5.2 Consulta al salir del campo RNC

```pascal
procedure TFrmCliente.ConsultarRncDGII;
var
  ResJSON, RncNorm: string;
begin
  RncNorm := SoloDigitos(edtRNC.Text);
  if Length(RncNorm) < 9 then Exit;

  ResJSON := FacturaService.ConsultarDirectorioPorRnc(
    RncNorm,                    // rncConsulta: cliente
    '',                         // urlBaseDgii: automático
    RNC_Empresa,                // rncEmisorCertificado
    '', '', '',                 // pathCert, pass, token: vacíos
    IntToStr(Emp_Codigo)        // emp_codigo
  );

  ProcesarRespuestaDirectorio(ResJSON);
end;
```

### 5.3 Parsear el JSON (ejemplo simple)

Si usan **SuperObject**, **mORMot** u otra librería JSON, lean los campos directamente.

Ejemplo conceptual:

```pascal
procedure ProcesarRespuestaDirectorio(const AJson: string);
var
  Ok, Encontrado: Boolean;
  Nombre, Mensaje: string;
begin
  // Reemplazar por su parser JSON real
  Ok := JsonGetBoolean(AJson, 'ok');
  Encontrado := JsonGetBoolean(AJson, 'encontrado');
  Nombre := JsonGetString(AJson, 'nombre');
  Mensaje := JsonGetString(AJson, 'message');

  if Ok and Encontrado then
  begin
    edtNombre.Text := Nombre;
    // Opcional: guardar urlRecepcion / urlAceptacion en tabla Clientes
  end
  else if Mensaje <> '' then
  begin
    // 204 u otro: no es emisor electrónico o error
    if Pos('no registrado', LowerCase(Mensaje)) > 0 then
      ShowMessage(Mensaje)
    else
      ShowMessage('Consulta DGII: ' + Mensaje);
  end;
end;
```

**Campo principal para la UI:** `message` (siempre tiene texto útil).  
**Campo principal para autocompletar:** `nombre` cuando `encontrado = true`.

---

## 6. Configuración requerida (`config.ini`)

Archivo junto a la DLL: `C:\Dasha\FacturacionElectronica\config.ini`

### 6.1 URLs comunes

```ini
[general]
url_base=https://ecf.dgii.gov.do/CerteCF/
urlfc_base=https://fc.dgii.gov.do/CerteCF/
```

Certificación/pruebas: `https://ecf.dgii.gov.do/TesteCF/`

### 6.2 Dueño del sistema (empresas SIN FE)

```ini
[sistema]
rnc=131083298
pathCertificado=certificados\dueno_sistema.p12
passCertificado=clave_del_certificado
```

Archivo físico: `C:\Dasha\FacturacionElectronica\certificados\dueno_sistema.p12`

### 6.3 Empresa CON FE (por RNC)

```ini
[rnc_131083298]
emp_codigo=1
connectionString=Persist Security Info=False;database=Steeltec;server=.;User ID=...;Password=...
pathCertificado=certificados\empresa_fe.p12
passCertificado=clave_certificado
```

---

## 7. Tabla `Parametros` (base DashA)

Campos relevantes para esta integración:

| Campo | Uso |
|-------|-----|
| `emp_codigo` | Identificador de empresa (pasarlo en parámetro 7) |
| `Usa_FacturacionElectronica` | `1` = usa certificado propio; `0` = usa certificado `[sistema]` |
| `URL_FacturacionElectronica` | URL DGII si no se pasa `urlBaseDgii` |

---

## 8. Checklist de pruebas

| Caso | RNC consultado | Resultado esperado |
|------|----------------|-------------------|
| Emisor electrónico activo | RNC de cliente FE | `ok=true`, `nombre` con razón social |
| Cliente normal (no FE) | RNC cualquiera no registrado | `ok=false`, `httpStatus=204` |
| Empresa sin FE configurada | Cualquiera | `message` pide certificado `[sistema]` |
| Empresa con FE | Cualquiera | Usa certificado de `[rnc_XXX]` |
| RNC corto / vacío | `< 9 dígitos` | No consultar, o `message` de RNC inválido |
| Certificación | Ambiente TesteCF | `url_base` o `URL_FacturacionElectronica` en test |

---

## 9. Preguntas frecuentes

**¿Debo pasar si la empresa usa FE?**  
No. La DLL lee `Parametros.Usa_FacturacionElectronica` con el `emp_codigo`.

**¿El RNC del parámetro 1 es el de mi empresa?**  
No. Es el RNC del **cliente** que están capturando.

**¿El RNC del parámetro 3 para qué es?**  
Es el RNC de la **empresa emisora** (la activa en DashA), para cargar su sección en `config.ini` y la conexión a BD.

**¿Puedo cachear el token?**  
Opcional. Puede pasar `tokenDgii` en el parámetro 6; si viene vacío, la DLL lo obtiene y también puede reutilizar `GlobalVariables.token` de un envío FE previo en la misma sesión.

**¿Qué muestro al usuario?**  
Si `encontrado = true` → llenar nombre.  
Si no → mostrar `message` (especialmente en 204: "no registrado como emisor electrónico").

---

## 10. Referencia técnica DGII

Endpoint que llama la DLL internamente:

```
GET {url_base}consultadirectorio/api/consultas/obtenerdirectorioporrnc?RNC={rnc}
Authorization: Bearer {token}
Accept: application/json
```

Documentación ampliada (técnica): `docs/API-ConsultaDirectorio-RNC-DGII.md`  
Ejemplo `config.ini` multi-empresa: `docs/config.ini.ejemplo_multi_rnc`

---

*DLL: FacturacionElectronicaDGII — integración DashA / Delphi 7*
