using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Net;
using System.IO;
using System.ComponentModel;
using System.Collections.Concurrent;

namespace _2chAPIProxy.HtmlConverter
{

    /// <summary>
    /// HTML変換とその応答に関する機能を提供するクラス
    /// </summary>
    public class HtmltoDat : IHtmlConverter
    {
        Assembly m_CompiledAssembly = null;
        readonly object m_SyncObj = new object();

        string m_CurrentError = "";
        /// <summary>
        /// 現在のエラー情報、INotifyPropertyChangedによる通知あり
        /// </summary>
        public string CurrentError
        {
            get => m_CurrentError;
            private set
            {
                lock (m_SyncObj)
                {
                    m_CurrentError = value;
                    this.NotifyPropertyChanged(nameof(CurrentError));
                }
            }
        }

        /// <summary>
        /// HTML取得に使用するUserAgent
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// HTML取得時に経由するプロクシのアドレス
        /// </summary>
        public string ProxyAddress { get; set; }

        /// <summary>
        /// 外部のHTML変換定義を使用するか否か
        /// </summary>
        public bool IsExternalConverterUse { get; set; }

        /// <summary>
        /// スレッド生存判定をスキップするか否か
        /// </summary>
        public bool IsAliveCheckSkip { get; set; }

        /// <summary>
        /// Rangeヘッダに対応した差分を検出するか否か
        /// </summary>
        public bool IsDifferenceDetect { get; set; }

        /// <summary>
        /// 5ch.net -> 2ch.netへ置換を行うか否か
        /// </summary>
        public bool Is5chURIReplace { get; set; }

        /// <summary>
        /// https -> httpへの置換を行うか否か（2ch or 5ch のリンクのみ）
        /// </summary>
        public bool IsHttpsReplace { get; set; }

        private enum CGIType
        {
            Old,
            Until202306,
            Krsw,
            Ver202306
        }

        /// <summary>
        /// 指定されたスレのHTMLをdatへ変換する
        /// </summary>
        /// <param name="URI">終端を/で終わらせること</param>
        /// <param name="range"></param>
        /// <param name="UA">使用しない。UAを受け取っていたが適切ではなかった</param>
        /// <param name="CRReplace"></param>
        /// <param name="LastMod">datを途中まで取得していた場合の最終更新日付時刻。差分検出に使用する</param>
        /// <returns></returns>
        public Byte[] Gethtml(String URI, int range, String UA, bool CRReplace, String LastMod = null)
        {
            // 現在（23/06/18）pinkはまだ新形式ではない
            //if (URI.Contains(".5ch.net/"))
            //{
            //    URI = URI.Replace("test/read.cgi/", "test/read.cgi/c/");
            //}

            System.Diagnostics.Debug.WriteLine($"{URI} をHTML変換開始");
            System.Diagnostics.Debug.WriteLine($"Range:{range}, UA:{this.UserAgent}, CRReplace:{CRReplace}, LastMod:{LastMod}");

            URI = URI.Replace("2ch.net", "5ch.net");
            URI += "1-";

            if (this.IsExternalConverterUse)
            {
                return HTMLTranceOutRegex(URI, range, this.UserAgent, LastMod);
            }
            using (WebClient get = new WebClient())
            {
                get.Headers["User-Agent"] = this.UserAgent;

                try
                {
                    if (this.ProxyAddress != "") get.Proxy = new WebProxy(this.ProxyAddress);
                    using (System.IO.StreamReader html = new System.IO.StreamReader(get.OpenRead(URI), Encoding.GetEncoding("Shift_JIS")))
                    {
                        String title = "もうずっと人大杉";

                        CGIType cgiver = CGIType.Ver202306;

                        //bool cgi_ver_202306 = false;
                        //bool cgi_until_202306 = false;
                        // krsw鯖のHTML形式の検出
                        //bool is_krsw = false;

                        string line = html.ReadLine();

                        // krsw鯖のhtmlは1行に詰まってるので、1行読むと終端に達する
                        if (html.EndOfStream == false)
                        {
                            //タイトルの検索
                            for (; !html.EndOfStream; line = html.ReadLine())
                            {
                                if (Regex.IsMatch(line, @"<title>(.+?)<\/title>"))
                                {
                                    title = Regex.Match(line, @"<title>(.+?)<\/title>").Groups[1].Value;
                                    cgiver = CGIType.Old;
                                    break;
                                }
                                else if (Regex.IsMatch(line, @"<title>(.+?)$"))
                                {
                                    title = Regex.Match(line, @"<title>(.+?)$").Groups[1].Value;

                                    // 202306以降かどうかを判定
                                    bool is_until_202306 =
                                        line.Contains("</script><title>") ||
                                        line.Contains("bootstrap.min") ||
                                        !line.Contains("ad-manager.min");

                                    if (is_until_202306)
                                    {
                                        cgiver = CGIType.Until202306;
                                    }
                                    else
                                    {
                                        cgiver = CGIType.Ver202306;
                                    }

                                    break;
                                }
                            }
                        }
                        else
                        {
                            cgiver = CGIType.Krsw;
                            title = Regex.Match(line, @"<title>(.+?)<\/title>").Groups[1].Value;
                        }

                        if (Regex.IsMatch(title, @"(５ちゃんねる error \d+|もうずっと人大杉|datが存在しません.削除されたかURL間違ってますよ)")) return new byte[] { 0 };
                        if (Regex.IsMatch(title, @"(2|5)ch\.net\s(\[\d+\])"))
                        {
                            var tmatch = Regex.Match(title, @"(2|5)ch\.net\s(\[\d+\])").Groups;
                            title = title.Replace(tmatch[0].Value, $"{tmatch[1].Value}ch.net\t {tmatch[2].Value}");
                        }
                        if (CRReplace) title = title.Replace("&#169;", "&copy;");

                        System.Diagnostics.Debug.WriteLine($"Title:{title}");

                        StringBuilder Builddat = null;
                        string ketu = "0";
                        //新CGI形式と古いCGI形式で処理を分ける

                        switch (cgiver)
                        {
                            case CGIType.Ver202306:
                                // 2023年6月ごろから導入の新HTML形式
                                System.Diagnostics.Debug.WriteLine("CGI ver202306形式");

                                // 1400行ほど飛ばす
                                for (int i = 0; i <= 1400; ++i)
                                {
                                    html.ReadLine();
                                }

                                // レス本文探索
                                for (; !html.EndOfStream; line = html.ReadLine())
                                {
                                    if (line.Contains("<article"))
                                    {
                                        break;
                                    }
                                }

                                // 先に終端に到達したらやめる
                                if (html.EndOfStream)
                                {
                                    break;
                                }

                                line += html.ReadToEnd();

                                Builddat = this.CGI202306_ConvertProcess(title, URI, line);
                                break;
                            case CGIType.Until202306:
                                // 2022年8月時点で主流のHTML形式（全5行くらいのやつ）
                                System.Diagnostics.Debug.WriteLine("新CGI形式");

                                Builddat = this.PresentCGIFormat(title, URI, html, out ketu);
                                break;
                            case CGIType.Krsw:
                                // 2022/08/05頃に観測された、krsw鯖の形式（1行に詰まってる）
                                System.Diagnostics.Debug.WriteLine("krsw鯖形式");

                                Builddat = this.krswCGIFormat(title, URI, line, out ketu);
                                break;
                            case CGIType.Old:
                                // API導入前の古い形式（1レス1行）
                                System.Diagnostics.Debug.WriteLine("旧CGI形式");

                                Builddat = this.OldCGIFormat(title, html, out ketu);
                                break;
                            default:
                                System.Diagnostics.Debug.WriteLine("未知のCGI形式");
                                break;
                        }

                        //スレッドが生存している場合
                        if (Builddat == null)
                        {
                            System.Diagnostics.Debug.WriteLine("スレ生存により変換中止");
                            return new byte[] { 0, 0 };
                        }

                        Byte[] Bdat = Encoding.GetEncoding("Shift_JIS").GetBytes((Is5chURIReplace || IsHttpsReplace) ? (this.ResContentReplace(Builddat.ToString())) : (Builddat.ToString()));

                        System.Diagnostics.Debug.WriteLine($"変換完了。Length:{Bdat.Length}");

                        //全件応答
                        if (this.IsDifferenceDetect == false || range < 0) return Bdat;

                        //HTML表示datサイズの取得
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
                        return CommonMethods.DifferenceDetection(Bdat, LastMod, UA, range, size);
                    }
                }
                catch (System.Threading.ThreadAbortException e)
                {
                    throw e;
                }
                catch (Exception err)
                {
                    System.Diagnostics.Debug.WriteLine(err.ToString());
                    CurrentError = URI + "をHTMLから変換中にエラーが発生しました。\n" + err.ToString();
                    //ViewModel.OnModelNotice(URI + "をHTMLから変換中にエラーが発生しました。\n" + err.ToString());
                    return new byte[] { 0 };
                }

            }
        }

        private StringBuilder CGI202306_ConvertProcess(string title, string URI, string allres)
        {
            // datの全体保持用
            var Builddat = new StringBuilder(510 * 1024);
            // 1レス分のhtml保持用
            var Bres = new StringBuilder(5 * 1024);
            // pinkレスずれ処理用
            bool pink = URI.Contains("bbspink.com");
            int datResnumber = 1, htmlResnumber = 0;
            long ThreadTime = long.Parse(Regex.Match(URI, @"/(\d{9,})").Groups[1].Value);

            // レスの連続抽出はざっくりとやる
            var ResMatches = Regex.Matches(allres, @"<article id=.+?</section></article>");
            // ↑で抽出した1つのレス内で各要素を抽出
            Regex ResContent = new Regex(@"<article id=.(?<num>\d+?).+?<summary>.+?<span class=.postusername.>(?<name><b>.*?</b>)</span></summary><span class=.date.>(?<date>.+?)</span><span class=.uid.>(?<id>.*?)</span>(?<be><span class=.be.+?</span>)?</details><section class=.post-content.>(?<massage>.+?)</section></article>");

            // 旧型式（API移行直後のhtml形式）の処理を再利用するために、レス部分のhtmlを1レスづつ旧型式に変換する
            // 細部のハンドリングを継承するための措置
            foreach (Match resmatch in ResMatches)
            {
                Match res_content = ResContent.Match(resmatch.Value);

                string resnumber = res_content.Groups["num"].Value;
                string name = res_content.Groups["name"].Value;
                string date = res_content.Groups["date"].Value;
                string id = res_content.Groups["id"].Value; // 無ければ空文字列
                string be = res_content.Groups["be"].Value; // 無ければ空文字列
                string message = res_content.Groups["massage"].Value;

                // いくつかのコーナーケースのハンドル処理

                // 0,NGの検出
                if (resnumber == "0" && date == "NG")
                {
                    // 飛ばす
                    continue;
                }
                // htmlでレスが飛んでいるときを検出（pinkのみ）
                if (pink && int.TryParse(resnumber, out htmlResnumber) && datResnumber < htmlResnumber)
                {
                    for (int j = htmlResnumber - datResnumber; j > 0; --j)
                    {
                        Builddat.Append("うふ～ん<>うふ～ん<>うふ～ん<>うふ～ん<>うふ～ん\n");
                    }
                    datResnumber = htmlResnumber;
                }
                // あぼーんの検出（pinkはたぶんうふ～んになる）
                if (date == "NG" && message == "あぼーん")
                {
                    // 昔はID:DELETEDになっていたらしいが今は違う
                    // datでは "あぼーん<>あぼーん<>あぼーん<>あぼーん<>あぼーん"のようになる
                    // ここで直でdat構築したほうが早そう
                    date = "あぼーん";
                    id = "";
                }
                if (res_content.Groups["be"].Success)
                {
                    // beリンクの変換
                    // <span class="be r2BP"><a href="http://be.5ch.net/user/823355746" target="_blank">?2BP(0)</a></span> これを
                    // <a href="javascript:be(823355746);">?2BP(0)</a> みたいにする

                    var mb = Regex.Match(be, @"<a href.+?(\d{2,}).+?>(.+)</span>");
                    be = $" <a href={'"'}javascript:be({mb.Groups[1].Value});{'"'}>{mb.Groups[2].Value}";

                    // 本文内のアイコンリンクは処理する必要ない
                }
                if (message.Contains("<span class="))
                {
                    // <span class="AA">を無視する
                    message = Regex.Match(message, @"^<span class=.+?>(.+?)</span>$").Groups[1].Value;
                }
                if (Regex.IsMatch(message, $@" class=.reply_link.>") == true)
                {
                    // class="reply_link"を取り除く
                    // rel="noopener noreferrer" target="_blank" の2つの属性だけが安価リンクには残る
                    message = Regex.Replace(message, @" class=.reply_link.>", ">");

                    // これ以上の安価リンク処理は必要ない
                }

                // 本文を先に追加
                Bres.Append(message);

                // p53など、レス前後にスペースが無いときに補う。
                if (!Regex.IsMatch(message, @"^\s.+\s$"))
                {
                    Bres.Insert(0, " ");
                    Bres.Append(" ");
                }

                // ↓こうなるように変換
                // <dt>{レス番} ：<b>{メールリンク}{名前}</b>：{日付} {ID}{beリンク}<dd> {本文} <br><br>
                // IDがない時でも日付の後ろにスペースは入る

                Bres.Insert(0, $"<dt>{resnumber} ：{name}：{date} {id}{be}<dd>");
                Bres.Append("<br><br>");
                // レス1つ分をdat形式へ変換
                Builddat.Append(html2dat(Bres.ToString()));

                // 1レス目の末尾にはタイトルを付加する
                if (!String.IsNullOrEmpty(title))
                {
                    Builddat.Append(title + "\n");
                    title = "";
                }
                else
                {
                    Builddat.Append("\n");
                }

                Bres.Clear();
                datResnumber++;
            }

            return Builddat;
        }

        /// <summary>
        /// 2022/8/5頃にkrsw鯖の板のスレで見られた形式のHTMLをdat変換する
        /// HTMLファイルが1行に詰まってる形式
        /// </summary>
        /// <param name="URI">スレのURL</param>
        /// <param name="html">htmlの全体</param>
        /// <param name="title">スレタイ</param>
        /// <param name="datSize"></param>
        /// <returns></returns>
        private StringBuilder krswCGIFormat(string title, string URI, string html, out string datSize)
        {
            // レス部分の始まりを見つけて、それ以前を消しておく
            int begin = Regex.Match(html, @"<d(?:iv|l) class=.(?:thread|post).+?>").Index;
            string allres = html.Substring(begin);

            return CommonConvertProcess(title, URI, allres, out datSize);
        }

        /// <summary>
        /// 現在の（2022/8くらい）形式のHTMLをdatへ変換する
        /// </summary>
        /// <param name="title"></param>
        /// <param name="URI"></param>
        /// <param name="htmlStream"></param>
        /// <param name="datSize"></param>
        /// <returns></returns>
        private StringBuilder PresentCGIFormat(string title, string URI, StreamReader htmlStream, out string datSize)
        {
            var Builddat = new StringBuilder(510 * 1024);

            String line = htmlStream.ReadLine();
            //スレッド本文探索
            do
            {
                if (Regex.IsMatch(line, @"<d(?:iv|l) class=.(?:thread|post).+?>")) break;
                line = htmlStream.ReadLine();
            } while (!htmlStream.EndOfStream);
            
            String allres = line + htmlStream.ReadToEnd();

            return CommonConvertProcess(title, URI, allres, out datSize);
        }

        private StringBuilder CommonConvertProcess(string title, string URI, string allres, out string datSize)
        {
            // datの全体保持用
            var Builddat = new StringBuilder(510 * 1024);
            // 1レス分のhtml保持用
            var Bres = new StringBuilder(5 * 1024);
            //pinkレスずれ処理用
            bool pink = URI.Contains("bbspink.com");
            int datResnumber = 1, htmlResnumber = 0;
            long ThreadTime = long.Parse(Regex.Match(URI, @"/(\d{9,})").Groups[1].Value);
            var ResMatches = Regex.Matches(allres, @"<(?:div|dl) class=.post. id=.\d.+?>(.+?(?:</div></div>|</dd></dl>))");

            // 旧型式の処理を再利用するために、レス部分のhtmlを1レスづつ旧型式に変換する
            foreach (Match Res in ResMatches)
            {
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
                String name = Regex.Match(Res.Groups[1].Value, $"<(?:div|span) class=.name.+?>(.+?(?:</b>|</a>))</(?:div|span)>").Groups[1].Value;
                //目欄が空の時フォントカラー指定を消す
                if (!name.Contains("<a href=" + '"' + "mailto:"))
                {
                    name = Regex.Replace(name, @"<font color=.\w+.>", "");
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
                if (message.Contains("<span class=") == true)
                {
                    //<span class="AA">を無視する
                    message = Regex.Match(message, @"<span class=.+?>(\s.+?\s)(?:</span>)").Groups[1].Value;
                }
                if (Regex.IsMatch(message, $@"\sclass={'"'}reply_link{'"'}>") == true)
                {
                    // class="reply_link"
                    message = Regex.Replace(message, $@"\sclass={'"'}reply_link{'"'}>", ">");
                }
                // 安価のリンク修正、http://potato.2ch.net/test/read.cgi/jisaku/1447271149/9→../test/read.cgi/jisaku/1447271149/9
                // どうやら現在はこれ治ってるらしく、一時的なものだった模様
                Bres.Append(message);
                foreach (Match item in Regex.Matches(message, @"(<a href=.)(?:https?:)?//\w+\.((?:2|5)ch\.net|bbspink\.com)(/test/read.cgi/\w+/\d+/\d{1,4}.\s.+?>&gt;&gt;\d{1,5}</a>)"))
                {
                    // こういう-> <a href="../test/read.cgi/software/1458275801/1" rel="noopener noreferrer" target="_blank">&gt;&gt;1</a>
                    // 形式にしたい（rel="noopener noreferrer" target="_blank"の2つの属性はdatにもある、それ以外はない）
                    Bres.Replace(item.Groups[0].Value, item.Groups[1].Value + ".." + item.Groups[3].Value);
                }
                //お絵かきリンク修正
                //foreach (Match item in Regex.Matches(message, @"<a href=" + '"' + @"(?:https?:)?//jump.(?:2|5)ch.net/\?(https?://[a-zA-Z\d]+?\.8ch.net\/.+?\.\w+?)" + '"' + @">https?://[a-zA-Z\d]+?\.8ch.net\/.+?\.\w+?</a>"))
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
                // レス1つ分をdat形式へ変換
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
            datSize = Regex.Match(allres, @"<(?:div|li) class=.+?>(?<datsize>\d+?)KB</(?:div|li)>").Groups[1].Value;

            return Builddat;
        }

        /// <summary>
        /// 古い（API導入前の）形式のHTMLをdatへ変換する
        /// </summary>
        /// <param name="title">スレタイ</param>
        /// <param name="htmlStream">レス本文を含むHTMLの残りの部分</param>
        /// <param name="datSize">末尾についてるdatサイズ情報を返す</param>
        /// <returns></returns>
        private StringBuilder OldCGIFormat(string title, StreamReader htmlStream, out string datSize)
        {
            var Builddat = new StringBuilder(510 * 1024);

            if (this.IsAliveCheckSkip == false)
            {
                bool alive = true;
                //dat落ちかチェック
                for (String line = htmlStream.ReadLine(); !htmlStream.EndOfStream; line = htmlStream.ReadLine())
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
                if (alive)
                {
                    datSize = "";
                    return null;
                }
            }
            String ResHtml = htmlStream.ReadToEnd();
            System.Collections.Concurrent.ConcurrentDictionary<int, string> Trancedat = new System.Collections.Concurrent.ConcurrentDictionary<int, string>(4, 1005);
            System.Threading.Tasks.ParallelOptions option = new System.Threading.Tasks.ParallelOptions();
            option.MaxDegreeOfParallelism = 4;
            System.Threading.Tasks.Parallel.ForEach<Match>(Regex.Matches(ResHtml, @"<dt>(\d{1,4})\s：.+?<br><br>(?:\r|\n)").Cast<Match>(), option, match =>
            {
                Trancedat[int.Parse(match.Groups[1].Value) - 1] = html2dat(match.Groups[0].Value) + "\n";
            });
            Builddat.Append(Trancedat[0].Substring(0, Trancedat[0].Length - 1) + title + "\n");
            for (int i = 1; i < Trancedat.Count; ++i) Builddat.Append(Trancedat[i]);

            datSize = Regex.Match(ResHtml, @"<font\scolor.+?><b>(\d+)\sKB<\/b><\/font>").Groups[1].Value;

            return Builddat;
        }

        public String Compile(String SourceFilePath)
        {
            return CommonMethods.CompileConverterFromSource(SourceFilePath, ref m_CompiledAssembly);
        }

        public String TestExternalConverter(String URI)
        {
            if (m_CompiledAssembly == null) return "コンパイルが行われていません";
            return Encoding.GetEncoding("Shift_JIS").GetString(HTMLTranceOutRegex(URI, -1, UserAgent));
        }

        private Byte[] HTMLTranceOutRegex(String URI, int range, String UA, String LastMod = null)
        {
            if (m_CompiledAssembly == null)
            {
                //ViewModel.OnModelNotice("外部HTMLtoDatコードのコンパイルが行われていません");
                CurrentError = "外部HTMLtoDatコードのコンパイルが行われていません";
                return new byte[] { 0 };
            }
            Type t = m_CompiledAssembly.GetType("HtmlToDatConverter", false, false);
            using (WebClient get = new WebClient())
            {
                get.Headers["User-Agent"] = this.UserAgent;
                try
                {
                    String dat = "", ketu = "";
                    if (this.ProxyAddress != "") get.Proxy = new WebProxy(this.ProxyAddress);
                    using (System.IO.StreamReader html = new System.IO.StreamReader(get.OpenRead(URI), Encoding.GetEncoding("Shift_JIS")))
                    {
                        String thredhtml = html.ReadToEnd();
                        if (t != null) dat = (String)t.InvokeMember("HTMLConvert", BindingFlags.InvokeMethod, null, null, new object[] { thredhtml });
                        ketu = Regex.Match(thredhtml, @"<div class=.cLength.>(\d+)KB</div>").Groups[1].Value;
                    }
                    if (this.Is5chURIReplace || this.IsHttpsReplace)
                    {
                        dat = this.ResContentReplace(dat);
                    }
                    Byte[] Bdat = Encoding.GetEncoding("Shift_JIS").GetBytes(dat);
                    if (this.IsDifferenceDetect == true || range < 0) return Bdat;
                    int size;
                    try
                    {
                        size = int.Parse(ketu);
                    }
                    catch (FormatException)
                    {
                        size = 0;
                    }
                    return CommonMethods.DifferenceDetection(Bdat, LastMod, UA, range, size);
                }
                catch (System.Threading.ThreadAbortException e)
                {
                    throw e;
                }
                catch (Exception err)
                {
                    //ViewModel.OnModelNotice(URI + "をHTMLから変換中にエラーが発生しました。\n" + err.ToString());
                    CurrentError = URI + "をHTMLから変換中にエラーが発生しました。\n" + err.ToString();
                    System.Diagnostics.Debug.WriteLine(err.ToString());
                    return new byte[] { 0 };
                }
            }
        }


        /// <summary>
        /// HTMLの1レス分をdat一行分へ変換する（旧cgi形式のHTMLを前提としている）
        /// </summary>
        /// <param name="res">1レス分のHTML文字列（旧cgi形式）</param>
        /// <returns>変換されたdat一行</returns>
        private static String html2dat(String res)
        {
            // ここでの入力は1レス1行の、次のようなもの
            // API導入以前のHTML形式
            // <dt>126 ：<font color="green"><b>名無し募集中。。。</b></font>：2019/04/22(月) 00:42:21.47 0<dd> <a href="../test/read.cgi/morningcoffee/1555849244/67" rel="noopener noreferrer" target="_blank">&gt;&gt;67</a> <br> 待ってました！ <br><br>
            // <dt>138 ：<a href="mailto:sage"><b>名無し募集中。。。</b></a>：2019/04/22(月) 01:25:43.27 0<dd> こりゃ陸上競技見に行かなあかんな <br><br>
            // <dt>147 ：<font color="green"><b>名無し募集中。。。</b></font>：2019/04/22(月) 08:44:08.35 0<dd> 良スレ＝基地外がいないマターリスレになるからね <br><br>

            var BuildDat = new StringBuilder(res.Length * 2);
            String temp;
            bool be = false;

            //名前抽出
            temp = Regex.Match(res, @"<dt>\d{1,5}\s：(.+)<dd>\s?").Value;
            BuildDat.Append(Regex.Match(temp, $"<b>(?:<a href=(?:{'"'}.*?{'"'}|'.*?'|[^'{'"'}])+?>(?<name>.+?)</a>|(?<name>.+))</b>").Groups["name"].Value + "<>");
            
            //目蘭抽出
            BuildDat.Append(Regex.Match(temp, $"<a href={'"'}mailto:((?:{'"'}.*?{'"'}|'.*?'|[^'{'"'}])+?){'"'}>").Groups[1].Value + "<>");
            
            //あぼ～ん時の処理
            if (Regex.IsMatch(temp, @">：(.+\sID:DELETED)"))
            {
                // 最終的なdatは"あぼ～ん<>あぼ～ん<>あぼ～ん ID:DELETED<>あぼ～ん<>あぼ～ん<>"のようになる（末尾足りてない？）
                BuildDat.Append(Regex.Match(temp, @">：(.+\sID:DELETED)").Groups[1].Value + "<>");
                goto honbun;
            }
            //あぼ～ん時の処理2、ID:DELETEDではない現在（2023/06頃）の形式（pinkはどうなる？
            if (Regex.IsMatch(temp, @">：あぼーん "))
            {
                // 最終的なdatは"あぼーん<>あぼーん<>あぼーん<>あぼーん<>あぼーん"のようになる（末尾1つ多い）
                BuildDat.Append("あぼーん<>あぼーん<>あぼーん");
                goto skip_abone;
            }

            //投稿日時+ID抽出
            var DateID = Regex.Match(temp, @"：(?:(?<date>\d{4}\/.+ID:.+)\s<a\s|(?<date>\d{4}\/.+ID:.+)<dd>|(?<date>\d{4}\/.+?)<dd>\s<a\s|(?<date>\d{4}\/.+?)\s<a\s|(?<date>\d{4}\/.+)<dd>)").Groups["date"].Value;
            BuildDat.Append(DateID);
            
            //1001時の処理
            if (String.IsNullOrEmpty(DateID))
            {
                BuildDat.Append(Regex.Match(temp, @">：(.+)<dd>").Groups[1].Value + "<>");
                goto honbun;
            }

            //Be抽出
            //if (Regex.IsMatch(temp, @"javascript:be"))
            if (temp.Contains(@"javascript:be") == true)
            {
                var group = Regex.Match(temp, @"(\s<a href=" + '"' + ".+?" + '"' + @">.+?<\/a>\s)?<a href=" + '"' + @"javascript:be\((\d+)\).{4}(.+\(\d+\).*|.+)<\/a>").Groups;
                if (group[1].Value != "") BuildDat.Append(" ");
                BuildDat.Append(group[1].Value + " BE:" + group[2].Value + "-" + group[3].Value);

                be = true;
            }
            BuildDat.Append("<>");

            honbun:     //本文取得
            var ResBody = new StringBuilder(5 * 1024);
            temp = Regex.Match(res, @".+<dd>(\s.+\s|.{2}～ん)<br><br>$").Groups[1].Value;
            if (String.IsNullOrEmpty(temp))
            {
                //特殊処理 : https://choco.5ch.net/test/read.cgi/download/978600696/
                //本文先頭にスペースが無い、末尾にスペースがあったりなかったり
                const string Pattern = @".+<dd>(.+)<br><br>$";
                if (Regex.IsMatch(res, Pattern))
                {
                    //スペースを補うのは後続の処理のため
                    //この時代にBeアイコンや絵文字リンクが無いならいらない？
                    temp = " " + Regex.Match(res, Pattern).Groups[1].Value;
                    ResBody.Append(temp);
                }
                else
                {
                    ResBody.Append(Regex.Match(res, @".+やっと出た<dd>(.+?)<br><br>$").Groups[1].Value);
                }
            }
            else
            {
                ResBody.Append(temp);
            }

            //リンクをURL文字列に戻す、http://のみのリンクに対応するため、.*?にする。.+?だと正しく抽出されない。
            //var t = Regex.Matches(temp, @"<a\shref=.(?:https?:)?\/\/.+?>(https?:\/\/.*?)<\/a>");
            var t = Regex.Matches(temp, $@"<a\s(?:class={'"'}image{'"'}\s)?href=.(?:https?:)?\/\/.+?>(https?:\/\/.*?)<\/a>");
            foreach (Match m in t)
            {
                ResBody.Replace(m.Groups[0].Value, m.Groups[1].Value);
            }
            t = Regex.Matches(temp, @"<a\shref=.ftp:\/\/.+?>(ftp:\/\/.*?)<\/a>");
            foreach (Match m in t)
            {
                ResBody.Replace(m.Groups[0].Value, m.Groups[1].Value);
            }

            //お絵カキコリンクを処理
            var oekakiko = Regex.Matches(temp, @"<img\ssrc=" + '"' + @"(?:https?:)?(\/\/[a-zA-Z\d]+?\.8ch\.net\/\w+?\.\w+?)" + '"' + ">");
            foreach (Match m in oekakiko)
            {
                ResBody.Replace(m.Groups[0].Value, "sssp:" + m.Groups[1].Value);
            }
            
            //Beアイコン、絵文字リンクの成型http:→sssp:
            //先頭行のリンク処理
            if (Regex.IsMatch(temp, @"^\s(<img src=.(?:https?:)?(\/\/img\.(?:2|5)ch\.net.+?).>)(?:(\s)<br>)?"))
            {
                var mae = Regex.Match(temp, @"^\s(<img src=.(?:https?:)?(\/\/img\.(?:2|5)ch\.net.+?).>)(?:(\s)<br>)?").Groups;
                //if (Regex.IsMatch(BuildDat.ToString(), @"BE:\d+"))
                if (be == true)
                {
                    ResBody.Replace(mae[1].Value, "sssp:" + mae[2].Value);
                }
                else
                {
                    // ssspの前にスペース入ってるのはバグではないのか？？
                    ResBody.Replace(mae[1].Value, " sssp:" + mae[2].Value + mae[3].Value);
                }
            }

            //その他本文中リンク処理
            t = Regex.Matches(temp, @"(?:<br>(\s))?(<img src=.(?:https?:)?(\/\/img\.(?:2|5)ch\.net.+?).>)(?:(\s)<br>)?");
            foreach (Match m in t)
            {
                ResBody.Replace(m.Groups[2].Value, m.Groups[1].Value + "sssp:" + m.Groups[3].Value + m.Groups[4].Value);
            }
            
            //本文末に存在する場合にスペース付加
            if (Regex.IsMatch(temp, @"sssp:\/\/img\.(?:2|5)ch\.net\/\w+\/[^/]+\.\w{3,5}$")) ResBody.Append(" ");
            BuildDat.Append(ResBody);
            BuildDat.Append("<>");
            
            //<br>が文末or文頭にある時にスペースを補う
            BuildDat.Replace("<br> <>", "<br>  <>");
            BuildDat.Replace("<br> <>", "<br>  <>");

            skip_abone: // あぼーん時のスキップ

            //<br>の連続時にスペースを補う
            BuildDat.Replace("<br> <br>", "<br>  <br>");
            return BuildDat.Replace("<br> <br>", "<br>  <br>").ToString();
        }

        /// <summary>
        /// CommonMethods.ResContentReplaceを呼び出す。変数を束縛し扱いやすくしただけ
        /// </summary>
        /// <param name="dat">dat文字列</param>
        /// <returns></returns>
        public String ResContentReplace(String dat)
        {
            return CommonMethods.ResContentReplace(dat, this.Is5chURIReplace, this.IsHttpsReplace);
        }


        //プロパティ名毎のPropertyChangedEventArgsをキャッシュする
        ConcurrentDictionary<string, PropertyChangedEventArgs> PropertyChangedEventArgsCache = new System.Collections.Concurrent.ConcurrentDictionary<string, PropertyChangedEventArgs>(4, 10);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string PropertyName)
        {
            //キャッシュにない時だけEventArgsを作成
            if (!PropertyChangedEventArgsCache.ContainsKey(PropertyName)) PropertyChangedEventArgsCache[PropertyName] = new PropertyChangedEventArgs(PropertyName);
            this.PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache[PropertyName]);
        }

        protected bool SetProperty<T>(ref T StorageMember, T Value, String PropertyName)
        {
            if (object.Equals(StorageMember, Value)) return false;
            StorageMember = Value;
            this.NotifyPropertyChanged(PropertyName);
            return true;
        }

    }
}
