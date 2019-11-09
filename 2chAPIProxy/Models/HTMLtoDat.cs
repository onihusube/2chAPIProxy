using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Net;
using System.Web;

namespace _2chAPIProxy
{
    public static class HTMLtoDat
    {
        static Assembly CompiledAssembly = null;

        //public String Proxy { get; set; }
        //public String UserAgent { get; set; }
        //public bool CEExternalRead { get; set; }
        //public bool AllRes { get; set; }
        //public bool SkipeAliveCheck { get; set; }

        static public Byte[] Gethtml(String URI, int range, String UA, bool CRReplace, String LastMod = null)
        {
            URI = URI.Replace("2ch.net", "5ch.net");
            if (ViewModel.Setting.CEExternalRead)
            {
                return HTMLTranceOutRegex(URI, range, UA, LastMod);
            }
            using (WebClient get = new WebClient())
            {
                get.Headers["User-Agent"] = ViewModel.Setting.UserAgent4;
                try
                {
                    if (ViewModel.Setting.ProxyAddress != "") get.Proxy = new WebProxy(ViewModel.Setting.ProxyAddress);
                    using (System.IO.StreamReader html = new System.IO.StreamReader(get.OpenRead(URI), Encoding.GetEncoding("Shift_JIS")))
                    {
                        String title = "もうずっと人大杉", ketu = "";
                        //dat構築用StringBuilder
                        var Builddat = new StringBuilder(510 * 1024);
                        bool alive = true, NewCGI = false;
                        //タイトルの検索
                        for (String line = html.ReadLine(); !html.EndOfStream; line = html.ReadLine())
                        {
                            if (Regex.IsMatch(line, @"<title>(.+?)<\/title>"))
                            {
                                title = Regex.Match(line, @"<title>(.+?)<\/title>").Groups[1].Value;
                                break;
                            }
                            else if (Regex.IsMatch(line, @"<title>(.+?)$"))
                            {
                                title = Regex.Match(line, @"<title>(.+?)$").Groups[1].Value;
                                NewCGI = true;
                                break;
                            }
                        }
                        if (Regex.IsMatch(title, @"(５ちゃんねる error \d+|もうずっと人大杉|datが存在しません.削除されたかURL間違ってますよ)")) return new byte[] { 0 };
                        if (Regex.IsMatch(title, @"(2|5)ch\.net\s(\[\d+\])"))
                        {
                            var tmatch = Regex.Match(title, @"(2|5)ch\.net\s(\[\d+\])").Groups;
                            title = title.Replace(tmatch[0].Value, $"{tmatch[1].Value}ch.net\t {tmatch[2].Value}");
                        }
                        if (CRReplace) title = title.Replace("&#169;", "&copy;");
                        //新CGI形式と古いCGI形式で処理を分ける
                        if (NewCGI)
                        {
                            String line = html.ReadLine();
                            
                            //スレッド本文探索
                            do
                            {
                                if (Regex.IsMatch(line, @"<d(?:iv|l) class=.(?:thread|post).+?>")) break;
                                line = html.ReadLine();
                            } while (!html.EndOfStream);

                            //スレ生存チェック
                            if (!ViewModel.Setting.SkipAliveCheck)
                            {
                                if (Regex.IsMatch(line, @"<div class=" + '"' + @"[a-zA-Z\s]+?" + '"' + @">(.+?過去ログ倉庫.+?|レス数が\d{3,}を超えています.+?(書き込み.*?|表.?示)でき.+?)</div>") == false)
                                {
                                    return new byte[] { 0, 0 };
                                }
                            }

                            var Bres = new StringBuilder(5 * 1024);
                            //pinkレスずれ処理用
                            bool pink = URI.Contains("bbspink.com");
                            int datResnumber = 1, htmlResnumber = 0;
                            long ThreadTime = long.Parse(Regex.Match(URI, @"/(\d{9,})").Groups[1].Value);
                            var ResMatches = Regex.Matches(line, @"<(?:div|dl) class=.post. id=.\d.+?>(.+?(?:</div></div>|</dd></dl>))");
                            foreach (Match Res in ResMatches)
                            {
                                //Match date = Regex.Match(Res.Groups[1].Value, @"<(?:div|span) class=.date.+?>(.+?)</(?:div|span)>(?:<(?:div|span) class=.be\s.+?.>(.+?)</(?:div|span)>)?");
                                Match date = Regex.Match(Res.Groups[1].Value, @"<(?:div|span) class=.date.+?>(.+?(?:</span><span class=" + '"' + @"\w+?" + '"' + @">.*?)?)</(?:div|span)>(?:<(?:div|span) class=.be\s.+?.>(.+?)</(?:div|span)>)?");
                                String number = Regex.Match(Res.Groups[1].Value, @"<(?:div|span) class=.number.+?>(\d{1,5})(?: : )?</(?:div|span)>").Groups[1].Value;
                                //0,NGの検出
                                if (number == "0" && date.Groups[1].Value == "NG")
                                {
                                    //飛ばす
                                    continue;
                                }
                                //htmlでレスが飛んでいるときを検出
                                if (pink && int.TryParse(number, out htmlResnumber) && datResnumber < htmlResnumber)
                                {
                                    for (int j = htmlResnumber - datResnumber; j > 0; --j)
                                    {
                                        Builddat.Append("うふ～ん<>うふ～ん<>うふ～ん ID:DELETED<>うふ～ん<>うふ～ん<>\n");
                                    }
                                    datResnumber = htmlResnumber;
                                }
                                //String name = Regex.Match(Res.Groups[1].Value, $"<(?:div|span) class={'"'}name{'"'}>((?:{'"'}.*?{'"'}|'.*?'|[^'{'"'}])+?)</(?:div|span)>").Groups[1].Value;
                                String name = Regex.Match(Res.Groups[1].Value, $"<(?:div|span) class=.name.+?>(.+?(?:</b>|</a>))</(?:div|span)>").Groups[1].Value;
                                //目欄が空の時フォントカラー指定を消す
                                if (!name.Contains("<a href=" + '"' + "mailto:"))
                                {
                                    name = Regex.Replace(name, @"<font color=.green.>", "");
                                    name = name.Replace("</font>", "");
                                }
                                //ID部のspanタグ削除
                                String dateid = date.Groups[1].Value;
                                if (dateid.Contains("</span><span "))
                                {
                                    dateid = Regex.Replace(dateid, $"</span><span class={'"'}" + @"\w+?" + $"{'"'}>", " ");
                                }
                                //日付IDがNGになっているとき                         
                                if (dateid.Contains("NG NG"))
                                {
                                    DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                                    UnixEpoch = UnixEpoch.AddSeconds(ThreadTime);
                                    String time = UnixEpoch.ToLocalTime().ToString("yyyy/MM/dd(ddd) HH:mm:ss.00");
                                    dateid = time + " ID:NG0";
                                }
                                //beリンク処理
                                String be = "";
                                if (!string.IsNullOrEmpty(date.Groups[2].Value))
                                {
                                    var mb = Regex.Match(date.Groups[2].Value, @"<a href.+?(\d{2,}).+?>(.+)$");
                                    be = $" <a href={'"'}javascript:be({mb.Groups[1].Value});{'"'}>{mb.Groups[2].Value}";
                                }
                                String message = Regex.Match(Res.Groups[1].Value, @"<d(?:iv|d) class=.(?:message|thread_in).+?>(?:<span class=.escaped.>)?(.+?)(?:</span>)?(?:</div></div>|</dd></dl>)").Groups[1].Value;
                                //安価のリンク修正、http://potato.2ch.net/test/read.cgi/jisaku/1447271149/9→../test/read.cgi/jisaku/1447271149/9
                                Bres.Append(message);
                                foreach (Match item in Regex.Matches(message, @"(<a href=.)(?:https?:)?//\w+\.((?:2|5)ch\.net|bbspink\.com)(/test/read.cgi/\w+/\d+/\d{1,4}.\s.+?>&gt;&gt;\d{1,5}</a>)"))
                                {
                                    Bres.Replace(item.Groups[0].Value, item.Groups[1].Value + ".." + item.Groups[3].Value);
                                }
                                //お絵かきリンク修正
                                foreach (Match item in Regex.Matches(message, $@"<a\s(?:class={'"'}image{'"'}\s)?href=" + '"' + @"(?:https?:)?//jump.(?:2|5)ch\.net/\?(https?://[a-zA-Z\d]+?\.8ch.net\/.+?\.\w+?)" + '"' + @">https?://[a-zA-Z\d]+?\.8ch\.net\/.+?\.\w+?</a>"))
                                {
                                    Bres.Replace(item.Groups[0].Value, "<img src=" + '"' + item.Groups[1].Value + '"' + ">");
                                }
                                //p53など、レス前後にスペースが無いときに補う。
                                if (!Regex.IsMatch(message, @"^\s.+\s$"))
                                {
                                    Bres.Insert(0, " ");
                                    Bres.Append(" ");
                                }
                                Bres.Insert(0, "：" + dateid + be + "<dd>");
                                Bres.Insert(0, "<dt>" + number + " ：" + name);
                                Bres.Append("<br><br>");
                                Builddat.Append(html2dat(Bres.ToString()));
                                if (!String.IsNullOrEmpty(title))
                                {
                                    Builddat.Append(title + "\n");
                                    title = "";
                                }
                                else Builddat.Append("\n");
                                Bres.Clear();
                                datResnumber++;
                            }
                            ketu = Regex.Match(line, @"<(?:div|li) class=.+?>(?<datsize>\d+?)KB</(?:div|li)>").Groups[1].Value;
                        }
                        else
                        {
                            if (!ViewModel.Setting.SkipAliveCheck)
                            {
                                //dat落ちかチェック
                                for (String line = html.ReadLine(); !html.EndOfStream; line = html.ReadLine())
                                {
                                    if (Regex.IsMatch(line, @"<div.*?>(.+?過去ログ倉庫.+?|レス数が\d{3,}を超えています.+?(書き込み.*?でき|表示しません).+?)</div>"))
                                    {
                                        alive = false;
                                        break;
                                    }
                                    else if (Regex.IsMatch(line, @"<h1 style.+>.+?<\/h1>"))
                                    {
                                        alive = true;
                                        break;
                                    }
                                }
                                //生きているなら終了
                                if (alive) return new byte[] { 0, 0 };
                            }
                            String ResHtml = html.ReadToEnd();
                            System.Collections.Concurrent.ConcurrentDictionary<int, string> Trancedat = new System.Collections.Concurrent.ConcurrentDictionary<int, string>(4, 1005);
                            System.Threading.Tasks.ParallelOptions option = new System.Threading.Tasks.ParallelOptions();
                            option.MaxDegreeOfParallelism = 4;
                            System.Threading.Tasks.Parallel.ForEach<Match>(Regex.Matches(ResHtml, @"<dt>(\d{1,4})\s：.+?<br><br>(?:\r|\n)").Cast<Match>(), option, match =>
                            {
                                Trancedat[int.Parse(match.Groups[1].Value) - 1] = html2dat(match.Groups[0].Value) + "\n";
                            });
                            Builddat.Append(Trancedat[0].Substring(0, Trancedat[0].Length - 1) + title + "\n");
                            for (int i = 1; i < Trancedat.Count; ++i) Builddat.Append(Trancedat[i]);
                            if (!ViewModel.Setting.AllReturn || range > -1) ketu = Regex.Match(ResHtml, @"<font\scolor.+?><b>(\d+)\sKB<\/b><\/font>").Groups[1].Value;
                        }
                        //if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                        //{
                        //    Builddat = new StringBuilder(HTMLtoDat.ResContentReplace(Builddat.ToString()));
                        //}
                        //Byte[] Bdat = Encoding.GetEncoding("Shift_JIS").GetBytes(Builddat.ToString());
                        Byte[] Bdat = Encoding.GetEncoding("Shift_JIS").GetBytes((ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink) ? (HTMLtoDat.ResContentReplace(Builddat.ToString())) : (Builddat.ToString()));
                        if (ViewModel.Setting.AllReturn  || range < 0) return Bdat;
                        int size;
                        try
                        {
                            size = int.Parse(ketu);
                        }
                        catch (FormatException)
                        {
                            size = 0;
                        }
                        //差分返答処理
                        return DifferenceDetection(Bdat, LastMod, UA, range, size);
                    }
                }
                catch (System.Threading.ThreadAbortException e)
                {
                    throw e;
                }
                catch (Exception err)
                {
                    ViewModel.OnModelNotice(URI + "をHTMLから変換中にエラーが発生しました。\n" + err.ToString());
                    return new byte[] { 0 };
                }
            }
        }
        
        private static String html2dat(String res)
        {
            var Bdat = new StringBuilder(res.Length * 2);
            String temp;
            //名前抽出
            temp = Regex.Match(res, @"<dt>\d{1,5}\s：(.+)<dd>\s?").Value;
            //Bdat.Append(Regex.Match(temp, @"<b>(.+)<\/b>").Groups[1].Value + "<>");
            Bdat.Append(Regex.Match(temp, $"<b>(?:<a href=(?:{'"'}.*?{'"'}|'.*?'|[^'{'"'}])+?>(?<name>.+?)</a>|(?<name>.+))</b>").Groups["name"].Value + "<>");
            //目蘭抽出
            //Bdat.Append(Regex.Match(temp, @"mailto:(.+)" + '"' + "><b>").Groups[1].Value + "<>");
            Bdat.Append(Regex.Match(temp, $"<a href={'"'}mailto:((?:{'"'}.*?{'"'}|'.*?'|[^'{'"'}])+?){'"'}>").Groups[1].Value + "<>");
            //あぼ～ん時の処理
            if (Regex.IsMatch(temp, @">：(.+\sID:DELETED)"))
            {
                Bdat.Append(Regex.Match(temp, @">：(.+\sID:DELETED)").Groups[1].Value + "<>");
                goto honbun;
            }
            //投稿日時+ID抽出
            //var group = Regex.Match(temp, @"：(?:(\d{4}\/.+ID:.+)\s<a\s|(\d{4}\/.+ID:.+)<dd>|(\d{4}\/.+?)<dd>\s<a\s|(\d{4}\/.+?)\s<a\s|(\d{4}\/.+)<dd>)").Groups;
            //Bdat.Append(group[1].Value + group[2].Value + group[3].Value + group[4].Value + group[5].Value);
            var DateID = Regex.Match(temp, @"：(?:(?<date>\d{4}\/.+ID:.+)\s<a\s|(?<date>\d{4}\/.+ID:.+)<dd>|(?<date>\d{4}\/.+?)<dd>\s<a\s|(?<date>\d{4}\/.+?)\s<a\s|(?<date>\d{4}\/.+)<dd>)").Groups["date"].Value;
            Bdat.Append(DateID);
            //1001時の処理
            if (String.IsNullOrEmpty(DateID))
            {
                Bdat.Append(Regex.Match(temp, @">：(.+)<dd>").Groups[1].Value + "<>");
                goto honbun;
            }
            //Be抽出
            if (Regex.IsMatch(temp, @"javascript:be"))
            {
                var group = Regex.Match(temp, @"(\s<a href=" + '"' + ".+?" + '"' + @">.+?<\/a>\s)?<a href=" + '"' + @"javascript:be\((\d+)\).{4}(.+\(\d+\).*|.+)<\/a>").Groups;
                if (group[1].Value != "") Bdat.Append(" ");
                Bdat.Append(group[1].Value + " BE:" + group[2].Value + "-" + group[3].Value);
            }
            Bdat.Append("<>");
        honbun:     //本文取得
            var ResBody = new StringBuilder(5 * 1024);
            temp = "";
            temp = Regex.Match(res, @".+<dd>(\s.+\s|.{2}～ん)<br><br>$").Groups[1].Value;
            if (String.IsNullOrEmpty(temp)) ResBody.Append(Regex.Match(res, @".+やっと出た<dd>(.+?)<br><br>$").Groups[1].Value);
            else ResBody.Append(temp);
            //リンクをURL文字列に戻す、http://のみのリンクに対応するため、.*?にする。.+?だと正しく抽出されない。
            var t = Regex.Matches(temp, $@"<a\s(?:class={'"'}image{'"'}\s)?href=.(?:https?:)?\/\/.+?>(https?:\/\/.*?)<\/a>");
            foreach (Match m in t) ResBody.Replace(m.Groups[0].Value, m.Groups[1].Value);
            t = Regex.Matches(temp, @"<a\shref=.ftp:\/\/.+?>(ftp:\/\/.*?)<\/a>");
            foreach (Match m in t) ResBody.Replace(m.Groups[0].Value, m.Groups[1].Value);
            //お絵カキコリンクを処理
            var oekakiko = Regex.Matches(temp, @"<img\ssrc=" + '"' + @"(?:https?:)?(\/\/[a-zA-Z\d]+?\.8ch\.net\/\w+?\.\w+?)" + '"' + ">");
            foreach (Match m in oekakiko) ResBody.Replace(m.Groups[0].Value, "sssp:" + m.Groups[1].Value);
            //Beアイコン、絵文字リンクの成型http:→sssp:
            //先頭行のリンク処理
            if (Regex.IsMatch(temp, @"^\s(<img src=.(?:https?:)?(\/\/img\.(?:2|5)ch\.net.+?).>)(?:(\s)<br>)?"))
            {
                var mae = Regex.Match(temp, @"^\s(<img src=.(?:https?:)?(\/\/img\.(?:2|5)ch\.net.+?).>)(?:(\s)<br>)?").Groups;
                if (Regex.IsMatch(Bdat.ToString(), @"BE:\d+")) ResBody.Replace(mae[1].Value, "sssp:" + mae[2].Value);
                else ResBody.Replace(mae[1].Value, " sssp:" + mae[2].Value + mae[3].Value);
            }
            //その他本文中リンク処理
            t = Regex.Matches(temp, @"(?:<br>(\s))?(<img src=.(?:https?:)?(\/\/img\.(?:2|5)ch\.net.+?).>)(?:(\s)<br>)?");
            foreach (Match m in t) ResBody.Replace(m.Groups[2].Value, m.Groups[1].Value + "sssp:" + m.Groups[3].Value + m.Groups[4].Value);
            //本文末に存在する場合にスペース付加
            if (Regex.IsMatch(temp, @"sssp:\/\/img\.(?:2|5)ch\.net\/\w+\/[^/]+\.\w{3,5}$")) ResBody.Append(" ");
            Bdat.Append(ResBody);
            Bdat.Append("<>");
            //<br>が文末or文頭にある時にスペースを補う
            Bdat.Replace("<br> <>", "<br>  <>");
            Bdat.Replace("<br> <>", "<br>  <>");
            //<br>の連続時にスペースを補う
            Bdat.Replace("<br> <br>", "<br>  <br>");
            return Bdat.Replace("<br> <br>", "<br>  <br>").ToString();
        }

        private static Byte[] DifferenceDetection(Byte[] dat, String LastMod, String UA, int range, int size)
        {
            bool giko = UA.IndexOf("gikoNavi") > -1;
            bool xeno = UA.IndexOf("JaneXeno") > -1;
            //差分返答処理
            if (LastMod != null)
            {
                using (System.IO.MemoryStream stream = new System.IO.MemoryStream(dat))
                using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.GetEncoding("Shift_JIS")))
                {
                    var Cul = new System.Globalization.CultureInfo("ja-jp");
                    String[] ParseFormat = { "ddd, d MMM yyyy HH:mm:ss K", "r" };
                    DateTime LastModfied = DateTime.Parse(LastMod, Cul, System.Globalization.DateTimeStyles.AssumeLocal);
                    if (LastMod.IndexOf("+0900") >= 0) LastModfied = LastModfied.AddHours(9);
                    DateTime PostTime = DateTime.Now;
                    Regex GetResTime = new Regex(@"^.+?<>.*?<>(\d{4}\/\d{2}/\d{2}.{3}\s\d{2}:\d{2}:\d{2}(\.\d{1,3})?)(?:\d{1,})?\s(ID:.+?)?<>");
                    String Res = "", bRes = "";
                    int gindex = 0;
                    int beforeSecond = 0;

                    //LastModifiedから差分レスを推測
                    for (String res = reader.ReadLine(); !reader.EndOfStream; res = reader.ReadLine())
                    {
                        try
                        {
                            beforeSecond = PostTime.Second;
                            PostTime = DateTime.Parse(GetResTime.Match(res).Groups[1].Value, Cul, System.Globalization.DateTimeStyles.AssumeLocal);
                        }
                        catch (Exception)
                        {
                            //一部の板で、ミリ秒単位がｳﾝｺになってる場合への対策
                            try
                            {
                                String unko = Regex.Match(res, @"^.+?<>.*?<>((\d{4}\/\d{2}/\d{2}.{3}\s\d{2}:\d{2}:\d{2})(\.ｳﾝｺ))\s(ID:.+?)?<>").Groups[2].Value;
                                PostTime = DateTime.Parse(unko, Cul, System.Globalization.DateTimeStyles.AssumeLocal);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                        }
                        if (LastModfied.CompareTo(PostTime) <= 0)
                        {
                            //LastModifiedのミリ秒単位の四捨五入に対応する処理
                            if (LastModfied.Second != PostTime.Second && LastModfied.Second - beforeSecond == 1)
                            {
                                Res = res + "\n";
                                if (giko) res = bRes;
                            }
                            else if (LastModfied.Second != PostTime.Second && beforeSecond == 59)
                            {
                                Res = res + "\n";
                                if (giko) res = bRes;
                            }
                            //ギコナビ、-16バイト返答に対応する処理
                            if (giko)
                            {
                                Res = res + "\n" + Res;
                                gindex = Encoding.GetEncoding("Shift_JIS").GetBytes(res + "\n").Length;
                            }
                            string temp = reader.ReadLine();
                            //1000レス以降を二回読まないように
                            if (!String.IsNullOrEmpty(temp))
                            {
                                if (temp.IndexOf("<><>Over 1000 Thread<>") >= 0) return new Byte[] { 0 };
                                Res += temp + "\n";
                            }
                            break;
                        }
                        if (giko) bRes = res;
                    }
                    if (reader.EndOfStream && Res == "") return new Byte[] { 0 };
                    Res += reader.ReadToEnd();
                    if (giko)
                    {
                        //ギコナビ返答用に-16バイト分を読み込み
                        Byte[] Bytedat = Encoding.GetEncoding("Shift_JIS").GetBytes(Res);
                        int ResLength = Bytedat.Length - gindex + 16;
                        Byte[] Resdat = new Byte[ResLength];
                        Buffer.BlockCopy(Bytedat, gindex - 16, Resdat, 0, ResLength);
                        return Resdat;
                    }
                    if (!xeno) Res = "\n" + Res;
                    return Encoding.GetEncoding("Shift_JIS").GetBytes(Res);
                }
            }
            //dat容量から差分推測
            if (size > 0)
            {
                int htmlbyte = size, gendatbyte = dat.Length, Resindex = range;
                Resindex += (giko) ? (16) : (1);
                if (Resindex >= gendatbyte) return new byte[] { 0 };
                htmlbyte *= 1024;
                int zure = htmlbyte - gendatbyte;
                if (zure >= 1024) Resindex -= (int)Math.Ceiling((double)zure * (range / htmlbyte));
                if (gendatbyte > Resindex)
                {
                    //前方に探索
                    for (int i = Resindex; i >= 0; --i) if (dat[i] == 0x0a) { Resindex = i; break; }
                    if (giko) Resindex -= 15;
                    Byte[] Resdat = new Byte[gendatbyte - Resindex];
                    Buffer.BlockCopy(dat, Resindex, Resdat, 0, Resdat.Length);
                    return Resdat;
                }
            }
            return new Byte[] { 0 };
        }

        public static Byte[] HTMLTranceOutRegex(String URI, int range, String UA, String LastMod = null)
        {
            if (CompiledAssembly == null)
            {
                ViewModel.OnModelNotice("外部HTMLtoDatコードのコンパイルが行われていません");
                return new byte[] { 0 };
            }
            Type t = CompiledAssembly.GetType("HtmlToDatConverter", false, false);
            using (WebClient get = new WebClient())
            {
                get.Headers["User-Agent"] = ViewModel.Setting.UserAgent4;
                try
                {
                    String dat = "", ketu = "";
                    if (ViewModel.Setting.ProxyAddress != "") get.Proxy = new WebProxy(ViewModel.Setting.ProxyAddress);
                    using (System.IO.StreamReader html = new System.IO.StreamReader(get.OpenRead(URI), Encoding.GetEncoding("Shift_JIS")))
                    {
                        String thredhtml = html.ReadToEnd();
                        if (t != null) dat = (String)t.InvokeMember("HTMLConvert", BindingFlags.InvokeMethod, null, null, new object[] { thredhtml });
                        ketu = Regex.Match(thredhtml, @"<div class=.cLength.>(\d+)KB</div>").Groups[1].Value;
                    }
                    if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                    {
                        dat = HTMLtoDat.ResContentReplace(dat);
                    }
                    Byte[] Bdat = Encoding.GetEncoding("Shift_JIS").GetBytes(dat);
                    if (ViewModel.Setting.AllReturn || range < 0) return Bdat;
                    int size;
                    try
                    {
                        size = int.Parse(ketu);
                    }
                    catch (FormatException)
                    {
                        size = 0;
                    }
                    return DifferenceDetection(Bdat, LastMod, UA, range, size);
                }
                catch (System.Threading.ThreadAbortException e)
                {
                    throw e;
                }
                catch (Exception err)
                {
                    ViewModel.OnModelNotice(URI + "をHTMLから変換中にエラーが発生しました。\n" + err.ToString());
                    return new byte[] { 0 };
                }
            }
        }

        public static String Compile(String SourceFilePath)
        {
            if (!System.IO.File.Exists(SourceFilePath)) return "指定されたソースファイルが見つかりません。";
            CodeDomProvider Cscp;
            if (System.IO.Path.GetExtension(SourceFilePath).ToLower() == ".vb")
            {
                Cscp = new Microsoft.VisualBasic.VBCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } });
            }
            else
            {
                Cscp = new Microsoft.CSharp.CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } });
            }
            using (Cscp)
            {
                CompilerParameters Cparam = new CompilerParameters();
                Cparam.GenerateInMemory = true;
                Cparam.IncludeDebugInformation = false;
                Cparam.CompilerOptions = "/optimize";
                Cparam.ReferencedAssemblies.Add("System.dll");
                Cparam.ReferencedAssemblies.Add("System.Core.dll");
                Cparam.ReferencedAssemblies.Add("System.Windows.dll");
                CompilerResults Cres = Cscp.CompileAssemblyFromFile(Cparam, SourceFilePath);
                if (Cres.Errors.Count == 0)
                {
                    CompiledAssembly = Cres.CompiledAssembly;
                    return "コンパイルに成功しました";
                }
                else
                {
                    String err = "コンパイルエラー";
                    foreach (CompilerError ce in Cres.Errors)
                    {
                        err += "\n" + ce.Line + "行目：" + ce.ErrorText;
                    }
                    return err;
                }
            }
        }

        public static String CompileTest(String URI)
        {
            if (CompiledAssembly == null) return "コンパイルが行われていません";
            return Encoding.GetEncoding("Shift_JIS").GetString(HTMLTranceOutRegex(URI, -1, ViewModel.Setting.UserAgent4));
        }

        static Regex CheckResContent = new Regex(@"((?:h?ttps?|sssp)://\w+?\.)5ch\.net(/test/read\.cgi/\w+?/\d{9,}?|/ico/\w+?\.gif|/[a-zA-Z0-9]+)", RegexOptions.Compiled);
        static Regex CheckHttpsLink = new Regex(@"(h?ttp)s(://\w+\.(?:(?:2|5)ch.net|bbspink\.com)/)", RegexOptions.Compiled);
        public static String ResContentReplace(String dat)
        {
            StringBuilder datb = new StringBuilder(dat);

            if (ViewModel.Setting.Replace5chURI)
            {
                var replace = CheckResContent.Matches(dat);
                foreach (Match link in replace)
                {
                    datb.Replace(link.Groups[0].Value, link.Groups[1].Value + "2ch.net" + link.Groups[2].Value);
                }
            }
            if (ViewModel.Setting.ReplaceHttpsLink)
            {
                var replace = CheckHttpsLink.Matches(datb.ToString());
                foreach (Match https in replace)
                {
                    datb.Replace(https.Groups[0].Value, $" {https.Groups[1].Value}" + https.Groups[2].Value);
                }
            }
            return datb.ToString();
        }
    }
}

/*
APIからのdatはうふ～んやあぼ～んが入るが、HTMLでは飛んでいる
http://mercury.bbspink.com/test/read.cgi/natuero/1092435303/

ところが、今のところ2chだとレス番飛びはDAT異常とみなすほうがいいかも
http://potato.2ch.net/test/read.cgi/applism/1484168622/
レス番0が追加されて、レス番564が抜けているが
564を補完すると逆に以降対応がずれる
（もちろんブラウザでHTMLを見てもずれている） 

 */
//20000越えスレ
//http://hitomi.2ch.net/test/read.cgi/poverty/1489804669/
//10000越えスレ
//http://hanabi.2ch.net/test/read.cgi/ms/1489765320/
//5000越え
//http://hitomi.2ch.net/test/read.cgi/poverty/1489766071/
