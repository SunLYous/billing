using Billing.Domain.Interfaces;
using Billing.Domain.Models;
using Npgsql;

namespace Billing.Infrastructure.Repositories;

public sealed class ResultRepository : IResultRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ResultRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<IReadOnlyList<RatedCall>> GetByBatchIdAsync(
        Guid batchId, int page, int pageSize, CancellationToken ct = default)
    {
        const string sql = """
            SELECT rc.id, rc.connection_fee, rc.minutes_cost, rc.calculated_cost,
                   rc.call_id, rc.tariff_id, rc.batch_id,
                   c.start_time, c.end_time, c.calling_party, c.called_party,
                   c.call_direction, c.disposition, c.duration, c.billable_seconds,
                   c.pbx_charge, c.account_code, c.call_id AS cdr_call_id, c.trunk_name,
                   t.prefix, t.destination, t.rate_per_minute, t.connection_fee AS t_conn_fee
            FROM rated_calls rc
            JOIN calls c ON c.id = rc.call_id
            JOIN tariffs t ON t.id = rc.tariff_id
            WHERE rc.batch_id = $1
            ORDER BY c.start_time
            LIMIT $2 OFFSET $3
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(batchId);
        cmd.Parameters.AddWithValue(pageSize);
        cmd.Parameters.AddWithValue((page - 1) * pageSize);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<RatedCall>();

        while (await reader.ReadAsync(ct))
        {
            results.Add(new RatedCall
            {
                Id = reader.GetInt64(0),
                ConnectionFee = reader.GetDecimal(1),
                MinutesCost = reader.GetDecimal(2),
                CalculatedCost = reader.GetDecimal(3),
                CallId = reader.GetInt64(4),
                TariffId = reader.GetInt32(5),
                BatchId = reader.GetGuid(6),
                Call = new Call
                {
                    Id = reader.GetInt64(4),
                    StartTime = reader.GetDateTime(7),
                    EndTime = reader.GetDateTime(8),
                    CallingParty = reader.GetString(9),
                    CalledParty = reader.GetString(10),
                    CallDirection = reader.GetString(11),
                    Disposition = reader.GetString(12),
                    Duration = reader.GetInt32(13),
                    BillableSeconds = reader.GetInt32(14),
                    PbxCharge = reader.GetDecimal(15),
                    AccountCode = reader.IsDBNull(16) ? null : reader.GetString(16),
                    CallId = reader.GetString(17),
                    TrunkName = reader.IsDBNull(18) ? null : reader.GetString(18),
                    BatchId = batchId
                },
                AppliedTariff = new Tariff
                {
                    Id = reader.GetInt32(5),
                    Prefix = reader.GetString(19),
                    Destination = reader.GetString(20),
                    RatePerMinute = reader.GetDecimal(21),
                    ConnectionFee = reader.GetDecimal(22),
                    BatchId = batchId
                }
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<(string Name, decimal Total)>> GetTotalsByBatchIdAsync(
        Guid batchId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COALESCE(s.client_name, c.calling_party) AS name,
                   SUM(rc.calculated_cost) AS total
            FROM rated_calls rc
            JOIN calls c ON c.id = rc.call_id
            LEFT JOIN subscribers s ON s.phone_number = c.calling_party AND s.batch_id = rc.batch_id
            WHERE rc.batch_id = $1
            GROUP BY COALESCE(s.client_name, c.calling_party)
            ORDER BY total DESC
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(batchId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(string, decimal)>();

        while (await reader.ReadAsync(ct))
            results.Add((reader.GetString(0), reader.GetDecimal(1)));

        return results;
    }

    public async Task<int> GetCountByBatchIdAsync(Guid batchId, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT COUNT(*)::int FROM rated_calls WHERE batch_id = $1");
        cmd.Parameters.AddWithValue(batchId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (int)(result ?? 0);
    }
}
