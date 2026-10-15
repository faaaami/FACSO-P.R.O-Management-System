using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DateOnlyCalendarDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These columns are calendar dates, not instants. They were stored
            // as "timestamp with time zone"; convert them by taking the UTC
            // calendar date, which is deterministic and independent of the
            // server/session time zone. The provider's default cast would use
            // the session time zone and silently shift values near midnight.
            //
            // This migration was scaffolded first (which produced bare
            // AlterColumn operations using the provider default cast) and then
            // rewritten by hand to pin the conversion to UTC.

            migrationBuilder.Sql(
                @"ALTER TABLE ""service_contracts""
                    ALTER COLUMN ""StartDate"" TYPE date
                    USING (""StartDate"" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""service_contracts""
                    ALTER COLUMN ""EndDate"" TYPE date
                    USING (""EndDate"" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""retainer_invoices""
                    ALTER COLUMN ""InvoiceDate"" TYPE date
                    USING (""InvoiceDate"" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""retainer_invoices""
                    ALTER COLUMN ""DueDate"" TYPE date
                    USING (""DueDate"" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""retainer_invoices""
                    ALTER COLUMN ""PeriodStart"" TYPE date
                    USING (""PeriodStart"" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""retainer_invoices""
                    ALTER COLUMN ""PeriodEnd"" TYPE date
                    USING (""PeriodEnd"" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""service_fee_invoices""
                    ALTER COLUMN ""InvoiceDate"" TYPE date
                    USING (""InvoiceDate"" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.Sql(
                @"ALTER TABLE ""service_fee_invoices""
                    ALTER COLUMN ""DueDate"" TYPE date
                    USING (""DueDate"" AT TIME ZONE 'UTC')::date;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately non-reversible. A "date" carries no time of day, so
            // converting it back to "timestamp with time zone" has to invent
            // one; the original instant (and its time zone) is permanently
            // lost. Re-adding time-of-day information must be a conscious,
            // explicit decision rather than a silent migration default.
            throw new NotSupportedException(
                "DateOnlyCalendarDates cannot be reverted: converting a " +
                "calendar date back to a timestamp would fabricate a time " +
                "of day that was never stored.");
        }
    }
}
