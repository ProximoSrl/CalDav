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
		void Save(ICalendarInfo calendar, ICalendarObject e);

		ICalendarObject GetObjectByUID(String calendarId, string uid);
		IQueryable<ICalendarObject> GetObjectsByFilter(String calendarId, Filter filter);
		IQueryable<ICalendarObject> GetObjects(String calendarId);

		void DeleteObject(ICalendarInfo calendar, string uid);
	}
}
