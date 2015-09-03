using CalDav.Server.Models;
using System;
using System.Linq;
using System.Security.Principal;

namespace CalDav.MVC.Models {
	public class CalendarRepository : ICalendarRepository {
		private string _Directory;

		public CalendarRepository() {
			_Directory = System.IO.Path.Combine(System.Web.HttpRuntime.AppDomainAppPath, "App_Data\\Calendars");
			System.IO.Directory.CreateDirectory(_Directory);
		}

		public IQueryable<ICalendarInfo> GetCalendars() {
			var files = System.IO.Directory.GetDirectories(_Directory, "*.ical", System.IO.SearchOption.AllDirectories);
			if (files.Length == 0)
				return new[] { CreateCalendar("me") }.AsQueryable();

			var serializer = new Serializer();
			return files.Select(x => {
				using (var file = System.IO.File.OpenText(x)) {
					var cal = new CalendarInfo();
					cal.Deserialize(file, serializer);
					cal.Filename = x;
					cal.ID = System.IO.Path.GetDirectoryName(x)
						.Trim(System.IO.Path.DirectorySeparatorChar)
						.Split(System.IO.Path.DirectorySeparatorChar)
						.LastOrDefault();
					return cal;
				}
			}).Where(x => x != null).AsQueryable();
		}

		private static string MakePathSafe(string input) {
			if (input == null) return null;
			foreach (var c in System.IO.Path.GetInvalidFileNameChars())
				input = input.Replace(c, '_');
			return input.Trim('_');
		}

		public ICalendarInfo CreateCalendar(string id) {
			if (string.IsNullOrEmpty(id)) return null;
			id = MakePathSafe(id);
			var filename = System.IO.Path.Combine(_Directory, id + "\\_.ical");

            var ical = new CalendarInfo();
			var serializer = new Serializer();
			System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_Directory, id));
			using (var file = System.IO.File.OpenWrite(filename))
				serializer.Serialize(file, ical);
			ical.Filename = filename;
			return ical;
		}

		public ICalendarInfo GetCalendarByID(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "me";
            id = MakePathSafe(id);
            var filename = System.IO.Path.Combine(_Directory, id + "\\_.ical");
            if (!System.IO.File.Exists(filename))
            {
                return CreateCalendar(id);
            }

            return LoadSerializedCalendarFromFileName(id, filename);
        }

        private CalendarInfo LoadSerializedCalendarFromFileName(string id, string filename)
        {
            var calendar = new CalendarInfo();
            using (var file = System.IO.File.OpenText(filename))
            {
                calendar.Deserialize(file);
                calendar.Filename = filename;
                calendar.ID = id;
                return calendar;
            }
        }

        public ICalendarObject GetObjectByUID(String calendarId, string uid) {
			var filename = System.IO.Path.Combine(_Directory, calendarId, uid + ".ics");
			if (!System.IO.File.Exists(filename)) return null;
			var serializer = new Serializer();
			using (var file = System.IO.File.OpenText(filename)) {
				var ical = (serializer.Deserialize<CalendarCollection>(file))[0];
				return ical.Events.OfType<ICalendarObject>()
					.Union(ical.ToDos)
					.Union(ical.FreeBusy)
					.Union(ical.JournalEntries)
					.FirstOrDefault();
			}
		}

		public void Save(ICalendarInfo calendar, ICalendarObject e) {
			var filename = System.IO.Path.Combine(_Directory, calendar.ID, e.UID + ".ics");
			var ical = new CalDav.Calendar();
			ical.AddItem(e);
			var serializer = new Serializer();
			using (var file = System.IO.File.Open(filename, System.IO.FileMode.Create))
				serializer.Serialize(file, ical);

            //update accordingly the _.ical file
            var calFileName = System.IO.Path.Combine(_Directory, calendar.ID + "\\_.ical");

            var globalIcal = LoadSerializedCalendarFromFileName(calendar.ID, calFileName);
            foreach (var item in ical.Items)
            {
                globalIcal.AddItem(item);
            }
            using (var file = System.IO.File.Open(calFileName, System.IO.FileMode.Open, System.IO.FileAccess.Write))
                serializer.Serialize(file, globalIcal);
        }

		public IQueryable<ICalendarObject> GetObjectsByFilter(String calendarId, Filter filter) {
            return GetObjects(calendarId);
        }

		public IQueryable<ICalendarObject> GetObjects(String calendarId) {
			if (calendarId == null) return new ICalendarObject[0].AsQueryable();
			var directory = System.IO.Path.Combine(_Directory, calendarId);
			var files = System.IO.Directory.GetFiles(directory, "*.ics");
			var serializer = new Serializer();
			return files
				.SelectMany(x => serializer.Deserialize<CalendarCollection>(x))
				.SelectMany(x => x.Items)
				.AsQueryable();
		}

		public ICalendarObject GetObjectByPath(string path) {
			var uid = path.Split('/').Last().Split('.').FirstOrDefault();
			return GetObjectByUID(path, uid);
		}

		public void DeleteObject(ICalendarInfo calendar, string path) {
			var uid = path.Split('/').Last().Split('.').FirstOrDefault();
			var obj = GetObjectByUID(calendar.ID, uid);
			if (obj == null) return;
			var filename = System.IO.Path.Combine(_Directory, calendar.ID, obj.UID + ".ics");
			if (!System.IO.File.Exists(filename))
				return;
			System.IO.File.Delete(filename);
		}
	}
}