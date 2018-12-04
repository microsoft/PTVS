// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Data.Odbc;
using System.Data.SqlClient;

namespace Microsoft.CookiecutterTools.Model {
    static class ConnectionStringConverter {
        public const string OdbcSqlDriver = "{SQL Server}";

        public const string OdbcDriverKey = "Driver";
        public const string OdbcServerKey = "Server";
        public const string OdbcDatabaseKey = "Database";
        public const string OdbcUidKey = "Uid";
        public const string OdbcPasswordKey = "Pwd";
        public const string OdbcTrustedConnectionKey = "Trusted_Connection";

        /// <summary>
        /// Converts SQL Client (.NET) connection string to the ODBC connection string.
        /// </summary>
        public static string SqlClientToOdbc(this string sqlClientString) {
            if(string.IsNullOrEmpty(sqlClientString)) {
                return null;
            }
            try {
                var sql = new SqlConnectionStringBuilder(sqlClientString);
                var odbc = new OdbcConnectionStringBuilder();
                odbc[OdbcDriverKey] = OdbcSqlDriver;
                odbc[OdbcServerKey] = sql.DataSource;
                odbc[OdbcDatabaseKey] = sql.InitialCatalog;
                if (sql.IntegratedSecurity) {
                    odbc[OdbcTrustedConnectionKey]  = "yes";
                } else {
                    odbc[OdbcUidKey] = sql.UserID;
                    odbc[OdbcPasswordKey] = sql.Password;
                }
                return odbc.ConnectionString;
            } catch (ArgumentException) { }
            return null;
        }

        /// <summary>
        /// Converts ODBC connection string to the SQL Client (.NET).
        /// </summary>
        public static string OdbcToSqlClient(this string odbcString) {
            if (string.IsNullOrEmpty(odbcString)) {
                return null;
            }
            try {
                var odbc = new OdbcConnectionStringBuilder(odbcString);
                var server= odbc.GetValue(OdbcServerKey);
                var database = odbc.GetValue(OdbcDatabaseKey);
                if (!string.IsNullOrWhiteSpace(server) && !string.IsNullOrWhiteSpace(database)) {
                    var sql = new SqlConnectionStringBuilder();
                    sql.DataSource = server;
                    sql.InitialCatalog = database;

                    if (odbc.ContainsKey(OdbcUidKey)) {
                        //Standard Connection
                        sql.IntegratedSecurity = false;
                        sql.UserID = odbc.GetValue(OdbcUidKey);
                        sql.Password = odbc.GetValue(OdbcPasswordKey);
                    } else {
                        //Trusted Connection
                        sql.IntegratedSecurity = true;
                    }
                    return sql.ConnectionString;
                }
            } catch(ArgumentException) { }
            return null;
        }

        public static string GetValue(this string odbcString, string key) {
            var odbc = new OdbcConnectionStringBuilder(odbcString);
            return odbc.GetValue(key);
        }

        private static string GetValue(this OdbcConnectionStringBuilder odbc, string key) {
            object oValue;
            odbc.TryGetValue(key, out oValue);
            return oValue as string;
        }
    }
}
