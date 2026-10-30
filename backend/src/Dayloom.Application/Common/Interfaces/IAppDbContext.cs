using Dayloom.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace Dayloom.Application.Common.Interfaces;

/// <summary>
/// The database as the Application layer sees it. Application owns this interface and Infrastructure implements it,
/// so use cases never reference the concrete EF Core context, Npgsql or PostgreSQL (dependency inversion).
/// </summary>
/// <remarks>
/// Trade-off: exposing <see cref="DbSet{TEntity}"/> means Application depends on the EF Core package, which strict
/// Clean Architecture avoids with one repository per entity. We accept it to skip a repository class per entity
/// while keeping full LINQ in use cases (docs/ARCHITECTURE.md).
/// </remarks>
public interface IAppDbContext
{
    DbSet<Session> Sessions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
