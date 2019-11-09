using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;
using Fiddler;


namespace _2chAPIProxy.Models
{
    class IPAuthData
    {
        public bool Auth { get; set; } = false;
        public String Nonce { get; set; } = "";
    }

    public class ProxyServer : ViewModels.VMBase
    {
        Dictionary<String, String> m_Cookie = new Dictionary<string, string>();
        Dictionary<String, IPAuthData> m_AuthIPList = new Dictionary<string, IPAuthData>();
        Regex m_Check2churi = new Regex(@"^(\w+?)\.((?:2|5)ch\.net|bbspink\.com)$", RegexOptions.Compiled);
        Regex m_CheckDaturi = new Regex(@"^https?:\/\/(\w+?)\.((?:2|5)ch\.net|bbspink\.com)\/(\w+?)\/dat\/(\d+?)\.dat", RegexOptions.Compiled);
        Regex m_CheckWriteuri = new Regex(@"^https?:\/\/\w+?(\.(?:2|5)ch.net|\.bbspink.com)(?::\d{2,})?\/test\/(?:sub)?bbs\.cgi", RegexOptions.Compiled);
        Regex m_CheckKakouri = new Regex(@"(^https?:\/\/rokka\.((?:2|5)ch\.net|bbspink\.com)\/(\w+?)\/(\w+?)\/(\d+?)\/.+|http:\/\/\w+?.(2|5)ch\.net\/test\/offlaw2\.so.+)", RegexOptions.Compiled);
        Regex m_CheckKakouri2 = new Regex(@"^https?:\/\/((?:\w+?)\.(?:(?:2|5)ch\.net|bbspink\.com))\/(\w+?)\/kako\/(?:\d{4}\/\d{5}|\d{3})\/(\d+?)\.dat", RegexOptions.Compiled);
        Regex m_CheckOldBe = new Regex(@"^https?://(?:be.(?:2|5)ch.net/(?:test/)?(login|index).php)", RegexOptions.Compiled);
        Regex m_CheckShitaraba = new Regex(@"^http://jbbs.(shitaraba.net|livedoor.jp)", RegexOptions.Compiled);
        Regex m_CheckShitarabaPost = new Regex(@"^https?://jbbs.(shitaraba.net|livedoor.jp)/.+?/write.cgi", RegexOptions.Compiled);
        Regex m_CheckItauri = new Regex(@"^https?://\w+?\.((?:2|5)ch\.net|bbspink\.com)/\w+(/?$|/subject.txt$)", RegexOptions.Compiled);
        Regex m_BBSMenuReplace = new Regex(@"^https?://menu\.(?:2|5)ch\.net/bbsmenu.html", RegexOptions.Compiled);
        volatile bool m_SIDNowUpdate = false;

        public APIAccess APIAccessor { get; private set; }
        public String Proxy { get; set; }
        public String WriteUA { get; set; }
        public String NormalUA { get; set; }
        public String WANUserName { get; set; }
        public String WANPW { get; set; }
        public bool GetHTML { get; set; }
        public bool AllowWANAccese { get; set; }
        public bool CangeUARetry { get; set; }
        public bool OfflawRokkaPerm { get; set; }
        public bool GZipRes { get; set; }
        public bool ChunkRes { get; set; }
        public bool SocksPoxy { get; set; }
        public bool OnlyORPerm { get; set; }
        public bool CRReplace { get; set; }
        public bool KakolinkPerm { get; set; }
        public bool AllUAReplace { get; set; }
        public bool BeLogin { get; set; }

        public ProxyServer(String Appkey, String HMkey, String UA1, String sidUA, String UA2, String RouninID, String RouninPW, String ProxyAddress)
        {
            System.Threading.Timer GetSid = null;
            GetSid = new System.Threading.Timer((e) =>
            {
                using (GetSid)
                {
                    this.APIAccessor = new APIAccess(Appkey, HMkey, UA1, sidUA, UA2, ProxyAddress);                    
                }
            }, null, 0, System.Threading.Timeout.Infinite);
            //Fiddler設定変更
            Fiddler.CONFIG.bReuseClientSockets = true;
            Fiddler.CONFIG.bReuseServerSockets = true;
            //正規表現キャッシュサイズ
            Regex.CacheSize = 75;
            GetHTML = true;
            AllowWANAccese = false;

        }


        private void BBSMenuURLReplace(ref Session oSession, bool is2ch)
        {
            if (ViewModel.Setting.UseTLSWrite) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
            oSession.bBufferResponse = true;
            SessionStateHandler MRHandler = null;
            MRHandler = (ooSession) =>
            {
                FiddlerApplication.BeforeResponse -= MRHandler;
                var html = ooSession.GetResponseBodyAsString();
                var ItaMatches = Regex.Matches(html, $@"<(?:A HREF|a href)={'"'}(?:https?:)?(//\w+?\.(?:2ch\.net|5ch\.net|bbspink\.com)/\w+/?){'"'}>(.+)</(?:A|a)>");
                foreach (Match ita in ItaMatches)
                {
                    String replace = $"<A HREF=http:{ita.Groups[1].Value}>{ita.Groups[2].Value}</A>";
                    html.Replace(ita.Value, replace);
                }
                html = Regex.Replace(html, $@"<(?:A HREF|a href)={'"'}(?:https?:)?(//.+?){'"'}>(.+?)</(?:A|a)>", "<A HREF=http:$1>$2</A>");
                if (is2ch) html = html.Replace(".5ch.net/", ".2ch.net/");
                ooSession.ResponseBody = Encoding.GetEncoding("shift_jis").GetBytes(html);
            };
            FiddlerApplication.BeforeResponse += MRHandler;
            if (AllUAReplace) oSession.oRequest.headers["User-Agent"] = WriteUA;
            return;
        }

        private void Replacehttps(ref Session oSession, bool is2ch)
        {
            if (ViewModel.Setting.UseTLSWrite) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
            oSession.bBufferResponse = true;
            SessionStateHandler BRHandler = null;
            BRHandler = (ooSession) =>
            {
                FiddlerApplication.BeforeResponse -= BRHandler;
                String itenuri = ooSession.GetResponseBodyAsString();
                if (ooSession.responseCode == 301)
                {
                    ooSession.oResponse.headers.SetStatus(302, "302 Found");
                    if (ooSession.fullUrl.Contains("subject.txt")) ooSession.oResponse.headers["Location"] = "http://www2.2ch.net/live.html";
                }
                if (itenuri.Contains("Change your bookmark") || itenuri.Contains("The document has moved"))
                {
                    itenuri = itenuri.Replace($"{'"'}//", $"{'"'}http://");
                    itenuri = itenuri.Replace("https://", "http://");
                    if (is2ch) itenuri = itenuri.Replace(".5ch.net/", ".2ch.net/");
                    ooSession.ResponseBody = Encoding.GetEncoding("shift_jis").GetBytes(itenuri);
                }
                if (!String.IsNullOrEmpty(ooSession.oResponse.headers["Location"]))
                {
                    string locate = ooSession.oResponse.headers["Location"].Replace("https://", "http://");
                    if (is2ch) locate = locate.Replace(".5ch.net/", ".2ch.net/");
                    ooSession.oResponse.headers["Location"] = locate;
                }
            };
            FiddlerApplication.BeforeResponse += BRHandler;
            if (AllUAReplace) oSession.oRequest.headers["User-Agent"] = WriteUA;
        }

        bool WANAcceseAuth(ref Session oSession)
        {
            try
            {
                //接続してきたアドレスは認証済みかチェック
                if (!m_AuthIPList.ContainsKey(oSession.clientIP) || !m_AuthIPList[oSession.clientIP].Auth)
                {
                    //認証されていない時
                    if (m_AuthIPList.ContainsKey(oSession.clientIP))
                    {
                        //帰ってきたMD5をチェックする
                        String Res = oSession.oRequest.headers["Authorization"];
                        String /*nonce = Regex.Match(Res, @".*(?<!c)nonce=" + '"' + "(.+?)" + '"').Groups[1].Value,*/
                            uri = Regex.Match(Res, @".*uri=" + '"' + "(.+?)" + '"').Groups[1].Value,
                            nc = Regex.Match(Res, @".*nc=(.+?),").Groups[1].Value,
                            cnonce = Regex.Match(Res, @".*cnonce=" + '"' + "(.+?)" + '"').Groups[1].Value,
                            resp = Regex.Match(Res, @".*response=" + '"' + "(.+?)" + '"').Groups[1].Value;
                        String method = oSession.RequestMethod;
                        String A1 = CMD5(WANUserName + ":2chAPIProxy Auth:" + WANPW);
                        String A2 = CMD5(method + ":" + uri);
                        String hash = CMD5(A1 + ":" + m_AuthIPList[oSession.clientIP].Nonce + ":" + nc + ":" + cnonce + ":" + "auth" + ":" + A2);
                        if (hash == resp)
                        {
                            ViewModel.OnModelNotice(oSession.clientIP + "を認証");
                            m_AuthIPList[oSession.clientIP].Nonce = "";
                            m_AuthIPList[oSession.clientIP].Auth = true;
                            if (uri != "/" && uri != "/?") return true;
                            oSession.utilCreateResponseAndBypassServer();
                            oSession.oResponse.headers.HTTPResponseCode = 200;
                            oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                            oSession.oResponse.headers["Date"] = DateTime.Now.ToString("R");
                            oSession.oResponse.headers["Server"] = "2chAPIProxy";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=UTF-8";
                            oSession.utilSetResponseBody("<!DOCTYPE html><html><body>" + DateTime.Now.ToString("F") + "<br>" + oSession.clientIP + "を登録<br>2chAPIProxy再起動まで有効です</body></html>");
                            return true;
                        }
                    }
                    //未登録
                    ViewModel.OnModelNotice(oSession.clientIP + "から接続されました");
                    m_AuthIPList[oSession.clientIP] = new IPAuthData();
                    m_AuthIPList[oSession.clientIP].Auth = false;
                    m_AuthIPList[oSession.clientIP].Nonce = System.Web.Security.Membership.GeneratePassword(24, 0);
                    oSession.utilCreateResponseAndBypassServer();
                    oSession.oResponse.headers.HTTPResponseCode = 401;
                    oSession.oResponse.headers.HTTPResponseStatus = "401 Authorization Required";
                    oSession.oResponse.headers["Date"] = DateTime.Now.ToString("R");
                    oSession.oResponse.headers["Server"] = "2chAPIProxy";
                    oSession.oResponse.headers["WWW-Authenticate"] = "Digest realm=" + '"' + "2chAPIProxy Auth" + '"' + ", nonce=" + '"' + m_AuthIPList[oSession.clientIP].Nonce + '"' + ", algorithm=MD5, qop=" + '"' + "auth" + '"';
                    oSession.oResponse.headers["Connection"] = "close";
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Cache-Control"] = "no-cache";
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                ViewModel.OnModelNotice(oSession.clientIP + "から接続されました");
                oSession.utilCreateResponseAndBypassServer();
                m_AuthIPList[oSession.clientIP] = new IPAuthData();
                m_AuthIPList[oSession.clientIP].Auth = false;
                m_AuthIPList[oSession.clientIP].Nonce = System.Web.Security.Membership.GeneratePassword(24, 0);
                oSession.oResponse.headers.SetStatus(401, "401 Authorization Required"); oSession.oResponse.headers["Date"] = DateTime.Now.ToString("R");
                oSession.oResponse.headers["Server"] = "2chAPIProxy";
                oSession.oResponse.headers["WWW-Authenticate"] = "Digest realm=" + '"' + "2chAPIProxy Auth" + '"' + ", nonce=" + '"' + m_AuthIPList[oSession.clientIP].Nonce + '"' + ", algorithm=MD5, qop=" + '"' + "auth" + '"';
                oSession.oResponse.headers["Connection"] = "close";
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                oSession.oResponse.headers["Cache-Control"] = "no-cache";
                //AuthIP[oSession.clientIP] = false;
                return false;
            }
        }

        private bool OtherLinkHTMLTrance(ref Session oSession)
        {
            try
            {
                oSession.fullUrl = oSession.fullUrl.Replace(".5ch.net/", ".2ch.net/");
                System.Threading.Thread HtmlTranceThread = null;
                bool offlowperm;
                String err = "";
                Byte[] Htmldat = null;

                if (OfflawRokkaPerm && m_CheckKakouri.IsMatch(oSession.fullUrl))
                {
                    //offlow2,rokkaへのアクセスをバイパスする
                    offlowperm = true;
                    oSession.utilCreateResponseAndBypassServer();
                    String URI = oSession.fullUrl;
                    String Host = oSession.oRequest.headers["Host"].Replace(".5ch.net", ".2ch.net");
                    String Referer = oSession.oRequest.headers["Referer"].Replace(".5ch.net", ".2ch.net"); ;
                    HtmlTranceThread = new System.Threading.Thread(() =>
                    {
                        String ThreadURI;
                        try
                        {
                            if (URI.Contains("offlaw2"))
                            {
                                ThreadURI = Referer;
                                if (ThreadURI.IndexOf(@"2ch.net/test/read.cgi/") < 0)
                                {
                                    String
                                        sever = Host,
                                        ita = Regex.Match(URI, @"&bbs=(.\w+?)&").Groups[1].Value,
                                        key = Regex.Match(URI, @"&key=(.\d+)").Groups[1].Value;
                                    ThreadURI = @"http://" + sever + @"/test/read.cgi/" + ita + @"/" + key + @"/";
                                }
                            }
                            else
                            {
                                err = "Success Archive\n";
                                var group = m_CheckKakouri.Match(URI).Groups;
                                ThreadURI = @"http://" + group[3].Value + "." + group[2].Value + @"/test/read.cgi/" + group[4].Value + @"/" + group[5].Value + @"/";
                            }
                            Htmldat = HTMLtoDat.Gethtml(ThreadURI, -1, "", CRReplace);
                        }
                        catch (System.Threading.ThreadAbortException)
                        {
                            ViewModel.OnModelNotice("タイムアウトによりHTML変換スレッドを中断。\nURI:" + URI);
                        }
                    });
                }
                else if (KakolinkPerm && !OnlyORPerm && m_CheckKakouri2.IsMatch(oSession.fullUrl))
                {
                    //kakoリンクのHTML変換応答置換
                    offlowperm = false;
                    oSession.utilCreateResponseAndBypassServer();
                    String URI = oSession.fullUrl;
                    HtmlTranceThread = new System.Threading.Thread(() =>
                    {
                        try
                        {
                            var group = m_CheckKakouri2.Match(URI).Groups;
                            String ThreadURI = "http://" + group[1].Value + "/test/read.cgi/" + group[2].Value + "/" + group[3].Value + "/";
                            Htmldat = HTMLtoDat.Gethtml(ThreadURI, -1, "", CRReplace);
                        }
                        catch (System.Threading.ThreadAbortException)
                        {
                            ViewModel.OnModelNotice("タイムアウトによりHTML変換スレッドを中断。\nURI:" + URI);
                        }
                    });
                }
                else return false;

                HtmlTranceThread.IsBackground = true;
                HtmlTranceThread.Start();
                if (HtmlTranceThread.Join(30 * 1000))
                {
                    if (offlowperm)
                    {
                        //offlaw2変換時
                        if (Htmldat.Length > 2)
                        {
                            ViewModel.OnModelNotice(oSession.fullUrl + " をhtmlから変換");
                            oSession.oResponse.headers.HTTPResponseCode = 200;
                            oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                            byte[] Res;
                            if (err != "") Res = Encoding.GetEncoding("shift_jis").GetBytes(err).Concat(Htmldat).ToArray();
                            else Res = Htmldat;
                            oSession.ResponseBody = Res;
                            if (ChunkRes) oSession.utilChunkResponse(3);
                        }
                        else
                        {
                            if (err.IndexOf("Success") > -1) err = "Error 13\n";
                            else err = "ERROR ret=2001 OL2ERROR##### dat()[.dat]";
                            oSession.oResponse.headers.SetStatus(302, "302 Found");
                            oSession.ResponseBody = Encoding.GetEncoding("shift_jis").GetBytes(err);
                        }
                    }
                    else
                    {
                        //kakoリンク変換時
                        if (Htmldat.Length > 2)
                        {
                            ViewModel.OnModelNotice(oSession.fullUrl + " をhtmlから変換");
                            oSession.oResponse.headers.SetStatus(200, "200 OK");
                            oSession.ResponseBody = Htmldat;
                        }
                        else oSession.oResponse.headers.SetStatus(302, "302 Found");
                    }
                    oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                    oSession.oResponse.headers["Server"] = "2chAPIProxy";
                    oSession.oResponse.headers["Vary"] = "Accept-Encoding";
                    oSession.oResponse.headers["Connection"] = "close";
                    oSession.oResponse.headers["Content-Type"] = "text/plain";
                    if (GZipRes) oSession.utilGZIPResponse();
                }
                else
                {
                    //変換が終わらなかった場合
                    HtmlTranceThread.Abort();
                    oSession.oResponse.headers.SetStatus(302, "302 Found");
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                    oSession.oResponse.headers["Connection"] = "close";
                }
            }
            catch (Exception err)
            {
                oSession.oResponse.headers.SetStatus(302, "302 Found");
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                oSession.oResponse.headers["Connection"] = "close";
                ViewModel.OnModelNotice("offlow2/rokka/過去ログ倉庫へのアクセス置換部でエラーです。\n" + err.ToString());
            }
            return true;
        }

        void Be21Login(Session oSession, bool is2ch)
        {
            if (!BeLogin)
            {
                //Live2ch、Beログイン中のままになる対策
                if (oSession.oRequest.headers["User-Agent"].IndexOf("Live2ch") < 0)
                {
                    oSession.Ignore();
                    return;
                }
                SessionStateHandler BRHandler = null;
                BRHandler = (ooSession) =>
                {
                    //レスポンスヘッダにConnection:Closeを明示し、接続を切る
                    FiddlerApplication.BeforeResponse -= BRHandler;
                    ooSession.oResponse.headers["Connection"] = "Close";
                };
                FiddlerApplication.BeforeResponse += BRHandler;
                if (AllUAReplace) oSession.oRequest.headers["User-Agent"] = WriteUA;
                //レスポンス時に捕まえる必要があるため今は何もしない
                return;
            }
            //beの時、ログインセッション代行処理
            oSession.utilCreateResponseAndBypassServer();
            HttpWebRequest BeLoginReq = (HttpWebRequest)WebRequest.Create("https://be.5ch.net/log");
            BeLoginReq.Method = "POST";
            BeLoginReq.UserAgent = ViewModel.Setting.UserAgent4;
            if (Proxy != "") BeLoginReq.Proxy = new WebProxy(Proxy);
            BeLoginReq.Accept = "text/html";
            BeLoginReq.Referer = "https://be.5ch.net/";
            BeLoginReq.ContentType = "application/x-www-form-urlencoded";
            BeLoginReq.ServicePoint.Expect100Continue = false;
            BeLoginReq.CookieContainer = new CookieContainer();
            BeLoginReq.Host = "be.5ch.net";
            String reqdata = oSession.GetRequestBodyAsString();
            Byte[] PostData;
            if (Regex.IsMatch(reqdata, @"^mail=.+?@.+?&pass=.+?&login=$"))
            {
                PostData = oSession.requestBodyBytes;
            }
            else
            {
                String mail, pass;
                mail = Regex.Match(reqdata, @"(?:m|mail)=(.+?(?:@|%40).+?)(?:&|$)").Groups[1].Value;
                pass = Regex.Match(reqdata, @"(?:p|pass)=(.+?)(?:&|$)").Groups[1].Value;
                //var m = Regex.Match(reqdata, @"m=(.+?(?:@|%40).+?)&p=(.+?)(?:$|&.+$)").Groups;
                PostData = Encoding.ASCII.GetBytes("mail=" + mail + "&pass=" + pass + "&login=");
            }
            try
            {
                using (System.IO.Stream PostStream = BeLoginReq.GetRequestStream())
                {
                    PostStream.Write(PostData, 0, PostData.Length);
                    HttpWebResponse wres;
                    try
                    {
                        wres = (HttpWebResponse)BeLoginReq.GetResponse();
                    }
                    catch (WebException err)
                    {
                        wres = (HttpWebResponse)err.Response;
                    }
                    if (wres.Cookies.Count > 0)
                    {
                        var cul = new System.Globalization.CultureInfo("en-US");
                        string domain = (is2ch) ? (".2ch.net") : (".5ch.net");
                        foreach (Cookie cookie in wres.Cookies)
                        {
                            String tc = cookie.ToString();
                            tc += "; domain=" + domain;
                            if (cookie.Expires != null) tc += "; expires=" + cookie.Expires.ToUniversalTime().ToString("ddd, dd-MMM-yyyy HH:mm:ss", cul) + " GMT";
                            if (!String.IsNullOrEmpty(cookie.Path)) tc += "; path=" + cookie.Path;
                            oSession.oResponse.headers.Add("Set-Cookie", tc);
                        }
                    }
                    if ((int)wres.StatusCode == 200 && m_CheckOldBe.Match(oSession.fullUrl).Groups[1].Value == "index")
                    {
                        oSession.oResponse.headers.SetStatus(302, "Found");
                        oSession.oResponse.headers["Location"] = "http://be.2ch.net/status";
                    }
                    else oSession.oResponse.headers.SetStatus((int)wres.StatusCode, wres.StatusDescription);
                    oSession.oResponse.headers["Date"] = wres.Headers[HttpResponseHeader.Date];
                    oSession.oResponse.headers["Content-Type"] = wres.Headers[HttpResponseHeader.ContentType];
                    oSession.oResponse.headers["Connection"] = "close";
                    if (wres != null) wres.Close();
                    return;
                }
            }
            catch (Exception err)
            {
                ViewModel.OnModelNotice("Beログイン中にエラーが発生しました。\n" + err.ToString());
            }
        }

        private static void ShitarabaPost(Session oSession)
        {
            //したらば書き込み時
            if (!Regex.IsMatch(oSession.fullUrl, @"^https?://jbbs.(shitaraba.net|livedoor.jp)/bbs/write.cgi/\w+/\d+/\d+"))
            {
                //正規のURLでないとき
                try
                {
                    //書き込み置換処理、URL変更とデータの組直し
                    String oBody = HttpUtility.UrlDecode(oSession.GetRequestBodyAsString(), Encoding.GetEncoding("euc-jp")),
                    dir = Regex.Match(oSession.oRequest.headers["Referer"], @"^https?://jbbs.(?:shitaraba.net|livedoor.jp)/(\w+)/\d+").Groups[1].Value,
                    bbs = Regex.Match(oBody, @"BBS=(\d+)(?:&|$)", RegexOptions.IgnoreCase).Groups[1].Value,
                    key = Regex.Match(oBody, @"KEY=(\d+)(?:&|$)", RegexOptions.IgnoreCase).Groups[1].Value,
                    time = Regex.Match(oBody, @"TIME=(\d+)(?:&|$)", RegexOptions.IgnoreCase).Groups[1].Value,
                    name = HttpUtility.UrlEncode(Regex.Match(oBody, @"NAME=(.*?)(?:&\w+?=|$)", RegexOptions.IgnoreCase).Groups[1].Value, Encoding.GetEncoding("euc-jp")),
                    mail = HttpUtility.UrlEncode(Regex.Match(oBody, @"MAIL=(.*?)(?:&|$)", RegexOptions.IgnoreCase).Groups[1].Value, Encoding.GetEncoding("euc-jp")),
                    message = HttpUtility.UrlEncode(Regex.Match(oBody, @"MESSAGE=((?:.|\s)*?)(?:&\w+?=|$)", RegexOptions.IgnoreCase).Groups[1].Value, Encoding.GetEncoding("euc-jp"));
                    oSession.fullUrl = $"https://jbbs.shitaraba.net/bbs/write.cgi/{dir}/{bbs}/{key}/";
                    oSession.oRequest.headers["Content-Type"] = "application/x-www-form-urlencoded";
                    oSession.RequestBody = Encoding.ASCII.GetBytes("submit=%bd%f1%a4%ad%b9%fe%a4%e0&DIR=" + dir + "&BBS=" + bbs + "&KEY=" + key + "&TIME=" + time + "&MESSAGE=" + message + "&NAME=" + name + "&MAIL=" + mail);
                    oSession.oRequest.headers["Referer"] = oSession.oRequest.headers["Referer"].Replace("livedoor.jp", "shitaraba.net");
                }
                catch (NullReferenceException) { }
            }
            oSession.oRequest.headers.Remove("Pragma");
            oSession.oRequest.headers["Connection"] = "Keep-Alive";
            //BeforeResponseで応答を書き換えるために必須
            oSession.bBufferResponse = true;
            SessionStateHandler WRHandler = null;
            WRHandler = (ooSession) =>
            {
                //応答内容書き換え、句点を付ける
                FiddlerApplication.BeforeResponse -= WRHandler;
                ooSession.utilSetResponseBody(Regex.Replace(ooSession.GetResponseBodyAsString(), @"<title>書き(こ|込)み(まし|が完了しまし)た。?</title>", "<title>書きこみました。</title>"));
                ooSession.oResponse.headers["Connection"] = "Close";
            };
            FiddlerApplication.BeforeResponse += WRHandler;
            return;
        }

        private void ResPost(Session oSession, bool is2ch)
        {
            try
            {
                String ReqBody = oSession.GetRequestBodyAsString();
                //ギコナビ、レス投稿時にもsubject=が付いてる対策
                ReqBody = ReqBody.Replace("subject=&", "");
                bool retry = CangeUARetry, ResPost = !ReqBody.Contains("subject=");
                if (oSession.fullUrl.Contains("subbbs.cgi"))
                {
                    oSession.fullUrl = oSession.fullUrl.Replace("subbbs.cgi", "bbs.cgi");
                }
                String PostURI = (ViewModel.Setting.UseTLSWrite) ? (oSession.fullUrl.Replace("http://", "https://")) : (oSession.fullUrl);

                HttpWebRequest Write = (HttpWebRequest)WebRequest.Create(PostURI);
                Write.UserAgent = (String.IsNullOrEmpty(WriteUA)) ? (oSession.oRequest.headers["User-Agent"]) : (WriteUA);
                Write.KeepAlive = false;
                Write.Accept = "text/html, */*";
                try
                {
                    if (oSession.oRequest.headers["Referer"].Contains("test/read.cgi/"))
                    {
                        String referer = oSession.oRequest.headers["Referer"].Replace("test/read.cgi/", "");
                        referer = Regex.Replace(referer, @"\d{10,}(/?$|/l?\d{1,4})", "");
                        Write.Referer = referer.Replace("2ch.net", "5ch.net");
                    }
                    else
                    {
                        Write.Referer = oSession.oRequest.headers["Referer"];
                    }
                }
                catch
                {
                    Write.Referer = oSession.oRequest.headers["Referer"];
                }
                UAReTry:
                Write.Method = "POST";
                Write.ContentType = "application/x-www-form-urlencoded";
                Write.ServicePoint.Expect100Continue = false;
                if (Proxy != "") Write.Proxy = new WebProxy(Proxy);
                Write.Host = oSession.oRequest.host.Replace(".2ch.net", ".5ch.net");
                Write.CookieContainer = new CookieContainer();
                //送信されてきたクッキーを抽出
                foreach (Match mc in Regex.Matches(oSession.oRequest.headers["Cookie"], @"(?:\s+|^)((.+?)=(?:|.+?)(?:;|$))"))
                {
                    m_Cookie[mc.Groups[2].Value] = mc.Groups[1].Value;
                }
                m_Cookie.Remove("sid");
                m_Cookie.Remove("SID");
                //送信クッキーのセット
                String domain = m_CheckWriteuri.Match(oSession.fullUrl).Groups[1].Value;
                foreach (var cook in m_Cookie)
                {
                    if (cook.Value != "")
                    {
                        var m = Regex.Match(cook.Value, @"^(.+?)=(.*?)(;|$)");
                        try
                        {
                            Write.CookieContainer.Add(new Cookie(m.Groups[1].Value, m.Groups[2].Value, "/", domain));
                        }
                        catch (CookieException)
                        {
                            continue;
                        }
                    }
                }
                //浪人を無効化
                if (ViewModel.Setting.PostRoninInvalid && ReqBody.Contains("sid="))
                {
                    ReqBody = Regex.Replace(ReqBody, @"sid=.+?(?:&|$)", "");
                    ReqBody = Regex.Replace(ReqBody, @"&$", "");
                }
                //お絵かき用のデータ追加
                if (ResPost && !ReqBody.Contains("&oekaki_thread") && !oSession.host.Contains("qb5.5ch.net"))
                {
                    ReqBody = ReqBody.Replace("\r\n", "");
                    ReqBody += "&oekaki_thread1=";
                }
                Byte[] Body = Encoding.GetEncoding("Shift_JIS").GetBytes(ReqBody);
                Write.ContentLength = Body.Length;
                try
                {
                    using (System.IO.Stream PostStream = Write.GetRequestStream())
                    {
                        PostStream.Write(Body, 0, Body.Length);
                        HttpWebResponse wres;
                        try
                        {
                            wres = (HttpWebResponse)Write.GetResponse();
                            //スレ立て時はリトライ無効
                            if (retry && ResPost)
                            {
                                String ReCookie = wres.Headers[HttpResponseHeader.SetCookie];
                                if (String.IsNullOrEmpty(ReCookie) || (!ReCookie.Contains("PON=") && !ReCookie.Contains("HAP=") && !ReCookie.Contains("PREN=")))
                                {
                                    using (System.IO.StreamReader Res = new System.IO.StreamReader(wres.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                                    {
                                        String result = Res.ReadToEnd();
                                        if (result.Contains("書きこみました"))
                                        {
                                            retry = false;
                                            wres?.Close();
                                            Write = (HttpWebRequest)WebRequest.Create(PostURI);
                                            Write.UserAgent = NormalUA;
                                            Write.Accept = "text/html, application/xhtml+xml, */*";
                                            Write.Headers.Add("Accept-Language", "ja-JP");
                                            Write.AutomaticDecompression = DecompressionMethods.GZip;
                                            Write.KeepAlive = true;
                                            Write.Referer = oSession.oRequest.headers["Referer"];
                                            ViewModel.OnModelNotice("書き込みリトライが行われました。");
                                            goto UAReTry;
                                        }
                                        else
                                        {
                                            //吸い込まれていないが、書き込めていない場合（連投規制、埋め立て対策等）
                                            oSession.oResponse.headers.HTTPResponseCode = (int)wres.StatusCode;
                                            oSession.oResponse.headers.HTTPResponseStatus = (int)wres.StatusCode + " " + wres.StatusDescription;
                                            oSession.oResponse.headers["Connection"] = "Close";
                                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=Shift_JIS";
                                            oSession.oResponse.headers["Date"] = wres.Headers[HttpResponseHeader.Date];
                                            oSession.oResponse.headers["Vary"] = "Accept-Encoding";
                                            oSession.utilSetResponseBody(result);
                                            if (GZipRes) oSession.utilGZIPResponse();
                                            wres?.Close();
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        catch (WebException err)
                        {
                            if (err.Status == WebExceptionStatus.ServerProtocolViolation)
                            {
                                m_Cookie["DMDM"] = m_Cookie["MDMD"] = "";
                                ViewModel.OnModelNotice("書き込み中にエラーが発生しました。サーバーの応答がおかしいようです。\n" + err.ToString());
                                oSession.oResponse.headers.SetStatus(200, "OK");
                                oSession.oResponse.headers["Connection"] = "Close";
                                oSession.oResponse.headers["Content-Type"] = "text/html; charset=Shift_JIS";
                                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                                oSession.oResponse.headers["Vary"] = "Accept-Encoding";
                                String Reshtml = "<html lang=" + '"' + "ja" + '"' + "><head><title>書きこみました。</title><meta http-equiv=" + '"' + "Content-Type" + '"' + " content=" + '"' + "text/html; charset=Shift_JIS" + '"' + "><meta content=1;URL=" + oSession.fullUrl + " http-equiv=refresh></head><body>書きこみが終わりました。 [0.183750 sec.]<br><br>画面を切り替えるまでしばらくお待ち下さい。<br><br><br><br><br><br><br><center></center></body></html>";
                                oSession.utilSetResponseBody(Reshtml);
                                if (GZipRes) oSession.utilGZIPResponse();
                                return;
                            }
                            wres = (HttpWebResponse)err.Response;
                        }
                        if (wres.Cookies.Count > 0)
                        {
                            var cul = new System.Globalization.CultureInfo("en-US");
                            foreach (System.Net.Cookie cookie in wres.Cookies)
                            {
                                String tc = m_Cookie[cookie.Name] = cookie.ToString();
                                if (cookie.Expires != null) tc += "; expires=" + cookie.Expires.ToUniversalTime().ToString("ddd, dd-MMM-yyyy HH:mm:ss", cul) + " GMT";
                                if (!String.IsNullOrEmpty(cookie.Path)) tc += "; path=" + cookie.Path;
                                if (!String.IsNullOrEmpty(cookie.Domain)) tc += "; domain=" + ((is2ch) ? (cookie.Domain.Replace("5ch.net", "2ch.net")) : (cookie.Domain));
                                oSession.oResponse.headers.Add("Set-Cookie", tc);
                            }
                        }
                        m_Cookie["DMDM"] = m_Cookie["MDMD"] = "";
                        using (System.IO.StreamReader Res = new System.IO.StreamReader(wres.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                        {
                            oSession.oResponse.headers.HTTPResponseCode = (int)wres.StatusCode;
                            oSession.oResponse.headers.HTTPResponseStatus = (int)wres.StatusCode + " " + wres.StatusDescription;
                            if (oSession.oRequest.headers["User-Agent"].Contains("Live2ch")) oSession.oResponse.headers["Connection"] = "Close";
                            else oSession.oResponse.headers["Connection"] = "keep-alive";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=Shift_JIS";
                            oSession.oResponse.headers["Date"] = wres.Headers[HttpResponseHeader.Date];
                            oSession.oResponse.headers["Vary"] = "Accept-Encoding";
                            String resdat = Res.ReadToEnd();
                            oSession.utilSetResponseBody(resdat);
                        }
                        if (wres != null) wres.Close();
                        return;
                    }
                }
                catch (WebException err)
                {
                    m_Cookie["DMDM"] = m_Cookie["MDMD"] = "";
                    ViewModel.OnModelNotice("書き込み中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                    return;
                }
                catch (NullReferenceException err)
                {
                    m_Cookie["DMDM"] = m_Cookie["MDMD"] = "";
                    ViewModel.OnModelNotice("書き込み中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                    return;
                }
            }
            catch (Exception err)
            {
                m_Cookie["DMDM"] = m_Cookie["MDMD"] = "";
                oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=Shift_JIS";
                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                oSession.oResponse.headers["Connection"] = "Close";
                oSession.utilSetResponseBody("2chAPIProxy書き込み処理中にエラーが発生しました。\n" + err.ToString());
                ViewModel.OnModelNotice("書き込み部でエラーです。\n" + err.ToString());
            }
            return;
        }

        private void GetDat(ref Session oSession, bool is2ch)
        {
            try
            {
                int range;
                bool retry = true, retrydat = true;
                HttpWebResponse dat;
                String last = oSession.oRequest.headers["If-Modified-Since"], hrange = oSession.oRequest.headers["Range"];
                range = (!String.IsNullOrEmpty(hrange)) ? (int.Parse(Regex.Match(hrange, @"\d+").Value)) : (-1);
                if (String.IsNullOrEmpty(last)) last = "1970/12/1";
                //スレッドステータス
                int Status = 0;

                Match ch2uri = m_CheckDaturi.Match(oSession.fullUrl);
                datget:
                try
                {
                    dat = APIAccessor.GetDat(ch2uri.Groups[1].Value, ch2uri.Groups[3].Value, ch2uri.Groups[4].Value, range, last);
                }
                catch (Exception err)
                {
                    if (retrydat)
                    {
                        retrydat = false;
                        goto datget;
                    }
                    else
                    {
                        ViewModel.OnModelNotice("datアクセス中にエラーが発生しました。\n" + err.ToString());
                        oSession.oResponse.headers.HTTPResponseCode = 304;
                        oSession.oResponse.headers.HTTPResponseStatus = "304 Not Modified";
                        oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                        oSession.oResponse.headers["Connection"] = "close";
                        return;
                    }
                }

                if (dat == null)
                {
                    ViewModel.OnModelNotice("datの取得に失敗しました。");
                    oSession.oResponse.headers.HTTPResponseCode = 304;
                    oSession.oResponse.headers.HTTPResponseStatus = "304 Not Modified";
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Connection"] = "close";
                    return;
                }

                switch (dat.StatusCode)
                {
                    case HttpStatusCode.PartialContent:
                        oSession.oResponse.headers.HTTPResponseCode = 206;
                        oSession.oResponse.headers.HTTPResponseStatus = "206 Partial Content";
                        oSession.oResponse.headers["Content-Type"] = "text/plain";
                        oSession.oResponse.headers["Last-Modified"] = dat.Headers[HttpResponseHeader.LastModified];
                        oSession.oResponse.headers["Accept-Ranges"] = "bytes";
                        oSession.oResponse.headers["Content-Range"] = dat.Headers[HttpResponseHeader.ContentRange];
                        oSession.oResponse.headers["ETag"] = dat.Headers[HttpResponseHeader.ETag];
                        using (System.IO.StreamReader reader = new System.IO.StreamReader(dat.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                        //using (System.IO.BinaryReader res = new System.IO.BinaryReader(dat.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                        {
                            //oSession.ResponseBody = res.ReadBytes(50 * 1024 * 1024);
                            String resdat = reader.ReadToEnd();
                            if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                            {
                                resdat = HTMLtoDat.ResContentReplace(resdat);
                            }
                            oSession.ResponseBody = Encoding.GetEncoding("Shift_JIS").GetBytes(resdat);
                        }
                        break;
                    case HttpStatusCode.NotModified:
                        oSession.oResponse.headers.HTTPResponseCode = 304;
                        oSession.oResponse.headers.HTTPResponseStatus = "304 Not Modified";
                        oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                        oSession.oResponse.headers["ETag"] = dat.Headers[HttpResponseHeader.ETag];
                        break;
                    case HttpStatusCode.OK:
                        //goto case HttpStatusCode.InternalServerError;
                        //Thread-Statusチェック
                        try
                        {
                            String th = dat.Headers["Thread-Status"];
                            if (!String.IsNullOrEmpty(th)) Status = int.Parse(th);
                            else Status = 1;
                        }
                        catch (FormatException)
                        {
                            Status = 1;
                        }
                        if (Status >= 2) goto case HttpStatusCode.NotImplemented;
                        using (System.IO.StreamReader reader = new System.IO.StreamReader(dat.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                        {
                            String res1 = reader.ReadLine();
                            if (dat.ContentLength > 0 && dat.ContentLength < 26)
                            {
                                //res = reader.ReadToEnd();
                                if (Regex.IsMatch(res1, @"ng \(([a-z]\s?)+\)"))
                                {
                                    ViewModel.OnModelNotice("SessionIDがおかしいようです。各keyを確認の上再取得してください。\n" + res1, false);
                                    goto case HttpStatusCode.NotModified;
                                }
                            }
                            if (CRReplace)
                            {
                                try
                                {
                                    //res1 = reader.ReadLine();
                                    String title = Regex.Match(res1, @"<>.*?<>.+?<>.+?<>(.+?&#169;.+?)$").Groups[1].Value;
                                    if (!String.IsNullOrEmpty(title))
                                    {
                                        String ntitle = title.Replace("&#169;", "&copy;");
                                        res1 = res1.Replace(title, ntitle);
                                        //ret = Encoding.GetEncoding("Shift_JIS").GetBytes(res1 + "\n" + reader.ReadToEnd());
                                    }
                                }
                                catch (Exception) { }
                            }
                            String resdat = res1 + "\n" + reader.ReadToEnd();
                            if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                            {
                                resdat = HTMLtoDat.ResContentReplace(resdat);
                            }
                            oSession.ResponseBody = Encoding.GetEncoding("Shift_JIS").GetBytes(resdat);
                        }
                        oSession.oResponse.headers.HTTPResponseCode = 200;
                        oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                        oSession.oResponse.headers["Content-Type"] = "text/plain";
                        oSession.oResponse.headers["Last-Modified"] = dat.Headers[HttpResponseHeader.LastModified];
                        oSession.oResponse.headers["ETag"] = dat.Headers[HttpResponseHeader.ETag];
                        if (GZipRes) oSession.utilGZIPResponse();
                        break;
                    case HttpStatusCode.NotImplemented:
                        if (!GetHTML || OnlyORPerm)
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 302;
                            oSession.oResponse.headers.HTTPResponseStatus = "302 Found";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }
                        goto case HttpStatusCode.InternalServerError;
                    case HttpStatusCode.Unauthorized:
                        lock (this.APIAccessor)
                        {
                            if (retry == false || m_SIDNowUpdate == true)
                            {
                                if (m_SIDNowUpdate) ViewModel.OnModelNotice("403応答によるSessionID更新を10秒間停止中です、しばらくお待ちください。");
                                goto case HttpStatusCode.NotModified;
                            }
                            m_SIDNowUpdate = true;
                        }
                        try
                        {
                            APIAccessor.UpdateSID();
                            ViewModel.OnModelNotice("SessionIDを更新しました。（期限切れ）");
                        }
                        catch (Exception err)
                        {
                            ViewModel.OnModelNotice("SessionIDの更新に失敗しました\n" + err.ToString());
                        }
                        dat.Close();
                        retry = false;
                        //403応答によるSID更新を10秒間ブロックする
                        System.Threading.Timer ReleaseSIDUpdate = null;
                        ReleaseSIDUpdate = new System.Threading.Timer((e) =>
                        {
                            using (ReleaseSIDUpdate)
                            {
                                m_SIDNowUpdate = false;
                            }
                        }, null, 10000, System.Threading.Timeout.Infinite);
                        goto datget;
                    case HttpStatusCode.InternalServerError:
                        Byte[] Htmldat = null;
                        String uri = @"http://" + ch2uri.Groups[1].Value + "." + ch2uri.Groups[2].Value + "/test/read.cgi/" + ch2uri.Groups[3].Value + @"/" + ch2uri.Groups[4].Value + @"/";
                        if (GetHTML && !OnlyORPerm)
                        {
                            String UA = oSession.oRequest.headers["User-Agent"];
                            System.Threading.Thread HtmlTranceThread = new System.Threading.Thread(() =>
                            {
                                try
                                {
                                    //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                                    //sw.Start();
                                    Htmldat = HTMLtoDat.Gethtml(uri, range, UA, CRReplace, (last != "1970/12/1") ? (last) : (null));
                                    //sw.Stop();
                                    //System.Diagnostics.Debug.WriteLine("処理時間：" + sw.ElapsedMilliseconds + "ms");
                                }
                                catch (System.Threading.ThreadAbortException)
                                {
                                    ViewModel.OnModelNotice("タイムアウトによりHTML変換スレッドを中断。\nURI:" + uri);
                                }
                            });
                            HtmlTranceThread.IsBackground = true;
                            HtmlTranceThread.Start();
                            if (!HtmlTranceThread.Join(30 * 1000))
                            {
                                //変換が終わらなかった場合
                                HtmlTranceThread.Abort();
                                Htmldat = new byte[] { 0 };
                            }
                        }
                        else
                        {
                            if (CheckAlive(@"http://itest.2ch.net/public/newapi/client.php?subdomain=" + ch2uri.Groups[1].Value + "&board=" + ch2uri.Groups[3].Value + "&dat=" + ch2uri.Groups[4].Value)) Htmldat = new byte[] { 0, 0 };
                            else Htmldat = new byte[] { 0 };
                        }
                        if (Htmldat.Length == 2 && Status < 2) goto case HttpStatusCode.NotModified;
                        if (Htmldat.Length == 1 || (Htmldat.Length == 2 && Status >= 2))
                        {
                            oSession.oResponse.headers.SetStatus(302, "302 Found");
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }
                        ViewModel.OnModelNotice(uri + " をhtmlから変換");
                        if (!ViewModel.Setting.AllReturn && range > 0)
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 206;
                            oSession.oResponse.headers.HTTPResponseStatus = "206 Partial Content";
                            oSession.oResponse.headers["Accept-Ranges"] = "bytes";
                            oSession.oResponse.headers["Content-Range"] = "bytes " + range + "-" + (range + Htmldat.Length - 1) + "/" + (range + Htmldat.Length);
                        }
                        else
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 200;
                            oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                        }
                        oSession.oResponse.headers["Last-Modified"] = DateTime.Now.ToUniversalTime().ToString("R");
                        oSession.oResponse.headers["Content-Type"] = "text/plain";
                        oSession.ResponseBody = Htmldat;
                        break;
                    case HttpStatusCode.BadGateway:
                        goto case HttpStatusCode.NotModified;
                    case HttpStatusCode.Found:
                        goto case HttpStatusCode.NotImplemented;
                    case HttpStatusCode.NotFound:
                        //if (CheckAlive(@"http://" + ch2uri.Groups[1].Value + "." + ch2uri.Groups[2].Value + "/test/read.cgi/" + ch2uri.Groups[3].Value + @"/" + ch2uri.Groups[4].Value + @"/"))
                        if (CheckAlive(@"http://itest.2ch.net/public/newapi/client.php?subdomain=" + ch2uri.Groups[1].Value + "&board=" + ch2uri.Groups[3].Value + "&dat=" + ch2uri.Groups[4].Value))
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 416;
                            oSession.oResponse.headers.HTTPResponseStatus = "416 Requested range not satisfiable";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }
                        else goto case HttpStatusCode.NotImplemented;
                    default:
                        oSession.oResponse.headers.HTTPResponseCode = (int)dat.StatusCode;
                        oSession.oResponse.headers.HTTPResponseStatus = (int)dat.StatusCode + " " + dat.StatusDescription;
                        oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                        break;
                }
                oSession.oResponse.headers["Date"] = dat.Headers[HttpResponseHeader.Date];
                oSession.oResponse.headers["Set-Cookie"] = (is2ch) ? (dat.Headers[HttpResponseHeader.SetCookie].Replace("5ch.net", "2ch.net")) : (dat.Headers[HttpResponseHeader.SetCookie]);
                oSession.oResponse.headers["Connection"] = "close";
                dat.Close();
            }
            catch (Exception err)
            {
                oSession.oResponse.headers.SetStatus(304, "304 Not Modified");
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                oSession.oResponse.headers["Connection"] = "close";
                ViewModel.OnModelNotice("datアクセス部でエラーです。\n" + err.ToString());
            }
            return;
        }

        String CMD5(String value)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                Byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        bool CheckAlive(String URI)
        {
            try
            {
                URI = URI.Replace("2ch.net", "5ch.net");
                using (WebClient get = new WebClient())
                {
                    get.Headers["User-Agent"] = NormalUA;
                    if (Proxy != "") get.Proxy = new WebProxy(Proxy);
                    using (System.IO.StreamReader html = new System.IO.StreamReader(get.OpenRead(URI), Encoding.GetEncoding("Shift_JIS")))
                    {
                        if (html.EndOfStream) return false;
                        else return true;
                    }
                }
            }
            catch (WebException)
            {
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public int Start(int PortNum)
        {
            FiddlerCoreStartupFlags f = (AllowWANAccese) ? (FiddlerCoreStartupFlags.AllowRemoteClients | FiddlerCoreStartupFlags.OptimizeThreadPool) : (FiddlerCoreStartupFlags.OptimizeThreadPool);
            FiddlerApplication.Startup(PortNum, f);
            return FiddlerApplication.oProxy.ListenPort;
        }

        public void End()
        {
            FiddlerApplication.Shutdown();
        }
        
        public void Update()
        {
            try
            {
                this.APIAccessor.UpdateSID();
            }
            catch (Exception err)
            {
                ViewModel.OnModelNotice(err.ToString());
            }
        }

    }
}
