namespace ADPortal.Models;

using System.ComponentModel.DataAnnotations;

public class AdUser
{
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string OuPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? LastLogon { get; set; }
    public DateTime? PasswordLastSet { get; set; }
    public List<string> MemberOf { get; set; } = new();
}

public class AdOU
{
    public string Name { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<AdOU> Children { get; set; } = new();
    public int UserCount { get; set; }
}

public class ResetPasswordViewModel
{
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public bool MustChangeOnNextLogon { get; set; } = true;
}

public class DashboardStats
{
    public int TotalUsers { get; set; }
    public int EnabledUsers { get; set; }
    public int DisabledUsers { get; set; }
    public int TotalOUs { get; set; }
    public List<AdUser> RecentUsers { get; set; } = new();
}

public class DirectoryObjectItem
{
    public string Name { get; set; } = string.Empty;
    public string SamAccountName { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class OrganisationalUnitsViewModel
{
    public string SearchBase { get; set; } = string.Empty;
    public List<AdOU> OUs { get; set; } = new();
    public List<DirectoryObjectItem> Groups { get; set; } = new();
}

public class OuContentsViewModel
{
    public string DistinguishedName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<DirectoryObjectItem> Users { get; set; } = new();
    public List<DirectoryObjectItem> Groups { get; set; } = new();
    public List<DirectoryObjectItem> ChildOUs { get; set; } = new();
}

public class CreateOuViewModel
{
    [Required]
    [Display(Name = "OU Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Parent OU Distinguished Name")]
    public string ParentOuDn { get; set; } = string.Empty;
}

public class CreateGroupViewModel
{
    [Required]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Parent OU Distinguished Name")]
    public string ParentOuDn { get; set; } = string.Empty;

    [Display(Name = "SAM Account Name")]
    public string SamAccountName { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Group Scope")]
    public string GroupScope { get; set; } = "Global";

    [Display(Name = "Security Group")]
    public bool SecurityEnabled { get; set; } = true;
}

public class CreateUserViewModel
{
    [Required]
    [Display(Name = "Username (SAM)")]
    public string SamAccountName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "First Name")]
    public string GivenName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Last Name")]
    public string Surname { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    [Display(Name = "Job Title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Password")]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Target OU Distinguished Name")]
    public string TargetOuDn { get; set; } = string.Empty;

    [Display(Name = "Enable Account")]
    public bool Enabled { get; set; } = true;

    [Display(Name = "User Must Change Password At Next Logon")]
    public bool MustChangePasswordAtNextLogon { get; set; } = true;
}

public class ProvisionUsersViewModel
{
    public string SourceOuDn { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Target OU Distinguished Name")]
    public string TargetOuDn { get; set; } = string.Empty;

    public string TargetOuName { get; set; } = string.Empty;

    public List<AdUser> AvailableUsers { get; set; } = new();

    public List<DirectoryObjectItem> AvailableGroups { get; set; } = new();

    public List<AdOU> AvailableOUs { get; set; } = new();

    public List<string> SelectedSamAccountNames { get; set; } = new();

    public List<string> SelectedGroupDns { get; set; } = new();
}
