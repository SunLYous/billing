using Billing.Domain.Interfaces;
using Billing.Domain.Models;
using Npgsql;
using NpgsqlTypes;

namespace Billing.Infrastructure.Repositories;

public sealed class BulkRepository : IBulkRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public BulkRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task BulkInsertCallsAsync(IReadOnlyList<Call> calls, CancellationToken ct = default)
    {
        if (calls.Count == 0) return;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY calls (start_time, end_time, calling_party, called_party, " +
            "call_direction, disposition, duration, billable_seconds, " +
            "pbx_charge, account_code, call_id, trunk_name, batch_id) " +
            "FROM STDIN (FORMAT BINARY)", ct);

        foreach (var c in calls)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(c.StartTime, NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(c.EndTime, NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(c.CallingParty, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(c.CalledParty, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(c.CallDirection, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(c.Disposition, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(c.Duration, NpgsqlDbType.Integer, ct);
            await writer.WriteAsync(c.BillableSeconds, NpgsqlDbType.Integer, ct);
            await writer.WriteAsync(c.PbxCharge, NpgsqlDbType.Numeric, ct);
            await WriteNullableString(writer, c.AccountCode, ct);
            await writer.WriteAsync(c.CallId, NpgsqlDbType.Varchar, ct);
            await WriteNullableString(writer, c.TrunkName, ct);
            await writer.WriteAsync(c.BatchId, NpgsqlDbType.Uuid, ct);
        }

        await writer.CompleteAsync(ct);

        await using var cmd = _dataSource.CreateCommand(
            "SELECT id, call_id FROM calls WHERE batch_id = $1 ORDER BY id");
        cmd.Parameters.AddWithValue(calls[0].BatchId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var byCallId = calls.ToDictionary(c => c.CallId);

        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt64(0);
            var callId = reader.GetString(1);
            if (byCallId.TryGetValue(callId, out var call))
                SetId(call, id);
        }
    }

    public async Task BulkInsertTariffsAsync(IReadOnlyList<Tariff> tariffs, CancellationToken ct = default)
    {
        if (tariffs.Count == 0) return;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY tariffs (prefix, destination, rate_per_minute, connection_fee, " +
            "timeband, weekday, priority, effective_date, expiry_date, batch_id) " +
            "FROM STDIN (FORMAT BINARY)", ct);

        foreach (var t in tariffs)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(t.Prefix, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(t.Destination, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(t.RatePerMinute, NpgsqlDbType.Numeric, ct);
            await writer.WriteAsync(t.ConnectionFee, NpgsqlDbType.Numeric, ct);
            await WriteNullableString(writer, t.Timeband, ct);
            await WriteNullableString(writer, t.Weekday, ct);
            await writer.WriteAsync(t.Priority, NpgsqlDbType.Integer, ct);

            if (t.EffectiveDate.HasValue)
                await writer.WriteAsync(t.EffectiveDate.Value, NpgsqlDbType.Date, ct);
            else
                await writer.WriteNullAsync(ct);

            if (t.ExpiryDate.HasValue)
                await writer.WriteAsync(t.ExpiryDate.Value, NpgsqlDbType.Date, ct);
            else
                await writer.WriteNullAsync(ct);

            await writer.WriteAsync(t.BatchId, NpgsqlDbType.Uuid, ct);
        }

        await writer.CompleteAsync(ct);

        await using var cmd = _dataSource.CreateCommand(
            "SELECT id, prefix, priority FROM tariffs WHERE batch_id = $1 ORDER BY id");
        cmd.Parameters.AddWithValue(tariffs[0].BatchId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var tariffQueue = new Queue<Tariff>(tariffs);

        while (await reader.ReadAsync(ct) && tariffQueue.Count > 0)
        {
            var id = reader.GetInt32(0);
            var tariff = tariffQueue.Dequeue();
            SetId(tariff, id);
        }
    }

    public async Task BulkInsertSubscribersAsync(IReadOnlyList<Subscriber> subscribers, CancellationToken ct = default)
    {
        if (subscribers.Count == 0) return;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY subscribers (phone_number, client_name, batch_id) " +
            "FROM STDIN (FORMAT BINARY)", ct);

        foreach (var s in subscribers)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(s.PhoneNumber, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(s.ClientName, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(s.BatchId, NpgsqlDbType.Uuid, ct);
        }

        await writer.CompleteAsync(ct);
    }

    public async Task BulkInsertRatedCallsAsync(IReadOnlyList<RatedCall> ratedCalls, CancellationToken ct = default)
    {
        if (ratedCalls.Count == 0) return;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY rated_calls (call_id, tariff_id, connection_fee, minutes_cost, calculated_cost, batch_id) " +
            "FROM STDIN (FORMAT BINARY)", ct);

        foreach (var r in ratedCalls)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(r.CallId, NpgsqlDbType.Bigint, ct);
            await writer.WriteAsync(r.TariffId, NpgsqlDbType.Integer, ct);
            await writer.WriteAsync(r.ConnectionFee, NpgsqlDbType.Numeric, ct);
            await writer.WriteAsync(r.MinutesCost, NpgsqlDbType.Numeric, ct);
            await writer.WriteAsync(r.CalculatedCost, NpgsqlDbType.Numeric, ct);
            await writer.WriteAsync(r.BatchId, NpgsqlDbType.Uuid, ct);
        }

        await writer.CompleteAsync(ct);
    }

    private static async Task WriteNullableString(
        NpgsqlBinaryImporter writer, string? value, CancellationToken ct)
    {
        if (value is not null)
            await writer.WriteAsync(value, NpgsqlDbType.Varchar, ct);
        else
            await writer.WriteNullAsync(ct);
    }

    private static void SetId<T>(T entity, long id)
    {
        var prop = typeof(T).GetProperty("Id")!;
        prop.SetValue(entity, Convert.ChangeType(id, prop.PropertyType));
    }
}
