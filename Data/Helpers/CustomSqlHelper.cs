using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HISWEBAPI.Data.Helpers
{
    public class CustomSqlHelper : ICustomSqlHelper
    {
        private readonly IConfiguration _configuration;
        public string gstrCommandText = "";
        private SqlConnection con;

        public CustomSqlHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnectionString()
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

            return new SqlConnection(connectionString);
        }

        public static SqlTransaction getSqlTransaction(SqlConnection con)
        {
            if (con.State == ConnectionState.Closed)
                con.Open();

            return con.BeginTransaction(IsolationLevel.Serializable);
        }

        #region DML Methods

        public dynamic DML(SqlTransaction tnx, SqlConnection con, string sqlCmd, CommandType commandType, object inParameters = null, object outParameters = null)
        {
            SqlCommand cmd = new SqlCommand(sqlCmd, tnx.Connection, tnx);
            cmd.CommandType = commandType;
            CreateCmdParameters(cmd, inParameters, outParameters);

            return outParameters != null ? cmd.ExecuteScalar() : cmd.ExecuteNonQuery();
        }

        public dynamic DML(SqlTransaction tnx, string sqlCmd, CommandType commandType, object inParameters = null, object outParameters = null)
        {
            SqlCommand cmd = new SqlCommand(sqlCmd, tnx.Connection, tnx);
            cmd.CommandType = commandType;
            CreateCmdParameters(cmd, inParameters, outParameters);

            return outParameters != null ? cmd.ExecuteScalar() : cmd.ExecuteNonQuery();
        }

        public dynamic DML(string sqlCmd, CommandType commandType, object inParameters = null, object outParameters = null)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand cmd = new SqlCommand(sqlCmd, con);
                cmd.CommandType = commandType;
                CreateCmdParameters(cmd, inParameters, outParameters);

                int rowsAffected = cmd.ExecuteNonQuery();

                if (outParameters == null)
                    return rowsAffected;

                // Return the SP's @Result output (new/updated id, or negative status code like -1 = duplicate).
                // Fall back to rows affected when @Result is NULL/non-numeric (e.g. SCOPE_IDENTITY() after a DELETE),
                // which preserves the previous behaviour for those procedures.
                var outValue = cmd.Parameters["@Result"].Value;
                if (outValue != null && outValue != DBNull.Value && int.TryParse(outValue.ToString(), out int resultValue))
                    return resultValue;

                return rowsAffected;
            }
            finally
            {
                CloseConnection(con);
            }
        }

        #endregion

        #region ExecuteScalar Methods

        public dynamic ExecuteScalar(string sqlCmd, object inParameters)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand cmd = new SqlCommand(sqlCmd, con);
                cmd.CommandType = CommandType.Text;
                CreateCmdParameters(cmd, inParameters, null);

                return cmd.ExecuteScalar();
            }
            finally
            {
                CloseConnection(con);
            }
        }

        public dynamic ExecuteScalar(string sqlCmd, CommandType commandType, object inParameters)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand cmd = new SqlCommand(sqlCmd, con);
                cmd.CommandType = commandType;
                CreateCmdParameters(cmd, inParameters, null);

                return cmd.ExecuteScalar();
            }
            finally
            {
                CloseConnection(con);
            }
        }

        public dynamic ExecuteScalar(SqlTransaction tnx, string sqlCmd, CommandType commandType, object inParameters = null, object outParameters = null)
        {
            SqlCommand cmd = new SqlCommand(sqlCmd, tnx.Connection, tnx);
            cmd.CommandType = commandType;
            CreateCmdParameters(cmd, inParameters, outParameters);
            return cmd.ExecuteScalar();
        }

        public dynamic ExecuteScalar(string sqlCmd, CommandType commandType, object inParameters = null, object outParameters = null)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand cmd = new SqlCommand(sqlCmd, con);
                cmd.CommandType = commandType;
                CreateCmdParameters(cmd, inParameters, null);

                return cmd.ExecuteScalar();
            }
            finally
            {
                CloseConnection(con);
            }
        }

        #endregion

        #region GetDataTable Methods

        public DataTable GetDataTable(string sqlCmd, CommandType commandType, object inParameters = null)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand cmd = new SqlCommand(sqlCmd, con);
                cmd.CommandType = commandType;
                CreateCmdParameters(cmd, inParameters, null);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataSet ds = new DataSet();
                da.Fill(ds);

                return ds.Tables[0];
            }
            finally
            {
                CloseConnection(con);
            }
        }

        public DataTable GetDataTable(SqlTransaction tnx, string sqlCmd, CommandType commandType, object inParameters = null)
        {
            SqlCommand cmd = new SqlCommand(sqlCmd, tnx.Connection, tnx);
            cmd.CommandType = commandType;
            CreateCmdParameters(cmd, inParameters, null);

            SqlDataAdapter da = new SqlDataAdapter(cmd);
            DataSet ds = new DataSet();
            da.Fill(ds);

            return ds.Tables[0];
        }

        #endregion

        #region GetDataSet Methods

        public DataSet GetDataSet(string sqlCmd, CommandType commandType, object inParameters = null)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand cmd = new SqlCommand(sqlCmd, con);
                cmd.CommandType = commandType;
                CreateCmdParameters(cmd, inParameters, null);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataSet ds = new DataSet();
                da.Fill(ds);

                return ds;
            }
            finally
            {
                CloseConnection(con);
            }
        }

        public DataSet GetDataSet(string sqlCmd, CommandType commandType, object inParameters = null, string TableName = "")
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand cmd = new SqlCommand(sqlCmd, con);
                cmd.CommandType = commandType;
                CreateCmdParameters(cmd, inParameters, null);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataSet ds = new DataSet();
                da.Fill(ds, TableName);

                return ds;
            }
            finally
            {
                CloseConnection(con);
            }
        }

        #endregion

        #region RunProcedure Methods

        public string RunProcedureRetInsert(string storedProcName, IDataParameter[] parameters)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand command = BuildIntCommand(storedProcName, parameters);
                gstrCommandText = command.Parameters.Count.ToString();
                command.ExecuteNonQuery();

                return command.Parameters["@RetVal"].Value?.ToString() ?? "";
            }
            finally
            {
                CloseConnection(con);
            }
        }

        public DataSet RunProcedure(string storedProcName, IDataParameter[] parameters, string tableName)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlDataAdapter sqlDA = new SqlDataAdapter();
                sqlDA.SelectCommand = BuildQueryCommand(storedProcName, parameters);

                DataSet dSet = new DataSet();
                sqlDA.Fill(dSet, tableName);

                return dSet;
            }
            finally
            {
                CloseConnection(con);
            }
        }

        public DataSet RunProcedure(string storedProcName, string tableName)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlDataAdapter sqlDA = new SqlDataAdapter();
                sqlDA.SelectCommand = BuildQueryCommand(storedProcName);

                DataSet dSet = new DataSet();
                sqlDA.Fill(dSet, tableName);

                return dSet;
            }
            finally
            {
                CloseConnection(con);
            }
        }

        public long RunProcedureInsert(string storedProcName, IDataParameter[] parameters)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand command = BuildIntCommand(storedProcName, parameters);
                gstrCommandText = command.Parameters.Count.ToString();
                command.ExecuteNonQuery();

                return long.Parse(command.Parameters["@Result"].Value.ToString());
            }
            finally
            {
                CloseConnection(con);
            }
        }

        public void RunProcedure(string storedProcName, SqlParameter[] parameters)
        {
            try
            {
                con = GetConnectionString();
                if (con.State == ConnectionState.Closed)
                    con.Open();

                SqlCommand command = new SqlCommand(storedProcName, con);
                command.CommandType = CommandType.StoredProcedure;

                foreach (SqlParameter parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }

                command.ExecuteNonQuery();

                // Output parameters are automatically populated after ExecuteNonQuery
            }
            finally
            {
                CloseConnection(con);
            }
        }

        #endregion

        #region Helper Methods

        private void CreateCmdParameters(SqlCommand cmd, object inParameters, object outParameters)
        {
            if (inParameters != null)
            {
                foreach (var prop in inParameters.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                {
                    cmd.Parameters.Add(new SqlParameter("@" + prop.Name, prop.GetValue(inParameters, null) ?? DBNull.Value));
                }
            }

            if (outParameters != null)
            {
                SqlParameter outParameter = new SqlParameter
                {
                    ParameterName = "@Result",
                    SqlDbType = SqlDbType.VarChar,
                    Size = 50,
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outParameter);
            }
        }

        private SqlCommand BuildIntCommand(string storedProcName, IDataParameter[] parameters)
        {
            return BuildQueryCommand(storedProcName, parameters);
        }

        private SqlCommand BuildQueryCommand(string storedProcName, IDataParameter[] parameters)
        {
            SqlCommand command = new SqlCommand(storedProcName, con);
            command.CommandType = CommandType.StoredProcedure;
            foreach (SqlParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
            return command;
        }

        private SqlCommand BuildQueryCommand(string storedProcName)
        {
            SqlCommand command = new SqlCommand(storedProcName, con);
            command.CommandType = CommandType.StoredProcedure;
            return command;
        }

        private void CloseConnection(SqlConnection connection)
        {
            if (connection != null)
            {
                if (connection.State == ConnectionState.Open)
                    connection.Close();
                connection.Dispose();
            }
        }

        SqlConnection? ICustomSqlHelper.GetConnectionString()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}