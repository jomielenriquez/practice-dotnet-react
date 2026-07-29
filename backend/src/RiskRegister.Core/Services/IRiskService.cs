using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;

namespace RiskRegister.Core.Services;

/// <summary>
/// Application logic for the risk register. Sits between the controller and the repository:
/// the controller does HTTP, this does rules, the repository does storage.
/// </summary>
public interface IRiskService
{
    /// <inheritdoc cref="Repositories.IRiskRepository.GetAllAsync"/>
    Task<IReadOnlyList<Risk>> GetRegisterAsync(RiskStatus? status, CancellationToken cancellationToken);

    /// <summary>Creates a risk. New risks start <see cref="RiskStatus.Open"/>.</summary>
    Task<Risk> CreateAsync(
        string title,
        string? description,
        string owner,
        byte likelihood,
        byte impact,
        CancellationToken cancellationToken);
}
