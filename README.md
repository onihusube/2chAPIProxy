# 2chAPIProxy

このアプリケーションについて → http://prokusi.wiki.fc2.com/wiki/2chAPIProxy

## ビルド

- VS2017, 2019
  - 最新のアップデートを使用してください
- [FiddlerCore.dll](https://www.telerik.com/login/v2/download?ReturnUrl=https%3a%2f%2fwww.telerik.com%2fdownload-trial-file%2fv2%2ffiddlercore#register)
  - バージョンはなんでもいいと思います、配布で使用しているのは4.6.2です。

1. FiddlerCore.dllを2chAPIProxyフォルダの中（2chAPIProxy.csprojと同じディレクトリ）に置いてください。
2. VSで2chAPIProxy.slnを開いて「ソリューションのビルド」します
3. `2chAPIProxy\bin\Release`フォルダに2chAPIProxy.exeが出来上がります

### XP対応や旧UI

2chAPIProxyフォルダ内の`MainWindow1.xaml`が旧UIを記述しているファイルです。これを`MainWindow.xaml`と入れ替えることでUIを戻せます。

同様に、`icon3xp.ico`はXP環境のためにサイズを落としたアイコンです。プロジェクトのプロパティからアイコンを変更し、`MainWindow1.xaml`内でアイコン指定している部分も変更することでXP対応できます。

## ライセンス
ソースコードはMITライセンスですが、アイコンは[bbs2chreader](http://bbs2ch.osdn.jp/)からお借りしているのでそちらのライセンスに準じます。
