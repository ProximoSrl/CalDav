using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalDav.Server.Models
{
    public interface ICalendarInfo : ISerializeToICAL
    {
        string ID { get; }
        string Name { get; }
        string Description { get; }
        DateTime LastModified { get; }

        String Color { get; }

    }
}
