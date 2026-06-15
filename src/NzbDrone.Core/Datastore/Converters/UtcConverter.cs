using System;
using System.Data;
using System.Globalization;
using Dapper;

namespace NzbDrone.Core.Datastore.Converters
{
    public class DapperUtcConverter : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.Value = value.ToUniversalTime();
        }

        public override DateTime Parse(object value)
        {
            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            if (value is string text && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed;
            }

            return (DateTime)value;
        }
    }
}
