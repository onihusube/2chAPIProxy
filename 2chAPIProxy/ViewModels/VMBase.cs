﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Windows.Input;

namespace _2chAPIProxy.ViewModels
{

    public abstract class VMBase : INotifyPropertyChanged
    {
        //バインディングのための各種実装

        //プロパティ名毎のPropertyChangedEventArgsをキャッシュする
        ConcurrentDictionary<string, PropertyChangedEventArgs> PropertyChangedEventArgsCache = new System.Collections.Concurrent.ConcurrentDictionary<string, PropertyChangedEventArgs>(4, 10);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void NoticePropertyChanged(string PropertyName = null)
        {
            //キャッシュにない時だけEventArgsを作成
            if (!PropertyChangedEventArgsCache.ContainsKey(PropertyName)) PropertyChangedEventArgsCache[PropertyName] = new PropertyChangedEventArgs(PropertyName);
            this.PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache[PropertyName]);
        }

        protected bool SetProperty<T>(ref T StorageMember, T Value, String PropertyName = null)
        {
            if (object.Equals(StorageMember, Value)) return false;
            StorageMember = Value;
            this.NoticePropertyChanged(PropertyName);
            return true;
        }
    }


    /// <summary>
    /// その機能を中継することのみを目的とするコマンド
    /// デリゲートを呼び出すことにより、他のオブジェクトに対して呼び出します。
    ///CanExecute メソッドの既定の戻り値は 'true' です。
    /// <see cref="RaiseCanExecuteChanged"/> は、次の場合は必ず呼び出す必要があります。
    /// <see cref="CanExecute"/> は、別の値を返すことが予期されます。
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        /// <summary>
        /// RaiseCanExecuteChanged が呼び出されたときに生成されます。
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// 常に実行可能な新しいコマンドを作成します。
        /// </summary>
        /// <param name="execute">実行ロジック。</param>
        public RelayCommand(Action execute)
            : this(execute, null)
        {
        }

        /// <summary>
        /// 新しいコマンドを作成します。
        /// </summary>
        /// <param name="execute">実行ロジック。</param>
        /// <param name="canExecute">実行ステータス ロジック。</param>
        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException("execute");
            _canExecute = canExecute;
        }

        /// <summary>
        /// 現在の状態でこの <see cref="RelayCommand"/> が実行できるかどうかを判定します。
        /// </summary>
        /// <param name="parameter">
        /// コマンドによって使用されるデータ。コマンドが、データの引き渡しを必要としない場合、このオブジェクトを null に設定できます。
        /// </param>
        /// <returns>このコマンドが実行可能な場合は true、それ以外の場合は false。</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute();
        }

        /// <summary>
        /// 現在のコマンド ターゲットに対して <see cref="RelayCommand"/> を実行します。
        /// </summary>
        /// <param name="parameter">
        /// コマンドによって使用されるデータ。コマンドが、データの引き渡しを必要としない場合、このオブジェクトを null に設定できます。
        /// </param>
        public void Execute(object parameter)
        {
            _execute();
        }

        /// <summary>
        /// <see cref="CanExecuteChanged"/> イベントを発生させるために使用されるメソッド
        /// <see cref="CanExecute"/> の戻り値を表すために
        /// メソッドが変更されました。
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 任意の型の引数を1つ受け付けるRelayCommand
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        /// <summary>
        /// RaiseCanExecuteChanged が呼び出されたときに生成されます。
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// 常に実行可能な新しいコマンドを作成します。
        /// </summary>
        /// <param name="execute">実行ロジック。</param>
        public RelayCommand(Action<T> execute)
            : this(execute, null)
        {
        }

        /// <summary>
        /// 新しいコマンドを作成します。
        /// </summary>
        /// <param name="execute">実行ロジック。</param>
        /// <param name="canExecute">実行ステータス ロジック。</param>
        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException("execute");
            _canExecute = canExecute;
        }

        /// <summary>
        /// 現在の状態でこの <see cref="RelayCommand"/> が実行できるかどうかを判定します。
        /// </summary>
        /// <param name="parameter">
        /// コマンドによって使用されるデータ。コマンドが、データの引き渡しを必要としない場合、このオブジェクトを null に設定できます。
        /// </param>
        /// <returns>このコマンドが実行可能な場合は true、それ以外の場合は false。</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute((T)parameter);
        }

        /// <summary>
        /// 現在のコマンド ターゲットに対して <see cref="RelayCommand"/> を実行します。
        /// </summary>
        /// <param name="parameter">
        /// コマンドによって使用されるデータ。コマンドが、データの引き渡しを必要としない場合、このオブジェクトを null に設定できます。
        /// </param>
        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        /// <summary>
        /// <see cref="CanExecuteChanged"/> イベントを発生させるために使用されるメソッド
        /// <see cref="CanExecute"/> の戻り値を表すために
        /// メソッドが変更されました。
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
