using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Data;

/// <summary>
/// Guarda las cuentas en una sola tabla. Identity sigue validando contraseñas,
/// creando sus hashes, bloqueando intentos fallidos y validando las cookies.
/// </summary>
public class UsuarioStore(TallerDbContext db) :
    IQueryableUserStore<IdentityUser>, IUserPasswordStore<IdentityUser>,
    IUserEmailStore<IdentityUser>, IUserSecurityStampStore<IdentityUser>,
    IUserLockoutStore<IdentityUser>
{
    public IQueryable<IdentityUser> Users => db.Users;
    public void Dispose() { } // El contenedor de dependencias administra el contexto.

    public Task<IdentityUser?> FindByIdAsync(string id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    public Task<IdentityUser?> FindByNameAsync(string name, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == name, ct);
    public Task<IdentityUser?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == email, ct);

    public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken ct)
    {
        db.Users.Add(user);
        return Guardar(user, ct);
    }
    public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken ct)
    {
        db.Attach(user);
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        db.Update(user);
        return Guardar(user, ct);
    }
    public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken ct)
    {
        db.Users.Remove(user);
        return Guardar(user, ct);
    }
    private async Task<IdentityResult> Guardar(IdentityUser user, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return IdentityResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(user).State = EntityState.Detached;
            return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(user).State = EntityState.Detached;
            return IdentityResult.Failed(new IdentityErrorDescriber().DuplicateUserName(user.UserName ?? ""));
        }
    }

    public Task<string> GetUserIdAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.Id);
    public Task<string?> GetUserNameAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.UserName);
    public Task SetUserNameAsync(IdentityUser u, string? value, CancellationToken ct)
    { u.UserName = value; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(IdentityUser u, string? value, CancellationToken ct)
    { u.NormalizedUserName = value; return Task.CompletedTask; }

    public Task SetPasswordHashAsync(IdentityUser u, string? value, CancellationToken ct)
    { u.PasswordHash = value; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash);
    public Task<bool> HasPasswordAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash != null);

    public Task SetEmailAsync(IdentityUser u, string? value, CancellationToken ct)
    { u.Email = value; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.Email);
    public Task<bool> GetEmailConfirmedAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.EmailConfirmed);
    public Task SetEmailConfirmedAsync(IdentityUser u, bool value, CancellationToken ct)
    { u.EmailConfirmed = value; return Task.CompletedTask; }
    public Task<string?> GetNormalizedEmailAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.NormalizedEmail);
    public Task SetNormalizedEmailAsync(IdentityUser u, string? value, CancellationToken ct)
    { u.NormalizedEmail = value; return Task.CompletedTask; }

    public Task SetSecurityStampAsync(IdentityUser u, string value, CancellationToken ct)
    { u.SecurityStamp = value; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnd);
    public Task SetLockoutEndDateAsync(IdentityUser u, DateTimeOffset? value, CancellationToken ct)
    { u.LockoutEnd = value; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(++u.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(IdentityUser u, CancellationToken ct)
    { u.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(IdentityUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnabled);
    public Task SetLockoutEnabledAsync(IdentityUser u, bool value, CancellationToken ct)
    { u.LockoutEnabled = value; return Task.CompletedTask; }
}
