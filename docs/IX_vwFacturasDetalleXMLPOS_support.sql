/*
  Índices de apoyo para vwFacturasDetalleXMLPOS y para los SP filtrados:
    - dbo.spFacturasDetalleXMLPOSGet
    - dbo.spFacturasXMLPOSGet (encabezado; mismo patrón de join)

  Revisa nombres de columnas en Ticket/Montos_Ticket si difieren en tu BD.
  Ejecuta en horario de baja; los CREATE INDEX ONLINE requieren Enterprise (opcional).

  Tras crear índices: UPDATE STATISTICS dbo.Ticket WITH FULLSCAN; (o por muestra según política)
*/
USE [casa];
GO

/* Ticket: seek por la clave de join desde Montos_Ticket + cobertura de columnas del SELECT */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Ticket_POS_XML_Join'
      AND object_id = OBJECT_ID(N'dbo.Ticket', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Ticket_POS_XML_Join]
    ON [dbo].[Ticket] (
        [emp_codigo],
        [suc_codigo],
        [usu_codigo],
        [fecha],
        [caja],
        [ticket]
    )
    INCLUDE ([producto], [cantidad], [precio], [descuento], [secuencia]);
END
GO

/* Montos_Ticket: seek cuando filtras por ticket / fecha / sucursal (típico en FE) */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Montos_Ticket_POS_XML_Join'
      AND object_id = OBJECT_ID(N'dbo.Montos_Ticket', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Montos_Ticket_POS_XML_Join]
    ON [dbo].[Montos_Ticket] (
        [emp_codigo],
        [suc_codigo],
        [usu_codigo],
        [fecha],
        [caja],
        [ticket]
    );
END
GO

/* Productos: omitir si ya tienes PK/índice clustered (emp_codigo, pro_codigo) */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Productos_emp_pro' AND object_id = OBJECT_ID(N'dbo.Productos', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Productos_emp_pro]
    ON [dbo].[Productos] ([emp_codigo], [pro_codigo])
    INCLUDE ([pro_nombre], [pro_montoitbis], [pro_servicio], [UnidadID]);
END
GO

/* PARAMETROS: tabla chica; índice por empresa ayuda al agregado GROUP BY emp_codigo */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_PARAMETROS_emp_itbis'
      AND object_id = OBJECT_ID(N'dbo.PARAMETROS', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PARAMETROS_emp_itbis]
    ON [dbo].[PARAMETROS] ([emp_codigo])
    INCLUDE ([par_itbisincluido]);
END
GO

/*
  --- Opcional: alineado al WHERE de spFacturasDetalleXMLPOSGet / spFacturasXMLPOSGet
  (emp_codigo, suc_codigo, ticket, usu_codigo, caja) sin depender de fecha en el primer seek.
  Útil si el optimizador no elige bien IX_*_POS_XML_Join. Ajusta INCLUDE según columnas que lea Montos_Ticket.
*/
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Montos_Ticket_spPOS_Filter'
      AND object_id = OBJECT_ID(N'dbo.Montos_Ticket', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Montos_Ticket_spPOS_Filter]
    ON [dbo].[Montos_Ticket] (
        [emp_codigo],
        [suc_codigo],
        [ticket],
        [usu_codigo],
        [caja]
    )
    INCLUDE ([fecha]);
    /* Ampliar INCLUDE con columnas de Montos_Ticket que proyecte el SP/vista si quieres evitar lookups. */
END
GO

/* UnidadMedida: JOIN por UnidadID casteado a VARCHAR(1); si la tabla es grande, índice en UnidadID ayuda */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_UnidadMedida_UnidadID'
      AND object_id = OBJECT_ID(N'dbo.UnidadMedida', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_UnidadMedida_UnidadID]
    ON [dbo].[UnidadMedida] ([UnidadID])
    INCLUDE ([Cod_DGI]);
END
GO
