﻿using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class Vibix : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("vibix", AppInit.conf.Vibix);

        [HttpGet]
        [Route("lite/vibix")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int t = -1, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.Vibix;
            if (!init.enable || string.IsNullOrEmpty(init.token))
                return OnError();

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            JObject data = await search(imdb_id, kinopoisk_id);
            if (data == null)
                return OnError();

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var proxy = proxyManager.Get();

            if (data.Value<string>("type") == "movie")
            {
                #region Фильм
                string iframe_url = data.Value<string>("iframe_url");

                string memKey = $"vibix:video:{iframe_url}";
                if (!hybridCache.TryGetValue(memKey, out string file))
                {
                    string html = await HttpClient.Get(iframe_url, timeoutSeconds: 8, proxy: proxy);
                    if (html == null)
                        return OnError(proxyManager);

                    file = Regex.Match(html, "file:([^\n\r]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(file) || !file.Contains("/get_file/"))
                        return OnError();

                    hybridCache.Set(memKey, file, cacheTime(20));
                }

                var mtpl = new MovieTpl(title, original_title);

                var match = new Regex("([0-9]+p)\\](https?://[^,\t ]+\\.mp4)").Match(file);
                while (match.Success)
                {
                    mtpl.Append(match.Groups[1].Value, HostStreamProxy(init, match.Groups[2].Value, proxy: proxy, plugin: "vibix"));
                    match = match.NextMatch();
                }

                return ContentTo(rjson ? mtpl.ToJson(reverse: true) : mtpl.ToHtml(reverse: true));
                #endregion
            }
            else
            {
                return OnError();
            }
        }


        #region search
        async ValueTask<JObject> search(string imdb_id, long kinopoisk_id)
        {
            string memKey = $"vibix:view:{kinopoisk_id}:{imdb_id}";

            if (!hybridCache.TryGetValue(memKey, out JObject root))
            {
                var init = AppInit.conf.Vibix;

                async ValueTask<JObject> goSearch(string imdb_id, long kinopoisk_id)
                {
                    if (string.IsNullOrEmpty(imdb_id) && kinopoisk_id == 0)
                        return null;

                    string uri = kinopoisk_id > 0 ? $"kp/{kinopoisk_id}" : $"imdb/{imdb_id}";
                    var header = httpHeaders(init, HeadersModel.Init(
                        ("Accept", "application/json"),
                        ("Authorization", $"Bearer {init.token}"),
                        ("X-CSRF-TOKEN", "")
                    ));

                    var video = await HttpClient.Get<JObject>($"{init.host}/api/v1/publisher/videos/{uri}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: header);

                    if (video == null)
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    if (!video.ContainsKey("id"))
                        return null;

                    return video;
                }

                root = await goSearch(null, kinopoisk_id) ?? await goSearch(imdb_id, 0);
                if (root == null)
                    return null;

                proxyManager.Success();
                hybridCache.Set(memKey, root, cacheTime(30, init: init));
            }

            return root;
        }
        #endregion
    }
}