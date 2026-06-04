using System.Collections.Generic;

namespace Ryuka.SulfurConfig
{
    internal sealed class ConfigSectionGroup
    {
        public string Name;
        public string DisplayName;

        public List<ConfigEntryModel> Entries = new List<ConfigEntryModel>();
    }
}
