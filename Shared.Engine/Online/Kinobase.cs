﻿using Lampac.Models.LITE.Kinobase;
using Shared.Model.Online.Kinobase;
using Shared.Model.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class KinobaseInvoke
    {
        #region KinobaseInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public KinobaseInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onpost = onpost;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        async public ValueTask<EmbedModel?> Embed(string? title, int year, Func<string, ValueTask<string?>>? oneval = null)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            string? content = await onget($"{apihost}/search?query={HttpUtility.UrlEncode(title)}");
            if (content == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string? link = null, reservedlink = null;
            foreach (string row in content.Split("<li class=\"item\">").Skip(1))
            {
                if (row.Contains(">Трейлер</span>"))
                    continue;

                var g = Regex.Match(row, "alt=\"([^\"]+) \\(([0-9]{4})\\)\"").Groups;

                if (g[1].Value.ToLower().Trim() == title.ToLower())
                {
                    string rlnk = Regex.Match(row, "href=\"/([^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(rlnk))
                        continue;

                    reservedlink = rlnk;

                    if (g[2].Value == year.ToString())
                    {
                        link = reservedlink;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(link))
            {
                if (string.IsNullOrEmpty(reservedlink))
                {
                    if (content.Contains(">По запросу"))
                        return new EmbedModel() { IsEmpty = true };

                    return null;
                }

                link = reservedlink;
            }

            string? news = await onget($"{apihost}/{link}");
            if (news == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string video = Regex.Match(news, "<vide[^>]+ src=\"([^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(video))
            {
                requesterror?.Invoke();
                return null;
            }

            return new EmbedModel() { content = video };

            try
            {
                var res = JsonSerializer.Deserialize<List<Season>>(Regex.Match(content, "^pl\\|(\\[[^\n\r]+\\])").Groups[1].Value);
                if (res == null || res.Count == 0)
                    return null;

                return new EmbedModel() { serial = res, quality = content.Contains("1080p") ? "1080p" : content.Contains("720p") ? "720p" : content.Contains("480p") ? "480p" : "360p" };
            }
            catch { return null; }
        }
        #endregion

        #region Html
        public string Html(EmbedModel? md, string? title, int year, int s, bool rjson = false)
        {
            if (md == null || md.IsEmpty)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            #region getSubtitle
            SubtitleTpl getSubtitle(string _sub)
            {
                var subtitles = new SubtitleTpl();
                if (string.IsNullOrEmpty(_sub))
                    return subtitles;

                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,\\[\\| ]+\\.vtt)").Match(_sub);
                while (match.Success)
                {
                    subtitles.Append(match.Groups[1].Value, onstreamfile(match.Groups[2].Value));
                    match = match.NextMatch();
                }

                return subtitles;
            }
            #endregion

            if (md.content != null)
            {
                var mtpl = new MovieTpl(title);
                mtpl.Append(title, onstreamfile(md.content));

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
            }
            else
            {
                #region getStreamLink
                (string hls, string streansquality) getStreamLink(string _data)
                {
                    var streams = new List<(string link, string quality)>() { Capacity = 4 };

                    foreach (string quality in new List<string> { "1080", "720", "480", "360" })
                    {
                        var g = Regex.Match(_data, $"\\[({quality})p?\\](\\{{[^\\}}]+\\}})?(https?://[^\\[\\|,;\n\r\t ]+\\.(mp4|m3u8))").Groups;

                        string link = g[3].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        streams.Add((onstreamfile.Invoke(link), $"{quality}p"));
                    }

                    return (streams[0].link, "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}");
                }
                #endregion

                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);

                if (s == -1)
                {
                    html.Append($"<!--q:{md.quality}-->");

                    for (int i = 0; i < md.serial.Count; i++)
                    {
                        var season = md.serial[i];
                        if ((season?.playlist != null && season.playlist.Count > 0) || (season?.folder != null && season.folder.Count > 0))
                        {
                            string link = host + $"lite/kinobase?title={enc_title}&year={year}&s={i}";

                            html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + (season.comment ?? season.title) + "</div></div></div>");
                            firstjson = false;
                        }
                        else
                        {
                            if (season.file == null)
                                continue;

                            var streams = getStreamLink(season.file);
                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"1\" e=\"" + Regex.Match(season.comment ?? season.title, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams.hls + $"\",{streams.streansquality},\"title\":\"" + title + "\", \"subtitles\":" + getSubtitle(season.subtitle).ToJson() + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + (season.comment ?? season.title) + "</div></div>");
                            firstjson = false;
                        }
                    }
                }
                else
                {
                    string nameseason = Regex.Match(md.serial[s].comment ?? md.serial[s].title, "^([0-9]+)").Groups[1].Value;

                    var episodes = md.serial[s]?.playlist ?? md.serial[s]?.folder;
                    if (episodes == null)
                        return string.Empty;

                    foreach (var episode in episodes)
                    {
                        if (episode.file == null)
                            continue;

                        var streams = getStreamLink(episode.file);
                        html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + nameseason + "\" e=\"" + Regex.Match(episode.comment ?? episode.title, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams.hls + $"\",{streams.streansquality},\"title\":\"" + $"{title} ({episode.comment ?? episode.title})" + "\", \"subtitles\":" + getSubtitle(episode.subtitle).ToJson() + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + (episode.comment ?? episode.title) + "</div></div>");
                        firstjson = false;
                    }
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}
