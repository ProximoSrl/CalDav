﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace CalDav {
	public class Event : ICalendarObject {
		private DateTime DTSTAMP = DateTime.UtcNow;

		public Event() {
			Attendees = new List<Contact>();
			Alarms = new List<Alarm>();
			Categories = new List<string>();
			Recurrences = new List<Recurrence>();
			Properties = new List<Tuple<string, string, System.Collections.Specialized.NameValueCollection>>();
			Attachments = new List<Uri>();
            ExDates = new List<DateTime>();
		}

        /// <summary>
        /// if you build event with code and not with deserialization, it makes sense to 
        /// allow manual setting of dtStamp.
        /// </summary>
        /// <param name="dtStamp"></param>
        public void SetDtStamp(DateTime dtStamp)
        {
            DTSTAMP = dtStamp;
        }

        public virtual DateTime DtStamp { get { return DTSTAMP;  } }

		public virtual Calendar Calendar { get; set; }
		public virtual ICollection<Contact> Attendees { get; set; }
		public virtual ICollection<Alarm> Alarms { get; set; }
		public virtual ICollection<string> Categories { get; set; }
		public virtual ICollection<Uri> Attachments { get; set; }
		public virtual Classes? Class { get; set; }
		public virtual DateTime? Created { get; set; }
		public virtual string Description { get; set; }

		public virtual DateTime? LastModified { get; set; }
		public virtual DateTime? Start { get; set; }

        public virtual String StartTzid { get; set; }

        public virtual DateTime? End { get; set; }
        public virtual String EndTzid { get; set; }
        public virtual string Location { get; set; }
		public virtual int? Priority { get; set; }
		public virtual Statuses? Status { get; set; }
		public virtual int? Sequence { get; set; }
		public virtual string Summary { get; set; }
		public virtual string Transparency { get; set; }
		public virtual string UID { get; set; }
		public virtual Uri Url { get; set; }
		public virtual Contact Organizer { get; set; }
		public virtual ICollection<Recurrence> Recurrences { get; set; }

        /// <summary>
        /// With recurring appointment, when you want to exclude a single appointment
        /// from the scheduling.
        /// </summary>
        public virtual ICollection<DateTime> ExDates { get; set; }

        public ICollection<Tuple<string, string, System.Collections.Specialized.NameValueCollection>> Properties { get; set; }

		public void Deserialize(System.IO.TextReader rdr, Serializer serializer) {
			string name, value, rigthPart;
			var parameters = new System.Collections.Specialized.NameValueCollection();
			while (rdr.Property(out name, out value, out rigthPart, parameters) && !string.IsNullOrEmpty(name)) {
				switch (name.ToUpper()) {
					case "BEGIN":
						switch (value) {
							case "VALARM":
								var a = serializer.GetService<Alarm>();
								a.Deserialize(rdr, serializer);
								Alarms.Add(a);
								break;
						}
						break;
					case "ATTENDEE":
						var contact = new Contact();
						contact.Deserialize(value, parameters);
						Attendees.Add(contact);
						break;
					case "CATEGORIES":
						Categories = value.SplitEscaped().ToList();
						break;
					case "CLASS": Class = value.ToEnum<Classes>(); break;
					case "CREATED": Created = value.ToDateTime(); break;
					case "DESCRIPTION": Description = value; break;
					case "DTEND":
                        End = value.ToDateTime();
                        EndTzid = parameters["TZID"];
                        break;
					case "DTSTAMP": DTSTAMP = value.ToDateTime().GetValueOrDefault(); break;
					case "DTSTART":
                        Start = value.ToDateTime();
                        StartTzid = parameters["TZID"];
                        break;
					case "LAST-MODIFIED": LastModified = value.ToDateTime(); break;
					case "LOCATION": Location = value; break;
					case "ORGANIZER":
						Organizer = serializer.GetService<Contact>();
						Organizer.Deserialize(value, parameters);
						break;
					case "PRIORITY": Priority = value.ToInt(); break;
					case "SEQUENCE": Sequence = value.ToInt(); break;
					case "STATUS": Status = value.ToEnum<Statuses>(); break;
					case "SUMMARY": Summary = value; break;
					case "TRANSP": Transparency = value; break;
					case "UID": UID = value; break;
					case "URL": Url = value.ToUri(); break;
					case "ATTACH":
						var attach = value.ToUri();
						if (attach != null)
							Attachments.Add(attach);
						break;
					case "RRULE":
						var rule = serializer.GetService<Recurrence>();
						rule.Deserialize(null, parameters);
						Recurrences.Add(rule);
						break;
                    case "EXDATE":
                        ExDates.Add(value.ToDateTime().GetValueOrDefault());
                        break;
                    case "END": return;
					default:
						Properties.Add(Tuple.Create(name, value, parameters));
						break;
				}
			}

		}

        public Boolean IsAllDayEvent
        {
            get
            {
                if (!Start.HasValue || !End.HasValue) return false;

                return Start.Value.TimeOfDay == TimeSpan.Zero &&
                       End.Value.TimeOfDay == TimeSpan.Zero;
            }
        }

		public void Serialize(System.IO.TextWriter wrtr) {
			if (End != null && Start != null && End < Start)
				End = Start;

            
			wrtr.BeginBlock("VEVENT");
			wrtr.Property("UID", UID);
			if (Attendees != null)
				foreach (var attendee in Attendees)
					wrtr.Property("ATTENDEE", attendee);
			if (Categories != null && Categories.Count > 0)
				wrtr.Property("CATEGORIES", Categories);
			wrtr.Property("CLASS", Class);
			wrtr.Property("CREATED", Created);
			wrtr.Property("DESCRIPTION", Description);
            NameValueCollection startParameter = new NameValueCollection();
            NameValueCollection endParameter = new NameValueCollection();
            if (!String.IsNullOrEmpty(StartTzid)) startParameter.Add("TZID", StartTzid);
            if (!String.IsNullOrEmpty(EndTzid)) endParameter.Add("TZID", EndTzid);

            if (!IsAllDayEvent)
            {
                wrtr.Property("DTSTART", Start, parameters : startParameter);
                wrtr.Property("DTEND", End, parameters: endParameter);
            }
            else
            {
                wrtr.Property("DTSTART", Start.Value.ToString("yyyyMMdd"), parameters: startParameter);
                wrtr.Property("DTEND", End.Value.ToString("yyyyMMdd"), parameters: endParameter);
            }

            wrtr.Property("DTSTAMP", DTSTAMP);
			wrtr.Property("LAST-MODIFIED", LastModified);
			wrtr.Property("LOCATION", Location);
			wrtr.Property("ORGANIZER", Organizer);
			wrtr.Property("PRIORITY", Priority);
			wrtr.Property("SEQUENCE", Sequence);
			wrtr.Property("STATUS", Status);
			wrtr.Property("SUMMARY", Summary);
            if (Recurrences.Any())
                foreach (var recurrence in Recurrences)
                {
                    wrtr.Property("RRULE", recurrence.ToString(), encoded : true);
                }

            if (ExDates.Any())
            {
                if (!IsAllDayEvent)
                {
                    foreach (var exdate in ExDates)
                    {
                        wrtr.Property("EXDATE", exdate);
                    }
                }
                else
                {
                    foreach (var exdate in ExDates)
                    {
                        //allday
                        NameValueCollection extDateParam = new NameValueCollection();
                        extDateParam.Add("VALUE", "DATE");
                        wrtr.Property("EXDATE", exdate.ToString("yyyyMMdd"), parameters: extDateParam);
                    }
                }
            }
               
            wrtr.Property("TRANSP", Transparency);
			wrtr.Property("URL", Url);
			if (Properties != null)
				foreach (var prop in Properties)
					wrtr.Property(prop.Item1, prop.Item2, parameters: prop.Item3);

			if (Alarms != null)
				foreach (var alarm in Alarms)
					alarm.Serialize(wrtr);
			wrtr.EndBlock("VEVENT");
		}
	}
}
