# AD Portal – Setup & Deployment Guide

**Domain:** corp.lurking.site  
**Stack:** ASP.NET Core 8 · IIS · Windows Authentication (Negotiate/NTLM/Kerberos)

---

## Prerequisites

Install these on the **Windows Server** (or your dev machine):

| Tool | Where to get it | Notes |
|------|----------------|-------|
| .NET 8 SDK | https://dotnet.microsoft.com/download/dotnet/8.0 | For building the project |
| ASP.NET Core Hosting Bundle | Same page, under "Hosting Bundle" | Required for IIS to run .NET 8 apps |
| Visual Studio Code | https://code.visualstudio.com | Recommended editor |
| C# Dev Kit extension | VS Code Extensions panel → search "C# Dev Kit" | Provides IntelliSense and run support |
| IIS | Windows Features → "Internet Information Services" | Must be enabled on the server |

> **Note:** The Hosting Bundle installs both the .NET Runtime *and* the ASP.NET Core IIS Module (ANCM). Without it, IIS cannot forward requests to your app.

---

## Part 1 – Create and Run the Project in VS Code

### Step 1 — Open the project folder

1. Copy the `ADPortal` project folder to your desired location, e.g. `C:\inetpub\wwwroot\ADPortal` or `C:\Projects\ADPortal`.
2. Open **VS Code**.
3. Click **File → Open Folder** and select the `ADPortal` folder.
4. VS Code may prompt *"This workspace has a C# project. Install recommended extensions?"* — click **Yes / Install**.

### Step 2 — Restore NuGet packages

Open the **Terminal** in VS Code (`Ctrl + `` ` ```) and run:

```bash
dotnet restore
```

This downloads:
- `Microsoft.AspNetCore.Authentication.Negotiate` — Windows Auth support
- `System.DirectoryServices` — LDAP/AD access
- `System.DirectoryServices.AccountManagement` — higher-level AD user management

### Step 3 — Run the app locally (dev mode)

```bash
dotnet run
```

You should see output like:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

Open a browser and go to `http://localhost:5000`.

**On a domain-joined machine:** Windows will automatically pass your current Windows login — no prompt appears.  
**On a non-domain machine:** The browser will show a username/password dialog. Enter your AD credentials as `CORP\username` or `username@corp.lurking.site`.

> **Tip:** If you get a 401 Unauthorized with no prompt, check that your browser allows Negotiate auth for the site. In Internet Explorer/Edge: Internet Options → Security → Local Intranet → add the site URL. Chrome on Windows uses the Windows credential store automatically for intranet sites.

---

## Part 2 – Publish and Deploy to IIS

### Step 4 — Publish the app

In the VS Code terminal:

```bash
dotnet publish -c Release -o C:\inetpub\ADPortal
```

This compiles the app and places all files into `C:\inetpub\ADPortal`. The output folder will contain:
- `ADPortal.dll` — the compiled app
- `web.config` — IIS configuration (already included in the project)
- `wwwroot\` — static files (CSS, etc.)
- All dependency DLLs

### Step 5 — Set up IIS

#### 5a. Open IIS Manager

Press `Win + R` → type `inetmgr` → Enter.

#### 5b. Create an Application Pool

1. In the left panel, right-click **Application Pools** → **Add Application Pool**.
2. Fill in:
   - **Name:** `ADPortalPool`
   - **.NET CLR Version:** `No Managed Code` ← important for .NET Core/8
   - **Managed Pipeline Mode:** `Integrated`
3. Click **OK**.
4. Right-click `ADPortalPool` → **Advanced Settings**.
5. Under **Process Model → Identity**, set to **`ApplicationPoolIdentity`** or a dedicated service account that has read access to AD (see note below).

> **Service Account Note:** The app pool identity needs to be able to query Active Directory via LDAP. `ApplicationPoolIdentity` works if the server is domain-joined. If password resets fail, create a dedicated AD service account and set it as the pool identity — grant it "Reset Password" rights on the relevant OUs via Active Directory Users and Computers (ADUC) → OU Properties → Security → Advanced.

#### 5c. Create the IIS Website (or Application)

**Option A — New website** (standalone on its own port or hostname):

1. Right-click **Sites** → **Add Website**.
2. Fill in:
   - **Site name:** `ADPortal`
   - **Application pool:** `ADPortalPool`
   - **Physical path:** `C:\inetpub\ADPortal`
   - **Binding → Type:** `http` (or `https` if you have a cert)
   - **Port:** `80` (or any unused port)
3. Click **OK**.

**Option B — Application under Default Web Site:**

1. Expand **Sites → Default Web Site**.
2. Right-click → **Add Application**.
3. **Alias:** `adportal`
4. **Application pool:** `ADPortalPool`
5. **Physical path:** `C:\inetpub\ADPortal`
6. Click **OK**. The app is now at `http://servername/adportal`.

#### 5d. Enable Windows Authentication in IIS

1. Click your new site/application in IIS Manager.
2. Double-click **Authentication** in the middle panel.
3. Set:
   - **Anonymous Authentication** → **Disabled**
   - **Windows Authentication** → **Enabled**
4. Click **Apply** in the right panel.

> This matches the `web.config` already included in the project. If both are set correctly, browsers will prompt for Windows credentials using NTLM or Kerberos.

#### 5e. Set folder permissions

The app pool identity needs to read the published folder:

```
Right-click C:\inetpub\ADPortal → Properties → Security → Edit → Add
Object name: IIS AppPool\ADPortalPool
Permissions: Read & Execute, List Folder Contents, Read
```

Or via PowerShell (run as Administrator):

```powershell
icacls "C:\inetpub\ADPortal" /grant "IIS AppPool\ADPortalPool:(OI)(CI)RX"
```

### Step 6 — Test the deployment

1. Open a browser and navigate to your site URL (e.g. `http://adportal.corp.lurking.site` or `http://localhost`).
2. You should see a Windows credentials dialog (if not already SSO'd).
3. Enter your domain credentials: `CORP\yourusername` and password.
4. The dashboard should load showing your AD stats.

---

## Part 3 – Troubleshooting

### "HTTP Error 500.30 – ASP.NET Core app failed to start"

**Cause:** Missing Hosting Bundle or publish not completed.  
**Fix:**
1. Confirm the ASP.NET Core Hosting Bundle is installed: `dotnet --info` in a command prompt should show the runtime.
2. Re-run `dotnet publish` and check for errors.
3. Check `C:\inetpub\ADPortal\logs\stdout*.log` — enable it temporarily by setting `stdoutLogEnabled="true"` in `web.config`.

### "401 Unauthorized" with no login prompt

**Cause:** Browser doesn't trust the site for Windows Auth negotiation.  
**Fix (Internet Explorer / Edge):**
- Internet Options → Security → Local Intranet → Sites → Advanced → add your URL.

**Fix (Chrome):**
- Chrome uses Windows credential store automatically for `.local` / intranet domains.
- For custom hostnames, add to the AuthServerAllowlist via Group Policy or registry:
  ```
  HKLM\SOFTWARE\Policies\Google\Chrome\AuthServerAllowlist = *.lurking.site
  ```

### "Access Denied" when querying Active Directory

**Cause:** App pool identity doesn't have AD read rights.  
**Fix:** Either use a domain account as the pool identity, or ensure the server's machine account has the necessary AD permissions.

### Password reset fails with "Access Denied"

**Cause:** The app pool identity doesn't have "Reset Password" permission on the target OU.  
**Fix:**
1. Open **Active Directory Users and Computers (ADUC)**.
2. Enable **View → Advanced Features**.
3. Right-click the OU containing the users → **Properties → Security → Advanced**.
4. Add the service account / pool identity with **Reset Password** permission.

### App loads but AD data shows error

**Cause:** LDAP path or domain name incorrect.  
**Fix:** Check `appsettings.json`:
```json
"ActiveDirectory": {
    "Domain": "corp.lurking.site",
    "LdapPath": "LDAP://corp.lurking.site",
    "SearchBase": "DC=corp,DC=lurking,DC=site"
}
```
Each part of `corp.lurking.site` becomes `DC=corp,DC=lurking,DC=site` — one `DC=` per dot segment.

---

## Part 4 – After It's Working (Next Steps)

- **HTTPS:** Get a certificate (free via Let's Encrypt / ADCS) and add an https binding in IIS.
- **Role-based access:** Add AD group checks in controllers with `[Authorize(Roles = "Domain Admins")]`.
- **Audit logging:** Add a SQL Server / SQLite table to record who reset which password and when.
- **Move to OU:** Add a `POST /Users/MoveOU` endpoint using `entry.MoveTo(newParentEntry)`.
- **Drag-and-drop provisioning:** Enhance the OU Tree view to accept dragged user cards and call a move API.

---

## File Structure Reference

```
ADPortal/
├── ADPortal.csproj              # Project + NuGet packages
├── Program.cs                   # App startup, auth config
├── appsettings.json             # Domain / LDAP settings
├── appsettings.Development.json # Dev overrides
├── web.config                   # IIS config (Windows Auth, ANCM)
├── Controllers/
│   ├── HomeController.cs        # Dashboard
│   ├── UsersController.cs       # User list, details, password reset
│   └── OUsController.cs        # OU list + tree JSON endpoint
├── Models/
│   └── AdModels.cs             # AdUser, AdOU, ResetPasswordViewModel, DashboardStats
├── Services/
│   ├── IActiveDirectoryService.cs
│   └── ActiveDirectoryService.cs  # All LDAP/AD operations
├── Views/
│   ├── Shared/_Layout.cshtml   # Nav, Bootstrap, footer
│   ├── Home/Index.cshtml       # Dashboard stats + quick actions
│   ├── Users/
│   │   ├── Index.cshtml        # Searchable user table
│   │   ├── Details.cshtml      # Full user profile + groups
│   │   └── ResetPassword.cshtml
│   └── OUs/
│       ├── Index.cshtml        # Flat OU list
│       └── Tree.cshtml         # jsTree interactive tree
├── Properties/
│   └── launchSettings.json     # Dev server config
└── wwwroot/
    └── css/site.css
```
