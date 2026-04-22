using ADPortal.Models;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;

namespace ADPortal.Services;

public class ActiveDirectoryService : IActiveDirectoryService
{
    private readonly IConfiguration _config;
    private readonly ILogger<ActiveDirectoryService> _logger;
    private readonly string _domain;
    private readonly string _ldapPath;
    private readonly string _searchBase;

    public ActiveDirectoryService(IConfiguration config, ILogger<ActiveDirectoryService> logger)
    {
        _config = config;
        _logger = logger;
        _domain = config["ActiveDirectory:Domain"] ?? "corp.lurking.site";
        _ldapPath = config["ActiveDirectory:LdapPath"] ?? "LDAP://corp.lurking.site";
        _searchBase = config["ActiveDirectory:SearchBase"] ?? "DC=corp,DC=lurking,DC=site";
    }

    public async Task<List<AdUser>> GetAllUsersAsync(string? searchTerm = null)
    {
        return await Task.Run(() =>
        {
            var users = new List<AdUser>();
            try
            {
                using var entry = new DirectoryEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry);

                // Build filter - only get user objects, exclude system accounts
                var filter = searchTerm != null
                    ? $"(&(objectClass=user)(objectCategory=person)(|(sAMAccountName=*{searchTerm}*)(displayName=*{searchTerm}*)(mail=*{searchTerm}*)(department=*{searchTerm}*)))"
                    : "(&(objectClass=user)(objectCategory=person))";

                searcher.Filter = filter;
                searcher.PageSize = 1000;
                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "sAMAccountName", "displayName", "givenName", "sn", "mail",
                    "department", "title", "distinguishedName", "userAccountControl",
                    "lastLogon", "pwdLastSet", "memberOf"
                });

                var results = searcher.FindAll();
                foreach (SearchResult result in results)
                {
                    var user = MapToAdUser(result);
                    users.Add(user);
                }

                return users.OrderBy(u => u.DisplayName).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching AD users");
                throw;
            }
        });
    }

    public async Task<List<AdUser>> GetUsersInOuAsync(string sourceOuDn)
    {
        return await Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(sourceOuDn))
                return new List<AdUser>();

            var users = new List<AdUser>();
            try
            {
                using var ouEntry = new DirectoryEntry($"LDAP://{sourceOuDn.Trim()}");
                using var searcher = new DirectorySearcher(ouEntry)
                {
                    Filter = "(&(objectClass=user)(objectCategory=person))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1000
                };

                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "sAMAccountName", "displayName", "givenName", "sn", "mail",
                    "department", "title", "distinguishedName", "userAccountControl",
                    "lastLogon", "pwdLastSet", "memberOf"
                });

                var results = searcher.FindAll();
                foreach (SearchResult result in results)
                {
                    var user = MapToAdUser(result);
                    if (!string.IsNullOrWhiteSpace(user.SamAccountName))
                        users.Add(user);
                }

                return users
                    .OrderBy(u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.SamAccountName : u.DisplayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users from OU {OU}", sourceOuDn);
                throw;
            }
        });
    }

    public async Task<List<DirectoryObjectItem>> GetGroupsAsync()
    {
        return await Task.Run(() =>
        {
            var groups = new List<DirectoryObjectItem>();
            try
            {
                using var entry = new DirectoryEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(&(objectClass=group)(objectCategory=group))",
                    PageSize = 1000
                };

                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "name", "sAMAccountName", "distinguishedName", "description", "objectClass"
                });

                var results = searcher.FindAll();
                foreach (SearchResult result in results)
                {
                    groups.Add(new DirectoryObjectItem
                    {
                        Name = GetProperty(result, "name"),
                        SamAccountName = GetProperty(result, "sAMAccountName"),
                        DistinguishedName = GetProperty(result, "distinguishedName"),
                        Description = GetProperty(result, "description"),
                        ObjectType = "Group"
                    });
                }

                return groups
                    .OrderBy(g => string.IsNullOrWhiteSpace(g.Name) ? g.SamAccountName : g.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching groups");
                throw;
            }
        });
    }

    public async Task<OuContentsViewModel> GetOuContentsAsync(string distinguishedName)
    {
        return await Task.Run(() =>
        {
            var normalizedDn = distinguishedName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDn))
            {
                return new OuContentsViewModel();
            }

            try
            {
                using var ouEntry = new DirectoryEntry($"LDAP://{normalizedDn}");
                using var searcher = new DirectorySearcher(ouEntry)
                {
                    Filter = "(|(objectClass=user)(objectClass=group)(objectClass=organizationalUnit))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1000
                };

                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "name", "sAMAccountName", "distinguishedName", "description", "objectClass",
                    "displayName", "userAccountControl"
                });

                var users = new List<DirectoryObjectItem>();
                var groups = new List<DirectoryObjectItem>();
                var childOus = new List<DirectoryObjectItem>();

                var results = searcher.FindAll();
                foreach (SearchResult result in results)
                {
                    var dn = GetProperty(result, "distinguishedName");
                    if (string.IsNullOrWhiteSpace(dn) || dn.Equals(normalizedDn, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var type = GetObjectType(result);
                    var item = new DirectoryObjectItem
                    {
                        Name = string.IsNullOrWhiteSpace(GetProperty(result, "displayName"))
                            ? GetProperty(result, "name")
                            : GetProperty(result, "displayName"),
                        SamAccountName = GetProperty(result, "sAMAccountName"),
                        DistinguishedName = dn,
                        Description = GetProperty(result, "description"),
                        ObjectType = type
                    };

                    if (type.Equals("User", StringComparison.OrdinalIgnoreCase))
                        users.Add(item);
                    else if (type.Equals("Group", StringComparison.OrdinalIgnoreCase))
                        groups.Add(item);
                    else if (type.Equals("OU", StringComparison.OrdinalIgnoreCase))
                        childOus.Add(item);
                }

                return new OuContentsViewModel
                {
                    DistinguishedName = normalizedDn,
                    Name = GetOuNameFromDn(normalizedDn),
                    Users = users.OrderBy(x => x.Name).ToList(),
                    Groups = groups.OrderBy(x => x.Name).ToList(),
                    ChildOUs = childOus.OrderBy(x => x.Name).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching contents for OU {OU}", normalizedDn);
                throw;
            }
        });
    }

    public async Task<AdUser?> GetUserAsync(string samAccountName)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var entry = new DirectoryEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry);
                searcher.Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(samAccountName)}))";
                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "sAMAccountName", "displayName", "givenName", "sn", "mail",
                    "department", "title", "distinguishedName", "userAccountControl",
                    "lastLogon", "pwdLastSet", "memberOf"
                });

                var result = searcher.FindOne();
                return result != null ? MapToAdUser(result) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user {User}", samAccountName);
                throw;
            }
        });
    }

    public async Task<List<AdOU>> GetAllOUsAsync()
    {
        return await Task.Run(() =>
        {
            var ous = new List<AdOU>();
            try
            {
                using var entry = new DirectoryEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry);
                searcher.Filter = "(objectClass=organizationalUnit)";
                searcher.PageSize = 500;
                searcher.PropertiesToLoad.AddRange(new[] { "name", "distinguishedName" });

                var results = searcher.FindAll();
                foreach (SearchResult result in results)
                {
                    var ou = new AdOU
                    {
                        Name = GetProperty(result, "name"),
                        DistinguishedName = GetProperty(result, "distinguishedName"),
                        Path = GetProperty(result, "distinguishedName")
                    };
                    ous.Add(ou);
                }

                return ous.OrderBy(o => o.DistinguishedName).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching OUs");
                throw;
            }
        });
    }

    public async Task<AdOU> GetOUTreeAsync()
    {
        var allOUs = await GetAllOUsAsync();

        // Build root node
        var root = new AdOU
        {
            Name = _domain,
            DistinguishedName = _searchBase,
            Path = _searchBase
        };

        // Build tree from flat list by matching DN parent paths
        foreach (var ou in allOUs)
        {
            var parentDn = GetParentDn(ou.DistinguishedName);
            var parent = FindOU(root, parentDn) ?? root;
            parent.Children.Add(ou);
        }

        return root;
    }

    public async Task<bool> CreateOuAsync(CreateOuViewModel model)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var parentEntry = new DirectoryEntry($"LDAP://{model.ParentOuDn.Trim()}");
                using var ouEntry = parentEntry.Children.Add($"OU={EscapeDnComponent(model.Name.Trim())}", "organizationalUnit");
                ouEntry.CommitChanges();

                _logger.LogInformation("Created OU {OU} under {Parent}", model.Name, model.ParentOuDn);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating OU {OU} under {Parent}", model.Name, model.ParentOuDn);
                throw;
            }
        });
    }

    public async Task<bool> CreateGroupAsync(CreateGroupViewModel model)
    {
        return await Task.Run(() =>
        {
            try
            {
                var groupName = model.Name.Trim();
                var sam = string.IsNullOrWhiteSpace(model.SamAccountName)
                    ? groupName
                    : model.SamAccountName.Trim();

                using var parentEntry = new DirectoryEntry($"LDAP://{model.ParentOuDn.Trim()}");
                using var groupEntry = parentEntry.Children.Add($"CN={EscapeDnComponent(groupName)}", "group");

                groupEntry.Properties["sAMAccountName"].Value = sam;
                groupEntry.Properties["description"].Value = model.Description?.Trim() ?? string.Empty;

                const int ADS_GROUP_TYPE_GLOBAL_SECURITY = unchecked((int)0x80000002);
                const int ADS_GROUP_TYPE_GLOBAL_DISTRIBUTION = 0x00000002;

                groupEntry.Properties["groupType"].Value = model.SecurityEnabled
                    ? ADS_GROUP_TYPE_GLOBAL_SECURITY
                    : ADS_GROUP_TYPE_GLOBAL_DISTRIBUTION;

                groupEntry.CommitChanges();

                _logger.LogInformation("Created group {Group} under {Parent}", model.Name, model.ParentOuDn);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group {Group} under {Parent}", model.Name, model.ParentOuDn);
                throw;
            }
        });
    }

    public async Task<bool> ResetPasswordAsync(string samAccountName, string newPassword, bool mustChange)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var ctx = new PrincipalContext(ContextType.Domain, _domain);
                using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, samAccountName);

                if (user == null)
                {
                    _logger.LogWarning("User not found for password reset: {User}", samAccountName);
                    return false;
                }

                user.SetPassword(newPassword);

                if (mustChange)
                {
                    // Force password change at next logon
                    user.ExpirePasswordNow();
                }

                user.Save();
                _logger.LogInformation("Password reset for user: {User}", samAccountName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for {User}", samAccountName);
                throw;
            }
        });
    }

    public async Task<bool> CreateUserAsync(CreateUserViewModel model)
    {
        return await Task.Run(() =>
        {
            try
            {
                var targetOuDn = model.TargetOuDn.Trim();
                var userCn = string.IsNullOrWhiteSpace(model.DisplayName)
                    ? model.SamAccountName.Trim()
                    : model.DisplayName.Trim();

                using var ouEntry = new DirectoryEntry($"LDAP://{targetOuDn}");
                using var userEntry = ouEntry.Children.Add($"CN={EscapeDnComponent(userCn)}", "user");

                userEntry.Properties["sAMAccountName"].Value = model.SamAccountName.Trim();
                userEntry.Properties["userPrincipalName"].Value = $"{model.SamAccountName.Trim()}@{_domain}";
                userEntry.Properties["givenName"].Value = model.GivenName.Trim();
                userEntry.Properties["sn"].Value = model.Surname.Trim();
                userEntry.Properties["displayName"].Value = model.DisplayName.Trim();

                if (!string.IsNullOrWhiteSpace(model.Email))
                    userEntry.Properties["mail"].Value = model.Email.Trim();
                if (!string.IsNullOrWhiteSpace(model.Department))
                    userEntry.Properties["department"].Value = model.Department.Trim();
                if (!string.IsNullOrWhiteSpace(model.Title))
                    userEntry.Properties["title"].Value = model.Title.Trim();

                userEntry.CommitChanges();

                userEntry.Invoke("SetPassword", new object[] { model.Password });

                const int ADS_UF_NORMAL_ACCOUNT = 0x0200;
                const int ADS_UF_ACCOUNTDISABLE = 0x0002;
                var userAccountControl = ADS_UF_NORMAL_ACCOUNT;
                if (!model.Enabled)
                    userAccountControl |= ADS_UF_ACCOUNTDISABLE;

                userEntry.Properties["userAccountControl"].Value = userAccountControl;

                if (model.MustChangePasswordAtNextLogon)
                {
                    // pwdLastSet = 0 forces change password at next logon.
                    userEntry.Properties["pwdLastSet"].Value = 0;
                }

                userEntry.CommitChanges();

                _logger.LogInformation("Created AD user {User} in {OU}", model.SamAccountName, targetOuDn);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AD user {User}", model.SamAccountName);
                throw;
            }
        });
    }

    public async Task<int> MoveUsersToOuAsync(IEnumerable<string> samAccountNames, string targetOuDn)
    {
        return await Task.Run(() =>
        {
            var cleanedNames = samAccountNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!cleanedNames.Any())
                return 0;

            if (string.IsNullOrWhiteSpace(targetOuDn))
                throw new ArgumentException("Target OU cannot be empty.", nameof(targetOuDn));

            var movedCount = 0;

            try
            {
                using var rootEntry = new DirectoryEntry(_ldapPath);
                using var targetOuEntry = new DirectoryEntry($"LDAP://{targetOuDn.Trim()}");

                foreach (var sam in cleanedNames)
                {
                    try
                    {
                        using var searcher = new DirectorySearcher(rootEntry)
                        {
                            Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(sam)}))"
                        };

                        var result = searcher.FindOne();
                        if (result == null)
                        {
                            _logger.LogWarning("User {User} not found during OU move", sam);
                            continue;
                        }

                        using var userEntry = result.GetDirectoryEntry();
                        userEntry.MoveTo(targetOuEntry);
                        userEntry.CommitChanges();
                        movedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error moving user {User} to OU {OU}", sam, targetOuDn);
                    }
                }

                _logger.LogInformation("Moved {Count} user(s) to OU {OU}", movedCount, targetOuDn);
                return movedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing OU move for target OU {OU}", targetOuDn);
                throw;
            }
        });
    }

    public async Task<int> AddUsersToGroupsAsync(IEnumerable<string> samAccountNames, IEnumerable<string> groupDns)
    {
        return await Task.Run(() =>
        {
            var users = samAccountNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var groupList = (groupDns ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!users.Any() || !groupList.Any())
                return 0;

            var addedCount = 0;

            try
            {
                using var rootEntry = new DirectoryEntry(_ldapPath);

                foreach (var sam in users)
                {
                    using var userSearcher = new DirectorySearcher(rootEntry)
                    {
                        Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(sam)}))"
                    };

                    var userResult = userSearcher.FindOne();
                    if (userResult == null)
                    {
                        _logger.LogWarning("User {User} not found while adding to groups", sam);
                        continue;
                    }

                    using var userEntry = userResult.GetDirectoryEntry();
                    var userDn = userEntry.Properties["distinguishedName"].Value?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(userDn))
                        continue;

                    foreach (var groupDn in groupList)
                    {
                        try
                        {
                            using var groupEntry = new DirectoryEntry($"LDAP://{groupDn}");
                            groupEntry.Properties["member"].Add(userDn);
                            groupEntry.CommitChanges();
                            addedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error adding user {User} to group {Group}", sam, groupDn);
                        }
                    }
                }

                _logger.LogInformation("Added users to {Count} group membership link(s)", addedCount);
                return addedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding users to groups");
                throw;
            }
        });
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var entry = new DirectoryEntry(_ldapPath);
                using var searcher = new DirectorySearcher(entry);

                // Get all users for stats
                searcher.Filter = "(&(objectClass=user)(objectCategory=person))";
                searcher.PageSize = 1000;
                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "sAMAccountName", "displayName", "givenName", "sn", "mail",
                    "department", "title", "distinguishedName", "userAccountControl",
                    "lastLogon", "pwdLastSet", "memberOf"
                });

                var results = searcher.FindAll();
                var allUsers = new List<AdUser>();
                foreach (SearchResult result in results)
                    allUsers.Add(MapToAdUser(result));

                // Get OU count
                using var ouSearcher = new DirectorySearcher(entry);
                ouSearcher.Filter = "(objectClass=organizationalUnit)";
                ouSearcher.PageSize = 500;
                var ouCount = ouSearcher.FindAll().Count;

                var stats = new DashboardStats
                {
                    TotalUsers = allUsers.Count,
                    EnabledUsers = allUsers.Count(u => u.IsEnabled),
                    DisabledUsers = allUsers.Count(u => !u.IsEnabled),
                    TotalOUs = ouCount,
                    RecentUsers = allUsers
                        .OrderByDescending(u => u.LastLogon)
                        .Take(5)
                        .ToList()
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard stats");
                throw;
            }
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AdUser MapToAdUser(SearchResult result)
    {
        var uac = GetPropertyInt(result, "userAccountControl");
        // Bit 2 (0x2) = account disabled
        var isEnabled = (uac & 0x2) == 0;

        return new AdUser
        {
            SamAccountName = GetProperty(result, "sAMAccountName"),
            DisplayName = GetProperty(result, "displayName"),
            GivenName = GetProperty(result, "givenName"),
            Surname = GetProperty(result, "sn"),
            Email = GetProperty(result, "mail"),
            Department = GetProperty(result, "department"),
            Title = GetProperty(result, "title"),
            DistinguishedName = GetProperty(result, "distinguishedName"),
            OuPath = ExtractOuPath(GetProperty(result, "distinguishedName")),
            IsEnabled = isEnabled,
            LastLogon = GetPropertyDateTime(result, "lastLogon"),
            PasswordLastSet = GetPropertyDateTime(result, "pwdLastSet"),
            MemberOf = GetPropertyList(result, "memberOf")
        };
    }

    private static string GetProperty(SearchResult result, string propertyName)
    {
        if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
            return result.Properties[propertyName][0]?.ToString() ?? string.Empty;
        return string.Empty;
    }

    private static int GetPropertyInt(SearchResult result, string propertyName)
    {
        if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
        {
            var val = result.Properties[propertyName][0];
            if (val is int i) return i;
            if (int.TryParse(val?.ToString(), out var parsed)) return parsed;
        }
        return 0;
    }

    private static DateTime? GetPropertyDateTime(SearchResult result, string propertyName)
    {
        if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
        {
            var val = result.Properties[propertyName][0];
            if (val is long ticks && ticks > 0)
            {
                try { return DateTime.FromFileTime(ticks); }
                catch { return null; }
            }
        }
        return null;
    }

    private static List<string> GetPropertyList(SearchResult result, string propertyName)
    {
        var list = new List<string>();
        if (result.Properties.Contains(propertyName))
        {
            foreach (var item in result.Properties[propertyName])
            {
                // Extract just the CN from the DN for readability
                var dn = item?.ToString() ?? string.Empty;
                var cn = dn.Split(',').FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase));
                list.Add(cn != null ? cn[3..] : dn);
            }
        }
        return list;
    }

    private static string ExtractOuPath(string dn)
    {
        if (string.IsNullOrEmpty(dn)) return string.Empty;
        // Remove the CN= part and return the OU path
        var parts = dn.Split(',').Skip(1);
        return string.Join(", ", parts.Where(p =>
            p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase)));
    }

    private static string GetParentDn(string dn)
    {
        var commaIndex = dn.IndexOf(',');
        return commaIndex >= 0 ? dn[(commaIndex + 1)..] : string.Empty;
    }

    private static AdOU? FindOU(AdOU node, string dn)
    {
        if (node.DistinguishedName.Equals(dn, StringComparison.OrdinalIgnoreCase))
            return node;
        foreach (var child in node.Children)
        {
            var found = FindOU(child, dn);
            if (found != null) return found;
        }
        return null;
    }

    private static string GetObjectType(SearchResult result)
    {
        if (result.Properties.Contains("objectClass"))
        {
            var classes = result.Properties["objectClass"].Cast<object>()
                .Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .ToList();

            if (classes.Any(x => x.Equals("group", StringComparison.OrdinalIgnoreCase)))
                return "Group";
            if (classes.Any(x => x.Equals("user", StringComparison.OrdinalIgnoreCase)))
                return "User";
            if (classes.Any(x => x.Equals("organizationalUnit", StringComparison.OrdinalIgnoreCase)))
                return "OU";
        }

        return string.Empty;
    }

    private static string GetOuNameFromDn(string dn)
    {
        if (string.IsNullOrWhiteSpace(dn))
            return string.Empty;

        var firstPart = dn.Split(',').FirstOrDefault() ?? string.Empty;
        if (firstPart.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            return firstPart[3..];

        return dn;
    }

    private static string EscapeLdap(string input)
    {
        // Basic LDAP injection prevention
        return input
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }

    private static string EscapeDnComponent(string input)
    {
        // Escape special chars used in a DN component (CN=...).
        return input
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace("+", "\\+")
            .Replace("\"", "\\\"")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace(";", "\\;")
            .Replace("=", "\\=");
    }
}
