# Recomendaciones y Consideraciones – LUGANIS

Referencia extraída del Manual de integración usuario Plataforma Facturación Electrónica RD (LUGANIS CORP).

---

## 1. Recomendaciones generales (10)

### a) Otras monedas

Cuando se referencien tramas en moneda distinta al peso dominicano (RD$), deben usarse los campos específicos para garantizar la correcta visualización en las representaciones impresas (PDF).

### b) Secciones vacías en el TXT

Si una sección del TXT **no contiene datos**, **no debe incluirse** en el archivo.

### c) Codificación del archivo TXT

El archivo TXT debe estar codificado en **UTF-8 sin BOM**. De lo contrario pueden verse afectados caracteres especiales (acentos, ñ) en la representación impresa y en la generación del XML.

### d) Uso del token (integración online)

- **No** hacer un nuevo login en cada petición.
- Reutilizar el token asignado siempre que sea posible.

### e) Vencimiento del token (RefreshToken)

- Usar **RefreshToken** unos 5 minutos antes de que expire el token actual.
- Evita interrupciones de sesión durante múltiples peticiones.

### f) Cierre de sesión

- Realizar **Logout** después de concluir todas las peticiones a la API.
- Recomendable como medida de seguridad.

---

## 2. URLs de referencia

### Ambiente pruebas

| Servicio | URL |
|----------|-----|
| API | https://rd.stage-api.tech-luganis.net |
| Portal Web | https://rd.stage-web.tech-luganis.net/ |

> Si no se tienen credenciales de pruebas, solicitarlas a un representante de ventas.

---

## 3. Consideraciones para integración en lote

| Regla | Descripción |
|-------|-------------|
| Un archivo por tipo | Solo un archivo TXT por tipo de comprobante fiscal. |
| Separador entre tramas | 15 asteriscos `***************` entre tramas. |
| Máximo de comprobantes | 25.000 por archivo TXT. |
| Tamaño por trama | Máximo 10.000 caracteres. |
| Tamaño por archivo | Máximo 1,50 MB. |
| Envíos consecutivos | Máximo 3 archivos TXT seguidos. |
| Codificación | UTF-8 sin BOM. |
| Nombre del archivo | RNC_Emisor + Tipo_Comprobante + Fecha_envío (DDMMYYYYHHMMSS). |
