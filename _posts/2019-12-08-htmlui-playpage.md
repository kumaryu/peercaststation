---
layout: blog
title:  "新機能: 視聴専用ページについて"
date:   2019-12-08 23:00:00 +0900
categories: blog
tags:   新機能
---

今日は2.9.0での新機能、視聴専用ページについてのご紹介です。

<!--more-->

視聴専用ページとは
===================
視聴専用ページは従来からあるHTML UIの一部で、Webブラウザで開くものです。

![視聴専用ページのイメージ](/assets/2019-12-08-htmlui-playpage/01.png)

見た目はHTML UIのリレー一覧ページをすっきりさせた感じです。実際、機能としてもそのとおりで、既にリレーしているチャンネルをここから直接視聴できるだけのページです。

何に使うのかというと、このページのアドレスを他人に渡すことで、PeerCastを使えない人でも自分の配信を直接視聴したりリレーしているチャンネルをいっしょに視聴をしたりできる、というのを実現することができます。

HTML UIをフル開放してしまうと新しくリレーを開始したり設定を変更したりもできてしまうようになっていましたが、視聴専用ページは操作許可は不要で、視聴許可のみで開くことができますので、そのあたりの心配なく他人にアドレスを渡すことができます。

使い方
=======
開くだけだったらPeerCastStationを起動して[http://localhost:7144/html/play.html](http://localhost:7144/html/play.html)あたりにアクセスすれば開くことができます。
ただこのアドレスを他人に渡しても外からはアクセスできませんのでご注意ください。

外からの直接視聴許可
----------------
外からアクセスしてもらうには、まず直接視聴許可設定をする必要があります。

### 簡単設定(GUI)

GUIから設定画面を開くと、外からアクセスの項目があります(2.9.0以降)。『視聴』のところをONにしましょう。

![GUIの設定画面(簡単)](/assets/2019-12-08-htmlui-playpage/02.png)

『操作』もONにすると外から全部の操作ができるようになります。自分で使う分にはいいのですが、他人にアドレスを教える場合には『操作』はOFFにしておいた方が安全です。

ポートの開放ができていれば、右に『視聴専用ページURL』というのが出てきます。クリックするとブラウザで開くかクリップボードにコピーか選べますので好きな方を実行してください。たいていはコピーで済むと思います。

### 詳細設定(GUI)

詳細設定の場合は、外からのアクセスに使用したいポートの『WANからの接続を許可』の『視聴』をONにします。『要認証』は既定でONになっているはずですが、ONになっているか念のため確認しておきましょう。

![GUIの設定画面(詳細)](/assets/2019-12-08-htmlui-playpage/03.png)

ポートの開放ができていれば、右に『視聴専用ページURL』というのが出てきます。クリックするとブラウザで開くかクリップボードにコピーか選べますので好きな方を実行してください。たいていはコピーで済むと思います。

### 詳細設定(HTML UI)

HTML UIの場合もGUIの詳細設定と同じです。

![HTML UIの設定画面](/assets/2019-12-08-htmlui-playpage/04.png)

外からのアクセスに使用したいポートの『WANからの接続を許可』の『視聴』をONにします。『要認証』は既定でONになっているはずですが、ONになっているか念のため確認しておきましょう。

2.9.0時点ではHTML UIでは、右に『視聴専用ページURL』が出てきません。リレーページの右下に出てきますのでそちらを使用してください。

![リレーページの右下に視聴専用ページへのリンク](/assets/2019-12-08-htmlui-playpage/05.png)

### 視聴専用ページが開けない!?

設定画面から『視聴専用ページURL』をブラウザで開こうとしても開けない場合がありますが、だいたい正常です。

視聴専用ページURLは外からアクセス用にIPアドレス部分がグローバルアドレスとなっています。
ルーターにもよるのですが、LAN内から自分のグローバルアドレス経由でアクセスしようとすると接続に失敗するルーターが多く、そのために上手く開けない場合がほとんどです。

ブラウザで「接続が拒否されました」や「CONNECTION REFUSED」といったエラーメッセージが出ている場合はこれに該当します。

この場合、外からちゃんとつながるかの確認はLAN内からはできません。モバイル回線を使うなどなんらかの方法で外からアクセスしてみるしかないですね。

外からアクセスしてもらう
----------------
設定画面やHTML UIのリレーページ右下で表示された『視聴専用ページURL』をコピペして渡しましょう。

末尾に`?auth=～`といった文字列が付いているはずですが、これもそのままコピペしてください。これがないと認証を求められます。

設定が上手くいっていればリレーしているチャンネル一覧が表示されます。何もリレーしていない場合は空っぽのページになるので、何か配信するなりリレーしている状態で渡すのが良いでしょう。

再生ボタンを押すとプレイリストがダウンロードされるので、対応したようなプレイヤーで開いてもらいましょう。
VLCとかをおすすめしておきますが、なんでもいいです。

ここで直接視聴が行われた場合、その分リレーと同様に帯域を消費します。帯域には注意しましょう。

注意点
-------
* 2.9.0現在、認証用のトークン(パスワード)は手動で作り直さない限りずっと同じで有効となっています。そのため一度他人に教えたURLはいつまでもアクセス可能となります。不特定多数が見られるような場所にURLを貼らないことをおすすめします。今後のバージョンで一定期間しか使えない認証用トークンとか作れるようにしたい。
* 2.9.0現在では、認証用のトークン(パスワード)がポート毎に1つしか持てないため、外からの操作と視聴を一つのポートで両方許可すると、同じ認証情報でHTML UIの全てにアクセスできるようになっています。外からの操作を許可する場合には、他人に視聴のみアクセスを許可するポートを分けるのをおすすめします。今後のバージョンで認証用情報も分けられるようにしたい。

