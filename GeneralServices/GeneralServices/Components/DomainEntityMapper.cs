﻿using GeneralServices.Helpers;
using GeneralServices.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Linq;

namespace GeneralServices.Components
{
    public static class DomainEntityMapper
    {
        private static DataTable createEmpty_EntityPropertiesTable()
        {
            DataTable dtEntityProperties = new DataTable();
            dtEntityProperties.Columns.Add("EntityPropertyID", typeof(int));
            dtEntityProperties.Columns.Add("EntityPropertyName", typeof(string));
            dtEntityProperties.Columns.Add("EntityTypeID", typeof(int));
            return dtEntityProperties;
        }

        private static void createMappingTables(SqlConnection connection)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;

            if (connection != null)
            {
                try
                {
                    bool isEntityTypeLookupTableCreated = createEntityTypeLookupTable(connection);
                    if (!isEntityTypeLookupTableCreated)
                    {
                        throw new Exception(string.Format("{0} : Could not create entity type lookup table [{1}].",
                            Reflection.GetCurrentMethodName(), Consts.SQL_TABLES_ENTITY_PROPERTY_LOOKUP_TABLE));
                    }

                    bool isEntityPropertyLookupTableCreated = createEntityPropertyLookupTable(connection);
                    if (!isEntityPropertyLookupTableCreated)
                    {
                        throw new Exception(string.Format("{0} : Could not create entity property lookup table [{1}].",
                            Reflection.GetCurrentMethodName(), Consts.SQL_TABLES_ENTITY_PROPERTY_LOOKUP_TABLE));
                    }

                }
                catch (Exception sqlEx)
                {
                    throw sqlEx;
                }
            }
        }

        private static bool createHelperUserDefinedTypes(SqlConnection connection)
        {
            bool result = true;

            string cmdString = string.Format(
                            "IF NOT EXISTS(select * from sys.types where name = '{0}')" +
                            "BEGIN " +
                            "CREATE TYPE {0} AS TABLE(" +
                            "    EntityPropertyID int NOT NULL," +
                            "    EntityPropertyName NVARCHAR(100) NOT NULL," +
                            "    EntityTypeID int NOT NULL" +
                            ") " +
                            "END "
                , Consts.SQL_TYPES_UDT_DomainMapperHelper_EntityProperties);

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = cmdString;

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception sqlCommandEx)
            {
                result = false;
                throw sqlCommandEx;
            }

            return result;
        }

        private static bool createHelperStoredProcedures(SqlConnection connection)
        {
            bool result = true;

            string cmdString = string.Format(
                "IF EXISTS(select * from sys.procedures where object_id = object_id(('{0}'))) " +
                "BEGIN " +
                    "DROP PROCEDURE {0} " +
                "END "
                , Consts.SQL_PROCEDURES_USP_DomainMapperHelper_InsertIntoEntityPropertyLookupTable);

            string cmdProcString = string.Format(

                "CREATE PROCEDURE {0} " +
                    "@dtEntityProperties {1} READONLY " +
                "AS " +
                "    BEGIN " +
                "       INSERT INTO {2} " +
                "       SELECT * FROM @dtEntityProperties " +
                "    END "
                , Consts.SQL_PROCEDURES_USP_DomainMapperHelper_InsertIntoEntityPropertyLookupTable
                , Consts.SQL_TYPES_UDT_DomainMapperHelper_EntityProperties
                , Consts.SQL_TABLES_ENTITY_PROPERTY_LOOKUP_TABLE);

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;

                    command.CommandText = cmdString;
                    command.ExecuteNonQuery();

                    command.CommandText = cmdProcString;
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception sqlCommandEx)
            {
                result = false;
                throw sqlCommandEx;
            }

            return result;
        }

        private static bool createEntityTypeLookupTable(SqlConnection connection)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;
            bool result = true;

            string cmdString = string.Format(
                                        "IF NOT EXISTS(SELECT * FROM sys.tables WHERE object_id = object_id('{0}'))" +
                                        "   BEGIN" +
                                        "       CREATE TABLE {0}" +
                                        "           (" +
                                        "               ID INT PRIMARY KEY IDENTITY," +
                                        "               EntityTypeID INT NOT NULL UNIQUE," +
                                        "               EntityTypeName NVARCHAR(100) NULL," +
                                        "           )" +
                                        "END"
                                , Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE
                                );

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = cmdString;

                    rows = command.ExecuteNonQuery();
                }
            }
            catch (Exception sqlCommandEx)
            {
                result = false;
                throw sqlCommandEx;
            }

            return result;
        }

        private static bool createEntityPropertyLookupTable(SqlConnection connection)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;
            bool result = true;

            string cmdString = string.Format(
                                        "IF NOT EXISTS ( SELECT * FROM sys.tables WHERE object_id = object_id('{0}') )" +
                                        "   BEGIN" +
                                        "       CREATE TABLE {0}" +
                                        "           (" +
                                                    "ID INT IDENTITY PRIMARY KEY," +
                                                    "EntityPropertyID INT NOT NULL," +
                                                    "EntityPropertyName NVARCHAR(100) NULL," +
                                                    "EntityTypeID INT NOT NULL," +

                                                    "CONSTRAINT FK_EntityType FOREIGN KEY(EntityTypeID) REFERENCES {1}(EntityTypeID)" +
                                        "           )" +
                                        "END"
                                , Consts.SQL_TABLES_ENTITY_PROPERTY_LOOKUP_TABLE, Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE
                                );

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = cmdString;

                    rows = command.ExecuteNonQuery();
                }
            }
            catch (Exception sqlCommandEx)
            {
                result = true;
                throw sqlCommandEx;
            }

            return result;
        }

        private static int addEntityTypeLookupEntry(SqlConnection connection, EntityTypeLookup EntityTypeLookupEntry, string domainEntityTypeLookupTableName)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;
            string cmdString = string.Format("INSERT INTO {0} (EntityTypeID,EntityTypeName) VALUES (@etid, @etn)", domainEntityTypeLookupTableName);

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = cmdString;
                    command.Parameters.AddWithValue("@etid", EntityTypeLookupEntry.EntityTypeID);
                    command.Parameters.AddWithValue("@etn", EntityTypeLookupEntry.EntityTypeName);

                    rows = command.ExecuteNonQuery();
                }
            }
            catch (Exception sqlCommandEx)
            {
                throw sqlCommandEx;
            }

            return rows;
        }

        private static int addEntityPropertiesToLookupTable(SqlConnection connection, EntityTypeLookup EntityTypeLookupEntry)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;
            DataTable dtEntityProperties = createEmpty_EntityPropertiesTable();
            DataRow dtRow;

            if (EntityTypeLookupEntry != null)
            {
                foreach (EntityPropertyLookup epl in EntityTypeLookupEntry.EntityProperties)
                {
                    dtRow = dtEntityProperties.NewRow();
                    dtRow["EntityPropertyID"] = epl.EntityPropertyID;
                    dtRow["EntityPropertyName"] = epl.EntityPropertyName;
                    dtRow["EntityTypeID"] = EntityTypeLookupEntry.EntityTypeID;
                    dtEntityProperties.Rows.Add(dtRow);
                }
            }

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = Consts.SQL_PROCEDURES_USP_DomainMapperHelper_InsertIntoEntityPropertyLookupTable;

                    SqlParameter entitiesParam = command.Parameters.AddWithValue("@dtEntityProperties", dtEntityProperties);
                    entitiesParam.SqlDbType = SqlDbType.Structured;

                    rows = command.ExecuteNonQuery();
                }
            }
            catch (Exception sqlCommandEx)
            {
                throw sqlCommandEx;
            }

            return rows;
        }

        private static int addEntityProertyLookupEntry(SqlConnection connection, EntityPropertyLookup EntityPropertyLookupEntry, int EntityTypeLookupID, string domainEntityPropertyLookupTableName)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;
            string cmdString = string.Format("INSERT INTO {0} (EntityPropertyID,EntityPropertyName,EntityTypeID) VALUES (@epid, @epn,@etid)", domainEntityPropertyLookupTableName);

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = cmdString;
                    command.Parameters.AddWithValue("@epid", EntityPropertyLookupEntry.EntityPropertyID);
                    command.Parameters.AddWithValue("@epn", EntityPropertyLookupEntry.EntityPropertyName);
                    command.Parameters.AddWithValue("@etid", EntityTypeLookupID);

                    rows = command.ExecuteNonQuery();
                }
            }
            catch (Exception sqlCommandEx)
            {
                throw sqlCommandEx;
            }

            return rows;
        }

        public static EntityTypeLookup CreateEntityMap(Type EntityType)
        {
            EntityTypeLookup etl = null;

            if (EntityType != null && EntityType != typeof(object))
            {
                string typeName = EntityType.FullName;

                etl = new EntityTypeLookup()
                {
                    EntityTypeName = typeName,
                    EntityTypeID = General.calculateClassHash(EntityType).Value,
                    EntityProperties = new List<EntityPropertyLookup>()
                };

                PropertyInfo[] props = EntityType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (PropertyInfo p in props)
                {
                    EntityPropertyLookup epl = new EntityPropertyLookup()
                    {
                        EntityPropertyID = Helpers.General.calculateSingleFieldHash(p).Value,
                        EntityPropertyName = p.Name,
                        EntityTypeLookupID = etl.ID,
                        EntityType = etl
                    };

                    etl.EntityProperties.Add(epl);
                }
            }


            return etl;
        }

        public static void Initialize(SqlConnection connection)
        {
            try
            {
                createMappingTables(connection);

                createHelperUserDefinedTypes(connection);

                createHelperStoredProcedures(connection);
            }
            catch (Exception sqlEx)
            {
                throw sqlEx;
            }

        }

        public static int SaveDomainMappingToDatabase(string connectionString, EntityTypeLookup EntityTypeLookupEntry, string domainEntityTypeLookupTableName, string domainEntityPropertyLookupTableName)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;

            if (string.IsNullOrEmpty(connectionString) == false)
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        bool entityHashExists = isEntityHashExists(connection, EntityTypeLookupEntry);
                        if (entityHashExists == false)
                        {
                            rows = addEntityTypeLookupEntry(connection, EntityTypeLookupEntry, domainEntityTypeLookupTableName);
                            if (rows == Consts.SQL_INVALID_ROW_COUNT || rows == Consts.SQL_NO_ROWS_AFFECTED)
                            {
                                throw new Exception(string.Format("{0} : Unable to add entry to {1}.", Reflection.GetCurrentMethodName(), domainEntityTypeLookupTableName));
                            }

                            rows = addEntityPropertiesToLookupTable(connection, EntityTypeLookupEntry);
                            if (rows == Consts.SQL_INVALID_ROW_COUNT)
                            {
                                throw new Exception(string.Format("{0} : Unable to add entity properties mapping to {1}.", Reflection.GetCurrentMethodName(), domainEntityPropertyLookupTableName));
                            }
                        }

                        connection.Close();
                    }
                }
                catch (Exception sqlEx)
                {
                    throw sqlEx;
                }
            }

            return rows;
        }

        private static bool isEntityHashExists(SqlConnection connection, EntityTypeLookup entityType)
        {
            bool result = true;
            int rows = Consts.SQL_INVALID_ROW_COUNT;

            string cmdString = string.Format(
                            "SELECT EntityTypeID " +
                            "FROM {0} " +
                            "WHERE EntityTypeID = {1}" +
                            ") "
                , Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE, entityType.EntityTypeID);

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = cmdString;

                    rows = command.ExecuteNonQuery();
                    result = rows > 0;
                }
            }
            catch (Exception sqlCommandEx)
            {
                result = false;
                throw sqlCommandEx;
            }

            return result;
        }

        public static DataTable loadDomainEntityMapping(SqlConnection connection)
        {
            DataTable dtEntityMapping = new DataTable();

            string cmdString = string.Format("SELECT EntityTypeName, EntityTypeID FROM {0}", Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE);

            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = cmdString;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        dtEntityMapping.Load(reader);
                    }
                }
            }
            catch (Exception SqlEx)
            {
                throw SqlEx;
            }

            return dtEntityMapping;
        }

        public static void DomainMapperOrch(string connectionString, string domainModelAssemblyName)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception(string.Format("{0} : Connection string must not be empty.", Reflection.GetCurrentMethodName()));
            }

            DataTable dtDomainMapping = null;
            Dictionary<string, int> dicDomainMapping = null;

            Type[] domainModelTypes = Reflection.GetDomainTypes(domainModelAssemblyName);

            if (domainModelTypes != null && domainModelTypes.Length > 0)
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        // Load existing domain mapping, if any
                        dtDomainMapping = loadDomainEntityMapping(connection);
                        if (dtDomainMapping == null || (dtDomainMapping.Rows != null && dtDomainMapping.Rows.Count == 0))
                        {
                            // Mapping does not exist or empty
                            // create new mapping
                            Initialize(connection);
                        }
                        else
                        {
                            // convert into dictionary
                            dicDomainMapping = dtDomainMapping.AsEnumerable().ToDictionary(dr => dr.Field<string>("EntityTypeName"), dr => dr.Field<int>("EntityTypeID"));
                        }

                        if (dicDomainMapping != null)
                        {
                            int existingHash = 0;
                            foreach (Type domainModelType in domainModelTypes)
                            {
                                // Build type lookup (hash)
                                EntityTypeLookup entityMap = CreateEntityMap(domainModelType);
                                // Compare the existing hash with the new one
                                dicDomainMapping.TryGetValue(entityMap.EntityTypeName, out existingHash);
                                if (existingHash == 0 || existingHash != entityMap.EntityTypeID)
                                {
                                    // change is detected, remap the entity into DB
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("Unable to build domain mapping dicionary");
                        }

                        connection.Close();
                    }
                }
                catch (Exception Ex)
                {
                    throw Ex;
                }
            }
        }
    }
}
