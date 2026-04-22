using System.DirectoryServices;

namespace ADWebApp.Services
{
    public class ActiveDirectoryService
    {
        private readonly string _ldapPath = "LDAP://corp.lurking.site";

        public List<string> GetUsers()
        {
            var users = new List<string>();

            using (DirectoryEntry entry = new DirectoryEntry(_ldapPath))
            using (DirectorySearcher searcher = new DirectorySearcher(entry))
            {
                searcher.Filter = "(objectClass=user)";
                searcher.PropertiesToLoad.Add("sAMAccountName");

                foreach (SearchResult result in searcher.FindAll())
                {
                    if (result.Properties["sAMAccountName"].Count > 0)
                    {
                        users.Add(result.Properties["sAMAccountName"][0].ToString());
                    }
                }
            }

            return users;
        }

        public List<string> GetOUs()
        {
            var ous = new List<string>();

            using (DirectoryEntry entry = new DirectoryEntry(_ldapPath))
            using (DirectorySearcher searcher = new DirectorySearcher(entry))
            {
                searcher.Filter = "(objectClass=organizationalUnit)";
                searcher.PropertiesToLoad.Add("name");

                foreach (SearchResult result in searcher.FindAll())
                {
                    if (result.Properties["name"].Count > 0)
                    {
                        ous.Add(result.Properties["name"][0].ToString());
                    }
                }
            }

            return ous;
        }

        public void ResetPassword(string username, string newPassword)
        {
            using (DirectoryEntry entry = new DirectoryEntry(_ldapPath))
            using (DirectorySearcher searcher = new DirectorySearcher(entry))
            {
                searcher.Filter = $"(sAMAccountName={username})";
                var result = searcher.FindOne();

                if (result != null)
                {
                    using (DirectoryEntry user = result.GetDirectoryEntry())
                    {
                        user.Invoke("SetPassword", new object[] { newPassword });
                        user.CommitChanges();
                    }
                }
            }
        }
    }
}