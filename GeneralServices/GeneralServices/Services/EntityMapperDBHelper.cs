﻿using GeneralServices.Helpers;
using GeneralServices.Models;
using System;
using System.Data;
using System.Data.SqlClient;
using static GeneralServices.Enums;

namespace GeneralServices.Services
{
    internal static class EntityMapperDBHelper
    {
        #region Private Methods
        private static DataTable createEmpty_EntityPropertiesTable()
        {
            DataTable dtEntityProperties = new DataTable();
            dtEntityProperties.Columns.Add("EntityPropertyID", typeof(int));
            dtEntityProperties.Columns.Add("EntityPropertyName", typeof(string));
            dtEntityProperties.Columns.Add("EntityTypeID", typeof(int));
            return dtEntityProperties;
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

        private static int deleteFromTableByEntityTypeID(string Table, int ID, SqlConnection connection)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;
            string cmdString = string.Format("DELETE FROM {0} WHERE EntityTypeID = {1}; SELECT @@ROWCOUNT", Table, ID);

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = cmdString;

                rows = command.ExecuteNonQuery();
            }

            return rows;
        }

        private static int addEntityTypeLookupEntry(SqlConnection connection, EntityTypeLookup EntityTypeLookupEntry)
        {
            int rows = Consts.SQL_INVALID_ROW_COUNT;
            string cmdString = string.Format("INSERT INTO {0} (EntityTypeID,EntityTypeName) VALUES (@etid, @etn)", Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE);

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
        #endregion

        internal static bool SaveEntityMapping(string connectionString, EntityTypeLookup EntityEntry)
        {
            bool actionResult = false;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Wrap insert actions with transactions
                    actionResult = DBHelper.Transaction(connection, "__INSERT_NEW_MAPPING", SQL_TransactionCommands.Begin);

                    if (actionResult)
                    {
                        try
                        {
                            int rows = addEntityTypeLookupEntry(connection, EntityEntry);
                            if (rows == Consts.SQL_INVALID_ROW_COUNT || rows == Consts.SQL_NO_ROWS_AFFECTED)
                            {
                                throw new Exception(string.Format("{0} : Unable to add entry to {1}.", Reflection.GetCurrentMethodName(), 
                                        Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE));
                            }

                            rows = addEntityPropertiesToLookupTable(connection, EntityEntry);
                            if (rows == Consts.SQL_INVALID_ROW_COUNT)
                            {
                                throw new Exception(string.Format("{0} : Unable to add entity properties mapping to {1}.", Reflection.GetCurrentMethodName(), 
                                        Consts.SQL_TABLES_ENTITY_PROPERTY_LOOKUP_TABLE));
                            }
                        }
                        catch (Exception internalEx)
                        {
                            DBHelper.Transaction(connection, "__INSERT_NEW_MAPPING", SQL_TransactionCommands.Rollback);
                            throw internalEx;
                        }

                        actionResult = DBHelper.Transaction(connection, "__INSERT_NEW_MAPPING", SQL_TransactionCommands.Commit);
                    }

                    connection.Close();
                }
            }
            catch (Exception Ex)
            {
                throw new Exception(string.Format("{0} : Save mapping orchestration method failed.\r\n{1}", Reflection.GetCallingMethodName(), Ex.Message));
            }
            return actionResult;
        }

        internal static void createMappingTables(SqlConnection connection)
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

        internal static bool createHelperUserDefinedTypes(SqlConnection connection)
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

        internal static bool createHelperStoredProcedures(SqlConnection connection)
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

        internal static DataTable loadDomainEntityTypeMapping(string connectionString)
        {
            DataTable dtEntityMapping = new DataTable();

            string cmdString = string.Format("IF EXISTS (SELECT * FROM SYS.tables WHERE object_id = object_id('EntityTypeLookup')) "+ 
                "SELECT EntityTypeName, EntityTypeID FROM {0}", Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = cmdString;

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            dtEntityMapping.Load(reader);
                        }
                    }

                    connection.Close();
                }
            }
            catch (Exception SqlEx)
            {
                throw SqlEx;
            }

            return dtEntityMapping;
        }

        internal static DataTable loadDomainEntityPropertyMapping(string connectionString)
        {
            DataTable dtEntityMapping = new DataTable();

            string cmdString = string.Format("IF EXISTS (SELECT * FROM SYS.tables WHERE object_id = object_id('EntityTypeLookup')) " +
                "SELECT EntityPropertyID, EntityPropertyName FROM {0}", Consts.SQL_TABLES_ENTITY_PROPERTY_LOOKUP_TABLE);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = cmdString;

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            dtEntityMapping.Load(reader);
                        }
                    }

                    connection.Close();
                }
            }
            catch (Exception SqlEx)
            {
                throw SqlEx;
            }

            return dtEntityMapping;
        }

        internal static bool RemoveEntityMapping(int entityTypeID, string connectionString)
        {
            bool actionResult = false;
            int rows = Consts.SQL_INVALID_ROW_COUNT;

            if (entityTypeID != 0)
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        actionResult = DBHelper.Transaction(connection, "__DELETE_FROM_ETL", SQL_TransactionCommands.Begin);
                        // If we succeeded to open transaction, then begin deletion
                        if (actionResult)
                        {
                            try
                            {
                                rows = deleteFromTableByEntityTypeID(Consts.SQL_TABLES_ENTITY_PROPERTY_LOOKUP_TABLE, entityTypeID, connection);
                                if (rows != Consts.SQL_INVALID_ROW_COUNT)
                                {
                                    rows = deleteFromTableByEntityTypeID(Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE, entityTypeID, connection);
                                }
                                else
                                {
                                    DBHelper.Transaction(connection, "__DELETE_FROM_ETL", SQL_TransactionCommands.Rollback);
                                    throw new Exception(string.Format("{0} : Unable to delete entity mapping. rows = -1"));
                                }

                            }
                            catch (Exception internalEx)
                            {
                                DBHelper.Transaction(connection, "__DELETE_FROM_ETL", SQL_TransactionCommands.Rollback);
                                throw internalEx;
                            }
                            DBHelper.Transaction(connection, "__DELETE_FROM_ETL", SQL_TransactionCommands.Commit);
                        }
                        
                        connection.Close();
                    }
                }
                catch (Exception Ex)
                {
                    actionResult = false;
                    throw new Exception(string.Format("{0} : Unable to remove existing entity mapping for EntityTypeID {1} : {2}",
                        Reflection.GetCurrentMethodName(), entityTypeID, Ex.Message));
                }
            }

            return actionResult || rows != Consts.SQL_INVALID_ROW_COUNT;
        }

        internal static int GetEntityTypeLookupID(Type EntityType, string connectionString)
        {
            int entityID = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand())
                    {
                        string TypeName = EntityType.FullName;
                        command.Connection = connection;
                        command.CommandText = string.Format("SELECT EntityTypeID FROM {0} WHERE EntityTypeName = '{1}'", Consts.SQL_TABLES_ENTITY_TYPE_LOOKUP_TABLE, TypeName);

                        entityID = (int)command.ExecuteScalar();
                    }

                    connection.Close();
                }
            }
            catch (Exception Ex)
            {
                throw new Exception(string.Format("{0} : Unable to get entity type ID for entity type {1}.\r\n{2}", Reflection.GetCurrentMethodName(), EntityType.Name, Ex.Message));
            }

            return entityID;
        }

        internal static string GetEntityPropertyNameByID(int PropertyID, string connectionString)
        {
            string entityPropertyName = "";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = string.Format("SELECT EntityPropertyName FROM {0} WHERE EntityPropertyID = @entityPropertyID", Consts.SQL_TABLES_ENTITY_PROPERTY_LOOKUP_TABLE);

                        command.Parameters.AddWithValue("@entityPropertyID", PropertyID);

                        entityPropertyName = (string)command.ExecuteScalar();
                    }

                    connection.Close();
                }
            }
            catch (Exception Ex)
            {
                throw new Exception(string.Format("{0} : Unable to get domain entity property name by ID. {1}", Reflection.GetCurrentMethodName(), Ex.Message), Ex);
            }

            return entityPropertyName;
        }
    }
}
