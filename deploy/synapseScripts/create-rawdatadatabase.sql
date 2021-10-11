CREATE DATABASE climatebox_rawdata
GO

-- TODO: make sure that the connectionstring to the datalake container that contains the raw messages is correct
--       We also assume that the hierarchy/folder structure within this container is year={YYYY}/month={MM}/day={DD}/hour={HH}/minute={mm}

CREATE VIEW telemetrydata
AS
    SELECT *, rows.filepath(1) as [Year], rows.filepath(2) AS [Month], rows.filepath(3) AS [Day], rows.filepath(4) AS [Hour], rows.filepath(5) AS [Minute]
    FROM OPENROWSET(
        BULK 'https://fgdatalakepocstorage.blob.core.windows.net/climateboxes-rawdata/year=*/month=*/day=*/hour=*/minute=*/*.json',
        FORMAT = 'csv',
        FIELDTERMINATOR ='0x0b',
        FIELDQUOTE = '0x0b'
    ) WITH (doc nvarchar(max)) AS rows