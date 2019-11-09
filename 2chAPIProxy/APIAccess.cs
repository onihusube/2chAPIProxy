using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net;
using System.Web;
using System.IO;
using System.Threading;

namespace _2chAPIProxy
{
    class HoboData
    {
        public short Count { get; set; }

        Byte[] m_hobo;
        public Byte[] hobo
        {
            get { if (Count < 11) ++Count; return m_hobo; }
            set { m_hobo = value; }
        }

        public HoboData(short count, Byte[] data)
        {
            Count = count;
            m_hobo = data;
        }
    }

    public partial class APIAccess
    {
        public String AppKey { get; set; }

        public String HMKey { get; set; } = "";

        public String X2chUA { get; set; } = "";

        public String SidUA { get; set; } = "";

        public String DatUA { get; set; } = "";

        public String RouninID { get; set; } = "";

        public String RouninPW { get; set; } = "";

        public String Proxy { get; set; } = "";

        readonly string m_DefaultSID = "24435386Z78507D59284D46893Z55945T45741Z29183f65630d66139T82258c3442O53506n58942M48038D83651D14687r50234R6786f19427I86154p86883o54015c71781T19953D19830n36479K17338A62340746Z84798";

        String m_SID = "";

        Timer m_HoboCheck;

        short sidup = 0;

        //hoboキーキャッシュ、鯖キー/板キー/スレキーがキー
        //その中のHoboData.hoboがhoboキー、HoboData.Countが更新回数
        //1件当たりおよそ350Byte（推定）
        //Dictionary<String, HoboData> HoboCache = new Dictionary<String, HoboData>();
        System.Collections.Concurrent.ConcurrentDictionary<String, HoboData> m_HoboCache = new System.Collections.Concurrent.ConcurrentDictionary<string, HoboData>();
        //API鯖のURLリスト
        readonly String[] ApiServerUri = new string[] { "https://api.2ch.net", "https://api.5ch.net" };
        public bool GetSIDFailed { get; set; }

        public APIAccess(String Appkey, String HMkey, String x2chua, String sidUA, String datUA, String RID, String RPW, String proxy)
        {
            //必要なキー等を初期化
            this.AppKey = HttpUtility.UrlEncode(AppKey);
            this.HMKey = HttpUtility.UrlEncode(HMKey);
            this.X2chUA = X2chUA;
            this.SidUA = SidUA;
            this.DatUA = DatUA;
            this.Proxy = Proxy;

            m_HoboCheck = new Timer((o) => {
                //更新回数2回以下のデータを削除
                var rmkey = from db in m_HoboCache.ToArray().AsParallel()
                            where db.Value.Count < 2
                            select db.Key;
                HoboData value;
                foreach (var key in rmkey) m_HoboCache.TryRemove(key, out value);
                //更新回数5回以下のデータをリセット
                var rkey = from db in m_HoboCache.ToArray().AsParallel()
                           where db.Value.Count <= 5
                           select db.Key;
                foreach (var key in rkey) m_HoboCache[key].Count = 0;
                //更新回数6回以上のデータを5にセット
                var skey = from db in m_HoboCache.ToArray().AsParallel()
                           where db.Value.Count > 5
                           select db.Key;
                foreach (var key in skey) m_HoboCache[key].Count = 5;
                sidup += 8;
                if (sidup >= 24)
                {
                    try
                    {
                        GetSid();
                        if (!GetSIDFailed) ViewModel.OnModelNotice("SessionIDを更新しました。（自動更新）");
                        GetSIDFailed = false;
                    }
                    catch (Exception err) 
                    {
                        ViewModel.OnModelNotice("SessionIDの更新に失敗しました。\n" + err.ToString());
                    }
                    sidup = 0;
                }
                m_HoboCheck.Change(3600000 * 8, Timeout.Infinite);
            }, null, 3600000 * 8, Timeout.Infinite);

            UpdateKey(HttpUtility.UrlEncode(Appkey), HttpUtility.UrlEncode(HMkey), x2chua, sidUA, datUA, RID, RPW, proxy);
            GetSIDFailed = false;
        }

        ~APIAccess()
        {
            using (m_HoboCheck){ }
        }

        public void UpdateKey(String Akey, String Hkey, String ua1, String sidUA, String ua2, String RID, String RPW, String proxy)
        {
            String oa = AppKey, oh = HMKey, oi = RouninID, op = RouninPW;
            AppKey = Akey;
            HMKey = Hkey;
            X2chUA = ua1;
            this.SidUA = (sidUA == "") ? (ua2) : (sidUA);
            //this.sidUA = sidUA;
            DatUA = ua2;
            RouninID = RID;
            RouninPW = RPW;
            Proxy = proxy;
            try
            {
                if (oa != Akey || oh != Hkey || oi != RID || op != RPW) 
                { 
                    GetSid();
                    //if (!GetSIDFailed) ViewModel.OnModelNotice("各キーとSessionIDを更新しました。");
                    //else ViewModel.OnModelNotice("各キーを更新しました。");
                }
                m_HoboCheck.Change(3600000 * 8, Timeout.Infinite);
            }
            catch (Exception err)
            {
                ViewModel.OnModelNotice(err.ToString());
                return;
            }
        }

        String KeyGen(String message)
        {
            using (HMACSHA256 hs256 = new HMACSHA256(Encoding.UTF8.GetBytes(HMKey)))
            {
                byte[] hash = hs256.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        public void GetSid()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | (SecurityProtocolType)768 | (SecurityProtocolType)3072;
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
            }
            
            HttpWebRequest APIRequest = (HttpWebRequest)WebRequest.Create(ApiServerUri[1] + "/v1/auth/");
            try
            {
                APIRequest.Proxy = (Proxy != "") ? (new WebProxy(Proxy)) : (null);
            }
            catch (UriFormatException)
            {
                APIRequest.Proxy = null;
            }
            APIRequest.Method = "POST";
            APIRequest.UserAgent = this.SidUA;
            var res = X2chUA.Split(':');
            APIRequest.Headers.Add("X-2ch-UA", (res.Length == 1) ? (X2chUA) : (res[1]));
            APIRequest.Timeout = 40000;
            APIRequest.ContentType = "application/x-www-form-urlencoded";
            APIRequest.ServicePoint.Expect100Continue = false;

            String Value = "ID=" + HttpUtility.UrlEncode(RouninID);
            Value += "&PW=" + HttpUtility.UrlEncode(RouninPW);
            Value += "&KY=" + HttpUtility.UrlEncode(AppKey);
            byte[] postData = Encoding.ASCII.GetBytes(Value);

            APIRequest.ContentLength = postData.Length;
            try
            {
                using (Stream PostStream = APIRequest.GetRequestStream())
                {
                    PostStream.Write(postData, 0, postData.Length);
                    WebResponse wres = APIRequest.GetResponse();
                    using (StreamReader Res = new StreamReader(wres.GetResponseStream()))
                    {
                        String ResData = Res.ReadToEnd();
                        //
                        m_SID = ResData.Split(':')[1];
                        if (ResData.IndexOf("ERROR:") > -1 || m_SID == "")
                        {
                            ViewModel.OnModelNotice("SessionIDの取得に失敗しました。\n" + ResData, true);
                            m_SID = m_DefaultSID;
                            GetSIDFailed = true;
                        }
                    }
                    ViewModel.RestartStatus = 13;
                    wres?.Close();
                }
            }
            catch (WebException err)
            {
                if (m_SID== "") m_SID = m_DefaultSID;
                if (ViewModel.RestartStatus > 0) ++ViewModel.RestartStatus;
                ViewModel.OnModelNotice("SessionIDの取得に失敗しました。\n" + err.ToString());
                err.Response?.Close();
                GetSIDFailed = true;
                return;
            }
            
            foreach (KeyValuePair<string, HoboData> key in m_HoboCache.AsParallel())
            {
                String hobo = KeyGen("/v1/" + key.Key + m_SID + AppKey);
                m_HoboCache[key.Key].hobo = Encoding.ASCII.GetBytes("sid=" + m_SID + "&hobo=" + hobo + "&appkey=" + AppKey);
            }
            m_HoboCheck.Change(3600000 * 8, Timeout.Infinite);
            sidup = 0;
        }

        public HttpWebResponse GetDat(String saba, String ita, String thread, int range, String lastmod, bool is2ch = true)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | (SecurityProtocolType)768 | (SecurityProtocolType)3072;
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
            }

            String hkey = saba + "/" + ita + "/" + thread;
            String host = ApiServerUri[1];
            HttpWebRequest datRequest = (HttpWebRequest)WebRequest.Create(host + "/v1/" + hkey);
            datRequest.Method = "POST";
            datRequest.UserAgent = DatUA;
            datRequest.KeepAlive = true;
            datRequest.ContentType = "application/x-www-form-urlencoded";
            datRequest.AutomaticDecompression = DecompressionMethods.GZip;
            datRequest.ServicePoint.Expect100Continue = false;
            datRequest.Timeout = 30000;
            try
            {
                if (Proxy != "") datRequest.Proxy = new WebProxy(Proxy);
            }
            catch (UriFormatException)
            {
                datRequest.Proxy = null;
            }
            if (-1 < range)
            {
                if (DateTime.TryParse(lastmod, out DateTime ifModifiedSince) == true)
                {
                    datRequest.IfModifiedSince = ifModifiedSince;
                }
                else
                {
                    datRequest.IfModifiedSince = DateTime.Parse("1970/12/1");
                }

                datRequest.AddRange(range);
            }

            if (m_HoboCache.ContainsKey(hkey) == false)
            {
                var hobo = Encoding.ASCII.GetBytes("sid=" + m_SID + "&hobo=" + KeyGen("/v1/" + hkey + m_SID + AppKey) + "&appkey=" + AppKey);
                m_HoboCache.TryAdd(hkey, new HoboData(1, hobo));
            }
            datRequest.ContentLength = m_HoboCache[hkey].hobo.Length;
            try
            {
                using (Stream PostStream = datRequest.GetRequestStream())
                {
                    PostStream.Write(m_HoboCache[hkey].hobo, 0, m_HoboCache[hkey].hobo.Length);
                    return (HttpWebResponse)datRequest.GetResponse();
                }
            }
            catch (WebException err)
            {
                return (HttpWebResponse)err.Response;
            }
        }
    }
}


//http://codepad.org/9ZfVq5aZ
//http://codepad.org/mxjxFd73
/*
 APIキー、バックアップ
 
 JaneStyle/3.80 
 HM:DgQ3aNpoluV1cl3GFJAqitBg5xKiXZ
 AP:xxfvFQcOzpTBvwuwPMwwzLZxiCSaGb
 JaneStyle/3.81
 HM:DgQ3aNpoluV1cl3GFJAqitBg5xKiXZ"
 AP:xxfvFQcOzpTBvwuwPMwwzLZxiCSaGb
 */