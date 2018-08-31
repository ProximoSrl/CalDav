using CalDav.Server.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Mvc;
using System.Web.Routing;
using System.Xml.Linq;
//http://greenbytes.de/tech/webdav/draft-dusseault-caldav-05.html
//http://wwcsd.net/principals/__uids__/wiki-ilovemysmartboard/

namespace CalDav.Server.Controllers
{
    public class CalDavController : Controller
    {
        public Boolean EnableCors { get; set; }

        #region Logging

        private String _currentXmlRequest;

        private String _currentXmlResponse;

        private const string _currentUrlThreadProperty = "CurrentUrl";
        private const string _currentUserAgent = "User Agent";

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            //log4net.ThreadContext.Properties[_currentUrlThreadProperty] = Request.Url.AbsoluteUri;
            //log4net.ThreadContext.Properties[_currentUserAgent] = Request.UserAgent;
        }

        protected override void EndExecute(IAsyncResult asyncResult)
        {
            base.EndExecute(asyncResult);

            //_logger.DebugFormat("Request url {0} - Method {1}.\n\nRequest:\n{2}\n\nResponse:{3}",
            //    Request.Url.AbsoluteUri, Request.HttpMethod, _currentXmlRequest, _currentXmlResponse);

            //log4net.ThreadContext.Properties.Clear();
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            base.OnException(filterContext);
            //_logger.Error(string.Format("Error Executing request  url {0} - Method {1}.\n\nRequest:\n{2}\n\nResponse:{3}",
            //    Request.Url.AbsoluteUri, Request.HttpMethod, _currentXmlRequest, _currentXmlResponse), filterContext.Exception);
        }

        #endregion Logging

        #region Headers

        protected override IAsyncResult BeginExecute(RequestContext requestContext, AsyncCallback callback, object state)
        {
            var result = base.BeginExecute(requestContext, callback, state);
            Response.AddHeader("Access-Control-Allow-Origin", "*");
            Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS, PROPFIND, PROPPATCH, REPORT, PUT, MOVE, DELETE, LOCK, UNLOCK");
            Response.AddHeader("Access-Control-Allow-Headers", "User-Agent, Authorization, Content-type, Depth, If-match, If-None-Match, Lock-Token, Timeout, Destination, Overwrite, Prefer, X-client, X-Requested-With");
            Response.AddHeader("Access-Control-Expose-Headers", "Etag, Preference-Applied");
            return result;
        }

        #endregion

        public static void RegisterRoutes(
            RouteCollection routes, 
            string routePrefix = "caldav",
            Boolean useCalendarPrefix = true,
            bool disallowMakeCalendar = false, 
            bool requireAuthentication = false, 
            string basicAuthenticationRealm = null)
        {
            RegisterRoutes<CalDavController>(routes, routePrefix, useCalendarPrefix, disallowMakeCalendar, requireAuthentication, basicAuthenticationRealm);
        }

        public static void RegisterRoutes<T>(
            RouteCollection routes, 
            string routePrefix = "caldav", 
            Boolean useCalendarPrefix = true,
            bool disallowMakeCalendar = false, 
            bool requireAuthentication = false, 
            string basicAuthenticationRealm = null)
            where T : CalDavController
        {
            var caldavControllerType = typeof(T);

            var namespaces = new[] { caldavControllerType.Namespace };
            var controller = caldavControllerType.Name;
            if (caldavControllerType.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                controller = caldavControllerType.Name.Substring(0, caldavControllerType.Name.Length - "controller".Length);

            var defaults = new { controller, action = "index" };
            MapFirst(routes, "CalDav Root", string.Empty, new { controller, action = "PropFind" }, namespaces, new { httpMethod = new Method("PROPFIND") });
            MapFirst(routes, "CalDav", BASE = routePrefix, defaults, namespaces);
            MapFirst(
                routes,
                "CalDav User",
                USER_ROUTE = routePrefix + "/user/{id}/",
                new { controller, action = "userRoot" },
                namespaces);

            var calendarRoute = useCalendarPrefix ? "/calendar" : "";
            MapFirst(routes, "CalDav Calendar", CALENDAR_ROUTE = routePrefix + calendarRoute + "/{id}/", defaults, namespaces);
            //Added to support options called root of the caldav.

            MapFirst(
                routes,
                "CalDav Calendar options home",
                routePrefix + calendarRoute + "/",
                new { controller, action = "indexRoot" },
                namespaces
            );

            MapFirst(routes, "CalDav Object", OBJECT_ROUTE = routePrefix + "/{uid}.ics", defaults, namespaces);
            MapFirst(routes, "CalDav Calendar Object", CALENDAR_OBJECT_ROUTE = routePrefix + calendarRoute + "/{id}/{uid}.ics", defaults, namespaces);

            OBJECT_ROUTE = OBJECT_ROUTE.TrimStart('/');
            CALENDAR_OBJECT_ROUTE = CALENDAR_OBJECT_ROUTE.TrimStart('/');

            rxObjectRoute = new Regex(routePrefix + calendarRoute + "(/(?<id>[^/]+))?/(?<uid>.+?).ics", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            RequireAuthentication = requireAuthentication;
            BasicAuthenticationRealm = basicAuthenticationRealm;
            DisallowMakeCalendar = disallowMakeCalendar;
        }

        private static void MapFirst(System.Web.Routing.RouteCollection routes, string name, string path, object defaults, string[] namespaces, object constraints = null)
        {
            Route route = CreateRoute(routes, name, path, defaults, constraints);
            routes.Insert(0, route);
        }

        private static void MapLast(System.Web.Routing.RouteCollection routes, string name, string path, object defaults, string[] namespaces, object constraints = null)
        {
            Route route = CreateRoute(routes, name, path, defaults, constraints);
            routes.Add(route);
        }

        private static Route CreateRoute(RouteCollection routes, string name, string path, object defaults, object constraints)
        {
            path = path.TrimStart('/');
            var route = routes.MapRoute(name, path, defaults);
            if (constraints != null)
                route.Constraints = new System.Web.Routing.RouteValueDictionary(constraints);
            routes.Remove(route);
            return route;
        }


        public virtual ActionResult IndexRoot(string id, string uid)
        {
            if (RequireAuthentication && !User.Identity.IsAuthenticated)
            {
                return new Result
                {
                    Status = System.Net.HttpStatusCode.Unauthorized,
                    Headers = BasicAuthenticationRealm == null ? null : new Dictionary<string, string> {
                            {"WWW-Authenticate", "Basic realm=\"" + Request.Url.Host + "\"" }
                     }
                };
            }

            switch (Request.HttpMethod)
            {
                case "OPTIONS": return CalendarOptions();
                default: return NotImplemented();
            }
        }

        public virtual ActionResult UserRoot(string id, string uid)
        {
            if (RequireAuthentication && !User.Identity.IsAuthenticated)
            {
                return new Result
                {
                    Status = System.Net.HttpStatusCode.Unauthorized,
                    Headers = BasicAuthenticationRealm == null ? null : new Dictionary<string, string> {
                            {"WWW-Authenticate", "Basic realm=\"" + Request.Url.Host + "\"" }
                     }
                };
            }

            switch (Request.HttpMethod)
            {
                case "PROPFIND": return UserPropFind();
                case "OPTIONS": return UserOption();
                default: return NotImplemented();
            }
        }

        public virtual ActionResult Index(string id, string uid)
        {
            if (RequireAuthentication && !User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            try
            {
                switch (Request.HttpMethod)
                {
                    case "OPTIONS": return CalendarOptions();
                    case "PROPFIND": return PropFind(id);
                    case "REPORT": return Report(id);
                    case "DELETE": return Delete(id, uid);
                    case "PUT": return Put(id, uid);
                    case "MKCALENDAR":
                        if (DisallowMakeCalendar) return NotImplemented();
                        return MakeCalendar(id);
                    case "GET": return Get(id, uid);
                    default:
                        var xdoc = GetRequestXml();
                        return NotImplemented();                       
                }
            }
            catch (SecurityException ex)
            {
                Serilog.Log.Warning(string.Format("SecurityException Executing request url {0} - Method {1}.\n\nRequest:\n{2}\n\nResponse:{3}",
                    Request.Url.AbsoluteUri, Request.HttpMethod, _currentXmlRequest, _currentXmlResponse), ex);

                return Unauthorized();
            }

        }

        private ActionResult Unauthorized()
        {
            return new Result
            {
                Status = System.Net.HttpStatusCode.Unauthorized,
                Headers = BasicAuthenticationRealm == null ? null : new Dictionary<string, string> {
                            {"WWW-Authenticate", "Basic realm=\"" + Request.Url.Host + "\"" }
                     }
            };
        }

        private static string BASE, CALENDAR_ROUTE, OBJECT_ROUTE, CALENDAR_OBJECT_ROUTE, USER_ROUTE;
        private static Regex rxObjectRoute;
        public static bool DisallowMakeCalendar { get; set; }
        public static bool RequireAuthentication { get; set; }
        public static string BasicAuthenticationRealm { get; set; }

        /// <summary>
        /// Modified to support multiple calendars on iOS, each url is a single calendar, so each
        /// url has a principal the very same requested url.
        /// </summary>
        /// <returns></returns>
        protected virtual string GetCurrentUserUrl()
        {
            var segmentRequest = Request.Url.Segments.Where(s => s != "/" && s != "\\").Last(); 
            //var userUrl = "/" + USER_ROUTE.Replace("{id}", Thread.CurrentPrincipal.Identity.Name);

            var userUrl = "/" + USER_ROUTE.Replace("{id}", segmentRequest);
            return userUrl;
            //return Request.Path;
        }

        /// <summary>
        /// Modified to support multiple calendars on iOS, each url is a single calendar, so each
        /// url has a principal the very same requested url.
        /// </summary>
        protected virtual string GetCurrentUserCalendar()
        {
          
            var segmentRequest = Request.Url.Segments.Where(s => s != "/" && s != "\\").Last();
            //  var calendarUserUrl = "/" + CALENDAR_ROUTE.Replace("{id}", Thread.CurrentPrincipal.Identity.Name);

            var calendarUserUrl = "/" + CALENDAR_ROUTE.Replace("{id}", segmentRequest);
            return calendarUserUrl;
            //return Request.Path;
        }

        protected virtual string GetUserEmail(string id = null)
        {
            if (string.IsNullOrEmpty(id)) id = User.Identity.Name;
            return id + "@" + Request.Url.Host;
        }
        protected virtual string GetCalendarUrl(string id)
        {
            if (string.IsNullOrEmpty(id)) return "/" + BASE;
            return "/" + CALENDAR_ROUTE.Replace("{id}", Uri.EscapeDataString(id));
        }
        protected virtual string GetCalendarObjectUrl(string id, string uid)
        {
            if (string.IsNullOrEmpty(id))
                return "/" + OBJECT_ROUTE.Replace("{uid}", Uri.EscapeDataString(uid));
            var url = "/" + CALENDAR_OBJECT_ROUTE.Replace("{id}", Uri.EscapeDataString(id)).Replace("{uid}", Uri.EscapeDataString(uid));
            return url;
        }
        protected virtual string GetObjectUIDFromPath(string path)
        {
            return rxObjectRoute.Match(path).Groups["uid"].Value;
        }


        public virtual ActionResult UserPropFind()
        {
            var xdoc = GetRequestXml();
            _currentXmlRequest = xdoc.ToString();
            IEnumerable<XElement> props;
            var propNode = xdoc.Descendants(CalDav.Common.xDav.GetName("prop")).FirstOrDefault();
            if (propNode == null)
            {
                props = new XElement[0];
            }
            else
            {
                props = propNode.Elements();
            }
            var hrefName = Common.xDav.GetName("href");

            var calendarHomeSetName = Common.xCalDav.GetName("calendar-home-set");
            var calendarHomeSet = !props.Any(x => x.Name == calendarHomeSetName) ? null :
                calendarHomeSetName.Element(hrefName.Element(GetCurrentUserCalendar()));

            var calendarUserAddressSetName = Common.xCalDav.GetName("calendar-user-address-set");
            var calendarUserAddressSet = !props.Any(x => x.Name == calendarUserAddressSetName) ? null :
                calendarUserAddressSetName.Element(hrefName.Element(GetCurrentUserCalendar()));

            var currentPrincipalUrlName = Common.xDav.GetName("principal-URL");
            var currentPrincipalUrl = !props.Any(x => x.Name == currentPrincipalUrlName) ? null :
                currentPrincipalUrlName.Element(hrefName.Element(GetCurrentUserUrl()));

            var currentPrincipalCollectionSetName = Common.xDav.GetName("principal-collection-set");
            var currentPrincipalCollectionSet = !props.Any(x => x.Name == currentPrincipalCollectionSetName) ? null :
                currentPrincipalCollectionSetName.Element(hrefName.Element(GetCurrentUserUrl()));

            var displayNameName = Common.xDav.GetName("displayname");
            var displayName = !props.Any(x => x.Name == displayNameName) ? null :
                displayNameName.Element(Thread.CurrentPrincipal.Identity.Name);

            var supportedReportSetName = Common.xDav.GetName("supported-report-set");
            var supportedReportSet = !props.Any(x => x.Name == supportedReportSetName) ? null :
                supportedReportSetName.Element(
                    //Common.xDav.Element("supported-report", Common.xDav.Element("report", "calendar-multiget"))
                    new[] {
                        Common.xDav.Element("supported-report", Common.xDav.Element("report", "principal-property-search")),
                        Common.xDav.Element("supported-report", Common.xDav.Element("report", "sync-collection")),
                        Common.xDav.Element("supported-report", Common.xDav.Element("report", "expand-property")),
                        Common.xDav.Element("supported-report", Common.xDav.Element("report", "principal-search-property-set")),
                    });

            var supportedProperties = new HashSet<XName> {
                calendarHomeSetName
                , displayNameName
                , calendarUserAddressSetName
                , currentPrincipalUrlName
                , currentPrincipalCollectionSetName
                , supportedReportSetName
            };

            var prop404 = Common.xDav.Element("prop", props
                      .Where(p => !supportedProperties.Contains(p.Name))
                      .Select(p => new XElement(p.Name))
              );

            var propStat404 = Common.xDav.Element("propstat",
             Common.xDav.Element("status", "HTTP/1.1 404 Not Found"), prop404);

            var result = new Result
            {
                Status = (System.Net.HttpStatusCode)207,
                Content = new XElement(Common.xDav + "multistatus",
                       new XAttribute(XNamespace.Xmlns + "d", Common.xDav),
                       new XAttribute(XNamespace.Xmlns + "c", Common.xCalDav),
                       new XAttribute(XNamespace.Xmlns + "cs", Common.xCalCs),
                   //new XAttribute(XNamespace.Xmlns + "ICAL", Common.xApple),
                   Common.xDav.Element("response",
                   Common.xDav.Element("href", Request.RawUrl),
                   Common.xDav.Element("propstat",
                               Common.xDav.Element("status", "HTTP/1.1 200 OK"),
                               Common.xDav.Element("prop"
                                   , calendarHomeSet
                                   , displayName
                                   , calendarUserAddressSet
                                   , currentPrincipalUrl
                                   , currentPrincipalCollectionSet
                                   , supportedReportSet
                               )
                           ),
                           (prop404.Elements().Any() ? propStat404 : null)
                    )
                )
            };

            _currentXmlResponse = result.Content.ToString();
            return result;

            //var request = xdoc.Root.Elements().FirstOrDefault();
            //switch (request.Name.LocalName.ToLower())
            //{
            //    case "calendar-collection-set":
            //        var repo = GetService<ICalendarRepository>();
            //        var calendars = repo.GetCalendars().ToArray();

            //        return new Result
            //        {
            //            Content = CalDav.Common.xDav.Element("options-response",
            //             CalDav.Common.xCalDav.Element("calendar-collection-set",
            //                 calendars.Select(calendar =>
            //                     CalDav.Common.xDav.Element("href",
            //                         new Uri(Request.Url, GetCalendarUrl(calendar.Name))
            //                         ))
            //             )
            //         )
            //        };
            //}
            //return null;
        }

        public virtual ActionResult UserOption()
        {
            return new Result
            {
                Headers = new Dictionary<string, string> {
                   {"Allow", "DELETE, HEAD, GET, MKCALENDAR, MKCOL, MOVE, OPTIONS, PROPFIND, PROPPATCH, PUT, REPORT" },
                   { "DAV", " 1, 2, 3, calendar-access, addressbook, extended-mkcol" }
               }
            };
        }

        public virtual ActionResult CalendarOptions()
        {
            return new Result
            {
                Headers = new Dictionary<string, string> {
                    {"Allow", "DELETE, HEAD, GET, MKCALENDAR, MKCOL, MOVE, OPTIONS, PROPFIND, PROPPATCH, PUT, REPORT" },
                    { "DAV", " 1, 2, 3, calendar-access, addressbook, extended-mkcol" }
                }
            };
        }

        public class Method : ActionMethodSelectorAttribute, IRouteConstraint
        {
            private string _Method;
            public Method(string method)
            {
                _Method = method.ToUpper();
            }

            public override bool IsValidForRequest(ControllerContext controllerContext, System.Reflection.MethodInfo methodInfo)
            {
                return _Method.Equals(controllerContext.HttpContext.Request.HttpMethod, StringComparison.OrdinalIgnoreCase);
            }

            public bool Match(System.Web.HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
            {
                return _Method.Equals(httpContext.Request.HttpMethod, StringComparison.OrdinalIgnoreCase);
            }
        }

        public virtual ActionResult PropFind(string id)
        {
            var xdoc = GetRequestXml();
            _currentXmlRequest = xdoc.ToString();

            var depth = Request.Headers["Depth"].ToInt() ?? 0;
            var repo = GetService<ICalendarRepository>();
            var calendar = repo.GetCalendarByID(id);

            IEnumerable<XElement> props;
            var propNode = xdoc.Descendants(CalDav.Common.xDav.GetName("prop")).FirstOrDefault();
            if (propNode == null)
            {
                props = new XElement[0];
            }
            else
            {
                props = propNode.Elements();
            }

            var allprop = xdoc.Descendants(Common.xDav.GetName("allprop")).Any();
            var hrefName = Common.xDav.GetName("href");
            //var scheduleInboxURLName = Common.xCalDav.GetName("schedule-inbox-URL");
            //var scheduleOutoxURLName = Common.xCalDav.GetName("schedule-outbox-URL");
            //var addressbookHomeSetName = Common.xCalDav.GetName("addressbook-home-set");

            var calendarUserAddressSetName = Common.xCalDav.GetName("calendar-user-address-set");
            var calendarUserAddress = !allprop && !props.Any(x => x.Name == calendarUserAddressSetName) ? null :
                calendarUserAddressSetName.Element(
                    hrefName.Element(GetCurrentUserUrl()),
                    hrefName.Element("mailto:" + GetUserEmail())
                );


            var supportedReportSetName = Common.xDav.GetName("supported-report-set");
            var supportedReportSet = !allprop && !props.Any(x => x.Name == supportedReportSetName) ? null :
                supportedReportSetName.Element(
                    //Common.xDav.Element("supported-report", Common.xDav.Element("report", "calendar-multiget"))
                    new[] {
                        Common.xDav.Element("supported-report", Common.xDav.Element("report", "principal-property-search")),
                        Common.xDav.Element("supported-report", Common.xDav.Element("report", "sync-collection")),
                        Common.xDav.Element("supported-report", Common.xDav.Element("report", "expand-property")),
                        Common.xDav.Element("supported-report", Common.xDav.Element("report", "principal-search-property-set")),
                    });

            var calendarHomeSetName = Common.xCalDav.GetName("calendar-home-set");
            var calendarHomeSet = !allprop && !props.Any(x => x.Name == calendarHomeSetName) ? null :
                calendarHomeSetName.Element(hrefName.Element(GetCurrentUserUrl()));

            String ctag;
            if (calendar == null)
                ctag = DateTime.Now.Ticks.ToString();
            else
                ctag = repo.GetCtag(calendar.ID);
            var getetagName = Common.xDav.GetName("getetag");
            var getetag = !allprop && !props.Any(x => x.Name == getetagName) ? null :
                getetagName.Element(ctag);

            var getctagName = Common.xCalCs.GetName("getctag");
            var getctag = !allprop && !props.Any(x => x.Name == getctagName) ? null :
                getctagName.Element("\"" + ctag + "\"");

            var syncTokenName = Common.xDav.GetName("sync-token");
            var syncToken = !allprop && !props.Any(x => x.Name == syncTokenName) ? null :
                syncTokenName.Element("http://csharpdav.org/ns/sync-token/" + ctag);

            var currentUserPrincipalName = Common.xDav.GetName("current-user-principal");
            var currentUserPrincipal = !props.Any(x => x.Name == currentUserPrincipalName) ? null :
                currentUserPrincipalName.Element(hrefName.Element(GetCurrentUserUrl()));

            var currentPrincipalUrlName = Common.xDav.GetName("principal-URL");
            var currentPrincipalUrl = !props.Any(x => x.Name == currentPrincipalUrlName) ? null :
                currentPrincipalUrlName.Element(hrefName.Element(GetCurrentUserUrl()));

            var currentUserPrivilegeSetName = Common.xDav.GetName("current-user-privilege-set");
            var currentUserPrivilegeSet = !props.Any(x => x.Name == currentUserPrivilegeSetName) ? null :
                currentUserPrivilegeSetName.Element(new[] {
                        Common.xDav.Element("privilege", 
                            Common.xDav.Element("all"),
                            Common.xDav.Element("read"),
                            Common.xDav.Element("write"),
                            Common.xDav.Element("write-properties"),
                            Common.xDav.Element("write-content")),
                       });

            var resourceTypeName = Common.xDav.GetName("resourcetype");
            XElement resourceType;
            if (Request.RawUrl == "/")
            {
                resourceType = !allprop && !props.Any(x => x.Name == resourceTypeName) ? null : (
                       resourceTypeName.Element(Common.xDav.Element("collection"))
                   );
            }
            else
            {
                resourceType = !allprop && !props.Any(x => x.Name == resourceTypeName) ? null : (
                    resourceTypeName.Element(Common.xDav.Element("collection"), Common.xCalDav.Element("calendar"), Common.xDav.Element("principal"))
                );
            }


            var ownerName = Common.xDav.GetName("owner");
            var owner = !allprop && !props.Any(x => x.Name == ownerName) ? null :
                ownerName.Element(hrefName.Element(GetCurrentUserUrl()));

            var displayNameName = Common.xDav.GetName("displayname");
            var displayName = calendar == null || (!allprop && !props.Any(x => x.Name == displayNameName)) ? null :
                displayNameName.Element(calendar.Name ?? calendar.ID);

            var color = calendar != null ? calendar.Color : "";
            if (String.IsNullOrEmpty(color)) color = "#888888FF";

            color = "FF5800";
            var calendarColorName = Common.xApple.GetName("calendar-color");
            var calendarColor = !allprop && !props.Any(x => x.Name == calendarColorName) ? null :
                calendarColorName.Element(color);

            var calendarOrderName = Common.xApple.GetName("calendar-order");
            var calendarOrder = !allprop && !props.Any(x => x.Name == calendarOrderName) ? null :
                calendarOrderName.Element("0");

            var calendarDescriptionName = Common.xCalDav.GetName("calendar-description");
            var calendarDescription = calendar == null || (!allprop && !props.Any(x => x.Name == calendarDescriptionName)) ? null :
                calendarDescriptionName.Element(calendar.Name);

            var supportedComponentsName = Common.xCalDav.GetName("supported-calendar-component-set");
            var supportedComponents = !allprop && !props.Any(x => x.Name == supportedComponentsName) ? null :
                supportedComponentsName.Element(new[]{
                    Common.xCalDav.Element("comp", new XAttribute("name", "VEVENT")),
                    Common.xCalDav.Element("comp", new XAttribute("name", "VTODO")),
                    Common.xCalDav.Element("comp", new XAttribute("name", "VJOURNAL"))
                });

            var getContentTypeName = Common.xDav.GetName("getcontenttype");
            var getContentType = !allprop && !props.Any(x => x.Name == getContentTypeName) ? null :
                getContentTypeName.Element("text/calendar; component=vevent");

            var supportedProperties = new HashSet<XName> {
                resourceTypeName
                , ownerName
                , supportedComponentsName
                , getContentTypeName
                , displayNameName
                , calendarDescriptionName
                , calendarColorName
                , currentUserPrincipalName
                , calendarHomeSetName
                , calendarUserAddressSetName
                , supportedComponentsName
                , supportedReportSetName
                , getctagName
                , getetagName
                , calendarOrderName
                , currentUserPrivilegeSetName
                , currentPrincipalUrlName
                , syncTokenName
            };

            var childSupportedProperties = new HashSet<XName> {
                resourceTypeName
                , getetagName
                , currentUserPrivilegeSetName
                //, ownerName
                , supportedComponentsName
                , getContentTypeName
                //, displayNameName
                //, calendarDescriptionName
                //, calendarColorName
                //, currentUserPrincipalName
                //, calendarHomeSetName
                //, calendarUserAddressSetName
                //, supportedComponentsName
                , supportedReportSetName
                //, getctagName
            };

            var prop404 = Common.xDav.Element("prop", props
                        .Where(p => !supportedProperties.Contains(p.Name))
                        .Select(p => new XElement(p.Name))
                );

            var prop404ForChilds = Common.xDav.Element("prop", props
                        .Where(p => !childSupportedProperties.Contains(p.Name))
                        .Select(p => new XElement(p.Name))
                );

            var propStat404 = Common.xDav.Element("propstat",
                Common.xDav.Element("status", "HTTP/1.1 404 Not Found"), prop404);

            var propStat404ForChilds = Common.xDav.Element("propstat",
                Common.xDav.Element("status", "HTTP/1.1 404 Not Found"), prop404ForChilds);

            var result = new Result
            {
                Status = (System.Net.HttpStatusCode)207,
                Content = new XElement(Common.xDav + "multistatus",
                        new XAttribute(XNamespace.Xmlns + "d", Common.xDav),
                        new XAttribute(XNamespace.Xmlns + "c", Common.xCalDav),
                        new XAttribute(XNamespace.Xmlns + "cs", Common.xCalCs),
                        new XAttribute(XNamespace.Xmlns + "ICAL", Common.xApple),
                    Common.xDav.Element("response",
                    Common.xDav.Element("href", Request.RawUrl),
                    Common.xDav.Element("propstat",
                                Common.xDav.Element("status", "HTTP/1.1 200 OK"),
                                Common.xDav.Element("prop"
                                    , resourceType
                                    , supportedReportSet
                                    , supportedComponents
                                    , getctag
                                    , displayName
                                    , getetag
                                    , syncToken
                                    , getContentType
                                    , calendarDescription
                                    , calendarHomeSet
                                    , currentUserPrincipal
                                    , calendarColor
                                    , calendarUserAddress
                                    , owner
                                    , calendarOrder
                                    , currentUserPrivilegeSet
                                    , currentPrincipalUrl
                                )
                            ),

                            (prop404.Elements().Any() ? propStat404 : null)
                     ),

                     (depth == 0 ? null :
                         (repo.GetObjects(calendar.ID)
                         .Where(x => x != null)
                         .ToArray()
                            .Select(item => Common.xDav.Element("response",
                                hrefName.Element(GetCalendarObjectUrl(calendar.ID, item.Object.UID)),
                                    item.Deleted 
                                    ?
                                    Common.xDav.Element("status", "HTTP/1.1 404 Not Found")
                                    :
                                    //item is not deleted
                                    Common.xDav.Element("propstat",
                                        Common.xDav.Element("status", "HTTP/1.1 200 OK"),
                                         Common.xDav.Element("prop"
                                            , currentUserPrivilegeSet
                                            , resourceTypeName.Element()
                                            , supportedComponents
                                            , supportedReportSet
                                            , (getContentType == null ? null : getContentTypeName.Element("text/calendar; component=v" + item.GetType().Name.ToLower()))
                                            , getetag == null ? null : getetagName.Element(Common.EtagFromDate(item.Object.LastModified))
                                            //, getctag == null ? null : getctagName.Element(Common.EtagFromDate(item.Object.LastModified))
                                        )
                                    )
                                    , (prop404ForChilds.Elements().Any() ? propStat404ForChilds : null)
                                ))
                            .ToArray()))
                 )
            };

            _currentXmlResponse = result.Content.ToString();
            return result;
        }

        public virtual ActionResult MakeCalendar(string id)
        {
            var repo = GetService<ICalendarRepository>();
            var calendar = repo.CreateCalendar(id);

            return new Result
            {
                Headers = new Dictionary<string, string> {
                    {"Location", GetCalendarUrl(calendar.ID) },
                },
                Status = System.Net.HttpStatusCode.Created
            };
        }

        public virtual ActionResult Delete(string id, string uid)
        {
            var repo = GetService<ICalendarRepository>();
            var calendar = repo.GetCalendarByID(id);
            repo.DeleteObject(calendar, uid);
            return new Result();
        }

        public virtual ActionResult Put(string id, string uid)
        {
            var repo = GetService<ICalendarRepository>();
            var calendar = repo.GetCalendarByID(id);
            var input = GetRequestCalendar();
            var e = input.Items.FirstOrDefault();
            e.LastModified = DateTime.UtcNow;
            repo.Save(calendar, e, input.TimeZones);

            return new Result
            {
                Headers = new Dictionary<string, string> {
                    {"Location", GetCalendarObjectUrl(calendar.ID, e.UID) },
                    {"ETag", Common.FormatDate(e.LastModified) }
                },
                Status = System.Net.HttpStatusCode.Created
            };
        }

        public virtual ActionResult Get(string id, string uid)
        {
            var repo = GetService<ICalendarRepository>();
            Response.ContentType = "text/calendar";

            if (uid != null)
            {
                IEnumerable<TimeZone> timeZones;
                var obj = repo.GetObjectByUID(id, uid);
                Response.Write(ToString(obj));
            }
            else
            {
                //TODO: refactor to avoid this strange usage of stream.
                var calendar = repo.GetCalendarByID(id);
                StringBuilder sb = new StringBuilder();
                using (TextWriter w = new StringWriter(sb))
                    calendar.Serialize(w);

                Response.Write(sb.ToString());
            }


            return null;
        }



        public virtual ActionResult Report(string id)
        {
            var xdoc = GetRequestXml();
            _currentXmlRequest = xdoc.ToString();
            if (xdoc == null) return new Result();

            var repo = GetService<ICalendarRepository>();

            var request = xdoc.Root;
            var filterElm = request.Element(CalDav.Common.xCalDav.GetName("filter"));
            var filter = filterElm == null ? null : new Filter(filterElm);
            var hrefName = CalDav.Common.xDav.GetName("href");
            var hrefs = xdoc.Descendants(hrefName).Select(x => x.Value).ToArray();
            var getetagName = CalDav.Common.xDav.GetName("getetag");
            var getetag = xdoc.Descendants(getetagName).FirstOrDefault();

            var calendarDataName = CalDav.Common.xCalDav.GetName("calendar-data");
            var calendarData = xdoc.Descendants(calendarDataName).FirstOrDefault();

            var syncTokenName = Common.xDav.GetName("sync-token");
            var syncToken = xdoc.Descendants(syncTokenName).FirstOrDefault();

            var ownerName = Common.xDav.GetName("owner");
            var displaynameName = Common.xDav.GetName("displayname");

            IQueryable<CalendarObjectData> result = null;
            if (filter != null)
            {
                //Get object by filter, this still need to be correctly implemented
                //by repo
                result = repo.GetObjectsByFilter(id, filter);
            }
            else if (hrefs.Any())
            {
                //we have a list of hrefs of element to retrieve.
                result = hrefs
                  .SelectMany<String, CalendarObjectData>(x =>
                  {
                      var objectUid = GetObjectUIDFromPath(x);

                      if (!String.IsNullOrEmpty(objectUid))
                      {
                          return new[] { repo.GetObjectByUID(id, GetObjectUIDFromPath(x)) };
                      }

                      return repo.GetObjects(id);
                  })
                  .Where(x => x != null)
                  .AsQueryable();
            }
            else if (syncToken != null)
            {
                //synctoken request
                var syncTokenUrl = new Uri(syncToken.Value);
                var ticks = Int64.Parse( syncTokenUrl.Segments.Last());
                DateTime dateFrom = new DateTime(ticks);
                result = repo.GetObjectsByFilter(id, null)
                    .Where(obj => obj.Object.LastModified >= dateFrom);
            }
            Result returnValue;
            if (result != null)
            {
                returnValue = new Result
                {
                    Status = (System.Net.HttpStatusCode)207,
                    Content = CalDav.Common.xDav.Element("multistatus",
                        new XAttribute(XNamespace.Xmlns + "d", Common.xDav),
                        new XAttribute(XNamespace.Xmlns + "c", Common.xCalDav),
                        new XAttribute(XNamespace.Xmlns + "cs", Common.xCalCs),
                    result.Select(r =>
                     CalDav.Common.xDav.Element("response",
                         CalDav.Common.xDav.Element("href", Request.RawUrl.TrimEnd('/') + "/" + r.Object.UID + ".ics"),
                         r.Deleted
                         ?
                         Common.xDav.Element("status", "HTTP/1.1 404 Not Found")
                         :
                         CalDav.Common.xDav.Element("propstat",
                             CalDav.Common.xDav.Element("status", "HTTP/1.1 200 OK"),
                             CalDav.Common.xDav.Element("prop",
                                (getetag == null ? null : CalDav.Common.xDav.Element("getetag", Common.EtagFromDate(r.Object.LastModified))),
                                (calendarData == null ? null : CalDav.Common.xCalDav.Element("calendar-data",
                                    ToString(r)
                                ))
                             )
                         )
                     )
                    ))
                };
            }
            else
            {
                var calendar = repo.GetCalendarByID(id);
                returnValue = new Result
                    {
                        Headers = new Dictionary<string, string> {
                        {"ETag" , id == null ? null : Common.FormatDate( calendar.LastModified ) }
                    }
                };
            }

            if (returnValue.Content != null)
            {
                _currentXmlResponse = returnValue.Content.ToString();
            }
            return returnValue;
        }

        public ActionResult NotImplemented()
        {
            return new Result { Status = System.Net.HttpStatusCode.NotImplemented };
        }

        private List<IDisposable> _Disposables = new List<IDisposable>();
        private T GetService<T>()
        {
            var obj = System.Web.Mvc.DependencyResolver.Current.GetService<T>();
            if (obj != null && obj is IDisposable)
                _Disposables.Add(obj as IDisposable);
            return obj;
        }

        protected override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            base.OnResultExecuted(filterContext);
            foreach (var disposable in _Disposables)
                if (disposable != null)
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception) { }
        }

        internal System.IO.Stream Stream { get; set; }

        private XDocument GetRequestXml()
        {
            if (!(Request.ContentType ?? string.Empty).ToLower().Contains("xml") || Request.ContentLength == 0)
                return null;
            using (var str = (Stream ?? Request.InputStream))
                return XDocument.Load(str);
        }

        private Calendar GetRequestCalendar()
        {
            if (!(Request.ContentType ?? string.Empty).ToLower().Contains("calendar") || Request.ContentLength == 0)
                return null;
            var serializer = new Serializer();
            using (var str = (Stream ?? Request.InputStream))
            {
                var ical = serializer.Deserialize<CalDav.CalendarCollection>(str, Request.ContentEncoding ?? System.Text.Encoding.Default);
                return ical.FirstOrDefault();
            }
        }

        private static string ToString(CalendarObjectData obj)
        {
            var calendar = new CalDav.Calendar();
            calendar.AddItem(obj.Object);
            if (obj.TimeZones != null)
            {
                foreach (var tz in obj.TimeZones)
                {
                    calendar.TimeZones.Add(tz);
                }
            }
            var serializer = new Serializer();
            using (var str = new System.IO.StringWriter())
            {
                serializer.Serialize(str, calendar);
                return str.ToString();
            }
        }

        private static string ToString(ICalendarInfo obj)
        {
            var serializer = new Serializer();
            using (var str = new System.IO.StringWriter())
            {
                serializer.Serialize(str, obj);
                return str.ToString();
            }
        }
    }
}
