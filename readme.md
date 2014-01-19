PeerCastStation
===============
PeerCastStationはPeerCastクローンです。

* http://www.pecastation.org/
* http://github.com/kumaryu/peercaststation/

ビルド方法
==========
PeerCastStation/PeerCastStation.slnをVisualStudio 2010以降で開いて普通にビルドしてください。
VisualStudioにNuGet拡張が入ってた方がいいかもしれませんが無くてもたぶんいけます。

Test～プロジェクト群が開けないと言われますが気にしなくて大丈夫です。

WPF GUIのプロジェクトをなんとか外せばMonoでもxbuildでビルドできます。
MonoDevelopは試してないのでわかりません。

ドキュメントはruby(2.0以降がよさげ)とjekyllを入れてdoc/siteやdoc/helpで`jekyll build`を実行してください。
RUBYOPT環境変数に`-Eutf-8`を付けておかないとエラーが出るかもしれません。

    gem install bundler
    cd doc
    bundle install
    cd help
    RUBYOPT=-Eutf-8 jekyll build

こんな感じ

開発に参加するには
==================
開発はGitHubでやってるので何か要望や問題があればGitHubのIssueにつっこんでください。

ソースいじったらpull requestを送ってください。
pull requestの作法なんかはよくわかってないので適当でいいです。

自分で書き起こしたソースにはライセンスをつけてくれると助かります(ちょっとしたパッチには必要ないです)。
ライセンスはGPLv3互換であればなんでもいいですが、特に考えがなければGPLv3にしてください。

今のところ.NET Frameworkは3.5でやってください。
Monoで使えなさそうなクラスの使用はご遠慮ください(PeerCastStation.WPF.dllのように特定のアセンブリで閉じている場合には可)。
といってもやたら変なクラスを使わない限り動くのであんまり気にしなくてもいいです。

