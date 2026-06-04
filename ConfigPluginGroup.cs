using System.Collections.Generic;

namespace Ryuka.SulfurConfig
{
    internal sealed class ConfigPluginGroup
    {
        public string Guid;
        public string Name;
        public string DisplayName;
        public string Description;
        public string Location;

        public List<ConfigSectionGroup> Sections = new List<ConfigSectionGroup>();
    }
}
