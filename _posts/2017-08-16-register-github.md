---
layout: blog
title:  "解説: GitHubへのアカウント登録からIssue作成まで"
date:   2017-08-16 00:00:00 +0900
categories: blog
tags:   解説
---

ここではGitHubにアカウントを登録して、
PeercastStationのIssueにバグを登録するまでの手順を解説します。

<!--more-->


アカウントの登録手順
------

### GitHubのトップページ

既にアカウントを持っていない場合はGitHubのトップページからアカウントを登録する必要があります。

以下の画像でGitHubのトップページと入力すべき内容を示しています。

![GitHubのトップページ]({{"/images/github_register/github_toppage.png"|relative_url}})

上から順に、ユーザー名(英数字のみ)、(受信できる)メールアドレス、パスワードです。

実際に入力してみると以下のようになります。

![GitHubのトップページに入力してみた]({{"/images/github_register/github_toppage_2.png"|relative_url}})

この状態で*Sign up for GitHub*をクリックすると登録が始まります。

### GitHubのアカウント登録画面

アカウント登録でする操作はボタンを2つクリックしてメールを確認するだけです。

まず、次の画面で*Unlimited public repository for free.*というラジオボタンがチェックされているのを確認して、*Continue*ボタンをクリックします。

![GitHubのアカウント種別選択画面]({{"/images/github_register/github_select_repository.png"|relative_url}})

次にアンケート画面が出ますが、これをスキップします(*skip this step*をクリックします)。

![GitHubのアンケート]({{"/images/github_register/github_question_experience.png"|relative_url}})

最後に登録したメールアドレスへメールアドレスが正しいかを確認するメールが来ていると思います。ここで*Verify email address*をクリックすると、GitHubにメールアドレスが正式に登録されます。

![メールアドレス認証]({{"/images/github_register/github_email_verify.png"|relative_url}})


Issueを登録する
---------------

アカウントを取得したら次はIssueを登録しましょう
(そのためにアカウントを取得したはずですよね？)。

### PeercastStationのリポジトリを探す

左上の検索ボックスに*PeercastStation*と入力して、
リポジトリ(ソースコードとか色々まとまっている場所)を探します。

![リポジトリを検索]({{"/images/github_register/github_type_peercaststation.png"|relative_url}})

Enterキーを押すと色々出てくると思いますが、*kumaryu/peercaststation*が目的のリポジトリです。

![リポジトリの検索結果]({{"/images/github_register/github_searchresult_peercaststation.png"|relative_url}})

なお見つからない場合は[こちらからも行けます](https://github.com/kumaryu/peercaststation)。

### Issueを登録する

![PeercastStationのトップページ]({{"/images/github_register/github_peercaststation_top.png"|relative_url}})

これがPeercastStationリポジトリのトップページです。

この画面の上のほうにある、*Issues*をクリックすることで、現在登録されているIssue(バグとか要望とか)を表示する画面に移ることができます。

![Issue表示画面]({{"/images/github_register/github_issue_page.png"|relative_url}})

この画面で*New Issue*をクリックすることで、新しいIssueを作成する画面に移ることができます。

![Issue作成画面]({{"/images/github_register/github_new_issue.png"|relative_url}})

上段がタイトルを入力する部分、下段が詳細な内容を入力する部分です。

タイトルは一目で見て概要がすぐわかるものが望ましいです(ただし【バグ】とかのマークをわざわざ付ける必要はありません)。詳細な内容についてはあらかじめテンプレートが用意されているので、それに従って入力すると非常に良いでしょう。

なお、Issueを入力するための手引きが[*the guidelines for contributing*](https://github.com/kumaryu/peercaststation/blob/master/CONTRIBUTING.md)というリンクの先から行けるので見ておくとなおよいです。

![Issueのガイドライン]({{"/images/github_register/github_issue_guideline.png"|relative_url}})


まとめ
------

これまででおおよそIssueの作成方法が理解できたでしょうか。
Issueを作成することを怖がらないでください。
間違ったIssueでもあとから修正したりすることができます。

ちなみに、PeercastStationのサイトのリンク切れ等もIssueにすることが出来るので、
気軽にIssueを作成してみてください。
