/*
  Índices de apoyo para dbo.spReporteFacturaGet.
  Objetivo: seek por la factura y acelerar joins/applies del reporte.

  Ajustar USE y columnas INCLUDE según tu esquema real.
*/
USE [l1];
GO

/* FACTURAS: filtro principal del SP */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_FACTURAS_ReporteFactura_Key'
      AND object_id = OBJECT_ID(N'dbo.FACTURAS', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FACTURAS_ReporteFactura_Key]
    ON [dbo].[FACTURAS] (
        [emp_codigo],
        [suc_codigo],
        [fac_numero],
        [fac_forma],
        [tfa_codigo]
    )
    INCLUDE ([cli_codigo], [fac_descuento], [fac_itbis], [fac_fecha], [eNCF], [DGII_MontoEnviado], [fechafirma], [codigoseguridad], [tip_codigo]);
END
GO

/* det_factura: JOIN principal + subconsultas OUTER APPLY (df0/df2) */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_det_factura_ReporteFactura_Key'
      AND object_id = OBJECT_ID(N'dbo.det_factura', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_det_factura_ReporteFactura_Key]
    ON [dbo].[det_factura] (
        [emp_codigo],
        [suc_codigo],
        [fac_numero],
        [fac_forma],
        [tfa_codigo]
    )
    INCLUDE ([pro_codigo], [det_cantidad], [det_precio], [det_descuento], [det_itbis]);
END
GO

/* Productos: join por emp+producto en consulta principal y Det */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Productos_emp_pro_ReporteFactura'
      AND object_id = OBJECT_ID(N'dbo.Productos', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Productos_emp_pro_ReporteFactura]
    ON [dbo].[Productos] ([emp_codigo], [pro_codigo])
    INCLUDE ([pro_montoitbis]);
END
GO

/* Facturas_infoDGI: usado por columnas y subconsulta top 1 */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Facturas_infoDGI_emp_suc_fac_rep'
      AND object_id = OBJECT_ID(N'dbo.Facturas_infoDGI', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Facturas_infoDGI_emp_suc_fac_rep]
    ON [dbo].[Facturas_infoDGI] ([emp_codigo], [suc_codigo], [fac_numero])
    INCLUDE ([fac_itbis_dgi], [AceptadoDGII]);
END
GO

/* PARAMETROS: OUTER APPLY por empresa para integracion_luganis */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PARAMETROS_emp_integracion_luganis'
      AND object_id = OBJECT_ID(N'dbo.PARAMETROS', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PARAMETROS_emp_integracion_luganis]
    ON [dbo].[PARAMETROS] ([emp_codigo])
    INCLUDE ([integracion_luganis]);
END
GO

/* TipoNCF: subconsulta top 1 por tip_codigo */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TipoNCF_tip_codigo_cod_dgii'
      AND object_id = OBJECT_ID(N'dbo.TipoNCF', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TipoNCF_tip_codigo_cod_dgii]
    ON [dbo].[TipoNCF] ([tip_codigo])
    INCLUDE ([cod_dgii]);
END
GO
