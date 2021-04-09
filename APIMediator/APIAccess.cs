using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;

namespace _2chAPIProxy.APIMediator
{
    /// <summary>
    /// 一度計算したHoboキーを保持するクラス
    /// </summary>
    class HoboData
    {
        /// <summary>
        /// 参照回数
        /// </summary>
        public short Count { get; set; }

        Byte[] m_hobo;

        /// <summary>
        /// Hoboキー
        /// </summary>
        public Byte[] Hobo
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

    public class APIAccess : IAPIMediator
    {
        //Hoboキーのキャッシュ
        ConcurrentDictionary<string, HoboData> m_HoboCache = new ConcurrentDictionary<string, HoboData>();

        //SID自動更新用タイマー
        Timer m_HoboCheck = null;

        //タイマーの間隔
        int m_ElapsedTime = 0;

        //デフォルトのSID、これでdat取得等をすると更新が帰ってくる
        static readonly string DefaultSID = "24435386Z78507D59284D46893Z55945T45741Z29183f65630d66139T82258c3442O53506n58942M48038D83651D14687r50234R6786f19427I86154p86883o54015c71781T19953D19830n36479K17338A62340746Z84798";

        string m_SID = DefaultSID;

        /// <summary>
        /// 現在のSID、INotifyPropertyChangedによる通知あり
        /// </summary>
        public String SessionID
        {
            get { return m_SID; }
            private set { this.SetProperty(ref m_SID, value, nameof(SessionID)); }
        }

        private readonly object m_SyncObj = new object();

        string m_CurrentError = "";
        /// <summary>
        /// 現在のエラー情報、INotifyPropertyChangedによる通知あり
        /// </summary>
        public string CurrentError
        {
            get => m_CurrentError;
            private set
            {
                //一回のPropertyChangedイベントが終わるまでは更新しないように
                lock (m_SyncObj)
                {
                    m_CurrentError = value;
                    this.NotifyPropertyChanged(nameof(CurrentError));
                }
            }
        }

        /// <summary>
        /// APIサーバのURIの先頭部分（https～ドメイン名まで、スラッシュはいらない）
        /// </summary>
        public String APIServerURI { get; set; } = "https://api.5ch.net";

        /// <summary>
        /// SID取得時タイムアウト時間[ミリ秒]
        /// </summary>
        public int GetSIDTimeout { get; set; } = 40 * 1000;

        /// <summary>
        /// Dat取得時タイムアウト時間[ミリ秒]
        /// </summary>
        public int GetDatTimeout { get; set; } = 30 * 1000;

        public string AppKey { get; set; }

        public string HMKey { get; set; }

        public string X2chUA { get; set; }

        public string SidUA { get; set; }

        public string DatUA { get; set; }

        public string RouninID { get; set; }

        public string RouninPW { get; set; }

        public string ProxyAddress { get; set; } = "";

        public APIAccess()
        {
            //可能であればTLS1.1/1.2/1.3を使用するように
            try
            {
                try
                {
                    //可能であればTLS1.2/1.3を使用するように
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288;
                }
                catch
                {
                    //だめならTLS1.0/1.1/1.2を使用するように
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | (SecurityProtocolType)768 | (SecurityProtocolType)3072;
                    CurrentError = "APIへのアクセスにTLS1.0/1.1/1.2を使用";
                }
            }
            catch
            {
                //最終手段
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
                CurrentError = "APIへのアクセスにSSL3.0/TLS1.0を使用";
            }

            //SID自動更新タイマー開始
            this.m_HoboCheck = new Timer((o) => {
                //更新回数2回以下のデータを削除
                var rmkey = from db in this.m_HoboCache.ToArray().AsParallel()
                            where db.Value.Count < 2
                            select db.Key;
                foreach (var key in rmkey) this.m_HoboCache.TryRemove(key, out HoboData value);
                //更新回数5回以下のデータをリセット
                var rkey = from db in this.m_HoboCache.ToArray().AsParallel()
                           where db.Value.Count <= 5
                           select db.Key;
                foreach (var key in rkey) this.m_HoboCache[key].Count = 0;
                //更新回数6回以上のデータを5にセット
                var skey = from db in this.m_HoboCache.ToArray().AsParallel()
                           where db.Value.Count > 5
                           select db.Key;
                foreach (var key in skey) this.m_HoboCache[key].Count = 5;

                this.m_ElapsedTime += 8;
                if (this.m_ElapsedTime >= 24)
                {
                    try
                    {
                        this.UpdateSID();
                    }
                    catch (Exception err)
                    {
                        System.Diagnostics.Debug.WriteLine(err.ToString());
                        CurrentError = $"SessionIDの更新に失敗しました。\n{err.ToString()}";
                        //ViewModel.OnModelNotice("SessionIDの更新に失敗しました。\n" + err.ToString());
                    }
                    this.m_ElapsedTime = 0;
                }
                this.m_HoboCheck.Change(3600000 * 8, -1);
            }, null, 3600000 * 8, -1);
        }

        /// <summary>
        /// 保持するSessionIDを更新する
        /// </summary>
        public void UpdateSID()
        {
            HttpWebRequest APIRequest = (HttpWebRequest)WebRequest.Create(this.APIServerURI + "/v1/auth/");
            try
            {
                APIRequest.Proxy = (String.IsNullOrEmpty(ProxyAddress) == false) ? (new WebProxy(ProxyAddress)) : (null);
            }
            catch (UriFormatException)
            {
                APIRequest.Proxy = null;
            }
            APIRequest.Method = "POST";
            APIRequest.UserAgent = this.SidUA;
            var res = this.X2chUA.Split(':');
            APIRequest.Headers.Add("X-2ch-UA", (res.Length == 1) ? (this.X2chUA) : (res[1]));
            APIRequest.Timeout = GetSIDTimeout;
            APIRequest.ContentType = "application/x-www-form-urlencoded";
            APIRequest.ServicePoint.Expect100Continue = false;
            String Value = "ID=" + HttpUtility.UrlEncode(this.RouninID);
            Value += "&PW=" + HttpUtility.UrlEncode(this.RouninPW);
            Value += "&KY=" + HttpUtility.UrlEncode(this.AppKey);
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
                        this.SessionID = ResData.Split(':')[1];
                        if (ResData.Contains("ERROR:") == true || this.SessionID == "")
                        {
                            CurrentError = $"SessionIDの取得に失敗しました。\n{ResData}";
                            //ViewModel.OnModelNotice("SessionIDの取得に失敗しました。\n" + ResData, true);
                            this.SessionID = DefaultSID;
                        }
                    }
                    wres?.Close();
                }
            }
            catch (WebException err)
            {
                if (this.SessionID == "") this.SessionID = DefaultSID;

                System.Diagnostics.Debug.WriteLine(err.ToString());
                CurrentError = $"SessionIDの取得に失敗しました。\n{err.ToString()}";
                //ViewModel.OnModelNotice("SessionIDの取得に失敗しました。\n" + err.ToString());
                err.Response?.Close();
                return;
            }

            foreach (KeyValuePair<string, HoboData> key in this.m_HoboCache)
            {
                String hobo = KeyGen("/v1/" + key.Key + m_SID + this.AppKey);
                this.m_HoboCache[key.Key].Hobo = Encoding.ASCII.GetBytes("sid=" + m_SID + "&hobo=" + hobo + "&appkey=" + this.AppKey);
            }
            this.m_HoboCheck.Change(3600000 * 8, -1);
            this.m_ElapsedTime = 0;
        }

        /// <summary>
        /// datを取得する
        /// </summary>
        /// <param name="saba">サーバ名</param>
        /// <param name="ita">板名</param>
        /// <param name="thread">スレッドID</param>
        /// <param name="range">取得済みのdat長</param>
        /// <param name="lastmod">既取得分の最終更新日時</param>
        /// <returns>アクセス結果のHttpWebResponse</returns>
        public HttpWebResponse GetDat(String saba, String ita, String thread, int range, String lastmod)
        {
            String hkey = saba + "/" + ita + "/" + thread;

            HttpWebRequest datRequest = (HttpWebRequest)WebRequest.Create(this.APIServerURI + "/v1/" + hkey);
            datRequest.Method = "POST";
            datRequest.UserAgent = this.DatUA;
            datRequest.KeepAlive = true;
            datRequest.ContentType = "application/x-www-form-urlencoded";
            datRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            datRequest.ServicePoint.Expect100Continue = false;
            datRequest.Timeout = GetDatTimeout;

            try
            {
                if (String.IsNullOrEmpty(ProxyAddress) == false) datRequest.Proxy = new WebProxy(ProxyAddress);
            }
            catch (UriFormatException)
            {
                datRequest.Proxy = null;
            }

            if (-1 < range)
            {
                if (DateTime.TryParse(lastmod, out DateTime ifModifiedSince) == false)
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
            datRequest.ContentLength = m_HoboCache[hkey].Hobo.Length;

            try
            {
                using (Stream PostStream = datRequest.GetRequestStream())
                {
                    PostStream.Write(m_HoboCache[hkey].Hobo, 0, m_HoboCache[hkey].Hobo.Length);
                    return (HttpWebResponse)datRequest.GetResponse();
                }
            }
            catch (WebException err)
            {
                return (HttpWebResponse)err.Response;
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
