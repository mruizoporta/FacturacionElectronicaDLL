# Integración en Lote (BATCH) – LUGANIS

Referencia extraída del Manual de integración usuario Plataforma Facturación Electrónica RD (LUGANIS CORP).

> **Nota:** Este documento se incluye solo como referencia. La implementación actual de la DLL no contempla integración en lote.

---

## 1. Descripción general

- Se envían **grupos de tramas** del mismo tipo de documento, consolidadas en un único archivo.
- Las respuestas se gestionan de forma consolidada al finalizar el procesamiento.
- Protocolo de intercambio: **Amazon S3**.

### Flujo

1. Cada emisor tiene un directorio/repositorio exclusivo.
2. Se depositan archivos TXT con las tramas a procesar.
3. Se recogen archivos TXT con las respuestas procesadas.

---

## 2. Protocolo de transferencia (8.1)

Se utiliza el protocolo nativo de **Amazon S3** para el intercambio de archivos.

- Guía instalación AWS CLI: https://docs.aws.amazon.com/es_es/cli/latest/userguide/getting-started-install.html

---

## 3. Configuración (8.2)

Tras instalar AWS CLI:

1. Abrir consola DOS o terminal con permisos de ejecución.
2. Ir a un directorio donde exista acceso a la librería instalada.
3. Ejecutar: `aws configure`
4. Indicar cuando se solicite:
   - **AWS Access Key ID:** proporcionada por LUGANIS
   - **AWS Secret Access Key:** proporcionada por LUGANIS
   - **Default region name:** `us-east-1`
   - **Default output format:** `None`

---

## 4. Estructura de directorios (8.3)

| Directorio | Función |
|------------|---------|
| **IN/RNC** | Entrada: archivos TXT a procesar |
| **IN-BACKUP/RNC** | Copia de seguridad de los TXT depositados en IN |
| **OUT/RNC** | Salida: archivos de respuesta con el estado de cada documento |
| **OUT-BACKUP/RNC** | Copia de seguridad de los archivos de respuesta |
| **ERRORS/RNC** | Trazas de error en caso de fallos de procesamiento |

---

## 5. Envío de archivos TXT en lote (8.4)

### Inicio del procesamiento

- Depositar los archivos TXT en la ruta **IN/RNC**.
- El servicio detecta y procesa los archivos de forma automática.

### Nombre del archivo

Formato: `RNC_Emisor + Tipo_Comprobante + Fecha_envío (DDMMYYYYHHMMSS)`

**Ejemplo:** `132944372E3116042024090025.txt`

- `132944372` – RNC del emisor
- `E31` – Tipo de comprobante
- `16042024090025` – 16/04/2024 09:00:25

### Comando de subida (AWS S3)

```bash
aws s3 cp /RUTA_ORIGEN/ARCHIVO_A_SUBIR.txt s3://RUTA_DESTINO/
```

**Ejemplo:**

```bash
aws s3 cp /MiDirectorioLocal/132944372E3116042024153206.txt s3://IN/132944372
```

---

## 6. Consulta de respuestas / estatus (8.5)

- Al terminar el procesamiento, se genera un archivo de salida en **OUT/RNC**.
- Nombre del archivo de respuesta: **R-** + mismo nombre del archivo enviado.
- Ejemplo: `R-132944372E3116042024153206.txt`

### Formato del archivo de respuesta (layout)

Campos separados por pipe `|`:

```
ENCF | Código Respuesta | Mensaje Respuesta | Fecha de Procesamiento
```

### Comando de descarga (AWS S3)

```bash
aws s3 cp s3://RUTA_DE_ARCHIVO/ARCHIVO_A_DESCARGAR.txt /RUTA_DE_DESCARGA/
```

**Ejemplo:**

```bash
aws s3 cp s3://OUT/132944372/R-132944372E3116042024153206.txt /MiDirectoriolocal/
```

---

## 7. Reglas y límites

| Regla | Valor |
|-------|-------|
| Un solo tipo de e-CF por archivo TXT | Sí |
| Separador entre tramas | 15 asteriscos `***************` |
| Máximo de comprobantes por archivo | 25.000 |
| Tamaño máximo por trama interna | 10.000 caracteres |
| Tamaño máximo por archivo TXT | 1,50 MB |
| Envíos consecutivos | Máximo 3 archivos seguidos |
| Codificación | UTF-8 sin BOM |
| Nombre del archivo | RNC_Emisor + Tipo_Comprobante + DDMMYYYYHHMMSS |
