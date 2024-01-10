using System;
using System.Linq;

namespace GeneralImprovements.OtherMods
{
    internal static class AdvancedCompanyHelper
    {
        public static bool IsActive { get; private set; }

        // Lazy load and cache reflection info

        public static void Initialize()
        {
            // Check for conflicting mods
            IsActive = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.Contains("AdvancedCompany,"));
        }
    }
}