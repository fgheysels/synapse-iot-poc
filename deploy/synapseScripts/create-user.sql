-- The below is no longer necessary
-- USE MASTER
-- CREATE LOGIN [frederik.gheysels@codit.eu] FROM EXTERNAL PROVIDER;

-- USE climatebox_rawdata
--CREATE USER [frederik.gheysels@codit.eu] FROM EXTERNAL PROVIDER;

-- Create a contained user in Synapse for an Azure Function
-- https://docs.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-configure?tabs=azure-powershell
USE climatebox_rawdata
CREATE USER [fg-datalakepoc-rawdataprocessor] FROM EXTERNAL PROVIDER;

ALTER ROLE db_datareader ADD MEMBER [fg-datalakepoc-rawdataprocessor];

GRANT ADMINISTER DATABASE BULK OPERATIONS TO [fg-datalakepoc-rawdataprocessor]

-- Connect to the Synapse DB via this connectionstring:
