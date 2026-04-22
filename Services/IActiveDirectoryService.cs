using ADPortal.Models;

namespace ADPortal.Services;

public interface IActiveDirectoryService
{
    Task<List<AdUser>> GetAllUsersAsync(string? searchTerm = null);
    Task<List<AdUser>> GetUsersInOuAsync(string sourceOuDn);
    Task<List<DirectoryObjectItem>> GetGroupsAsync();
    Task<OuContentsViewModel> GetOuContentsAsync(string distinguishedName);
    Task<AdUser?> GetUserAsync(string samAccountName);
    Task<List<AdOU>> GetAllOUsAsync();
    Task<AdOU> GetOUTreeAsync();
    Task<bool> CreateOuAsync(CreateOuViewModel model);
    Task<bool> CreateGroupAsync(CreateGroupViewModel model);
    Task<bool> CreateUserAsync(CreateUserViewModel model);
    Task<int> MoveUsersToOuAsync(IEnumerable<string> samAccountNames, string targetOuDn);
    Task<int> AddUsersToGroupsAsync(IEnumerable<string> samAccountNames, IEnumerable<string> groupDns);
    Task<bool> ResetPasswordAsync(string samAccountName, string newPassword, bool mustChange);
    Task<DashboardStats> GetDashboardStatsAsync();
}
