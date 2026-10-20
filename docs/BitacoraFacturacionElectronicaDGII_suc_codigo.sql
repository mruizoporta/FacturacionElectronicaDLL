/*
  Columna suc_codigo en dbo.BitacoraFacturacionElectronicaDGII.
  El numero de documento se repite entre sucursales; sin esta columna
  la DLL reutiliza el trackId de otra sucursal.

  Ejecutar en la base de la aplicacion ANTES de copiar la DLL nueva.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.BitacoraFacturacionElectronicaDGII', N'suc_codigo') IS NULL
    ALTER TABLE dbo.BitacoraFacturacionElectronicaDGII ADD suc_codigo INT NULL;
GO

UPDATE b
SET suc_codigo = s.suc_unica
FROM dbo.BitacoraFacturacionElectronicaDGII b
INNER JOIN (
    SELECT emp_codigo, MIN(suc_codigo) AS suc_unica
    FROM dbo.Sucursales
    GROUP BY emp_codigo
    HAVING COUNT(*) = 1
) s ON s.emp_codigo = b.emp_codigo
WHERE b.suc_codigo IS NULL;
GO

UPDATE b
SET suc_codigo = d.suc_codigo
FROM dbo.BitacoraFacturacionElectronicaDGII b
INNER JOIN dbo.Devolucion d
    ON d.emp_codigo = b.emp_codigo
   AND d.DEV_NUMERO = b.fac_numero
WHERE b.suc_codigo IS NULL
  AND b.tipo = 'DEVOLUCION'
  AND (
      SELECT COUNT(*)
      FROM dbo.Devolucion x
      WHERE x.emp_codigo = b.emp_codigo
        AND x.DEV_NUMERO = b.fac_numero
  ) = 1;
GO

UPDATE b
SET suc_codigo = (
    SELECT MIN(f.suc_codigo)
    FROM dbo.Facturas f
    WHERE f.emp_codigo = b.emp_codigo
      AND f.fac_numero = b.fac_numero
)
FROM dbo.BitacoraFacturacionElectronicaDGII b
WHERE b.suc_codigo IS NULL
  AND (b.tipo = 'FACTURAS' OR b.tipo IS NULL)
  AND (
      SELECT COUNT(DISTINCT x.suc_codigo)
      FROM dbo.Facturas x
      WHERE x.emp_codigo = b.emp_codigo
        AND x.fac_numero = b.fac_numero
  ) = 1;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_BitacoraFacturacionElectronicaDGII_SucTipo'
      AND object_id = OBJECT_ID(N'dbo.BitacoraFacturacionElectronicaDGII')
)
    CREATE NONCLUSTERED INDEX IX_BitacoraFacturacionElectronicaDGII_SucTipo
        ON dbo.BitacoraFacturacionElectronicaDGII (emp_codigo, suc_codigo, fac_numero, tipo, Fecha_Envio DESC);
GO
