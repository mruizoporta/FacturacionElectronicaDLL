/*
  Índices de apoyo para vwFacturasXML y dbo.spFacturasXMLGet
  (FACTURAS + det_factura, OUTER APPLY por clave de factura).

  Ajustar USE [l1] / nombres de columnas según tu catálogo.
  Tras crear: UPDATE STATISTICS en tablas tocadas según política.
*/
USE [l1];
GO

/* FACTURAS: seek por clave completa (mismo filtro que la DLL) */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_FACTURAS_FacturaXML_Key' AND object_id = OBJECT_ID(N'dbo.FACTURAS', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FACTURAS_FacturaXML_Key]
    ON [dbo].[FACTURAS] (
        [emp_codigo],
        [suc_codigo],
        [fac_numero],
        [fac_forma],
        [tfa_codigo]
    );
END
GO

/* det_factura: mismas columnas para JOIN, OUTER APPLY T/C y EXISTS en vista/SP */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_det_factura_FacturaXML_Key' AND object_id = OBJECT_ID(N'dbo.det_factura', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_det_factura_FacturaXML_Key]
    ON [dbo].[det_factura] (
        [emp_codigo],
        [suc_codigo],
        [fac_numero],
        [fac_forma],
        [tfa_codigo]
    )
    INCLUDE ([pro_codigo], [det_cantidad], [det_precio], [det_descuento], [det_itbis], [det_servicio_construccion]);
    /* Quitar INCLUDE si alguna columna no existe en tu versión de det_factura. */
END
GO

/* Facturas_infoDGI: subconsulta TOP 1 (outer SELECT) y LEFT JOIN FG en el cuerpo */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Facturas_infoDGI_emp_suc_fac' AND object_id = OBJECT_ID(N'dbo.Facturas_infoDGI', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Facturas_infoDGI_emp_suc_fac]
    ON [dbo].[Facturas_infoDGI] ([emp_codigo], [suc_codigo], [fac_numero]);
    /* Ampliar INCLUDE según columnas que lea el JOIN (dgi, emp_nombre_dgi, etc.) si el plan lo justifica. */
END
GO

/*
  PARAMETROS: los subselects por emp_codigo se benefician de IX_PARAMETROS_emp_itbis
  en docs/IX_vwFacturasDetalleXMLPOS_support.sql. Si falta servicio_construccion en INCLUDE,
  recrea ese índice con INCLUDE (par_itbisincluido, servicio_construccion) o añade un índice filtrado por empresa.
*/
