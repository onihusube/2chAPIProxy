using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace _2chAPIProxy.APIMediator
{
    /// <summary>
    /// APIアクセスクラスのインターフェース、INotifyPropertyChangedを含む
    /// </summary>
    public interface IAPIMediator : INotifyPropertyChanged
    {
        string SessionID { get; }

        string CurrentError { get; }

        string APIServerURI { get; set; }

        int GetSIDTimeout { get; set; }

        int GetDatTimeout { get; set; }

        string AppKey { get; set; }

        string HMKey { get; set; }

        string X2chUA { get; set; }

        string SidUA { get; set; }

        string DatUA { get; set; }

        string RouninID { get; set; }

        string RouninPW { get; set; }

        string ProxyAddress { get; set; }

        void UpdateSID();

        System.Net.HttpWebResponse GetDat(String saba, String ita, String thread, int range, String lastmod);
    }
}
