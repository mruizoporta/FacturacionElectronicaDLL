/*
  dbo.spFacturasXMLGet

  Equivalente filtrado a vwFacturasXML (facturación administrativa: FACTURAS + det_factura).
  Misma proyección que la vista; filtra por clave de factura.

  Parámetros:
    @emp_codigo, @suc_codigo, @fac_numero, @fac_forma, @tfa_codigo

  DLL: FacturaElectronicaService — reemplaza SELECT * FROM vwFacturasXML WHERE ...

  Índices: docs/IX_vwFacturasXML_support.sql

  Ajustar USE antes de ejecutar.
*/
USE [l1];
GO

SET ANSI_NULLS ON;
GO

SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spFacturasXMLGet]
    @emp_codigo  INT,
    @suc_codigo  INT,
    @fac_numero  INT,
    @fac_forma   VARCHAR(10),
    @tfa_codigo  VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        x.*,
        CASE
            WHEN ISNULL(x.servicio_construccion, 0) = 1
                 AND ISNULL(x.has_det_servicio_construccion, 0) = 1
            THEN CAST(ROUND(ISNULL(x.fac_total, 0) * 0.10, 2) AS DECIMAL(18, 4))
            ELSE dbo.fn_obtenerTotalGravadoDGINew(x.emp_codigo, x.fac_numero, 1, x.tfa_codigo, x.fac_forma, x.suc_codigo)
        END AS monto_grabado,
        CASE
            WHEN ISNULL(x.servicio_construccion, 0) = 1
                 AND ISNULL(x.has_det_servicio_construccion, 0) = 1
            THEN CAST(ROUND(ISNULL(x.fac_total, 0) * 0.10, 2) AS DECIMAL(18, 4))
            ELSE dbo.fn_obtenerTotalGravadoDGINew(x.emp_codigo, x.fac_numero, 18, x.tfa_codigo, x.fac_forma, x.suc_codigo)
        END AS monto_grabado1,
        dbo.fn_obtenerTotalGravadoDGINew(x.emp_codigo, x.fac_numero, 16, x.tfa_codigo, x.fac_forma, x.suc_codigo) AS monto_grabado2,
        dbo.fn_obtenerTotalGravadoDGINew(x.emp_codigo, x.fac_numero, 0, x.tfa_codigo, x.fac_forma, x.suc_codigo) AS monto_grabado3,
        (SELECT TOP 1 ISNULL(DG.fac_itbis_dgi, 0)
           FROM Facturas_infoDGI AS DG
          WHERE DG.emp_codigo = x.emp_codigo
            AND DG.suc_codigo = x.suc_codigo
            AND DG.fac_numero = x.fac_numero) AS fac_itbis_dgi,
        x.eNCF AS ncfEnvia,
        CASE WHEN x.precio_itbis_incluido IS NULL THEN 0 ELSE 1 END AS itbis_incluido,
        (
          CASE
              WHEN ISNULL(x.servicio_construccion, 0) = 1
                   AND ISNULL(x.has_det_servicio_construccion, 0) = 1
              THEN CAST(ROUND(ISNULL(x.fac_total, 0) * 0.10, 2) AS DECIMAL(18, 4))
              ELSE dbo.fn_obtenerTotalGravadoDGINew(x.emp_codigo, x.fac_numero, 1, x.tfa_codigo, x.fac_forma, x.suc_codigo)
          END
          + x.fac_itbis
          + x.monto_exento
        ) AS MontoTotalRedondeado
    FROM (
        SELECT
            f.fac_descuento,
            f.eNCF,
            f.codigoseguridad,
            f.fac_forma,
            f.tip_codigo,
            f.emp_codigo,
            ISNULL(p.servicio_construccion, 0) AS servicio_construccion,
            ISNULL(C.has_det_servicio_construccion, 0) AS has_det_servicio_construccion,
            CASE f.tip_codigo
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
                WHEN tf.tfa_nombre = 'FACTURA CONTADO' THEN 1
                WHEN tf.tfa_nombre = 'FACTURA CREDITO' THEN 2
                WHEN tf.tfa_nombre IS NULL THEN 1
                ELSE 3
            END AS TipoPago,
            RTRIM(NCF_Fijo) + RIGHT('00000000' + CAST(NCF_Secuencia AS VARCHAR(8)), 8) AS NCF,
            NCF_Secuencia,
            f.tfa_codigo,
            CASE
                WHEN f.tip_codigo = 2 AND f.fac_vence IS NULL THEN
                    SUBSTRING(CONVERT(VARCHAR(8), GETDATE(), 112), 7, 2) + '-' +
                    SUBSTRING(CONVERT(VARCHAR(8), GETDATE(), 112), 5, 2) + '-' +
                    SUBSTRING(CONVERT(VARCHAR(8), GETDATE(), 112), 1, 4)
                ELSE
                    SUBSTRING(CONVERT(VARCHAR(8), f.fac_vence, 112), 7, 2) + '-' +
                    SUBSTRING(CONVERT(VARCHAR(8), f.fac_vence, 112), 5, 2) + '-' +
                    SUBSTRING(CONVERT(VARCHAR(8), f.fac_vence, 112), 1, 4)
            END AS fac_vence,
            CASE f.tfa_codigo
                WHEN 1 THEN cpa_nombre
                WHEN 2 THEN cpa_nombre
                ELSE ''
            END AS condicion_pago,
            emp_rnc,
            CASE WHEN FG.dgi = 1 THEN FG.emp_nombre_dgi ELSE emp_nombre END AS emp_nombre,
            emp_localidad,
            CASE WHEN FG.dgi = 1 THEN FG.emp_direccion_dgi ELSE emp_direccion END AS emp_direccion,
            emp_telefono,
            CASE WHEN FG.dgi = 1 THEN FG.emp_web_dgi ELSE emp_web END AS emp_web,
            CASE WHEN FG.dgi = 1 THEN FG.emp_email_dgi ELSE emp_email END AS emp_email,
            s.suc_nombre,
            f.ven_codigo,
            f.fac_numero,
            SUBSTRING(CONVERT(VARCHAR(8), f.fac_fecha, 112), 5, 2) + '-' +
            SUBSTRING(CONVERT(VARCHAR(8), f.fac_fecha, 112), 7, 2) + '-' +
            SUBSTRING(CONVERT(VARCHAR(8), f.fac_fecha, 112), 1, 4) AS fac_fecha,
            CASE
                WHEN ISNULL(f.fac_rnc, '0') = '0' THEN
                ISNULL(NULLIF(LTRIM(RTRIM(cli.cli_rnc)), ''), cli.cli_rnc)
                ELSE f.fac_rnc
            END AS cli_rnc,
            CASE
                WHEN ISNULL(f.fac_rnc, '0') = '0' THEN
                  CASE LEN(LTRIM(RTRIM(cli.cli_nombre))) WHEN 0 THEN '' ELSE cli.cli_nombre END
                ELSE f.fac_nombre
            END AS cli_nombre,
            '' AS cli_contacto,
            '' AS MunicipioComprador,
            '' AS ProvinciaComprador,
            LTRIM(RTRIM(cli.cli_email)) AS cli_email,
            CASE WHEN LEN(LTRIM(RTRIM(cli.cli_direccion))) = 0 THEN 'ND' ELSE LTRIM(RTRIM(cli.cli_direccion)) END AS cli_direccion,
            cli.cli_localidad,
            FG.municipio_dgi AS municipio,
            FG.provincia_dgi AS provincia,
            CASE WHEN FG.dgi = 1 THEN FG.CodigoVendedor_dgi ELSE CAST(f.ven_codigo AS VARCHAR(255)) END AS CodigoVendedor,
            CASE WHEN FG.dgi = 1 THEN FG.NumeroFacturaInterna_sdi ELSE CAST(f.fac_numero AS VARCHAR(100)) END AS NumeroFacturaInterna,
            FG.NumeroPedidoInterno_dgi AS NumeroPedidoInterno,
            FG.ZonaVenta_dgi AS ZonaVenta,
            FG.FechaEntrega_dgi AS FechaEntrega,
            FG.FechaOrdenCompra_dgi AS FechaOrdenCompra,
            FG.NumeroOrdenCompra_dgi AS NumeroOrdenCompra,
            FG.emp_telefono2 AS emp_telefono2,
            FG.CodigoInternoComprador_dgi AS CodigoInternoComprador,
            fac_total,
            fac_total - fac_abono AS ValorPagar,
            FG.DGI AS dgi,
            0 AS monto_grabado_dgi,
            CASE emp_rnc WHEN '105081105' THEN F.fac_itbis ELSE
            ISNULL(dbo.fn_obtenerTotalTipoItbisDGI(f.emp_codigo, f.fac_numero, 18, f.fac_forma, f.tfa_codigo, f.suc_codigo), 0) +
            ISNULL(dbo.fn_obtenerTotalTipoItbisDGI(f.emp_codigo, f.fac_numero, 16, f.fac_forma, f.tfa_codigo, f.suc_codigo), 0) +
            ISNULL(dbo.fn_obtenerTotalTipoItbisDGI(f.emp_codigo, f.fac_numero, 0, f.fac_forma, f.tfa_codigo, f.suc_codigo), 0)
            END AS fac_itbis,
            CASE emp_rnc WHEN '105081105' THEN F.fac_itbis ELSE
            dbo.fn_obtenerTipoItbisDGI(f.emp_codigo, f.fac_numero, 18)
            END AS det_itbis_1_dgi,
            dbo.fn_obtenerTipoItbisDGI(f.emp_codigo, f.fac_numero, 16) AS det_itbis_2_dgi,
            dbo.fn_obtenerTipoItbisDGI(f.emp_codigo, f.fac_numero, 0) AS det_itbis_3_dgi,
            CASE emp_rnc WHEN '105081105' THEN F.fac_itbis ELSE
            ISNULL(dbo.fn_obtenerTotalTipoItbisDGI(f.emp_codigo, f.fac_numero, 18, f.fac_forma, f.tfa_codigo, f.suc_codigo), 0)
            END AS det_totalitbis_1,
            ISNULL(dbo.fn_obtenerTotalTipoItbisDGI(f.emp_codigo, f.fac_numero, 16, f.fac_forma, f.tfa_codigo, f.suc_codigo), 0) AS det_totalitbis_2,
            ISNULL(dbo.fn_obtenerTotalTipoItbisDGI(f.emp_codigo, f.fac_numero, 0, f.fac_forma, f.tfa_codigo, f.suc_codigo), 0) AS det_totalitbis_3,
            ISNULL(FG.TotalITBISPercepcion_dgi, 0) AS TotalITBISPercepcion_dgi,
            ISNULL(FG.numeroContenedor, '') AS numeroContenedor,
            ISNULL(FG.numeroReferencia, '') AS numeroReferencia,
            CASE
                WHEN (SELECT par_itbisincluido FROM PARAMETROS pa WHERE pa.par_itbisincluido = 'True' AND pa.emp_codigo = f.emp_codigo) IS NULL
                THEN CASE WHEN dbo.fn_obtenerTotalGravadoDGINew(f.emp_codigo, f.fac_numero, 1, f.tfa_codigo, f.fac_forma, f.suc_codigo) > 0 THEN 1 ELSE 0 END
                ELSE CASE WHEN dbo.fn_obtenerTotalGravadoDGINew(f.emp_codigo, f.fac_numero, 1, f.tfa_codigo, f.fac_forma, f.suc_codigo) > 0 THEN 1 ELSE 0 END
            END AS IndicadorMontoGravado,
            CASE
                WHEN (SELECT par_itbisincluido FROM PARAMETROS pa WHERE pa.par_itbisincluido = 'True' AND pa.emp_codigo = f.emp_codigo) IS NULL
                THEN 0 ELSE 1 END AS precio_itbis_incluido,
            f.fechafirma,
            f.suc_codigo,
            CASE
              WHEN
                MAX(CASE WHEN pro.pro_montoitbis = 18 AND ISNULL(df.det_itbis, 0) > 1 THEN 18 ELSE 0 END)
                  OVER (PARTITION BY f.emp_codigo, f.suc_codigo, f.fac_forma, f.tfa_codigo, f.fac_numero) = 18
              THEN 18
              WHEN
                MAX(CASE WHEN pro.pro_montoitbis IN (16, 18) AND ISNULL(df.det_itbis, 0) > 1 THEN 1 ELSE 0 END)
                  OVER (PARTITION BY f.emp_codigo, f.suc_codigo, f.fac_forma, f.tfa_codigo, f.fac_numero) = 0
                AND ISNULL(f.fac_itbis, 0) > 0
                AND e.emp_rnc IN ('105081105', '131861717')
              THEN 18
              ELSE 0
            END AS det_itbis_1,
            CASE
                WHEN pro.pro_montoitbis = 16 THEN 16
                WHEN pro.pro_montoitbis = 0
                     AND EXISTS (
                        SELECT 1
                        FROM det_factura df2
                        JOIN Productos pro2
                          ON pro2.pro_codigo = df2.pro_codigo
                         AND pro2.emp_codigo = f.emp_codigo
                       WHERE df2.emp_codigo = f.emp_codigo
                         AND df2.suc_codigo = f.suc_codigo
                         AND df2.fac_forma = f.fac_forma
                         AND df2.tfa_codigo = f.tfa_codigo
                         AND df2.fac_numero = f.fac_numero
                         AND pro2.pro_montoitbis = 16
                     )
                THEN 16
                ELSE 0
            END AS det_itbis_2,
            CASE pro.pro_montoitbis WHEN 0 THEN 0 ELSE 0 END AS det_itbis_3,
            CASE
                WHEN f.tip_codigo = 4 THEN CAST(ISNULL(f.fac_total, 0) AS DECIMAL(18, 4))
                WHEN ISNULL(p.servicio_construccion, 0) = 1
                     AND ISNULL(C.has_det_servicio_construccion, 0) = 1
                THEN CAST(ROUND(ISNULL(f.fac_total, 0) * 0.88231827112, 2) AS DECIMAL(18, 4))
                ELSE
                    SUM(
                        CASE
                            WHEN ISNULL(pro.pro_montoitbis, 0) = 0 THEN
                            (
                               (
                                  (ISNULL(df.det_cantidad, 0) * ISNULL(df.det_precio, 0))
                                  -
                                  CASE
                                    WHEN ISNULL(df.det_descuento, 0) BETWEEN 0 AND 100
                                      THEN (ISNULL(df.det_cantidad, 0) * ISNULL(df.det_precio, 0)) * (ISNULL(df.det_descuento, 0) / 100.0)
                                    ELSE ISNULL(df.det_descuento, 0)
                                  END
                               )
                               - CASE
                                    WHEN ISNULL(T.total_neto_factura, 0) > 0
                                         AND (CASE WHEN ISNULL(f.fac_descuento, 0) > ISNULL(T.total_desc_linea_factura, 0)
                                                   THEN ISNULL(f.fac_descuento, 0) - ISNULL(T.total_desc_linea_factura, 0)
                                                   ELSE 0 END) > 0
                                    THEN
                                        (
                                         (
                                          (ISNULL(df.det_cantidad, 0) * ISNULL(df.det_precio, 0))
                                          -
                                          CASE
                                            WHEN ISNULL(df.det_descuento, 0) BETWEEN 0 AND 100
                                              THEN (ISNULL(df.det_cantidad, 0) * ISNULL(df.det_precio, 0)) * (ISNULL(df.det_descuento, 0) / 100.0)
                                            ELSE ISNULL(df.det_descuento, 0)
                                          END
                                         )
                                         / T.total_neto_factura
                                        )
                                        * (
                                            ISNULL(f.fac_descuento, 0) - ISNULL(T.total_desc_linea_factura, 0)
                                          )
                                    ELSE 0
                                 END
                            )
                            ELSE 0
                        END
                    ) OVER (
                        PARTITION BY f.emp_codigo, f.suc_codigo, f.fac_forma, f.tfa_codigo, f.fac_numero
                    )
            END AS monto_exento,
            f.FechaLimitePago
        FROM FACTURAS f
        INNER JOIN Empresas e ON e.emp_codigo = f.emp_codigo
        INNER JOIN Sucursales s ON s.suc_codigo = f.suc_codigo AND s.emp_codigo = f.emp_codigo
        INNER JOIN parametros p ON p.emp_codigo = f.emp_codigo
        LEFT JOIN TiposFactura tf ON tf.tfa_codigo = f.tfa_codigo AND tf.emp_codigo = f.emp_codigo
        INNER JOIN det_factura df ON f.emp_codigo = df.emp_codigo AND f.suc_codigo = df.suc_codigo
                                  AND f.fac_forma = df.fac_forma AND f.tfa_codigo = df.tfa_codigo
                                  AND f.fac_numero = df.fac_numero
        LEFT JOIN Productos pro ON pro.pro_codigo = df.pro_codigo AND pro.emp_codigo = f.emp_codigo
        LEFT JOIN Condiciones Cn ON Cn.emp_codigo = f.emp_codigo AND Cn.cpa_codigo = f.CPA_CODIGO
        LEFT JOIN Clientes cli ON cli.cli_codigo = f.cli_codigo AND cli.cli_codigo IS NOT NULL
                               AND cli.emp_codigo = f.emp_codigo
        LEFT JOIN Facturas_infoDGI FG ON FG.emp_codigo = df.emp_codigo AND df.suc_codigo = FG.suc_codigo
                                       AND f.fac_numero = FG.fac_numero
        OUTER APPLY (
            SELECT
                total_neto_factura =
                    SUM(
                      (ISNULL(df0.det_cantidad, 0) * ISNULL(df0.det_precio, 0))
                      -
                      CASE
                        WHEN ISNULL(df0.det_descuento, 0) BETWEEN 0 AND 100
                          THEN (ISNULL(df0.det_cantidad, 0) * ISNULL(df0.det_precio, 0)) * (ISNULL(df0.det_descuento, 0) / 100.0)
                        ELSE ISNULL(df0.det_descuento, 0)
                      END
                    ),
                total_desc_linea_factura =
                    SUM(
                      CASE
                        WHEN ISNULL(df0.det_descuento, 0) BETWEEN 0 AND 100
                          THEN (ISNULL(df0.det_cantidad, 0) * ISNULL(df0.det_precio, 0)) * (ISNULL(df0.det_descuento, 0) / 100.0)
                        ELSE ISNULL(df0.det_descuento, 0)
                      END
                    )
            FROM det_factura df0
            WHERE df0.emp_codigo = f.emp_codigo
              AND df0.suc_codigo = f.suc_codigo
              AND df0.fac_forma = f.fac_forma
              AND df0.tfa_codigo = f.tfa_codigo
              AND df0.fac_numero = f.fac_numero
        ) AS T
        OUTER APPLY (
            SELECT TOP 1 1 AS has_det_servicio_construccion
            FROM det_factura dfc
            WHERE dfc.emp_codigo = f.emp_codigo
              AND dfc.suc_codigo = f.suc_codigo
              AND dfc.fac_forma = f.fac_forma
              AND dfc.tfa_codigo = f.tfa_codigo
              AND dfc.fac_numero = f.fac_numero
              AND ISNULL(dfc.det_servicio_construccion, 0) = 1
        ) AS C
        WHERE f.emp_codigo = @emp_codigo
          AND f.suc_codigo = @suc_codigo
          AND f.fac_numero = @fac_numero
          AND f.fac_forma = @fac_forma
          AND f.tfa_codigo = @tfa_codigo
    ) AS x;
END;
GO
