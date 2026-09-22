/*
  dbo.spFacturasXMLPOSGet

  Equivalente filtrado a vwFacturasXMLPOS: misma proyección que la vista,
  con filtro por ticket (fac_numero), cajero y caja para limitar trabajo en el servidor.

  Parámetros (alineados a FacturaElectronicaService / GenerarXML POS):
    @emp_codigo, @suc_codigo, @fac_numero (ticket), @usu_codigo, @caja

  La DLL usa: EXEC dbo.spFacturasXMLPOSGet @emp_codigo=..., @suc_codigo=..., @fac_numero=..., @usu_codigo=N'...', @caja=N'...'

  Ajustar USE a la base correspondiente antes de ejecutar.
*/
USE [l1];
GO

SET ANSI_NULLS ON;
GO

SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spFacturasXMLPOSGet]
    @emp_codigo   INT,
    @suc_codigo   INT,
    @fac_numero   INT,
    @usu_codigo   VARCHAR(50),
    @caja         VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        x.*,
        dbo.fn_obtenerTotalGravadoDGINewPOS(x.emp_codigo, x.ticket, 1, x.usu_codigo, x.caja) AS monto_grabado,
        dbo.fn_obtenerTotalGravadoDGINewPOS(x.emp_codigo, x.ticket, 18, x.usu_codigo, x.caja) AS monto_grabado1,
        dbo.fn_obtenerTotalGravadoDGINewPOS(x.emp_codigo, x.ticket, 16, x.usu_codigo, x.caja) AS monto_grabado2,
        dbo.fn_obtenerTotalGravadoDGINewPOS(x.emp_codigo, x.ticket, 0, x.usu_codigo, x.caja) AS monto_grabado3,
        (SELECT TOP 1 ISNULL(DG.fac_itbis_dgi, 0)
           FROM Facturas_infoDGI AS DG
          WHERE DG.emp_codigo = x.emp_codigo
            AND DG.suc_codigo = x.suc_codigo
            AND DG.fac_numero = x.fac_numero) AS fac_itbis_dgi,
        CASE WHEN x.precio_itbis_incluido IS NULL THEN 0 ELSE 1 END AS itbis_incluido,
        dbo.fn_obtenerTotalGravadoDGINewPOS(x.emp_codigo, x.ticket, 18, x.usu_codigo, x.caja)
          + x.fac_itbis
          + x.monto_exento AS MontoTotalRedondeado
    FROM (
        SELECT
            f.descuento AS fac_descuento,
            f.eNCF,
            f.codigoseguridad,
            f.usu_codigo,
            f.caja,
            f.fecha,
            f.ticket,
            f.tip_codigo,
            f.emp_codigo,
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
            f.tfa_codigo,
            CASE f.tfa_codigo
                WHEN 1 THEN cpa_nombre
                WHEN 2 THEN cpa_nombre
                ELSE ''
            END AS condicion_pago,
            emp_rnc,
            emp_nombre,
            emp_localidad,
            emp_direccion,
            emp_telefono,
            emp_web,
            emp_email,
            s.suc_nombre,
            f.ven_codigo,
            f.ticket AS fac_numero,
            SUBSTRING(CONVERT(VARCHAR(8), f.fecha, 112), 5, 2) + '-' +
            SUBSTRING(CONVERT(VARCHAR(8), f.fecha, 112), 7, 2) + '-' +
            SUBSTRING(CONVERT(VARCHAR(8), f.fecha, 112), 1, 4) AS fac_fecha,
            CASE
                WHEN ISNULL(f.rnc, '0') = '0'
                    THEN ISNULL(NULLIF(LTRIM(RTRIM(cli.cli_rnc)), ''), cli.cli_rnc)
                ELSE f.rnc
            END AS cli_rnc,
            CASE
                WHEN ISNULL(f.nombre, '0') = '0' THEN
                    CASE LEN(LTRIM(RTRIM(cli.cli_nombre))) WHEN 0 THEN '' ELSE cli.cli_nombre END
                ELSE f.nombre
            END AS cli_nombre,
            '' AS cli_contacto,
            '' AS MunicipioComprador,
            '' AS ProvinciaComprador,
            LTRIM(RTRIM(cli.cli_email)) AS cli_email,
            CASE WHEN LEN(LTRIM(RTRIM(cli.cli_direccion))) = 0 THEN 'ND' ELSE
            LTRIM(RTRIM(cli.cli_direccion)) END AS cli_direccion,
            cli.cli_localidad,
            '' AS municipio,
            '' AS provincia,
            CAST(f.ven_codigo AS VARCHAR(255)) AS CodigoVendedor,
            CAST(f.ticket AS VARCHAR(100)) AS NumeroFacturaInterna,
            '' AS NumeroPedidoInterno,
            '' AS ZonaVenta,
            '' AS FechaEntrega,
            '' AS FechaOrdenCompra,
            '' AS NumeroOrdenCompra,
            '' AS emp_telefono2,
            '' AS CodigoInternoComprador,
            total AS fac_total,
            total AS ValorPagar,
            0 AS dgi,
            0 AS monto_grabado_dgi,
            CASE emp_rnc
                WHEN '105081105' THEN f.itbis
                ELSE
                    ISNULL(dbo.fn_obtenerTotalTipoItbisDGIPOS(f.emp_codigo, f.ticket, 18, f.usu_codigo, f.caja, f.fecha, f.suc_codigo), 0) +
                    ISNULL(dbo.fn_obtenerTotalTipoItbisDGIPOS(f.emp_codigo, f.ticket, 16, f.usu_codigo, f.caja, f.fecha, f.suc_codigo), 0) +
                    ISNULL(dbo.fn_obtenerTotalTipoItbisDGIPOS(f.emp_codigo, f.ticket, 0, f.usu_codigo, f.caja, f.fecha, f.suc_codigo), 0)
            END AS fac_itbis,
            CASE emp_rnc
                WHEN '105081105' THEN f.itbis
                ELSE dbo.fn_obtenerTipoItbisDGI(f.emp_codigo, f.ticket, 18)
            END AS det_itbis_1_dgi,
            CASE emp_rnc
                WHEN '105081105' THEN f.itbis
                ELSE ISNULL(dbo.fn_obtenerTotalTipoItbisDGIPOS(f.emp_codigo, f.ticket, 18, f.usu_codigo, f.caja, f.fecha, f.suc_codigo), 0)
            END AS det_totalitbis_1,
            ISNULL(dbo.fn_obtenerTotalTipoItbisDGIPOS(f.emp_codigo, f.ticket, 16, f.usu_codigo, f.caja, f.fecha, f.suc_codigo), 0) AS det_totalitbis_2,
            ISNULL(dbo.fn_obtenerTotalTipoItbisDGIPOS(f.emp_codigo, f.ticket, 0, f.usu_codigo, f.caja, f.fecha, f.suc_codigo), 0) AS det_totalitbis_3,
            0 AS TotalITBISPercepcion_dgi,
            '' AS numeroContenedor,
            '' AS numeroReferencia,
            CASE
                WHEN (SELECT par_itbisincluido
                      FROM PARAMETROS pa
                      WHERE pa.par_itbisincluido = 'True'
                        AND pa.emp_codigo = f.emp_codigo) IS NULL THEN
                    CASE WHEN dbo.fn_obtenerTotalGravadoDGINewPOS(f.emp_codigo, f.ticket, 1, f.usu_codigo, f.caja) > 0 THEN 1 ELSE 0 END
                ELSE
                    CASE WHEN dbo.fn_obtenerTotalGravadoDGINewPOS(f.emp_codigo, f.ticket, 1, f.usu_codigo, f.caja) > 0 THEN 1 ELSE 0 END
            END AS IndicadorMontoGravado,
            CASE
                WHEN (SELECT par_itbisincluido
                      FROM PARAMETROS pa
                      WHERE pa.par_itbisincluido = 'True'
                        AND pa.emp_codigo = f.emp_codigo) IS NULL THEN 0
                ELSE 1
            END AS precio_itbis_incluido,
            f.fechafirma,
            f.suc_codigo,

            ISNULL(MAX(CASE WHEN pro.pro_montoitbis = 18 THEN 18 END) OVER (
                PARTITION BY f.emp_codigo, f.suc_codigo, f.usu_codigo, f.caja, f.ticket, f.fecha
            ), 0) AS det_itbis_1,

            ISNULL(MAX(CASE WHEN pro.pro_montoitbis = 16 THEN 16 END) OVER (
                PARTITION BY f.emp_codigo, f.suc_codigo, f.usu_codigo, f.caja, f.ticket, f.fecha
            ), 0) AS det_itbis_2,

            ISNULL(MAX(CASE WHEN pro.pro_montoitbis = 0 THEN 0 END) OVER (
                PARTITION BY f.emp_codigo, f.suc_codigo, f.usu_codigo, f.caja, f.ticket, f.fecha
            ), 0) AS det_itbis_3,

            SUM(
                CASE
                    WHEN pro.pro_montoitbis = 0 THEN df.cantidad * precio
                    WHEN df.itbis = 0 THEN df.cantidad * precio
                    ELSE 0
                END
            ) OVER (
                PARTITION BY f.emp_codigo, f.suc_codigo, f.usu_codigo, f.caja, f.ticket, f.fecha
            ) AS monto_exento,
            f.FechaLimitePago
        FROM Montos_Ticket f
        INNER JOIN Empresas e ON e.emp_codigo = f.emp_codigo
        INNER JOIN Sucursales s ON s.suc_codigo = f.suc_codigo AND s.emp_codigo = f.emp_codigo
        INNER JOIN Parametros p ON p.emp_codigo = f.emp_codigo
        LEFT JOIN TiposFactura tf ON tf.tfa_codigo = f.tfa_codigo AND tf.emp_codigo = f.emp_codigo
        INNER JOIN Ticket df ON f.emp_codigo = df.emp_codigo
                              AND f.suc_codigo = df.suc_codigo
                              AND f.caja = df.caja
                              AND f.usu_codigo = df.usu_codigo
                              AND f.ticket = df.ticket
        LEFT JOIN Productos pro ON pro.pro_codigo = df.producto
                               AND pro.emp_codigo = df.emp_codigo
        LEFT JOIN Clientes cli ON cli.cli_codigo = f.cli_codigo
                               AND cli.emp_codigo = f.emp_codigo
        LEFT JOIN Condiciones c ON cli.cpa_codigo = c.CPA_CODIGO
        WHERE f.emp_codigo = @emp_codigo
          AND f.suc_codigo = @suc_codigo
          AND f.ticket = @fac_numero
          AND f.usu_codigo = @usu_codigo
          AND f.caja = @caja
    ) AS x;
END;
GO
