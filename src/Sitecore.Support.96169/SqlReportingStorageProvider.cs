using Sitecore.Analytics.Aggregation;
using Sitecore.Analytics.Automation.Aggregation.Data;
using Sitecore.Configuration;
using Sitecore.Data.DataProviders.Sql;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web;

namespace Sitecore.Support.Analytics.Aggregation
{
    public class SqlReportingStorageProvider : Sitecore.Analytics.Aggregation.SqlReportingStorageProvider
    {
        public SqlReportingStorageProvider(SqlDataApi sqlDataApi) : base(sqlDataApi)
        {
        }

        public SqlReportingStorageProvider(string connectionStringName) : base(connectionStringName)
        {
        }

        public SqlReportingStorageProvider(string connectionStringName, CutoffBehavior cutoffBehavior) : base(connectionStringName, cutoffBehavior)
        {
        }

        public SqlReportingStorageProvider(string connectionStringName, string cutoffBehavior) : base(connectionStringName, cutoffBehavior)
        {
        }

        [Obsolete]
        public SqlReportingStorageProvider(string connectionStringName, AggregatedStorageRole storageRole) : base(connectionStringName, storageRole)
        {
        }

        protected override void TruncateAggregationTrails()
        {
            try
            {
                this.TruncateAggregationTrail("[Trail_Interactions]");
            }
            catch (Exception exception)
            {
                Log.Error("Failed to truncate the interaction aggregation trail table.", exception, this);
            }
            try
            {
                this.TruncateAggregationTrail("[Trail_AutomationStates]");
            }
            catch (Exception exception2)
            {
                Log.Error("Failed to truncate the automation state aggregation trail table.", exception2, this);
            }
        }

        protected new void TruncateAggregationTrail(string tableName)
        {
            SqlDataApi dataApi = base.DataApi;
            if (dataApi != null && base.TrailLength > TimeSpan.Zero)
            {
                int num = 1;
                do
                {
                    DateTime dateTime = DateTime.UtcNow - base.TrailLength;
                    SqlParameter parameter = new SqlParameter("@date", dateTime);
                    SqlParameter parameter2 = new SqlParameter("@limit", 8096);
                    string str = string.Format(CultureInfo.InvariantCulture, "DELETE TOP (@limit) FROM {0} WHERE ([Processed] < @date);", new object[]
                    {
                        tableName
                    });
                    Factory.GetRetryer().ExecuteNoResult(delegate
                    {
                        using (DataProviderTransaction dataProviderTransaction = dataApi.CreateTransaction())
                        {
                            using (IDbCommand dbCommand = new SqlCommand())
                            {
                                dbCommand.CommandText = str;
                                dbCommand.CommandType = CommandType.Text;
                                dbCommand.Connection = dataProviderTransaction.Transaction.Connection;
                                dbCommand.Transaction = dataProviderTransaction.Transaction;
                                dbCommand.Parameters.Add(parameter);
                                dbCommand.Parameters.Add(parameter2);
                                num = dbCommand.ExecuteNonQuery();
                            }
                            dataProviderTransaction.Complete();
                        }
                    });
                }
                while (num > 0);
            }
        }
    }
}