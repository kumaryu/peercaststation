PeerCastStation
===============
PeerCastStationはPeerCastクローンです。

* http://www.pecastation.org/
* http://github.com/kumaryu/peercaststation/

ビルド方法
==========
PeerCastStation/PeerCastStation.slnをVisualStudio 2022以降で開いて普通にビルドしてください。
.NET 10 SDKが必要です。

ドキュメントはrubyとjekyllを入れてdoc/helpで`jekyll build`を実行してください。

    gem install -N jekyll
    cd doc/help
    jekyll build

こんな感じ

開発に参加するには
==================
開発はGitHubでやってるので何か要望や問題があればGitHubのIssueにつっこんでください。

ソースいじったらpull requestを送ってください。
pull requestの作法なんかはよくわかってないので適当でいいです。

自分で書き起こしたソースにはライセンスをつけてくれると助かります(ちょっとしたパッチには必要ないです)。
ライセンスはGPLv3互換であればなんでもいいですが、特に考えがなければGPLv3にしてください。

今のところ.NETは10でやってください。
Monoで使えなさそうなクラスの使用はご遠慮ください(PeerCastStation.WPF.dllのように特定のアセンブリで閉じている場合には可)。
といってもやたら変なクラスを使わない限り動くのであんまり気にしなくてもいいです。

