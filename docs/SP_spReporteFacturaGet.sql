/*
  dbo.spReporteFacturaGet

  Procedimiento para reporte de factura (equivalente al SQL ad-hoc enviado).
  Filtra por:
    @EMP, @SUC, @FORMA, @NUMERO, @TIPO

  Ajustar USE antes de ejecutar en cada base.
*/
USE [l1];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spReporteFacturaGet]
    @TIPO   INT,
    @NUMERO INT,
    @EMP    INT,
    @FORMA  CHAR(1),
    @SUC    INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.suc_direccion, s.suc_localidad, s.suc_telefono, s.suc_rnc, s.suc_nombre, s.suc_fax,
        CAJ_CODIGO, f.CLI_CODIGO, COT_NUMERO, f.CPA_CODIGO,
        F.EMP_CODIGO,
        (FAC_ABONO/FAC_TASA) as FAC_ABONO,
        (ISNULL(FAC_DESCUENTO,0)/ISNULL(FAC_TASA,0)) as FAC_DESCUENTO,
        FAC_DIRECCION,
        FAC_FAX, FAC_FECHA, f.FAC_FORMA,
        (FAC_ITBIS/FAC_TASA) as FAC_ITBIS,
        FAC_LOCALIDAD,
        FAC_NOMBRE, FAC_NOTA, f.FAC_NUMERO,
        (FAC_OTROS/FAC_TASA) AS FAC_OTROS,
        FAC_STATUS,
        FAC_TELEFONO,
        (FAC_TOTAL/FAC_TASA) as FAC_TOTAL,
        f.PED_NUMERO, F.TFA_CODIGO,
        F.USU_CODIGO, F.VEN_CODIGO, f.CLI_REFERENCIA, F.ALM_CODIGO,
        RTRIM(TF.TFA_MENSAJE1) FAC_MENSAJE1, RTRIM(FAC_MENSAJE2) FAC_MENSAJE2, RTRIM(FAC_MENSAJE3) FAC_MENSAJE3,
        FAC_HORA, FAC_VENCE, f.MON_CODIGO, FAC_CONITBIS,
        F.SUC_CODIGO, NCF_Fijo, NCF_Secuencia, fac_rnc,
        OrdenID, Placa, Chofer, Modelo, F.Marca, Compania, fac_tasa, NUMERO_RESERVA, FAC_PROPINA, F.tip_codigo, fac_caja, porc_desc_gral,
        ISNULL(Tdesc_gral,0) Tdesc_gral, NIF,
        fac_devuelto,
        ((ISNULL(FAC_DESCUENTO,0)+ISNULL(Tdesc_gral,0))/ISNULL(FAC_TASA,0)) TOTALDESC,
        (ISNULL(FAC_TOTAL,0)+ISNULL(FAC_DESCUENTO,0)+ISNULL(Tdesc_gral,0)-ISNULL(FAC_ITBIS,0))/ISNULL(FAC_TASA,0) SUBTOTAL,
        FAC_TOTAL_DOLAR,

        ISNULL(eNCF,
          CASE
            WHEN f.NCF_Fijo IS NOT NULL AND LTRIM(RTRIM(f.NCF_Fijo)) <> ''
            THEN LTRIM(RTRIM(f.NCF_Fijo)) +
                 CASE
                   WHEN LEN(CAST(ISNULL(f.NCF_Secuencia,0) AS varchar(50))) < 8
                     THEN REPLICATE('0', 8 - LEN(CAST(ISNULL(f.NCF_Secuencia,0) AS varchar(50))))
                          + CAST(ISNULL(f.NCF_Secuencia,0) AS varchar(50))
                   ELSE CAST(ISNULL(f.NCF_Secuencia,0) AS varchar(50))
                 END
            ELSE ''
          END) AS eNCF,

        CASE
            WHEN ISNULL(Par.integracion_luganis, 0) = 1
                 AND ISNULL(LTRIM(RTRIM(f.qrCodeLuganis)), '') <> ''
            THEN f.qrCodeLuganis
            ELSE dbo.fnGenerarUrlTimbre(
                    f.emp_codigo,
                    e.emp_rnc,
                    cl.cli_rnc,
                    F.eNCF,
                    F.fac_fecha,
                    COALESCE(
                        NULLIF(ISNULL(F.DGII_MontoEnviado, 0), 0),
                        (
                            dbo.fn_obtenerTotalGravadoDGINew_ForQR(
                                f.emp_codigo, f.suc_codigo, f.fac_numero, 1, f.tfa_codigo, f.fac_forma
                            )
                            + f.fac_itbis
                            + ISNULL(Det.monto_exento, 0)
                        )
                    ),
                    F.fechafirma,
                    F.codigoseguridad
                 )
        END AS UrlCodigoQR,

        F.codigoseguridad,
        CONVERT(varchar(23), fechafirma, 121) AS fechafirma,
        (select top 1 tf2.cod_dgii from TipoNCF tf2 where tf2.tip_codigo = f.tip_codigo) as cod_dgii,
        ISNULL(AceptadoDGII,0) AS AceptadoDGII

    FROM FACTURAS F
    INNER JOIN sucursales s
        ON F.emp_codigo = s.emp_codigo
       AND F.suc_codigo = s.suc_codigo

    LEFT JOIN TIPOSFACTURA TF
        ON F.emp_codigo = TF.emp_codigo
       AND F.TFA_CODIGO = TF.TFA_CODIGO

    INNER JOIN Empresas E
        ON E.emp_codigo = F.emp_codigo

    INNER JOIN det_factura df
        ON f.emp_codigo = df.emp_codigo
       AND f.suc_codigo = df.suc_codigo
       AND f.fac_forma = df.fac_forma
       AND f.tfa_codigo = df.tfa_codigo
       AND f.fac_numero = df.fac_numero

    LEFT JOIN Productos pro
        ON pro.pro_codigo = df.pro_codigo
       AND pro.emp_codigo = f.emp_codigo

    LEFT JOIN Clientes cl
        ON cl.cli_codigo = f.cli_codigo
       AND cl.emp_codigo = f.emp_codigo

    OUTER APPLY (
        SELECT TOP 1
            ISNULL(pa.integracion_luganis,0) AS integracion_luganis
        FROM PARAMETROS pa
        WHERE pa.emp_codigo = f.emp_codigo
    ) AS Par

    OUTER APPLY (
        SELECT
            monto_exento =
                SUM(
                    CASE
                        WHEN ISNULL(pro2.pro_montoitbis,0) = 0 THEN
                        (
                            ((ISNULL(df2.det_cantidad,0) * ISNULL(df2.det_precio,0)) - ISNULL(df2.det_descuento,0))
                            - CASE
                                WHEN ISNULL(T.total_neto_factura,0) > 0
                                     AND ISNULL(T.desc_extra,0) > 0
                                THEN
                                    (
                                      ((ISNULL(df2.det_cantidad,0) * ISNULL(df2.det_precio,0)) - ISNULL(df2.det_descuento,0))
                                      / T.total_neto_factura
                                    ) * T.desc_extra
                                ELSE 0
                              END
                        )
                        ELSE 0
                    END
                )
        FROM det_factura df2
        LEFT JOIN Productos pro2
          ON pro2.emp_codigo = df2.emp_codigo
         AND pro2.pro_codigo = df2.pro_codigo

        OUTER APPLY (
            SELECT
                total_neto_factura =
                    SUM((ISNULL(df0.det_cantidad,0) * ISNULL(df0.det_precio,0)) - ISNULL(df0.det_descuento,0)),
                total_desc_linea =
                    SUM(ISNULL(df0.det_descuento,0)),
                desc_extra =
                    CASE
                        WHEN ISNULL(f.fac_descuento,0) > ISNULL(SUM(ISNULL(df0.det_descuento,0)),0)
                        THEN ISNULL(f.fac_descuento,0) - ISNULL(SUM(ISNULL(df0.det_descuento,0)),0)
                        ELSE 0
                    END
            FROM det_factura df0
            WHERE df0.emp_codigo = f.emp_codigo
              AND df0.suc_codigo = f.suc_codigo
              AND df0.fac_forma  = f.fac_forma
              AND df0.tfa_codigo = f.tfa_codigo
              AND df0.fac_numero = f.fac_numero
        ) AS T

        WHERE df2.emp_codigo = f.emp_codigo
          AND df2.suc_codigo = f.suc_codigo
          AND df2.fac_forma  = f.fac_forma
          AND df2.tfa_codigo = f.tfa_codigo
          AND df2.fac_numero = f.fac_numero
    ) AS Det

    WHERE F.emp_codigo = @emp
      AND F.tfa_codigo = @tipo
      AND f.fac_numero = @numero
      AND f.fac_forma = @forma
      AND F.suc_codigo = @suc;
END;
GO
