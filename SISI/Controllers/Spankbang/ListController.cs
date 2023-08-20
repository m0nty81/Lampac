﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Spankbang
{
    public class ListController : BaseSisiController
    {
        #region headers
        public static List<(string name, string val)> headers = new List<(string name, string val)>() 
        {
            ("cache-control", "no-cache"),
            ("dnt", "1"),
            ("pragma", "no-cache"),
            ("sec-ch-ua", "\"Chromium\";v=\"112\", \"Google Chrome\";v=\"112\", \"Not:A-Brand\";v=\"99\""),
            ("sec-ch-ua-mobile", "?0"),
            ("sec-ch-ua-platform", "\"Windows\""),
            ("sec-fetch-dest", "empty"),
            ("sec-fetch-mode", "cors"),
            ("sec-fetch-site", "cross-site"),
            ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36")
        };
        #endregion

        [HttpGet]
        [Route("sbg")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.Spankbang.enable)
                return OnError("disable");

            string memKey = $"sbg:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("sbg", AppInit.conf.Spankbang);
                var proxy = proxyManager.Get();

                string html = await SpankbangTo.InvokeHtml(AppInit.conf.Spankbang.host, search, sort, pg, url => HttpClient.Get(url, httpversion: 2, timeoutSeconds: 10, proxy: proxy, addHeaders: headers));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = SpankbangTo.Playlist($"{host}/sbg/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 2));
            }

            return OnResult(playlists, SpankbangTo.Menu(host, sort));
        }
    }
}