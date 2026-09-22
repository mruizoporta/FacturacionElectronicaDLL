/*
  Vista encabezado e-CF para flujo RESBAR (DLL: fac_numero=ticket, usu_codigo, caja).

  Llaves lógicas (alineadas a Factura_RestBar):
    - fac_numero  = FacturaID   (mismo valor que envía la DLL como "ticket")
    - usu_codigo  = CajeroID
    - caja        = CajaID
    - emp_codigo, suc_codigo   = EMP_CODIGO, SUC_CODIGO

  Sin lógica de servicio de construcción ni reglas especiales por RNC emisor (p. ej. 105081105 / 131861717).
  Totales por tasa ITBIS se calculan desde Factura_Items + Productos (pro_montoitbis).
  Sustituir/agregar dbo.fn_*RestBar cuando las definan.

  Formas de pago: docs/vwFormasPagoXMLRESBAR.sql
  Detalle: docs/vwFacturasDetalleXMLRESBAR.sql (ahí sí se usa dbo.fn_calcularPrecioSinITBIS; en esta vista de encabezado no).

  La DLL no consulta Impuestos_adicionales_dgi en flujo RESBAR (isResBar).

  Columnas DGII en Factura_RestBar (ver docs/ALTER_Factura_RestBar_DGII_columns.sql):
  eNCF, codigoseguridad, fechafirma, FechaLimitePago, Enviado_DGII, Error_DGII,
  AceptadoDGII, DGII_*, secuenciaUtilizadaDGI (sin columnas Luganis en RestBar).
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER VIEW dbo.vwFacturasXMLRESBAR
AS
SELECT
    x.*,

    /* Monto gravado total = suma de tasas (compatibilidad con GenerarXML) */
    CAST(ISNULL(x.monto_grabado1, 0) + ISNULL(x.monto_grabado2, 0) + ISNULL(x.monto_grabado3, 0) AS DECIMAL(18, 4)) AS monto_grabado,

    (SELECT TOP 1 ISNULL(DG.fac_itbis_dgi, 0)
       FROM dbo.Facturas_infoDGI AS DG WITH (NOLOCK)
      WHERE DG.emp_codigo = x.emp_codigo
        AND DG.suc_codigo = x.suc_codigo
        AND CONVERT(VARCHAR(50), DG.fac_numero) = CONVERT(VARCHAR(50), x.fac_numero)) AS fac_itbis_dgi,

    x.eNCF AS ncfEnvia,

    CASE WHEN x.precio_itbis_incluido IS NULL THEN 0 ELSE 1 END AS itbis_incluido,

    CAST(
        ISNULL(x.monto_grabado1, 0) + ISNULL(x.monto_grabado2, 0) + ISNULL(x.monto_grabado3, 0)
        + ISNULL(x.fac_itbis, 0)
        + ISNULL(x.monto_exento, 0)
    AS DECIMAL(18, 4)) AS MontoTotalRedondeado

FROM (
    SELECT
        rb.Descuento AS fac_descuento,
        /* eNCF electrónico asignado por DGII; si aún vacío, cae al NCF fiscal de mesa/POS bar */
        COALESCE(NULLIF(LTRIM(RTRIM(CAST(rb.eNCF AS VARCHAR(100)))), ''), CAST(rb.NCF AS VARCHAR(100))) AS eNCF,
        rb.codigoseguridad AS codigoseguridad,
        CAST('RB' AS VARCHAR(10)) AS fac_forma,
        CAST(1 AS INT) AS tfa_codigo,
        rb.TipoNCF AS tip_codigo,
        rb.EMP_CODIGO AS emp_codigo,

        /* Misma convención TipoeCF que facturación estándar (ajustar si TipoNCF no usa estos códigos) */
        CASE rb.TipoNCF
            WHEN 2 THEN 31
            WHEN 1 THEN 32
            WHEN 3 THEN 45
            WHEN 4 THEN 44
            WHEN 5 THEN 46
            WHEN 6 THEN 47
            WHEN 7 THEN 41
            ELSE 43
        END AS TipoeCF,

        CASE
            WHEN ISNULL(rb.Credito, 0) = 1 THEN 2
            ELSE 1
        END AS TipoPago,

        RTRIM(ISNULL(rb.NCF, '')) AS NCF,
        CAST(0 AS INT) AS NCF_Secuencia,

        CASE
            WHEN rb.TipoNCF = 2 AND rb.Fecha IS NULL THEN
                SUBSTRING(CONVERT(VARCHAR(8), GETDATE(), 112), 7, 2) + '-' +
                SUBSTRING(CONVERT(VARCHAR(8), GETDATE(), 112), 5, 2) + '-' +
                SUBSTRING(CONVERT(VARCHAR(8), GETDATE(), 112), 1, 4)
            ELSE
                SUBSTRING(CONVERT(VARCHAR(8), ISNULL(rb.Fecha, GETDATE()), 112), 7, 2) + '-' +
                SUBSTRING(CONVERT(VARCHAR(8), ISNULL(rb.Fecha, GETDATE()), 112), 5, 2) + '-' +
                SUBSTRING(CONVERT(VARCHAR(8), ISNULL(rb.Fecha, GETDATE()), 112), 1, 4)
        END AS fac_vence,

        CASE WHEN ISNULL(rb.Credito, 0) = 1 THEN N'CREDITO' ELSE N'CONTADO' END AS condicion_pago,

        e.emp_rnc,
        CASE WHEN FG.dgi = 1 THEN FG.emp_nombre_dgi    ELSE e.emp_nombre    END AS emp_nombre,
        e.emp_localidad,
        CASE WHEN FG.dgi = 1 THEN FG.emp_direccion_dgi ELSE e.emp_direccion END AS emp_direccion,
        e.emp_telefono,
        CASE WHEN FG.dgi = 1 THEN FG.emp_web_dgi       ELSE e.emp_web       END AS emp_web,
        CASE WHEN FG.dgi = 1 THEN FG.emp_email_dgi     ELSE e.emp_email     END AS emp_email,

        s.suc_nombre,
        rb.CajeroID AS ven_codigo,

        /* --- Claves para la DLL / JOIN con formas de pago y detalle --- */
        CAST(rb.FacturaID AS VARCHAR(50)) AS fac_numero,
        CAST(rb.CajeroID AS VARCHAR(50)) AS usu_codigo,
        CAST(rb.CajaID AS VARCHAR(50)) AS caja,
        rb.SUC_CODIGO AS suc_codigo,

        SUBSTRING(CONVERT(VARCHAR(8), ISNULL(rb.Fecha, GETDATE()), 112), 5, 2) + '-' +
        SUBSTRING(CONVERT(VARCHAR(8), ISNULL(rb.Fecha, GETDATE()), 112), 7, 2) + '-' +
        SUBSTRING(CONVERT(VARCHAR(8), ISNULL(rb.Fecha, GETDATE()), 112), 1, 4) AS fac_fecha,

        CASE
            WHEN ISNULL(rb.rnc, '0') = '0' THEN
                ISNULL(NULLIF(LTRIM(RTRIM(cli.cli_rnc)), ''), cli.cli_rnc)
            ELSE rb.rnc
        END AS cli_rnc,

        CASE
            WHEN ISNULL(rb.rnc, '0') = '0' THEN
                CASE LEN(LTRIM(RTRIM(cli.cli_nombre))) WHEN 0 THEN '' ELSE cli.cli_nombre END
            ELSE ISNULL(rb.Nombre, '')
        END AS cli_nombre,

        N'' AS cli_contacto,
        N'' AS MunicipioComprador,
        N'' AS ProvinciaComprador,

        LTRIM(RTRIM(cli.cli_email)) AS cli_email,
        CASE WHEN LEN(LTRIM(RTRIM(cli.cli_direccion))) = 0 THEN N'ND' ELSE LTRIM(RTRIM(cli.cli_direccion)) END AS cli_direccion,
        cli.cli_localidad,

        FG.municipio_dgi AS municipio,
        FG.provincia_dgi AS provincia,
        CASE WHEN FG.dgi = 1 THEN FG.CodigoVendedor_dgi ELSE CAST(rb.CajeroID AS VARCHAR(255)) END AS CodigoVendedor,
        CASE WHEN FG.dgi = 1 THEN FG.NumeroFacturaInterna_sdi ELSE CAST(rb.FacturaID AS VARCHAR(100)) END AS NumeroFacturaInterna,
        FG.NumeroPedidoInterno_dgi AS NumeroPedidoInterno,
        FG.ZonaVenta_dgi AS ZonaVenta,
        FG.FechaEntrega_dgi AS FechaEntrega,
        FG.FechaOrdenCompra_dgi AS FechaOrdenCompra,
        FG.NumeroOrdenCompra_dgi AS NumeroOrdenCompra,
        FG.emp_telefono2 AS emp_telefono2,
        FG.CodigoInternoComprador_dgi AS CodigoInternoComprador,

        ISNULL(rb.Total, 0) AS fac_total,
        ISNULL(rb.Total, 0) - ISNULL(rb.Pagado, 0) AS ValorPagar,
        ISNULL(FG.DGI, 0) AS dgi,
        CAST(0 AS DECIMAL(18, 4)) AS monto_grabado_dgi,

        ISNULL(rb.Itbis, 0) AS fac_itbis,

        ISNULL(L.sum_monto_itbis_18, 0) AS det_itbis_1_dgi,

        ISNULL(L.sum_monto_itbis_16, 0) AS det_itbis_2_dgi,
        ISNULL(L.sum_monto_itbis_exento, 0) AS det_itbis_3_dgi,

        ISNULL(L.sum_monto_itbis_18, 0) AS det_totalitbis_1,
        ISNULL(L.sum_monto_itbis_16, 0) AS det_totalitbis_2,
        ISNULL(L.sum_monto_itbis_exento, 0) AS det_totalitbis_3,

        ISNULL(FG.TotalITBISPercepcion_dgi, 0) AS TotalITBISPercepcion_dgi,
        ISNULL(FG.numeroContenedor, N'') AS numeroContenedor,
        ISNULL(FG.numeroReferencia, N'') AS numeroReferencia,

        CASE
            WHEN (SELECT par_itbisincluido FROM dbo.PARAMETROS pa WHERE pa.par_itbisincluido = 'True' AND pa.emp_codigo = rb.EMP_CODIGO) IS NULL
            THEN CASE WHEN ISNULL(rb.Grabado, 0) > 0 THEN 1 ELSE 0 END
            ELSE CASE WHEN ISNULL(rb.Grabado, 0) > 0 THEN 1 ELSE 0 END
        END AS IndicadorMontoGravado,

        CASE
            WHEN (SELECT par_itbisincluido FROM dbo.PARAMETROS pa WHERE pa.par_itbisincluido = 'True' AND pa.emp_codigo = rb.EMP_CODIGO) IS NULL
            THEN 0 ELSE 1
        END AS precio_itbis_incluido,

        rb.fechafirma AS fechafirma,

        /* Desglose gravado por líneas (e44 / TipoNCF 4 => todo exento, sin gravados) */
        CASE WHEN rb.TipoNCF = 4 THEN CAST(0 AS DECIMAL(18, 4)) ELSE ISNULL(L.gravado18, 0) END AS monto_grabado1,
        CASE WHEN rb.TipoNCF = 4 THEN CAST(0 AS DECIMAL(18, 4)) ELSE ISNULL(L.gravado16, 0) END AS monto_grabado2,
        CAST(0 AS DECIMAL(18, 4)) AS monto_grabado3,

        CASE WHEN rb.TipoNCF = 4 THEN 0
             WHEN ISNULL(L.gravado18, 0) > 0 THEN 18
             ELSE 0
        END AS det_itbis_1,

        CASE WHEN rb.TipoNCF = 4 THEN 0
             WHEN ISNULL(L.gravado16, 0) > 0 THEN 16
             ELSE 0
        END AS det_itbis_2,

        0 AS det_itbis_3,

        CASE WHEN rb.TipoNCF = 4 THEN ISNULL(rb.Total, 0)
             ELSE ISNULL(rb.Exento, 0) + ISNULL(L.gravado_exento_lines, 0)
        END AS monto_exento,

        rb.FechaLimitePago AS FechaLimitePago

    FROM dbo.Factura_RestBar AS rb WITH (NOLOCK)
    INNER JOIN dbo.Empresas AS e ON e.emp_codigo = rb.EMP_CODIGO
    INNER JOIN dbo.Sucursales AS s ON s.suc_codigo = rb.SUC_CODIGO AND s.emp_codigo = rb.EMP_CODIGO
    LEFT JOIN dbo.Clientes AS cli ON cli.cli_codigo = rb.CLI_CODIGO AND cli.emp_codigo = rb.EMP_CODIGO
    LEFT JOIN dbo.Facturas_infoDGI AS FG ON FG.emp_codigo = rb.EMP_CODIGO
        AND FG.suc_codigo = rb.SUC_CODIGO
        AND CONVERT(VARCHAR(50), FG.fac_numero) = CONVERT(VARCHAR(50), rb.FacturaID)

    OUTER APPLY (
        SELECT
            SUM(CASE WHEN ISNULL(pro.pro_montoitbis, 0) = 18 THEN
                    (ISNULL(fi.Cantidad, 0) * ISNULL(fi.Precio, 0))
                    - CASE
                        WHEN ISNULL(fi.Descuento, 0) BETWEEN 0 AND 100
                        THEN (ISNULL(fi.Cantidad, 0) * ISNULL(fi.Precio, 0)) * (ISNULL(fi.Descuento, 0) / 100.0)
                        ELSE ISNULL(fi.Descuento, 0)
                      END
                 ELSE 0 END) AS gravado18,

            SUM(CASE WHEN ISNULL(pro.pro_montoitbis, 0) = 16 THEN
                    (ISNULL(fi.Cantidad, 0) * ISNULL(fi.Precio, 0))
                    - CASE
                        WHEN ISNULL(fi.Descuento, 0) BETWEEN 0 AND 100
                        THEN (ISNULL(fi.Cantidad, 0) * ISNULL(fi.Precio, 0)) * (ISNULL(fi.Descuento, 0) / 100.0)
                        ELSE ISNULL(fi.Descuento, 0)
                      END
                 ELSE 0 END) AS gravado16,

            SUM(CASE WHEN ISNULL(pro.pro_montoitbis, 0) = 0 THEN
                    (ISNULL(fi.Cantidad, 0) * ISNULL(fi.Precio, 0))
                    - CASE
                        WHEN ISNULL(fi.Descuento, 0) BETWEEN 0 AND 100
                        THEN (ISNULL(fi.Cantidad, 0) * ISNULL(fi.Precio, 0)) * (ISNULL(fi.Descuento, 0) / 100.0)
                        ELSE ISNULL(fi.Descuento, 0)
                      END
                 ELSE 0 END) AS gravado_exento_lines,

            SUM(CASE WHEN ISNULL(pro.pro_montoitbis, 0) = 18 THEN ISNULL(fi.MontoItbis, 0) ELSE 0 END) AS sum_monto_itbis_18,
            SUM(CASE WHEN ISNULL(pro.pro_montoitbis, 0) = 16 THEN ISNULL(fi.MontoItbis, 0) ELSE 0 END) AS sum_monto_itbis_16,
            SUM(CASE WHEN ISNULL(pro.pro_montoitbis, 0) = 0 THEN ISNULL(fi.MontoItbis, 0) ELSE 0 END) AS sum_monto_itbis_exento
        FROM dbo.Factura_Items AS fi WITH (NOLOCK)
        LEFT JOIN dbo.Productos AS pro WITH (NOLOCK)
            ON pro.pro_codigo = fi.ProductoID
           AND pro.emp_codigo = fi.EMP_CODIGO
        WHERE fi.EMP_CODIGO = rb.EMP_CODIGO
          AND fi.SUC_CODIGO = rb.SUC_CODIGO
          AND fi.FacturaID = rb.FacturaID
    ) AS L

) AS x;
GO
