using CalDav;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalDav.Server.Models {
	public interface ICalendarRepository {
		IQueryable<ICalendarInfo> GetCalendars();
		ICalendarInfo GetCalendarByID(string id);
		ICalendarInfo CreateCalendar(string id);
		void Save(ICalendarInfo calendar, ICalendarObject e, IEnumerable<TimeZone> timeZones);

        ObjectData GetObjectByUID(String calendarId, string uid);
		IQueryable<ObjectData> GetObjectsByFilter(String calendarId, Filter filter);
		IQueryable<ObjectData> GetObjects(String calendarId);

		void DeleteObject(ICalendarInfo calendar, string uid);
	}

    public class ObjectData
    {
        public ICalendarObject Object { get; set; }

        public IEnumerable<TimeZone> TimeZones { get; set; }
    }
}
