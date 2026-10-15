using System.Data;
using Dapper;

namespace PROManagementSystem.Infrastructure.Persistence.Sql;

/// <summary>
/// Registers process-wide Dapper handlers that map PostgreSQL <c>date</c>
/// columns to <see cref="DateOnly"/> (and <see cref="DateOnly"/>?).
///
/// The Dapper read repositories (retainer/service-fee invoice lists and
/// details, agent invoice lookup) project those columns into DTOs whose
/// properties are now <see cref="DateOnly"/>. Npgsql returns a
/// <see cref="DateOnly"/> for a <c>date</c> column, so the handler simply
/// preserves that value while remaining tolerant of a <see cref="DateTime"/>
/// or ISO-8601 string if a provider ever hands one back.
/// </summary>
public static class DapperDateOnlyTypeHandlers
{
    private static int _registered;

    /// <summary>
    /// Idempotently installs the handlers. Safe to call once at startup.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyHandler());
    }

    private static DateOnly ToDateOnly(object value) => value switch
    {
        DateOnly date => date,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.Parse(
            text,
            System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new DataException(
            $"Cannot convert '{value}' of type " +
            $"'{value.GetType().FullName}' to DateOnly.")
    };

    private sealed class DateOnlyHandler
        : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value)
            => ToDateOnly(value);

        public override void SetValue(
            IDbDataParameter parameter,
            DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value;
        }
    }

    private sealed class NullableDateOnlyHandler
        : SqlMapper.TypeHandler<DateOnly?>
    {
        public override DateOnly? Parse(object value)
            => value is null or DBNull
                ? null
                : ToDateOnly(value);

        public override void SetValue(
            IDbDataParameter parameter,
            DateOnly? value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.HasValue
                ? value.Value
                : DBNull.Value;
        }
    }
}
