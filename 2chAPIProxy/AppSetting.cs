using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace _2chAPIProxy
{
    public class AppSetting
    {
        int _PortNumber = 8080;
        bool _AutoStart = false;
        bool _AutoSelect = false;
        bool _SyncEnd = false;
        bool _KakotoHTML = false;
        bool _ChangeUARetry = true;
        bool _duplication = false;
        bool _ClosetoMin = true;
        bool _WANAccess = false;
        bool _Socks4aProxy= false;
        bool _OfflawRokkaPermutation = true;
        bool _gZipResponse = true;
        bool _ChunkedResponse = false;
        bool _ShowWindow = false;
        bool _AllReturn = false;
        bool _OnlyORPerm = false;
        bool _CRReplace = false;
        bool _CEExternalRead = false;
        bool _SkipAliveCheck = false;
        bool _KakolinkPermutation = true;
        bool _AllUAReplace = false;
        bool _BeLogin = false;
        bool _UseTLSWrite = false;
        bool _PostRoninInvalid = false;
        bool _Use5chnet = true;
        bool _Replace5chURI = true;
        bool _ReplaceHttpsLink = false;
        bool _PostNoReplace = false;
        bool _SetReferrer = false;
        String _HMkey = "hO2QHdapzbqbTFOaJgZTKXgT2gWqYS";
        String _Appkey = "JYW2J6wh9z8p8xjGFxO3M2JppGCyjQ";
        String _UserAgent0 = "";
        String _UserAgent1 = "X-2ch-UA: JaneStyle/4.0.0";
        String _UserAgent2 = "Mozilla/4.0 (compatible; JaneStyle/4.0.0)";
        String _UserAgent3 = "Monazilla/1.00 JaneStyle/4.00 Windows/6.1.7601 Service Pack 1";
        String _UserAgent4 = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.140 Safari/537.36 Edge/17.17134";
        String _RouninID = "";
        String _RouninPW = "";
        String _SneburaPath = "";
        String _ProxyAddress = "";
        String _WANID = "test";
        String _WANPW = "testpw";
        String _CESrcfilePath = "";

        public bool change = false;

        public AppSetting()
        {
        }

        public int PortNumber
        {
            get {return _PortNumber;}
            set { _PortNumber = value; change = true; }
        }

        public bool AutoStart
        {
            get { return _AutoStart; }
            set { _AutoStart = value; change = true; }
        }

        public bool AutoSelect
        {
            get { return _AutoSelect; }
            set { _AutoSelect = value; change = true; }
        }

        public bool duplication
        {
            get { return _duplication; }
            set { _duplication = value; change = true; }
        }

        public bool ClosetoMin
        {
            get { return _ClosetoMin; }
            set { _ClosetoMin = value; change = true; }
        }

        public bool WANAccess
        {
            get { return _WANAccess; }
            set { _WANAccess = value; change = true; }
        }

        public bool ChangeUARetry
        {
            get { return _ChangeUARetry; }
            set { _ChangeUARetry = value; change = true; }
        }

        public bool Socks4aProxy
        {
            get { return _Socks4aProxy; }
            set { _Socks4aProxy = value; change = true; }
        }

        public bool OfflawRokkaPermutation
        {
            get { return _OfflawRokkaPermutation; }
            set { _OfflawRokkaPermutation = value; change = true; }
        }

        public bool gZipResponse
        {
            get { return _gZipResponse; }
            set { _gZipResponse = value; change = true; }
        }

        public bool ChunkedResponse
        {
            get { return _ChunkedResponse; }
            set { _ChunkedResponse = value; change = true; }
        }

        public bool ShowWindow
        {
            get { return _ShowWindow; }
            set { _ShowWindow = value; change = true; }
        }

        public bool AllReturn
        {
            get { return _AllReturn; }
            set { _AllReturn = value; change = true; }
        }

        public bool OnlyORPerm
        {
            get { return _OnlyORPerm; }
            set { _OnlyORPerm = value; change = true; }
        }

        public bool CRReplace
        {
            get { return _CRReplace; }
            set { _CRReplace = value; change = true; }
        }

        public bool CEExternalRead
        {
            get { return _CEExternalRead;}
            set { _CEExternalRead = value; change = true; }
        }

        public bool SkipAliveCheck
        {
            get { return _SkipAliveCheck; }
            set { _SkipAliveCheck = value; change = true; }
        }

        public bool KakolinkPermutation
        {
            get { return _KakolinkPermutation; }
            set { _KakolinkPermutation = value; change = true; }
        }

        public bool AllUAReplace
        {
            get { return _AllUAReplace; }
            set { _AllUAReplace = value; change = true; }
        }

        public bool BeLogin
        {
            get { return _BeLogin; }
            set { _BeLogin = value; change = true; }
        }

        public bool UseTLSWrite
        {
            get { return _UseTLSWrite; }
            set { _UseTLSWrite = value; change = true; }
        }

        public bool PostRoninInvalid
        {
            get { return _PostRoninInvalid; }
            set { _PostRoninInvalid = value; change = true; }
        }

        public bool Use5chnet
        {
            get { return _Use5chnet; }
            set { _Use5chnet = value; change = true; }
        }

        public bool ReplaceHttpsLink
        {
            get { return _ReplaceHttpsLink; }
            set { _ReplaceHttpsLink = value; change = true; }
        }

        public bool Replace5chURI
        {
            get { return _Replace5chURI; }
            set { _Replace5chURI = value; change = true; }
        }

        public bool PostNoReplace
        {
            get { return _PostNoReplace; }
            set { _PostNoReplace = value; change = true; }
        }

        public bool SetReferrer
        {
            get { return _SetReferrer; }
            set { _SetReferrer = value; change = true; }
        }

        public String HMkey
        {
            get { return _HMkey; }
            set { _HMkey = value; change = true; }
        }

        public String Appkey
        {
            get { return _Appkey; }
            set { _Appkey = value; change = true; }
        }

        //SID取得時に使う専ブラUA
        public String UserAgent0
        {
            get { return _UserAgent0; }
            set { _UserAgent0 = value; change = true; }
        }

        //SID取得時に使うX-2ch-UA
        public String UserAgent1
        {
            get { return _UserAgent1; }
            set { _UserAgent1 = value; change = true; }
        }

        //dat取得時に使う專ブラUA
        public String UserAgent2
        {
            get { return _UserAgent2; }
            set { _UserAgent2 = value; change = true; }
        }

        //書き込み用UA
        public String UserAgent3
        {
            get { return _UserAgent3; }
            set { _UserAgent3 = value; change = true; }
        }

        //HTML取得時などに使うその他UA
        public String UserAgent4
        {
            get { return _UserAgent4; }
            set { _UserAgent4 = value; change = true; }
        }

        public String SenburaPath
        {
            get { return _SneburaPath; }
            set { _SneburaPath = value; change = true; }
        }

        public String ProxyAddress
        {
            get { return _ProxyAddress; }
            set { _ProxyAddress = value; change = true; }
        }

        public bool SyncEnd
        {
            get { return _SyncEnd; }
            set { _SyncEnd = value; change = true; }
        }

        public bool KakotoHTML
        {
            get { return _KakotoHTML; }
            set { _KakotoHTML = value; change = true; }
        }

        public String RouninID
        {
            get { return _RouninID; }
            set { _RouninID = value; change = true; }
        }

        public String RouninPW
        {
            get { return _RouninPW; }
            set { _RouninPW = value; change = true; }
        }

        public String WANID
        {
            get { return _WANID; }
            set { _WANID = value; change = true; }
        }

        public String WANPW
        {
            get { return _WANPW; }
            set { _WANPW = value; change = true; }
        }

        public String CESrcfilePath
        {
            get { return _CESrcfilePath; }
            set { _CESrcfilePath = value; change = true; }
        }

        public String CryptData(String data, bool encrypt = true)
        {
            if (data == "") return "";
            Byte[] key = new byte[] { 194, 187, 68, 9, 91, 93, 89, 82, 138, 177, 56, 81, 25, 237, 56, 123 }, ByteData, value;
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.BlockSize = aes.KeySize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;

                if (encrypt)
                {
                    aes.GenerateIV();
                    ByteData = Encoding.Unicode.GetBytes(data);
                    using (ICryptoTransform Encrypt = aes.CreateEncryptor())
                    {
                        Byte[] enc = Encrypt.TransformFinalBlock(ByteData, 0, ByteData.Length);
                        value = new byte[16 + enc.Length];
                        Buffer.BlockCopy(aes.IV, 0, value, 0, 16);
                        Buffer.BlockCopy(enc, 0, value, 16, enc.Length);
                        return Convert.ToBase64String(value);
                    }
                }
                else
                {
                    ByteData = Convert.FromBase64String(data);
                    Byte[] iv = new byte[16];
                    Buffer.BlockCopy(ByteData, 0, iv, 0, 16);
                    aes.IV = iv;
                    using (ICryptoTransform Decrypt = aes.CreateDecryptor())
                    {
                        value = Decrypt.TransformFinalBlock(ByteData, 16, ByteData.Length - 16);
                        return Encoding.Unicode.GetString(value);
                    }
                }
            }
        }
    }

    public class BoardSettings
    {
        public string UserAgent { get; set; }

        public bool SetOekaki { get; set; } = false;

        public bool KeepAlive { get; set; } = false;

        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }
}
