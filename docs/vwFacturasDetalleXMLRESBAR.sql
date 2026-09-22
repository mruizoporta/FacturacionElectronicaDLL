/*
  Detalle e-CF para flujo RESBAR (mismas columnas que consume GenerarXML en FacturaElectronicaService).

  IMPORTANTE — dbo.fn_calcularPrecioSinITBIS
  -----------------------------------------
  NO está en vwFacturasXMLRESBAR (vista de encabezado). Solo aplica aquí, en vwFacturasDetalleXMLRESBAR.

  Dentro del CTE "DetalleBase", la función aparece 4 veces, siempre bajo la misma condición:
    EXISTS (PARAMETROS.par_itbisincluido = 'True' para la empresa de la línea)
  y entonces:
    dbo.fn_calcularPrecioSinITBIS(fi.Precio, ISNULL(pro.pro_montoitbis, 0), 1)

  Ubicación en el SQL más abajo (buscar "fn_calcularPrecioSinITBIS"):
    1) Columna calculada PrecioUnitarioItemBase
    2) Columna MontoBrutoBase (precio sin ITBIS * cantidad)
    3) Columna DescuentoMontoBase (parte del descuento % sobre base sin ITBIS)
    4) Columna MontoSubDescuento_1 (rama cuando hay Descuento % en la línea)

  Si la función no existe en la BD, sustituir esas expresiones por la misma lógica que use vwFacturasDetalleXML.

  Filtro DLL:
    fac_numero = FacturaID, usu_codigo = CajeroID, caja = CajaID, emp_codigo, suc_codigo

  Basado en la idea de vwFacturasDetalleXML pero:
    - Origen: Factura_Items + Factura_RestBar + Productos (+ PARAMETROS, UnidadMedida, SubDescuento_DGI opcional)
    - Sin servicio de construcción (sin línea extra ni 90/10 %)
    - Sin Luganis (sin ajuste de precio/monto por par_luganis_baseurl)
    - Sin reglas por RNC emisor

  Base de datos: coral
*/
USE [coral];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER VIEW dbo.vwFacturasDetalleXMLRESBAR
AS
WITH TotCant AS (
    SELECT
        fi.EMP_CODIGO,
        fi.SUC_CODIGO,
        fi.FacturaID,
        SUM(ISNULL(fi.Cantidad, 0)) AS TotalCantidad
    FROM dbo.Factura_Items AS fi WITH (NOLOCK)
    GROUP BY fi.EMP_CODIGO, fi.SUC_CODIGO, fi.FacturaID
),
DetalleBase AS (
    SELECT
        rb.FacturaID,
        rb.CajeroID,
        rb.CajaID,
        rb.EMP_CODIGO,
        rb.SUC_CODIGO,
        fi.ProductoID,
        pro.pro_montoitbis,
        ISNULL(fi.Cantidad, 0) AS CantidadItem,
        ISNULL(u.Cod_DGI, 43) AS UnidadMedida,
        CONVERT(VARCHAR(10), ISNULL(rb.Fecha, GETDATE()), 110) AS FechaElaboracion,
        fi.Descuento AS det_descuento,
        ISNULL(rb.Recargo, 0) AS RecargoHeader,
        ISNULL(tc.TotalCantidad, 0) AS TotalCantidadDocumento,

        ROW_NUMBER() OVER (
            PARTITION BY fi.EMP_CODIGO, fi.SUC_CODIGO, fi.FacturaID
            ORDER BY fi.DetalleID, fi.ProductoID
        ) AS NumeroLineaBase,

        CASE
            WHEN ISNULL(rb.Itbis, 0) > 0
                 AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.Factura_Items AS fi2 WITH (NOLOCK)
                    INNER JOIN dbo.Productos AS pro2 WITH (NOLOCK)
                        ON pro2.emp_codigo = fi2.EMP_CODIGO
                       AND pro2.pro_codigo = fi2.ProductoID
                    WHERE fi2.EMP_CODIGO = rb.EMP_CODIGO
                      AND fi2.SUC_CODIGO = rb.SUC_CODIGO
                      AND fi2.FacturaID = rb.FacturaID
                      AND ISNULL(pro2.pro_montoitbis, 0) IN (16, 18)
                 )
                 AND ISNULL(pro.pro_montoitbis, 0) = 0
            THEN '1'
            WHEN ISNULL(pro.pro_montoitbis, 0) = 18 THEN '1'
            WHEN ISNULL(pro.pro_montoitbis, 0) = 16 THEN '2'
            ELSE '4'
        END AS IndicadorFacturacion,

        LEFT(ISNULL(NULLIF(LTRIM(RTRIM(fi.Nombre)), ''), pro.pro_nombre), 80) AS NombreItem,

        CASE
            WHEN pro.pro_servicio = 'true' THEN '2'
            WHEN pro.pro_servicio = 'false' THEN '1'
            ELSE '0'
        END AS IndicadorBienoServicio,

        fi.Precio AS PrecioUnitarioOriginal,

        ISNULL(
            CASE
                WHEN EXISTS (
                    SELECT 1
                    FROM dbo.PARAMETROS AS pa WITH (NOLOCK)
                    WHERE pa.emp_codigo = fi.EMP_CODIGO
                      AND pa.par_itbisincluido = 'True'
                ) THEN 1
                ELSE 0
            END, 0
        ) AS precio_itbis_incluido,

        /* --- fn_calcularPrecioSinITBIS #1 → PrecioUnitarioItemBase --- */
        CASE
            WHEN EXISTS (
                SELECT 1
                FROM dbo.PARAMETROS AS pa WITH (NOLOCK)
                WHERE pa.emp_codigo = fi.EMP_CODIGO
                  AND pa.par_itbisincluido = 'True'
            )
            THEN dbo.fn_calcularPrecioSinITBIS(fi.Precio, ISNULL(pro.pro_montoitbis, 0), 1)
            ELSE fi.Precio
        END AS PrecioUnitarioItemBase,

        /* --- fn_calcularPrecioSinITBIS #2 → factor en MontoBrutoBase --- */

        CASE
            WHEN EXISTS (
                SELECT 1
                FROM dbo.PARAMETROS AS pa WITH (NOLOCK)
                WHERE pa.emp_codigo = fi.EMP_CODIGO
                  AND pa.par_itbisincluido = 'True'
            )
            THEN dbo.fn_calcularPrecioSinITBIS(fi.Precio, ISNULL(pro.pro_montoitbis, 0), 1)
            ELSE fi.Precio
        END * ISNULL(fi.Cantidad, 0) AS MontoBrutoBase,

        /* --- fn_calcularPrecioSinITBIS #3 → DescuentoMontoBase --- */
        (CASE
            WHEN EXISTS (
                SELECT 1
                FROM dbo.PARAMETROS AS pa WITH (NOLOCK)
                WHERE pa.emp_codigo = fi.EMP_CODIGO
                  AND pa.par_itbisincluido = 'True'
            )
            THEN dbo.fn_calcularPrecioSinITBIS(fi.Precio, ISNULL(pro.pro_montoitbis, 0), 1)
            ELSE fi.Precio
         END * ISNULL(fi.Cantidad, 0))
        * (ISNULL(fi.Descuento, 0) / 100.0) AS DescuentoMontoBase,

        CAST(ISNULL(
            CASE
                WHEN ISNULL(rb.Recargo, 0) > 0 AND ISNULL(tc.TotalCantidad, 0) <> 0
                THEN (ISNULL(rb.Recargo, 0) / tc.TotalCantidad) * ISNULL(fi.Cantidad, 0)
                ELSE 0
            END, 0) AS DECIMAL(18, 2)) AS RecargoMontoBase,

        CASE
            WHEN ISNULL(fi.Descuento, 0) > 0 THEN '$'
            ELSE subdes.TipoSubDescuento_1
        END AS TipoSubDescuento_1,

        /* --- fn_calcularPrecioSinITBIS #4 → MontoSubDescuento_1 (línea con Descuento %) --- */
        CASE
            WHEN ISNULL(fi.Descuento, 0) > 0 THEN
                (CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM dbo.PARAMETROS AS pa WITH (NOLOCK)
                        WHERE pa.emp_codigo = fi.EMP_CODIGO
                          AND pa.par_itbisincluido = 'True'
                    )
                    THEN dbo.fn_calcularPrecioSinITBIS(fi.Precio, ISNULL(pro.pro_montoitbis, 0), 1)
                    ELSE fi.Precio
                 END * ISNULL(fi.Cantidad, 0)) * (ISNULL(fi.Descuento, 0) / 100.0)
            ELSE ISNULL(subdes.MontoSubDescuento_1, 0)
        END AS MontoSubDescuento_1,

        subdes.TipoSubDescuento_2,
        subdes.MontoSubDescuento_2,

        CAST(0 AS DECIMAL(18, 2)) AS MontosubRecargo_1,
        '' AS TipoSubRecargo_1,
        CAST(0 AS DECIMAL(18, 2)) AS MontosubRecargo_2,
        '' AS TipoSubRecargo_2,
        CAST(0 AS DECIMAL(18, 2)) AS SubRecargoPorcentaje_1,
        CAST(0 AS DECIMAL(18, 2)) AS SubRecargoPorcentaje_2,

        CAST(0 AS DECIMAL(18, 4)) AS cantidadreferencia,
        CAST(0 AS DECIMAL(18, 4)) AS unidadreferencia,
        CAST(0.0 AS DECIMAL(18, 2)) AS Subcantidad,
        CAST('' AS VARCHAR(10)) AS CodigoSubcantidad,
        CAST(0 AS DECIMAL(18, 4)) AS gradosalcohol,
        CAST(0 AS DECIMAL(18, 4)) AS preciounitarioreferencia,
        CAST('' AS VARCHAR(20)) AS TipoImpuesto1,
        CAST('' AS VARCHAR(20)) AS TipoImpuesto2
    FROM dbo.Factura_RestBar AS rb WITH (NOLOCK)
    INNER JOIN dbo.Factura_Items AS fi WITH (NOLOCK)
        ON fi.FacturaID = rb.FacturaID
       AND fi.EMP_CODIGO = rb.EMP_CODIGO
       AND fi.SUC_CODIGO = rb.SUC_CODIGO
    INNER JOIN dbo.Productos AS pro WITH (NOLOCK)
        ON pro.pro_codigo = fi.ProductoID
       AND pro.emp_codigo = fi.EMP_CODIGO
    LEFT JOIN TotCant AS tc
        ON tc.EMP_CODIGO = fi.EMP_CODIGO
       AND tc.SUC_CODIGO = fi.SUC_CODIGO
       AND tc.FacturaID = fi.FacturaID
    LEFT JOIN dbo.UnidadMedida AS u WITH (NOLOCK)
        ON CAST(u.UnidadID AS VARCHAR(10)) = CAST(pro.UnidadID AS VARCHAR(10))
    LEFT JOIN dbo.SubDescuento_DGI AS subdes WITH (NOLOCK)
        ON subdes.emp_codigo = fi.EMP_CODIGO
       AND subdes.fac_numero = fi.FacturaID
       AND subdes.pro_codigo = fi.ProductoID
),
DetalleConMontos AS (
    SELECT
        *,
        (MontoBrutoBase * (ISNULL(100 - ISNULL(det_descuento, 0), 0) / 100.0) + RecargoMontoBase) AS MontoItemOriginal
    FROM DetalleBase
)
SELECT
    CAST(d.FacturaID AS INT) AS fac_numero,
    CAST(d.CajeroID AS VARCHAR(50)) AS usu_codigo,
    CAST(d.CajaID AS VARCHAR(50)) AS caja,
    d.EMP_CODIGO AS emp_codigo,
    d.SUC_CODIGO AS suc_codigo,
    CAST(N'RB' AS VARCHAR(10)) AS fac_forma,
    CAST(1 AS INT) AS tfa_codigo,
    d.precio_itbis_incluido,

    ROW_NUMBER() OVER (
        PARTITION BY d.EMP_CODIGO, d.SUC_CODIGO, d.FacturaID, d.CajeroID, d.CajaID
        ORDER BY d.NumeroLineaBase
    ) AS NumeroLinea,

    CAST(N'Interna' AS VARCHAR(20)) AS TipoCodigo,
    d.ProductoID AS CodigoItem,
    d.IndicadorFacturacion,
    d.NombreItem,
    d.NombreItem AS DescripcionItem,
    d.CantidadItem,
    d.UnidadMedida,
    d.FechaElaboracion,
    d.PrecioUnitarioItemBase AS PrecioUnitarioItem,
    d.DescuentoMontoBase AS DescuentoMonto,
    d.MontoItemOriginal AS MontoItem,
    d.TipoSubDescuento_1,
    d.MontoSubDescuento_1,
    d.TipoSubDescuento_2,
    d.MontoSubDescuento_2,
    d.RecargoMontoBase AS RecargoMonto,
    d.MontosubRecargo_1,
    d.TipoSubRecargo_1,
    d.MontosubRecargo_2,
    d.TipoSubRecargo_2,
    d.SubRecargoPorcentaje_1,
    d.SubRecargoPorcentaje_2,
    d.cantidadreferencia,
    d.unidadreferencia,
    d.Subcantidad,
    d.CodigoSubcantidad,
    d.gradosalcohol,
    d.preciounitarioreferencia,
    d.TipoImpuesto1,
    d.TipoImpuesto2,
    d.IndicadorBienoServicio,
    d.pro_montoitbis,
    d.PrecioUnitarioOriginal
FROM DetalleConMontos AS d;
GO
