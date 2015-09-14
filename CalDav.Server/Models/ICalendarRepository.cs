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

        CalendarObjectData GetObjectByUID(String calendarId, string uid);
		IQueryable<CalendarObjectData> GetObjectsByFilter(String calendarId, Filter filter);
		IQueryable<CalendarObjectData> GetObjects(String calendarId);

		void DeleteObject(ICalendarInfo calendar, string uid);
	}

    public class CalendarObjectData
    {
        public ICalendarObject Object { get; set; }

        public IEnumerable<TimeZone> TimeZones { get; set; }
    }
}
