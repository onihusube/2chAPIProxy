using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace _2chAPIProxy.HtmlConverter
{
    /// <summary>
    /// 状態を持たない、クラスに依存する必要のない関数群
    /// </summary>
    public static class CommonMethods
    {

        private static readonly System.Globalization.CultureInfo CultureInfo = new System.Globalization.CultureInfo("ja-jp");

        private static Regex GetResTime = new Regex(@"^.+?<>.*?<>(\d{4}\/\d{2}/\d{2}.{3}\s\d{2}:\d{2}:\d{2}(\.\d{1,3})?)(?:\d{1,})?\s(ID:.+?)?<>", RegexOptions.Compiled);

        /// <summary>
        /// 更新日時/dat容量から差分点を推測する
        /// </summary>
        /// <param name="dat">htmlから変換したdat</param>
        /// <param name="LastMod">dat取得要求のIf-Modified-Sinceヘッダ</param>
        /// <param name="UA">専ブラのUserAgent</param>
        /// <param name="range">dat取得要求のRangeヘッダ</param>
        /// <param name="size">Htmlに書かれているdat容量</param>
        /// <returns>推測された差分dat</returns>
        public static Byte[] DifferenceDetection(Byte[] dat, String LastMod, String UA, int range, int size)
        {
            bool giko = UA.IndexOf("gikoNavi") > -1;
            bool xeno = UA.IndexOf("JaneXeno") > -1;

            //If-Modified-Sinceヘッダが利用可能かで処理を分ける
            if (string.IsNullOrEmpty(LastMod) == false)
            {
                System.Diagnostics.Debug.WriteLine("LastModから位置を推測");

                using (System.IO.MemoryStream stream = new System.IO.MemoryStream(dat))
                using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.GetEncoding("Shift_JIS")))
                {
                    //var Cul = new System.Globalization.CultureInfo("ja-jp");
                    //String[] ParseFormat = { "ddd, d MMM yyyy HH:mm:ss K", "r" };
                    DateTime LastModfied = DateTime.Parse(LastMod, CultureInfo, System.Globalization.DateTimeStyles.AssumeLocal);
                    //if (LastMod.IndexOf("+0900") >= 0) LastModfied = LastModfied.AddHours(9);
                    if (LastMod.Contains("+0900") == true) LastModfied = LastModfied.AddHours(9);
                    DateTime PostTime = DateTime.Now;
                    //Regex GetResTime = new Regex(@"^.+?<>.*?<>(\d{4}\/\d{2}/\d{2}.{3}\s\d{2}:\d{2}:\d{2}(\.\d{1,3})?)(?:\d{1,})?\s(ID:.+?)?<>");
                    String Res = "", bRes = "";
                    int gindex = 0;
                    //ひとつ前のレスの秒単位を記録
                    int beforeSecond = 0;

                    //LastModifiedから差分レスを推測
                    for (String res = reader.ReadLine(); reader.EndOfStream == false; res = reader.ReadLine())
                    {
                        try
                        {
                            beforeSecond = PostTime.Second;
                            PostTime = DateTime.Parse(GetResTime.Match(res).Groups[1].Value, CultureInfo, System.Globalization.DateTimeStyles.AssumeLocal);
                        }
                        catch (Exception)
                        {
                            //一部の板で、ミリ秒単位がｳﾝｺになってる場合への対策
                            try
                            {
                                System.Diagnostics.Debug.WriteLine("ウンコ検出");
                                //分かるところまでを抽出する
                                String unko = Regex.Match(res, @"^.+?<>.*?<>((\d{4}\/\d{2}/\d{2}.{3}\s\d{2}:\d{2}:\d{2})(\.ｳﾝｺ))\s(ID:.+?)?<>").Groups[2].Value;
                                PostTime = DateTime.Parse(unko, CultureInfo, System.Globalization.DateTimeStyles.AssumeLocal);
                            }
                            catch (Exception)
                            {
                                //諦める
                                continue;
                            }
                        }

                        //現在のレスの日時が、LastModfiedよりも後かをチェック（LastModfiedの日時は取得時点の最終レスと同じになる）
                        if (LastModfied.CompareTo(PostTime) <= 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"検出された差分時刻:{PostTime}");

                            //LastModifiedのミリ秒単位の四捨五入に対応する処理
                            //（If-Modified-Since/Last-Modified)ヘッダには秒単位までしかなく、どうやらミリ秒単位で四捨五入されている
                            //LastModfiedの秒とレスから取得した秒が異なる場合に四捨五入が発生していると考えられる
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
                            if (String.IsNullOrEmpty(temp) == false)
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
                System.Diagnostics.Debug.WriteLine("表示容量から推測");

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

        public static String CompileConverterFromSource(String SourceFilePath, ref Assembly assembly)
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
                    assembly = Cres.CompiledAssembly;
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


        static Regex CheckResContent = new Regex(@"((?:h?ttps?|sssp)://\w+?\.)5ch\.net(/test/read\.cgi/\w+?/\d{9,}?|/ico/\w+?\.gif|/[a-zA-Z0-9]+)", RegexOptions.Compiled);
        static Regex CheckHttpsLink  = new Regex(@"(h?ttp)s(://\w+\.(?:(?:2|5)ch.net|bbspink\.com)/)", RegexOptions.Compiled);

        /// <summary>
        /// dat内のhttpsと5ch.net置換を行う
        /// </summary>
        /// <param name="dat">変換/取得したdat</param>
        /// <returns></returns>
        public static String ResContentReplace(String dat, bool is5chURIReplace, bool isHttpsReplace)
        {
            StringBuilder datb = new StringBuilder(dat);

            if (is5chURIReplace)
            {
                var replace = CheckResContent.Matches(dat);
                foreach (Match link in replace)
                {
                    datb.Replace(link.Groups[0].Value, link.Groups[1].Value + "2ch.net" + link.Groups[2].Value);
                }
            }
            if (isHttpsReplace)
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
