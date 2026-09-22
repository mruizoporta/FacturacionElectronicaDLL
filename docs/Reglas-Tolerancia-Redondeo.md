# Reglas de Tolerancia y Redondeo – LUGANIS

Referencia extraída del Manual de integración usuario Plataforma Facturación Electrónica RD (LUGANIS CORP).

---

## 1. Reglas de Tolerancia (5.2)

### i. Tolerancia por Transacción

Cada transacción generada debe reflejar una diferencia de **±1 unidad** en el valor resultante de multiplicar el precio por la cantidad de ítems en cada línea.

| Cantidad | Precio | Monto Item Emisor | Monto calculado | Dif. aceptada |
|----------|--------|-------------------|-----------------|---------------|
| 10.00    | 99.95  | 1,000.50          | 999.50          | 1.00          |
| 50.75    | 145.99 | 7,409.99          | 7,408.99        | 1.00          |
| 5.00     | 255.30 | 1,277.45          | 1,276.50        | 0.95          |

### ii. Tolerancia Global

El monto total del e-CF debe ser equivalente a la suma de las líneas de "Detalle de Bienes o Servicios". La tolerancia global no debe ser mayor que la **cantidad de líneas de Items**.

---

## 2. Reglas de Redondeo (5.3)

### Regla general

Los campos numéricos permiten hasta **dos (2) decimales**, redondeados según:

- **Tercer decimal < 5** → se mantiene el segundo decimal (ej.: 1,150.2134 → 1,150.21).
- **Tercer decimal ≥ 5** → se incrementa el segundo decimal (ej.: 1,150.2196 → 1,150.22).

### Excepciones (más decimales permitidos)

| Campo | Sección | Decimales permitidos | Notas |
|-------|---------|----------------------|-------|
| Precio Unitario Ítem | Detalle Bienes/Servicios | 4 | |
| Precio Unitario Ítem Otra Moneda | Detalle Bienes/Servicios | 4 | |
| Tipo de Cambio | Encabezado | 4 | Según Banco Central |
| Subcantidad | Detalle Bienes/Servicios | 3 | |
