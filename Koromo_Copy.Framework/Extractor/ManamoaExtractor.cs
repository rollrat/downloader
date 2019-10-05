﻿// This source code is a part of Koromo Copy Project.
// Copyright (C) 2019. dc-koromo. Licensed under the MIT Licence.

using HtmlAgilityPack;
using Koromo_Copy.Framework.Network;
using Koromo_Copy.Framework.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Koromo_Copy.Framework.Extractor.IExtractorOption;

namespace Koromo_Copy.Framework.Extractor
{
    public class ManamoaExtractorOption : IExtractorOption
    {
    }

    public class ManamoaExtractor : ExtractorModel
    {
        public ManamoaExtractor()
        {
            HostName = new Regex(@"manamoa\d+\.net");
            ValidUrl = new Regex(@"^(?<host>https?://manamoa\d+\.net)/bbs/(?<type>page|board)\.php.*(manga_id|wr_id)=(?<code>\d+).*?$");
        }

        public class ComicInformation
        {
            public string Title;
            public string Author;
        }

        public override IExtractorOption RecommendOption(string url)
        {
            var match = ValidUrl.Match(url).Groups;

            if (match["type"].Value == "board")
                return new NaverExtractorOption { Type = ExtractorType.EpisodeImages };
            else
                return new NaverExtractorOption { Type = ExtractorType.Works };
        }

        public override string RecommendFormat(IExtractorOption option)
        {
            if (option.Type == ExtractorType.EpisodeImages)
                return "%(episode)s/%(file)s.%(ext)s";
            else
                return "%(title)s/%(episode)s/%(file)s.%(ext)s";
        }

        public override Tuple<List<NetTask>, object> Extract(string url, IExtractorOption option = null)
        {
            if (option == null)
                option = RecommendOption(url);

            var html = NetTools.DownloadString(url);
            var match = ValidUrl.Match(url).Groups;

            var document = new HtmlDocument();
            document.LoadHtml(html);
            var node = document.DocumentNode;

            if (option.Type == ExtractorType.EpisodeImages)
            {
                var images = get_board_images(html);
                var title = node.SelectSingleNode("/html[1]/head[1]/title[1]").InnerText;

                var result = new List<NetTask>();
                int count = 1;
                foreach (var img in images)
                {
                    var task = NetTask.MakeDefault(img);
                    task.SaveFile = true;
                    task.Filename = count.ToString("000") + Path.GetExtension(img.Split('/').Last());
                    task.Format = new ExtractorFileNameFormat
                    {
                        Episode = title,
                        FilenameWithoutExtension = count.ToString("000"),
                        Extension = Path.GetExtension(task.Filename).Replace(".", "")
                    };
                    result.Add(task);
                    count++;
                }

                return new Tuple<List<NetTask>, object>(result, null);
            }
            else if (option.Type == ExtractorType.Works)
            {
                var title = node.SelectSingleNode("/html[1]/body[1]/div[1]/div[2]/div[1]/div[1]/div[1]/div[1]/div[1]/div[1]/div[1]/div[1]/div[2]/div[1]/div[1]").InnerText;
                var sub_urls = new List<string>();
                var sub_titles = new List<string>();

                foreach (var item in node.SelectNodes("/html[1]/body[1]/div[1]/div[2]/div[1]/div[1]/div[1]/div[1]/div[1]/div[1]/div[2]/div[2]/div[1]/div[1]/div"))
                {
                    sub_urls.Add(item.SelectSingleNode("./a[1]").GetAttributeValue("href", ""));
                    sub_titles.Add(item.SelectSingleNode("./a[1]/div[1]").MyText());
                }

                var result = new List<NetTask>();
                for (int i = 0; i < sub_urls.Count; i++)
                {
                    var surl = match["host"] + sub_urls[i];
                    option.PageReadCallback?.Invoke(surl);
                    var html2 = NetTools.DownloadString(surl);
                    var images = get_board_images(html2);
                    int count = 1;
                    foreach (var img in images)
                    {
                        var task = NetTask.MakeDefault(img);
                        task.SaveFile = true;
                        task.Filename = count.ToString("000") + Path.GetExtension(img.Split('/').Last());
                        task.Format = new ExtractorFileNameFormat
                        {
                            Title = title,
                            Episode = sub_titles[i],
                            FilenameWithoutExtension = count.ToString("000"),
                            Extension = Path.GetExtension(task.Filename).Replace(".", "")
                        };
                        result.Add(task);
                        count++;
                    }
                }

                return new Tuple<List<NetTask>, object>(result, null);
            }

            return null;
        }

        private List<string> get_board_images(string html)
        {
            var list = JArray.Parse(Regex.Match(html, "var img_list = (.*);").Groups[1].Value);
            return list.Select(x => x.ToString()).ToList();
        }
    }
}