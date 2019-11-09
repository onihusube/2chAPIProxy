using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _2chAPIProxy.HtmlConverter
{
    /// <summary>
    /// HTML -> dat 変換クラスのインターフェース、INotifyPropertyChangedを含む
    /// </summary>
    public interface IHtmlConverter : System.ComponentModel.INotifyPropertyChanged
    {
        string CurrentError { get; }

        string UserAgent { get; set; }

        string ProxyAddress { get; set; }

        bool IsExternalConverterUse { get; set; }

        bool IsAliveCheckSkip { get; set; }

        bool IsDifferenceDetect { get; set; }

        bool Is5chURIReplace { get; set; }

        bool IsHttpsReplace { get; set; }

        Byte[] Gethtml(String URI, int range, String UA, bool CRReplace, String LastMod = null);

        String Compile(String SourceFilePath);

        String TestExternalConverter(String URI);

        String ResContentReplace(String dat);
    }
}
