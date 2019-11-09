using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
//using System.Windows.Forms;
using System.Reflection;
using System.IO;
using System.Xml.Serialization;
using System.Timers;

namespace _2chAPIProxy
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        ViewModel VM;

        public MainWindow()
        {
            InitializeComponent();
            if (!App.Setting.duplication && App.dupli) return;
            ViewModel.ActivateWindow = () =>
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    Show();
                    WindowState = System.Windows.WindowState.Normal;
                    this.ShowInTaskbar = true;
                    this.Activate();
                }));
            };
            //初期化処理を後回し
            this.Dispatcher.BeginInvoke((Action)(() => Init()), System.Windows.Threading.DispatcherPriority.Background, null);
            //ViewModelの初期化とバインド
            VM = new ViewModel();
            this.DataContext = VM;
            if (VM.AutoStart)
            {
                this.WindowState = WindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
            }
            //ウィンドウイベント処理の追加
            this.Closing += (sender, e) =>
            {
                var element = (FrameworkElement)Keyboard.FocusedElement;
                if (element != null) element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                if (VM.ClosetoMin)
                {
                    e.Cancel = true;
                    WindowState = System.Windows.WindowState.Minimized;
                }
                else VM.BeforeShutdown();
            };
            this.StateChanged += (sender, e) =>
            {
                if (WindowState == System.Windows.WindowState.Minimized)
                {
                    this.Hide();
                    ShowInTaskbar = false;
                    VM.TaskTrayIcon.Visible = true;
                }
            };
        }

        void Init()
        {
            //タスクトレイ関連初期化
            VM.TaskTrayIcon = new System.Windows.Forms.NotifyIcon();
            VM.TaskTrayIcon.Text = "2chAPIProxy\n│･ω･`)起動中・・・";
            VM.TaskTrayIcon.Icon = new System.Drawing.Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("_2chAPIProxy.ttray.ico"));
            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            //右クリックからの開始/停止
            System.Windows.Forms.ToolStripMenuItem item = new System.Windows.Forms.ToolStripMenuItem("開始/停止");
            item.Click += (sender, e) =>
            {
                VM.OnClick.Execute("Start");
            };
            item.CheckOnClick = true;
            if (VM.AutoStart) item.Checked = true;
            menu.Items.Add(item);
            //SID更新
            System.Windows.Forms.ToolStripMenuItem item2 = new System.Windows.Forms.ToolStripMenuItem("SID更新");
            item2.Click += (sender, e) =>
            {
                VM.OnClick.Execute("UpdateSID");
            };
            menu.Items.Add(item2);
            //終了
            System.Windows.Forms.ToolStripMenuItem item3 = new System.Windows.Forms.ToolStripMenuItem("終了");
            item3.Click += (sender, e) => 
            {
                this.Activate();
                var element = (FrameworkElement)Keyboard.FocusedElement;
                if (element != null) element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                VM.BeforeShutdown(); 
            };
            menu.Items.Add(item3);
            VM.TaskTrayIcon.ContextMenuStrip = menu;
            VM.TaskTrayIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left && WindowState == System.Windows.WindowState.Minimized)
                {
                    Show();
                    WindowState = System.Windows.WindowState.Normal;
                    this.ShowInTaskbar = true;
                }
                if (e.Button == System.Windows.Forms.MouseButtons.Left) Activate();
            };
            VM.TaskTrayIcon.Visible = true;
            VM.Init();
            if (VM.NowStart) VM.TaskTrayIcon.Text = "2chAPIProxy\n(｀・ω・´)" + portnum.Text + "番で待機中";
            else VM.TaskTrayIcon.Text = "2chAPIProxy\n( ｰωｰ)停止中・・・";
        }
    }

    public class PasswordBoxHelper : DependencyObject
    {
        public static readonly DependencyProperty IsAttachedProperty = DependencyProperty.RegisterAttached(
            "IsAttached",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(false, PasswordBoxHelper.IsAttachedProperty_Changed));

        public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
            "Password",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PasswordBoxHelper.PasswordProperty_Changed));

        public static bool GetIsAttached(DependencyObject dp)
        {
            return (bool)dp.GetValue(PasswordBoxHelper.IsAttachedProperty);
        }

        public static string GetPassword(DependencyObject dp)
        {
            return (string)dp.GetValue(PasswordBoxHelper.PasswordProperty);
        }

        public static void SetIsAttached(DependencyObject dp, bool value)
        {
            dp.SetValue(PasswordBoxHelper.IsAttachedProperty, value);
        }

        public static void SetPassword(DependencyObject dp, string value)
        {
            dp.SetValue(PasswordBoxHelper.PasswordProperty, value);
        }

        private static void IsAttachedProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;

            if ((bool)e.OldValue)
            {
                passwordBox.PasswordChanged -= PasswordBoxHelper.PasswordBox_PasswordChanged;
            }

            if ((bool)e.NewValue)
            {
                passwordBox.PasswordChanged += PasswordBoxHelper.PasswordBox_PasswordChanged;
            }
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            PasswordBoxHelper.SetPassword(passwordBox, passwordBox.Password);
        }

        private static void PasswordProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            var newPassword = (string)e.NewValue;

            if (!GetIsAttached(passwordBox))
            {
                SetIsAttached(passwordBox, true);
            }

            if ((string.IsNullOrEmpty(passwordBox.Password) && string.IsNullOrEmpty(newPassword)) ||
                passwordBox.Password == newPassword)
            {
                return;
            }

            passwordBox.PasswordChanged -= PasswordBoxHelper.PasswordBox_PasswordChanged;
            passwordBox.Password = newPassword;
            passwordBox.PasswordChanged += PasswordBoxHelper.PasswordBox_PasswordChanged;
        }
    }
}