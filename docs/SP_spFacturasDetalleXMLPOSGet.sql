/*
  dbo.spFacturasDetalleXMLPOSGet

  Equivalente filtrado a vwFacturasDetalleXMLPOS (misma proyección y ROW_NUMBER).
  Filtra por ticket (fac_numero), cajero y caja para acotar lecturas.

  Parámetros:
    @emp_codigo, @suc_codigo, @fac_numero (ticket), @usu_codigo, @caja

  DLL: FacturaElectronicaService — EXEC dbo.spFacturasDetalleXMLPOSGet ...

  Índices: docs/IX_vwFacturasDetalleXMLPOS_support.sql (sección SP detalle).

  Ajustar USE antes de ejecutar.
*/
USE [l1];
GO

SET ANSI_NULLS ON;
GO

SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spFacturasDetalleXMLPOSGet]
    @emp_codigo   INT,
    @suc_codigo   INT,
    @fac_numero   INT,
    @usu_codigo   VARCHAR(50),
    @caja         VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        usu_codigo,
        caja,
        fac_fecha,
        precio_itbis_incluido,
        fac_numero,
        emp_codigo,
        suc_codigo,
        NumeroLinea = ROW_NUMBER() OVER (
            PARTITION BY emp_codigo, suc_codigo, usu_codigo, caja, fac_numero
            ORDER BY NumeroLineaOriginal
        ),
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
    FROM (
        SELECT
            df.secuencia AS NumeroLineaOriginal,
            df.descuento AS det_descuento,
            f.usu_codigo,
            f.caja,
            CAST(ISNULL(par_emp.itbis_incluido, 0) AS INT) AS precio_itbis_incluido,
            1 + (pro.pro_montoitbis / 100.0) AS Factoritbis,
            f.ticket AS fac_numero,
            f.emp_codigo,
            f.suc_codigo,
            'Interna' AS TipoCodigo,
            df.producto AS CodigoItem,
            CASE pro.pro_montoitbis
                WHEN 18 THEN '1'
                WHEN 16 THEN '2'
                ELSE '4'
            END AS IndicadorFacturacion,
            LEFT(pro.pro_nombre, 80) AS NombreItem,
            pro.pro_montoitbis,
            pro.pro_nombre AS DescripcionItem,
            df.cantidad AS CantidadItem,
            ISNULL(u.Cod_DGI, 43) AS UnidadMedida,
            CONVERT(VARCHAR(10), f.fecha, 110) AS FechaElaboracion,
            f.fecha AS fac_fecha,
            CASE
                WHEN ISNULL(par_emp.itbis_incluido, 0) = 1
                THEN dbo.fn_calcularPrecioSinITBIS(df.precio, pro.pro_montoitbis, 1)
                ELSE df.precio
            END AS PrecioUnitarioItem,
            (CASE
                WHEN ISNULL(par_emp.itbis_incluido, 0) = 1
                THEN dbo.fn_calcularPrecioSinITBIS(df.precio, pro.pro_montoitbis, 1)
                ELSE df.precio
             END * df.cantidad) * (ISNULL(df.descuento, 0) / 100.0) AS DescuentoMonto,
            (CASE
                WHEN ISNULL(par_emp.itbis_incluido, 0) = 1
                THEN dbo.fn_calcularPrecioSinITBIS(df.precio, pro.pro_montoitbis, 1)
                ELSE df.precio
             END * df.cantidad) * (ISNULL(100 - ISNULL(df.descuento, 0), 0) / 100.0) AS MontoItem,
            CASE WHEN ISNULL(df.descuento, 0) > 0 THEN '$' END AS TipoSubDescuento_1,
            CASE
                WHEN ISNULL(df.descuento, 0) > 0 THEN
                    (CASE
                        WHEN ISNULL(par_emp.itbis_incluido, 0) = 1
                        THEN dbo.fn_calcularPrecioSinITBIS(df.precio, pro.pro_montoitbis, 1)
                        ELSE df.precio
                     END * df.cantidad) * (ISNULL(df.descuento, 0) / 100.0)
                ELSE 0
            END AS MontoSubDescuento_1,
            0 AS TipoSubDescuento_2,
            0 AS MontoSubDescuento_2,
            0 AS RecargoMonto,
            0 AS MontosubRecargo_1,
            0 AS TipoSubRecargo_1,
            0 AS MontosubRecargo_2,
            0 AS TipoSubRecargo_2,
            0 AS SubRecargoPorcentaje_1,
            0 AS SubRecargoPorcentaje_2,
            0 AS cantidadreferencia,
            0 AS unidadreferencia,
            '0' AS subcantidad,
            '' AS codigosubcantidad,
            0 AS gradosalcohol,
            0 AS preciounitarioreferencia,
            '' AS tipoimpuesto1,
            '' AS tipoimpuesto2,
            CASE
                WHEN pro.pro_servicio = 'true' THEN '2'
                WHEN pro.pro_servicio = 'false' THEN '1'
                ELSE '0'
            END AS IndicadorBienoServicio,
            df.precio AS PrecioUnitarioOriginal
        FROM dbo.Montos_Ticket f
        INNER JOIN (
            SELECT
                emp_codigo,
                CAST(MAX(CASE WHEN par_itbisincluido = 'True' THEN 1 ELSE 0 END) AS TINYINT) AS itbis_incluido
            FROM dbo.PARAMETROS
            GROUP BY emp_codigo
        ) AS par_emp
            ON par_emp.emp_codigo = f.emp_codigo
        INNER JOIN dbo.Ticket df
            ON f.emp_codigo = df.emp_codigo
           AND f.suc_codigo = df.suc_codigo
           AND f.usu_codigo = df.usu_codigo
           AND f.fecha = df.fecha
           AND f.caja = df.caja
           AND f.ticket = df.ticket
        INNER JOIN dbo.Productos pro
            ON pro.pro_codigo = df.producto
           AND pro.emp_codigo = df.emp_codigo
        LEFT JOIN dbo.UnidadMedida u
            ON CAST(u.UnidadID AS VARCHAR(1)) = CAST(pro.UnidadID AS VARCHAR(1))
        WHERE f.emp_codigo = @emp_codigo
          AND f.suc_codigo = @suc_codigo
          AND f.ticket = @fac_numero
          AND f.usu_codigo = @usu_codigo
          AND f.caja = @caja
    ) AS detalle;
END;
GO
