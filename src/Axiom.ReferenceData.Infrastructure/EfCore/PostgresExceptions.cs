// PostgreSQL 异常识别辅助：把数据库约束冲突翻译为领域异常，避免 500 泄露内部细节。
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Axiom.ReferenceData.Infrastructure.EfCore;

internal static class PostgresExceptions
{
    /// <summary>判断 EF Core 写入异常是否由唯一键冲突（SQLSTATE 23505）引发。</summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
