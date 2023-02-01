using System;
using System.Collections.Generic;

namespace coreAPI.Models
{
    public partial class SchemaMigration
    {
        public long Version { get; set; }
        public DateTime? InsertedAt { get; set; }
    }
}
