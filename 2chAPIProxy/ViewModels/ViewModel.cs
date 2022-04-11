using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Xml.Serialization;
using System.Timers;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;

using YamlDotNet;

using _2chAPIProxy.ViewModels;
using System.Net;

namespace _2chAPIProxy
{
    public class SendObject : MarshalByRefObject
    {
        public bool data { get; set; }
        public bool first = true;
        public delegate void CallEventHandler(bool e);
        public static event CallEventHandler OnTrance;

        public void SendMessage(bool val) => OnTrance?.BeginInvoke(val, (e) => OnTrance(val), null);

        public override object InitializeLifetimeService() => null;
    }

    public class ViewModel : ViewModelImpl<Models.ModelFuctory> { }

    //ビューモデルの実装
    public class ViewModelImpl<ModelFuctory> : VMBase where ModelFuctory : Models.IModelFuctory, new()
    {
        //設定保持と参照用
        public static AppSetting Setting { get; set; }

        //專ブラ同時起動を行ったかどうかのフラグ
        bool m_SyncStart = false;
        //同時起動した専ブラのプロセスID
        int m_SenburaPID = -1;

        //プロセス間通信用オブジェクト保持
        SendObject RemoteObject { get; set; }
        //タスクトレイアイコン保持
        public System.Windows.Forms.NotifyIcon TaskTrayIcon { get; set; }
        //再開動作中のステータス、0:通常、1:再開中、2以上:SID取得エラーでループ中
        public static ushort RestartStatus { get; set; } = 0;
        //プロクシ動作クラス
        public DatProxy DatProxy { get; private set; }

        public ViewModelImpl()
        {
            Setting = App.Setting;
            ModelNotice += t => SystemLog = t;
            //各プロパティの初期化
            _NowStart = false;
            _AutoStart = Setting.AutoStart;
            _AutoSelect = Setting.AutoSelect;
            //_ChangeUARetry = Setting.ChangeUARetry;
            _PostNoReplace = Setting.PostNoReplace;
            _Socks4aProxy = Setting.Socks4aProxy;
            _gZipResponse = Setting.gZipResponse;
            _KakotoHTML = Setting.KakotoHTML;
            _OfflawRokkaPermutation = Setting.OfflawRokkaPermutation;
            _AllReturn = Setting.AllReturn;
            _OnlyORPerm = Setting.OnlyORPerm;
            _ChunkedResponse = Setting.ChunkedResponse;
            _SyncEnd = Setting.SyncEnd;
            _WANAccess = Setting.WANAccess;
            _duplication = Setting.duplication;
            _ShowWindow = Setting.ShowWindow;
            _ClosetoMin = Setting.ClosetoMin;
            _CRReplace = Setting.CRReplace;
            _SkipAliveCheck = Setting.SkipAliveCheck;
            _KakolinkPermutation = Setting.KakolinkPermutation;
            _AllUAReplace = Setting.AllUAReplace;
            _BeLogin = Setting.BeLogin;
            _UseTLSWrite = Setting.UseTLSWrite;
            _PostRoninInvalid = Setting.PostRoninInvalid;
            _Use5chnet = Setting.Use5chnet;
            _Replace5chURI = Setting.Replace5chURI;
            _ReplaceHttpsLink = Setting.ReplaceHttpsLink;
            _SetReferrer = Setting.SetReferrer;
            _PortNumber = Setting.PortNumber;
            _Appkey = Setting.Appkey;
            _HMkey = Setting.HMkey;
            _UserAgent0 = Setting.UserAgent0;
            _UserAgent1 = Setting.UserAgent1;
            _UserAgent2 = Setting.UserAgent2;
            _UserAgent3 = Setting.UserAgent3;
            _UserAgent4 = Setting.UserAgent4;
            try
            {
                _RouninID = (Setting.RouninID != "") ? (Setting.CryptData(Setting.RouninID, false)) : ("");
                _RouninPW = (Setting.RouninPW != "") ? (Setting.CryptData(Setting.RouninPW, false)) : ("");
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                _RouninID = "";
                _RouninPW = "";
                SystemLog = "浪人ID/PWの復号に失敗しました。再設定してください";
            }
            _ProxyAddress = Setting.ProxyAddress;
            _SenburaPath = Setting.SenburaPath;
            _WANID = Setting.WANID;
            _WANPW = Setting.WANPW;
            _CEExternalRead = Setting.CEExternalRead;
            _CESrcfilePath = Setting.CESrcfilePath;
            _CEResultView = "ここにコンパイルとテストの結果が表示されます";
            enablePostv2 = Setting.EnablePostv2;
            enablePostv2onPink = Setting.EnablePostv2onPink;
            enableUTF8Post = Setting.EnableUTF8Post;
            postFieldOrder = Setting.PostFieldOrder;

            //スリープ/休止状態時の処理
            Microsoft.Win32.SystemEvents.PowerModeChanged += new Microsoft.Win32.PowerModeChangedEventHandler(PowermodeChanged);
        }

        public void Init()
        {
            var fuctory = new ModelFuctory();

            //プロクシ処理用クラス初期化
            try
            {
                DatProxy = new DatProxy(Appkey, HMkey, UserAgent1, UserAgent0, UserAgent2, RouninID, RouninPW, ProxyAddress)
                {
                    APIMediator = fuctory.CreateAPIMediator(),
                    HtmlConverter = fuctory.CreateHtmlConverter()
                };
            }
            catch (System.IO.FileLoadException)
            {
                using (TaskTrayIcon)
                {
                    System.Windows.MessageBox.Show("FiddlerCore4.dllのバージョンが違うか必要なdllが無いようです。zipファイルに同梱のすべてのdllを上書きコピーしてください。", "dll読み込みエラー");
                    Microsoft.Win32.SystemEvents.PowerModeChanged -= new Microsoft.Win32.PowerModeChangedEventHandler(PowermodeChanged);
                    System.Windows.Application.Current.Shutdown();
                    return;
                }
            }
            //書き込み板毎設定の読み込み
            if (File.Exists("./BoardSettings.yaml"))
            {
                using (var stream = File.OpenText("./BoardSettings.yaml"))
                {
                    try
                    {
                        var deserializer = new YamlDotNet.Serialization.Deserializer();
                        DatProxy.BoardSettings = deserializer.Deserialize<Dictionary<string, BoardSettings>>(stream);
                    }
                    catch (Exception err)
                    {
                        this.SystemLog = "YAMLファイルの書式が間違っているようです。\n" + err.ToString();
                    }
                }
            }
            DatProxy.BoardSettings ??= new Dictionary<string, BoardSettings>();
            this.SystemLog = $"{DatProxy.BoardSettings.Count()}板分の設定を読み込みました。";
            if (DatProxy.BoardSettings.ContainsKey("2chapiproxy_default") == false)
            {
                // ファイルが無いかデフォルト設定が無い時、JaneStyleの設定を使用
                var def = new BoardSettings { UserAgent = "Monazilla/1.00 JaneStyle/4.22 Windows/10.0.22000", SetOekaki = false, KeepAlive = false };
                def.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                def.Headers.Add("Accept-Encoding", "gzip, identity");
                def.Headers.Add("ContentType", "application/x-www-form-urlencoded");
                DatProxy.BoardSettings.Add("2chapiproxy_default", def);
                this.SystemLog = "書き込みのデフォルト設定としてJaneStyleのものを使用します";
            }

            //外部コードのコンパイル
            if (CEExternalRead) CEResultView = DatProxy.HtmlConverter.Compile(CESrcfilePath);

            //設定の適用、プロクシクラス
            DatProxy.AllowWANAccese = WANAccess;
            DatProxy.user = WANID;
            DatProxy.pw = WANPW;
            DatProxy.WriteUA = UserAgent3;
            DatProxy.GetHTML = KakotoHTML;
            DatProxy.OfflawRokkaPerm = OfflawRokkaPermutation;
            DatProxy.CangeUARetry = ChangeUARetry;
            DatProxy.SocksPoxy = Socks4aProxy;
            DatProxy.gZipRes = gZipResponse;
            DatProxy.ChunkRes = ChunkedResponse;
            DatProxy.OnlyORPerm = OnlyORPerm;
            DatProxy.CRReplace = CRReplace;
            DatProxy.KakolinkPerm = KakolinkPermutation;
            DatProxy.AllUAReplace = (UserAgent3 == "") ? (false) : (AllUAReplace);
            DatProxy.BeLogin = BeLogin;
            DatProxy.SetReferrer = SetReferrer;
            DatProxy.EnablePostv2 = EnablePostv2;
            DatProxy.EnablePostv2onPink = EnablePostv2onPink;
            DatProxy.EnableUTF8Post = EnableUTF8Post;
            DatProxy.PostFieldOrder = PostFieldOrder;

            //設定の適用、APIアクセスクラス
            DatProxy.APIMediator.AppKey = this.Appkey;
            DatProxy.APIMediator.HMKey = this.HMkey;
            DatProxy.APIMediator.SidUA = this.UserAgent0;
            DatProxy.APIMediator.X2chUA = this.UserAgent1;
            DatProxy.APIMediator.DatUA = this.UserAgent2;
            DatProxy.APIMediator.RouninID = this.RouninID;
            DatProxy.APIMediator.RouninPW = this.RouninPW;
            DatProxy.APIMediator.ProxyAddress = this.ProxyAddress;
            DatProxy.UpdateAsync();

            //設定の適用、html変換クラス
            DatProxy.HtmlConverter.UserAgent = _UserAgent4;
            DatProxy.HtmlConverter.ProxyAddress = _ProxyAddress;
            DatProxy.HtmlConverter.IsDifferenceDetect = !_AllReturn;
            DatProxy.HtmlConverter.IsAliveCheckSkip = _SkipAliveCheck;
            DatProxy.HtmlConverter.Is5chURIReplace = _Replace5chURI;
            DatProxy.HtmlConverter.IsHttpsReplace = _ReplaceHttpsLink;
            DatProxy.HtmlConverter.IsExternalConverterUse = _CEExternalRead;

            //エラー通知用コールバック登録
            DatProxy.APIMediator.PropertyChanged += (sender, e) =>
          {
              if (e.PropertyName == nameof(DatProxy.APIMediator.CurrentError))
              {
                  //ここで取得しておく
                  string error = DatProxy.APIMediator.CurrentError;

                  App.Current.Dispatcher.BeginInvoke((Action)(() =>
                  {
                      this.SystemLog = error;
                  }));
              }
          };

            DatProxy.HtmlConverter.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(DatProxy.HtmlConverter.CurrentError))
                {
                    //ここで取得しておく
                    string error = DatProxy.HtmlConverter.CurrentError;

                    App.Current.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        this.SystemLog = error;
                    }));
                }
            };

            //自動開始処理
            if (Setting.AutoStart)
            {
                int pnum = (Setting.AutoSelect) ? (0) : (Setting.PortNumber);
                pnum = DatProxy.Start(pnum);
                if (pnum != 0)
                {
                    SystemLog = "開始、ポート番号：" + pnum;
                    NowStart = true;
                    PortNumber = pnum;
                }
                else
                {
                    SystemLog = "既に使用中のポートみたいです、別のポートを指定して下さい。";
                    DatProxy.End();
                }
            }
            //同時起動と終了を設定
            if (!String.IsNullOrEmpty(Setting.SenburaPath))
            {
                bool kage = true;
                System.Diagnostics.Process p = new System.Diagnostics.Process();
                p.StartInfo.FileName = Setting.SenburaPath;
                if (System.IO.Path.GetFileName(Setting.SenburaPath) != "kage.exe")
                {
                    kage = false;
                    p.EnableRaisingEvents = true;
                    p.Exited += (sender, e) =>
                    {
                        App.Current.Dispatcher.BeginInvoke((Action)(() => { if (Setting.SyncEnd) BeforeShutdown(); }), null);
                    };
                }
                try
                {
                    p.Start();
                    m_SenburaPID = p.Id;
                    SystemLog = Setting.SenburaPath + " を起動";
                    m_SyncStart = true;
                    //かちゅ～しゃの場合の終了同期処理の追加
                    if (kage)
                    {
                        System.Diagnostics.Process[] plist = null;
                        for (int i = 0; i < 50; ++i)
                        {
                            plist = System.Diagnostics.Process.GetProcessesByName("Katjusha");
                            if (plist.Length != 0) break;
                            System.Threading.Thread.Sleep(100);
                        }
                        plist[0].EnableRaisingEvents = true;
                        plist[0].Exited += (sender, e) =>
                        {
                            App.Current.Dispatcher.BeginInvoke((Action)(() => { if (Setting.SyncEnd) BeforeShutdown(); }), null);
                        };
                        m_SenburaPID = plist[0].Id;
                    }
                }
                catch (Exception err)
                {
                    SystemLog = Setting.SenburaPath + " の起動に失敗\n" + err.ToString();
                }
            }
            Setting.change = false;
            //プロセス間通信のサーバ登録
            try
            {
                IpcServerChannel server = new IpcServerChannel("2chApiProxyIPC");
                ChannelServices.RegisterChannel(server, true);
                RemoteObject = new SendObject();
                SendObject.OnTrance += new SendObject.CallEventHandler((e) =>
                {
                    if (RemoteObject.first)
                    {
                        RemoteObject.first = false;
                        App.Current.Dispatcher.BeginInvoke((Action)(() => { if (e) ActivateWindow(); }));
                    }
                    else RemoteObject.first = true;
                });
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(SendObject), "ShowWindow", WellKnownObjectMode.Singleton);
            }
            catch (Exception) { }
            SystemLog = "2chAPIProxy起動";
        }

        private void HtmlConverter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void BeforeShutdown()
        {
            using (TaskTrayIcon)
            {
                //test.ServerClose();
                if (DatProxy != null) DatProxy.End();
                Microsoft.Win32.SystemEvents.PowerModeChanged -= new Microsoft.Win32.PowerModeChangedEventHandler(PowermodeChanged);
                if (Setting.SyncEnd && m_SyncStart)
                {
                    try
                    {
                        var SenburaProcess = System.Diagnostics.Process.GetProcessById(m_SenburaPID);
                        try
                        {
                            //PIDが使いまわされている可能性を考慮、実行ファイル名をチェック
                            String SenburaName = System.IO.Path.GetFileName(Setting.SenburaPath);
                            SenburaName = SenburaName.Replace("kage.exe", "Katjusha.exe");
                            if (System.IO.Path.GetFileName(SenburaProcess.MainModule.FileName) == SenburaName) SenburaProcess.CloseMainWindow();
                        }
                        catch (System.ComponentModel.Win32Exception) { }
                    }
                    catch (ArgumentException) { }
                }
                if (Setting.change)
                {
                    XmlSerializer xser = new XmlSerializer(typeof(AppSetting));
                    using (StreamWriter sw = new StreamWriter("./settings.xml", false, Encoding.UTF8))
                    {
                        xser.Serialize(sw, Setting);
                    }
                }
                System.Windows.Application.Current.Shutdown();
            }
        }

        void PowermodeChanged(Object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            try
            {
                if (e.Mode == Microsoft.Win32.PowerModes.Resume && NowStart)
                {
                    DatProxy.Start(Setting.PortNumber);
                    SystemLog = "再開、ポート番号：" + Setting.PortNumber;
                }
                else if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
                {
                    DatProxy.End();
                }
            }
            catch (Exception err)
            {
                SystemLog = err.ToString();
            }
        }

        //以下バインディングプロパティ
        bool _NowStart;
        public bool NowStart
        {
            get { return _NowStart; }
            set
            {
                if (_NowStart != value)
                {
                    _NowStart = value;
                    NoticePropertyChanged("NowStart");
                }
            }
        }

        bool _AutoStart;
        public bool AutoStart
        {
            get { return _AutoStart; }
            set
            {
                if (_AutoStart != value)
                {
                    Setting.AutoStart = _AutoStart = value;
                    NoticePropertyChanged("AutoStart");
                }
            }
        }

        bool _AutoSelect;
        public bool AutoSelect
        {
            get { return _AutoSelect; }
            set
            {
                if (_AutoSelect != value)
                {
                    Setting.AutoSelect = _AutoSelect = value;
                    NoticePropertyChanged("AutoSelect");
                }
            }
        }

        bool _ChangeUARetry;
        public bool ChangeUARetry
        {
            get { return _ChangeUARetry; }
            set
            {
                if (_ChangeUARetry != value)
                {
                    DatProxy.CangeUARetry = Setting.ChangeUARetry = _ChangeUARetry = value;
                    NoticePropertyChanged("ChangeUARetry");
                }
            }
        }

        bool _Socks4aProxy;
        public bool Socks4aProxy
        {
            get { return _Socks4aProxy; }
            set
            {
                if (_Socks4aProxy != value)
                {
                    DatProxy.SocksPoxy = Setting.Socks4aProxy = _Socks4aProxy = value;
                    NoticePropertyChanged("Socks4aProxy");
                }
            }
        }

        bool _gZipResponse;
        public bool gZipResponse
        {
            get { return _gZipResponse; }
            set
            {
                if (_gZipResponse != value)
                {
                    DatProxy.gZipRes = Setting.gZipResponse = _gZipResponse = value;
                    NoticePropertyChanged("gZipResponse");
                }
            }
        }

        bool _KakotoHTML;
        public bool KakotoHTML
        {
            get { return _KakotoHTML; }
            set
            {
                if (_KakotoHTML != value)
                {
                    DatProxy.GetHTML = Setting.KakotoHTML = _KakotoHTML = value;
                    NoticePropertyChanged("KakotoHTML");
                }
            }
        }

        bool _OfflawRokkaPermutation;
        public bool OfflawRokkaPermutation
        {
            get { return _OfflawRokkaPermutation; }
            set
            {
                if (_OfflawRokkaPermutation != value)
                {
                    DatProxy.OfflawRokkaPerm = Setting.OfflawRokkaPermutation = _OfflawRokkaPermutation = value;
                    if (!value) DatProxy.OnlyORPerm = false;
                    else DatProxy.OnlyORPerm = OnlyORPerm;
                    NoticePropertyChanged("OfflawRokkaPermutation");
                }
            }
        }

        bool _AllReturn;
        public bool AllReturn
        {
            get { return _AllReturn; }
            set
            {
                if (_AllReturn != value)
                {
                    Setting.AllReturn = _AllReturn = value;
                    DatProxy.HtmlConverter.IsDifferenceDetect = !value;
                    NoticePropertyChanged("AllReturn");
                }
            }
        }

        bool _OnlyORPerm;
        public bool OnlyORPerm
        {
            get { return _OnlyORPerm; }
            set
            {
                if (_OnlyORPerm != value)
                {
                    DatProxy.OnlyORPerm = Setting.OnlyORPerm = _OnlyORPerm = value;
                    NoticePropertyChanged("OnlyORPerm");
                }
            }
        }


        bool _ChunkedResponse;
        public bool ChunkedResponse
        {
            get { return _ChunkedResponse; }
            set
            {
                if (_ChunkedResponse != value)
                {
                    DatProxy.ChunkRes = Setting.ChunkedResponse = _ChunkedResponse = value;
                    NoticePropertyChanged("ChunkedResponse");
                }
            }
        }

        bool _SyncEnd;
        public bool SyncEnd
        {
            get { return _SyncEnd; }
            set
            {
                if (_SyncEnd != value)
                {
                    Setting.SyncEnd = _SyncEnd = value;
                    NoticePropertyChanged("SyncEnd");
                }
            }
        }

        bool _WANAccess;
        public bool WANAccess
        {
            get { return _WANAccess; }
            set
            {
                if (_WANAccess != value)
                {
                    DatProxy.AllowWANAccese = Setting.WANAccess = _WANAccess = value;
                    NoticePropertyChanged("WANAccess");
                }
            }
        }

        bool _duplication;
        public bool duplication
        {
            get { return _duplication; }
            set
            {
                if (_duplication != value)
                {
                    Setting.duplication = _duplication = value;
                    NoticePropertyChanged("duplication");
                }
            }
        }

        bool _ShowWindow;
        public bool ShowWindow
        {
            get { return _ShowWindow; }
            set
            {
                if (_ShowWindow != value)
                {
                    Setting.ShowWindow = _ShowWindow = value;
                    NoticePropertyChanged("ShowWindow");
                }
            }
        }

        bool _ClosetoMin;
        public bool ClosetoMin
        {
            get { return _ClosetoMin; }
            set
            {
                if (_ClosetoMin != value)
                {
                    Setting.ClosetoMin = _ClosetoMin = value;
                    NoticePropertyChanged("ClosetoMin");
                }
            }
        }

        bool _CRReplace;
        public bool CRReplace
        {
            get { return _CRReplace; }
            set
            {
                if (_CRReplace != value)
                {
                    DatProxy.CRReplace = Setting.CRReplace = _CRReplace = value;
                    NoticePropertyChanged("CRReplace");
                }
            }
        }

        bool _CEExternalRead;
        public bool CEExternalRead
        {
            get { return _CEExternalRead; }
            set
            {
                if (_CEExternalRead != value)
                {
                    Setting.CEExternalRead = _CEExternalRead = DatProxy.HtmlConverter.IsExternalConverterUse = value;
                    NoticePropertyChanged("CEExternalRead");
                }
            }
        }

        bool _SkipAliveCheck;
        public bool SkipAliveCheck
        {
            get { return _SkipAliveCheck; }
            set
            {
                if (_SkipAliveCheck != value)
                {
                    Setting.SkipAliveCheck = _SkipAliveCheck = DatProxy.HtmlConverter.IsAliveCheckSkip = value;
                    NoticePropertyChanged("SkipAliveCheck");
                }
            }
        }

        bool _KakolinkPermutation;
        public bool KakolinkPermutation
        {
            get { return _KakolinkPermutation; }
            set
            {
                if (_KakolinkPermutation != value)
                {
                    DatProxy.KakolinkPerm = Setting.KakolinkPermutation = _KakolinkPermutation = value;
                    NoticePropertyChanged("KakolinkPermutation");
                }
            }
        }

        bool _AllUAReplace;
        public bool AllUAReplace
        {
            get { return _AllUAReplace; }
            set
            {
                if (_AllUAReplace != value)
                {
                    DatProxy.AllUAReplace = Setting.AllUAReplace = _AllUAReplace = value;
                    NoticePropertyChanged("AllUAReplace");
                }
            }
        }

        bool _BeLogin;
        public bool BeLogin
        {
            get { return _BeLogin; }
            set
            {
                if (_BeLogin != value)
                {
                    DatProxy.BeLogin = Setting.BeLogin = _BeLogin = value;
                    NoticePropertyChanged("BeLogin");
                }
            }
        }

        bool _UseTLSWrite;
        public bool UseTLSWrite
        {
            get { return _UseTLSWrite; }
            set
            {
                if (_UseTLSWrite != value)
                {
                    Setting.UseTLSWrite = _UseTLSWrite = value;
                    NoticePropertyChanged("UseTLSWrite");
                }
            }
        }

        bool _PostRoninInvalid;
        public bool PostRoninInvalid
        {
            get { return _PostRoninInvalid; }
            set
            {
                if (_PostRoninInvalid != value)
                {
                    Setting.PostRoninInvalid = _PostRoninInvalid = value;
                    NoticePropertyChanged("PostRoninInvalid");
                }
            }
        }
        bool _Use5chnet;
        public bool Use5chnet
        {
            get { return _Use5chnet; }
            set
            {
                if (_Use5chnet != value)
                {
                    Setting.Use5chnet = _Use5chnet = value;
                    //this.prox2.Update();
                    NoticePropertyChanged("Use5chnet");
                }
            }
        }

        bool _Replace5chURI;
        public bool Replace5chURI
        {
            get { return _Replace5chURI; }
            set
            {
                if (_Replace5chURI != value)
                {
                    Setting.Replace5chURI = _Replace5chURI = DatProxy.HtmlConverter.Is5chURIReplace = value;
                    NoticePropertyChanged("Replace5chURI");
                }
            }
        }

        bool _ReplaceHttpsLink;
        public bool ReplaceHttpsLink
        {
            get { return _ReplaceHttpsLink; }
            set
            {
                if (_ReplaceHttpsLink != value)
                {
                    Setting.ReplaceHttpsLink = _ReplaceHttpsLink = DatProxy.HtmlConverter.IsHttpsReplace = value;
                    NoticePropertyChanged("ReplaceHttpsLink");
                }
            }
        }

        bool _PostNoReplace;
        public bool PostNoReplace
        {
            get { return _PostNoReplace; }
            set
            {
                if (_PostNoReplace != value)
                {
                    Setting.PostNoReplace = _PostNoReplace = value;
                    NoticePropertyChanged("PostNoReplace");
                }
            }
        }

        bool _SetReferrer;
        public bool SetReferrer
        {
            get { return _SetReferrer; }
            set
            {
                if (_SetReferrer != value)
                {
                    Setting.SetReferrer = _SetReferrer = DatProxy.SetReferrer = value;
                    NoticePropertyChanged("SetReferrer");
                }
            }
        }

        //変更保存、ボタンの通知ポップアップ制御
        bool _PopupVisible = false;
        public bool PopupVisible
        {
            get { return _PopupVisible; }
            set
            {
                if (_PopupVisible != value)
                {
                    _PopupVisible = value;
                    NoticePropertyChanged("PopupVisible");
                }
            }
        }

        private bool enablePostv2;

        public bool EnablePostv2
        {
            get => enablePostv2;
            set
            {
                if (enablePostv2 != value)
                {
                    Setting.EnablePostv2 = DatProxy.EnablePostv2 = enablePostv2 = value;
                    NoticePropertyChanged("EnablePostv2");
                }
            }
        }

        private bool enablePostv2onPink;

        public bool EnablePostv2onPink
        {
            get => enablePostv2onPink;
            set
            {
                if (enablePostv2onPink != value)
                {
                    Setting.EnablePostv2onPink = DatProxy.EnablePostv2onPink = enablePostv2onPink = value;
                    NoticePropertyChanged("EnablePostv2onPink");
                }
            }
        }

        private bool enableUTF8Post;

        public bool EnableUTF8Post
        {
            get => enableUTF8Post;
            set
            {
                if (enableUTF8Post != value)
                {
                    Setting.EnableUTF8Post = DatProxy.EnableUTF8Post = enableUTF8Post = value;
                    NoticePropertyChanged("EnableUTF8Post");
                }
            }
        }

        private bool addX2chUAHeader;

        public bool AddX2chUAHeader
        {
            get => addX2chUAHeader;
            set
            {
                if (addX2chUAHeader != value)
                {
                    Setting.AddX2chUAHeader = DatProxy.AddX2chUAHeader = addX2chUAHeader = value;
                    NoticePropertyChanged("AddX2chUAHeader");
                }
            }
        }

        int _PortNumber;
        public int PortNumber
        {
            get { return _PortNumber; }
            set
            {
                if (_PortNumber != value)
                {
                    if (value < 0 || value > 65535) throw new ArgumentException();
                    Setting.PortNumber = _PortNumber = value;
                    NoticePropertyChanged("PortNumber");
                }
            }
        }

        String _Appkey;
        public String Appkey
        {
            get { return _Appkey; }
            set
            {
                if (_Appkey != value)
                {
                    DatProxy.APIMediator.AppKey = Setting.Appkey = _Appkey = value.TrimEnd(' ');
                    NoticePropertyChanged("Appkey");
                }
            }
        }

        String _HMkey;
        public String HMkey
        {
            get { return _HMkey; }
            set
            {
                if (_HMkey != value)
                {
                    DatProxy.APIMediator.HMKey = Setting.HMkey = _HMkey = value.TrimEnd(' ');
                    NoticePropertyChanged("HMkey");
                }
            }
        }

        //SID取得用
        String _UserAgent0;
        public String UserAgent0
        {
            get { return _UserAgent0; }
            set
            {
                if (_UserAgent0 != value)
                {
                    DatProxy.APIMediator.SidUA = Setting.UserAgent0 = _UserAgent0 = value.TrimEnd(' ');
                    NoticePropertyChanged("UserAgent0");
                }
            }
        }

        //SID取得用X-2chUA
        String _UserAgent1;
        public String UserAgent1
        {
            get { return _UserAgent1; }
            set
            {
                if (_UserAgent1 != value)
                {
                    // "X-2ch-UA: JaneStyle/4.0.0"のように入力されている時、最初の"X-2ch-UA"を取り除く
                    var tmp = value.TrimEnd(' ').Split(':');
                    int skip = tmp[0].ToLower().Contains("x-2ch-ua") switch
                    {
                        true => 1,
                        false => 0
                    };

                    string x2chua = "";
                    foreach (var str in tmp.Skip(skip))
                    {
                        x2chua += str;
                    }

                    DatProxy.APIMediator.X2chUA = Setting.UserAgent1 = _UserAgent1 = x2chua;
                    NoticePropertyChanged("UserAgent1");
                }
            }
        }

        //dat取得用
        String _UserAgent2;
        public String UserAgent2
        {
            get { return _UserAgent2; }
            set
            {
                if (_UserAgent2 != value)
                {
                    DatProxy.APIMediator.DatUA = Setting.UserAgent2 = _UserAgent2 = value.TrimEnd(' ');
                    NoticePropertyChanged("UserAgent2");
                }
            }
        }

        //書き込み用
        String _UserAgent3;
        public String UserAgent3
        {
            get { return _UserAgent3; }
            set
            {
                if (_UserAgent3 != value)
                {
                    DatProxy.WriteUA = Setting.UserAgent3 = _UserAgent3 = value.TrimEnd(' ');
                    NoticePropertyChanged("UserAgent3");
                    if (value == "") DatProxy.AllUAReplace = false;
                    else DatProxy.AllUAReplace = AllUAReplace;
                }
            }
        }

        //その他、HTML取得時など
        String _UserAgent4;
        public String UserAgent4
        {
            get { return _UserAgent4; }
            set
            {
                if (_UserAgent4 != value)
                {
                    DatProxy.HtmlConverter.UserAgent = Setting.UserAgent4 = _UserAgent4 = value;
                    NoticePropertyChanged("UserAgent4");
                }
            }
        }

        String _RouninID;
        public String RouninID
        {
            get { return _RouninID; }
            set
            {
                if (_RouninID != value)
                {
                    try
                    {
                        Setting.RouninID = Setting.CryptData(value);
                        _RouninID = value;
                        NoticePropertyChanged("RouninID");
                    }
                    catch (System.Security.Cryptography.CryptographicException)
                    {
                        return;
                    }
                    DatProxy.APIMediator.RouninID = _RouninID = value;
                    NoticePropertyChanged("RouninID");
                }
            }
        }

        String _RouninPW;
        public String RouninPW
        {
            get { return _RouninPW; }
            set
            {
                if (_RouninPW != value)
                {
                    try
                    {
                        Setting.RouninPW = Setting.CryptData(value);
                    }
                    catch (System.Security.Cryptography.CryptographicException)
                    {
                        return;
                    }
                    DatProxy.APIMediator.RouninPW = _RouninPW = value;
                    NoticePropertyChanged("RouninPW");
                }
            }
        }

        String _ProxyAddress;
        public String ProxyAddress
        {
            get { return _ProxyAddress; }
            set
            {
                if (_ProxyAddress != value)
                {
                    if (value == "" || System.Text.RegularExpressions.Regex.IsMatch(value, @"(.+?:\d+|\s+)"))
                    {
                        DatProxy.APIMediator.ProxyAddress = DatProxy.HtmlConverter.ProxyAddress = DatProxy.Proxy = Setting.ProxyAddress = _ProxyAddress = value;
                    }
                    else
                    {
                        throw new ArgumentException();
                    }
                    NoticePropertyChanged("ProxyAddress");
                }
            }
        }

        String _SenburaPath;
        public String SenburaPath
        {
            get { return _SenburaPath; }
            set
            {
                if (_SenburaPath != value)
                {
                    Setting.SenburaPath = _SenburaPath = value;
                    NoticePropertyChanged("SenburaPath");
                }
            }
        }

        String _WANID;
        public String WANID
        {
            get { return _WANID; }
            set
            {
                if (_WANID != value)
                {
                    Setting.WANID = _WANID = value;
                    NoticePropertyChanged("WANID");
                }
            }
        }

        String _WANPW;
        public String WANPW
        {
            get { return _WANPW; }
            set
            {
                if (_WANPW != value)
                {
                    Setting.WANPW = _WANPW = value;
                    NoticePropertyChanged("WANPW");
                }
            }
        }

        String _CESrcfilePath;
        public String CESrcfilePath
        {
            get { return _CESrcfilePath; }
            set
            {
                if (_CESrcfilePath != value)
                {
                    Setting.CESrcfilePath = _CESrcfilePath = value;
                    NoticePropertyChanged("CESrcfilePath");
                }
            }
        }

        String _TestURI;
        public String TestURI
        {
            get { return _TestURI; }
            set
            {
                if (_TestURI != value)
                {
                    _TestURI = value;
                    NoticePropertyChanged("TestURI");
                }
            }
        }

        String _CEResultView;
        public String CEResultView
        {
            get { return _CEResultView; }
            set
            {
                if (_CEResultView != value)
                {
                    _CEResultView = value + "\n" + _CEResultView;
                    NoticePropertyChanged("CEResultView");
                }
            }
        }

        String _SystemLog;
        public String SystemLog
        {
            get { return _SystemLog; }
            set
            {
                _SystemLog = DateTime.Now.ToString() + "\n" + value + "\n" + _SystemLog;
                NoticePropertyChanged("SystemLog");
            }
        }

        private string postFieldOrder;

        public string PostFieldOrder 
        { 
            get => postFieldOrder;
            set
            {
                if (postFieldOrder != value)
                {
                    Setting.PostFieldOrder = DatProxy.PostFieldOrder = postFieldOrder = value;
                    NoticePropertyChanged("PostFieldOrder");
                }
            }
        }


        //ボタンを押した時のイベントハンドラ
        RelayCommand<String> _OnClick;
        public RelayCommand<String> OnClick
        {
            get
            {
                if (_OnClick == null) _OnClick = new RelayCommand<String>((Text) =>
                {
                    switch (Text)
                    {
                        case "Start":
                            if (!NowStart)
                            {
                                int pnum = PortNumber;
                                if (AutoSelect) pnum = 0;
                                pnum = DatProxy.Start(pnum);
                                if (pnum == 0)
                                {
                                    SystemLog = "既に使用中のポートみたいです、別のポートを指定して下さい。";
                                    DatProxy.End();
                                    return;
                                }
                                SystemLog = "開始、ポート番号：" + pnum;
                                PortNumber = pnum;
                                NowStart = true;
                                TaskTrayIcon.Text = "2chApiProxy\n(｀・ω・´)" + pnum + "番で待機中";
                            }
                            else
                            {
                                DatProxy.End();
                                SystemLog = "停止";
                                NowStart = false;
                                TaskTrayIcon.Text = "2chApiProxy\n( ｰωｰ)停止中・・・";
                            }
                            break;
                        case "UpdateKey":
                            this.PopupVisible = false;
                            //DatProxy.Update(Appkey, HMkey, UserAgent1, UserAgent0, UserAgent2, RouninID, RouninPW);
                            DatProxy.UpdateAsync()
                            .ContinueWith(task =>
                            {
                                App.Current.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    if (task.IsFaulted == true)
                                    {
                                        this.SystemLog = task.Exception.ToString();
                                    }
                                    else
                                    {
                                        this.SystemLog = "SessionIDを更新しました。（ユーザー操作）";
                                    }
                                }));
                            });
                            XmlSerializer xser = new XmlSerializer(typeof(AppSetting));
                            using (StreamWriter sw = new StreamWriter("./settings.xml", false, Encoding.UTF8))
                            {
                                xser.Serialize(sw, Setting);
                                Setting.change = false;
                                SystemLog = "現在の設定を保存しました。";
                                this.PopupVisible = true;
                            }
                            // Monakeyをリセット
                            DatProxy.ResetMonakey();
                            break;
                        case "KeyReset":
                            var defaultSetting = new AppSetting();
                            this.Appkey = defaultSetting.Appkey;
                            this.HMkey = defaultSetting.HMkey;
                            this.UserAgent1 = defaultSetting.UserAgent1;
                            this.UserAgent0 = defaultSetting.UserAgent0;
                            this.UserAgent2 = defaultSetting.UserAgent2;
                            this.SystemLog = "各キーとUAをリセットしました。";
                            DatProxy.UpdateAsync()
                            .ContinueWith(task =>
                            {
                                App.Current.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    if (task.IsFaulted == true)
                                    {
                                        this.SystemLog = task.Exception.ToString();
                                    }
                                    else
                                    {
                                        this.SystemLog = "SessionIDを更新しました。（ユーザー操作）";
                                    }
                                }));
                            });
                            break;
                        case "UpdateSID":
                            DatProxy.UpdateAsync()
                            .ContinueWith(task =>
                            {
                                App.Current.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    if (task.IsFaulted == true)
                                    {
                                        this.SystemLog = task.Exception.ToString();
                                    }
                                    else
                                    {
                                        this.SystemLog = "SessionIDを更新しました。（ユーザー操作）";
                                    }
                                }));
                            });
                            break;
                        case "SenburaChoose":
                            System.Windows.Forms.OpenFileDialog GetPath = new System.Windows.Forms.OpenFileDialog();
                            GetPath.Filter = "exeファイル(*.exe;*.EXE)|*.exe;*.EXE|すべてのファイル(*.*)|*.*";
                            GetPath.Title = "同時起動する専ブラを選択";
                            if (GetPath.ShowDialog() == System.Windows.Forms.DialogResult.OK) SenburaPath = GetPath.FileName;
                            break;
                        default:
                            break;
                    }
                });
                return _OnClick;
            }
        }

        RelayCommand _ChooseSrcFile;
        public RelayCommand ChooseSrcFile
        {
            get
            {
                if (_ChooseSrcFile == null) _ChooseSrcFile = new RelayCommand(() =>
                {
                    System.Windows.Forms.OpenFileDialog GetPath = new System.Windows.Forms.OpenFileDialog();
                    GetPath.Filter = "csファイル(*.cs;*.CS)|*.cs;*.CS|vbファイル(*.vb;*.VB)|*.vb;*.VB|すべてのファイル(*.*)|*.*";
                    GetPath.Title = "変換処理の記述されたソースファイルを選択";
                    if (GetPath.ShowDialog() == System.Windows.Forms.DialogResult.OK) CESrcfilePath = GetPath.FileName;
                });
                return _ChooseSrcFile;
            }
        }

        RelayCommand m_Compile = null;
        public RelayCommand Compile
        {
            get
            {
                return m_Compile ?? (m_Compile = new RelayCommand(() =>
                {
                    CEResultView = DatProxy.HtmlConverter.Compile(_CESrcfilePath);
                    if (!String.IsNullOrEmpty(_TestURI))
                    {
                        System.Threading.Timer ConvertTest = null;
                        ConvertTest = new System.Threading.Timer((e) =>
                        {
                            using (ConvertTest)
                            {
                                try
                                {
                                    CEResultView = DatProxy.HtmlConverter.TestExternalConverter(_TestURI);
                                }
                                catch (Exception err)
                                {
                                    CEResultView = err.ToString();
                                }
                            }
                        }, null, 0, System.Threading.Timeout.Infinite);
                    }
                    else
                    {
                        CEResultView = "テスト用URIが指定されていないため変換テストを行いませんでした。";
                    }
                }));
            }
        }

        RelayCommand _SaveSetting;
        public RelayCommand SaveSetting
        {
            get
            {
                if (_SaveSetting == null) _SaveSetting = new RelayCommand(() =>
                {
                    XmlSerializer xser = new XmlSerializer(typeof(AppSetting));
                    using (StreamWriter sw = new StreamWriter("./settings.xml", false, Encoding.UTF8))
                    {
                        xser.Serialize(sw, Setting);
                        Setting.change = false;
                        SystemLog = "現在の設定を保存しました。";
                    }

                    // Monakeyをリセット
                    DatProxy.ResetMonakey();
                });
                return _SaveSetting;
            }
        }

        //モデルからの通知を受け取るハンドラの定義
        public delegate void ModelNoticeHandler(String text);
        public static event ModelNoticeHandler ModelNotice;
        //ウィンドウをアクティブ化する、View側でラムダを入れなければnull
        public static Action ActivateWindow;
        //同期オブジェクト
        private static object m_SyncObj = new object();

        /// <summary>
        /// モデルからの通知を受け取る、
        /// インスタンスがあればNullではないので呼び出し可能
        /// 引数はString型の通知テキスト。
        /// </summary>
        public static void OnModelNotice(String Text, bool ActiveWindow = false)
        {
            lock (m_SyncObj)
            {
                ModelNotice?.Invoke(Text);
            }
            if (ActiveWindow == true) ActivateWindow?.Invoke();
        }

    }

}
