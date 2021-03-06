﻿using CalDav;
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

        /// <summary>
        /// Retrieve CTAG information for a given calendar.
        /// </summary>
        /// <param name="calendarId"></param>
        /// <returns></returns>
        String GetCtag(string calendarId);
	}

    public class CalendarObjectData
    {
        public ICalendarObject Object { get; set; }

        public IEnumerable<TimeZone> TimeZones { get; set; }

        /// <summary>
        /// http://sabre.io/dav/building-a-caldav-client/
        /// We need to return a 404 for events that were deleted, this means that 
        /// the repository should return info for deleted object.
        /// </summary>
        public Boolean Deleted { get; set; }
    }
}
