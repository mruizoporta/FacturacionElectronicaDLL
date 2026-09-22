# API LUGANIS – Integración en Línea (ONLINE)

Referencia extraída del Manual de integración usuario Plataforma Facturación Electrónica RD (LUGANIS CORP).

---

## 1. Información general

### URL base (ambiente pruebas)

```
https://rd.stage-api.tech-luganis.net
```

### Formato de respuesta JSON

| Caso | Estructura |
|------|------------|
| Éxito | `{ "status": { "code": "0", "message": "Transacción exitosa" }, "data": { ... } }` |
| Error | `{ "status": { "code": "ERROR-CODE", "message": "Error information" } }` |

### Códigos HTTP

| Código | Significado |
|--------|-------------|
| 200 | OK – Petición procesada |
| 401 | No autorizada – Token expirado o inválido |
| 403 | Forbidden – Sin permisos para la URL |
| 500 | Internal Server Error – Reintentar |
| 503 | Unavailable – Servicio no disponible |

---

## 2. Autenticación

- Es obligatorio obtener un **Token** antes de hacer peticiones.
- Duración del token: **3600 segundos** (1 hora).
- Usar **RefreshToken** unos 5 minutos antes de que expire para mantener sesión activa.

---

## 3. Login

### Request

| Método | URL |
|--------|-----|
| POST | `{{SERVER-URL}}/authentication-service/auth/login/COMPANY` |

### Body (JSON)

```json
{
  "identifierValue": "[[CLIENT-USERNAME]]",
  "authenticationValue": "[[CLIENT-PASSWORD]]",
  "deviceInfo": {
    "appVersion": "[[CLIENT-APP-VERSION]]",
    "os": "[[CLIENT-OS]]",
    "deviceId": "[[DEVICE-ID]]",
    "latitude": "[[CLIENT-LATITUDE]]",
    "longitude": "[[CLIENT-LONGITUDE]]",
    "providerIpAddress": "[[CLIENT-IP-ADDRESS]]"
  }
}
```

### Respuesta exitosa

```json
{
  "status": { "code": "0", "message": "Transacción exitosa" },
  "data": {
    "token": {
      "accessToken": "...",
      "refreshToken": "...",
      "expiresIn": 3600
    },
    "lastLoginTime": "2024-02-02 22:09:37",
    "lastLoginTimeStr": "2024-02-02T22:09:37-04:00",
    "passwordTemporal": true
  }
}
```

Token a usar: `data.token.accessToken`

---

## 4. RefreshToken

### Request

| Método | URL |
|--------|-----|
| PUT | `{{SERVER-URL}}/authentication-service/refreshToken` |

### Headers

| Header | Obligatorio | Descripción |
|--------|-------------|-------------|
| device-id | Sí | Mismo usado en LOGIN |
| Authorization | Sí | Bearer {accessToken} |

### Body (JSON)

```json
{
  "refreshToken": "{{REFRESH-TOKEN}}"
}
```

---

## 5. Logout

### Request

| Método | URL |
|--------|-----|
| GET | `{{SERVER-URL}}/authentication-service/logout` |

### Headers

| Header | Obligatorio |
|--------|-------------|
| device-id | Sí |
| Authorization | Sí |

---

## 6. Envío de archivo TXT

### Request

| Método | URL |
|--------|-----|
| POST | `{{SERVER-URL}}/parser-service/send` |

### Headers

| Header | Obligatorio |
|--------|-------------|
| Content-Type | application/json |
| Accept | application/json |
| device-id | Sí |
| Authorization | Sí (Bearer token) |

### Body (JSON)

```json
{
  "filename": "132944372E310000002238.txt",
  "filecontent": "<BASE64_DEL_TXT_UTF8_SIN_BOM>"
}
```

### Formato de filename

`RNCE99SECUENCIAL.txt` (ej.: `132944372E310000002238.txt`)

- RNC: identificación del emisor
- E: facturación electrónica
- 99: tipo e-CF (31, 32, 33, 34, 41, 43, 44, 45, 46, 47)
- SECUENCIAL: 10 dígitos, ceros a la izquierda

### Respuesta exitosa

```json
{
  "status": { "code": "0", "message": "Transacción exitosa" },
  "data": {
    "trackId": "3a3D460D-6D5D-4B31-90D1-EBCB7E0CDC8C9",
    "receDateLate": "2024-02-02 22:32:06"
  }
}
```

---

## 7. Consulta estado por trackId (6.8)

### Request

| Método | URL |
|--------|-----|
| GET | `{{SERVER-URL}}/parser-service/read/trackId/{{TRACK-ID}}` |

### Headers

| Header | Obligatorio |
|--------|-------------|
| device-id | Sí |
| Authorization | Sí |

### TRACK-ID

UUID devuelto en el método de envío (ej.: `e591745d-7e7f-452e-902e-89aa395e5e1c`).

### Respuesta

```json
{
  "code": "0",
  "message": "Transacción exitosa",
  "data": {
    "trackId": "dcb44803-eb64-4e9f-8fb7-8b10614026a9",
    "requestDate": "2024-04-08 13:30:20",
    "filename": "152904573f53180000223b.xml"
  },
  "status": "Aceptado",
  "responseMessage": "Aceptado"
}
```

---

## 8. Consulta estado por documento (7.4)

### Request

| Método | URL |
|--------|-----|
| GET | `{{SERVER-URL}}/client-service/download/STATUS/{{CLIENT-ID}}/{{FILENAME}}` |

### Headers

| Header | Obligatorio |
|--------|-------------|
| device-id | Sí |
| Authorization | Sí |

### Path parameters

| Parámetro | Ejemplo | Descripción |
|-----------|---------|-------------|
| CLIENT-ID | RNC-132944372 | Tipo documento + identificador empresa |
| FILENAME | E310000020045 | Formato E99SECUENCIAL (sin .txt) |

### Respuesta

```json
{
  "status": { "code": "8", "message": "Transacción exitosa" },
  "data": [{
    "request": {
      "type": "STATUS",
      "customerIdentityDocument": "RNC-132944372",
      "documentNumber": "E310000020415"
    },
    "detail": {
      "responseCode": "1",
      "responseMessage": "Aceptado"
    }
  }]
}
```

---

## 9. Descarga PDF

### Request

| Método | URL |
|--------|-----|
| GET | `{{SERVER-URL}}/client-service/download/PDF/{{CLIENT-ID}}/{{FILENAME}}` |

### Path parameters

| Parámetro | Ejemplo |
|-----------|---------|
| CLIENT-ID | RNC-132944372 |
| FILENAME | E310000020045 |

### Respuesta

```json
{
  "data": {
    "type": "PDF",
    "filename": "E310000020045.pdf",
    "base64FileContent": "..."
  }
}
```

---

## 10. Descarga XML

### Request

| Método | URL |
|--------|-----|
| GET | `{{SERVER-URL}}/client-service/download/XML/{{CLIENT-ID}}/{{FILENAME}}` |

### Path parameters

| Parámetro | Ejemplo |
|-----------|---------|
| CLIENT-ID | RNC-132944372 |
| FILENAME | E310000020045 |

---

## 11. Descarga QR

### Request

| Método | URL |
|--------|-----|
| GET | `{{SERVER-URL}}/client-service/download/QR/{{CLIENT-ID}}/{{FILENAME}}` |

### Respuesta

```json
{
  "data": {
    "detail": {
      "qrCode": "https://eF.dgii.gob.do/TesterE/Consulta?index=..."
    }
  }
}
```
