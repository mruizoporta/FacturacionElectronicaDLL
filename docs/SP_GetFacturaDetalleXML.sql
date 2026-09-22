/*
  dbo.GetFacturaDetalleXML — mismo resultado que SELECT * FROM vwFacturasDetalleXML WHERE emp/suc/forma/tfa/numero = ...

  Por qué un SP y no solo la vista:
  - La vista con CTE "FacturasConLineaItbis1618" sobre TODA Det_Factura + Productos puede costar mucho aunque el WHERE filtre una factura.
  - Aquí el filtro por factura está en el FROM desde el inicio y el "¿hay línea 16/18?" es OUTER APPLY solo para esa factura.

  Requisito: misma lógica que docs/vwFacturasDetalleXML_OPTIM_rendimiento.sql (Luganis, construcción, etc.).

  Si dbo.fn_calcularPrecioSinITBIS es escalar antigua (sin INLINE), puede seguir siendo el cuello de botella:
  conviértela a función inline escalar (SQL 2019+) o sustituye la fórmula aquí.

  Ejecutar en la base correcta (sin USE fijo en el script).
  Idempotente: SQL 2008 R2+ (sin CREATE OR ALTER).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.GetFacturaDetalleXML', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.GetFacturaDetalleXML @emp_codigo INT, @suc_codigo INT, @fac_forma NVARCHAR(10), @tfa_codigo NVARCHAR(20), @fac_numero INT AS SET NOCOUNT ON;');
GO

ALTER PROCEDURE [dbo].[GetFacturaDetalleXML]
    @emp_codigo INT,
    @suc_codigo INT,
    @fac_forma  NVARCHAR(10),
    @tfa_codigo NVARCHAR(20),  /* mismo criterio que vista: tfa_codigo='valor' (texto); evita desajuste INT vs columna CHAR/VARCHAR */
    @fac_numero INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH DetalleBase AS (
        SELECT
            f.fac_forma,
            f.tfa_codigo,
            f.fac_numero,
            f.emp_codigo,
            f.suc_codigo,
            p.servicio_construccion AS EmpServicioConstruccion,
            pro.with_servicio_construccion AS ProServicioConstruccion,
            pro.pro_montoitbis,
            df.det_cantidad AS CantidadItem,
            ISNULL(u.Cod_DGI, 43) AS UnidadMedida,
            CONVERT(VARCHAR(10), f.fac_fecha, 110) AS FechaElaboracion,
            df.det_descuento,
            f.Recargo,
            ISNULL(tc.TotalCantidad, 0) AS TotalCantidadDocumento,
            CASE WHEN ISNULL(p.par_luganis_baseurl, '') <> '' THEN 1 ELSE 0 END AS LuganisActivo,

            ROW_NUMBER() OVER (
                PARTITION BY f.emp_codigo, f.suc_codigo, f.fac_forma, f.tfa_codigo, f.fac_numero
                ORDER BY df.det_secuencia, df.pro_codigo
            ) AS NumeroLineaBase,

            'Interna' AS TipoCodigo,
            df.pro_codigo AS CodigoItem,

            CASE
                WHEN ISNULL(f.FAC_ITBIS, 0) > 0
                     AND itb.Hit IS NULL
                     AND ISNULL(pro.pro_montoitbis, 0) = 0
                THEN '1'
                WHEN pro.pro_montoitbis = 18 AND ISNULL(df.det_conitbis, '') <> 'N' THEN '1'
                WHEN pro.pro_montoitbis = 16 AND ISNULL(df.det_conitbis, '') <> 'N' THEN '2'
                ELSE '4'
            END AS IndicadorFacturacion,

            LEFT(pro.pro_nombre, 80) AS NombreItem,

            CASE WHEN ISNULL(f.dgi, 0) = 1 THEN df.pro_nombre ELSE pro.pro_nombre END AS DescripcionItem,

            CASE
                WHEN pro.pro_servicio = 'true' THEN '2'
                WHEN pro.pro_servicio = 'false' THEN '1'
                ELSE '0'
            END AS IndicadorBienoServicio,

            df.det_precio AS PrecioUnitarioOriginal,

            CASE
                WHEN ISNULL(f.dgi, 0) = 1 THEN 1
                WHEN p.par_itbisincluido = 'True' THEN 1
                ELSE 0
            END AS precio_itbis_incluido,

            1 + (pro.pro_montoitbis / 100.0) AS Factoritbis,

            CASE
                WHEN p.par_itbisincluido = 'True'
                THEN dbo.fn_calcularPrecioSinITBIS(df.det_precio, pro.pro_montoitbis, 1)
                ELSE df.det_precio
            END AS PrecioUnitarioItemBase,

            CASE
                WHEN p.par_itbisincluido = 'True'
                THEN dbo.fn_calcularPrecioSinITBIS(df.det_precio, pro.pro_montoitbis, 1)
                ELSE df.det_precio
            END * df.det_cantidad AS MontoBrutoBase,

            (CASE
                WHEN p.par_itbisincluido = 'True'
                THEN dbo.fn_calcularPrecioSinITBIS(df.det_precio, pro.pro_montoitbis, 1)
                ELSE df.det_precio
             END * df.det_cantidad) * (ISNULL(df.det_descuento, 0) / 100.0) AS DescuentoMontoBase,

            CAST(
                CASE
                    WHEN ISNULL(f.Recargo, 0) > 0
                    THEN (ISNULL(f.Recargo, 0) / NULLIF(tc.TotalCantidad, 0)) * ISNULL(df.det_cantidad, 0)
                    ELSE 0
                END
            AS DECIMAL(18, 2)) AS RecargoMontoBase,

            CASE
                WHEN ISNULL(df.det_descuento, 0) > 0 THEN '$'
                ELSE subdes.TipoSubDescuento_1
            END AS TipoSubDescuento_1,

            CASE
                WHEN ISNULL(df.det_descuento, 0) > 0 THEN
                    (CASE
                        WHEN p.par_itbisincluido = 'True'
                        THEN dbo.fn_calcularPrecioSinITBIS(df.det_precio, pro.pro_montoitbis, 1)
                        ELSE df.det_precio
                     END * df.det_cantidad) * (ISNULL(df.det_descuento, 0) / 100.0)
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

            0 AS cantidadreferencia,
            0 AS unidadreferencia,
            '0' AS subcantidad,
            '' AS codigosubcantidad,
            0 AS gradosalcohol,
            0 AS preciounitarioreferencia,
            '' AS tipoimpuesto1,
            '' AS tipoimpuesto2
        FROM dbo.FACTURAS f
        INNER JOIN dbo.parametros p
            ON f.emp_codigo = p.emp_codigo
        /* Una sola vez por factura (no por línea de detalle): total cantidades e indicador ITBIS 16/18 */
        OUTER APPLY (
            SELECT SUM(ISNULL(df3.det_cantidad, 0)) AS TotalCantidad
            FROM dbo.Det_Factura df3
            WHERE df3.emp_codigo = f.emp_codigo
              AND df3.suc_codigo = f.suc_codigo
              AND df3.fac_forma = f.fac_forma
              AND df3.tfa_codigo = f.tfa_codigo
              AND df3.fac_numero = f.fac_numero
        ) tc
        OUTER APPLY (
            SELECT TOP (1) 1 AS Hit
            FROM dbo.Det_Factura df2
            INNER JOIN dbo.Productos pro2
                ON pro2.emp_codigo = df2.emp_codigo
               AND pro2.pro_codigo = df2.pro_codigo
            WHERE df2.emp_codigo = f.emp_codigo
              AND df2.suc_codigo = f.suc_codigo
              AND df2.fac_forma = f.fac_forma
              AND df2.tfa_codigo = f.tfa_codigo
              AND df2.fac_numero = f.fac_numero
              AND ISNULL(pro2.pro_montoitbis, 0) IN (16, 18)
        ) itb
        INNER JOIN dbo.Det_Factura df
            ON f.emp_codigo = df.emp_codigo
           AND f.suc_codigo = df.suc_codigo
           AND f.fac_forma = df.fac_forma
           AND f.tfa_codigo = df.tfa_codigo
           AND f.fac_numero = df.fac_numero
        INNER JOIN dbo.Productos pro
            ON pro.pro_codigo = df.pro_codigo
           AND pro.emp_codigo = df.emp_codigo
        LEFT JOIN dbo.UnidadMedida u
            ON CAST(u.UnidadID AS VARCHAR(1)) = CAST(pro.UnidadID AS VARCHAR(1))
        LEFT JOIN dbo.SubDescuento_DGI subdes
            ON subdes.emp_codigo = df.emp_codigo
           AND subdes.fac_numero = df.fac_numero
           AND subdes.pro_codigo = df.pro_codigo
        /* RTRIM: fac_forma en tablas suele ser CHAR(n) con espacios; la consulta antigua comparaba con literal sin relleno */
        WHERE f.emp_codigo = @emp_codigo
          AND f.suc_codigo = @suc_codigo
          AND RTRIM(f.fac_forma) = RTRIM(@fac_forma)
          /* Sin TRY_CONVERT (compatible SQL 2008 R2+) */
          AND (
                (LEN(LTRIM(RTRIM(@tfa_codigo))) > 0
                 AND ISNUMERIC(LTRIM(RTRIM(@tfa_codigo)) + N'e0') = 1
                 AND f.tfa_codigo = CAST(LTRIM(RTRIM(@tfa_codigo)) AS INT))
             OR LTRIM(RTRIM(CONVERT(NVARCHAR(40), f.tfa_codigo))) = LTRIM(RTRIM(@tfa_codigo))
              )
          AND f.fac_numero = @fac_numero
    ),
    DetalleConMontos AS (
        SELECT
            *,
            (MontoBrutoBase * (ISNULL(100 - ISNULL(det_descuento, 0), 0) / 100.0) + RecargoMontoBase) AS MontoItemOriginal
        FROM DetalleBase
    ),
    Lineas AS (
        SELECT
            0 AS EsServicio,
            fac_forma, tfa_codigo,
            precio_itbis_incluido,
            fac_numero, emp_codigo, suc_codigo,
            NumeroLineaBase,
            TipoCodigo,
            CodigoItem,
            CASE
                WHEN EmpServicioConstruccion = 1 AND ProServicioConstruccion = 1 THEN '4'
                ELSE IndicadorFacturacion
            END AS IndicadorFacturacion,
            NombreItem,
            DescripcionItem,
            CantidadItem,
            UnidadMedida,
            FechaElaboracion,
            CASE
                WHEN EmpServicioConstruccion = 1 AND ProServicioConstruccion = 1 AND CantidadItem <> 0
                THEN (MontoItemOriginal * 0.90) / CantidadItem
                WHEN (EmpServicioConstruccion = 0 OR ProServicioConstruccion = 0) AND LuganisActivo = 1 AND CantidadItem <> 0
                THEN (PrecioUnitarioItemBase * (1 + ISNULL(pro_montoitbis, 0) / 100.0))
                ELSE PrecioUnitarioItemBase
            END AS PrecioUnitarioItem,
            DescuentoMontoBase AS DescuentoMonto,
            RecargoMontoBase AS RecargoMonto,
            CASE
                WHEN EmpServicioConstruccion = 1 AND ProServicioConstruccion = 1
                THEN MontoItemOriginal * 0.90
                WHEN (EmpServicioConstruccion = 0 OR ProServicioConstruccion = 0) AND LuganisActivo = 1
                THEN MontoItemOriginal * (1 + ISNULL(pro_montoitbis, 0) / 100.0)
                ELSE MontoItemOriginal
            END AS MontoItem,
            TipoSubDescuento_1,
            MontoSubDescuento_1,
            TipoSubDescuento_2,
            MontoSubDescuento_2,
            MontosubRecargo_1,
            TipoSubRecargo_1,
            MontosubRecargo_2,
            TipoSubRecargo_2,
            SubRecargoPorcentaje_1,
            SubRecargoPorcentaje_2,
            cantidadreferencia,
            unidadreferencia,
            subcantidad,
            codigosubcantidad,
            gradosalcohol,
            preciounitarioreferencia,
            tipoimpuesto1,
            tipoimpuesto2,
            IndicadorBienoServicio,
            CASE
                WHEN EmpServicioConstruccion = 1 AND ProServicioConstruccion = 1 THEN 0
                ELSE pro_montoitbis
            END AS pro_montoitbis,
            PrecioUnitarioOriginal
        FROM DetalleConMontos

        UNION ALL

        SELECT
            1 AS EsServicio,
            fac_forma, tfa_codigo,
            precio_itbis_incluido,
            fac_numero, emp_codigo, suc_codigo,
            NumeroLineaBase,
            TipoCodigo,
            CodigoItem,
            '1' AS IndicadorFacturacion,
            'SERVICIO' AS NombreItem,
            'SERVICIO' AS DescripcionItem,
            1 AS CantidadItem,
            UnidadMedida,
            FechaElaboracion,
            (MontoItemOriginal * 0.10) * (1 + pro_montoitbis / 100.0) AS PrecioUnitarioItem,
            0 AS DescuentoMonto,
            0 AS RecargoMonto,
            (MontoItemOriginal * 0.10) * (1 + pro_montoitbis / 100.0) AS MontoItem,
            TipoSubDescuento_1,
            0 AS MontoSubDescuento_1,
            TipoSubDescuento_2,
            0 AS MontoSubDescuento_2,
            MontosubRecargo_1,
            TipoSubRecargo_1,
            MontosubRecargo_2,
            TipoSubRecargo_2,
            SubRecargoPorcentaje_1,
            SubRecargoPorcentaje_2,
            cantidadreferencia,
            unidadreferencia,
            subcantidad,
            codigosubcantidad,
            gradosalcohol,
            preciounitarioreferencia,
            tipoimpuesto1,
            tipoimpuesto2,
            '2' AS IndicadorBienoServicio,
            pro_montoitbis,
            PrecioUnitarioOriginal
        FROM DetalleConMontos
        WHERE EmpServicioConstruccion = 1
          AND ProServicioConstruccion = 1
    )
    SELECT
        fac_forma,
        tfa_codigo,
        precio_itbis_incluido,
        fac_numero,
        emp_codigo,
        suc_codigo,
        ROW_NUMBER() OVER (
            PARTITION BY emp_codigo, suc_codigo, fac_forma, tfa_codigo, fac_numero
            ORDER BY NumeroLineaBase, EsServicio
        ) AS NumeroLinea,
        TipoCodigo,
        CodigoItem,
        IndicadorFacturacion,
        NombreItem,
        DescripcionItem,
        CantidadItem,
        UnidadMedida,
        FechaElaboracion,
        PrecioUnitarioItem,
        DescuentoMonto,
        MontoItem,
        TipoSubDescuento_1,
        MontoSubDescuento_1,
        TipoSubDescuento_2,
        MontoSubDescuento_2,
        RecargoMonto,
        MontosubRecargo_1,
        TipoSubRecargo_1,
        MontosubRecargo_2,
        TipoSubRecargo_2,
        SubRecargoPorcentaje_1,
        SubRecargoPorcentaje_2,
        cantidadreferencia,
        unidadreferencia,
        subcantidad,
        codigosubcantidad,
        gradosalcohol,
        preciounitarioreferencia,
        tipoimpuesto1,
        tipoimpuesto2,
        IndicadorBienoServicio,
        pro_montoitbis,
        PrecioUnitarioOriginal
    FROM Lineas
    OPTION (RECOMPILE);
END
GO

/*
  Prueba:
  EXEC dbo.GetFacturaDetalleXML @emp_codigo=1,@suc_codigo=1,@fac_forma=N'A',@tfa_codigo=N'1',@fac_numero=264319;
*/
