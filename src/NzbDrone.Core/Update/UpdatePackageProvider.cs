using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Update
{
    public interface IUpdatePackageProvider
    {
        UpdatePackage GetLatestUpdate(string branch, Version currentVersion);
        List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null);
    }

    public class UpdatePackageProvider : IUpdatePackageProvider
    {
        public UpdatePackageProvider()
        {
        }

        public UpdatePackage GetLatestUpdate(string branch, Version currentVersion)
        {
            // No public update server for this fork; disable update checks gracefully.
            return null;
        }

        public List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion)
        {
            return new List<UpdatePackage>();
        }
    }
}
