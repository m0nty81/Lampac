﻿using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class RequestInfo
    {
        #region RequestInfo
        static List<(IPAddress prefix, int prefixLength)> cloudflare_ips = null;

        private readonly RequestDelegate _next;
        IMemoryCache memoryCache;

        public RequestInfo(RequestDelegate next, IMemoryCache mem)
        {
            _next = next;
            memoryCache = mem;
        }
        #endregion

        public Task Invoke(HttpContext httpContext)
        {
            #region stats
            {
                string skey = $"stats:request:{DateTime.Now.Minute}";
                if (!memoryCache.TryGetValue(skey, out long _req))
                    _req = 0;

                _req++;
                memoryCache.Set(skey, _req, DateTime.Now.AddMinutes(58));
            }
            #endregion

            bool IsLocalRequest = false;
            string clientIp = httpContext.Connection.RemoteIpAddress.ToString();

            if (httpContext.Request.Headers.TryGetValue("localrequest", out var _localpasswd))
            {
                if (_localpasswd.ToString() != FileCache.ReadAllText("passwd"))
                    return httpContext.Response.WriteAsync("error passwd", httpContext.RequestAborted);

                IsLocalRequest = true;

                if (httpContext.Request.Headers.TryGetValue("x-client-ip", out var xip) && !string.IsNullOrEmpty(xip))
                    clientIp = xip;
            }
            else if (AppInit.conf.real_ip_cf || AppInit.conf.frontend == "cloudflare")
            {
                #region cloudflare
                if (cloudflare_ips == null)
                {
                    string ips = HttpClient.Get("https://www.cloudflare.com/ips-v4").Result;
                    if (ips != null)
                    {
                        string ips_v6 = HttpClient.Get("https://www.cloudflare.com/ips-v6").Result;
                        if (ips_v6 != null)
                        {
                            foreach (string ip in (ips + "\n" + ips_v6).Split('\n'))
                            {
                                if (string.IsNullOrEmpty(ip) || !ip.Contains("/"))
                                    continue;

                                if (cloudflare_ips == null)
                                    cloudflare_ips = new List<(IPAddress prefix, int prefixLength)>();

                                string[] ln = ip.Split('/');
                                cloudflare_ips.Add((IPAddress.Parse(ln[0].Trim()), int.Parse(ln[1].Trim())));
                            }
                        }
                    }
                }

                if (cloudflare_ips != null)
                {
                    var clientIPAddress = IPAddress.Parse(clientIp);
                    foreach (var cf in cloudflare_ips)
                    {
                        if (new IPNetwork(cf.prefix, cf.prefixLength).Contains(clientIPAddress))
                        {
                            if (httpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var xip) && !string.IsNullOrEmpty(xip))
                                clientIp = xip;

                            if (httpContext.Request.Headers.TryGetValue("CF-Visitor", out var cfVisitor))
                            {
                                var visitorInfo = JsonNode.Parse(cfVisitor);
                                if (visitorInfo != null && visitorInfo["scheme"] != null)
                                    httpContext.Request.Scheme = visitorInfo["scheme"].ToString();
                            }

                            break;
                        }
                    }
                }
                #endregion
            }

            var req = new RequestModel()
            {
                IsLocalRequest = IsLocalRequest,
                IP = clientIp,
                Path = httpContext.Request.Path.Value,
                Query = httpContext.Request.QueryString.Value,
                UserAgent = httpContext.Request.Headers.UserAgent
            };

            if (!Regex.IsMatch(httpContext.Request.Path.Value, "^/(proxy-dash/|proxy/|proxyimg|lifeevents|externalids|ts|ws|weblog|rch/result|merchant/payconfirm|tmdb/)"))
                req.Country = GeoIP2.Country(req.IP);

            if (string.IsNullOrEmpty(AppInit.conf.accsdb.domainId_pattern))
            {
                #region getuid
                string getuid()
                {
                    if (!string.IsNullOrEmpty(httpContext.Request.Query["token"].ToString()))
                        return httpContext.Request.Query["token"].ToString();

                    if (!string.IsNullOrEmpty(httpContext.Request.Query["account_email"].ToString()))
                        return httpContext.Request.Query["account_email"].ToString();

                    if (!string.IsNullOrEmpty(httpContext.Request.Query["uid"].ToString()))
                        return httpContext.Request.Query["uid"].ToString();

                    if (!string.IsNullOrEmpty(httpContext.Request.Query["box_mac"].ToString()))
                        return httpContext.Request.Query["box_mac"].ToString();

                    return null;
                }
                #endregion

                req.user = AppInit.conf.accsdb.findUser(httpContext, out string uid);
                req.user_uid = uid;

                if (string.IsNullOrEmpty(req.user_uid))
                    req.user_uid = getuid();

                if (req.user != null)
                    req.@params = AppInit.conf.accsdb.@params;

                httpContext.Features.Set(req);
                return _next(httpContext);
            }
            else
            {
                string uid = Regex.Match(httpContext.Request.Host.Host, AppInit.conf.accsdb.domainId_pattern).Groups[1].Value;
                req.user = AppInit.conf.accsdb.findUser(uid);
                req.user_uid = uid;

                if (req.user == null)
                    return httpContext.Response.WriteAsync("user not found", httpContext.RequestAborted);

                req.@params = AppInit.conf.accsdb.@params;

                httpContext.Features.Set(req);
                return _next(httpContext);
            }
        }
    }
}
