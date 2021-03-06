﻿using CalDav;
using Shouldly;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Linq;
using NUnit.Framework;

namespace Tests {
	[TestFixture]
	public class ParsingBasic {
		[Test]
		public void KeyValue() {
			var values = DeserializeProperty("TEST;VALUE1=ONE;VALUE2=TWO:tested\n\t tested");
			values.Item1.ShouldBe("TEST");
			values.Item2.ShouldBe("tested tested");
			values.Item3["VALUE1"].ShouldBe("ONE");
			values.Item3["VALUE2"].ShouldBe("TWO");
		}

		private static Tuple<string, string, NameValueCollection> DeserializeProperty(string text) {
			using (var rdr = new System.IO.StringReader(text)) {
				string name, value, rigthPart;
				var parameters = new System.Collections.Specialized.NameValueCollection();
				rdr.Property(out name, out value, out rigthPart, parameters);
				if (name == null) return null;
				return Tuple.Create(name, value, parameters);
			}
		}

		private static T Deserialize<T>(string property) where T : class, IHasParameters, new() {
			var t = new T();
			var values = DeserializeProperty(property);
			t.Deserialize(values.Item2, values.Item3);
			return t;
		}

        private static Calendar DeserializeCalendar(string serializedCalendar) 
        {
            var serializer = new Serializer();
            using (Stream str = new MemoryStream(Encoding.UTF8.GetBytes(serializedCalendar)))
            {
                var ical = serializer.Deserialize<CalDav.Calendar>(str, System.Text.Encoding.UTF8) ;
                return ical;
            }
        }


        [Test]
		public void Contact() {
			var text = "ORGANIZER;CN=JohnSmith;DIR=\"ldap" + "://host.com:6666/o=3DDC Associates,c=3DUS??(cn=3DJohn Smith)\":MAILTO" + ":jsmith@host1.com";
			var contact = Deserialize<Contact>(text);

			contact.Name.ShouldBe("JohnSmith");
			contact.Email.ShouldBe("jsmith@host1.com");
			var addr = (MailAddress)contact;
			addr.DisplayName.ShouldBe("JohnSmith");
			addr.Address.ShouldBe("jsmith@host1.com");

			contact.Directory.ShouldBe("ldap" + "://host.com:6666/o=DC Associates,c=US??(cn=John Smith)");

			var text2 = Serialize("ORGANIZER", contact);
			text2.ShouldBe(text);
		}

		private static string Serialize(string name, IHasParameters obj) {
			return name + Common.FormatParameters(obj.GetParameters()) + ":" + obj.ToString();
		}

		[Test]
		public void Trigger() {
			var text = "TRIGGER;VALUE=DATE-TIME:20120328T133700Z";
			var trigger = Deserialize<Trigger>(text);
			trigger.DateTime.ShouldBe(new DateTime(2012, 3, 28, 13, 37, 0, DateTimeKind.Utc));
			var text2 = Serialize("TRIGGER", trigger);
			text2.ShouldBe(text);

			text = "TRIGGER;RELATED=END:-P1W3DT2H3M45S";
			trigger = Deserialize<Trigger>(text);
			trigger.Related.ShouldBe(CalDav.Trigger.Relateds.End);
			trigger.Duration.ShouldBe(-(new TimeSpan(1 * 7 + 3, 2, 3, 45, 0)));
			text2 = Serialize("TRIGGER", trigger);
			text2.ShouldBe(text);
		}


        [Test]
        public void TimeZone_Deserialized_from_calendar()
        {
            var text = TestData.AndroidPut;
            var calendar = DeserializeCalendar(text);
            Assert.AreEqual(1, calendar.TimeZones.Count);
            CalDav.TimeZone timeZone = calendar.TimeZones.First();
            Assert.AreEqual("Europe/Rome", timeZone.TzId);
            Assert.AreEqual("Europe/Rome", timeZone.XLicLocation);
            Assert.AreEqual("http://tzurl.org/zoneinfo/Europe/Rome", timeZone.TzUrl);

            StringBuilder sb = new StringBuilder();
            using (var tw = new StringWriter(sb)) {
                calendar.Serialize(tw);
            }
            Console.WriteLine(sb.ToString());
        }

        [Test]
        public void TimeZone_parse_start_and_end()
        {
            var text = TestData.AndroidPut;
            var calendar = DeserializeCalendar(text);
            Assert.AreEqual(1, calendar.TimeZones.Count);
            CalDav.TimeZone timeZone = calendar.TimeZones.First();
            var evt = (Event) calendar.Items.Single();
            Assert.AreEqual(new DateTime(2015, 09, 16, 09, 30, 00), evt.Start);
            Assert.AreEqual("Europe/Rome", evt.StartTzid);
            Assert.AreEqual("Europe/Rome", evt.EndTzid);
            StringBuilder sb = new StringBuilder();
            using (var tw = new StringWriter(sb))
            {
                calendar.Serialize(tw);
            }
            Console.WriteLine(sb.ToString());

            Assert.IsTrue(sb.ToString().Contains("DTSTART;TZID=Europe/Rome:20150916T093000"));
            Assert.IsTrue(sb.ToString().Contains("DTEND;TZID=Europe/Rome:20150916T103000"));
        }

        [Test]
        public void Correctly_serialize_recurring_events()
        {
            var text = TestData.PutRecurring;
            var calendar = DeserializeCalendar(text);

            var evt = (Event)calendar.Items.Single();
            Assert.AreEqual(1, evt.Recurrences.Count);

            StringBuilder sb = new StringBuilder();
            string serialized = SerializeCalendar(calendar, sb);

            Assert.IsTrue(serialized.Contains("RRULE:FREQ=DAILY;UNTIL=20151015T110000Z"));
        }

        [Test]
        public void Event_support_exdate()
        {
            var text = TestData.PutWithEXDATE;
            var calendar = DeserializeCalendar(text);

            var evt = (Event)calendar.Items.Single();
            Assert.AreEqual(1, evt.Recurrences.Count);
            Assert.AreEqual(1, evt.ExDates.Count, "Count of Exdate wrong");
            StringBuilder sb = new StringBuilder();
            string serialized = SerializeCalendar(calendar, sb);

            Assert.IsTrue(serialized.Contains("EXDATE:20151013T140000"), "No EXDATE in serialized form");
            Assert.IsFalse(serialized.Contains("EXDATE:20151013T140000Z"), "EXDATE should not end with Z");
        }

        [Test]
        public void Event_support_exdate_for_all_day_event()
        {
            var text = TestData.PutWithEXDATE_WithoutTIme;
            var calendar = DeserializeCalendar(text);

            var evt = (Event)calendar.Items.Single();
            Assert.AreEqual(1, evt.ExDates.Count, "Count of Exdate wrong");
            StringBuilder sb = new StringBuilder();
            string serialized = SerializeCalendar(calendar, sb);
            Console.WriteLine(serialized);
            Assert.IsTrue(serialized.Contains("EXDATE;VALUE=DATE:20160713"), "EXTDATE should be in value DATE");
        }

        private static string SerializeCalendar(Calendar calendar, StringBuilder sb)
        {
            using (var tw = new StringWriter(sb))
            {
                calendar.Serialize(tw);
            }
            var serialized = sb.ToString();
            return serialized;
        }
    }
}
