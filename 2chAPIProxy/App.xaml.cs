using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Xml.Serialization;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;

namespace _2chAPIProxy
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        public static AppSetting Setting = new AppSetting();
        public static bool dupli = false;
        Mutex Check = new Mutex(false, "2chAPIProxy");
        SendObject RemoteObject { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            RemoteObject = null;
            if (File.Exists("./settings.xml"))
            {
                XmlSerializer xser = new XmlSerializer(typeof(AppSetting));
                using (StreamReader sr = new StreamReader("./settings.xml", System.Text.Encoding.UTF8))
                {
                    try
                    {
                        Setting = (AppSetting)xser.Deserialize(sr);
                    }
                    catch (InvalidOperationException) { }
                }
            }
            if (!Check.WaitOne(0, false) && !Setting.duplication)
            {
                using (Check)
                {
                    dupli = true;
                    if (Setting.ShowWindow)
                    {
                        try
                        {
                            IpcClientChannel client = new IpcClientChannel();
                            ChannelServices.RegisterChannel(client, true);
                            RemoteObject = (SendObject)Activator.GetObject(typeof(SendObject), "ipc://2chApiProxyIPC/ShowWindow");
                            RemoteObject.SendMessage(true);
                        }
                        catch (RemotingException){ }
                    }
                    System.Windows.Application.Current.Shutdown();
                    return;
                }
            }
            this.Exit += (esender, ee) =>
            {
                if (Check != null)
                {
                    using (Check)
                    {
                        try
                        {
                            Check.ReleaseMutex();
                        }
                        catch (ApplicationException)
                        {
                            Check.Close();
                        }
                    }
                }
            };
        }
    }
}
