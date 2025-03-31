using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;
using Fiddler;
using System.CodeDom.Compiler;
using System.Reflection;
using _2chAPIProxy.HtmlConverter;
using _2chAPIProxy.APIMediator;
using System.Security.Cryptography;
using System.Threading;
using System.ComponentModel;

namespace _2chAPIProxy
{
    class IPAuthData
    {
        public bool Auth = false;
        public String nonce = "";
    }

    public class DatProxy : ViewModels.VMBase
    {
        Dictionary<String, String> Cookie = new Dictionary<string, string>();
        Dictionary<String, IPAuthData> AuthIPList = new Dictionary<string, IPAuthData>();
        Regex Check2churi = new Regex(@"^(\w+?)\.((?:2|5)ch\.net|bbspink\.com)$", RegexOptions.Compiled);
        Regex CheckDaturi = new Regex(@"^https?:\/\/(\w+?)\.((?:2|5)ch\.net|bbspink\.com)\/(\w+?)\/dat\/(\d+?)\.dat", RegexOptions.Compiled);
        Regex CheckWriteuri = new Regex(@"^https?:\/\/\w+?(\.(?:2|5)ch.net|\.bbspink.com)(?::\d{2,})?\/test\/(?:sub)?bbs\.cgi", RegexOptions.Compiled);
        Regex CheckKakouri = new Regex(@"(^https?:\/\/rokka\.((?:2|5)ch\.net|bbspink\.com)\/(\w+?)\/(\w+?)\/(\d+?)\/.+|http:\/\/\w+?.(2|5)ch\.net\/test\/offlaw2\.so.+)", RegexOptions.Compiled);
        Regex CheckKakouri2 = new Regex(@"^https?:\/\/((?:\w+?)\.(?:(?:2|5)ch\.net|bbspink\.com))\/(\w+?)\/kako\/(?:\d{4}\/\d{5}|\d{3})\/(\d+?)\.dat", RegexOptions.Compiled);
        Regex CheckOldBe = new Regex(@"^https?://(?:be.(?:2|5)ch.net/(?:test/)?(login|index).php)", RegexOptions.Compiled);
        Regex CheckShitaraba = new Regex(@"^http://jbbs.(shitaraba.net|livedoor.jp)", RegexOptions.Compiled);
        //Regex CheckShitarabaPost = new Regex(@"^https?://jbbs.(shitaraba.net|livedoor.jp)(/bbs/(rawmode|read).cgi/\w+/\d+/\d+|.+?/write.cgi.*?)", RegexOptions.Compiled);
        Regex CheckShitarabaPost = new Regex(@"^https?://jbbs.(shitaraba.net|livedoor.jp)/.+?/write.cgi", RegexOptions.Compiled);
        //Regex CheckItauri = new Regex(@"^https?:\/\/(\w+?)\.(2ch\.net|bbspink\.com)\/(\w+?)/?$", RegexOptions.Compiled);
        Regex CheckItauri = new Regex(@"^https?://\w+?\.((?:2|5)ch\.net|bbspink\.com)/\w+(/?$|/subject.txt$)", RegexOptions.Compiled);
        Regex BBSMenuReplace = new Regex(@"^https?://menu\.(?:2|5)ch\.net/bbsmenu.html", RegexOptions.Compiled);
        volatile bool SIDNowUpdate = false;


        public IAPIMediator APIMediator { get; set; }

        public IHtmlConverter HtmlConverter { get; set; }

        public Dictionary<string, BoardSettings> BoardSettings { get; set; }
        //public HTMLtoDat htmlconverter { get; set; }
        public String Proxy { get; set; }
        public String WriteUA { get; set; }
        //public String NormalUA { get; set; }
        public String user { get; set; }
        public String pw { get; set; }
        public bool GetHTML { get; set; }
        public bool AllowWANAccese { get; set; }
        public bool CangeUARetry { get; set; }
        public bool OfflawRokkaPerm { get; set; }
        public bool gZipRes { get; set; }
        public bool ChunkRes { get; set; }
        public bool SocksPoxy { get; set; }
        public bool NotReplaceNormalDatAccess { get; set; }
        public bool CRReplace { get; set; }
        public bool KakolinkPerm { get; set; }
        public bool AllUAReplace { get; set; }
        public bool BeLogin { get; set; }
        public bool SetReferrer { get; set; }
        public bool EnablePostv2 { get; set; }
        public bool EnablePostv2onPink { get; set; }
        public bool EnableUTF8Post { get; set; }
        public bool AddX2chUAHeader { get; set; }
        public bool AddMsToNonce { get; set; }
        public bool AssumeReqBodyIsUTF8 { get; set; }


        private string[] PostFieldOrederArray;

        private string postFieldOrder;
        public String PostFieldOrder 
        { 
            get => postFieldOrder;
            set 
            {
                postFieldOrder = value;
                PostFieldOrederArray = value.Split('&');
            } 
        }

        private string[] ThreadPostFieldOrederArray;

        private string threadPostFieldOrder;
        public String ThreadPostFieldOrder
        {
            get => threadPostFieldOrder;
            set
            {
                threadPostFieldOrder = value;
                ThreadPostFieldOrederArray = value.Split('&');
            }
        }

        public DatProxy(String Akey, String Hkey, String ua1, String sidUA, String ua2, String RID, String RPW, String ProxyAddrese)
        {
            //System.Threading.Timer GetSid = null;
            //GetSid = new System.Threading.Timer((e) =>
            //{
            //    using (GetSid)
            //    {
            //        APIMediator.RouninID = RID;
            //        APIMediator.RouninPW = RPW;
            //        APIMediator.Init(Akey, Hkey, ua1, sidUA, ua2, ProxyAddrese);
            //        //APIMediator = new APIAccess(Akey, Hkey, ua1, sidUA, ua2, RID, RPW, ProxyAddrese);
            //    }
            //}, null, 0, System.Threading.Timeout.Infinite);
            //Fiddler設定変更
            Fiddler.CONFIG.bReuseClientSockets = true;
            Fiddler.CONFIG.bReuseServerSockets = true;
            // TLS1.2までを使用可能なように設定（XPとかVistaとかだとここ何が起こるだろう・・・？
            Fiddler.CONFIG.oAcceptedServerHTTPSProtocols = System.Security.Authentication.SslProtocols.Tls | (System.Security.Authentication.SslProtocols)768 | (System.Security.Authentication.SslProtocols)3072;
            //正規表現キャッシュサイズ
            Regex.CacheSize = 75;
            GetHTML = true;
            AllowWANAccese = false;

            FiddlerApplication.BeforeRequest += (oSession) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(Proxy) == false) oSession["X-OverrideGateway"] = (SocksPoxy) ? ("socks=" + Proxy) : (Proxy);
                    if (AllowWANAccese && !oSession.clientIP.Contains("127.0.0.1"))
                    {
                        //WANアクセス有効時の認証と識別
                        if (!WANAcceseAuth(ref oSession)) return;
                    }
                    if (oSession.HTTPMethodIs("CONNECT"))
                    {
                        // HTTPSのConnect要求はスルーする
                        oSession.Ignore();
                        return;
                    }
                    if (Check2churi.IsMatch(oSession.hostname))
                    {
                        //元のURLが2chか5chか
                        bool is2ch = oSession.fullUrl.Contains(".2ch.net/");
                        //2ch→5ch置換
                        oSession.fullUrl = oSession.fullUrl.Replace(".2ch.net/", ".5ch.net/");
                        if (oSession.oRequest.headers.Exists("Referer"))
                        {
                            oSession.oRequest.headers["Referer"] = oSession.oRequest.headers["Referer"].Replace(".2ch.net/", ".5ch.net/");
                        }
                        if (CheckDaturi.IsMatch(oSession.fullUrl))
                        {
                            /*
                            //dat読みをAPIへ
                            oSession.utilCreateResponseAndBypassServer();
                            GetDat(ref oSession, is2ch);
                            return;
                            */
                            if (ViewModel.Setting.UseTLSWrite) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");

                            var match = CheckDaturi.Match(oSession.fullUrl);
                            // HTML取得用URL
                            string threaduri = @$"https://{match.Groups[1].Value}.{match.Groups[2].Value}/test/read.cgi/{match.Groups[3].Value}/{match.Groups[4].Value}/";

                            // レスポンス返し直前に介入する
                            oSession.bBufferResponse = true;
                            SessionStateHandler BRHandler = null;
                            BRHandler = (ooSession) =>
                            {
                                FiddlerApplication.BeforeResponse -= BRHandler;
                                intervene_in_dat_response(ref ooSession, is2ch, threaduri, false);
                            };
                            FiddlerApplication.BeforeResponse += BRHandler;

                            System.Diagnostics.Debug.WriteLine("dat URI：" + oSession.fullUrl);

                            return;
                        }
                        else if (CheckKakouri2.IsMatch(oSession.fullUrl))
                        {
                            // 2023/07/11導入？の過去ログURLへの振り替えを行う
                            // まず最初は過去ログ倉庫からの取得を試みる（HTML変換はそれが失敗してから）
                            System.Diagnostics.Debug.WriteLine("kako URI：" + oSession.fullUrl);

                            var match = CheckKakouri2.Match(oSession.fullUrl);

                            string thread_key = match.Groups[3].Value;
                            // https://鯖名.5ch.net/板名/oyster/スレッドキー上位4桁の数字/スレッドキー.dat の形式に変換
                            oSession.fullUrl = @$"https://{match.Groups[1].Value}/{match.Groups[2].Value}/oyster/{thread_key.Substring(0, 4)}/{thread_key}.dat";
                            // HTML取得用URL
                            string threadurl = $@"https://{match.Groups[1].Value}/test/read.cgi/{match.Groups[2].Value}/{thread_key}/";

                            // レスポンス返し直前に介入する
                            oSession.bBufferResponse = true;
                            SessionStateHandler BRHandler = null;
                            BRHandler = (ooSession) =>
                            {
                                FiddlerApplication.BeforeResponse -= BRHandler;
                                intervene_in_dat_response(ref ooSession, is2ch, threadurl, true);
                            };
                            FiddlerApplication.BeforeResponse += BRHandler;

                            return;
                        } 
                        else if (CheckKakouri.IsMatch(oSession.fullUrl))
                        {
                            if (GetHTML)
                            {
                                // offlaw,rokkaのHTML変換応答
                                if (OtherLinkHTMLTrance(ref oSession)) return;
                            }
                        }
                        else if (CheckWriteuri.IsMatch(oSession.fullUrl))
                        {
                            if (ViewModel.Setting.PostNoReplace == true)
                            {
                                System.Diagnostics.Debug.WriteLine("書き込み関与を最小限にして書き込み");
                                if (oSession.oRequest.headers["User-Agent"].Contains("gikoNavi") == true)
                                {
                                    oSession.utilReplaceInRequest("\r\n", "");
                                }
                                if (ViewModel.Setting.UseTLSWrite == true) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
                                if (string.IsNullOrEmpty(ViewModel.Setting.UserAgent3) == false) oSession.oRequest.headers["User-Agent"] = ViewModel.Setting.UserAgent3;
                                oSession.Ignore();
                            }
                            else
                            {
                                //書き込みをバイパスする
                                oSession.utilCreateResponseAndBypassServer();

                                if (EnablePostv2)
                                {
                                    if (!EnablePostv2onPink && oSession.fullUrl.Contains("bbspink.com"))
                                    {
                                        // pinkはまだ新書き込み仕様になってないらしい？
                                        ResPost(oSession, is2ch);
                                    }
                                    else
                                    {
                                        ResPostv2(oSession, is2ch);
                                    }
                                }
                                else
                                {
                                    ResPost(oSession, is2ch);
                                }
                            }
                            return;
                        }
                        else if (CheckOldBe.IsMatch(oSession.fullUrl))
                        {
                            //Be2.1ログイン処理代行
                            Be21Login(oSession, is2ch);
                            return;
                        }
                        else if (CheckItauri.IsMatch(oSession.fullUrl))
                        {
                            //移転時のhttpsリンクを書き換える
                            Replacehttps(ref oSession, is2ch);
                            return;
                        }
                        else if (BBSMenuReplace.IsMatch(oSession.fullUrl))
                        {
                            //板一覧のリンク前後についているダブルクォート削除
                            //BBSMenuの置き換え
                            BBSMenuURLReplace(ref oSession, is2ch);
                            return;
                        }
                        else if (oSession.fullUrl.Contains("://dig."))
                        {
                            //スレタイ検索(dig.2ch.net)
                            oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
                            if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                            {
                                oSession.bBufferResponse = true;
                                SessionStateHandler BRHandler = null;
                                BRHandler = (ooSession) =>
                                {
                                    FiddlerApplication.BeforeResponse -= BRHandler;
                                    //ooSession.ResponseBody = Encoding.UTF8.GetBytes(HTMLtoDat.ResContentReplace(ooSession.GetResponseBodyAsString()));
                                    ooSession.ResponseBody = Encoding.UTF8.GetBytes(HtmlConverter.ResContentReplace(ooSession.GetResponseBodyAsString()));
                                };
                                FiddlerApplication.BeforeResponse += BRHandler;
                                return;
                            }
                            oSession.Ignore();
                            return;
                        }
                        //APIや書き込み等以外のアクセス時の処理
                        if (ViewModel.Setting.UseTLSWrite) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
                        if (AllUAReplace) oSession.oRequest.headers["User-Agent"] = WriteUA;
                    }
                    else if (CheckShitaraba.IsMatch(oSession.fullUrl))
                    {
                        if (ViewModel.Setting.UseTLSWrite)
                        {
                            //したらばTLS接続
                            oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
                            oSession["x-OverrideSslProtocols"] = " tls1.0;tls1.1;tls1.2";
                            System.Diagnostics.Debug.WriteLine("HTTPS化：" + oSession.fullUrl);
                        }

                        if (CheckShitarabaPost.IsMatch(oSession.fullUrl))
                        {
                            //したらば書き込みと結果置換
                            ShitarabaPost(oSession);
                            System.Diagnostics.Debug.WriteLine("書き込み置換：" + oSession.fullUrl);
                            return;
                        }

                        oSession.Ignore();
                        return;
                    }

                    //不要ヘッダの削除
                    oSession.oRequest.headers.Remove("Pragma");
                    if (oSession.oRequest.headers.Exists("Proxy-Connection"))
                    {
                        oSession.oRequest.headers["Connection"] = oSession.oRequest.headers["Proxy-Connection"];
                        oSession.oRequest.headers.Remove("Proxy-Connection");
                    }
                    oSession.Ignore();
                }
                catch (Exception err)
                {
                    ViewModel.OnModelNotice($"プロクシ処理中にエラーが発生しました。URL:{oSession.fullUrl}\n{err.ToString()}");
                }
            };
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
                // 板一覧の板URLの前後に""があった場合に消す（大丈夫そうならいらない？）
                var ItaMatches = Regex.Matches(html, $@"<(?:A HREF|a href)={'"'}(?:https?:)?(//\w+?\.(?:2ch\.net|5ch\.net|bbspink\.com)/\w+/?){'"'}>(.+)</(?:A|a)>");
                foreach (Match ita in ItaMatches)
                {
                    String replace = $"<A HREF=https:{ita.Groups[1].Value}>{ita.Groups[2].Value}</A>";
                    html = html.Replace(ita.Value, replace);
                }

                if (is2ch) html = html.Replace(".5ch.net/", ".2ch.net/");
                // 板のhttpsリンクをhttpにする
                if (ViewModel.Setting.ReplaceHttpsLink) html = html.Replace("https", "http");
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
                //if (!AuthIP.ContainsKey(oSession.clientIP) || !AuthIP[oSession.clientIP])
                if (!AuthIPList.ContainsKey(oSession.clientIP) || !AuthIPList[oSession.clientIP].Auth)
                {
                    //認証されていない時
                    //if (AuthIP.ContainsKey(oSession.clientIP))
                    if (AuthIPList.ContainsKey(oSession.clientIP))
                    {
                        //帰ってきたMD5をチェックする
                        String Res = oSession.oRequest.headers["Authorization"];
                        String /*nonce = Regex.Match(Res, @".*(?<!c)nonce=" + '"' + "(.+?)" + '"').Groups[1].Value,*/
                            uri = Regex.Match(Res, @".*uri=" + '"' + "(.+?)" + '"').Groups[1].Value,
                            nc = Regex.Match(Res, @".*nc=(.+?),").Groups[1].Value,
                            cnonce = Regex.Match(Res, @".*cnonce=" + '"' + "(.+?)" + '"').Groups[1].Value,
                            resp = Regex.Match(Res, @".*response=" + '"' + "(.+?)" + '"').Groups[1].Value;
                        String method = oSession.RequestMethod;
                        String A1 = CMD5(user + ":2chAPIProxy Auth:" + pw);
                        String A2 = CMD5(method + ":" + uri);
                        String hash = CMD5(A1 + ":" + AuthIPList[oSession.clientIP].nonce + ":" + nc + ":" + cnonce + ":" + "auth" + ":" + A2);
                        if (hash == resp)
                        {
                            ViewModel.OnModelNotice(oSession.clientIP + "を認証");
                            AuthIPList[oSession.clientIP].nonce = "";
                            AuthIPList[oSession.clientIP].Auth = true;
                            //AuthIP[oSession.clientIP] = true;
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
                    AuthIPList[oSession.clientIP] = new IPAuthData();
                    AuthIPList[oSession.clientIP].Auth = false;
                    AuthIPList[oSession.clientIP].nonce = System.Web.Security.Membership.GeneratePassword(24, 0);
                    oSession.utilCreateResponseAndBypassServer();
                    oSession.oResponse.headers.HTTPResponseCode = 401;
                    oSession.oResponse.headers.HTTPResponseStatus = "401 Authorization Required";
                    oSession.oResponse.headers["Date"] = DateTime.Now.ToString("R");
                    oSession.oResponse.headers["Server"] = "2chAPIProxy";
                    oSession.oResponse.headers["WWW-Authenticate"] = "Digest realm=" + '"' + "2chAPIProxy Auth" + '"' + ", nonce=" + '"' + AuthIPList[oSession.clientIP].nonce + '"' + ", algorithm=MD5, qop=" + '"' + "auth" + '"';
                    oSession.oResponse.headers["Connection"] = "close";
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Cache-Control"] = "no-cache";
                    //AuthIP[oSession.clientIP] = false;
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                ViewModel.OnModelNotice(oSession.clientIP + "から接続されました");
                oSession.utilCreateResponseAndBypassServer();
                AuthIPList[oSession.clientIP] = new IPAuthData();
                AuthIPList[oSession.clientIP].Auth = false;
                AuthIPList[oSession.clientIP].nonce = System.Web.Security.Membership.GeneratePassword(24, 0);
                oSession.oResponse.headers.SetStatus(401, "401 Authorization Required"); oSession.oResponse.headers["Date"] = DateTime.Now.ToString("R");
                oSession.oResponse.headers["Server"] = "2chAPIProxy";
                oSession.oResponse.headers["WWW-Authenticate"] = "Digest realm=" + '"' + "2chAPIProxy Auth" + '"' + ", nonce=" + '"' + AuthIPList[oSession.clientIP].nonce + '"' + ", algorithm=MD5, qop=" + '"' + "auth" + '"';
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

                if (OfflawRokkaPerm && CheckKakouri.IsMatch(oSession.fullUrl))
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
                                var group = CheckKakouri.Match(URI).Groups;
                                ThreadURI = @"http://" + group[3].Value + "." + group[2].Value + @"/test/read.cgi/" + group[4].Value + @"/" + group[5].Value + @"/";
                            }
                            //Htmldat = HTMLtoDat.Gethtml(ThreadURI, -1, "", CRReplace);
                            Htmldat = HtmlConverter.Gethtml(ThreadURI, -1, "", CRReplace);
                        }
                        catch (System.Threading.ThreadAbortException)
                        {
                            ViewModel.OnModelNotice("タイムアウトによりHTML変換スレッドを中断。\nURI:" + URI);
                        }
                    });
                }
                else if (KakolinkPerm && CheckKakouri2.IsMatch(oSession.fullUrl))
                {
                    //kakoリンクのHTML変換応答置換
                    offlowperm = false;
                    oSession.utilCreateResponseAndBypassServer();
                    String URI = oSession.fullUrl;
                    HtmlTranceThread = new System.Threading.Thread(() =>
                    {
                        try
                        {
                            var group = CheckKakouri2.Match(URI).Groups;
                            String ThreadURI = "http://" + group[1].Value + "/test/read.cgi/" + group[2].Value + "/" + group[3].Value + "/";
                            //Htmldat = HTMLtoDat.Gethtml(ThreadURI, -1, "", CRReplace);
                            Htmldat = HtmlConverter.Gethtml(ThreadURI, -1, "", CRReplace);
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
                    if (gZipRes) oSession.utilGZIPResponse();
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
                    if ((int)wres.StatusCode == 200 && CheckOldBe.Match(oSession.fullUrl).Groups[1].Value == "index")
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

        private string post_form_feature = "";

        private void ResPost(Session oSession, bool is2ch)
        {
            try
            {
                String ReqBody = oSession.GetRequestBodyAsString();
                //ギコナビ、レス投稿時にもsubject=が付いてる対策
                ReqBody = ReqBody.Replace("subject=&", "");
                //主にギコナビ、submitに改行が入っている
                ReqBody = ReqBody.Replace("\r\n", "");
                //スレ立てと書き込みを識別する、同じbbs.cgiを使用しているため
                bool IsResPost = !ReqBody.Contains("subject=");
                if (oSession.fullUrl.Contains("subbbs.cgi"))
                {
                    oSession.fullUrl = oSession.fullUrl.Replace("subbbs.cgi", "bbs.cgi");
                }


                String PostURI = (ViewModel.Setting.UseTLSWrite) ? (oSession.fullUrl.Replace("http://", "https://")) : (oSession.fullUrl);
                HttpWebRequest Write = (HttpWebRequest)WebRequest.Create(PostURI);
                Write.Method = "POST";
                Write.ServicePoint.Expect100Continue = false;
                Write.Headers.Clear();
                //ここで指定しないとデコードされない
                Write.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                //デフォルトがtrueなのでオフっとく
                Write.KeepAlive = false;
                Write.Connection = null;    // こうしないとヘッダから消えない

                //デバッグ出力
                System.Diagnostics.Debug.WriteLine("オリジナルリクエストヘッダ");
                foreach (var header in oSession.RequestHeaders)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Name}:{header.Value}");
                }

                // デフォルトUAを設定（優先度最低、空の時は知らない）
                Write.UserAgent = BoardSettings["2chapiproxy_default"].UserAgent;

                // pinkへの書き込みを識別
                bool is_pink = oSession.fullUrl.Contains("bbspink.com") && BoardSettings.ContainsKey("2chapiproxy_pink_common");

                // 板毎設定の引き当て
                BoardSettings PostSetting = null;
                if (1 < BoardSettings.Count())
                {
                    // Pink共通設定があれば引き当てておく
                    if (is_pink)
                    {
                        PostSetting = BoardSettings["2chapiproxy_pink_common"];
                    }

                    // 板名を抽出
                    var bbs_match = Regex.Match(ReqBody, @"bbs=(\w+)");

                    // 板事設定があればそれが最優先
                    if (bbs_match.Success)
                    {
                        var bbs = bbs_match.Groups[1].Value;
                        if (BoardSettings.ContainsKey(bbs))
                        {
                            PostSetting = BoardSettings[bbs];
                        }
                    }
                }

                // UAの設定
                // デフォルト→板毎設定→書き込みUAの順に優先
                if (is_pink)
                {
                    // pink共通設定は最優先
                    Write.UserAgent = PostSetting.UserAgent;
                }
                else if (String.IsNullOrEmpty(WriteUA))
                {
                    // 設定があるときだけ上書き
                    if (string.IsNullOrEmpty(PostSetting?.UserAgent) == false)
                    {
                        Write.UserAgent = PostSetting.UserAgent;
                        // お絵かき設定はどうしようね・・・
                        if (PostSetting.Headers.Count() == 0) PostSetting = null;
                    }
                }
                else
                {
                    // UIの書き込みUAを私用
                    Write.UserAgent = WriteUA;
                }

                if (is_pink == false)
                {
                    // デフォルト設定の引き当て（Pink投稿時は共通設定がないときのみ
                    PostSetting ??= BoardSettings["2chapiproxy_default"];
                }

                // ヘッダの設定

                // 個別の設定項目があるやつ
                if (PostSetting.Headers.ContainsKey("Accept") == true)
                {
                    Write.Accept = PostSetting.Headers["Accept"];
                }
                if (PostSetting.Headers.ContainsKey("Expect") == true)
                {
                    Write.Expect = PostSetting.Headers["Expect"];
                }
                if (PostSetting.Headers.ContainsKey("Content-Type") == true)
                {
                    Write.ContentType = PostSetting.Headers["Content-Type"];
                }
                if (PostSetting.KeepAlive)
                {
                    Write.KeepAlive = true;
                    //Write.Connection = PostSetting.Headers["Connection"];
                }

                // 直接設定できるのはまとめて
                foreach (var header in PostSetting.Headers)
                {
                    try
                    {
                        if (Regex.IsMatch(header.Key, @"(^HTTPVer$|^Accept$|^User-Agent$|^Expect$|^Content-Type$|^Connection$|^Cookie$)") == true) continue;
                        Write.Headers.Add(header.Key, header.Value);
                    }
                    catch (Exception err)
                    {
                        ViewModel.OnModelNotice($"{header.Key}ヘッダは設定できません。");
                        System.Diagnostics.Debug.WriteLine("●ヘッダ定義の適用中のエラー\n" + err.ToString());
                    }
                }

                // これ順番ここじゃなきゃだめ？
                if (PostSetting.Headers.ContainsKey("HTTPVer") == true)
                {
                    if (PostSetting.Headers["HTTPVer"] == "1.0")
                    {
                        Write.ProtocolVersion = HttpVersion.Version10;
                    }
                }

                // referer調整
                String referer = oSession.oRequest.headers["Referer"];
                if (IsResPost && SetReferrer && Regex.IsMatch(referer, @"https?://\w+\.(?:(?:2|5)ch\.net|bbspink\.com)/test/read\.cgi/\w+/\d{9,}") == false)
                {
                    var bbs = Regex.Match(ReqBody, @"bbs=(\w+)").Groups[1].Value;
                    var key = Regex.Match(ReqBody, @"key=(\w+)").Groups[1].Value;
                    referer = @$"https://{Write.Host}/test/read.cgi/{bbs}/{key}/";
                }
                else
                {
                    referer = oSession.oRequest.headers["Referer"].Replace("2ch.net", "5ch.net").Replace("http:", "https:");
                }
                Write.Referer = referer;

                if (string.IsNullOrEmpty(Proxy) == false) Write.Proxy = new WebProxy(Proxy);

                Write.CookieContainer = new CookieContainer();

                // どんぐり枯れレスポンス/MonaTicket無効化を検知するマーカー
                const string mark_acorn_dride_up = "ignore next acorn";
                // どんぐりクッキー名
                const string acorn_cookie = "acorn";
                // MonaTicketクッキー名
                const string monaticket_cookie = "MonaTicket";

                {
                    bool ignore_acorn = false;
                    bool ignore_monaticket = false;

                    // どんぐりが枯れた次のレス投稿の場合、acornを送らない
                    if (Cookie.ContainsKey(acorn_cookie) && Cookie[acorn_cookie] == mark_acorn_dride_up)
                    {
                        ignore_acorn = true;
                        Cookie[acorn_cookie] = "";
                    }
                    // Monaticketも同様に削除
                    if (Cookie.ContainsKey(acorn_cookie) && Cookie[acorn_cookie] == mark_acorn_dride_up)
                    {
                        ignore_monaticket = true;
                        Cookie[monaticket_cookie] = "";
                    }

                    //送信されてきたクッキーを抽出
                    foreach (Match mc in Regex.Matches(oSession.oRequest.headers["Cookie"], @"(?:\s+|^)((.+?)=(?:|.+?)(?:;|$))"))
                    {
                        Cookie[mc.Groups[2].Value] = mc.Groups[1].Value;
                    }

                    // acornクッキーを削除し、送らないようにする
                    if (ignore_acorn)
                    {
                        Cookie.Remove(acorn_cookie);
                    }
                    // MonaTicketクッキーを削除し、送らないようにする
                    if (ignore_monaticket)
                    {
                        Cookie.Remove(monaticket_cookie);
                    }
                }

                Cookie.Remove("sid");
                Cookie.Remove("SID");
                // TAKO=ODORIを消す
                Cookie.Remove("TAKO");


                //送信クッキーのセット
                String domain = CheckWriteuri.Match(oSession.fullUrl).Groups[1].Value;
                foreach (var cook in Cookie.Where(c => string.IsNullOrEmpty(c.Value) == false))
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

                //浪人を無効化
                if (ViewModel.Setting.PostRoninInvalid && ReqBody.Contains("sid="))
                {
                    ReqBody = Regex.Replace(ReqBody, @"sid=.+?(?:&|$)", "");
                    ReqBody = Regex.Replace(ReqBody, @"&$", "");
                }
                // feature=confirmed:xxxを追加
                if (string.IsNullOrEmpty(post_form_feature) == false)
                {
                    // 送られてきてない場合のみ
                    if (ReqBody.Contains("&feature=confirmed%3A") == false)
                    {
                        ReqBody = ReqBody.Replace("\r\n", "");
                        ReqBody += $"&feature=confirmed%3A{post_form_feature}";

                        post_form_feature = "";
                    }                    
                }
                //お絵かき用のデータ追加
                if (IsResPost)
                {
                    if (PostSetting.SetOekaki && !ReqBody.Contains("&oekaki_thread"))
                    {
                        ReqBody = ReqBody.Replace("\r\n", "");
                        ReqBody += "&oekaki_thread1=";
                    }
                    else if (PostSetting.SetOekaki == false && ReqBody.Contains("&oekaki_thread"))
                    {
                        ReqBody = ReqBody.Replace("&oekaki_thread1=", "");
                    }
                }
                
                Byte[] Body = Encoding.GetEncoding("Shift_JIS").GetBytes(ReqBody);
                Write.ContentLength = Body.Length;
                try
                {
                    using (System.IO.Stream PostStream = Write.GetRequestStream())
                    {
                        PostStream.Write(Body, 0, Body.Length);
                        foreach (var header in Write.Headers.AllKeys)
                        {
                            System.Diagnostics.Debug.WriteLine($"{header}:{Write.Headers[header].ToString()}");
                        }

                        HttpWebResponse wres = (HttpWebResponse)Write.GetResponse();

                        // Set-Cookieの抽出
                        if (wres.Cookies.Count > 0)
                        {
                            var cul = new System.Globalization.CultureInfo("en-US");
                            foreach (System.Net.Cookie cookie in wres.Cookies)
                            {
                                String tc = Cookie[cookie.Name] = cookie.ToString();
                                if (cookie.Expires != null) tc += "; expires=" + cookie.Expires.ToUniversalTime().ToString("ddd, dd-MMM-yyyy HH:mm:ss", cul) + " GMT";
                                if (!String.IsNullOrEmpty(cookie.Path)) tc += "; path=" + cookie.Path;
                                if (!String.IsNullOrEmpty(cookie.Domain)) tc += "; domain=" + ((is2ch) ? (cookie.Domain.Replace("5ch.net", "2ch.net")) : (cookie.Domain));
                                oSession.oResponse.headers.Add("Set-Cookie", tc);
                            }
                        }

                        if (wres.Headers.AllKeys.Contains("X-Chx-Error") == true)
                        {
                            // どんぐりが枯れてしまいました。
                            // X-Chx-Error:1930 Acorn have dried up.
                            // X-Chx-Error:1932 should login to the donguri system.;
                            if (wres.Headers["X-Chx-Error"].Contains("1930") || wres.Headers["X-Chx-Error"].Contains("1932"))
                            {
                                // どんぐり枯れを検知したら、acornクッキーを削除する
                                // 本当に削除するのは、次の投稿時
                                Cookie[acorn_cookie] = mark_acorn_dride_up;
                            }

                            // ただ今あなたの投稿を拒否しております。
                            // X-Chx-Error : E4000 Reject your post.;
                            // Cookie の内容が壊れていますのでいったん削除してください。
                            // X-Chx-Error : E3000 Delete your cookie.
                            if (wres.Headers["X-Chx-Error"].Contains("Delete your cookie") || wres.Headers["X-Chx-Error"].Contains("Reject your post"))
                            {
                                // MonaTicketクッキーの削除
                                // 本当に削除するのは、次の投稿時
                                Cookie[monaticket_cookie] = mark_acorn_dride_up;
                            }

                            ViewModel.OnModelNotice("X-Chx-Error : " + wres.Headers["X-Chx-Error"]);
                        }

                        Cookie["DMDM"] = Cookie["MDMD"] = "";
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

                            // 書き込み確認画面の処理
                            if (resdat.Contains("<title>■ 書き込み確認 ■</title>"))
                            {
                                // feature パラメータを抽出しておく
                                post_form_feature = Regex.Match(resdat, $@"<input type=hidden name={'"'}feature{'"'} value={'"'}confirmed:(\w+?){'"'}>").Groups[1].Value;
                            }
                        }

                        System.Diagnostics.Debug.WriteLine("リクエストヘッダ");
                        foreach (var header in Write.Headers.AllKeys)
                        {
                            System.Diagnostics.Debug.WriteLine($"{header}:{Write.Headers[header].ToString()}");
                        }

                        System.Diagnostics.Debug.WriteLine("レスポンスヘッダ");
                        foreach (var header in wres.Headers.AllKeys)
                        {
                            System.Diagnostics.Debug.WriteLine($"{header}:{wres.Headers[header].ToString()}");
                        }

                        if (wres != null) wres.Close();
                        return;
                    }
                }
                catch (WebException err)
                {
                    Cookie["DMDM"] = Cookie["MDMD"] = "";
                    ViewModel.OnModelNotice("書き込み中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                    return;
                }
                catch (NullReferenceException err)
                {
                    Cookie["DMDM"] = Cookie["MDMD"] = "";
                    ViewModel.OnModelNotice("書き込み中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                    return;
                }
            }
            catch (Exception err)
            {
                Cookie["DMDM"] = Cookie["MDMD"] = "";
                oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=Shift_JIS";
                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                oSession.oResponse.headers["Connection"] = "Close";
                oSession.utilSetResponseBody("2chAPIProxy書き込み処理中にエラーが発生しました。\n" + err.ToString());
                ViewModel.OnModelNotice("書き込み部でエラーです。\n" + err.ToString());
            }
            return;
        }

        private string monakey = "00000000-0000-0000-0000-000000000000";
        public string Monakey
        {
            get => monakey;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    monakey = "00000000-0000-0000-0000-000000000000";
                }
                else
                {
                    _ = SetProperty(ref monakey, value, nameof(Monakey));
                }
            }
        }

        public void ResetMonakey()
        {
            Monakey = "";
            ViewModel.OnModelNotice("MonaKeyをリセットしました。");
        }

        //private string Monakey = "7b6799cc2bb1eef3acadffeecc180df6d1c7caab887326120056660f6ac05b45";


        // キー要素があればそれを、無ければ空文字
        private string ValueOr(Dictionary<string, string> dict, string key)
        {
            return dict.TryGetValue(key, out string value) ? value : "";
        }

        private string CreatePostsignature(Dictionary<string, string> post_filed, string nonce, string UA, Encoding dst_encoding)
        {
            // PostSig計算用文字列
            string sigstr = $"{ValueOr(post_filed, "bbs")}<>{ValueOr(post_filed, "key")}<>{ValueOr(post_filed, "time")}<>{ValueOr(post_filed, "FROM")}<>{ValueOr(post_filed, "mail")}<>{ValueOr(post_filed, "MESSAGE")}<>{ValueOr(post_filed, "subject")}<>{UA}<>{Monakey}<><>{nonce}";

            using (HMACSHA256 hs256 = new HMACSHA256(Encoding.UTF8.GetBytes(this.APIMediator.HMKey)))
            {
                // UTF-8でポストするときはUTF-8で、Shift-JiSでポストするときはShift-Jisでハッシュを求める必要がある
                byte[] hash = hs256.ComputeHash(dst_encoding.GetBytes(sigstr));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// 結果が正しく大文字となるように、パーセントエンコーディングを行う
        /// 例えば、%a0 -> %A0 となるようにする
        /// </summary>
        /// <param name="input">入力文字列</param>
        /// <param name="encoding">元の文字列のエンコーディング指定</param>
        /// <returns>パーセントエンコーディング済み文字列</returns>
        private string URLEncode(string input, Encoding encoding)
        {
            // HttpUtility.UrlEncodeの結果は%xxのxxが小文字になる
            // RFC的には大文字が正しいらしい
            string lowerstr = HttpUtility.UrlEncode(input, encoding);
            return Regex.Replace(lowerstr, @"%[\x00-\xFF]{2}", m => m.Value.ToUpperInvariant());
        }

        private string ReConstructPostField(string[] field_order, Dictionary<string, string> post_filed_map, Encoding dst_encoding)
        {
            string postfield = "";

            // 順序指定があるものをその順序で指定する
            foreach (string key in field_order)
            {
                // データが送られてきてる場合のみ追加（無い場合に空文字を追加しない）
                if (post_filed_map.ContainsKey(key))
                {
                    postfield += $"{key}={URLEncode(post_filed_map[key], dst_encoding)}&";
                }
            }

            // 順序指定が無いものは送られてきた順序で（ほんまか？Dictionalyの内部順序って何？？）
            foreach (var kvpair in post_filed_map.Where(kv => !field_order.Contains(kv.Key)))
            {
                postfield += $"{kvpair.Key}={URLEncode(kvpair.Value, dst_encoding)}&";
            }

            // 余分にくっついてるのを削除
            return postfield.TrimEnd('&');
            //return $"FROM={value_or(post_filed, "FROM")}&mail={value_or(post_filed, "mail")}&MESSAGE={value_or(post_filed, "MESSAGE")}&bbs={value_or(post_filed, "bbs")}&key={value_or(post_filed, "key")}&time={value_or(post_filed, "time")}&submit={value_or(post_filed, "submit")}";
        }

        private void ResPostv2(Session oSession, bool is2ch)
        {
            try
            {
                String ReqBody = oSession.GetRequestBodyAsString();
                // ギコナビ、レス投稿時にもsubject=が付いてる対策
                ReqBody = ReqBody.Replace("subject=&", "");
                // 主にギコナビ、submitに改行が入っている
                ReqBody = ReqBody.Replace("\r\n", "");
                // スレ立てと書き込みを識別する、同じbbs.cgiを使用しているため
                bool IsResPost = !ReqBody.Contains("subject="); // trueの時レス投稿

                // 昔はスレ立ては別だったらしい
                if (oSession.fullUrl.Contains("subbbs.cgi"))
                {
                    oSession.fullUrl = oSession.fullUrl.Replace("subbbs.cgi", "bbs.cgi");
                }

                String PostURI = (ViewModel.Setting.UseTLSWrite) ? (oSession.fullUrl.Replace("http://", "https://")) : (oSession.fullUrl);
                HttpWebRequest Write = (HttpWebRequest)WebRequest.Create(PostURI);
                Write.Method = "POST";
                Write.ServicePoint.Expect100Continue = false;
                Write.Headers.Clear();
                //ここで指定しないとデコードされない
                Write.AutomaticDecompression = DecompressionMethods.GZip;
                //Write.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                //デフォルトがtrueなのでオフっとく
                Write.KeepAlive = false;
                Write.Connection = null;    // こうしないとヘッダから消えない
                // デフォルト1.0にしておく
                Write.ProtocolVersion = HttpVersion.Version10;

                //デバッグ出力
                System.Diagnostics.Debug.WriteLine("オリジナルリクエストヘッダ");
                foreach (var header in oSession.RequestHeaders)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Name}:{header.Value}");
                }


                // 板毎設定の引き当て
                BoardSettings PostSetting = null;
                if (1 < BoardSettings.Count())
                {
                    // 板名を抽出
                    var bbs_match = Regex.Match(ReqBody, @"bbs=(\w+)");
                    if (bbs_match.Success)
                    {
                        var bbs = bbs_match.Groups[1].Value;
                        if (BoardSettings.ContainsKey(bbs))
                        {
                            PostSetting = BoardSettings[bbs];
                        }
                    }
                }

                // デフォルトUAを設定（優先度最低、空の時は知らない）
                string UA = BoardSettings["2chapiproxy_default"].UserAgent;

                // UAの設定
                // 書き込みUAがあればそれを使用、無ければ板毎設定、それもなければデフォルト設定
                if (String.IsNullOrEmpty(WriteUA))
                {
                    // 設定があるときだけ上書き
                    if (string.IsNullOrEmpty(PostSetting?.UserAgent) == false)
                    {
                        UA = PostSetting.UserAgent;
                        // お絵かき設定はどうしようね・・・
                        if (PostSetting.Headers.Count() == 0) PostSetting = null;
                    }
                }
                else
                {
                    // UIの書き込みUAを私用
                    UA = WriteUA;
                }

                // 新しい書き込み仕様への対応

                // 送信されてきたエンコーディング取得
                var src_encoding = (AssumeReqBodyIsUTF8 || oSession.RequestHeaders["Content-Type"].Contains("UTF-8")) switch
                {
                    true => Encoding.UTF8,
                    false => Encoding.GetEncoding("Shift_JIS")
                };
                // 送信するエンコーディング取得
                var dst_encoding = EnableUTF8Post switch
                {
                    true => Encoding.UTF8,
                    false => Encoding.GetEncoding("Shift_JIS")
                };

                //ReqBody += "&sid=Monazilla/2.00:08urgq8vn478951437vn89574389v7843y584vht";

                // リクエストボディの分解（URLデコードもしておく）
                var post_field_map = ReqBody.Split('&')
                                    .Select(kvpair => kvpair.Split('='))
                                    .ToDictionary(pair => pair[0], pair => HttpUtility.UrlDecode(pair[1], src_encoding));

                // 送信されてきたクッキーを抽出
                var recv_cookie = new Dictionary<string, string>();
                foreach (Match mc in Regex.Matches(oSession.oRequest.headers["Cookie"], @"(?:\s+|^)((.+?)=(?:|.+?)(?:;|$))"))
                {
                    recv_cookie[mc.Groups[2].Value] = mc.Groups[1].Value;
                }

                // referer調整
                String referer = oSession.oRequest.headers["Referer"];
                if (IsResPost && SetReferrer && Regex.IsMatch(referer, @"https?://\w+\.(?:(?:2|5)ch\.net|bbspink\.com)/test/read\.cgi/\w+/\d{9,}") == false)
                {
                    var bbs = post_field_map["bbs"];
                    var key = post_field_map["key"];
                    referer = @$"https://{Write.Host}/test/read.cgi/{bbs}/{key}/";
                }
                else
                {
                    referer = oSession.oRequest.headers["Referer"].Replace("2ch.net", "5ch.net").Replace("http:", "https:");
                }
                Write.Referer = referer;

                // nonceの取得
                //string nonce = string.Format("{0}.{1:000}", (ulong)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds, DateTime.UtcNow.Millisecond);
                string nonce = AddMsToNonce switch
                {
                    true => string.Format("{0}.{1:000}", post_field_map["time"], DateTime.UtcNow.Millisecond),
                    false => post_field_map["time"].ToString()
                };

                // 各種値の計算とヘッダセット
                Write.Headers.Add("X-PostSig", CreatePostsignature(post_field_map, nonce, UA, dst_encoding));
                Write.Headers.Add("X-APIKey", this.APIMediator.AppKey);
                Write.Headers.Add("X-PostNonce", nonce);
                Write.Headers.Add("X-MonaKey", Monakey);
                if (AddX2chUAHeader) Write.Headers.Add("X-2ch-UA", APIMediator.X2chUA);

                // 浪人sidを適切に再配置
                if (ViewModel.Setting.PostRoninInvalid == false)
                {
                    if (post_field_map.ContainsKey("sid"))
                    {
                        // sidフィールドにある場合（専ブラ）
                        Write.Headers.Add("X-Ronin-Sid", post_field_map["sid"]);
                    }
                    else if (recv_cookie.TryGetValue("sid", out string sid_cookie))
                    {
                        // エンコーディングは何が正しい？全角文字は入らないから気にしなくていい・・・？
                        // sid=Monazilla/2.00:xxxxx.... の形式なので、: = /の3つがエンコードされるだけ？
                        var m = Regex.Match(HttpUtility.UrlDecode(sid_cookie, src_encoding), @"Monazilla/\d.\d\d:\w+");

                        if (m.Success)
                        {
                            // クッキーにある場合（一般ブラウザ、sikiなど？）
                            // クッキーはsid=xxxxの形で保存されてる（はず
                            Write.Headers.Add("X-Ronin-Sid", m.Value);
                        }
                    }
                }
                // 新仕様ではこのフィールドはなさそうなので削除
                post_field_map.Remove("sid");

                // UA設定
                Write.UserAgent = UA;

                // 板毎設定がなければ、デフォルト設定を引き当て
                PostSetting ??= BoardSettings["2chapiproxy_default"];

                // ヘッダの設定

                // 個別の設定項目があるやつ
                if (PostSetting.Headers.ContainsKey("Accept") == true)
                {
                    Write.Accept = PostSetting.Headers["Accept"];
                }
                if (PostSetting.Headers.ContainsKey("Expect") == true)
                {
                    Write.Expect = PostSetting.Headers["Expect"];
                }
                if (PostSetting.Headers.ContainsKey("Content-Type") == true)
                {
                    Write.ContentType = PostSetting.Headers["Content-Type"];
                }
                if (PostSetting.KeepAlive)
                {
                    Write.KeepAlive = true;
                    //Write.Connection = PostSetting.Headers["Connection"];
                }

                // 直接設定できるのはまとめて
                foreach (var header in PostSetting.Headers)
                {
                    try
                    {
                        if (Regex.IsMatch(header.Key, @"(^HTTPVer$|^Accept$|^User-Agent$|^Expect$|^Content-Type$|^Connection$|^Cookie$)") == true) continue;
                        Write.Headers.Add(header.Key, header.Value);
                    }
                    catch (Exception err)
                    {
                        ViewModel.OnModelNotice($"{header.Key}ヘッダは設定できません。");
                        System.Diagnostics.Debug.WriteLine("●ヘッダ定義の適用中のエラー\n" + err.ToString());
                    }
                }

                // これ順番ここじゃなきゃだめ？
                if (PostSetting.Headers.ContainsKey("HTTPVer") == true)
                {
                    if (PostSetting.Headers["HTTPVer"] == "1.0")
                    {
                        Write.ProtocolVersion = HttpVersion.Version10;
                    }
                }

                if (IsResPost)
                {
                    // 投稿設定でお絵描きデータを付加する設定になっていて、フィールドに含まれていない場合
                    if (PostSetting.SetOekaki && post_field_map.ContainsKey("oekaki_thread1") == false)
                    {
                        post_field_map.Add("oekaki_thread1", "");
                    }
                    else if (PostSetting.SetOekaki == false && post_field_map.ContainsKey("oekaki_thread1"))
                    {
                        // 逆にお絵描きデータ追加が無効になっている場合
                        post_field_map.Remove("oekaki_thread1");
                    }
                }

                // feature=confirmedを消すようにする
                // Xenoは送ってくることがあるらしい
                post_field_map.Remove("feature");

                // リクエストボディ再構成
                // レス投稿時とスレ立て時でどのブラウザもフィールド順序は異なっているらしい
                ReqBody = IsResPost switch
                {
                    true => ReConstructPostField(PostFieldOrederArray, post_field_map, dst_encoding),
                    false => ReConstructPostField(ThreadPostFieldOrederArray, post_field_map, dst_encoding)
                };

                if (string.IsNullOrEmpty(Proxy) == false) Write.Proxy = new WebProxy(Proxy);

                // Beログイン用クッキーのセット
                if (recv_cookie.ContainsKey("DMDM") || recv_cookie.ContainsKey("MDMD"))
                {
                    String domain = CheckWriteuri.Match(oSession.fullUrl).Groups[1].Value;

                    foreach (var cook in recv_cookie.Where(kv => (kv.Key == "DMDM") || (kv.Key == "MDMD")))
                    {
                        if (cook.Value != "")
                        {
                            // クッキーが確実にあるときだけクッキーコンテナを初期化
                            Write.CookieContainer ??= new CookieContainer();

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
                }

                byte[] Body = dst_encoding.GetBytes(ReqBody);
                if (EnableUTF8Post)
                {
                    // UTF-8でポスト（BordSettingの設定を強制上書きしている・・・
                    Write.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                }

                Write.ContentLength = Body.Length;

                try
                {
                    using (System.IO.Stream PostStream = Write.GetRequestStream())
                    {
                        PostStream.Write(Body, 0, Body.Length);

                        HttpWebResponse wres = (HttpWebResponse)Write.GetResponse();

                        // クッキー抽出と設定
                        if (wres.Cookies.Count > 0)
                        {
                            var cul = new System.Globalization.CultureInfo("en-US");
                            foreach (System.Net.Cookie cookie in wres.Cookies)
                            {
                                String tc = cookie.ToString();
                                if (cookie.Expires != null) tc += "; expires=" + cookie.Expires.ToUniversalTime().ToString("ddd, dd-MMM-yyyy HH:mm:ss", cul) + " GMT";
                                if (!String.IsNullOrEmpty(cookie.Path)) tc += "; path=" + cookie.Path;
                                if (!String.IsNullOrEmpty(cookie.Domain)) tc += "; domain=" + ((is2ch) ? (cookie.Domain.Replace("5ch.net", "2ch.net")) : (cookie.Domain));
                                oSession.oResponse.headers.Add("Set-Cookie", tc);
                            }
                        }

                        // MonaKeyの更新
                        if (wres.Headers.AllKeys.Contains("X-MonaKey") == true)
                        {
                            this.Monakey = wres.Headers["X-MonaKey"];
                            ViewModel.OnModelNotice("MonaKeyを更新しました。");

                            // 5秒待機する
                            Thread.Sleep(5000);
                        }
                        // ここでelseとしていることで 0001 Confirmation phase の場合にログを出さない
                        else if (wres.Headers.AllKeys.Contains("X-Chx-Error") == true)
                        {
                            // Monakeyが送られてきておらず、X-Chx-Errorヘッダがセットされている場合、なんかエラー

                            ViewModel.OnModelNotice("X-Chx-Error : " + wres.Headers["X-Chx-Error"]);

                            // E3300番台のエラーが帰ってきたらMonaKeyを更新する（雑な暫定対応
                            // E3331 Invalid signature.はリセットの必要がない（Postsigの計算が間違ってる）
                            if (wres.Headers["X-Chx-Error"].Contains("E3331") == false && wres.Headers["X-Chx-Error"].Contains("E33"))
                            {
                                ResetMonakey();
                            }

                            // 鍵の有効期限切れ（と思われる）場合は出力しない
                            if (wres.Headers["X-Chx-Error"].Contains("E3324") == false)
                            {
                                string header_log = "リクエストヘッダ\n";
                                foreach (var header in Write.Headers.AllKeys)
                                {
                                    header_log += $"{header} : {Write.Headers[header]}\n";
                                }

                                header_log += "\nレスポンスヘッダ\n";
                                foreach (var header in wres.Headers.AllKeys)
                                {
                                    header_log += $"{header} : {wres.Headers[header]}\n";
                                }
                                ViewModel.OnModelNotice(header_log);
                            }
                        }

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
                            //oSession.utilSetResponseBody(Res.ReadToEnd());
                            //if (gZipRes) oSession.utilGZIPResponse();
                        }

                        System.Diagnostics.Debug.WriteLine("リクエストヘッダ");
                        foreach (var header in Write.Headers.AllKeys)
                        {
                            System.Diagnostics.Debug.WriteLine($"{header}:{Write.Headers[header].ToString()}");
                        }

                        System.Diagnostics.Debug.WriteLine("レスポンスヘッダ");
                        foreach (var header in wres.Headers.AllKeys)
                        {
                            System.Diagnostics.Debug.WriteLine($"{header}:{wres.Headers[header].ToString()}");
                        }

                        if (wres != null) wres.Close();
                        return;
                    }
                }
                catch (WebException err)
                {
                    ViewModel.OnModelNotice("書き込み中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                    return;
                }
                catch (NullReferenceException err)
                {
                    ViewModel.OnModelNotice("書き込み中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                    return;
                }
            }
            catch (Exception err)
            {
                oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=Shift_JIS";
                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                oSession.oResponse.headers["Connection"] = "Close";
                oSession.utilSetResponseBody("2chAPIProxy書き込み処理中にエラーが発生しました。\n" + err.ToString());
                ViewModel.OnModelNotice("書き込み部でエラーです。\n" + err.ToString());
            }
            return;
        }

        private void intervene_in_dat_response(ref Session oSession, bool is2ch, string thread_url, bool accessing_kakolog)
        {
            try
            {
                if (is2ch && string.IsNullOrEmpty(oSession.oResponse.headers["Set-Cookie"]) == false)
                {
                    // クッキーのホストを変換
                    oSession.oResponse.headers["Set-Cookie"] = oSession.oResponse.headers["Set-Cookie"].Replace("5ch.net", "2ch.net");
                }

                switch (oSession.responseCode)
                {
                    case 206:
                        // 差分取得
                        if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                        {
                            var resdat = Encoding.GetEncoding("Shift_JIS").GetString(oSession.responseBodyBytes);
                            resdat = HtmlConverter.ResContentReplace(resdat);
                            oSession.ResponseBody = Encoding.GetEncoding("Shift_JIS").GetBytes(resdat);
                        }
                        return;
                    case 200:
                        // 全件取得
                        if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink || CRReplace)
                        {
                            // 全件取得時のみgzip圧縮されている
                            oSession.utilDecodeResponse();

                            var resdat = Encoding.GetEncoding("Shift_JIS").GetString(oSession.responseBodyBytes);

                            if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                            {
                                resdat = HtmlConverter.ResContentReplace(resdat);
                            }

                            if (CRReplace)
                            {
                                // スレタイの©マークを置換
                                var re = new Regex(@"^(.+?<>.*?<>.+?<>.+?<>.+?)&#169;(.+?\t)");

                                if (re.IsMatch(resdat))
                                {
                                    // 正確にスレタイに含まれているもののみ置換
                                    resdat = re.Replace(resdat, (match) => { return $"{match.Groups[1].Value}&copy;{match.Groups[2].Value}"; }, 1);
                                }
                            }

                            oSession.ResponseBody = Encoding.GetEncoding("Shift_JIS").GetBytes(resdat);
                        }

                        if (gZipRes) oSession.utilGZIPResponse();
                        return;
                    case 301:
                    case 404:
                    case 302:
                        // dat落ち

                        // 過去ログのHTML変換を行うかどうかを判定
                        bool is_convert = accessing_kakolog switch
                        {
                            true => GetHTML && KakolinkPerm,                // 過去ログ倉庫で見つからなかった場合 : 過去ログ変換が有効 かつ 過去ログ倉庫へのアクセス置換が有効
                            false => GetHTML && !NotReplaceNormalDatAccess  // 通常datアクセス時にdat落ちの場合 : 過去ログ変換が有効 かつ dat落ち検出時の変換が有効
                        };

                        if (is_convert)
                        {
                            // html変換 and 差分応答
                            String last = oSession.oRequest.headers["If-Modified-Since"], hrange = oSession.oRequest.headers["Range"];
                            if (String.IsNullOrEmpty(last)) last = "1970/12/1";

                            int range = String.IsNullOrEmpty(hrange) switch
                            {
                                true => -1,
                                false => int.Parse(Regex.Match(hrange, @"\d+").Value)
                            };

                            byte[] Htmldat = null;
                            string UA = oSession.oRequest.headers["User-Agent"];

                            System.Threading.Thread HtmlTranceThread = new System.Threading.Thread(() =>
                            {
                                try
                                {
                                    //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                                    //sw.Start();
                                    //Htmldat = HTMLtoDat.Gethtml(uri, range, UA, CRReplace, (last != "1970/12/1") ? (last) : (null));
                                    Htmldat = HtmlConverter.Gethtml(thread_url, range, UA, CRReplace, (last != "1970/12/1") ? (last) : (null));
                                    //sw.Stop();
                                    //System.Diagnostics.Debug.WriteLine("処理時間：" + sw.ElapsedMilliseconds + "ms");
                                }
                                catch (System.Threading.ThreadAbortException)
                                {
                                    ViewModel.OnModelNotice("タイムアウトによりHTML変換スレッドを中断。\nURI:" + thread_url);
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

                            // 長さ3以上なら成功のはず
                            if (3 <= Htmldat.Length)
                            {
                                ViewModel.OnModelNotice(thread_url + " をhtmlから変換");
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
                                return;
                            }
                        }
                        // dat落ち応答を404 -> 302に変換
                        oSession.oResponse.headers.HTTPResponseCode = 302;
                        oSession.oResponse.headers.HTTPResponseStatus = "302 Found";
                        oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                        return;
                    default:
                        // その他の場合
                        // 416 : あぼーん
                        // 304 : 更新無し
                        return;
                }

            }
            catch (Exception err)
            {
                ViewModel.OnModelNotice("dat応答介入時にエラーです。\n" + err.ToString());
            }
        }

        private void GetDat(ref Session oSession, bool is2ch)
        {
            // API以前のふるまいについて http://age.s22.xrea.com/talk2ch/
            try
            {
                String last = oSession.oRequest.headers["If-Modified-Since"], hrange = oSession.oRequest.headers["Range"];
                if (String.IsNullOrEmpty(last)) last = "1970/12/1";

                //int range = (!String.IsNullOrEmpty(hrange)) ? (int.Parse(Regex.Match(hrange, @"\d+").Value)) : (-1);
                int range = String.IsNullOrEmpty(hrange) switch
                {
                    true => -1,
                    false => int.Parse(Regex.Match(hrange, @"\d+").Value)
                };

                //スレッドステータス
                int Status = 0;

                //デバッグ出力
                System.Diagnostics.Debug.WriteLine($"オリジナルdatリクエストヘッダ(range:{range}, last:{last})");
                foreach (var header in oSession.RequestHeaders)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Name}:{header.Value}");
                }
                
                Match ch2uri = CheckDaturi.Match(oSession.fullUrl);
                HttpWebResponse dat;

                datget:
                try
                {
                    dat = APIMediator.GetDat(ch2uri.Groups[1].Value, ch2uri.Groups[3].Value, ch2uri.Groups[4].Value, range, last);
                }
                catch (Exception err)
                {
                    ViewModel.OnModelNotice("datアクセス中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.HTTPResponseCode = 304;
                    oSession.oResponse.headers.HTTPResponseStatus = "304 Not Modified";
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Connection"] = "close";
                    return;
                }
                //bool bat = CheckAlive(@"http://itest.2ch.net/public/newapi/client.php?subdomain=" + ch2uri.Groups[1].Value + "&board=" + ch2uri.Groups[3].Value + "&dat=" + ch2uri.Groups[4].Value);
                //ViewModel.OnModelNotice("生存判定：" + bat);

                if (dat == null)
                {
                    ViewModel.OnModelNotice("datの取得に失敗しました。");
                    if (oSession.oRequest.headers["User-Agent"].Contains("Jane"))
                    {
                        oSession.oResponse.headers.SetStatus(504, "504 Gateway Timeout");
                    }
                    else
                    {
                        oSession.oResponse.headers.SetStatus(304, "304 Not Modified");
                    }
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Connection"] = "close";

                    return;
                }


                System.Diagnostics.Debug.WriteLine($"datレスポンスヘッダ(status={dat.StatusCode})");
                foreach (var header in dat.Headers.AllKeys)
                {
                    System.Diagnostics.Debug.WriteLine($"{header}:{dat.Headers[header].ToString()}");
                }

                // SID期限切れ（無効）時にSID更新後リトライするとき、それを検出し制御する
                bool retry_on_sidupdate = true;

                bool? is_alive = null;

                switch (dat.StatusCode)
                {
                    case HttpStatusCode.PartialContent:
                        // あぼーん検出のため、一部の専ブラは取得済datサイズ-1のサイズを指定して取得しようとする
                        // API以前（初期も？）はその際304を返していたが、いつからか206を返してくるようになったらしい
                        // サイズを調べて304で応答する（ギコナビはもしかしたら16とかかもしれない？
                        if (dat.ContentLength == 1)
                        {
                            goto case HttpStatusCode.NotModified;
                        }
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
                                //resdat = HTMLtoDat.ResContentReplace(resdat);
                                resdat = HtmlConverter.ResContentReplace(resdat);
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
                                //resdat = HTMLtoDat.ResContentReplace(resdat);
                                resdat = HtmlConverter.ResContentReplace(resdat);
                            }
                            oSession.ResponseBody = Encoding.GetEncoding("Shift_JIS").GetBytes(resdat);
                        }
                        oSession.oResponse.headers.HTTPResponseCode = 200;
                        oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                        oSession.oResponse.headers["Content-Type"] = "text/plain";
                        oSession.oResponse.headers["Last-Modified"] = dat.Headers[HttpResponseHeader.LastModified];
                        oSession.oResponse.headers["ETag"] = dat.Headers[HttpResponseHeader.ETag];
                        if (gZipRes) oSession.utilGZIPResponse();
                        break;
                    case HttpStatusCode.NotImplemented:
                        if (!GetHTML || NotReplaceNormalDatAccess)
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 302;
                            oSession.oResponse.headers.HTTPResponseStatus = "302 Found";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }
                        goto case HttpStatusCode.InternalServerError;
                    case HttpStatusCode.Unauthorized:
                        // 例えば連続で更新をかけた場合など、複数スレッドから呼ばれうる？
                        // retryはローカル変数なので問題ない
                        // SIDNowUpdateはグローバル（クラススコープ）だけど、volatile boolなので読み書きはatomicになる（はず
                        if (!retry_on_sidupdate || SIDNowUpdate)
                        {
                            // SIDアプデ中は何もせず終わる
                            // retry == falseのとき、更新（エラー）後2回目のdat取得。ここにきているということはSID更新に失敗している。

                            if (SIDNowUpdate)
                            {
                                ViewModel.OnModelNotice("403応答によるSessionID更新を10秒間停止中です、しばらくお待ちください。");
                            }

                            goto case HttpStatusCode.NotModified;
                        }
                        SIDNowUpdate = true;

                        try
                        {
                            APIMediator.UpdateSID();
                            ViewModel.OnModelNotice("SessionIDを更新しました。（期限切れ）");
                        }
                        catch (Exception err)
                        {
                            ViewModel.OnModelNotice("SessionIDの更新に失敗しました\n" + err.ToString());
                        }
                        dat.Close();

                        // 403応答によるSID更新を10秒間ブロックする
                        // 更新直後はもちろん、更新失敗した時も、連打するのは無意味
                        System.Threading.Timer ReleaseSIDUpdate = null;
                        ReleaseSIDUpdate = new System.Threading.Timer((e) =>
                        {
                            using (ReleaseSIDUpdate)
                            {
                                SIDNowUpdate = false;
                            }
                        }, null, 10000, System.Threading.Timeout.Infinite);

                        // 更新されたSIDを用いてdatを再取得
                        retry_on_sidupdate = false;
                        goto datget;
                    case HttpStatusCode.InternalServerError:
                        Byte[] Htmldat = null;
                        String uri = @"https://" + ch2uri.Groups[1].Value + "." + ch2uri.Groups[2].Value + "/test/read.cgi/" + ch2uri.Groups[3].Value + @"/" + ch2uri.Groups[4].Value + @"/";
                        if (GetHTML && !NotReplaceNormalDatAccess)
                        {
                            String UA = oSession.oRequest.headers["User-Agent"];
                            System.Threading.Thread HtmlTranceThread = new System.Threading.Thread(() =>
                            {
                                try
                                {
                                    //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                                    //sw.Start();
                                    //Htmldat = HTMLtoDat.Gethtml(uri, range, UA, CRReplace, (last != "1970/12/1") ? (last) : (null));
                                    Htmldat = HtmlConverter.Gethtml(uri, range, UA, CRReplace, (last != "1970/12/1") ? (last) : (null));
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
                            is_alive ??= CheckAlive(@"http://itest.2ch.net/public/newapi/client.php?subdomain=" + ch2uri.Groups[1].Value + "&board=" + ch2uri.Groups[3].Value + "&dat=" + ch2uri.Groups[4].Value);

                            Htmldat = is_alive switch
                            {
                                true => new byte[] { 0, 0 },
                                false => new byte[] { 0 },
                                null => new byte[] { 0, 0 } // 起こりえない
                            };
                        }

                        // Htmldat.Length == 2 : スレッドは生存している
                        // Htmldat.Length == 1 : スレッドは生存していない
                        if (Htmldat.Length == 2 && Status < 2) goto case HttpStatusCode.NotModified;
                        if (Htmldat.Length == 1 || (Htmldat.Length == 2 && Status >= 2))
                        {
                            oSession.oResponse.headers.SetStatus(302, "302 Found");
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }

                        // Htmldat.Lengthが3以上ならば変換成功のはず
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
                        // is_aliveはここに来るときはnullであるはず
                        if (CheckAlive(@"http://itest.2ch.net/public/newapi/client.php?subdomain=" + ch2uri.Groups[1].Value + "&board=" + ch2uri.Groups[3].Value + "&dat=" + ch2uri.Groups[4].Value))
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 416;
                            oSession.oResponse.headers.HTTPResponseStatus = "416 Requested range not satisfiable";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }
                        else
                        {
                            is_alive = false;
                            goto case HttpStatusCode.NotImplemented;
                        }
                    default:
                        oSession.oResponse.headers.HTTPResponseCode = (int)dat.StatusCode;
                        oSession.oResponse.headers.HTTPResponseStatus = (int)dat.StatusCode + " " + dat.StatusDescription;
                        oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                        break;
                }
                oSession.oResponse.headers["Date"] = dat.Headers[HttpResponseHeader.Date];
                oSession.oResponse.headers["Set-Cookie"] = (is2ch) ? (dat.Headers[HttpResponseHeader.SetCookie]?.Replace("5ch.net", "2ch.net")) : (dat.Headers[HttpResponseHeader.SetCookie]);
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
                //if (ViewModel.Setting.Use5chnet) URI.Replace("2ch.net", "5ch.net");
                URI = URI.Replace("2ch.net", "5ch.net");
                using (WebClient get = new WebClient())
                {
                    get.Headers["User-Agent"] = HtmlConverter.UserAgent;
                    if (Proxy != "") get.Proxy = new WebProxy(Proxy);
                    using (System.IO.StreamReader html = new System.IO.StreamReader(get.OpenRead(URI), Encoding.GetEncoding("Shift_JIS")))
                    {
                        if (html.EndOfStream) return false;
                        else return true;
                        //for (int i = 0; i < 40 && !html.EndOfStream; ++i)
                        //{
                        //    String res = html.ReadLine();
                        //    //if (res.IndexOf(">■ このスレッドは過去ログ倉庫に格納されています<") >= 0) return false;
                        //    if (Regex.IsMatch(res, @"<div\s.+?>.*?(過去ログ倉庫に格納されています|レス数が1000を超えています).*?<\/div>")) return false;
                        //    if (Regex.IsMatch(res, @"(２ちゃんねる error \d+|(.+)?datが存在しません.削除されたかURL間違ってますよ)")) return false;
                        //}
                        //return true;
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

        public Task UpdateAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                APIMediator.UpdateSID();
            });
        }
    }
}

//http://www2.hatenadiary.jp/entry/2013/12/11/215927
//1050まであるスレ
//http://news.2ch.net/test/read.cgi/newsplus/1023016978/