using System.Data;
using Dapper;

namespace Rinha2024.Dotnet.Extensions;

public static class ConnectionExtesions
{
    public static async Task<KeyValuePair<string, object>[]?> QueryUnmapped(this IDbConnection conn,string sql, DynamicParameters parameters)
    {
        return ((await conn.QueryFirstOrDefaultAsync(sql, parameters)) as IEnumerable<KeyValuePair<string, object>>)?.ToArray(); 
    }
}