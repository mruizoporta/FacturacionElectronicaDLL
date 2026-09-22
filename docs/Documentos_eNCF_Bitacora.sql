/*

  Bitácora de eNCF — todos los tipos de documento (DLL FacturacionElectronicaDGII).

  Un registro por (tipo_documento + doc_clave + eNCF_nuevo); sin duplicados.



  Tipos: FACTURA, COMPRA, DESEMBOLSO, DEVOLUCION, NOTACREDITO, NOTADEBITO, POS, RESBAR



  Ejecutar en SSMS contra la base de la aplicación (ej. [REST]).

*/

USE [REST];

GO



SET ANSI_NULLS ON;

GO

SET QUOTED_IDENTIFIER ON;

GO



IF OBJECT_ID(N'dbo.TR_Facturas_eNCF_Bitacora', N'TR') IS NOT NULL

    DROP TRIGGER dbo.TR_Facturas_eNCF_Bitacora;

GO



/* -------------------------------------------------------------------------- */

/* Tabla unificada                                                            */

/* -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Documentos_eNCF_Bitacora', N'U') IS NULL

BEGIN

    CREATE TABLE dbo.Documentos_eNCF_Bitacora

    (

        id                        BIGINT IDENTITY(1,1) NOT NULL,

        fecha_registro            DATETIME2(0)         NOT NULL

                                      CONSTRAINT DF_Documentos_eNCF_Bitacora_fecha

                                      DEFAULT (SYSDATETIME()),

        tipo_documento            VARCHAR(30)          NOT NULL,

        doc_clave                 VARCHAR(120)         NOT NULL,

        emp_codigo                INT                  NOT NULL,

        suc_codigo                INT                  NOT NULL,

        fac_forma                 VARCHAR(1)           NULL,

        tfa_codigo                INT                  NULL,

        doc_numero                INT                  NULL,

        doc_numero_str            VARCHAR(30)          NULL,

        sup_codigo                INT                  NULL,

        usu_codigo                INT                  NULL,

        caja_codigo               INT                  NULL,

        secuencia_encf            INT                  NOT NULL,

        eNCF_anterior             VARCHAR(100)         NULL,

        eNCF_nuevo                VARCHAR(100)         NOT NULL,

        tipo_evento               VARCHAR(20)          NOT NULL,

        origen                    VARCHAR(30)          NULL,

        Error_DGII_anterior       BIT                  NULL,

        AceptadoDGII_anterior     BIT                  NULL,

        Error_Luganis_anterior    BIT                  NULL,

        AceptadoLuganis_anterior  BIT                  NULL,

        Enviado_DGII_anterior     BIT                  NULL,

        secuenciaUtilizada_anterior BIT                NULL,

        trackIdLuganis_anterior   VARCHAR(200)         NULL,

        rechazado                 BIT                  NOT NULL

                                      CONSTRAINT DF_Documentos_eNCF_Bitacora_rechazado

                                      DEFAULT (0),

        motivo_rechazo            NVARCHAR(MAX)        NULL,

        usuario_bd                SYSNAME              NULL,

        aplicacion                VARCHAR(128)         NULL,

        host_cliente              VARCHAR(128)         NULL,

        CONSTRAINT PK_Documentos_eNCF_Bitacora PRIMARY KEY CLUSTERED (id)

    );

END;

GO



IF NOT EXISTS (

    SELECT 1 FROM sys.indexes

    WHERE name = N'UX_Documentos_eNCF_Bitacora_Clave_ENCF'

      AND object_id = OBJECT_ID(N'dbo.Documentos_eNCF_Bitacora')

)

    CREATE UNIQUE NONCLUSTERED INDEX UX_Documentos_eNCF_Bitacora_Clave_ENCF

        ON dbo.Documentos_eNCF_Bitacora (tipo_documento, doc_clave, eNCF_nuevo);

GO



IF NOT EXISTS (

    SELECT 1 FROM sys.indexes

    WHERE name = N'IX_Documentos_eNCF_Bitacora_Tipo_Clave'

      AND object_id = OBJECT_ID(N'dbo.Documentos_eNCF_Bitacora')

)

    CREATE NONCLUSTERED INDEX IX_Documentos_eNCF_Bitacora_Tipo_Clave

        ON dbo.Documentos_eNCF_Bitacora (tipo_documento, doc_clave, secuencia_encf)

        INCLUDE (eNCF_nuevo, fecha_registro, rechazado, motivo_rechazo);

GO



/* -------------------------------------------------------------------------- */

/* Migrar datos desde Facturas_eNCF_Bitacora (versión anterior solo facturas) */

/* -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Facturas_eNCF_Bitacora', N'U') IS NOT NULL

   AND OBJECT_ID(N'dbo.Documentos_eNCF_Bitacora', N'U') IS NOT NULL

BEGIN

    INSERT INTO dbo.Documentos_eNCF_Bitacora

    (

        fecha_registro, tipo_documento, doc_clave,

        emp_codigo, suc_codigo, fac_forma, tfa_codigo, doc_numero,

        secuencia_encf, eNCF_anterior, eNCF_nuevo, tipo_evento, origen,

        Error_DGII_anterior, AceptadoDGII_anterior, Error_Luganis_anterior, AceptadoLuganis_anterior,

        Enviado_DGII_anterior, secuenciaUtilizada_anterior, trackIdLuganis_anterior,

        rechazado, motivo_rechazo, usuario_bd, aplicacion, host_cliente

    )

    SELECT

        f.fecha_registro,

        N'FACTURA',

        CONCAT(N'FACTURA|', f.emp_codigo, N'|', f.suc_codigo, N'|', f.fac_forma, N'|', f.tfa_codigo, N'|', f.fac_numero),

        f.emp_codigo, f.suc_codigo, f.fac_forma, f.tfa_codigo, f.fac_numero,

        f.secuencia_encf, f.eNCF_anterior, f.eNCF_nuevo, f.tipo_evento, f.origen,

        f.Error_DGII_anterior, f.AceptadoDGII_anterior, f.Error_Luganis_anterior, f.AceptadoLuganis_anterior,

        f.Enviado_DGII_anterior, f.secuenciaUtilizada_anterior, f.trackIdLuganis_anterior,

        f.rechazado, f.motivo_rechazo, f.usuario_bd, f.aplicacion, f.host_cliente

    FROM dbo.Facturas_eNCF_Bitacora AS f

    WHERE NOT EXISTS (

        SELECT 1

        FROM dbo.Documentos_eNCF_Bitacora AS d

        WHERE d.tipo_documento = N'FACTURA'

          AND d.doc_clave = CONCAT(N'FACTURA|', f.emp_codigo, N'|', f.suc_codigo, N'|', f.fac_forma, N'|', f.tfa_codigo, N'|', f.fac_numero)

          AND d.eNCF_nuevo = f.eNCF_nuevo

    );



    PRINT 'Migrados registros desde dbo.Facturas_eNCF_Bitacora (si había datos).';

END;

GO

