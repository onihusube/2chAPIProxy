using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _2chAPIProxy;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        //[TestMethod]
        //public void HTMLTranceTest()
        //{
        //    ViewModel test = new ViewModel();
        //    test.SkipAliveCheck = true;
        //    //var dat = HTMLtoDat.Gethtml(@"http://rosie.2ch.net/test/read.cgi/anime/1518737340/", 140, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.79 Safari/537.36 Edge/14.14393", false);
        //    var resdat = Encoding.GetEncoding("Shift_JIS").GetString(dat);
        //}

        [TestMethod]
        public void HTMLRegexTest()
        {
            String HTML = $"<div class=\"post\" id=\"3\" data-id=\"3\" data-userid=\"ID: C4i6qtzM0\" data-date=\"NG\"><div class=\"meta\"><span class=\"number\">3</span><span class=\"name\"><b><a href=\"mailto: sage\">名無しさん＠お腹いっぱい。</a></b></span><span class=\"date\">2017/12/28(木) 23:29:47.64</span><span class=\"uid\"></span><span class=\"be r2BP\"><a href=\"http://be.5ch.net/user/843246759\" target=\"_blank\\\">?2BP(1002)</a></span></div><div class=\"message\"><span class=\"escaped\"> <img src=\"//img.5ch.net/ico/3-2.gif\"> <br> この話題だせえ奴しかいないな </span></div></div>";
            Match date = Regex.Match(HTML, @"<(?:div|span) class=.date.+?>(.+?(?:</span><span class=" + '"' + @"\w+?" + '"' + @">.*?)?)</(?:div|span)>(?:<(?:div|span) class=.be\s.+?.>(.+?)</(?:div|span)>)?");
            Assert.AreEqual("2017/12/28(木) 23:29:47.64</span><span class=\"uid\">", date.Groups[1].Value);
        }

        [TestMethod]
        public void APITestfor5ch()
        {
            ViewModel test = new ViewModel();
            test.Use5chnet = false;
            APIAccess api = new APIAccess("JYW2J6wh9z8p8xjGFxO3M2JppGCyjQ", "hO2QHdapzbqbTFOaJgZTKXgT2gWqYS", "X-2ch-UA: JaneStyle/4.0.0", "Mozilla/4.0 (compatible; JaneStyle/4.0.0)", "Mozilla/4.0 (compatible; JaneStyle/4.0.0)", "", "", "");
            //APIAccess api = new APIAccess("eqCvjJPvTFUTI8JEc74lUQa0QWoNwF", "z3FZxjmD2hJrDYfOotFRNcmleILNtM", "X-2ch-UA: Hotzonu/2.0", "Monazilla/1.00 (Hotzonu/2.0)", "", "", "");
            //var res = api.GetDat("mercury", "erodoujin", "1514457513", -1, null, false);
            var res = api.GetDat("rosie", "anime", "1518737340", -1, null, false);
            if (res.StatusCode == System.Net.HttpStatusCode.OK)
            {
                using (System.IO.StreamReader reader = new System.IO.StreamReader(res.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                {
                    String dat = reader.ReadToEnd();
                }
            }
        }

        [TestMethod]
        public void ResReplace()
        {
            ViewModel test = new ViewModel();
            //APIAccess api = new APIAccess("JYW2J6wh9z8p8xjGFxO3M2JppGCyjQ", "hO2QHdapzbqbTFOaJgZTKXgT2gWqYS", "X-2ch-UA: JaneStyle/3.84", "Monazilla/1.00 JaneStyle/3.84 Windows/10.0.15063", "", "", "");
            APIAccess api = new APIAccess("eqCvjJPvTFUTI8JEc74lUQa0QWoNwF", "z3FZxjmD2hJrDYfOotFRNcmleILNtM", "X-2ch-UA: Hotzonu/2.0", "Monazilla/1.00 (Hotzonu/2.0)", "", "", "", "");
            var res = api.GetDat("egg", "software", "1506944835", -1, null);
            if (res.StatusCode == System.Net.HttpStatusCode.OK)
            {
                using (System.IO.StreamReader reader = new System.IO.StreamReader(res.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                {
                    StringBuilder datb = new StringBuilder(reader.ReadToEnd());
                    var replace = Regex.Matches(datb.ToString(), @"((?:h?ttps?|sssp)://\w+?\.)5ch\.net(/test/read\.cgi/\w+?/\d{9,}?|/ico/\w+?\.gif|/[a-zA-Z0-9]+)");
                    foreach(Match link in replace)
                    {
                        datb.Replace(link.Groups[0].Value, link.Groups[1].Value + "2ch.net" + link.Groups[2].Value);
                    }
                    datb.Replace("https://", " http://");
                }
            }
        }

        [TestMethod]
        public void MenuReplace()
        {
            String URI = $"https://menu.5ch.net/bbsmenu.html";
            HttpWebRequest GetItaList = (HttpWebRequest)WebRequest.Create(URI);
            GetItaList.Accept = "text/html, application/xhtml+xml, image/jxr, */*";
            GetItaList.AutomaticDecompression = DecompressionMethods.GZip;
            GetItaList.Headers[HttpRequestHeader.AcceptLanguage] = "ja-JP";
            GetItaList.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36 Edge/16.16299";
            GetItaList.KeepAlive = false;
            GetItaList.ServicePoint.Expect100Continue = false;

            HttpWebResponse SerchResultPage = (HttpWebResponse)GetItaList.GetResponse();
            using (StreamReader sr = new StreamReader(SerchResultPage.GetResponseStream(), Encoding.UTF8))
            {
                bool is2ch = true;
                String html = sr.ReadToEnd();
                var ItaMatches = Regex.Matches(html, $@"<(?:A HREF|a href)={'"'}(?:https?:)?(//\w+?\.(?:2ch\.net|5ch\.net|bbspink\.com)/\w+/?){'"'}>(.+)</(?:A|a)>");
                foreach (Match ita in ItaMatches)
                {
                    String replace = $"<A HREF=http:{ita.Groups[1].Value}>{ita.Groups[2].Value}</A>";
                    html.Replace(ita.Value, replace);
                }
                html = Regex.Replace(html, $@"<(?:A HREF|a href)={'"'}(?:https?:)?(//.+?){'"'}>(.+?)</(?:A|a)>", "<A HREF=http:$1>$2</A>");
                if (is2ch) html = html.Replace(".5ch.net/", ".2ch.net/");
            }
            return;
        }

        [TestMethod]
        public void ResAnkerReplace()
        {
            Regex CheckResAnker = new Regex(@"<a\s..\s(\w+?)\sread\.cgi\s(\w+?)\s(\d{9,11})\s(\d{1,5})" + '"' + @"\snoopener noreferrer" + '"' + @"\s(_blank" + '"' + @">&gt;&gt;\d{1,5}</a>)", RegexOptions.Compiled);

            String res = $"<a .. test read.cgi cryptocoin 1518419945 14{'"'} noopener noreferrer{'"'} _blank{'"'}>&gt;&gt;14</a>";
            var match = CheckResAnker.Match(res);
            String fixres = $"<a href={'"'}../{match.Groups[1].Value}/read.cgi/{match.Groups[2].Value}/{match.Groups[3].Value}/{match.Groups[4].Value}{'"'} rel={'"'}noopener noreferrer{'"'} target={'"'}{match.Groups[5].Value}";
            Assert.AreEqual($"<a href={'"'}../test/read.cgi/cryptocoin/1518419945/14{'"'} rel={'"'}noopener noreferrer{'"'} target={'"'}_blank{'"'}>&gt;&gt;14</a>", fixres);
        }

    }
}
